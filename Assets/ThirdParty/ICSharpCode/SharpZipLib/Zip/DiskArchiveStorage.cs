using System;
using System.IO;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class DiskArchiveStorage : BaseArchiveStorage
	{
		private Stream temporaryStream_;

		private string temporaryName_;

		public DiskArchiveStorage(ZipFile file, FileUpdateMode updateMode)
			: base(updateMode)
		{
			if (file.Name == null)
			{
				throw new ZipException("Cant handle non file archives");
			}
		}

		public DiskArchiveStorage(ZipFile file)
			: this(file, FileUpdateMode.Safe)
		{
		}

		public override Stream GetTemporaryOutput()
		{
			return new MemoryStream();
		}

		public override Stream ConvertTemporaryToFinal()
		{
			return null;
		}

		public override Stream MakeTemporaryCopy(Stream stream)
		{
			stream.Dispose();
			return new MemoryStream();
		}

		public override Stream OpenForDirectUpdate(Stream stream)
		{
			if (stream == null || !stream.CanWrite)
			{
				if (stream != null)
				{
					stream.Dispose();
				}
				return new MemoryStream();
			}
			return stream;
		}

		public override void Dispose()
		{
			if (temporaryStream_ != null)
			{
				temporaryStream_.Dispose();
			}
		}




	}
}
