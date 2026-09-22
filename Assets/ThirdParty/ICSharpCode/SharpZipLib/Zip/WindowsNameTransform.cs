using System;
using System.Runtime.InteropServices;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class WindowsNameTransform : INameTransform
	{
		private const int MaxPath = 260;

		private string _baseDirectory;

		private bool _trimIncomingPaths;

		private char _replacementChar;

		private static readonly char[] InvalidEntryChars;

		public string BaseDirectory
		{
			get
			{
				return _baseDirectory;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}
			}
		}

		public bool TrimIncomingPaths
		{
			get
			{
				return _trimIncomingPaths;
			}
			set
			{
				_trimIncomingPaths = value;
			}
		}

		public char Replacement
		{
			get
			{
				return _replacementChar;
			}
			set
			{
				for (int i = 0; i < InvalidEntryChars.Length; i++)
				{
					if (InvalidEntryChars[i] == value)
					{
						throw new ArgumentException("invalid path character");
					}
				}
				if (value == '\\' || value == '/')
				{
					throw new ArgumentException("invalid replacement character");
				}
				_replacementChar = value;
			}
		}

		public WindowsNameTransform(string baseDirectory)
		{
			_replacementChar = '_';

			if (baseDirectory == null)
			{
				throw new ArgumentNullException("baseDirectory", "Directory name is invalid");
			}
			BaseDirectory = baseDirectory;
		}

		public WindowsNameTransform()
		{
			_replacementChar = '_';

		}

		public string TransformDirectory(string name)
		{
			name = TransformFile(name);
			if (name.Length > 0)
			{
				while (name.EndsWith("\\"))
				{
					name = name.Remove(name.Length - 1, 1);
				}
				return name;
			}
			throw new ZipException("Cannot have an empty directory name");
		}

		public string TransformFile(string name)
		{
			if (name != null)
			{
				name = MakeValidName(name, _replacementChar);
				bool trimIncomingPath = _trimIncomingPaths;
				if (_baseDirectory == null)
				{
				}
			}
			else
			{
				name = string.Empty;
			}
			return name;
		}

		public static bool IsValidName(string name)
		{
			return name != null && name.Length <= 260 && string.Compare(name, MakeValidName(name, '_')) == 0;
		}

		static WindowsNameTransform()
		{
		}

		public static string MakeValidName(string name, char replacement)
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}
			name = WindowsPathUtils.DropPathRoot(name.Replace("/", "\\"));
			while (name.Length > 0 && name[0] == '\\')
			{
				name = name.Remove(0, 1);
			}
			while (name.Length > 0 && name[name.Length - 1] == '\\')
			{
				name = name.Remove(name.Length - 1, 1);
			}
			int num;
			for (num = name.IndexOf("\\\\"); num >= 0; num = name.IndexOf("\\\\"))
			{
				name = name.Remove(num, 1);
			}
			num = name.IndexOfAny(InvalidEntryChars);
			if (num >= 0)
			{
				StringBuilder stringBuilder = new StringBuilder(name);
				while (num >= 0)
				{
					stringBuilder[num] = replacement;
					num = ((num < name.Length) ? name.IndexOfAny(InvalidEntryChars, num + 1) : (-1));
				}
				name = stringBuilder.ToString();
			}
			int length = name.Length;
			int num2 = 260;
			return name;
		}




	}
}
