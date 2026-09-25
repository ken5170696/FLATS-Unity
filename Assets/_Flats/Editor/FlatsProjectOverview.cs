using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Everyday authoring entry points. No build, migration, or network side effects.
public sealed class FlatsProjectOverview : EditorWindow
{
    Vector2 scroll;
    const string Root="Assets/_Flats/";

    [MenuItem("FLATS/Project Overview", priority=0)]
    public static void Open() => GetWindow<FlatsProjectOverview>("FLATS Project");

    void OnGUI()
    {
        scroll=EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("FLATS",EditorStyles.largeLabel);
        EditorGUILayout.HelpBox("Start with MainMenu to exercise navigation. Edit shared UI in Prefab Mode; use scene instances for map-specific composition.",MessageType.Info);
        GUILayout.Label("Scenes",EditorStyles.boldLabel);
        Scene("MainMenu","Game entry and menu navigation");
        Scene("Tutorial","First-time player flow");
        foreach(var map in new[]{"Warehouse","NightLand","FlatCity","UrbanPark","DepartmentStore","BeachsideTown"})
            Scene(map,"Map geometry, lighting, spawns and navigation");
        GUILayout.Space(12);
        GUILayout.Label("Shared presentation",EditorStyles.boldLabel);
        Prefab("GameInterface","Scene composition, menu controller, cameras and shared screens");
        Prefab("MainMenuScreen","Main menu content");
        Prefab("SettingsScreen","Sound, graphics and input settings");
        Prefab("ModulesScreen","Module library, details, configuration and dialogs");
        Prefab("GameplayHUD","In-game information and controls");
        Prefab("ConfirmationDialog","Shared confirmation UI");
        Prefab("PauseScreen","Paused game navigation");
        GUILayout.Space(12);
        GUILayout.Label("Authoring guides",EditorStyles.boldLabel);
        Document("docs/PROJECT_GUIDE.md","Project map and editing responsibilities");
        Document("docs/ASSET_WORKFLOW.md","Asset identity, imports and delivery");
        Document("docs/RENDERING.md","Rendering and camera composition");
        EditorGUILayout.EndScrollView();
    }

    static void Scene(string name,string purpose)
    {
        string path=Root+"Scenes/"+name+".unity";
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if(GUILayout.Button(name,GUILayout.Width(160)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(path);
            GUILayout.Label(purpose,EditorStyles.wordWrappedLabel);
        }
    }

    static void Prefab(string name,string purpose)
    {
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button(name,GUILayout.Width(160)))
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/UI/"+name+".prefab"));
            GUILayout.Label(purpose,EditorStyles.wordWrappedLabel);
        }
    }

    static void Document(string path,string label)
    {
        if(GUILayout.Button(label))EditorUtility.RevealInFinder(System.IO.Path.GetFullPath(path));
    }
}
