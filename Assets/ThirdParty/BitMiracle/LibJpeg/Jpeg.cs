using System;
using System.IO;
using BitMiracle.LibJpeg.Classic;
namespace BitMiracle.LibJpeg
{
	internal class Jpeg
	{
		public delegate bool MarkerParser(Jpeg decompressor);

		private jpeg_compress_struct m_compressorObj;

		private jpeg_decompress_struct m_decompressorObj;

		private CompressionParameters m_compressionParameters;

		private DecompressionParameters m_decompressionParameters;

		private jpeg_compress_struct m_compressor
		{
			get
			{
				if (m_compressorObj == null)
				{
					m_compressorObj = new jpeg_compress_struct(new jpeg_error_mgr());
				}
				return m_compressorObj;
			}
		}

		private jpeg_decompress_struct m_decompressor
		{
			get
			{
				if (m_decompressorObj == null)
				{
					m_decompressorObj = new jpeg_decompress_struct(new jpeg_error_mgr());
				}
				return m_decompressorObj;
			}
		}

		public CompressionParameters CompressionParameters
		{
			get
			{
				return m_compressionParameters;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}
				m_compressionParameters = value;
			}
		}

		public DecompressionParameters DecompressionParameters
		{
			get
			{
				return m_decompressionParameters;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}
				m_decompressionParameters = value;
			}
		}

		public jpeg_compress_struct ClassicCompressor
		{
			get
			{
				return m_compressor;
			}
		}

		public jpeg_decompress_struct ClassicDecompressor
		{
			get
			{
				return m_decompressor;
			}
		}

		public void Compress(IRawImage source, Stream output)
		{
			if (source == null)
			{
				throw new ArgumentNullException("source");
			}
			if (output == null)
			{
				throw new ArgumentNullException("output");
			}
			m_compressor.Image_width = source.Width;
			m_compressor.Image_height = source.Height;
			m_compressor.In_color_space = (J_COLOR_SPACE)source.Colorspace;
			m_compressor.Input_components = source.ComponentsPerPixel;
			m_compressor.jpeg_set_defaults();
			applyParameters(m_compressionParameters);
			m_compressor.jpeg_stdio_dest(output);
			m_compressor.jpeg_start_compress(true);
			source.BeginRead();
			while (m_compressor.Next_scanline < m_compressor.Image_height)
			{
				byte[] pixelRow = source.GetPixelRow();
				if (pixelRow == null)
				{
					throw new IOException("Row of pixels is null");
				}
				byte[][] scanlines = new byte[1][] { pixelRow };
				m_compressor.jpeg_write_scanlines(scanlines, 1);
			}
			source.EndRead();
			m_compressor.jpeg_finish_compress();
		}

		public void Decompress(Stream jpeg, IDecompressDestination destination)
		{
			if (jpeg == null)
			{
				throw new ArgumentNullException("jpeg");
			}
			if (destination == null)
			{
				throw new ArgumentNullException("destination");
			}
			beforeDecompress(jpeg);
			m_decompressor.jpeg_start_decompress();
			LoadedImageAttributes imageParametersFromDecompressor = getImageParametersFromDecompressor();
			destination.SetImageAttributes(imageParametersFromDecompressor);
			destination.BeginWrite();
			byte[][] array = jpeg_common_struct.AllocJpegSamples(m_decompressor.Output_width * m_decompressor.Output_components, 1);
			while (m_decompressor.Output_scanline < m_decompressor.Output_height)
			{
				m_decompressor.jpeg_read_scanlines(array, 1);
				destination.ProcessPixelsRow(array[0]);
			}
			destination.EndWrite();
			m_decompressor.jpeg_finish_decompress();
		}

		private void beforeDecompress(Stream jpeg)
		{
			m_decompressor.jpeg_stdio_src(jpeg);
			m_decompressor.jpeg_read_header(true);
			applyParameters(m_decompressionParameters);
			m_decompressor.jpeg_calc_output_dimensions();
		}

		private LoadedImageAttributes getImageParametersFromDecompressor()
		{
			LoadedImageAttributes loadedImageAttributes = new LoadedImageAttributes();
			loadedImageAttributes.Colorspace = (Colorspace)m_decompressor.Out_color_space;
			loadedImageAttributes.QuantizeColors = m_decompressor.Quantize_colors;
			loadedImageAttributes.Width = m_decompressor.Output_width;
			loadedImageAttributes.Height = m_decompressor.Output_height;
			loadedImageAttributes.ComponentsPerSample = m_decompressor.Out_color_components;
			loadedImageAttributes.Components = m_decompressor.Output_components;
			loadedImageAttributes.ActualNumberOfColors = m_decompressor.Actual_number_of_colors;
			loadedImageAttributes.Colormap = m_decompressor.Colormap;
			loadedImageAttributes.DensityUnit = m_decompressor.Density_unit;
			loadedImageAttributes.DensityX = m_decompressor.X_density;
			loadedImageAttributes.DensityY = m_decompressor.Y_density;
			return loadedImageAttributes;
		}

		public void SetMarkerProcessor(int markerCode, MarkerParser routine)
		{
			jpeg_decompress_struct.jpeg_marker_parser_method routine2 = (jpeg_decompress_struct param0) => routine(this);
			m_decompressor.jpeg_set_marker_processor(markerCode, routine2);
		}

		private void applyParameters(DecompressionParameters parameters)
		{
			if (parameters.OutColorspace != Colorspace.Unknown)
			{
				m_decompressor.Out_color_space = (J_COLOR_SPACE)parameters.OutColorspace;
			}
			m_decompressor.Scale_num = parameters.ScaleNumerator;
			m_decompressor.Scale_denom = parameters.ScaleDenominator;
			m_decompressor.Buffered_image = parameters.BufferedImage;
			m_decompressor.Raw_data_out = parameters.RawDataOut;
			m_decompressor.Dct_method = (J_DCT_METHOD)parameters.DCTMethod;
			m_decompressor.Dither_mode = (J_DITHER_MODE)parameters.DitherMode;
			m_decompressor.Do_fancy_upsampling = parameters.DoFancyUpsampling;
			m_decompressor.Do_block_smoothing = parameters.DoBlockSmoothing;
			m_decompressor.Quantize_colors = parameters.QuantizeColors;
			m_decompressor.Two_pass_quantize = parameters.TwoPassQuantize;
			m_decompressor.Desired_number_of_colors = parameters.DesiredNumberOfColors;
			m_decompressor.Enable_1pass_quant = parameters.EnableOnePassQuantizer;
			m_decompressor.Enable_external_quant = parameters.EnableExternalQuant;
			m_decompressor.Enable_2pass_quant = parameters.EnableTwoPassQuantizer;
			m_decompressor.Err.Trace_level = parameters.TraceLevel;
		}

		private void applyParameters(CompressionParameters parameters)
		{
			m_compressor.Smoothing_factor = parameters.SmoothingFactor;
			m_compressor.jpeg_set_quality(parameters.Quality, true);
			if (parameters.SimpleProgressive)
			{
				m_compressor.jpeg_simple_progression();
			}
		}

		public Jpeg()
		{
			m_compressionParameters = new CompressionParameters();
			m_decompressionParameters = new DecompressionParameters();

		}




	}
}
