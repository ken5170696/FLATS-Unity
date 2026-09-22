namespace System.Text.Reign
{
	public class EncodingASCII : Encoding
	{
		public new delegate string GetStringCallbackMathod(byte[] bytes, int index, int count);

		public new delegate byte[] GetBytesCallbackMethod(string s);

		public new static EncodingASCII Singleton;

		public new GetStringCallbackMathod GetString;

		public new GetBytesCallbackMethod GetBytes;

		public EncodingASCII()
		{
			Singleton = this;
		}


	}
}
