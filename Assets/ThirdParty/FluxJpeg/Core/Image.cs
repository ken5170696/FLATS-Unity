using System;
namespace FluxJpeg.Core
{
	internal class Image
	{
		private delegate void ConvertColor(ref byte c1, ref byte c2, ref byte c3);

		private ColorModel _cm;

		private byte[][,] _raster;

		private int width;

		private int height;

		public byte[][,] Raster
		{
			get
			{
				return _raster;
			}
		}

		public ColorModel ColorModel
		{
			get
			{
				return _cm;
			}
		}

		public double DensityX { get; set; }

		public double DensityY { get; set; }

		public int ComponentCount
		{
			get
			{
				return _raster.Length;
			}
		}

		public int Width
		{
			get
			{
				return width;
			}
		}

		public int Height
		{
			get
			{
				return height;
			}
		}

		public Image ChangeColorSpace(ColorSpace cs)
		{
			if (_cm.ColorSpace == cs)
			{
				return this;
			}
			if (_cm.ColorSpace == ColorSpace.RGB && cs == ColorSpace.YCbCr)
			{
				for (int i = 0; i < width; i++)
				{
					for (int j = 0; j < height; j++)
					{
						YCbCr.fromRGB(ref _raster[0][i, j], ref _raster[1][i, j], ref _raster[2][i, j]);
					}
				}
				_cm.ColorSpace = ColorSpace.YCbCr;
			}
			else if (_cm.ColorSpace == ColorSpace.YCbCr && cs == ColorSpace.RGB)
			{
				for (int k = 0; k < width; k++)
				{
					for (int l = 0; l < height; l++)
					{
						YCbCr.toRGB(ref _raster[0][k, l], ref _raster[1][k, l], ref _raster[2][k, l]);
					}
				}
				_cm.ColorSpace = ColorSpace.RGB;
			}
			else if (_cm.ColorSpace == ColorSpace.Gray && cs == ColorSpace.YCbCr)
			{
				byte[,] array = new byte[width, height];
				byte[,] array2 = new byte[width, height];
				for (int m = 0; m < width; m++)
				{
					for (int n = 0; n < height; n++)
					{
						array[m, n] = 128;
						array2[m, n] = 128;
					}
				}
				_raster = new byte[3][,]
				{
					_raster[0],
					array,
					array2
				};
				_cm.ColorSpace = ColorSpace.YCbCr;
			}
			else
			{
				if (_cm.ColorSpace != ColorSpace.Gray || cs != ColorSpace.RGB)
				{
					throw new Exception("Colorspace conversion not supported.");
				}
				ChangeColorSpace(ColorSpace.YCbCr);
				ChangeColorSpace(ColorSpace.RGB);
			}
			return this;
		}

		public Image(ColorModel cm, byte[][,] raster)
		{
			width = raster[0].GetLength(0);
			height = raster[0].GetLength(1);
			_cm = cm;
			_raster = raster;
		}

		public static byte[][,] CreateRaster(int width, int height, int bands)
		{
			byte[][,] array = new byte[bands][,];
			for (int i = 0; i < bands; i++)
			{
				array[i] = new byte[width, height];
			}
			return array;
		}




	}
}
