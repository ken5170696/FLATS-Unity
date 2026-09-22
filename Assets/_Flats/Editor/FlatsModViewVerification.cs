using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only resolution selection for repeatable Game View checks, with restoration support.
public static class FlatsModViewVerification
{
    const string OriginalKey="Flats.ModQa.OriginalView";
    public static void Size(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=sizesType.GetProperty("currentGroup",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sizes);
        var groupType=group.GetType();
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,new object[]{Enum.Parse(kind,"FixedResolution"),width,height,"FLATS Mod QA "+width+"x"+height});
        groupType.GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int total=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null);
        var viewType=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);
        var index=viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(SessionState.GetInt(OriginalKey,-1)<0)SessionState.SetInt(OriginalKey,(int)index.GetValue(view));index.SetValue(view,total-1);view.Repaint();
    }
    public static void Restore()
    {
        var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(t);
        t.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,SessionState.GetInt(OriginalKey,0));SessionState.EraseInt(OriginalKey);
        var sizesType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=sizesType.GetProperty("currentGroup",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sizes);var groupType=group.GetType();
        int builtins=(int)groupType.GetMethod("GetBuiltinCount").Invoke(group,null),count=(int)groupType.GetMethod("GetCustomCount").Invoke(group,null);
        for(int i=count-1;i>=0;i--)
        {
            var size=groupType.GetMethod("GetGameViewSize").Invoke(group,new object[]{builtins+i});
            string name=(string)size.GetType().GetProperty("baseText").GetValue(size);
            if(name.StartsWith("FLATS Mod QA ",StringComparison.Ordinal))groupType.GetMethod("RemoveCustomSize").Invoke(group,new object[]{i});
        }
    }
}
