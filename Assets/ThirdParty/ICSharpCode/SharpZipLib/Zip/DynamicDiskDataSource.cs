using System;
using System.IO;
using System.Runtime.InteropServices;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class DynamicDiskDataSource : IDynamicDataSource
	{
		public DynamicDiskDataSource()
		{
		}

		public Stream GetSource(ZipEntry entry, string name)
		{
			Stream result = null;
			if (name != null)
			{
				result = null;
			}
			return result;
		}




	}
}
