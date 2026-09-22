using System;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.Core;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class ZipEntryFactory : IEntryFactory
	{
		public enum TimeSetting
		{
			LastWriteTime,
			LastWriteTimeUtc,
			CreateTime,
			CreateTimeUtc,
			LastAccessTime,
			LastAccessTimeUtc,
			Fixed
		}

		private INameTransform nameTransform_;

		private DateTime fixedDateTime_;

		private TimeSetting timeSetting_;

		private bool isUnicodeText_;

		private int getAttributes_;

		private int setAttributes_;

		public INameTransform NameTransform
		{
			get
			{
				return nameTransform_;
			}
			set
			{
				if (value == null)
				{
					nameTransform_ = new ZipNameTransform();
				}
				else
				{
					nameTransform_ = value;
				}
			}
		}

		public TimeSetting Setting
		{
			get
			{
				return timeSetting_;
			}
			set
			{
				timeSetting_ = value;
			}
		}

		public DateTime FixedDateTime
		{
			get
			{
				return fixedDateTime_;
			}
			set
			{
				if (value.Year < 1970)
				{
					throw new ArgumentException("Value is too old to be valid", "value");
				}
				fixedDateTime_ = value;
			}
		}

		public int GetAttributes
		{
			get
			{
				return getAttributes_;
			}
			set
			{
				getAttributes_ = value;
			}
		}

		public int SetAttributes
		{
			get
			{
				return setAttributes_;
			}
			set
			{
				setAttributes_ = value;
			}
		}

		public bool IsUnicodeText
		{
			get
			{
				return isUnicodeText_;
			}
			set
			{
				isUnicodeText_ = value;
			}
		}

		public ZipEntryFactory()
		{
			fixedDateTime_ = DateTime.Now;
			getAttributes_ = -1;

			nameTransform_ = new ZipNameTransform();
		}

		public ZipEntryFactory(TimeSetting timeSetting)
		{
			fixedDateTime_ = DateTime.Now;
			getAttributes_ = -1;

			timeSetting_ = timeSetting;
			nameTransform_ = new ZipNameTransform();
		}

		public ZipEntryFactory(DateTime time)
		{
			fixedDateTime_ = DateTime.Now;
			getAttributes_ = -1;

			timeSetting_ = TimeSetting.Fixed;
			FixedDateTime = time;
			nameTransform_ = new ZipNameTransform();
		}

		public ZipEntry MakeFileEntry(string fileName)
		{
			return MakeFileEntry(fileName, null, true);
		}

		public ZipEntry MakeFileEntry(string fileName, bool useFileSystem)
		{
			return MakeFileEntry(fileName, null, useFileSystem);
		}

		public ZipEntry MakeFileEntry(string fileName, string entryName, bool useFileSystem)
		{
			return null;
		}

		public ZipEntry MakeDirectoryEntry(string directoryName)
		{
			return MakeDirectoryEntry(directoryName, true);
		}

		public ZipEntry MakeDirectoryEntry(string directoryName, bool useFileSystem)
		{
			return null;
		}




	}
}
