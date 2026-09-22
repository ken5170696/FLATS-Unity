using System;
using System.Threading;
namespace AmplifyMotion
{
	internal class WorkerThreadPool
	{
		internal void InitializeAsyncUpdateThreads(int threadCount, bool systemThreadPool)
		{
		}

		internal void FinalizeAsyncUpdateThreads()
		{
		}

		internal void EnqueueAsyncUpdate(MotionState state)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				AsyncUpdateCallback(state);
			});
		}

		private static void AsyncUpdateCallback(object obj)
		{
			MotionState motionState = (MotionState)obj;
			motionState.AsyncUpdate();
		}

		private static void AsyncUpdateThread(object obj)
		{
		}

		public WorkerThreadPool()
		{
		}




	}
}
