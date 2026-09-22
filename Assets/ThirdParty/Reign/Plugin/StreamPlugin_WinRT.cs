using System;
using System.IO;
using System.Runtime.InteropServices;
namespace Reign.Plugin
{
	public class StreamPlugin_WinRT : IStreamPlugin
	{
		public delegate void InitNativeMethod(StreamPlugin_WinRT plugin);

		public IStreamPlugin Native;

		public static InitNativeMethod InitNative;

		public StreamPlugin_WinRT()
		{
			InitNative(this);
		}

		public void FileExists(string fileName, FolderLocations folderLocation, StreamExistsCallbackMethod callback)
		{
			Native.FileExists(fileName, folderLocation, callback);
		}

		public void DeleteFile(string fileName, FolderLocations folderLocation, StreamDeleteCallbackMethod callback)
		{
			Native.DeleteFile(fileName, folderLocation, callback);
		}

		public void SaveFile(string fileName, Stream stream, FolderLocations folderLocation, StreamSavedCallbackMethod streamSavedCallback)
		{
			Native.SaveFile(fileName, stream, folderLocation, streamSavedCallback);
		}

		public void SaveFile(string fileName, byte[] data, FolderLocations folderLocation, StreamSavedCallbackMethod streamSavedCallback)
		{
			Native.SaveFile(fileName, data, folderLocation, streamSavedCallback);
		}

		public void LoadFile(string fileName, FolderLocations folderLocation, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			Native.LoadFile(fileName, folderLocation, streamLoadedCallback);
		}

		public void SaveFileDialog(Stream stream, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback)
		{
			Native.SaveFileDialog(stream, folderLocation, fileTypes, streamSavedCallback);
		}

		public void SaveFileDialog(byte[] data, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback)
		{
			Native.SaveFileDialog(data, folderLocation, fileTypes, streamSavedCallback);
		}

		public void LoadFileDialog(FolderLocations folderLocation, int maxWidth, int maxHeight, int x, int y, int width, int height, string[] fileTypes, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			Native.LoadFileDialog(folderLocation, maxWidth, maxHeight, x, y, width, height, fileTypes, streamLoadedCallback);
		}

		public void LoadCameraPicker(CameraQuality quality, int maxWidth, int maxHeight, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			Native.LoadCameraPicker(quality, maxWidth, maxHeight, streamLoadedCallback);
		}

		public void Update()
		{
			Native.Update();
		}




	}
}
