using System.Collections.Generic;
using UnityEngine;

public static class FlatsOfflineScores
{
    public sealed class BotScore { public string name; public int kills,deaths,team,instanceId; public AI actor; }
    public static readonly List<BotScore> Bots = new List<BotScore>();
    public static bool FreeForAll { get { return PhotonNetwork.offlineMode && Menu.gameState=="Multiplayer" && Multiplayer.rule==1; } }
    public static void Reset() { Bots.Clear(); }
    public static void Register(AI actor)
    {
        if(!PhotonNetwork.offlineMode || Menu.gameState!="Multiplayer" || Multiplayer.rule==8)return;
        foreach(var score in Bots)if(score.actor==actor)return;
        foreach(var score in Bots)if(score.actor==null && score.team==actor.team){score.actor=actor;score.instanceId=actor.gameObject.GetInstanceID();return;}
        Bots.Add(new BotScore {name="Flatman Bot "+(Bots.Count+1),team=actor.team,actor=actor,instanceId=actor.gameObject.GetInstanceID()});
    }
    public static void Kill(Transform killer,GameObject victim)
    {
        if(!PhotonNetwork.offlineMode)return;
        if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-flats-verify")>=0)
            Debug.Log("FLATS_DEATH_EVENT victim="+victim.GetInstanceID()+" killer="+(killer==null?0:killer.gameObject.GetInstanceID())+" rule="+Multiplayer.rule+" time="+Time.realtimeSinceStartup);
        foreach(var score in Bots)
        {
            if(score.instanceId==victim.GetInstanceID())score.deaths++;
            if(killer!=null && score.instanceId==killer.gameObject.GetInstanceID())
            {
                score.kills++;
                var controller=Object.FindObjectOfType<Multiplayer>();
                if(FreeForAll && controller!=null && score.kills>=controller.detailedObjective)
                    controller.gameObject.GetPhotonView().RPC("GetTeamScore",PhotonTargets.All,new int[]{2,score.kills});
            }
        }
    }
}
