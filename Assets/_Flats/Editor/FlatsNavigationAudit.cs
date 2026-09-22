using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FlatsNavigationAudit {
 [Serializable] public class Point {public string map,name,position,sampled,surface;public int index;public bool sampledOK,route;public string[] nearbySurfaces;}
 [Serializable] public class Report {public string utc;public List<Point> points=new List<Point>();}
 public static void Run() {
  var report=new Report{utc=DateTime.UtcNow.ToString("o")};
  foreach(var map in new[]{"FlatCity","UrbanPark","BeachsideTown","DepartmentStore","Warehouse","NightLand"}) {
   EditorSceneManager.OpenScene("Assets/_Flats/Scenes/"+map+".unity");NavMesh.RemoveAllNavMeshData();
   var instance=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/_Flats/Data/Navigation/OfflineNavigation/"+map+".asset"));
   var waypoints=GameObject.Find("WayPoints").transform; int i=0;
   foreach(Transform spawn in GameObject.Find("SpawnPoints").transform) {
    var p=new Point{map=map,name=spawn.name,index=i++,position=spawn.position.ToString("F3")};report.points.Add(p);
    NavMeshHit hit;p.sampledOK=NavMesh.SamplePosition(spawn.position,out hit,5,-1);p.sampled=hit.position.ToString("F3");
    foreach(Transform wp in waypoints){NavMeshHit w;var path=new NavMeshPath();if(p.sampledOK&&NavMesh.SamplePosition(wp.position,out w,5,-1)&&NavMesh.CalculatePath(hit.position,w.position,-1,path)&&path.status==NavMeshPathStatus.PathComplete)p.route=true;}
    RaycastHit ground;if(Physics.Raycast(spawn.position+Vector3.up,Vector3.down,out ground,1000,~0,QueryTriggerInteraction.Ignore))p.surface=ground.collider.name+" parent="+(ground.transform.parent==null?"":ground.transform.parent.name)+" bounds="+ground.collider.bounds;
    if(!p.route){var surfaces=new List<string>();foreach(var c in UnityEngine.Object.FindObjectsOfType<Collider>())if(!c.isTrigger&&c.bounds.SqrDistance(hit.position)<100)surfaces.Add(c.name+" parent="+(c.transform.parent==null?"":c.transform.parent.name)+" bounds="+c.bounds);p.nearbySurfaces=surfaces.ToArray();}
   } instance.Remove();
  }
  string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));string ev=FlatsDeveloperPaths.Reports;
  File.WriteAllText(Path.Combine(ev,"navigation-audit.json"),JsonUtility.ToJson(report,true));EditorApplication.Exit(0);
 }
}
