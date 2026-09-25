using System;
using System.IO;
using UnityEngine;

public partial class Menu
{
    const string ShareText = "Play FLATS — https://flats-site.tail2511fc.ts.net/";
#if UNITY_WEBGL && !UNITY_EDITOR
    FlatsBrowserSaveTransfer browserShareTransfer;
#endif

    void ShareGame()
    {
        if(VRmode)
        {
            ShowConfirm("Share unavailable in VR", "Exit VR mode to save the image and copy the website link.", null, "OK", null);
            return;
        }
        try
        {
            byte[] png = EncodeShareImage();
#if UNITY_WEBGL && !UNITY_EDITOR
            if(browserShareTransfer == null)
            {
                browserShareTransfer = FlatsBrowserSaveTransfer.Create(transform);
                browserShareTransfer.gameObject.name = "FlatsShareTransfer-" + GetInstanceID();
            }
            browserShareTransfer.DownloadPng("FLATS-Share.png", png, ShareText, (status, error) =>
            {
                if(status == "cancel") return;
                ShowShareResult(status == "download" ? "Download requested" : "Share failed",
                    status == "download" ? "Check your browser downloads for FLATS-Share.png. The browser does not confirm whether you kept the file." : error);
            });
#else
            string directory = Path.Combine(FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath, "Shares");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "FLATS-Share-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            // CreateNew preserves earlier images if two requests share a timestamp.
            using(var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                output.Write(png, 0, png.Length);
            ShowShareResult("Share image saved", "PNG saved to:\n" + path + "\nUse your device's files app to attach this image.");
#endif
        }
        catch(Exception error) { ShowShareResult("Share failed", "Could not export the image: " + error.Message); }
    }

    byte[] EncodeShareImage()
    {
        if(flatsLogo == null || flatsLogo.texture == null) throw new InvalidOperationException("Share image is unavailable.");
        var source = flatsLogo.texture;
        // Imported textures need not be CPU-readable. Copy through the GPU instead.
        var previous = RenderTexture.active;
        RenderTexture rendered = null;
        Texture2D readable = null;
        try
        {
            rendered = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rendered);
            RenderTexture.active = rendered;
            readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            var png = readable.EncodeToPNG();
            if(png == null || png.Length == 0) throw new InvalidOperationException("Image encoding failed.");
            return png;
        }
        finally
        {
            RenderTexture.active = previous;
            if(rendered != null) RenderTexture.ReleaseTemporary(rendered);
            if(readable != null) Destroy(readable);
        }
    }

    void ShowShareResult(string title, string detail)
    {
        ShowConfirm(title, detail + "\n\n" + ShareText, CopyShareText, "Copy link", "Close");
    }

    void CopyShareText(bool confirmed)
    {
        if(!confirmed) return;
        try
        {
            GUIUtility.systemCopyBuffer = ShareText;
            if(GUIUtility.systemCopyBuffer != ShareText) throw new InvalidOperationException("Clipboard access was not confirmed.");
            ShowConfirm("Share text copied", ShareText, null, "OK", null);
        }
        catch(Exception error)
        {
            ShowConfirm("Could not copy", error.Message + "\n\n" + ShareText + "\nCopy the website address manually.", null, "OK", null);
        }
    }
}
