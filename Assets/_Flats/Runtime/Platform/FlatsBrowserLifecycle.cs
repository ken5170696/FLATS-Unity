#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

// Browser UI and persistent storage integration required during normal play.
public sealed class FlatsBrowserLifecycle : MonoBehaviour
{
    [DllImport("__Internal")] static extern void FlatsGameplayState(int playing);
    [DllImport("__Internal")] static extern void FlatsStorageMonitor();
    int lastPlaying = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var host = new GameObject("Browser lifecycle");
        DontDestroyOnLoad(host);
        host.AddComponent<FlatsBrowserLifecycle>();
    }

    void Awake() { FlatsStorageMonitor(); }

    void Update()
    {
        int playing = Menu.current == "Playing" ? 1 : 0;
        if (playing == lastPlaying) return;
        FlatsGameplayState(playing);
        lastPlaying = playing;
    }
}
#endif
