using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Native agent traversal regression, including generated drop links. Full AI
// target selection and combat are verified separately in a playable Player.
[InitializeOnLoad]
public static class FlatsNavigationPlayValidation
{
    const string Key="Flats.NavigationPlay";
    [Serializable] class Case {public string map;public Vector3 start,end;}
    [Serializable] class Cases {public List<Case> items=new List<Case>();}
    static readonly List<NavMeshAgent> agents=new List<NavMeshAgent>();
    static readonly List<string> results=new List<string>();
    static Cases cases;
    static int mapIndex,errors;
    static bool started;
    static double deadline,ready;
    static NavMeshDataInstance mesh;
    static readonly string[] maps={"Warehouse","NightLand","Tutorial"};
    static FlatsNavigationPlayValidation()
    {
        if(!SessionState.GetBool(Key,false))return;
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;};
    }
    public static void Run()
    {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLATS_MOD_TEST_ROOT")))throw new InvalidOperationException("An isolated test profile is required.");
        Directory.CreateDirectory(Environment.GetEnvironmentVariable("FLATS_MOD_TEST_ROOT"));
        var collected=new Cases();
        foreach(var map in maps)
        {
            EditorSceneManager.OpenScene("Assets/_Flats/Scenes/"+map+".unity");
            NavMesh.RemoveAllNavMeshData();
            var data=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/_Flats/Data/Navigation/OfflineNavigation/"+map+".asset"));
            var spawns=new List<Transform>();
            if(map=="Tutorial")spawns.Add(GameObject.Find("SpawnPosition").transform);
            else foreach(Transform spawn in GameObject.Find("SpawnPoints").transform)spawns.Add(spawn);
            foreach(Transform spawn in spawns)
            {
                if(!NavMesh.SamplePosition(spawn.position,out var from,5,-1))throw new Exception("Spawn missing navigation");
                float shortest=float.MaxValue;var end=Vector3.zero;
                foreach(Transform waypoint in GameObject.Find("WayPoints").transform)
                {
                    if(!NavMesh.SamplePosition(waypoint.position,out var to,5,-1))continue;
                    var path=new NavMeshPath();if(!NavMesh.CalculatePath(from.position,to.position,-1,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                    float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                    if(length<shortest){shortest=length;end=to.position;}
                }
                if(shortest==float.MaxValue)throw new Exception("No route from "+map+" "+spawn.position);
                collected.items.Add(new Case {map=map,start=from.position,end=end});
            }
            data.Remove();
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SessionState.SetString(Key+".Cases",JsonUtility.ToJson(collected));SessionState.SetBool(Key,true);
        ready=EditorApplication.timeSinceStartup+10;EditorApplication.update+=StartPlay;
    }
    static void StartPlay(){if(EditorApplication.timeSinceStartup<ready||EditorApplication.isUpdating)return;EditorApplication.update-=StartPlay;EditorApplication.isPlaying=true;}
    static void BeginMap()
    {
        string map=maps[mapIndex];
        mesh=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/_Flats/Data/Navigation/OfflineNavigation/"+map+".asset"));
        var template=Resources.Load<GameObject>("Flatman_Enemy").GetComponent<NavMeshAgent>();
        foreach(var item in cases.items.FindAll(c=>c.map==map))
        {
            var go=new GameObject(map+" "+item.start);go.transform.position=item.start;
            var agent=go.AddComponent<NavMeshAgent>();agent.radius=template.radius;agent.height=template.height;agent.speed=template.speed;
            agent.acceleration=template.acceleration;agent.angularSpeed=template.angularSpeed;agent.stoppingDistance=template.stoppingDistance;
            agent.autoTraverseOffMeshLink=template.autoTraverseOffMeshLink;agent.SetDestination(item.end);agents.Add(agent);
        }
        deadline=EditorApplication.timeSinceStartup+120;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        if(!started){started=true;cases=JsonUtility.FromJson<Cases>(SessionState.GetString(Key+".Cases",""));BeginMap();return;}
        bool finished=true;
        foreach(var agent in agents)
        {
            if(!agent.enabled)continue;
            if(!agent.pathPending&&agent.isOnNavMesh&&Vector3.Distance(agent.transform.position,agent.destination)<=agent.stoppingDistance+.5f)
            {results.Add("PASS "+agent.name+" arrived "+agent.transform.position);agent.enabled=false;}
            else finished=false;
        }
        if(errors>0||EditorApplication.timeSinceStartup>deadline){Finish(false,"Traversal timeout or console errors: "+errors);return;}
        if(!finished)return;
        foreach(var agent in agents)UnityEngine.Object.Destroy(agent.gameObject);agents.Clear();mesh.Remove();
        if(++mapIndex<maps.Length)BeginMap();else Finish(true,"All "+cases.items.Count+" spawn routes traversed");
    }
    static void Finish(bool pass,string detail)
    {
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
        Directory.CreateDirectory("Logs");results.Insert(0,(pass?"PASS ":"FAIL ")+detail);File.WriteAllLines("Logs/navigation-play.txt",results);
        EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(pass?0:1);
    }
}
