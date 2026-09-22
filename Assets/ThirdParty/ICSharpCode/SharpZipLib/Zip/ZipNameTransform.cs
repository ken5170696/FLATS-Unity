using System;
using System.Runtime.InteropServices;
using System.Text;
using ICSharpCode.SharpZipLib.Core;
namespace ICSharpCode.SharpZipLib.Zip
{
	public class ZipNameTransform : INameTransform
	{
		private string trimPrefix_;

		private static readonly char[] InvalidEntryChars;

		private static readonly char[] InvalidEntryCharsRelaxed;

		public string TrimPrefix
		{
			get
			{
				return trimPrefix_;
			}
			set
			{
				trimPrefix_ = value;
				if (trimPrefix_ != null)
				{
					trimPrefix_ = trimPrefix_.ToLower();
				}
			}
		}

		public ZipNameTransform()
		{
		}

		public ZipNameTransform(string trimPrefix)
		{
			TrimPrefix = trimPrefix;
		}

		static ZipNameTransform()
		{
			char[] array = new char[0];
			int num = array.Length + 2;
			InvalidEntryCharsRelaxed = new char[num];
			Array.Copy(array, 0, InvalidEntryCharsRelaxed, 0, array.Length);
			InvalidEntryCharsRelaxed[num - 1] = '*';
			InvalidEntryCharsRelaxed[num - 2] = '?';
			num = array.Length + 4;
			InvalidEntryChars = new char[num];
			Array.Copy(array, 0, InvalidEntryChars, 0, array.Length);
			InvalidEntryChars[num - 1] = ':';
			InvalidEntryChars[num - 2] = '\\';
			InvalidEntryChars[num - 3] = '*';
			InvalidEntryChars[num - 4] = '?';
		}

		public string TransformDirectory(string name)
		{
			name = TransformFile(name);
			if (name.Length > 0)
			{
				if (!name.EndsWith("/"))
				{
					name += "/";
				}
				return name;
			}
			throw new ZipException("Cannot have an empty directory name");
		}

		public string TransformFile(string name)
		{
			if (name != null)
			{
				string text = name.ToLower();
				if (trimPrefix_ != null && text.IndexOf(trimPrefix_) == 0)
				{
					name = name.Substring(trimPrefix_.Length);
				}
				name = name.Replace("\\", "/");
				name = WindowsPathUtils.DropPathRoot(name);
				while (name.Length > 0 && name[0] == '/')
				{
					name = name.Remove(0, 1);
				}
				while (name.Length > 0 && name[name.Length - 1] == '/')
				{
					name = name.Remove(name.Length - 1, 1);
				}
				for (int num = name.IndexOf("//"); num >= 0; num = name.IndexOf("//"))
				{
					name = name.Remove(num, 1);
				}
				name = MakeValidName(name, '_');
			}
			else
			{
				name = string.Empty;
			}
			return name;
		}

		private static string MakeValidName(string name, char replacement)
		{
			int num = name.IndexOfAny(InvalidEntryChars);
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
			int num2 = 65535;
			return name;
		}

		public static bool IsValidName(string name, bool relaxed)
		{
			bool flag = name != null;
			if (flag)
			{
				flag = ((!relaxed) ? (name.IndexOfAny(InvalidEntryChars) < 0 && name.IndexOf('/') != 0) : (name.IndexOfAny(InvalidEntryCharsRelaxed) < 0));
			}
			return flag;
		}

		public static bool IsValidName(string name)
		{
			return name != null && name.IndexOfAny(InvalidEntryChars) < 0 && name.IndexOf('/') != 0;
		}




	}
}
