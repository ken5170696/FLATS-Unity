using System;
using System.IO;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class StaticDiskDataSource : IStaticDataSource
	{
		public StaticDiskDataSource(string fileName)
		{
		}

		public Stream GetSource()
		{
			return null;
		}




	}
}
