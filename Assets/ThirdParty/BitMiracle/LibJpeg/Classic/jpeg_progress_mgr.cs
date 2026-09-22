using System;
namespace BitMiracle.LibJpeg.Classic
{
	internal class jpeg_progress_mgr
	{
		private int m_passCounter;

		private int m_passLimit;

		private int m_completedPasses;

		private int m_totalPasses;

		public int Pass_counter
		{
			get
			{
				return m_passCounter;
			}
			set
			{
				m_passCounter = value;
			}
		}

		public int Pass_limit
		{
			get
			{
				return m_passLimit;
			}
			set
			{
				m_passLimit = value;
			}
		}

		public int Completed_passes
		{
			get
			{
				return m_completedPasses;
			}
			set
			{
				m_completedPasses = value;
			}
		}

		public int Total_passes
		{
			get
			{
				return m_totalPasses;
			}
			set
			{
				m_totalPasses = value;
			}
		}

		public event EventHandler OnProgress;

		public void Updated()
		{
			if (this.OnProgress != null)
			{
				this.OnProgress(this, new EventArgs());
			}
		}

		public jpeg_progress_mgr()
		{
		}




	}
}
