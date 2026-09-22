using System.IO;

namespace Reign.Plugin
{
	public interface IStreamPlugin
	{
		void FileExists(string fileName, FolderLocations folderLocation, StreamExistsCallbackMethod callback);

		void DeleteFile(string fileName, FolderLocations folderLocation, StreamDeleteCallbackMethod callback);

		void SaveFile(string fileName, Stream stream, FolderLocations folderLocation, StreamSavedCallbackMethod steamSavedCallback);

		void SaveFile(string fileName, byte[] data, FolderLocations folderLocation, StreamSavedCallbackMethod steamSavedCallback);

		void LoadFile(string fileName, FolderLocations folderLocation, StreamLoadedCallbackMethod streamLoadedCallback);

		void SaveFileDialog(Stream stream, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback);

		void SaveFileDialog(byte[] data, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback);

		void LoadFileDialog(FolderLocations folderLocation, int maxWidth, int maxHeight, int x, int y, int width, int height, string[] fileTypes, StreamLoadedCallbackMethod streamLoadedCallback);

		void LoadCameraPicker(CameraQuality quality, int maxWidth, int maxHeight, StreamLoadedCallbackMethod streamLoadedCallback);

		void Update();
	}
}
