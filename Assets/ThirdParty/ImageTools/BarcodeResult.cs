using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Reign;
namespace ImageTools
{
	public class BarcodeResult
	{
		private Collection<Point> _points;

		private Collection<int> _rawBytes;

		public Collection<Point> Points
		{
			get
			{
				return _points;
			}
		}

		public Collection<int> RawBytes
		{
			get
			{
				return _rawBytes;
			}
		}

		public string Text { get; set; }

		public BarcodeResultFormat Format { get; set; }

		public BarcodeResult()
		{
			_points = new Collection<Point>();
			_rawBytes = new Collection<int>();

		}




	}
}
