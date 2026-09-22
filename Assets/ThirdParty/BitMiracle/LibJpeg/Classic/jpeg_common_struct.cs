using System;
using System.Globalization;
namespace BitMiracle.LibJpeg.Classic
{
	internal abstract class jpeg_common_struct
	{
		internal enum JpegState
		{
			DESTROYED = 0,
			CSTATE_START = 100,
			CSTATE_SCANNING = 101,
			CSTATE_RAW_OK = 102,
			CSTATE_WRCOEFS = 103,
			DSTATE_START = 200,
			DSTATE_INHEADER = 201,
			DSTATE_READY = 202,
			DSTATE_PRELOAD = 203,
			DSTATE_PRESCAN = 204,
			DSTATE_SCANNING = 205,
			DSTATE_RAW_OK = 206,
			DSTATE_BUFIMAGE = 207,
			DSTATE_BUFPOST = 208,
			DSTATE_RDCOEFS = 209,
			DSTATE_STOPPING = 210
		}

		internal jpeg_error_mgr m_err;

		internal jpeg_progress_mgr m_progress;

		internal JpegState m_global_state;

		public abstract bool IsDecompressor { get; }

		public jpeg_progress_mgr Progress
		{
			get
			{
				return m_progress;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}
				m_progress = value;
			}
		}

		public jpeg_error_mgr Err
		{
			get
			{
				return m_err;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}
				m_err = value;
			}
		}

		public static string Version
		{
			get
			{
				Version version = new Version("1.0.0");
				string text = version.Major.ToString(CultureInfo.InvariantCulture) + "." + version.Minor.ToString(CultureInfo.InvariantCulture);
				text = text + "." + version.Build.ToString(CultureInfo.InvariantCulture);
				return text + "." + version.Revision.ToString(CultureInfo.InvariantCulture);
			}
		}

		public static string Copyright
		{
			get
			{
				return "Copyright (C) 2008-2011, Bit Miracle";
			}
		}

		public jpeg_common_struct()
			: this(new jpeg_error_mgr())
		{
		}

		public jpeg_common_struct(jpeg_error_mgr errorManager)
		{
			Err = errorManager;
		}

		public static jvirt_array<byte> CreateSamplesArray(int samplesPerRow, int numberOfRows)
		{
			return new jvirt_array<byte>(samplesPerRow, numberOfRows, AllocJpegSamples);
		}

		public static jvirt_array<JBLOCK> CreateBlocksArray(int blocksPerRow, int numberOfRows)
		{
			return new jvirt_array<JBLOCK>(blocksPerRow, numberOfRows, allocJpegBlocks);
		}

		public static byte[][] AllocJpegSamples(int samplesPerRow, int numberOfRows)
		{
			byte[][] array = new byte[numberOfRows][];
			for (int i = 0; i < numberOfRows; i++)
			{
				array[i] = new byte[samplesPerRow];
			}
			return array;
		}

		private static JBLOCK[][] allocJpegBlocks(int blocksPerRow, int numberOfRows)
		{
			JBLOCK[][] array = new JBLOCK[numberOfRows][];
			for (int i = 0; i < numberOfRows; i++)
			{
				array[i] = new JBLOCK[blocksPerRow];
				for (int j = 0; j < blocksPerRow; j++)
				{
					array[i][j] = new JBLOCK();
				}
			}
			return array;
		}

		public void jpeg_abort()
		{
			if (IsDecompressor)
			{
				m_global_state = JpegState.DSTATE_START;
				jpeg_decompress_struct jpeg_decompress_struct2 = this as jpeg_decompress_struct;
				if (jpeg_decompress_struct2 != null)
				{
					jpeg_decompress_struct2.m_marker_list = null;
				}
			}
			else
			{
				m_global_state = JpegState.CSTATE_START;
			}
		}

		public void jpeg_destroy()
		{
			m_global_state = JpegState.DESTROYED;
		}

		public void ERREXIT(J_MESSAGE_CODE code)
		{
			ERREXIT((int)code);
		}

		public void ERREXIT(J_MESSAGE_CODE code, params object[] args)
		{
			ERREXIT((int)code, args);
		}

		public void ERREXIT(int code, params object[] args)
		{
			m_err.m_msg_code = code;
			m_err.m_msg_parm = args;
			m_err.error_exit();
		}

		public void WARNMS(J_MESSAGE_CODE code)
		{
			WARNMS((int)code);
		}

		public void WARNMS(J_MESSAGE_CODE code, params object[] args)
		{
			WARNMS((int)code, args);
		}

		public void WARNMS(int code, params object[] args)
		{
			m_err.m_msg_code = code;
			m_err.m_msg_parm = args;
			m_err.emit_message(-1);
		}

		public void TRACEMS(int lvl, J_MESSAGE_CODE code)
		{
			TRACEMS(lvl, (int)code);
		}

		public void TRACEMS(int lvl, J_MESSAGE_CODE code, params object[] args)
		{
			TRACEMS(lvl, (int)code, args);
		}

		public void TRACEMS(int lvl, int code, params object[] args)
		{
			m_err.m_msg_code = code;
			m_err.m_msg_parm = args;
			m_err.emit_message(lvl);
		}




	}
}
