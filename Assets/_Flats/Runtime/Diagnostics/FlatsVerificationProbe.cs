using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class FlatsVerificationProbe : MonoBehaviour {
 [Serializable] public class ModuleObservation {
  public bool requested,active,hudVisible,custom,originalVisible;
  public string style; public float size,renderedSize,previewSize;
  public int hudPresenters,customObjects;
  public string agreement;
  public ExternalObservation[] external;
 }
 [Serializable] public class ExternalObservation { public string id,version,sha256; public bool active,requested; }
 static ModuleObservation ObserveModules() {
  var o=new ModuleObservation();var service=Flats.Modules.BuiltinModules.Instance;if(service==null)return o;
  var r=service.Manager.Installed[0];o.requested=r.Requested;o.active=r.Active;
  var external=new List<ExternalObservation>();
  if(service.Center!=null&&service.Center.Ready){
   o.agreement=service.Center.Agreement();
   foreach(var p in service.Center.Running){
    bool active=false;foreach(var record in service.Manager.Installed)if(record.Manifest.Id==p.manifest.id)active=record.Active;
    external.Add(new ExternalObservation{id=p.manifest.id,version=p.manifest.version,sha256=p.sha256,active=active,requested=p.requested});
   }
  }
  o.external=external.ToArray();
  o.style=service.Crosshair.Settings.style.ToString();o.size=service.Crosshair.Settings.size;
  var presenters=UnityEngine.Object.FindObjectsByType<Flats.UI.LocalCrosshairPresenter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
  o.hudPresenters=presenters.Length;
  foreach(var p in presenters) {
   o.hudVisible=p.gameObject.activeInHierarchy;o.custom=p.IsCustom;
   foreach(var img in p.GetComponentsInChildren<Image>(true))if(img.isActiveAndEnabled)o.originalVisible=true;
   var graphics=p.GetComponentsInChildren<Flats.UI.CrosshairGraphic>(true);o.customObjects+=graphics.Length;
   foreach(var g in graphics)if(g.isActiveAndEnabled)o.renderedSize=g.Diameter*g.transform.lossyScale.x;
  }
  foreach(var page in UnityEngine.Object.FindObjectsByType<ModuleManagementPage>(FindObjectsSortMode.None))
   o.previewSize=page.preview.Diameter*page.preview.transform.lossyScale.x;
  return o;
 }
 [Serializable] public class ActionInfo { public string name, text, path; public float x,y; public bool interactable; }
 [Serializable] public class InputInfo { public string name,text,path; public float x,y; public bool interactable; }
 [Serializable] public class IconInfo { public string name,sprite; public Rect spriteRect; public Vector2 size; }
 [Serializable] public class NetworkActor { public int viewId,ownerId,instanceId,ammo; public string name; public Vector3 position,rotation; public float health,x,y,z; public bool mine; public Vector3[] approachPath; }
 [Serializable] public class NetworkScore { public int actorId,kills,deaths; }
 [Serializable] public class NetworkEvent { public string utc,runId,buildId,kind,room; public int processId,actorId,instanceId,viewId,ownerId,otherViewId; public float health; }
 private static FlatsVerificationProbe active;
 [Serializable] public class ContactObservation {
  public string utc,runId,buildId,colliderName,parentName;public int frame,playerId,colliderId;
  public Vector3 playerPosition,point,normal,moveDirection,boundsCenter,boundsSize;
  public float moveLength,stepOffset,slopeLimit;
 }
 public static void ContactObserved(ControllerColliderHit hit){
  if(active==null)return;
  var c=hit.controller;var obstacle=hit.collider;
  var row=new ContactObservation {utc=DateTime.UtcNow.ToString("o"),runId=active.runId,buildId=active.buildId,
   frame=Time.frameCount,playerId=c.gameObject.GetInstanceID(),colliderId=obstacle.GetInstanceID(),colliderName=obstacle.name,
   parentName=obstacle.transform.parent==null?"":obstacle.transform.parent.name,playerPosition=c.transform.position,
   point=hit.point,normal=hit.normal,moveDirection=hit.moveDirection,moveLength=hit.moveLength,
   boundsCenter=obstacle.bounds.center,boundsSize=obstacle.bounds.size,stepOffset=c.stepOffset,slopeLimit=c.slopeLimit};
  File.AppendAllText(Path.Combine(active.outputDirectory,"controller-contacts.jsonl"),JsonUtility.ToJson(row)+Environment.NewLine);
 }
 public static void NetworkObservation(string kind, GameObject subject, int otherViewId=0) {
  if(active==null)return;
  try {
  var view=subject.GetComponent<PhotonView>();var damage=subject.GetComponent<DamageReceiver>();
  var entry=new NetworkEvent{utc=DateTime.UtcNow.ToString("o"),runId=active.runId,buildId=active.buildId,kind=kind,
   room=PhotonNetwork.room==null?"":PhotonNetwork.room.Name,processId=System.Diagnostics.Process.GetCurrentProcess().Id,
   actorId=PhotonNetwork.player.ID,instanceId=subject.GetInstanceID(),viewId=view==null?0:view.viewID,
   ownerId=view==null||view.owner==null?0:view.owner.ID,otherViewId=otherViewId,health=damage==null?0:damage.hitPoints};
  File.AppendAllText(Path.Combine(active.outputDirectory,"network-events.jsonl"),JsonUtility.ToJson(entry)+Environment.NewLine);
  } catch(IOException) { Debug.LogWarning("FLATS_NETWORK_PROBE_WRITE_FAILED"); }
 }
 [Serializable] public class ActorInfo { public string name,position,path,destination,velocity; public Vector3[] approachPath; public string[] targetDetails; public int id,team,targets; public float health,x,y,z,remaining,speed; public bool zombie,vip,onMesh,hasPath,stopped,patrol,rootMotion; }
 [Serializable] public class GlassInfo { public int id; public string position,material,color; public bool broken,collider; public float x,y,z; }
 [Serializable] public class PickupInfo { public int id,weapon; public Vector3 position; public bool ready,persistent; }
 [Serializable] public class OpticInfo { public string name; public int id; public float fov; public bool enabled; public int width,height; public Vector3 position; public Quaternion rotation; public Matrix4x4 projectionMatrix,worldToCameraMatrix; public float fixedTargetPixelHeight; }
 [Serializable] public class ScopeSurfaceInfo {public string name;public int textureId,cameraId;public Vector3[] screenCorners;public int textureWidth,textureHeight;}
 [Serializable] public class AimGeometryInfo {public string name;public int colliderId;public float distance;public Vector3 point,normal,boundsCenter,boundsSize;}
 [Serializable] public class ScoreInfo { public string name; public int instanceId,kills,deaths,team; }
 [Serializable] public class Snapshot {
  public ModuleObservation modules;
  public string runId,utc,processStartUtc,controller,playerTeam,buildId;
  public int selectedRule,selectedMap,configuredBots,objective,detailedObjective,width,height;
  public bool menuBusy,canOpen,focused;
  public GlassInfo[] glass;
  public int tutorialStep = -1;
  public PickupInfo[] pickups;
  public OpticInfo[] optics;
  public ScopeSurfaceInfo[] scopeSurfaces;
  public AimGeometryInfo aimGeometry;
  public ScoreInfo[] scores;
  public string[] uiText;
  public float playerYaw,playerPitch;
  public float bodyYaw, verticalInput, horizontalInput;
  public Vector3 animatorDelta;
  public float playerHealth;
  public bool controllerGrounded, gameplayGrounded, zoom;
  public Vector3 controllerVelocity;
  public float fieldOfView;
  public string[] roots, cameraEffects;
  public int playerId,weaponId;
  public bool playerZombie,playerVIP;
  public int time,errors,ammo,bots,listeners,playingAudio,rule,processId,kills,deaths,redScore,blueScore;
  public string scene,menu,game,detail,player,selected,inputModule,device;
  public bool offlineRoom;
  public bool photonOffline,inRoom,connected;
  public string photonState,room,region;
  public int photonPlayerCount,photonActorId;
  public InputInfo[] inputs;
  public IconInfo[] icons;
  public NetworkActor[] networkActors;
  public NetworkScore[] networkScores;
  public string configurationSource;
  public bool configurationValid,matchEnded;
  public int matchTime,spectatorCameras;
  public ActionInfo[] actions;
  public ActorInfo[] actors;
  public string[] joysticks;
  public float padLeftX,padLeftY,padRightX,padRightY,padTrigger;
 }
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Init(){
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"-flats-verify")<0)return;
  var go=new GameObject("Flats Verification Probe");DontDestroyOnLoad(go);go.AddComponent<FlatsVerificationProbe>();
 }
 private string runId,outputDirectory,processStartUtc,buildId;
 private static string Arg(string name) { var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0 && i+1<a.Length?a[i+1]:null; }
 private int errors;
 private string lastState="";
 private int screenshot;
 private int stableState;
 private float nextMotionSample;
 private int diagnosticSpikeMs;
 private int diagnosticTargetFps;
 void Awake(){
  runId=Arg("-flats-run-id") ?? Guid.NewGuid().ToString("N");
  outputDirectory=Arg("-flats-evidence-dir") ?? Path.Combine(Application.dataPath,"../verification-runs/"+runId);
  Directory.CreateDirectory(outputDirectory);
  processStartUtc=System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("o");
  buildId=Application.buildGUID;
  active=this;
  int targetFps;
  if(int.TryParse(Arg("-flats-target-fps"),out targetFps) && targetFps>=15 && targetFps<=144) { diagnosticTargetFps=targetFps; QualitySettings.vSyncCount=0; Application.targetFrameRate=targetFps; }
  int.TryParse(Arg("-flats-frame-spike-ms"),out diagnosticSpikeMs);
  diagnosticSpikeMs=Mathf.Clamp(diagnosticSpikeMs,0,250);
 Application.logMessageReceived += OnLog; Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None); Application.SetStackTraceLogType(LogType.Error,StackTraceLogType.Full); }
 void OnDestroy(){ Application.logMessageReceived -= OnLog; if(active==this)active=null; }
 void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
 void LateUpdate(){
  // Menu applies normal user settings during startup and transitions. Reapply
  // only this opt-in diagnostic render cap after those callbacks, not deltaTime.
  if(diagnosticTargetFps>0){QualitySettings.vSyncCount=0;Application.targetFrameRate=diagnosticTargetFps;}
  if(outputDirectory!=null && diagnosticTargetFps>0)
   File.AppendAllText(Path.Combine(outputDirectory,"frame-timing.tsv"),string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:o}\t{1}\t{2}\t{3}\t{4}\n",DateTime.UtcNow,Time.frameCount,Time.unscaledDeltaTime,Application.targetFrameRate,QualitySettings.vSyncCount));
 }
 void Update(){
  if(diagnosticSpikeMs>=100 && Time.frameCount%180==0) System.Threading.Thread.Sleep(diagnosticSpikeMs);
  // Read-only 10 Hz movement ledger catches jumps between the 1 Hz snapshots.
  if(Time.unscaledTime >= nextMotionSample && outputDirectory != null) {
   nextMotionSample=Time.unscaledTime+0.1f;
   var local=Array.Find(FindObjectsOfType<FPSController>(),candidate=>Menu.network!=2 || (candidate.GetComponent<PhotonView>()!=null && candidate.GetComponent<PhotonView>().isMine));
   if(local!=null) {
    var cc=local.GetComponent<CharacterController>();
    if(local.GetComponent<FlatsVerificationContactProbe>()==null)local.gameObject.AddComponent<FlatsVerificationContactProbe>();
    var pos=local.transform.position;
    var grounded=(bool)typeof(FPSController).GetMethod("isGrounded",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(local,null);
    File.AppendAllText(Path.Combine(outputDirectory,"motion.tsv"),string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:o}\t{1}\t{2}\t{3:F4}\t{4:F4}\t{5:F4}\t{6}\t{7}\t{8}\n",DateTime.UtcNow,Time.frameCount,Menu.current,pos.x,pos.y,pos.z,cc.isGrounded,grounded,cc.velocity.ToString("F3")));
    var tutorial=FindObjectOfType<Tutorial>();
    int lesson=tutorial==null?-1:(int)typeof(Tutorial).GetField("step",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(tutorial);
    File.AppendAllText(Path.Combine(outputDirectory,"gameplay-timeline.tsv"),string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:o}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\n",DateTime.UtcNow,Time.frameCount,local.gameObject.GetInstanceID(),lesson,Time.unscaledDeltaTime,cc.collisionFlags,cc.velocity.y,Input.GetAxis("Vertical"),Input.GetAxis("Horizontal"),local.isZoom));
   }
   foreach(var source in FindObjectsOfType<AudioSource>()) if(source.isPlaying)
    File.AppendAllText(Path.Combine(outputDirectory,"audio-timeline.tsv"),string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:o}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\n",DateTime.UtcNow,source.GetInstanceID(),source.name,source.clip==null?"one-shot-or-unassigned":source.clip.name,source.volume,source.mute,source.spatialBlend,source.minDistance,source.maxDistance,source.rolloffMode,AudioListener.pause));
   int fragments=0;foreach(var body in FindObjectsOfType<Rigidbody>())if(body.transform.root.name.StartsWith("BrokenGlass"))fragments++;
   File.AppendAllText(Path.Combine(outputDirectory,"fragment-timeline.tsv"),DateTime.UtcNow.ToString("o")+"\t"+fragments+Environment.NewLine);
  }
  if(Input.GetMouseButtonDown(0))Debug.Log("FLATS_INPUT mouse="+Input.mousePosition+" focused="+Application.isFocused);
  var pad=InControl.InputManager.ActiveDevice;
  if(pad.Action1.WasPressed||pad.Action2.WasPressed||pad.Action3.WasPressed||pad.Action4.WasPressed||pad.CommandWasPressed||pad.RightTrigger.WasPressed)
   Debug.Log("FLATS_PAD device="+pad.Name+" A="+pad.Action1.WasPressed+" B="+pad.Action2.WasPressed+" X="+pad.Action3.WasPressed+" Y="+pad.Action4.WasPressed+" Start="+pad.CommandWasPressed+" RT="+pad.RightTrigger.WasPressed);
 }
 IEnumerator Start(){
  for(int i=0;i<3600;i++){
   yield return new WaitForSecondsRealtime(1);
   var p=Array.Find(FindObjectsOfType<FPSController>(),candidate=>Menu.network!=2 || (candidate.GetComponent<PhotonView>()!=null && candidate.GetComponent<PhotonView>().isMine));
   var gun=p==null?null:typeof(FPSController).GetField("currentGun",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p) as Gun;
   var actions=new List<ActionInfo>();
   foreach(var b in FindObjectsOfType<Button>()){
    var c=b.GetComponentInParent<Canvas>();
    if(c==null||!c.enabled)continue;
    var rect=b.transform as RectTransform;
    var center=RectTransformUtility.WorldToScreenPoint(c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera,rect.TransformPoint(rect.rect.center));
    string label="";foreach(var t in b.GetComponentsInChildren<Text>())label+=(label==""?"":" ")+t.text;
    actions.Add(new ActionInfo {name=b.name,text=label,path=b.transform.parent.name+"/"+b.name,x=center.x,y=Screen.height-center.y,interactable=b.IsInteractable()});
   }
   var inputs=new List<InputInfo>();
   foreach(var input in FindObjectsOfType<InputField>()) {
    var c=input.GetComponentInParent<Canvas>();if(c==null||!c.enabled)continue;
    var rect=input.transform as RectTransform;
    var center=RectTransformUtility.WorldToScreenPoint(c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera,rect.TransformPoint(rect.rect.center));
    inputs.Add(new InputInfo{name=input.name,text=input.text,path=input.transform.parent.name+"/"+input.name,x=center.x,y=Screen.height-center.y,interactable=input.IsInteractable()});
   }
   var icons=new List<IconInfo>();
   foreach(var icon in FindObjectsOfType<Image>()) if(icon.sprite!=null&&icon.sprite.name.StartsWith("Multiplayer"))
    icons.Add(new IconInfo{name=icon.name,sprite=icon.sprite.name,spriteRect=icon.sprite.rect,size=icon.rectTransform.sizeDelta});
   int playing=0,listeners=0;
   var actors=new List<ActorInfo>();
   foreach(var bot in FindObjectsOfType<AI>()){
    var screen=Camera.main==null?Vector3.zero:Camera.main.WorldToScreenPoint(bot.transform.position+Vector3.up*3);
    var damage=bot.GetComponent<DamageReceiver>();
    var agent=bot.GetComponent<UnityEngine.AI.NavMeshAgent>();
    bool onMesh=agent!=null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    var approach=new UnityEngine.AI.NavMeshPath();UnityEngine.AI.NavMeshHit ph,bh;
    if(p!=null&&UnityEngine.AI.NavMesh.SamplePosition(p.transform.position,out ph,5,-1)&&UnityEngine.AI.NavMesh.SamplePosition(bot.transform.position,out bh,5,-1))UnityEngine.AI.NavMesh.CalculatePath(ph.position,bh.position,-1,approach);
    var targetDetails=new List<string>();if(bot.targets!=null)foreach(var t in bot.targets)targetDetails.Add(t==null?"destroyed":t.gameObject.GetInstanceID()+":"+t.name+":"+t.position.ToString("F3")+":damage="+(t.GetComponent<DamageReceiver>()!=null));
    actors.Add(new ActorInfo{approachPath=approach.status==UnityEngine.AI.NavMeshPathStatus.PathComplete?approach.corners:new Vector3[0],targetDetails=targetDetails.ToArray(),id=bot.gameObject.GetInstanceID(),name=bot.name,position=bot.transform.position.ToString("F3"),team=bot.gameObject.layer,health=damage==null?0:damage.hitPoints,zombie=bot.zombie,vip=bot.vip,x=screen.x,y=Screen.height-screen.y,z=screen.z,onMesh=onMesh,hasPath=onMesh&&agent.hasPath,stopped=onMesh&&agent.isStopped,path=onMesh?agent.pathStatus.ToString():"off mesh",destination=onMesh?agent.destination.ToString("F3"):"",velocity=onMesh?agent.velocity.ToString("F3"):"",remaining=onMesh&&!float.IsInfinity(agent.remainingDistance)?agent.remainingDistance:-1,speed=agent==null?0:agent.speed,patrol=bot.isPatrol,targets=bot.targets==null?0:bot.targets.Count,rootMotion=bot.GetComponent<Animator>().applyRootMotion});
   }
   foreach(var a in FindObjectsOfType<AudioSource>())if(a.isPlaying)playing++;
   foreach(var a in FindObjectsOfType<AudioListener>())if(a.enabled)listeners++;
   var glass=new List<GlassInfo>();
   foreach(var g in FindObjectsOfType<Glass>()) {
    var c=g.GetComponent<Collider>();var renderer=g.GetComponent<Renderer>();var m=renderer==null?null:renderer.sharedMaterial;
    var screen=Camera.main==null?Vector3.zero:Camera.main.WorldToScreenPoint(g.transform.position);
    glass.Add(new GlassInfo{id=g.GetInstanceID(),position=g.transform.position.ToString("F3"),broken=g.broken,collider=c!=null&&c.enabled,material=m==null?"none":m.name,color=m!=null&&m.HasProperty("_Color")?m.color.ToString():"",x=screen.x,y=Screen.height-screen.y,z=screen.z});
   }
   var scores=new List<ScoreInfo>();foreach(var b in FlatsOfflineScores.Bots)scores.Add(new ScoreInfo{name=b.name,instanceId=b.instanceId,kills=b.kills,deaths=b.deaths,team=b.team});
   var texts=new List<string>();foreach(var t in FindObjectsOfType<Text>())if(t.isActiveAndEnabled&&!string.IsNullOrEmpty(t.text))texts.Add(t.text);
   var menu=FindObjectOfType<Menu>();var multi=FindObjectOfType<Multiplayer>();
   var state=new Snapshot {runId=runId,utc=DateTime.UtcNow.ToString("o"),processStartUtc=processStartUtc,buildId=buildId,
    controller=multi!=null?"Multiplayer":FindObjectOfType<Singleplayer>()!=null?"Singleplayer":"none",
    selectedRule=Menu.rule,selectedMap=menu==null?-1:(int)typeof(Menu).GetField("offlineMap",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(menu),
    configuredBots=Menu.botCount,objective=Menu.objective,detailedObjective=multi==null?-1:multi.detailedObjective,
    menuBusy=menu!=null&&(bool)typeof(Menu).GetField("fliping",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(menu),
    canOpen=Menu.canOpen,focused=Application.isFocused,width=Screen.width,height=Screen.height,
    playerTeam=PhotonNetwork.player==null?"none":PhotonNetwork.player.GetTeam().ToString(),glass=glass.ToArray(),scores=scores.ToArray(),uiText=texts.ToArray(),
    playerYaw=Camera.main==null?0:Camera.main.transform.eulerAngles.y,playerPitch=Camera.main==null?0:Camera.main.transform.eulerAngles.x,
    bodyYaw=p==null?0:p.transform.eulerAngles.y,verticalInput=Input.GetAxis("Vertical"),horizontalInput=Input.GetAxis("Horizontal"),
    animatorDelta=p==null?Vector3.zero:p.GetComponent<Animator>().deltaPosition,
    playerId=p==null?0:p.gameObject.GetInstanceID(),weaponId=gun==null?-1:gun.id,playerHealth=p==null?0:p.GetComponent<DamageReceiver>().hitPoints,playerZombie=p!=null&&p.biten,playerVIP=p!=null&&p.vip,
time=i,errors=errors,processId=System.Diagnostics.Process.GetCurrentProcess().Id,joysticks=Input.GetJoystickNames(),actors=actors.ToArray(),kills=Multiplayer.privateKillCount,deaths=Multiplayer.privateDeathCount,redScore=Multiplayer.redTeamScore,blueScore=Multiplayer.blueTeamScore,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
    menu=Menu.current,game=Menu.gameState,detail=Menu.currentDetail==null?"":Menu.currentDetail.name,
    selected=EventSystem.current==null||EventSystem.current.currentSelectedGameObject==null?"":EventSystem.current.currentSelectedGameObject.name,inputModule=EventSystem.current==null||EventSystem.current.currentInputModule==null?"":EventSystem.current.currentInputModule.GetType().Name,device=InControl.InputManager.ActiveDevice.Name,
    padLeftX=InControl.InputManager.ActiveDevice.LeftStickX,padLeftY=InControl.InputManager.ActiveDevice.LeftStickY,padRightX=InControl.InputManager.ActiveDevice.RightStickX,padRightY=InControl.InputManager.ActiveDevice.RightStickY,padTrigger=InControl.InputManager.ActiveDevice.RightTrigger,
    player=p==null?"none":p.transform.position.ToString("F3"),ammo=gun==null?-1:gun.currentAmmo,
    bots=FindObjectsOfType<AI>().Length,listeners=listeners,playingAudio=playing,
    rule=Menu.gameState=="Multiplayer"?Multiplayer.rule:Singleplayer.rule,offlineRoom=PhotonNetwork.offlineMode&&PhotonNetwork.inRoom,actions=actions.ToArray()};
   state.modules=ObserveModules();
   state.inputs=inputs.ToArray();state.icons=icons.ToArray();state.photonOffline=PhotonNetwork.offlineMode;
   var controller=p==null?null:p.GetComponent<CharacterController>();
   state.controllerGrounded=controller!=null&&controller.isGrounded;
   state.controllerVelocity=controller==null?Vector3.zero:controller.velocity;
   state.gameplayGrounded=p!=null&&(bool)typeof(FPSController).GetMethod("isGrounded",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
   state.zoom=p!=null&&p.isZoom;state.fieldOfView=Camera.main==null?0:Camera.main.fieldOfView;
   var roots=new List<string>();foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())roots.Add(root.name);state.roots=roots.ToArray();
   var effects=new List<string>();if(Camera.main!=null)foreach(var effect in Camera.main.GetComponents<Behaviour>())effects.Add(effect.GetType().Name+":"+effect.enabled);state.cameraEffects=effects.ToArray();
   state.inRoom=PhotonNetwork.inRoom;state.connected=PhotonNetwork.connectedAndReady;
   state.photonState=PhotonNetwork.connectionStateDetailed.ToString();state.room=PhotonNetwork.room==null?"":PhotonNetwork.room.Name;
   state.photonPlayerCount=PhotonNetwork.playerList.Length;state.photonActorId=PhotonNetwork.player.ID;
   state.region=PhotonNetwork.PhotonServerSettings.PreferredRegion.ToString();
   state.configurationSource=FlatsPhotonConfiguration.Source;state.configurationValid=FlatsPhotonConfiguration.Valid;
   state.matchEnded=Multiplayer.end;state.matchTime=Multiplayer.limit;
   state.spectatorCameras=FindObjectsOfType<WatchCamera>().Length;
   var networkScores=new List<NetworkScore>();foreach(var actor in PhotonNetwork.playerList)
    networkScores.Add(new NetworkScore{actorId=actor.ID,kills=actor.CustomProperties.ContainsKey("K")?(int)actor.CustomProperties["K"]:0,deaths=actor.CustomProperties.ContainsKey("D")?(int)actor.CustomProperties["D"]:0});
   state.networkScores=networkScores.ToArray();
   var networkActors=new List<NetworkActor>();foreach(var view in FindObjectsOfType<PhotonView>()) {
    var damage=view.GetComponent<DamageReceiver>();if(damage==null)continue;
    var fps=view.GetComponent<FPSController>();var weapon=fps==null?null:typeof(FPSController).GetField("currentGun",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(fps) as Gun;
    var screen=Camera.main==null?Vector3.zero:Camera.main.WorldToScreenPoint(view.transform.position+Vector3.up*3);
    var approach=new UnityEngine.AI.NavMeshPath();UnityEngine.AI.NavMeshHit ph,bh;
    if(p!=null&&UnityEngine.AI.NavMesh.SamplePosition(p.transform.position,out ph,8,-1)&&UnityEngine.AI.NavMesh.SamplePosition(view.transform.position,out bh,8,-1))UnityEngine.AI.NavMesh.CalculatePath(ph.position,bh.position,-1,approach);
    networkActors.Add(new NetworkActor{viewId=view.viewID,ownerId=view.owner==null?0:view.owner.ID,instanceId=view.gameObject.GetInstanceID(),name=view.name,position=view.transform.position,rotation=view.transform.eulerAngles,health=damage.hitPoints,mine=view.isMine,ammo=weapon==null?-1:weapon.currentAmmo,x=screen.x,y=Screen.height-screen.y,z=screen.z,approachPath=approach.status==UnityEngine.AI.NavMeshPathStatus.PathComplete?approach.corners:new Vector3[0]});
   }state.networkActors=networkActors.ToArray();
   var lessonOwner=FindObjectOfType<Tutorial>();
   state.tutorialStep=lessonOwner==null?-1:(int)typeof(Tutorial).GetField("step",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(lessonOwner);
   var pickups=new List<PickupInfo>();foreach(var pickup in FindObjectsOfType<DroppedGun>())pickups.Add(new PickupInfo{id=pickup.GetInstanceID(),weapon=pickup.weaponIndex,position=pickup.transform.position,ready=pickup.ready,persistent=pickup.dontDestroy});state.pickups=pickups.ToArray();
   var optics=new List<OpticInfo>();foreach(var camera in FindObjectsOfType<Camera>()) {
    // Same fixed world-space one-metre segment for every camera. Projection is
    // measured from the actual camera matrix, independent of the UI 6x label.
    var bottom=camera.WorldToScreenPoint(new Vector3(0,3,0));var top=camera.WorldToScreenPoint(new Vector3(0,4,0));
    optics.Add(new OpticInfo{name=camera.name,id=camera.GetInstanceID(),fov=camera.fieldOfView,enabled=camera.enabled,width=camera.pixelWidth,height=camera.pixelHeight,position=camera.transform.position,rotation=camera.transform.rotation,projectionMatrix=camera.projectionMatrix,worldToCameraMatrix=camera.worldToCameraMatrix,fixedTargetPixelHeight=Mathf.Abs(top.y-bottom.y)});
   }state.optics=optics.ToArray();
   if(Camera.main!=null){
    RaycastHit? nearest=null;
    foreach(var hit in Physics.RaycastAll(Camera.main.transform.position,Camera.main.transform.forward,1000f)){
     if(hit.collider.isTrigger||hit.collider.GetComponentInParent<DamageReceiver>()!=null)continue;
     if(!nearest.HasValue||hit.distance<nearest.Value.distance)nearest=hit;
    }
    if(nearest.HasValue){var hit=nearest.Value;state.aimGeometry=new AimGeometryInfo{name=hit.collider.name,colliderId=hit.collider.GetInstanceID(),distance=hit.distance,point=hit.point,normal=hit.normal,boundsCenter=hit.collider.bounds.center,boundsSize=hit.collider.bounds.size};}
   }
   // Measure where the render texture is actually displayed; texture-camera
   // FOV alone does not equal the player's on-screen magnification.
   var surfaces=new List<ScopeSurfaceInfo>();
   foreach(var raw in FindObjectsOfType<RawImage>()) {
    var texture=raw.texture as RenderTexture;if(texture==null)continue;
    var canvas=raw.GetComponentInParent<Canvas>();if(canvas==null)continue;
    Camera displayCamera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
    // A world-space Canvas.worldCamera can be its event camera (including
    // the scope texture camera). The weapon is rendered by Gun Camera.
    if(canvas.renderMode==RenderMode.WorldSpace){
     foreach(var candidate in FindObjectsOfType<Camera>())if(candidate.name=="Gun Camera")displayCamera=candidate;
    }
    var corners=new Vector3[4];raw.rectTransform.GetWorldCorners(corners);
    for(int j=0;j<corners.Length;j++)corners[j]=displayCamera==null?corners[j]:displayCamera.WorldToScreenPoint(corners[j]);
    foreach(var camera in FindObjectsOfType<Camera>())if(camera.targetTexture==texture)
     surfaces.Add(new ScopeSurfaceInfo{name=raw.name,textureId=texture.GetInstanceID(),cameraId=camera.GetInstanceID(),screenCorners=corners,textureWidth=texture.width,textureHeight=texture.height});
   }state.scopeSurfaces=surfaces.ToArray();
   string json=JsonUtility.ToJson(state);
   var statePath=Path.Combine(outputDirectory,"verification-state.json");
   File.AppendAllText(Path.Combine(outputDirectory,"snapshots.jsonl"),json+Environment.NewLine);
   try {
    File.WriteAllText(statePath+".tmp",json);
    if(File.Exists(statePath))File.Replace(statePath+".tmp",statePath,null);else File.Move(statePath+".tmp",statePath);
   } catch(IOException ex) { Debug.LogWarning("FLATS_PROBE_PUBLISH_RETRY "+ex.Message); }
   Debug.Log("FLATS_PROBE "+json);
   string key=state.scene+"-"+state.menu+"-"+state.detail;
   if(key!=lastState){
    lastState=key;stableState=0;
   }
   else if(++stableState==5)
    UnityEngine.ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory,"parity-"+state.processId+"-"+(screenshot++)+"-"+state.scene+"-"+state.menu+".png"));
  }
 }
}

// Added only by the opt-in read-only probe; does not alter collision response.
public sealed class FlatsVerificationContactProbe : MonoBehaviour {
 private int lastFrame=-1;
 private readonly System.Collections.Generic.HashSet<int> seen=new System.Collections.Generic.HashSet<int>();
 void OnControllerColliderHit(ControllerColliderHit hit){
  if(lastFrame!=Time.frameCount){seen.Clear();lastFrame=Time.frameCount;}
  if(seen.Add(hit.collider.GetInstanceID()))FlatsVerificationProbe.ContactObserved(hit);
 }
}
