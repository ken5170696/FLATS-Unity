using System;
using System.Runtime.InteropServices;
namespace ICSharpCode.SharpZipLib.Core
{
	public class FileSystemScanner
	{
		public ProcessDirectoryHandler ProcessDirectory;

		public ProcessFileHandler ProcessFile;

		public CompletedFileHandler CompletedFile;

		public DirectoryFailureHandler DirectoryFailure;

		public FileFailureHandler FileFailure;

		public FileSystemScanner(string filter)
		{
		}

		public FileSystemScanner(string fileFilter, string directoryFilter)
		{
		}

		public FileSystemScanner(IScanFilter fileFilter)
		{
		}

		public FileSystemScanner(IScanFilter fileFilter, IScanFilter directoryFilter)
		{
		}

		private bool OnDirectoryFailure(string directory, Exception e)
		{
			DirectoryFailureHandler directoryFailure = DirectoryFailure;
			bool flag = directoryFailure != null;
			if (flag)
			{
				ScanFailureEventArgs e2 = new ScanFailureEventArgs(directory, e);
				directoryFailure(this, e2);
			}
			return flag;
		}

		private bool OnFileFailure(string file, Exception e)
		{
			FileFailureHandler fileFailure = FileFailure;
			bool flag = fileFailure != null;
			if (flag)
			{
				ScanFailureEventArgs e2 = new ScanFailureEventArgs(file, e);
				FileFailure(this, e2);
			}
			return flag;
		}

		private void OnProcessFile(string file)
		{
			ProcessFileHandler processFile = ProcessFile;
			if (processFile != null)
			{
				ScanEventArgs e = new ScanEventArgs(file);
				processFile(this, e);
			}
		}

		private void OnCompleteFile(string file)
		{
			CompletedFileHandler completedFile = CompletedFile;
			if (completedFile != null)
			{
				ScanEventArgs e = new ScanEventArgs(file);
				completedFile(this, e);
			}
		}

		private void OnProcessDirectory(string directory, bool hasMatchingFiles)
		{
			ProcessDirectoryHandler processDirectory = ProcessDirectory;
			if (processDirectory != null)
			{
				DirectoryEventArgs e = new DirectoryEventArgs(directory, hasMatchingFiles);
				processDirectory(this, e);
			}
		}




	}
}
