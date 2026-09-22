using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Render the real sight prefabs against overlapping coloured geometry. A colour-only
// render target can pass shader compilation while losing all scene occlusion.
public static class FlatsSightValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var results=new List<string>();bool pass=true;
        foreach(var name in new[]{"reflex sight","2x sight","4x sight","6x sight","8x sight"})
        {
            var sight=FlatsSightTarget.Create("sights/"+name);
            var camera=sight.GetComponentInChildren<Camera>(true);
            if(camera==null){pass=false;results.Add("FAIL "+name+": missing camera");UnityEngine.Object.DestroyImmediate(sight);continue;}
            camera.gameObject.SetActive(true);camera.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var target=camera.targetTexture;
            if(target==null){pass=false;results.Add("FAIL "+name+": missing target");UnityEngine.Object.DestroyImmediate(sight);continue;}
            var panels=new List<GameObject>();var materials=new List<Material>();
            // Near red panel is deliberately submitted before a farther blue wall.
            for(int i=0;i<2;i++)
            {
                var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panels.Add(panel);
                panel.transform.position=new Vector3(i==0?-.3f:0,0,i==0?5:10);
                panel.transform.localScale=new Vector3(i==0?.6f:20,20,.1f);
                var material=new Material(Shader.Find("Unlit/Color"));materials.Add(material);
                material.color=i==0?Color.red:Color.blue;material.renderQueue=2000+i;
                panel.GetComponent<Renderer>().sharedMaterial=material;
            }
            camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
            var read=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            read.ReadPixels(new Rect(0,0,target.width,target.height),0,0);read.Apply();RenderTexture.active=old;
            int red=0,blue=0;foreach(var p in read.GetPixels()){if(p.r>.8f&&p.b<.2f)red++;if(p.b>.8f&&p.r<.2f)blue++;}
            var display=sight.GetComponentInChildren<RawImage>(true);
            bool valid=target.depth>=16&&red>100&&blue>100&&display.uvRect==new Rect(0,0,1,1);
            var distortion=camera.GetComponent<Fisheye>();
            if(name!="reflex sight")valid&=distortion!=null&&distortion.fishEyeShader!=null&&distortion.enabled;
            else
            {
                var correction=camera.GetComponent<ColorCorrectionCurves>();
                valid&=correction!=null&&correction.enabled&&correction.simpleColorCorrectionCurvesShader!=null;
            }
            // Exercise RawImage's actual mesh generation as well as its serialized
            // Rect. Zero UV extent turns a healthy camera output into one flat colour.
            using(var vertices=new VertexHelper())
            {
                typeof(RawImage).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(display,new object[]{vertices});
                var a=new UIVertex();var b=new UIVertex();vertices.PopulateUIVertex(ref a,0);vertices.PopulateUIVertex(ref b,2);
                valid&=Mathf.Abs(a.uv0.x-b.uv0.x)>.99f&&Mathf.Abs(a.uv0.y-b.uv0.y)>.99f;
            }
            // Activate the normally hidden lens exactly as aiming does, and
            // render its real mask/material/RawImage chain into a second target.
            var canvas=sight.GetComponentInChildren<Canvas>(true);canvas.gameObject.SetActive(true);
            canvas.transform.SetPositionAndRotation(new Vector3(1000,0,0),Quaternion.identity);canvas.transform.localScale=Vector3.one;
            var viewer=new GameObject("Sight UI validation camera").AddComponent<Camera>();
            viewer.transform.position=new Vector3(1000,0,-5);viewer.orthographic=true;viewer.orthographicSize=.15f;
            viewer.cullingMask=1<<13;viewer.clearFlags=CameraClearFlags.SolidColor;viewer.backgroundColor=Color.black;
            var presented=new RenderTexture(256,256,24);presented.Create();viewer.targetTexture=presented;
            canvas.worldCamera=viewer;Canvas.ForceUpdateCanvases();viewer.Render();
            RenderTexture.active=presented;read.ReadPixels(new Rect(0,0,256,256),0,0);read.Apply();RenderTexture.active=old;
            int uiRed=0,uiBlue=0;foreach(var p in read.GetPixels()){if(p.r>.8f&&p.b<.2f)uiRed++;if(p.b>.8f&&p.r<.2f)uiBlue++;}
            valid&=uiRed>100&&uiBlue>100;
            pass&=valid;
            results.Add((valid?"PASS ":"FAIL ")+name+" uv="+display.uvRect+" depth="+target.depth+" camera red/blue="+red+"/"+blue+" displayed red/blue="+uiRed+"/"+uiBlue+"; RawImage mesh UV coverage checked");
            Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/sight-"+name+".png",read.EncodeToPNG());
            foreach(var p in panels)UnityEngine.Object.DestroyImmediate(p);
            foreach(var m in materials)UnityEngine.Object.DestroyImmediate(m);
            viewer.targetTexture=null;UnityEngine.Object.DestroyImmediate(viewer.gameObject);presented.Release();UnityEngine.Object.DestroyImmediate(presented);
            UnityEngine.Object.DestroyImmediate(read);UnityEngine.Object.DestroyImmediate(sight);
        }
        File.WriteAllLines("Logs/sight-validation.txt",results);EditorApplication.Exit(pass?0:1);
    }
}
