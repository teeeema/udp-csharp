using System;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc7748;
using Org.BouncyCastle.Security;

namespace PredictionDemo.Security
{
    // Managed primitives work on Unity Mono, where System.Security.Cryptography.AesGcm
    // is not consistently supported. Never implement the primitives ourselves.
    public static class SecureCrypto
    {
        private static readonly SecureRandom Random = new SecureRandom();
        public static byte[] RandomBytes(int size) { var b = new byte[size]; Random.NextBytes(b); return b; }
        public static byte[] PublicKey(byte[] privateKey)
        {
            if (privateKey == null || privateKey.Length != 32) throw new ArgumentException("X25519 key length");
            var result = new byte[32]; X25519.GeneratePublicKey(privateKey, 0, result, 0); return result;
        }
        public static byte[] Agreement(byte[] privateKey, byte[] publicKey)
        {
            if (privateKey == null || privateKey.Length != 32 || publicKey == null || publicKey.Length != 32)
                throw new ArgumentException("X25519 key length");
            var secret = new byte[32];
            if (!X25519.CalculateAgreement(privateKey, 0, publicKey, 0, secret, 0))
            { Wipe(secret); throw new ArgumentException("Invalid X25519 public key"); }
            return secret;
        }
        public static byte[] Hash(byte[] input)
        {
            var d = new Sha256Digest(); d.BlockUpdate(input, 0, input.Length);
            var result = new byte[32]; d.DoFinal(result, 0); return result;
        }
        public static byte[] Hkdf(byte[] secret, byte[] salt, byte[] info, int length)
        {
            var hkdf = new HkdfBytesGenerator(new Sha256Digest());
            hkdf.Init(new HkdfParameters(secret, salt, info));
            var result = new byte[length]; hkdf.GenerateBytes(result, 0, length); return result;
        }
        public static byte[] DirectionKey(byte[] secret, byte[] transcript, string direction) =>
            Hkdf(secret, transcript, Encoding.ASCII.GetBytes("DUSTLINE/pr6/v1/" + direction), 32);
        public static byte[] EncryptAES(byte[] key, byte[] nonce, byte[] aad, byte[] plaintext)
        {
            Validate(key, nonce);
            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(true, new AeadParameters(new KeyParameter(key), 128, nonce, aad));
            var output = new byte[cipher.GetOutputSize(plaintext.Length)];
            try { var n = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0); cipher.DoFinal(output, n); return output; }
            catch { Wipe(output); throw; }
        }
        public static bool DecryptAES(byte[] key, byte[] nonce, byte[] aad, byte[] ciphertextAndTag, out byte[] plaintext)
        {
            plaintext = null;
            Validate(key, nonce);
            if (ciphertextAndTag == null || ciphertextAndTag.Length < 16) return false;
            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(false, new AeadParameters(new KeyParameter(key), 128, nonce, aad));
            var output = new byte[cipher.GetOutputSize(ciphertextAndTag.Length)];
            try
            {
                var n = cipher.ProcessBytes(ciphertextAndTag, 0, ciphertextAndTag.Length, output, 0);
                cipher.DoFinal(output, n); plaintext = output; return true;
            }
            catch (InvalidCipherTextException) { Wipe(output); return false; }
            catch { Wipe(output); throw; }
        }
        private static void Validate(byte[] key, byte[] nonce)
        {
            if (key == null || key.Length != 32 || nonce == null || nonce.Length != 12)
                throw new ArgumentException("AES-256-GCM requires a 32-byte key and 12-byte nonce");
        }
        public static bool Equal(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int d = 0; for (int i = 0; i < a.Length; i++) d |= a[i] ^ b[i]; return d == 0;
        }
        public static void Wipe(byte[] b) { if (b != null) Array.Clear(b, 0, b.Length); }
    }
}
