using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Opt-in integration driver: invokes real UI events; never fabricates network or game results.
// Evidence from this driver is programmatic standalone testing, not OS-input acceptance.
public class FlatsUiVerificationDriver : MonoBehaviour
{
    [Serializable] private class Command { public string id,action,name,path,text,value; }
    [Serializable] private class Receipt { public string id,status,error,utc; }
    private string directory;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-flats-ui-command-dir");
        if(Array.IndexOf(args,"-flats-verify")<0 || i<0 || i+1>=args.Length)return;
        var go=new GameObject("Flats opt-in UI integration driver");DontDestroyOnLoad(go);
        go.AddComponent<FlatsUiVerificationDriver>().directory=Path.GetFullPath(args[i+1]);
    }
    private IEnumerator Start()
    {
        Directory.CreateDirectory(directory);
        while(true)
        {
            yield return new WaitForSecondsRealtime(0.2f);
            string file=Path.Combine(directory,"command.json");if(!File.Exists(file))continue;
            Command command=null;var receipt=new Receipt { utc=DateTime.UtcNow.ToString("o") };
            try
            {
                command=JsonUtility.FromJson<Command>(File.ReadAllText(file));receipt.id=command.id;
                if(command.action=="click")
                {
                    Button found=null;
                    foreach(var b in FindObjectsOfType<Button>())
                    {
                        if(!b.IsInteractable() || !b.gameObject.activeInHierarchy)continue;
                        if(!string.IsNullOrEmpty(command.name)&&b.name!=command.name)continue;
                        if(!string.IsNullOrEmpty(command.path)&&b.transform.parent.name+"/"+b.name!=command.path)continue;
                        string label="";foreach(var t in b.GetComponentsInChildren<Text>())label+=(label==""?"":" ")+t.text;
                        if(!string.IsNullOrEmpty(command.text)&&label!=command.text)continue;
                        if(found!=null)throw new Exception("Ambiguous UI button");found=b;
                    }
                    if(found==null)throw new Exception("No matching active UI button");
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(found.gameObject);
                    found.onClick.Invoke();
                }
                else if(command.action=="input")
                {
                    InputField found=null;
                    foreach(var input in FindObjectsOfType<InputField>())
                    {
                        if(!input.IsInteractable() || !input.gameObject.activeInHierarchy)continue;
                        if(!string.IsNullOrEmpty(command.name)&&input.name!=command.name)continue;
                        if(!string.IsNullOrEmpty(command.path)&&input.transform.parent.name+"/"+input.name!=command.path)continue;
                        if(found!=null)throw new Exception("Ambiguous input field");found=input;
                    }
                    if(found==null)throw new Exception("No matching input field");found.text=command.value;found.onEndEdit.Invoke(found.text);
                }
                else if(command.action=="control")FlatsVerificationInput.Dispatch(command.value);
                else if(command.action=="protocol-test")
                {
                    var args=Environment.GetCommandLineArgs();
                    if(Array.IndexOf(args,"-flats-protocol-test")<0||Array.IndexOf(args,"-flats-module-settings-dir")<0||!PhotonNetwork.isMasterClient||!PhotonNetwork.inRoom||!PhotonNetwork.room.Name.StartsWith("FLATS-MODS-",StringComparison.Ordinal))
                        throw new Exception("Protocol verification requires an isolated owned test room");
                    object malformed=command.value=="type"?(object)42:"https://untrusted.invalid/not-a-package";
                    // CAS defers the local callback until Cloud accepts the change.
                    // Otherwise immediate local rejection can disconnect before it is sent.
                    var expected=new ExitGames.Client.Photon.Hashtable{{Flats.Modules.SessionModules.Property,PhotonNetwork.room.CustomProperties[Flats.Modules.SessionModules.Property]}};
                    PhotonNetwork.room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{{Flats.Modules.SessionModules.Property,malformed}},expected);
                }
                else throw new Exception("Unsupported UI action");
                receipt.status="DISPATCHED";
            }
            catch(Exception e) { receipt.status="FAIL";receipt.error=e.ToString(); }
            // The receipt only certifies event dispatch; the independent probe checks resulting state.
            string archive=Path.Combine(directory,(command==null?Guid.NewGuid().ToString("N"):command.id)+"-command.json");
            File.Move(file,archive);
            File.WriteAllText(Path.Combine(directory,receipt.id+"-receipt.json"),JsonUtility.ToJson(receipt));
        }
    }
}
