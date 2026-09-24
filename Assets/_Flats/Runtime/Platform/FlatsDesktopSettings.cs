using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class FlatsDesktopSettings : MonoBehaviour
{
    const string Prefix="Flats.Desktop.";
    readonly List<Vector2Int> sizes=new List<Vector2Int>();
    Transform page;
    int index;
    RectTransform reticle;
    Vector3 reticleScale;
    readonly Color[] crosshairColors={Color.white,Color.black,Color.red,Color.green,Color.cyan,Color.magenta};
    readonly string[] colorNames={"White","Black","Red","Green","Cyan","Magenta"};
    public void Initialize(Transform settingsPage)
    {
        page=settingsPage;
        AddSize(Screen.width,Screen.height);
        foreach(var size in Screen.resolutions)if(size.width>=1024 && size.height>=576)AddSize(size.width,size.height);
        if(FlatsPreferences.HasKey(Prefix+"Width") && Array.IndexOf(Environment.GetCommandLineArgs(),"-screen-width")<0)
        {
            int width=FlatsPreferences.GetInt(Prefix+"Width"),height=FlatsPreferences.GetInt(Prefix+"Height");
            int saved=sizes.IndexOf(new Vector2Int(width,height));
            if(saved>=0) {index=saved;Screen.SetResolution(width,height,FlatsPreferences.GetInt(Prefix+"Fullscreen",0)==1?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);}
        }
        QualitySettings.vSyncCount=FlatsPreferences.GetInt(Prefix+"VSync",QualitySettings.vSyncCount);
        // SettingsScreen.prefab authors both row sets on this page; desktop shows its own.
        ShowDesktopRows(true);
        var ui=GameObject.Find("UICamera");
        if(ui!=null && ui.transform.childCount>1 && ui.transform.GetChild(1).childCount>7){reticle=ui.transform.GetChild(1).GetChild(7) as RectTransform;if(reticle!=null)reticleScale=reticle.localScale;}
        ApplyCrosshair();
        Refresh();
    }
    static readonly string[] VrNames={"Resolution","EyeDistance","HeadRotation"};
    // The VR-Image page holds the VR rows and the desktop rows; only one set is shown.
    public void ShowDesktopRows(bool desktop)
    {
        foreach(var name in Names){var row=page.Find(name);if(row!=null)row.gameObject.SetActive(desktop);}
        foreach(var name in VrNames){var row=page.Find(name);if(row!=null)row.gameObject.SetActive(!desktop);}
    }
    static readonly string[] Names={"DesktopResolution","DesktopWindowMode","DesktopVSync","DesktopCrosshairColor","DesktopCrosshairSize"};
    void SetValue(int row,string value){var t=page.Find(Names[row]);if(t!=null)t.GetChild(1).GetComponent<Text>().text=value;}
    void AddSize(int width,int height){var size=new Vector2Int(width,height);if(!sizes.Contains(size))sizes.Add(size);}
    public void Change(Transform row,int direction)
    {
        bool fullscreen=Screen.fullScreen;
        if(row.name=="DesktopResolution") {index=(index+direction+sizes.Count)%sizes.Count;Screen.SetResolution(sizes[index].x,sizes[index].y,Screen.fullScreenMode);}
        else if(row.name=="DesktopWindowMode") {fullscreen=!fullscreen;Screen.fullScreenMode=fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;}
        else if(row.name=="DesktopVSync")QualitySettings.vSyncCount=QualitySettings.vSyncCount==0?1:0;
        // An enabled crosshair module replaces the original reticle, so these settings
        // would change nothing visible; they are shown as module-controlled instead.
        else if(ModuleCrosshair && (row.name=="DesktopCrosshairColor" || row.name=="DesktopCrosshairSize")) { Refresh(); return; }
        else if(row.name=="DesktopCrosshairColor")FlatsPreferences.SetInt(Prefix+"CrosshairColor",(FlatsPreferences.GetInt(Prefix+"CrosshairColor",0)+direction+6)%6);
        else if(row.name=="DesktopCrosshairSize")FlatsPreferences.SetInt(Prefix+"CrosshairSize",(FlatsPreferences.GetInt(Prefix+"CrosshairSize",1)+direction+4)%4);
        ApplyCrosshair();
        FlatsPreferences.SetInt(Prefix+"Width",sizes[index].x);FlatsPreferences.SetInt(Prefix+"Height",sizes[index].y);
        FlatsPreferences.SetInt(Prefix+"Fullscreen",fullscreen?1:0);
        FlatsPreferences.SetInt(Prefix+"VSync",QualitySettings.vSyncCount);FlatsPreferences.Save();
        Invoke("Refresh",0.2f);
    }
    static bool ModuleCrosshair { get { return Flats.UI.CrosshairPresentation.Appearance!=null; } }
    void ApplyCrosshair(){if(reticle==null)return;reticle.localScale=reticleScale*(0.75f+0.25f*FlatsPreferences.GetInt(Prefix+"CrosshairSize",1));if(FlatsPreferences.HasKey(Prefix+"CrosshairColor"))foreach(var image in reticle.GetComponentsInChildren<Image>(true)){var color=crosshairColors[FlatsPreferences.GetInt(Prefix+"CrosshairColor",0)%6];color.a=image.color.a;image.color=color;}}
    void Refresh(){SetValue(0,sizes[index].x+" x "+sizes[index].y);SetValue(1,Screen.fullScreen?"Fullscreen":"Windowed");SetValue(2,QualitySettings.vSyncCount==0?"OFF":"ON");SetValue(3,ModuleCrosshair?"Set in Mod center":colorNames[FlatsPreferences.GetInt(Prefix+"CrosshairColor",0)%6]);SetValue(4,ModuleCrosshair?"Set in Mod center":(75+25*FlatsPreferences.GetInt(Prefix+"CrosshairSize",1))+"%");}
}
