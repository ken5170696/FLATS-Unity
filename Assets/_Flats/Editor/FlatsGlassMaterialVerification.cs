using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class FlatsGlassMaterialVerification {
 [Serializable] class Report {public string utc,gpu,scope="GPU material property block contract only; not glass gameplay acceptance";public string expected,actual,sharedColorAfter;public bool pass;}
 public static void Run(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.transform.position=new Vector3(0,0,2);
  var mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Flats/Art/Shared/Materials/BrokenGlass.mat"));mat.mainTexture=Texture2D.whiteTexture;mat.color=Color.white;
  var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=mat;var pb=new MaterialPropertyBlock();pb.SetColor("_Color",Color.green);renderer.SetPropertyBlock(pb);
  var camera=new GameObject("camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=1;
  var rt=new RenderTexture(32,32,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
  var pixels=new Texture2D(32,32,TextureFormat.RGBA32,false,true);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();var actual=pixels.GetPixel(16,16);
  var report=new Report{utc=DateTime.UtcNow.ToString("o"),gpu=SystemInfo.graphicsDeviceName,expected=Color.green.ToString(),actual=actual.ToString(),sharedColorAfter=mat.color.ToString(),pass=actual.g>.9f&&actual.r<.05f&&actual.b<.05f&&mat.color==Color.white};
  string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));string ev=FlatsDeveloperPaths.Reports;File.WriteAllText(Path.Combine(ev,"glass-material-gpu.json"),JsonUtility.ToJson(report,true));
  camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(mat);EditorApplication.Exit(report.pass?0:1);
 }
}
