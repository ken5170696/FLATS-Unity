using System;
using System.IO;

namespace Reign
{
	internal class StreamManagerQue
	{
		public StreamManagerQueTypes Type;

		public StreamExistsCallbackMethod streamExistsCallback;

		public StreamDeleteCallbackMethod streamDeleteCallback;

		public StreamSavedCallbackMethod streamSavedCallback;

		public StreamLoadedCallbackMethod streamLoadedCallback;

		public string FileName;

		public FolderLocations FolderLocation;

		public CameraQuality CameraQuality;

		public Stream Stream;

		public byte[] Data;

		public string[] FileTypes;

		public int MaxWidth;

		public int MaxHeight;

		public StreamManagerQue(StreamManagerQueTypes type)
		{
			Type = type;
		}


	}
}
