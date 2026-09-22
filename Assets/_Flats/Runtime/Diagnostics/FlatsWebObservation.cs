#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

// Read-only telemetry. No SendMessage receiver, command dispatch or state setters.
public sealed class FlatsWebObservation : MonoBehaviour
{
    [DllImport("__Internal")] static extern int FlatsObserveEnabled();
    [DllImport("__Internal")] static extern void FlatsObserve(string json);
    [DllImport("__Internal")] static extern void FlatsGameplayState(int playing);
    [DllImport("__Internal")] static extern void FlatsStorageMonitor();
    [Serializable] public class Action { public string name, text, parent; public float x,y; public bool interactable; }
    [Serializable] public class Snapshot
    {
        public string scene,menu,game,build,networkState;
        public int frame,width,height,ammo,volume,errors,players;
        public bool player,focused,zoom,inRoom;
        public Vector3 position,rotation;
        public float vertical,horizontal,listenerVolume;
        public int playingAudio;
        public Action[] actions;
        public string[] texts;
    }
    bool observe; float next; int lastPlaying=-1,errors;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init() { var go=new GameObject("Web input observation"); DontDestroyOnLoad(go); go.AddComponent<FlatsWebObservation>(); }
    void Awake() { FlatsStorageMonitor(); observe=FlatsObserveEnabled()!=0; if(observe)Application.logMessageReceived+=Log; }
    void OnDestroy() { if(observe)Application.logMessageReceived-=Log; }
    void Log(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception)errors++; }
    void Update()
    {
        int playing=Menu.current=="Playing"?1:0;
        if(playing!=lastPlaying) { FlatsGameplayState(playing); lastPlaying=playing; }
        if(!observe||Time.unscaledTime<next)return;
        next=Time.unscaledTime+0.1f;
        var player=Array.Find(FindObjectsByType<FPSController>(FindObjectsSortMode.None),p=>Menu.network!=2||(p.GetComponent<PhotonView>()!=null&&p.GetComponent<PhotonView>().isMine));
        var gun=player==null||player.primaryWeapon==null?null:player.primaryWeapon.GetComponent<Gun>();
        var actions=new List<Action>(); var texts=new List<string>(); int audio=0;
        foreach(var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if(!button.isActiveAndEnabled)continue;
            var canvas=button.GetComponentInParent<Canvas>(); if(canvas==null||!canvas.enabled)continue;
            var rect=(RectTransform)button.transform;
            var pos=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
            string label="";foreach(var t in button.GetComponentsInChildren<Text>())label+=" "+t.text;
            actions.Add(new Action{name=button.name,text=label.Trim(),parent=button.transform.parent.name,x=pos.x,y=Screen.height-pos.y,interactable=button.IsInteractable()});
        }
        foreach(var t in FindObjectsByType<Text>(FindObjectsSortMode.None))if(t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text))texts.Add(t.text);
        foreach(var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))if(a.isPlaying)audio++;
        FlatsObserve(JsonUtility.ToJson(new Snapshot {
            scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,menu=Menu.current,game=Menu.gameState,build=Application.buildGUID,
            frame=Time.frameCount,width=Screen.width,height=Screen.height,player=player!=null,position=player==null?Vector3.zero:player.transform.position,
            rotation=Camera.main==null?Vector3.zero:Camera.main.transform.eulerAngles,ammo=gun==null?-1:gun.currentAmmo,zoom=player!=null&&player.isZoom,
            vertical=Input.GetAxis("Vertical"),horizontal=Input.GetAxis("Horizontal"),focused=Application.isFocused,
            volume=Menu.mySettings==null?-1:Menu.mySettings.sound_all,listenerVolume=AudioListener.volume,playingAudio=audio,
            actions=actions.ToArray(),texts=texts.ToArray(),errors=errors,networkState=PhotonNetwork.connectionStateDetailed.ToString(),inRoom=PhotonNetwork.inRoom,players=PhotonNetwork.playerList.Length
        }));
    }
}
#endif
