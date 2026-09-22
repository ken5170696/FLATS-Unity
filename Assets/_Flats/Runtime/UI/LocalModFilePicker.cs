using System;
using System.Runtime.InteropServices;


// Native chooser only; validation and installation remain in the existing package store.
internal static class LocalModFilePicker
{
    internal static string Choose()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Import a FLATS mod", "", "zip");
#elif UNITY_STANDALONE_WIN
        var dialog=new OpenFileName();dialog.size=Marshal.SizeOf(dialog);
        dialog.filter="FLATS mod packages (*.zip)\0*.zip\0\0";
        dialog.file=Marshal.AllocHGlobal(32768*2);dialog.maxFile=32768;Marshal.WriteInt16(dialog.file,0);dialog.owner=GetActiveWindow();
        dialog.title="Import a FLATS mod";dialog.flags=0x80000|0x1000|0x800|0x8;
        try { return GetOpenFileName(dialog)?Marshal.PtrToStringUni(dialog.file):""; }
        finally { Marshal.FreeHGlobal(dialog.file); }
#else
        return "";
#endif
    }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    sealed class OpenFileName
    {
        public int size;public IntPtr owner,instance;
        public string filter,customFilter;public int maxCustomFilter,filterIndex;
        public IntPtr file;public int maxFile;public string fileTitle;public int maxFileTitle;
        public string initialDirectory,title;public int flags;public short fileOffset,fileExtension;
        public string defaultExtension;public IntPtr customData,hook;public string template;
        public IntPtr reserved;public int reservedSize,flagsEx;
    }
    [DllImport("user32.dll")]static extern IntPtr GetActiveWindow();
    [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true,EntryPoint="GetOpenFileNameW")]
    [return:MarshalAs(UnmanagedType.Bool)]static extern bool GetOpenFileName([In,Out]OpenFileName dialog);
#endif
}
