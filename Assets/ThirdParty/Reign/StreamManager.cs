using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Reign.Plugin;
using UnityEngine;
namespace Reign
{
	public static class StreamManager
	{
		private static IStreamPlugin plugin;

		private static bool savingStream;

		private static bool loadingStream;

		private static bool checkingIfFileExists;

		private static bool deletingFile;

		private static List<StreamManagerQue> ques;

		private static StreamExistsCallbackMethod streamExistsCallback;

		private static StreamDeleteCallbackMethod streamDeleteCallback;

		private static StreamSavedCallbackMethod streamSavedCallback;

		private static StreamLoadedCallbackMethod streamLoadedCallback;

		static StreamManager()
		{
			ReignServices.CheckStatus();
			ques = new List<StreamManagerQue>();
			plugin = new StreamPlugin_WinRT();
			ReignServices.AddService(update, null, null);
		}

		private static void update()
		{
			plugin.Update();
			updateQues();
		}

		private static void async_streamExistsCallback(bool exists)
		{
			checkingIfFileExists = false;
			if (streamExistsCallback != null)
			{
				streamExistsCallback(exists);
			}
		}

		private static void async_streamDeleteCallback(bool succeeded)
		{
			deletingFile = false;
			if (streamDeleteCallback != null)
			{
				streamDeleteCallback(succeeded);
			}
		}

		private static void async_streamSavedCallback(bool succeeded)
		{
			savingStream = false;
			if (streamSavedCallback != null)
			{
				streamSavedCallback(succeeded);
			}
		}

		private static void async_streamLoadedCallback(Stream stream, bool succeeded)
		{
			loadingStream = false;
			if (streamLoadedCallback != null)
			{
				streamLoadedCallback(stream, succeeded);
			}
		}

		private static void updateQues()
		{
			if (ques.Count == 0)
			{
				return;
			}
			StreamManagerQue[] array = ques.ToArray();
			foreach (StreamManagerQue streamManagerQue in array)
			{
				switch (streamManagerQue.Type)
				{
				case StreamManagerQueTypes.FileExists:
					if (!checkingIfFileExists)
					{
						FileExists(streamManagerQue.FileName, streamManagerQue.FolderLocation, streamManagerQue.streamExistsCallback);
						ques.Remove(streamManagerQue);
					}
					break;
				case StreamManagerQueTypes.DeleteFile:
					if (!deletingFile)
					{
						DeleteFile(streamManagerQue.FileName, streamManagerQue.FolderLocation, streamManagerQue.streamDeleteCallback);
						ques.Remove(streamManagerQue);
					}
					break;
				case StreamManagerQueTypes.SaveFile:
					if (!savingStream)
					{
						if (streamManagerQue.Data != null)
						{
							SaveFile(streamManagerQue.FileName, streamManagerQue.Data, streamManagerQue.FolderLocation, streamManagerQue.streamSavedCallback);
						}
						else
						{
							SaveFile(streamManagerQue.FileName, streamManagerQue.Stream, streamManagerQue.FolderLocation, streamManagerQue.streamSavedCallback);
						}
						ques.Remove(streamManagerQue);
					}
					break;
				case StreamManagerQueTypes.SaveFileDialog:
					if (!savingStream)
					{
						if (streamManagerQue.Data != null)
						{
							SaveFileDialog(streamManagerQue.Data, streamManagerQue.FolderLocation, streamManagerQue.FileTypes, streamManagerQue.streamSavedCallback);
						}
						else
						{
							SaveFileDialog(streamManagerQue.Stream, streamManagerQue.FolderLocation, streamManagerQue.FileTypes, streamManagerQue.streamSavedCallback);
						}
						ques.Remove(streamManagerQue);
					}
					break;
				case StreamManagerQueTypes.LoadFile:
					if (!loadingStream)
					{
						LoadFile(streamManagerQue.FileName, streamManagerQue.FolderLocation, streamManagerQue.streamLoadedCallback);
						ques.Remove(streamManagerQue);
					}
					break;
				case StreamManagerQueTypes.LoadFileDialog:
					if (!loadingStream)
					{
						LoadFileDialog(streamManagerQue.FolderLocation, streamManagerQue.FileTypes, streamManagerQue.streamLoadedCallback);
						ques.Remove(streamManagerQue);
					}
					break;
				case StreamManagerQueTypes.LoadCameraPicker:
					if (!loadingStream)
					{
						LoadCameraPicker(streamManagerQue.CameraQuality, streamManagerQue.MaxWidth, streamManagerQue.MaxHeight, streamManagerQue.streamLoadedCallback);
						ques.Remove(streamManagerQue);
					}
					break;
				default:
					Debug.LogError("Unsuported StreamManagerQueTypes: " + streamManagerQue);
					break;
				}
			}
		}

		private static string getCorrectUnityPath(string fileName, FolderLocations folderLocation)
		{
			return ConvertToPlatformSlash(fileName);
		}

		public static void SaveScreenShotToPictures(StreamSavedCallbackMethod streamSavedCallback)
		{
			if (savingStream)
			{
				Debug.LogError("You must wait for the last saved file to finish!");
				if (streamSavedCallback != null)
				{
					streamSavedCallback(false);
				}
			}
			else
			{
				StreamManager.streamSavedCallback = streamSavedCallback;
				ReignServices.CaptureScreenShot(captureScreenShotCallback);
			}
		}

		private static void captureScreenShotCallback(byte[] data)
		{
			SaveFile("ScreenShot.png", data, FolderLocations.Pictures, streamSavedCallback);
		}

		public static void FileExists(string fileName, FolderLocations folderLocation, StreamExistsCallbackMethod streamExistsCallback)
		{
			if (checkingIfFileExists)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.FileExists);
				streamManagerQue.streamExistsCallback = streamExistsCallback;
				streamManagerQue.FileName = fileName;
				streamManagerQue.FolderLocation = folderLocation;
				ques.Add(streamManagerQue);
			}
			else
			{
				checkingIfFileExists = true;
				StreamManager.streamExistsCallback = streamExistsCallback;
				plugin.FileExists(getCorrectUnityPath(fileName, folderLocation), folderLocation, async_streamExistsCallback);
			}
		}

		public static void DeleteFile(string fileName, FolderLocations folderLocation, StreamDeleteCallbackMethod streamDeleteCallback)
		{
			if (checkingIfFileExists)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.DeleteFile);
				streamManagerQue.streamDeleteCallback = streamDeleteCallback;
				streamManagerQue.FileName = fileName;
				streamManagerQue.FolderLocation = folderLocation;
				ques.Add(streamManagerQue);
			}
			else
			{
				deletingFile = true;
				StreamManager.streamDeleteCallback = streamDeleteCallback;
				plugin.DeleteFile(getCorrectUnityPath(fileName, folderLocation), folderLocation, async_streamDeleteCallback);
			}
		}

		public static void SaveFile(string fileName, Stream stream, FolderLocations folderLocation, StreamSavedCallbackMethod streamSavedCallback)
		{
			if (savingStream)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.SaveFile);
				streamManagerQue.streamSavedCallback = streamSavedCallback;
				streamManagerQue.FileName = fileName;
				streamManagerQue.Stream = stream;
				streamManagerQue.FolderLocation = folderLocation;
				ques.Add(streamManagerQue);
			}
			else
			{
				savingStream = true;
				StreamManager.streamSavedCallback = streamSavedCallback;
				plugin.SaveFile(getCorrectUnityPath(fileName, folderLocation), stream, folderLocation, async_streamSavedCallback);
			}
		}

		public static void SaveFile(string fileName, byte[] data, FolderLocations folderLocation, StreamSavedCallbackMethod streamSavedCallback)
		{
			if (savingStream)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.SaveFile);
				streamManagerQue.streamSavedCallback = streamSavedCallback;
				streamManagerQue.FileName = fileName;
				streamManagerQue.Data = data;
				streamManagerQue.FolderLocation = folderLocation;
				ques.Add(streamManagerQue);
			}
			else
			{
				savingStream = true;
				StreamManager.streamSavedCallback = streamSavedCallback;
				plugin.SaveFile(getCorrectUnityPath(fileName, folderLocation), data, folderLocation, async_streamSavedCallback);
			}
		}

		public static void LoadFile(string fileName, FolderLocations folderLocation, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			if (loadingStream)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.LoadFile);
				streamManagerQue.streamLoadedCallback = streamLoadedCallback;
				streamManagerQue.FileName = fileName;
				streamManagerQue.FolderLocation = folderLocation;
				ques.Add(streamManagerQue);
			}
			else
			{
				loadingStream = true;
				StreamManager.streamLoadedCallback = streamLoadedCallback;
				plugin.LoadFile(getCorrectUnityPath(fileName, folderLocation), folderLocation, async_streamLoadedCallback);
			}
		}

		public static void SaveFileDialog(Stream stream, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback)
		{
			if (savingStream)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.SaveFileDialog);
				streamManagerQue.streamSavedCallback = streamSavedCallback;
				streamManagerQue.Stream = stream;
				streamManagerQue.FolderLocation = folderLocation;
				streamManagerQue.FileTypes = fileTypes;
				ques.Add(streamManagerQue);
			}
			else
			{
				savingStream = true;
				StreamManager.streamSavedCallback = streamSavedCallback;
				plugin.SaveFileDialog(stream, folderLocation, fileTypes, async_streamSavedCallback);
			}
		}

		public static void SaveFileDialog(byte[] data, FolderLocations folderLocation, string[] fileTypes, StreamSavedCallbackMethod streamSavedCallback)
		{
			if (savingStream)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.SaveFileDialog);
				streamManagerQue.streamSavedCallback = streamSavedCallback;
				streamManagerQue.Data = data;
				streamManagerQue.FolderLocation = folderLocation;
				streamManagerQue.FileTypes = fileTypes;
				ques.Add(streamManagerQue);
			}
			else
			{
				savingStream = true;
				StreamManager.streamSavedCallback = streamSavedCallback;
				plugin.SaveFileDialog(data, folderLocation, fileTypes, async_streamSavedCallback);
			}
		}

		public static void LoadFileDialog(FolderLocations folderLocation, string[] fileTypes, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			LoadFileDialog(folderLocation, 0, 0, 0, 0, 10, 10, fileTypes, streamLoadedCallback);
		}

		public static void LoadFileDialog(FolderLocations folderLocation, int maxWidth, int maxHeight, string[] fileTypes, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			LoadFileDialog(folderLocation, maxWidth, maxHeight, 0, 0, 10, 10, fileTypes, streamLoadedCallback);
		}

		public static void LoadFileDialog(FolderLocations folderLocation, int x, int y, int width, int height, string[] fileTypes, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			LoadFileDialog(folderLocation, 0, 0, 0, 0, 10, 10, fileTypes, streamLoadedCallback);
		}

		public static void LoadFileDialog(FolderLocations folderLocation, int maxWidth, int maxHeight, int x, int y, int width, int height, string[] fileTypes, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			if (loadingStream)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.LoadFileDialog);
				streamManagerQue.streamLoadedCallback = streamLoadedCallback;
				streamManagerQue.FolderLocation = folderLocation;
				streamManagerQue.FileTypes = fileTypes;
				ques.Add(streamManagerQue);
			}
			else
			{
				loadingStream = true;
				StreamManager.streamLoadedCallback = streamLoadedCallback;
				plugin.LoadFileDialog(folderLocation, maxWidth, maxHeight, x, y, width, height, fileTypes, async_streamLoadedCallback);
			}
		}

		public static void LoadCameraPicker(CameraQuality quality, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			LoadCameraPicker(quality, 0, 0, streamLoadedCallback);
		}

		public static void LoadCameraPicker(CameraQuality quality, int maxWidth, int maxHeight, StreamLoadedCallbackMethod streamLoadedCallback)
		{
			if (loadingStream)
			{
				StreamManagerQue streamManagerQue = new StreamManagerQue(StreamManagerQueTypes.LoadCameraPicker);
				streamManagerQue.streamLoadedCallback = streamLoadedCallback;
				streamManagerQue.MaxWidth = maxWidth;
				streamManagerQue.MaxHeight = maxHeight;
				streamManagerQue.CameraQuality = quality;
				ques.Add(streamManagerQue);
			}
			else
			{
				loadingStream = true;
				StreamManager.streamLoadedCallback = streamLoadedCallback;
				plugin.LoadCameraPicker(quality, maxWidth, maxHeight, async_streamLoadedCallback);
			}
		}

		public static MemoryStream CopyToMemoryStream(Stream stream)
		{
			MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[1024];
			int num;
			do
			{
				num = stream.Read(array, 0, array.Length);
				memoryStream.Write(array, 0, num);
			}
			while (num == array.Length);
			memoryStream.Position = 0L;
			return memoryStream;
		}

		public static string ConvertToPlatformSlash(string path)
		{
			return path.Replace('/', '\\');
		}

		public static string GetFileDirectory(string fileName)
		{
			bool flag = false;
			string text = fileName;
			foreach (char c in text)
			{
				if (c == '/' || c == '\\')
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return "";
			}
			Match match = Regex.Match(fileName, ".*[/\\\\]");
			if (match.Success && !string.IsNullOrEmpty(match.Value))
			{
				fileName = match.Value.Substring(0, match.Value.Length - 1);
			}
			return fileName + '\\';
		}

		public static string GetFileNameWithExt(string fileName)
		{
			Match match = Regex.Match(fileName, ".*[/\\\\]");
			if (match.Success && !string.IsNullOrEmpty(match.Value))
			{
				fileName = fileName.Substring(match.Value.Length, fileName.Length - match.Value.Length);
			}
			return fileName;
		}

		public static string GetFileNameWithoutExt(string fileName)
		{
			fileName = GetFileNameWithExt(fileName);
			string fileExt = GetFileExt(fileName);
			return fileName.Substring(0, fileName.Length - fileExt.Length);
		}

		public static string GetFileExt(string fileName)
		{
			string[] array = fileName.Split('.');
			if (array.Length < 2)
			{
				return null;
			}
			return '.' + array[array.Length - 1];
		}

		public static string TrimFileExt(string fileName)
		{
			Match match = Regex.Match(fileName, ".*\\.");
			if (match.Success && !string.IsNullOrEmpty(match.Value))
			{
				fileName = match.Value.Substring(0, match.Value.Length - 1);
			}
			return fileName;
		}

		public static bool IsAbsolutePath(string fileName)
		{
			throw new NotImplementedException();
		}

		public static int MakeFourCC(char ch0, char ch1, char ch2, char ch3)
		{
			return (byte)ch0 | ((byte)ch1 << 8) | ((byte)ch2 << 16) | ((byte)ch3 << 24);
		}


	}
}
