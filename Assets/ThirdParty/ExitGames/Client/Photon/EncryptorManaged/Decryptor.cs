using System;

namespace ExitGames.Client.Photon.EncryptorManaged
{
	public class Decryptor : CryptoBase
	{
		public byte[] DecryptBufferWithIV(byte[] data, int offset, int len, out int outLen)
		{
			outLen = 0;
			return null;
		}

		public bool CheckHMAC(byte[] data, int len)
		{
			return false;
		}

		public Decryptor()
		{
		}


	}
}
