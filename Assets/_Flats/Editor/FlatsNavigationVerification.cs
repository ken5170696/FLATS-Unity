using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class FlatsNavigationVerification
{
    [Serializable] public class MapCheck { public string map,basePath; public int spawnCount,sampledSpawns,spawnsWithRoute,waypoints,sampledWaypoints;public bool pass; }
    [Serializable] public class Report { public string scope="Read-only baked NavMesh connectivity; not bot behavior acceptance";public List<MapCheck> maps=new List<MapCheck>(); }
    public static void Run()
    {
        var report=new Report();
        foreach(var map in new[]{"FlatCity","UrbanPark","BeachsideTown","DepartmentStore","Warehouse","NightLand","Tutorial"})
        {
            EditorSceneManager.OpenScene("Assets/_Flats/Scenes/"+map+".unity");
            NavMesh.RemoveAllNavMeshData();
            var instance=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/_Flats/Data/Navigation/OfflineNavigation/"+map+".asset"));
            var item=new MapCheck {map=map};report.maps.Add(item);
            var points=new List<Vector3>();
            foreach(Transform point in GameObject.Find("WayPoints").transform)
            {
                item.waypoints++;NavMeshHit hit;
                if(NavMesh.SamplePosition(point.position,out hit,5,NavMesh.AllAreas)){item.sampledWaypoints++;points.Add(hit.position);}
            }
            var spawns=new List<Transform>();
            if(map=="Tutorial")spawns.Add(GameObject.Find("SpawnPosition").transform);
            else foreach(Transform spawn in GameObject.Find("SpawnPoints").transform)spawns.Add(spawn);
            foreach(Transform spawn in spawns)
            {
                item.spawnCount++;NavMeshHit hit;
                if(!NavMesh.SamplePosition(spawn.position,out hit,5,NavMesh.AllAreas))continue;
                item.sampledSpawns++;
                int before=item.spawnsWithRoute;
                foreach(var point in points){var path=new NavMeshPath();if(NavMesh.CalculatePath(hit.position,point,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){item.spawnsWithRoute++;break;}}
                if(before==item.spawnsWithRoute)Debug.Log("NAV_ISOLATED "+map+" "+spawn.name+" position="+spawn.position+" sampled="+hit.position);
            }
            if(map=="Tutorial")
            {
                item.basePath="not applicable";
                item.pass=item.spawnCount==1 && item.spawnsWithRoute==1 && item.sampledWaypoints==item.waypoints;
                instance.Remove();continue;
            }
            NavMeshHit red,blue;var between=new NavMeshPath();
            bool bases=NavMesh.SamplePosition(GameObject.Find("RedTeamBase").transform.position,out red,5,NavMesh.AllAreas) && NavMesh.SamplePosition(GameObject.Find("BlueTeamBase").transform.position,out blue,5,NavMesh.AllAreas);
            // Sample separately to make definite assignment and failure reasons explicit.
            NavMesh.SamplePosition(GameObject.Find("BlueTeamBase").transform.position,out blue,5,NavMesh.AllAreas);
            item.basePath=bases && NavMesh.CalculatePath(red.position,blue.position,NavMesh.AllAreas,between)?between.status.ToString():"base not on mesh";
            item.pass=item.spawnCount>0 && item.sampledSpawns==item.spawnCount && item.spawnsWithRoute==item.spawnCount && item.basePath=="PathComplete";
            instance.Remove();
        }
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/parity-nav-connectivity.json")),JsonUtility.ToJson(report,true));
        EditorApplication.Exit(report.maps.TrueForAll(m=>m.pass)?0:1);
    }
}
