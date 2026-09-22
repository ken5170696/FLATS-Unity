using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class FlatsTagVerification {
 public static void ReimportTaggedAssets(){
  foreach(var guid in AssetDatabase.FindAssets("t:Prefab"))AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);
  foreach(var guid in AssetDatabase.FindAssets("t:Scene"))AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid),ImportAssetOptions.ForceUpdate);
  AssetDatabase.SaveAssets();Run();
 }
 [Serializable] public class Check {public string prefab,name,tag,expected;public bool pass;}
 [Serializable] public class Report {public string[] tags;public List<Check> checks=new List<Check>();}
 public static void Run(){
  var report=new Report{tags=UnityEditorInternal.InternalEditorUtility.tags};
  foreach(var name in new[]{"Flatman","Flatman_Enemy"}) {
   var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/"+name+".prefab");
   foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t==root.transform||t.name=="CameraTarget") {
    string expected=t==root.transform?(name=="Flatman"?"Player":"Enemy"):"Head";
    report.checks.Add(new Check{prefab=name,name=t.name,tag=t.tag,expected=expected,pass=t.tag==expected});
   }
  }
  string ev=FlatsDeveloperPaths.Reports;File.WriteAllText(Path.Combine(ev,"tag-audit.json"),JsonUtility.ToJson(report,true));EditorApplication.Exit(report.checks.TrueForAll(c=>c.pass)?0:1);
 }
}
