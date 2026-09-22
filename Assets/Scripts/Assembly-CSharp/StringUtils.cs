using System;
using System.Text;
public static class StringUtils
{
	private const string ID_CHARS = "0123456789";

	public static string GeneratePassword(int length)
	{
		StringBuilder stringBuilder = new StringBuilder(length);
		Random random = new Random();
		for (int i = 0; i < length; i++)
		{
			int index = random.Next("0123456789".Length);
			char value = "0123456789"[index];
			stringBuilder.Append(value);
		}
		return stringBuilder.ToString();
	}


}
