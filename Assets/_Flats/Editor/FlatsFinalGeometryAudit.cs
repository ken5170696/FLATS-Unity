using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;

// Read-only level survey and route planning. Never moves a gameplay actor,
// saves a scene, builds navigation data, or substitutes for runtime traversal.
public static class FlatsFinalGeometryAudit
{
    [Serializable] public class Surface { public string scene,name,parent,type,globalId; public Vector3 center,size,euler,localPosition,localScale; public bool enabled,trigger,convex,rendererEnabled; }
    [Serializable] public class WallHit {public string name; public int id; public Vector3 point,normal; public float distance;}
    [Serializable] public class WallProbe {public Vector3 origin,direction; public List<WallHit> hits=new List<WallHit>();}
    [Serializable] public class WallReport {public List<WallProbe> probes=new List<WallProbe>();}
    [Serializable] public class Survey { public string utc; public List<Surface> surfaces=new List<Surface>(); }
    [Serializable] public class Clearance {public string scene,ceiling,floor; public Vector3 feet,ceilingPoint;public float gap;}
    [Serializable] public class Clearances {public List<Clearance> candidates=new List<Clearance>();}
    [Serializable] public class Request { public string scene; public Vector3 start,target; }
    [Serializable] public class Route { public string scene,status; public Vector3 requestedStart,requestedTarget,sampledStart,sampledTarget; public Vector3[] corners; }
    static string Arg(string key) { var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,key); return i<0?null:args[i+1]; }
    public static void Run()
    {
        var report=new Survey {utc=DateTime.UtcNow.ToString("o")};
        var clearances=new Clearances();
        foreach(var scene in new[]{"Tutorial","FlatCity","UrbanPark","BeachsideTown","DepartmentStore","Warehouse","NightLand"}) {
            EditorSceneManager.OpenScene("Assets/_Flats/Scenes/"+scene+".unity",OpenSceneMode.Single);
            Physics.SyncTransforms();
            if(scene=="FlatCity"){
                var step=new WallReport();
                foreach(float x in new[]{-10f,-6f,-5.1f,-4.9f,0f}){
                    var probe=new WallProbe {origin=new Vector3(x,10f,-90f),direction=Vector3.down};
                    foreach(var hit in Physics.RaycastAll(probe.origin,probe.direction,12))
                        probe.hits.Add(new WallHit {name=hit.collider.name,id=hit.collider.GetInstanceID(),point=hit.point,normal=hit.normal,distance=hit.distance});
                    step.probes.Add(probe);
                }
                foreach(float z in new[]{-102f,-100f,-97f,-95.1f,-94.9f,-92f,-90f}){
                    var probe=new WallProbe {origin=new Vector3(3f,10f,z),direction=Vector3.down};
                    foreach(var hit in Physics.RaycastAll(probe.origin,probe.direction,12))
                        probe.hits.Add(new WallHit {name=hit.collider.name,id=hit.collider.GetInstanceID(),point=hit.point,normal=hit.normal,distance=hit.distance});
                    step.probes.Add(probe);
                }
                File.WriteAllText(Path.Combine(Arg("-visualEvidence"),"small-step-rays.json"),JsonUtility.ToJson(step,true));
            }
            if(scene=="BeachsideTown"){
                var seam=new WallReport();
                foreach(float x in new[]{107.99f,108f,108.001f,108.01f,108.02f,108.05f,108.1f,108.2f,108.3f,108.4f,108.5f,109f,109.5f,109.9f,110f,110.1f,110.5f,111f}){
                    var probe=new WallProbe {origin=new Vector3(x,20f,180f),direction=Vector3.down};
                    foreach(var hit in Physics.RaycastAll(probe.origin,probe.direction,30))
                        probe.hits.Add(new WallHit {name=hit.collider.name,id=hit.collider.GetInstanceID(),point=hit.point,normal=hit.normal,distance=hit.distance});
                    seam.probes.Add(probe);
                }
                File.WriteAllText(Path.Combine(Arg("-visualEvidence"),"ramp-lip-rays.json"),JsonUtility.ToJson(seam,true));
            }
            if(scene=="UrbanPark"){
                var curb=new WallReport();
                foreach(float x in new[]{176f,178f,179.8f,180f,180.6f,180.8f,181f,181.2f,181.4f,182f,184f}){
                    var probe=new WallProbe {origin=new Vector3(x,20f,-226f),direction=Vector3.down};
                    foreach(var hit in Physics.RaycastAll(probe.origin,probe.direction,15))
                        probe.hits.Add(new WallHit {name=hit.collider.name,id=hit.collider.GetInstanceID(),point=hit.point,normal=hit.normal,distance=hit.distance});
                    curb.probes.Add(probe);
                }
                foreach(float y in new[]{10.15f,10.3f,10.5f}){
                    var probe=new WallProbe {origin=new Vector3(176f,y,-226f),direction=Vector3.right};
                    foreach(var hit in Physics.RaycastAll(probe.origin,probe.direction,10))
                        probe.hits.Add(new WallHit {name=hit.collider.name,id=hit.collider.GetInstanceID(),point=hit.point,normal=hit.normal,distance=hit.distance});
                    curb.probes.Add(probe);
                }
                File.WriteAllText(Path.Combine(Arg("-visualEvidence"),"curb-rays.json"),JsonUtility.ToJson(curb,true));
                var wall=new WallReport();
                foreach(float x in new[]{-280f,-288f}){
                    var probe=new WallProbe {origin=new Vector3(x,16.78f,71.13f),direction=x>-284f?Vector3.left:Vector3.right};
                    foreach(var hit in Physics.RaycastAll(probe.origin,probe.direction,8))
                        probe.hits.Add(new WallHit {name=hit.collider.name,id=hit.collider.GetInstanceID(),point=hit.point,normal=hit.normal,distance=hit.distance});
                    wall.probes.Add(probe);
                }
                File.WriteAllText(Path.Combine(Arg("-visualEvidence"),"thin-wall-rays.json"),JsonUtility.ToJson(wall,true));
            }
            foreach(var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None)) {
                var mesh=c as MeshCollider;
                report.surfaces.Add(new Surface {scene=scene,name=c.name,parent=c.transform.parent==null?"":c.transform.parent.name,type=c.GetType().Name,center=c.bounds.center,size=c.bounds.size,euler=c.transform.eulerAngles,enabled=c.enabled&&c.gameObject.activeInHierarchy,trigger=c.isTrigger,convex=mesh!=null&&mesh.convex,globalId=GlobalObjectId.GetGlobalObjectIdSlow(c).ToString(),localPosition=c.transform.localPosition,localScale=c.transform.localScale,rendererEnabled=c.GetComponent<Renderer>()!=null&&c.GetComponent<Renderer>().enabled});
                if(c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger&&c.bounds.size.y<3&&c.bounds.size.x>5&&c.bounds.size.z>5){
                    Vector3 underside=c.bounds.center-Vector3.up*(c.bounds.extents.y+.05f);
                    RaycastHit ground;
                    if(Physics.Raycast(underside,Vector3.down,out ground,12)&&ground.collider!=c){
                        float gap=underside.y+.05f-ground.point.y;
                        if(gap>6.5f&&gap<11.4f)clearances.candidates.Add(new Clearance{scene=scene,ceiling=c.name,floor=ground.collider.name,feet=ground.point,ceilingPoint=underside,gap=gap});
                    }
                }
            }
        }
        File.WriteAllText(Path.Combine(Arg("-visualEvidence"),"surfaces.json"),JsonUtility.ToJson(report,true));
        File.WriteAllText(Path.Combine(Arg("-visualEvidence"),"low-ceiling-candidates.json"),JsonUtility.ToJson(clearances,true));
        Debug.Log("FLATS_FINAL_GEOMETRY_SURVEY_COMPLETE");
    }
    public static void Plan()
    {
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(Arg("-geometryRequest")));
        if(Array.IndexOf(new[]{"FlatCity","UrbanPark","BeachsideTown","DepartmentStore","Warehouse","NightLand"},request.scene)<0)throw new ArgumentException("Invalid scene");
        EditorSceneManager.OpenScene("Assets/_Flats/Scenes/"+request.scene+".unity",OpenSceneMode.Single);
        NavMesh.RemoveAllNavMeshData();
        var instance=NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/_Flats/Data/Navigation/OfflineNavigation/"+request.scene+".asset"));
        try {
            NavMeshHit start,target;var path=new NavMeshPath();
            bool sampled=NavMesh.SamplePosition(request.start,out start,8,-1)&NavMesh.SamplePosition(request.target,out target,35,-1);
            bool ok=sampled&&NavMesh.CalculatePath(start.position,target.position,-1,path);
            var result=new Route {scene=request.scene,status=ok?path.status.ToString():"Unavailable",requestedStart=request.start,requestedTarget=request.target,sampledStart=start.position,sampledTarget=target.position,corners=path.corners};
            File.WriteAllText(Path.Combine(Arg("-visualEvidence"),"route.json"),JsonUtility.ToJson(result,true));
        }finally{instance.Remove();}
    }
}
