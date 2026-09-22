using System.IO;
public static class FlatsDeveloperPaths
{
    public static string Reports { get { var path=Path.GetFullPath("Logs/Verification"); Directory.CreateDirectory(path); return path; } }
}
