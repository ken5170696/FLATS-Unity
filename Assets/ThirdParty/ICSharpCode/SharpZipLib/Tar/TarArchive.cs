using System;
using System.IO;
using System.Runtime.InteropServices;
namespace ICSharpCode.SharpZipLib.Tar
{
	public class TarArchive : IDisposable
	{
		private bool asciiTranslate;

		private int userId;

		private string userName;

		private int groupId;

		private string groupName;

		private string rootPath;

		private string pathPrefix;

		private bool applyUserInfoOverrides;

		private TarInputStream tarIn;

		private TarOutputStream tarOut;

		private bool isDisposed;

		public bool AsciiTranslate
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return asciiTranslate;
			}
			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				asciiTranslate = value;
			}
		}

		public string PathPrefix
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return pathPrefix;
			}
			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				pathPrefix = value;
			}
		}

		public string RootPath
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return rootPath;
			}
			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				rootPath = value.Replace('\\', '/').TrimEnd('/');
			}
		}

		public bool ApplyUserInfoOverrides
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return applyUserInfoOverrides;
			}
			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				applyUserInfoOverrides = value;
			}
		}

		public int UserId
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return userId;
			}
		}

		public string UserName
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return userName;
			}
		}

		public int GroupId
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return groupId;
			}
		}

		public string GroupName
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				return groupName;
			}
		}

		public int RecordSize
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				if (tarIn != null)
				{
					return tarIn.RecordSize;
				}
				if (tarOut != null)
				{
					return tarOut.RecordSize;
				}
				return 10240;
			}
		}

		public bool IsStreamOwner
		{
			set
			{
				if (tarIn != null)
				{
					tarIn.IsStreamOwner = value;
				}
				else
				{
					tarOut.IsStreamOwner = value;
				}
			}
		}

		public event ProgressMessageHandler ProgressMessageEvent;

		protected virtual void OnProgressMessageEvent(TarEntry entry, string message)
		{
			ProgressMessageHandler progressMessageEvent = this.ProgressMessageEvent;
			if (progressMessageEvent != null)
			{
				progressMessageEvent(this, entry, message);
			}
		}

		protected TarArchive()
		{
			userName = string.Empty;
			groupName = string.Empty;

		}

		protected TarArchive(TarInputStream stream)
		{
			userName = string.Empty;
			groupName = string.Empty;

			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			tarIn = stream;
		}

		protected TarArchive(TarOutputStream stream)
		{
			userName = string.Empty;
			groupName = string.Empty;

			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}
			tarOut = stream;
		}

		public static TarArchive CreateInputTarArchive(Stream inputStream)
		{
			if (inputStream == null)
			{
				throw new ArgumentNullException("inputStream");
			}
			TarInputStream tarInputStream = inputStream as TarInputStream;
			if (tarInputStream != null)
			{
				return new TarArchive(tarInputStream);
			}
			return CreateInputTarArchive(inputStream, 20);
		}

		public static TarArchive CreateInputTarArchive(Stream inputStream, int blockFactor)
		{
			if (inputStream == null)
			{
				throw new ArgumentNullException("inputStream");
			}
			if (inputStream is TarInputStream)
			{
				throw new ArgumentException("TarInputStream not valid");
			}
			return new TarArchive(new TarInputStream(inputStream, blockFactor));
		}

		public static TarArchive CreateOutputTarArchive(Stream outputStream)
		{
			if (outputStream == null)
			{
				throw new ArgumentNullException("outputStream");
			}
			TarOutputStream tarOutputStream = outputStream as TarOutputStream;
			if (tarOutputStream != null)
			{
				return new TarArchive(tarOutputStream);
			}
			return CreateOutputTarArchive(outputStream, 20);
		}

		public static TarArchive CreateOutputTarArchive(Stream outputStream, int blockFactor)
		{
			if (outputStream == null)
			{
				throw new ArgumentNullException("outputStream");
			}
			if (outputStream is TarOutputStream)
			{
				throw new ArgumentException("TarOutputStream is not valid");
			}
			return new TarArchive(new TarOutputStream(outputStream, blockFactor));
		}

		public void SetKeepOldFiles(bool keepExistingFiles)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}
		}

		[Obsolete("Use the AsciiTranslate property")]
		public void SetAsciiTranslation(bool translateAsciiFiles)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}
			asciiTranslate = translateAsciiFiles;
		}

		public void SetUserInfo(int userId, string userName, int groupId, string groupName)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}
			this.userId = userId;
			this.userName = userName;
			this.groupId = groupId;
			this.groupName = groupName;
			applyUserInfoOverrides = true;
		}

		[Obsolete("Use Close instead")]
		public void CloseArchive()
		{
			Close();
		}

		public void ListContents()
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}
			while (true)
			{
				TarEntry nextEntry = tarIn.GetNextEntry();
				if (nextEntry == null)
				{
					break;
				}
				OnProgressMessageEvent(nextEntry, null);
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (isDisposed)
			{
				return;
			}
			isDisposed = true;
			if (disposing)
			{
				if (tarOut != null)
				{
					tarOut.Flush();
					tarOut.Dispose();
				}
				if (tarIn != null)
				{
					tarIn.Dispose();
				}
			}
		}

		public virtual void Close()
		{
			Dispose(true);
		}

		~TarArchive()
		{
			Dispose(false);
		}

		private static bool IsBinary(string filename)
		{
			return false;
		}




	}
}
