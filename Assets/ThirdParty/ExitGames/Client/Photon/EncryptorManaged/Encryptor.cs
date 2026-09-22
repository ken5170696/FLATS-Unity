using System;

namespace ExitGames.Client.Photon.EncryptorManaged
{
	public class Encryptor : CryptoBase
	{
		public void Encrypt(byte[] data, int len, byte[] output, ref int offset)
		{
		}

		public void HMAC(byte[] data, int offset, int count)
		{
		}

		public byte[] FinishHMAC()
		{
			return null;
		}

		public byte[] FinishHMAC(byte[] data, int offset, int count)
		{
			return null;
		}

		public Encryptor()
		{
		}


	}
}
