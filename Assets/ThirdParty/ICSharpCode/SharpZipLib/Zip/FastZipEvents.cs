using System;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Core;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class FastZipEvents
	{
		public ProcessDirectoryHandler ProcessDirectory;

		public ProcessFileHandler ProcessFile;

		public ProgressHandler Progress;

		public CompletedFileHandler CompletedFile;

		public DirectoryFailureHandler DirectoryFailure;

		public FileFailureHandler FileFailure;

		private TimeSpan progressInterval_;

		public TimeSpan ProgressInterval
		{
			get
			{
				return progressInterval_;
			}
			set
			{
				progressInterval_ = value;
			}
		}

		public bool OnDirectoryFailure(string directory, Exception e)
		{
			bool result = false;
			DirectoryFailureHandler directoryFailure = DirectoryFailure;
			if (directoryFailure != null)
			{
				ScanFailureEventArgs e2 = new ScanFailureEventArgs(directory, e);
				directoryFailure(this, e2);
				result = e2.ContinueRunning;
			}
			return result;
		}

		public bool OnFileFailure(string file, Exception e)
		{
			FileFailureHandler fileFailure = FileFailure;
			bool flag = fileFailure != null;
			if (flag)
			{
				ScanFailureEventArgs e2 = new ScanFailureEventArgs(file, e);
				fileFailure(this, e2);
				flag = e2.ContinueRunning;
			}
			return flag;
		}

		public bool OnProcessFile(string file)
		{
			bool result = true;
			ProcessFileHandler processFile = ProcessFile;
			if (processFile != null)
			{
				ScanEventArgs e = new ScanEventArgs(file);
				processFile(this, e);
				result = e.ContinueRunning;
			}
			return result;
		}

		public bool OnCompletedFile(string file)
		{
			bool result = true;
			CompletedFileHandler completedFile = CompletedFile;
			if (completedFile != null)
			{
				ScanEventArgs e = new ScanEventArgs(file);
				completedFile(this, e);
				result = e.ContinueRunning;
			}
			return result;
		}

		public bool OnProcessDirectory(string directory, bool hasMatchingFiles)
		{
			bool result = true;
			ProcessDirectoryHandler processDirectory = ProcessDirectory;
			if (processDirectory != null)
			{
				DirectoryEventArgs e = new DirectoryEventArgs(directory, hasMatchingFiles);
				processDirectory(this, e);
				result = e.ContinueRunning;
			}
			return result;
		}

		public FastZipEvents()
		{
			progressInterval_ = TimeSpan.FromSeconds(3.0);

		}




	}
}
