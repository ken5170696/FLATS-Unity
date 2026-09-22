namespace System.Text.Reign
{
	public class EncodingUTF8 : Encoding
	{
		public new delegate string GetStringCallbackMathod(byte[] bytes, int index, int count);

		public new delegate byte[] GetBytesCallbackMethod(string s);

		public new static EncodingUTF8 Singleton;

		public new GetStringCallbackMathod GetString;

		public new GetBytesCallbackMethod GetBytes;

		public EncodingUTF8()
		{
			Singleton = this;
		}


	}
}
