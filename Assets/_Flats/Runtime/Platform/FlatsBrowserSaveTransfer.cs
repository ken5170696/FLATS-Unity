#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Browser callbacks are scoped to this menu instance and invalidated on destruction.
public sealed class FlatsBrowserSaveTransfer : MonoBehaviour
{
    [Serializable] sealed class Reply { public string token, status, name, data, error; }
    [DllImport("__Internal")] static extern void FlatsSaveUpload(string target, string token);
    [DllImport("__Internal")] static extern void FlatsSaveDownload(string target, string token, string name, string json);
    [DllImport("__Internal")] static extern void FlatsShareDownload(string target, string token, string name, string png, string text);
    [DllImport("__Internal")] static extern void FlatsSaveFlush(string target, string token);
    [DllImport("__Internal")] static extern void FlatsSaveCancel(string target);
    string token;
    public bool IsFlushing { get; private set; }
    Action<byte[], string> imported;
    Action<string, string> completed;

    public static FlatsBrowserSaveTransfer Create(Transform parent)
    {
        var host = new GameObject("FlatsSaveTransfer-" + parent.GetInstanceID());
        host.transform.SetParent(parent, false);
        return host.AddComponent<FlatsBrowserSaveTransfer>();
    }

    void Begin(Action<string, string> completion)
    {
        if(IsFlushing)throw new InvalidOperationException("Browser save is still pending. Wait for completion before starting another transfer.");
        FlatsSaveCancel(gameObject.name);
        token = Guid.NewGuid().ToString("N");
        imported = null;
        completed = completion;
    }

    public void Upload(Action<byte[], string> receive, Action<string, string> completion)
    {
        Begin(completion);
        imported = receive;
        FlatsSaveUpload(gameObject.name, token);
    }

    public void Download(string filename, string json, Action<string, string> completion)
    {
        Begin(completion);
        FlatsSaveDownload(gameObject.name, token, filename, json);
    }

    public void DownloadPng(string filename, byte[] png, string text, Action<string, string> completion)
    {
        if(png == null || png.Length == 0 || png.Length > 16 * 1024 * 1024)
            throw new ArgumentException("Share image must be between 1 byte and 16 MiB.");
        Begin(completion);
        FlatsShareDownload(gameObject.name, token, filename, Convert.ToBase64String(png), text);
    }

    public void Flush(Action<string, string> completion)
    {
        Begin(completion);
        IsFlushing=true;
        try { FlatsSaveFlush(gameObject.name, token); }
        catch { IsFlushing=false;token=null;completed=null;throw; }
    }

    [UnityEngine.Scripting.Preserve]
    public void OnBrowserSaveReply(string json)
    {
        Reply reply;
        try { reply = JsonUtility.FromJson<Reply>(json); }
        catch { return; }
        if (reply == null || reply.token != token) return;
        var receive = imported;
        var completion = completed;
        token = null; imported = null; completed = null;IsFlushing=false;
        if (reply.status == "file")
        {
            try
            {
                if (reply.data == null || reply.data.Length > ((FlatsSaveTransfer.MaximumBytes + 2) / 3) * 4)
                    throw new InvalidOperationException("Save file is too large.");
                receive(Convert.FromBase64String(reply.data), reply.name);
            }
            catch (Exception error) { completion("error", error.Message); }
        }
        else completion(reply.status, reply.error);
    }

    void OnDestroy()
    {
        token = null; imported = null; completed = null;IsFlushing=false;
        FlatsSaveCancel(gameObject.name);
    }
}
#endif
