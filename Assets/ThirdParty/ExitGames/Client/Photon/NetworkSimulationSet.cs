using System;
using System.Threading;
namespace ExitGames.Client.Photon
{
	public class NetworkSimulationSet
	{
		private bool isSimulationEnabled;

		private int outgoingLag;

		private int outgoingJitter;

		private int outgoingLossPercentage;

		private int incomingLag;

		private int incomingJitter;

		private int incomingLossPercentage;

		internal PeerBase peerBase;

		public readonly ManualResetEvent NetSimManualResetEvent;

		internal Action SimulationMethod { get; set; }

		protected internal bool IsSimulationEnabled
		{
			get
			{
				return false;
			}
			set
			{
			}
		}

		public int OutgoingLag
		{
			get
			{
				return outgoingLag;
			}
			set
			{
				outgoingLag = value;
			}
		}

		public int OutgoingJitter
		{
			get
			{
				return outgoingJitter;
			}
			set
			{
				outgoingJitter = value;
			}
		}

		public int OutgoingLossPercentage
		{
			get
			{
				return outgoingLossPercentage;
			}
			set
			{
				outgoingLossPercentage = value;
			}
		}

		public int IncomingLag
		{
			get
			{
				return incomingLag;
			}
			set
			{
				incomingLag = value;
			}
		}

		public int IncomingJitter
		{
			get
			{
				return incomingJitter;
			}
			set
			{
				incomingJitter = value;
			}
		}

		public int IncomingLossPercentage
		{
			get
			{
				return incomingLossPercentage;
			}
			set
			{
				incomingLossPercentage = value;
			}
		}

		public int LostPackagesOut { get; internal set; }

		public int LostPackagesIn { get; internal set; }

		public override string ToString()
		{
			return string.Format("NetworkSimulationSet {6}.  Lag in={0} out={1}. Jitter in={2} out={3}. Loss in={4} out={5}.", incomingLag, outgoingLag, incomingJitter, outgoingJitter, incomingLossPercentage, outgoingLossPercentage, IsSimulationEnabled);
		}

		public NetworkSimulationSet()
		{
			outgoingLag = 100;
			outgoingLossPercentage = 1;
			incomingLag = 100;
			incomingLossPercentage = 1;
			NetSimManualResetEvent = new ManualResetEvent(false);

		}




	}
}
