using System;

namespace ExitGames.Client.Photon.EncryptorManaged
{
	public class CryptoBase
	{
		public const int BLOCK_SIZE = 16;

		public const int IV_SIZE = 16;

		public const int HMAC_SIZE = 32;

		public void Init(byte[] encryptionSecret, byte[] hmacSecret)
		{
		}

		public CryptoBase()
		{
		}


	}
}
