using System;
using System.IO;
using System.Runtime.InteropServices;
namespace Reign.Plugin
{
	public class StreamsPlugin_Dumy : IStreamPlugin
	{
		public void FileExists(string fileName, FolderLocations folderLocation, StreamExistsCallbackMethod callback)
		{
			if (callback != null)
			{
				callback(false);
			}
		}

		public void DeleteFile(string fileName, FolderLocations folderLocation, StreamDeleteCallbackMethod callback)
		{
			if (callback != null)
			{
				callback(false);
			}
		}

		public void SaveFile(string fileName, Stream stream, FolderLocations folderLocation, StreamSavedCallbackMethod steamSavedCallback)
		{
			if (steamSavedCallback != null)
			{
				steamSavedCallback(false);
			}
		}

		public void SaveFile(string fileName, byte[] data, FolderLocations folderLocation, StreamSavedCallbackMethod steamSavedCallback)
		{
			if (steamSavedCallback != null)
			{
				steamSavedCallback(false);
			}
		}

		public void LoadFile(string fileName, FolderLocations folderLocation, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			if (streamLoadedCallback != null)
			{
				streamLoadedCallback(null, false);
			}
		}

		public void SaveFileDialog(Stream stream, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback)
		{
			if (streamSavedCallback != null)
			{
				streamSavedCallback(false);
			}
		}

		public void SaveFileDialog(byte[] data, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback)
		{
			if (streamSavedCallback != null)
			{
				streamSavedCallback(false);
			}
		}

		public void LoadFileDialog(FolderLocations folderLocation, int maxWidth, int maxHeight, int x, int y, int width, int height, string[] fileTypes, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			if (streamLoadedCallback != null)
			{
				streamLoadedCallback(null, false);
			}
		}

		public void LoadCameraPicker(CameraQuality quality, int maxWidth, int maxHeight, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			if (streamLoadedCallback != null)
			{
				streamLoadedCallback(null, false);
			}
		}

		public void Update()
		{
		}

		public StreamsPlugin_Dumy()
		{
		}




	}
}
