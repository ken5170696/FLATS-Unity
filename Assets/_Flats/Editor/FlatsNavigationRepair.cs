using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FlatsNavigationRepair {
 [MenuItem("FLATS/Navigation/Rebuild All Maps")]
 public static void Run(){Bake(new[]{"FlatCity","UrbanPark","BeachsideTown","DepartmentStore","Warehouse","NightLand","Tutorial"});}
 [MenuItem("FLATS/Navigation/Rebuild Tutorial")]
 public static void Tutorial(){Bake(new[]{"Tutorial"});}
 static void Bake(string[] maps){
  if(EditorApplication.isPlayingOrWillChangePlaymode)
   throw new InvalidOperationException("Stop Play Mode before rebuilding navigation.");
  foreach(var name in maps)
   if(!File.Exists("Assets/_Flats/Scenes/"+name+".unity"))
    throw new FileNotFoundException("Navigation scene not found",name);
  // Single-scene loading must never discard the author's unsaved work. Batch
  // jobs fail explicitly instead of displaying a save dialog they cannot answer.
  if(Application.isBatchMode){
   for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
    if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
     throw new InvalidOperationException("Save all scenes before rebuilding navigation in batch mode.");
  }else if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  var previous=EditorSceneManager.GetSceneManagerSetup();
  try{BakeMaps(maps);}
  finally{EditorSceneManager.RestoreSceneManagerSetup(previous);}
 }
 static void BakeMaps(string[] maps){
  Directory.CreateDirectory("Assets/_Flats/Data/Navigation/OfflineNavigation");
  foreach(var name in maps){
   var scene=EditorSceneManager.OpenScene("Assets/_Flats/Scenes/"+name+".unity");
   var sources=new List<NavMeshBuildSource>();
   UnityEngine.AI.NavMeshBuilder.CollectSources(null,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
   sources.RemoveAll(s=>s.component!=null && ((s.component is Collider && ((Collider)s.component).isTrigger) || s.component.GetComponent<Rigidbody>()!=null || s.component.GetComponent<CharacterController>()!=null));
   var settings=NavMesh.GetSettingsByID(0);settings.agentRadius=0.5f;settings.agentHeight=2;settings.agentClimb=0.4f;settings.agentSlope=45;settings.overrideVoxelSize=true;settings.voxelSize=0.2f;
   // These maps include roof/raised-platform spawns. Preserve those locations;
   // generate one-way drops over the real collision geometry so bots can leave.
   // No jump-across or upward links are introduced.
   settings.ledgeDropHeight=name=="NightLand"?60f:name=="Warehouse"?5f:0f;
   if(settings.ledgeDropHeight>0)
    for(int i=0;i<sources.Count;i++){var source=sources[i];source.generateLinks=true;sources[i]=source;}
   var data=UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(4000,1000,4000)),Vector3.zero,Quaternion.identity);
   if(data==null)throw new Exception("NavMesh build failed: "+name);
   // Validate the result before replacing the last usable asset.
   var inst=NavMesh.AddNavMeshData(data);int triangles;
   try{triangles=NavMesh.CalculateTriangulation().indices.Length/3;}
   finally{inst.Remove();}
   if(triangles==0){UnityEngine.Object.DestroyImmediate(data);throw new Exception("No walkable triangles: "+name);}
   string path="Assets/_Flats/Data/Navigation/OfflineNavigation/"+name+".asset";
   var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
   if(existing!=null){EditorUtility.CopySerialized(data,existing);UnityEngine.Object.DestroyImmediate(data);data=existing;}else AssetDatabase.CreateAsset(data,path);
   var old=UnityEngine.Object.FindFirstObjectByType<FlatsOfflineNavigation>();
   var loader=old!=null?old:new GameObject("Offline Navigation").AddComponent<FlatsOfflineNavigation>();
   bool changed=loader.data!=data;loader.data=data;
   if(old==null||changed){
    EditorUtility.SetDirty(loader);EditorSceneManager.MarkSceneDirty(scene);
    if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save navigation binding: "+scene.path);
   }
   Debug.Log("FLATS_NAVMESH "+name+" sources="+sources.Count+" triangles="+triangles);
  }
  AssetDatabase.SaveAssets();
 }
}

