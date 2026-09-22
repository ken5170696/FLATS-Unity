using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
public static class FlatsAssetRepair {
 public static void Run() {
  using(var w=new StreamWriter("Builds/script-bindings.txt")) {
   foreach(var guid in AssetDatabase.FindAssets("t:MonoScript",new[]{"Assets/Scripts"})) {
    var path=AssetDatabase.GUIDToAssetPath(guid);var s=AssetDatabase.LoadAssetAtPath<MonoScript>(path);var t=s.GetClass();
    w.WriteLine(path+" => "+(t==null?"NULL":t.FullName));
   }
  }
  foreach(var guid in AssetDatabase.FindAssets("t:AnimatorController")) {
   var path=AssetDatabase.GUIDToAssetPath(guid);var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
   foreach(var layer in c.layers) {
    var sm=layer.stateMachine;var seen=new HashSet<AnimatorState>();var q=new Queue<AnimatorState>();
    foreach(var v in sm.states) {seen.Add(v.state);q.Enqueue(v.state);}
    Action<AnimatorState> add=s=>{if(s!=null && seen.Add(s)){sm.AddState(s,new Vector3(seen.Count*220,0,0));q.Enqueue(s);}};
    add(sm.defaultState);foreach(var t in sm.anyStateTransitions)add(t.destinationState);
    while(q.Count>0)foreach(var t in q.Dequeue().transitions)add(t.destinationState);
    EditorUtility.SetDirty(sm);
   }
   EditorUtility.SetDirty(c);
  }
  AssetDatabase.SaveAssets();
 }
}
