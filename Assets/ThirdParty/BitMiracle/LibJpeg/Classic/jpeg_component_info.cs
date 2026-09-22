using System;
namespace BitMiracle.LibJpeg.Classic
{
	internal class jpeg_component_info
	{
		private int component_id;

		private int component_index;

		private int h_samp_factor;

		private int v_samp_factor;

		private int quant_tbl_no;

		private int dc_tbl_no;

		private int ac_tbl_no;

		private int width_in_blocks;

		internal int height_in_blocks;

		internal int DCT_scaled_size;

		internal int downsampled_width;

		internal int downsampled_height;

		internal bool component_needed;

		internal int MCU_width;

		internal int MCU_height;

		internal int MCU_blocks;

		internal int MCU_sample_width;

		internal int last_col_width;

		internal int last_row_height;

		internal JQUANT_TBL quant_table;

		public int Component_id
		{
			get
			{
				return component_id;
			}
			set
			{
				component_id = value;
			}
		}

		public int Component_index
		{
			get
			{
				return component_index;
			}
			set
			{
				component_index = value;
			}
		}

		public int H_samp_factor
		{
			get
			{
				return h_samp_factor;
			}
			set
			{
				h_samp_factor = value;
			}
		}

		public int V_samp_factor
		{
			get
			{
				return v_samp_factor;
			}
			set
			{
				v_samp_factor = value;
			}
		}

		public int Quant_tbl_no
		{
			get
			{
				return quant_tbl_no;
			}
			set
			{
				quant_tbl_no = value;
			}
		}

		public int Dc_tbl_no
		{
			get
			{
				return dc_tbl_no;
			}
			set
			{
				dc_tbl_no = value;
			}
		}

		public int Ac_tbl_no
		{
			get
			{
				return ac_tbl_no;
			}
			set
			{
				ac_tbl_no = value;
			}
		}

		public int Width_in_blocks
		{
			get
			{
				return width_in_blocks;
			}
			set
			{
				width_in_blocks = value;
			}
		}

		public int Downsampled_width
		{
			get
			{
				return downsampled_width;
			}
		}

		internal jpeg_component_info()
		{
		}

		internal void Assign(jpeg_component_info ci)
		{
			component_id = ci.component_id;
			component_index = ci.component_index;
			h_samp_factor = ci.h_samp_factor;
			v_samp_factor = ci.v_samp_factor;
			quant_tbl_no = ci.quant_tbl_no;
			dc_tbl_no = ci.dc_tbl_no;
			ac_tbl_no = ci.ac_tbl_no;
			width_in_blocks = ci.width_in_blocks;
			height_in_blocks = ci.height_in_blocks;
			DCT_scaled_size = ci.DCT_scaled_size;
			downsampled_width = ci.downsampled_width;
			downsampled_height = ci.downsampled_height;
			component_needed = ci.component_needed;
			MCU_width = ci.MCU_width;
			MCU_height = ci.MCU_height;
			MCU_blocks = ci.MCU_blocks;
			MCU_sample_width = ci.MCU_sample_width;
			last_col_width = ci.last_col_width;
			last_row_height = ci.last_row_height;
			quant_table = ci.quant_table;
		}

		internal static jpeg_component_info[] createArrayOfComponents(int length)
		{
			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length");
			}
			jpeg_component_info[] array = new jpeg_component_info[length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new jpeg_component_info();
			}
			return array;
		}




	}
}
