using System;
using UnityEngine;

// Explicit isolated integration input. Feeds the normal local controller branch;
// it cannot set transforms, health, ammo, hit results or network replication state.
public static class FlatsVerificationInput
{
    [Serializable] public struct Sample
    {
        public float forward,strafe,lookX,lookY;
        public int milliseconds;
        public bool fire,reload;
    }
    public static readonly bool Enabled=Authorized();
    static Sample current;
    static float expires;
    static bool sentLook;
    static bool Authorized()
    {
        var args=Environment.GetCommandLineArgs();
        return Array.IndexOf(args,"-flats-verify")>=0 && Array.IndexOf(args,"-flats-gameplay-test")>=0 && Array.IndexOf(args,"-flats-module-settings-dir")>=0;
    }
    public static void Dispatch(string json)
    {
        if(!Enabled || Menu.current!="Playing")throw new InvalidOperationException("Isolated gameplay verification is not enabled");
        var sample=JsonUtility.FromJson<Sample>(json);
        foreach(float value in new[]{sample.forward,sample.strafe,sample.lookX,sample.lookY})if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Invalid input");
        sample.forward=Mathf.Clamp(sample.forward,-1,1);sample.strafe=Mathf.Clamp(sample.strafe,-1,1);
        sample.lookX=Mathf.Clamp(sample.lookX,-3600,3600);sample.lookY=Mathf.Clamp(sample.lookY,-3600,3600);
        current=sample;expires=Time.realtimeSinceStartup+Mathf.Clamp(sample.milliseconds,1,2000)/1000f;sentLook=false;
    }
    public static bool TrySample(out Sample sample)
    {
        sample=default(Sample);
        if(expires<=Time.realtimeSinceStartup)return false;
        sample=current;
        if(sentLook){sample.lookX=0;sample.lookY=0;sample.reload=false;}
        sentLook=true;return true;
    }
}
