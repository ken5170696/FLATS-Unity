using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Supplemental constructor check only. No live gameplay, room or RPC writes.
public static class FlatsMapVoteInitializationAudit
{
    public static void Run()
    {
        var holder = new GameObject("MapVoteConstructorAudit");
        holder.SetActive(false);
        try
        {
            var menu = holder.AddComponent<Menu>();
            var votes = (IList)typeof(Menu).GetField("vote", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
            if (votes.Count != 6) throw new Exception("Map vote entries are not ready before UI initialization");
            for (int i = 0; i < votes.Count; i++)
            {
                var entry = votes[i];
                var type = entry.GetType();
                if ((int)type.GetField("mapKey").GetValue(entry) != i || (int)type.GetField("mapValue").GetValue(entry) != 0)
                    throw new Exception("Unexpected initial map vote key/count");
            }
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-voteAuditOutput");
            File.WriteAllText(args[index+1], "{\"status\":\"VERIFIED_PASS\",\"entries\":6,\"beforeUiInitialization\":true,\"runtimeRpcInvoked\":false,\"boundary\":\"Supplemental inactive Editor fixture constructor inspection; actual Photon smoke required separately\"}");
        }
        finally { UnityEngine.Object.DestroyImmediate(holder); }
    }
}
