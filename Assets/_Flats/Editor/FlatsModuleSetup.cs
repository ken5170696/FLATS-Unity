using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Flats.UI;

public static class FlatsModuleSetup
{
    const string Composition="Assets/_Flats/Prefabs/UI/GameInterface.prefab";
    const string Screen="Assets/_Flats/Prefabs/UI/ModulesScreen.prefab";
    static Font font;
    static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    static Text Label(string name,Transform parent,string text,Vector2 position,Vector2 size,int fontSize=22)
    {
        var t=Rect(name,parent,position,size).gameObject.AddComponent<Text>();t.font=font;t.text=text;t.fontSize=fontSize;
        t.color=Color.white;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;
    }
    static Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size)
    {
        var r=Rect(name,parent,position,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.18f,.24f,.3f,.98f);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        Label("Label",r,text,Vector2.zero,size-Vector2.one*12).alignment=TextAnchor.MiddleCenter;return b;
    }
    [MenuItem("Flats/Modules/Install UI")]
    public static void Install()
    {
        if(EditorApplication.isPlaying || UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Editor must be idle with a clean scene");
        var root=PrefabUtility.LoadPrefabContents(Composition);
        try
        {
            if(root.GetComponent<ModulePageBinding>()!=null)throw new Exception("Module UI already installed; edit the existing prefab");
            font=root.GetComponentsInChildren<Text>(true).First(t=>t.font!=null).font;
            var menu=root.GetComponentInChildren<Menu>(true);
            var main=menu.transform.GetChild(2);
            // Dump verified binding paths once, for reproducible asset review.
            Debug.Log("MODULE_BINDING main="+main.name+" menu="+menu.name+" reticle="+root.transform.GetChild(1).GetChild(7).name);
            var entry=Button("OpenModules",main,"Modules",new Vector2(0,-200),new Vector2(220,52));
            var panel=Rect("ModulesScreen",null,Vector2.zero,new Vector2(960,540));
            var bg=panel.gameObject.AddComponent<Image>();bg.color=new Color(.045f,.065f,.085f,.99f);
            var page=panel.gameObject.AddComponent<ModuleManagementPage>();
            Label("Title",panel,"INSTALLED MODULES",new Vector2(-190,225),new Vector2(520,50),30);
            page.back=Button("ModulesBack",panel,"Back",new Vector2(365,225),new Vector2(160,48));
            var rows=Rect("ModuleList",panel,new Vector2(-335,0),new Vector2(220,370));
            var layout=rows.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=12;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;
            page.rows=rows;page.rowTemplate=Button("ModuleRowTemplate",rows,"Module",Vector2.zero,new Vector2(220,80));
            page.rowTemplate.gameObject.AddComponent<LayoutElement>().preferredHeight=80;page.rowTemplate.gameObject.SetActive(false);
            page.description=Label("Description",panel,"",new Vector2(120,136),new Vector2(610,125),18);
            page.status=Label("Status",panel,"",new Vector2(75,25),new Vector2(510,90),18);
            page.enable=Button("ModuleEnable",panel,"Enable",new Vector2(358,28),new Vector2(135,48));
            var settings=Rect("CrosshairSettings",panel,new Vector2(110,-100),new Vector2(620,165));page.crosshairPanel=settings.gameObject;
            page.settingsLabel=Label("SettingsLabel",settings,"",new Vector2(-55,55),new Vector2(500,55),20);
            page.style=Button("CrosshairStyle",settings,"Style",new Vector2(-210,-8),new Vector2(120,48));
            page.smaller=Button("CrosshairSmaller",settings,"-",new Vector2(-105,-8),new Vector2(65,48));
            page.larger=Button("CrosshairLarger",settings,"+",new Vector2(-30,-8),new Vector2(65,48));
            page.reset=Button("CrosshairDefaults",settings,"Defaults",new Vector2(-180,-66),new Vector2(180,42));
            var preview=Rect("CrosshairPreview",settings,new Vector2(200,-20),new Vector2(90,90));
            page.preview=preview.gameObject.AddComponent<CrosshairGraphic>();page.preview.raycastTarget=false;
            preview.localScale=Vector3.one*1.25f;
            page.notice=Label("Notice",panel,"",new Vector2(0,-232),new Vector2(880,50),16);
            foreach(Transform t in panel.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            foreach(Transform t in entry.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            PrefabUtility.SaveAsPrefabAsset(panel.gameObject,Screen);UnityEngine.Object.DestroyImmediate(panel.gameObject);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Screen),menu.transform);
            instance.transform.localScale=Vector3.one*.8f;
            page=instance.GetComponent<ModuleManagementPage>();page.mainScreen=main.gameObject;page.entry=entry;page.mainOptions=menu.transform.GetChild(12).gameObject;
            PrefabUtility.RecordPrefabInstancePropertyModifications(page);instance.SetActive(false);
            root.AddComponent<ModulePageBinding>().page=page;
            root.transform.GetChild(1).GetChild(7).gameObject.AddComponent<LocalCrosshairPresenter>();
            PrefabUtility.SaveAsPrefabAsset(root,Composition);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();Debug.Log("MODULE_UI_INSTALLED");
    }
}
