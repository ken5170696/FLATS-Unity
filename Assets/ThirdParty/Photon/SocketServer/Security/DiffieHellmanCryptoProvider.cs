using System;
using System.Numerics;
using System.Security.Cryptography;
namespace Photon.SocketServer.Security
{
// Same key exchange, byte order, SHA256 and AES-CBC/PKCS7 wire format as the Store source.
// Only the unavailable Windows Runtime crypto calls are replaced with .NET equivalents.
internal class DiffieHellmanCryptoProvider : ICryptoProvider
{
    private readonly BigInteger prime = new BigInteger(OakleyGroups.OakleyPrime768);
    private readonly BigInteger secret;
    private readonly BigInteger publicKey;
    private Aes aes;
    public bool IsInitialized { get { return aes != null; } }
    public byte[] PublicKey { get { return ToWire(publicKey); } }
    public DiffieHellmanCryptoProvider()
    {
        var random = new byte[21];
        using (var rng=RandomNumberGenerator.Create())
        {
            do { rng.GetBytes(random); random[20]=0; secret=new BigInteger(random); }
            while(secret<2 || secret>=prime-1);
        }
        publicKey=BigInteger.ModPow(new BigInteger(OakleyGroups.Generator),secret,prime);
    }
    public DiffieHellmanCryptoProvider(byte[] hash) { Initialize(hash); }
    private static byte[] ToWire(BigInteger value)
    {
        byte[] bytes=value.ToByteArray();Array.Reverse(bytes);
        if(bytes.Length>1 && bytes[0]==0) { var trimmed=new byte[bytes.Length-1];Buffer.BlockCopy(bytes,1,trimmed,0,trimmed.Length);return trimmed; }
        return bytes;
    }
    public void DeriveSharedKey(byte[] key)
    {
        if(key==null || key.Length==0)throw new ArgumentException("Empty Photon public key");
        var littleEndian=new byte[key.Length+1];for(int i=0;i<key.Length;i++)littleEndian[i]=key[key.Length-i-1];
        var other=new BigInteger(littleEndian);
        if(other<2 || other>=prime-1)throw new CryptographicException("Invalid Photon public key");
        using(var sha=SHA256.Create())Initialize(sha.ComputeHash(ToWire(BigInteger.ModPow(other,secret,prime))));
    }
    private void Initialize(byte[] key)
    {
        if(aes!=null)aes.Dispose();aes=Aes.Create();aes.Mode=CipherMode.CBC;aes.Padding=PaddingMode.PKCS7;aes.Key=key;aes.IV=new byte[16];
    }
    public byte[] Encrypt(byte[] data) { return Encrypt(data,0,data.Length); }
    public byte[] Decrypt(byte[] data) { return Decrypt(data,0,data.Length); }
    public byte[] Encrypt(byte[] data,int offset,int count)
    { if(aes==null)throw new InvalidOperationException("Photon encryption is not initialized");using(var transform=aes.CreateEncryptor())return transform.TransformFinalBlock(data,offset,count); }
    public byte[] Decrypt(byte[] data,int offset,int count)
    { if(aes==null)throw new InvalidOperationException("Photon encryption is not initialized");using(var transform=aes.CreateDecryptor())return transform.TransformFinalBlock(data,offset,count); }
    public void Dispose() { if(aes!=null){aes.Dispose();aes=null;} }
}}
