using System.IO;
namespace System.Windows.Resources.Reign
{
	public class StreamResourceInfo
	{
		private Stream stream;

		private string contentType;

		public string ContentType
		{
			get
			{
				return contentType;
			}
		}

		public Stream Stream
		{
			get
			{
				return stream;
			}
		}

		public StreamResourceInfo(Stream stream, string contentType)
		{
			this.stream = stream;
			this.contentType = contentType;
		}




	}
}
