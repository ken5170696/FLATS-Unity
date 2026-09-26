using System;
using System.IO;
using UnityEngine;

// The 128x128 avatar PNG that Photon room properties and the character screen read.
// The file can be missing (cleaned profile folder, failed first write) or unreadable;
// callers get the default icon instead of an unhandled exception during room entry.
public static class FlatsUserIcon
{
    public const string FileName = "Flats_UserIcon.png";
    const string LegacyFileName = "UserIcon.png";

    public static string Root => FlatsPreferences.IsolatedRoot ?? Application.persistentDataPath;
    public static string FilePath => Path.Combine(Root, FileName);

    // Returns the stored avatar, or writes and returns the default icon when the stored one
    // is missing, unreadable or smaller than 128x128. Never throws for file-system errors.
    public static byte[] Read(Texture2D defaultIcon)
    {
        byte[] bytes = TryRead();
        if (bytes != null && IsUsable(bytes)) return bytes;
        return ResetToDefault(defaultIcon) ?? bytes ?? new byte[0];
    }

    public static bool Exists() => File.Exists(FilePath);

    public static void Write(byte[] png)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllBytes(FilePath, png);
    }

    static byte[] TryRead()
    {
        try
        {
            string path = FilePath;
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    static bool IsUsable(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return false;
        var probe = new Texture2D(2, 2);
        try
        {
            return probe.LoadImage(bytes) && probe.width >= 128 && probe.height >= 128;
        }
        finally
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(probe);
            else UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    static byte[] ResetToDefault(Texture2D defaultIcon)
    {
        if (defaultIcon == null) return null;
        byte[] png;
        try { png = defaultIcon.EncodeToPNG(); }
        catch (Exception) { return null; }
        try
        {
            string legacy = Path.Combine(Root, LegacyFileName);
            if (File.Exists(legacy)) File.Delete(legacy);
            Write(png);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return png;
    }
}
