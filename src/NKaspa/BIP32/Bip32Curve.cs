using System.Security.Cryptography;
using NBitcoin.Secp256k1;

namespace NKaspa
{
    /// <summary>
    /// BIP32 child-key arithmetic on secp256k1.
    /// A private child is parent + tweak. A public child is parent point + tweak*G.
    /// </summary>
    internal static class Bip32Curve
    {
        public static byte[] TweakPrivate(byte[] secret, ReadOnlySpan<byte> tweak)
        {
            if (!ECPrivKey.TryCreate(secret, out var parent) || parent == null)
            {
                throw new InvalidOperationException("BIP32 parent private key is invalid.");
            }

            using (parent)
            {
                if (!parent.TryTweakAdd(tweak, out var child) || child == null)
                {
                    throw new InvalidOperationException("BIP32 child private key is invalid.");
                }

                using (child)
                {
                    var bytes = new byte[32];
                    child.WriteToSpan(bytes);
                    return bytes;
                }
            }
        }

        public static byte[] TweakPublic(byte[] compressed, ReadOnlySpan<byte> tweak)
        {
            if (!ECPubKey.TryCreate(compressed, null, out _, out var parent) || parent == null)
            {
                throw new InvalidOperationException("BIP32 parent public key is invalid.");
            }

            if (!parent.TryAddTweak(tweak, out var child) || child == null)
            {
                throw new InvalidOperationException("BIP32 child public key is invalid.");
            }

            return child.ToBytes(true);
        }

        public static byte[] HmacSha512(byte[] key, byte[] data)
        {
            using var hmac = new HMACSHA512(key);
            return hmac.ComputeHash(data);
        }
    }
}
