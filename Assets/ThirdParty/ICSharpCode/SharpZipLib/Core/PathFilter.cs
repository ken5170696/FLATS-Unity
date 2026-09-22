using System;
using System.Runtime.InteropServices;
namespace ICSharpCode.SharpZipLib.Core
{
	public class PathFilter : IScanFilter
	{
		public PathFilter(string filter)
		{
		}

		public virtual bool IsMatch(string name)
		{
			return false;
		}




	}
}
