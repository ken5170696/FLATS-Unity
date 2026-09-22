using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Engine-level diagnostic only. Does NOT claim FPSController gameplay acceptance.
// Unsaved fixtures and this Editor assembly are excluded from player builds.
public static class FlatsControllerGeometryChecks
{
    public static void Seam() {
        EditorSceneManager.OpenScene("Assets/_Flats/Scenes/BeachsideTown.unity",OpenSceneMode.Single);
        var rows=new List<Row>();
        foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"separate","combined","projected"}) {
            var actor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Flatman.prefab"));
            try {
                foreach(var b in actor.GetComponentsInChildren<Behaviour>(true))b.enabled=false;
                actor.transform.SetPositionAndRotation(new Vector3(145,-4.92f,180),Quaternion.identity);
                var cc=actor.GetComponent<CharacterController>();Physics.SyncTransforms();
                var row=new Row{name=mode,simulatedHz=hz,start=actor.transform.position,maxY=-4.92f,minY=-4.92f,stepOffset=cc.stepOffset,slopeLimit=cc.slopeLimit};
                for(int i=0;i<hz*4;i++) {
                    var velocity=Vector3.left*15;RaycastHit ground;
                    if(mode=="projected"&&Physics.Raycast(actor.transform.position+Vector3.up,Vector3.down,out ground,2)&&ground.normal.y>=Mathf.Cos(cc.slopeLimit*Mathf.Deg2Rad))
                        velocity.y=-(velocity.x*ground.normal.x+velocity.z*ground.normal.z)/ground.normal.y-.5f;
                    else velocity.y=-9.81f;
                    CollisionFlags flags;
                    if(mode=="separate") {flags=cc.Move(Vector3.left*15/hz);flags|=cc.Move(Vector3.down*9.81f/hz);}
                    else flags=cc.Move(velocity/hz);
                    row.below|=(flags&CollisionFlags.Below)!=0;row.sides|=(flags&CollisionFlags.Sides)!=0;
                    row.maxY=Mathf.Max(row.maxY,actor.transform.position.y);
                }
                row.end=actor.transform.position;row.status=row.end.x<103&&row.end.y>9?"PASS":"FAIL";rows.Add(row);
            }finally{UnityEngine.Object.DestroyImmediate(actor);}
        }
        var arguments=Environment.GetCommandLineArgs();string output=arguments[Array.IndexOf(arguments,"-visualEvidence")+1];
        File.WriteAllText(Path.Combine(output,"seam-engine-diagnosis.json"),JsonUtility.ToJson(new Report{unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("o"),cases=rows.ToArray()},true));
    }
    [Serializable] class Row {
        public string name, status, limitation = "Explicit CharacterController.Move stimulus; FPSController.Update is not exercised";
        public int simulatedHz; public float stepOffset, slopeLimit, maxY, minY, worldHeight, worldStepOffset;
        public Vector3 start, end; public bool above, below, sides, finite;
    }
    [Serializable] class Report { public string unity, utc, status = "PARTIAL"; public Row[] cases; }
    static GameObject Box(string name, Vector3 pos, Vector3 size, float angle = 0) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.position = pos; go.transform.localScale = size;
        go.transform.rotation = Quaternion.Euler(angle, 0, 0); return go;
    }
    public static void Run() {
        EditorSceneManager.OpenScene("Assets/_Flats/Scenes/MainMenu.unity", OpenSceneMode.Single);
        var rows = new List<Row>();
        foreach (int hz in new[] { 15, 30, 60, 144 })
        foreach (string test in new[] { "step-below-offset", "excessive-step", "stairs", "ceiling", "slope-30", "slope-60", "wall" }) {
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try {
                Box("floor", new Vector3(0, -.5f, 0), new Vector3(20, 1, 200));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Flatman.prefab");
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                // Initial fixture spawn only; never reposition an active gameplay player.
                actor.transform.SetPositionAndRotation(new Vector3(0, .05f, 0), Quaternion.identity);
                foreach (var b in actor.GetComponentsInChildren<Behaviour>(true)) b.enabled = false;
                var cc = actor.GetComponent<CharacterController>();
                float worldHeight=cc.height*actor.transform.lossyScale.y;
                // stepOffset is the configured traversal distance; unlike capsule height,
                // do not infer a larger traversable ledge from transform scaling.
                float worldStep=cc.stepOffset;
                if (actor.GetComponent<FPSController>() == null) throw new Exception("Real Flatman missing FPSController");
                if (test == "step-below-offset") Box(test, new Vector3(0, worldStep*.4f, 4), new Vector3(10, worldStep*.8f, 4));
                if (test == "excessive-step") Box(test, new Vector3(0, 1, 4), new Vector3(10, 2, 4));
                if (test == "stairs") for (int i=0;i<6;i++) Box("step-"+i, new Vector3(0, worldStep*.4f*(i+1), 3+i*2.4f), new Vector3(10,worldStep*.8f*(i+1),2.4f));
                if (test == "ceiling") Box(test, new Vector3(0, worldHeight+1, 0), new Vector3(10, 1, 10));
                if (test.StartsWith("slope")) {
                    float angle = test == "slope-30" ? 30 : 60;
                    Box(test, new Vector3(0, 2, 6), new Vector3(10,.5f,10), -angle);
                }
                if (test == "wall") Box(test, new Vector3(0, 3, 3), new Vector3(10,6,.1f));
                Physics.SyncTransforms();
                cc.Move(Vector3.down*.2f);
                var row = new Row { name=test, simulatedHz=hz, stepOffset=cc.stepOffset, slopeLimit=cc.slopeLimit,
                    start=actor.transform.position, minY=actor.transform.position.y, maxY=actor.transform.position.y, finite=true,worldHeight=worldHeight,worldStepOffset=worldStep };
                for(int frame=0;frame<hz*3;frame++) {
                    float dt=1f/hz;
                    float direction=(test=="stairs"||test.StartsWith("slope")) && frame>=hz*1.5f ? -1 : 1;
                    Vector3 delta = test=="ceiling" ? Vector3.up*(frame<hz/3 ? 12 : -9.81f)*dt : Vector3.forward*direction*15*dt+Vector3.down*9.81f*dt;
                    var flags=cc.Move(delta); var p=actor.transform.position;
                    row.above |= (flags & CollisionFlags.Above)!=0;
                    row.below |= (flags & CollisionFlags.Below)!=0;
                    row.sides |= (flags & CollisionFlags.Sides)!=0;
                    row.maxY=Mathf.Max(row.maxY,p.y); row.minY=Mathf.Min(row.minY,p.y);
                    row.finite &= !(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude));
                }
                row.end=actor.transform.position;
                bool ok = row.finite;
                if(test=="ceiling") ok &= row.above && row.below && row.maxY<1 && row.end.y<.2f;
                else if(test=="wall"||test=="excessive-step") ok &= row.sides && row.end.z<3;
                else if(test=="step-below-offset") ok &= row.maxY>.15f && row.end.z>6;
                else if(test=="stairs") ok &= row.maxY>1 && row.end.y<.2f;
                else if(test=="slope-30") ok &= row.maxY>2 && row.end.y<.2f;
                else if(test=="slope-60") ok &= row.maxY<.2f && row.sides;
                row.status=ok?"PASS":"FAIL"; rows.Add(row);
            } finally { UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
        }

        var arguments=Environment.GetCommandLineArgs();int outputIndex=Array.IndexOf(arguments,"-visualEvidence");
        string output=outputIndex>=0?arguments[outputIndex+1]:FlatsDeveloperPaths.Reports;
        File.WriteAllText(Path.Combine(output,"controller-engine-fixture.json"),JsonUtility.ToJson(new Report{unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("o"),cases=rows.ToArray()},true));
        Debug.Log("FLATS_CONTROLLER_ENGINE_FIXTURE_RECORDED");
    }
}
