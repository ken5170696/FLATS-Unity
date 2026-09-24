namespace System.Text.Reign
{
	public class Encoding
	{
		public delegate Encoding GetEncodingCallbackMethod(int codepage);

		public delegate string GetStringCallbackMathod(byte[] bytes, int index, int count);

		public delegate byte[] GetBytesCallbackMethod(string s);

		public static Encoding Singleton;

		public static GetEncodingCallbackMethod GetEncoding;

		public GetStringCallbackMathod GetString;

		public GetBytesCallbackMethod GetBytes;

		public static EncodingASCII ASCII
		{
			get
			{
				if (EncodingASCII.Singleton == null)
				{
					EncodingASCII.Singleton = new EncodingASCII();
				}
				return EncodingASCII.Singleton;
			}
		}

		public static EncodingUTF8 UTF8
		{
			get
			{
				if (EncodingUTF8.Singleton == null)
				{
					EncodingUTF8.Singleton = new EncodingUTF8();
				}
				return EncodingUTF8.Singleton;
			}
		}

		static Encoding()
		{
			Singleton = new Encoding();
		}

		public Encoding()
		{
		}




	}
}
