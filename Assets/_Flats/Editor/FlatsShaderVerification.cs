using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// GPU contract checks, not a claim of visual equivalence to the original binaries.
public static class FlatsShaderVerification
{
    [Serializable] public class Check { public string name, detail; public bool pass; }
    [Serializable] public class Report { public string gpu; public List<Check> checks = new List<Check>(); }
    static Report report;
    static void Record(string name, bool pass, string detail) { report.checks.Add(new Check { name=name, pass=pass, detail=detail }); }
    static Texture2D Solid(Color c) { var t=new Texture2D(8,8,TextureFormat.RGBAFloat,false,true); var pixels=new Color[64]; for(int i=0;i<64;i++)pixels[i]=c; t.SetPixels(pixels);t.Apply();return t; }
    static void Pixel(string shader, Color input, Color expected, Action<Material> setup=null, int pass=0)
    {
        var material=new Material(Shader.Find(shader)); var source=Solid(input);
        var target=RenderTexture.GetTemporary(8,8,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var old=RenderTexture.active; var read=new Texture2D(8,8,TextureFormat.RGBAFloat,false,true);
        try {
            if(setup!=null)setup(material);
            Graphics.Blit(source,target,material,pass); RenderTexture.active=target;
            read.ReadPixels(new Rect(0,0,8,8),0,0);read.Apply();var actual=read.GetPixel(4,4);
            float error=Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b),Mathf.Abs(actual.a-expected.a));
            Record(shader+" pass "+pass,error<0.025f,"expected="+expected+" actual="+actual+" error="+error);
        } finally { RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(read);UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(material); }
    }
    public static void Run()
    {
        report=new Report { gpu=SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType };
        try {
            foreach(var guid in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/_Flats/Art/Shared/Shaders","Assets/Resources"})) {
                var path=AssetDatabase.GUIDToAssetPath(guid);var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
                string detail=shader==null?"missing":shader.name;
                if(shader!=null)foreach(var message in ShaderUtil.GetShaderMessages(shader))detail+=" | "+message.message+" line "+message.line;
                Record(path,shader!=null && shader.isSupported && !ShaderUtil.ShaderHasError(shader),detail);
            }
            var red=new Color(1,0,0,0.6f);
            Pixel("Hidden/CC_Grayscale",red,new Color(.3f,.3f,.3f,.6f));
            Pixel("Hidden/CC_Grayscale",red,red,m=>m.SetVector("_Data",new Vector4(.3f,.59f,.11f,0)));
            var neutral=new Color(.2f,.4f,.6f,1);
            Pixel("Hidden/FxPro",neutral,neutral,m=>m.SetFloat("_SCurveIntensity",0));
            Pixel("Hidden/FxProTap",neutral,neutral);
            Pixel("Hidden/Amplify Motion/Combine",neutral,neutral);
            var oldDepth=Shader.GetGlobalTexture("_CameraDepthTexture");var oldZ=Shader.GetGlobalVector("_ZBufferParams");
            var depth=Solid(Color.white);
            try {
                Shader.SetGlobalTexture("_CameraDepthTexture",depth);Shader.SetGlobalVector("_ZBufferParams",new Vector4(0,1,0,1));
                Pixel("Hidden/DOFPro",neutral,Color.clear,m=>{m.SetFloat("_OneOverDepthScale",2);m.SetFloat("_FocalDist",2);m.SetFloat("_FocalLength",1);});
                Pixel("Hidden/DOFPro",neutral,new Color(.5f,.5f,.5f,.5f),m=>{m.SetFloat("_OneOverDepthScale",2);m.SetFloat("_FocalDist",1.5f);m.SetFloat("_FocalLength",1);});
            } finally {Shader.SetGlobalTexture("_CameraDepthTexture",oldDepth);Shader.SetGlobalVector("_ZBufferParams",oldZ);UnityEngine.Object.DestroyImmediate(depth);}
            var zero=Solid(new Color(.5f,.5f,0,1));
            try {
                foreach(var name in new[]{"Hidden/Amplify Motion/MotionBlurSM2","Hidden/Amplify Motion/MotionBlurSM3"})
                    for(int pass=0;pass<8;pass++)Pixel(name,neutral,neutral,m=>{m.SetTexture("_MotionTex",zero);m.SetVector("_AM_BLUR_STEP",Vector4.one);},pass);
            } finally { UnityEngine.Object.DestroyImmediate(zero); }
        } catch(Exception e) { Record("exception",false,e.ToString()); }
        string pathOut=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/parity-shader-gpu.json"));
        File.WriteAllText(pathOut,JsonUtility.ToJson(report,true));
        bool passed=report.checks.TrueForAll(x=>x.pass);
        Debug.Log("FLATS_SHADER_GPU_"+(passed?"PASS":"FAIL")+" checks="+report.checks.Count);
        EditorApplication.Exit(passed?0:1);
    }
}
