using System;
using System.IO;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Core;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class FastZip
	{
		public enum Overwrite
		{
			Prompt,
			Never,
			Always
		}

		public delegate bool ConfirmOverwriteDelegate(string fileName);

		private byte[] buffer_;

		private ZipOutputStream outputStream_;

		private ZipFile zipFile_;

		private string sourceDirectory_;

		private NameFilter fileFilter_;

		private NameFilter directoryFilter_;

		private Overwrite overwrite_;

		private ConfirmOverwriteDelegate confirmDelegate_;

		private bool restoreDateTimeOnExtract_;

		private bool restoreAttributesOnExtract_;

		private bool createEmptyDirectories_;

		private FastZipEvents events_;

		private IEntryFactory entryFactory_;

		private INameTransform extractNameTransform_;

		private UseZip64 useZip64_;

		public bool CreateEmptyDirectories
		{
			get
			{
				return createEmptyDirectories_;
			}
			set
			{
				createEmptyDirectories_ = value;
			}
		}

		public INameTransform NameTransform
		{
			get
			{
				return entryFactory_.NameTransform;
			}
			set
			{
				entryFactory_.NameTransform = value;
			}
		}

		public IEntryFactory EntryFactory
		{
			get
			{
				return entryFactory_;
			}
			set
			{
				if (value == null)
				{
					entryFactory_ = new ZipEntryFactory();
				}
				else
				{
					entryFactory_ = value;
				}
			}
		}

		public UseZip64 UseZip64
		{
			get
			{
				return useZip64_;
			}
			set
			{
				useZip64_ = value;
			}
		}

		public bool RestoreDateTimeOnExtract
		{
			get
			{
				return restoreDateTimeOnExtract_;
			}
			set
			{
				restoreDateTimeOnExtract_ = value;
			}
		}

		public bool RestoreAttributesOnExtract
		{
			get
			{
				return restoreAttributesOnExtract_;
			}
			set
			{
				restoreAttributesOnExtract_ = value;
			}
		}

		public FastZip()
		{
			entryFactory_ = new ZipEntryFactory();
			useZip64_ = UseZip64.Dynamic;

		}

		public FastZip(FastZipEvents events)
		{
			entryFactory_ = new ZipEntryFactory();
			useZip64_ = UseZip64.Dynamic;

			events_ = events;
		}

		private void ProcessDirectory(object sender, DirectoryEventArgs e)
		{
			if (!e.HasMatchingFiles && CreateEmptyDirectories)
			{
				if (events_ != null)
				{
					events_.OnProcessDirectory(e.Name, e.HasMatchingFiles);
				}
				if (e.ContinueRunning && e.Name != sourceDirectory_)
				{
					ZipEntry entry = entryFactory_.MakeDirectoryEntry(e.Name);
					outputStream_.PutNextEntry(entry);
				}
			}
		}

		private void ProcessFile(object sender, ScanEventArgs e)
		{
		}

		private void AddFileContents(string name, Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			if (buffer_ == null)
			{
				buffer_ = new byte[4096];
			}
			if (events_ != null && events_.Progress != null)
			{
				StreamUtils.Copy(stream, outputStream_, buffer_, events_.Progress, events_.ProgressInterval, this, name);
			}
			else
			{
				StreamUtils.Copy(stream, outputStream_, buffer_);
			}
			FastZipEvents events_2 = events_;
		}

		private void ExtractFileEntry(ZipEntry entry, string targetName)
		{
		}

		private static bool NameIsValid(string name)
		{
			return false;
		}




	}
}
