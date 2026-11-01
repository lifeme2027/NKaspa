using System.Text;
using NKaspa.Crypto;

namespace NKaspa
{
    /// <summary>
    /// rusty-kaspa <c>sign_message</c> and <c>verify_message</c>.
    /// The digest is BLAKE2b-256 of the UTF-8 message, keyed with the ASCII bytes
    /// <c>PersonalMessageSigningHash</c> (<c>crypto/hashes/src/hashers.rs</c>).
    /// The signature is BIP-340. This is not NBitcoin <c>Key.SignMessage</c> and it is not BIP322.
    /// Vectors live in <c>wallet/core/src/message.rs</c> at
    /// <c>https://github.com/kaspanet/rusty-kaspa/blob/8fe466311ba179e74fc8a52f13b36140bb6b9a4e/wallet/core/src/message.rs</c>.
    /// </summary>
    public static class PersonalMessage
    {
        /// <summary>BLAKE2b key. ASCII <c>PersonalMessageSigningHash</c>, 26 bytes.</summary>
        public const string Domain = "PersonalMessageSigningHash";

        /// <summary>BLAKE2b-256 of the UTF-8 message, keyed with <see cref="Domain"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public static byte[] Hash(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return Blake2b.ComputeHash(Encoding.UTF8.GetBytes(message), 32, Encoding.ASCII.GetBytes(Domain));
        }

        /// <summary>
        /// BIP-340 signature of <see cref="Hash"/>.
        /// An empty <paramref name="auxRand32"/> draws 32 fresh auxiliary bytes.
        /// Pass 32 explicit bytes, including zeros, for a deterministic signature.
        /// Thirty-two zero bytes are the auxiliary value in rusty-kaspa test case 0. They are not <c>no_aux_rand</c>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="key"/> is null. <paramref name="message"/> is null.</exception>
        /// <exception cref="System.ArgumentException">BIP-340 auxiliary randomness must be empty or 32 bytes. Schnorr message must be 32 bytes. Schnorr signature must be 64 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public static SchnorrSignature SignMessage(Key key, string message, ReadOnlySpan<byte> auxRand32 = default)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return key.SignSchnorr(Hash(message), auxRand32);
        }

        /// <summary>True when <paramref name="signature"/> is a BIP-340 signature of <see cref="Hash"/> by <paramref name="publicKey"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="publicKey"/> is null. <paramref name="message"/> is null. <paramref name="signature"/> is null.</exception>
        public static bool VerifyMessage(PubKey publicKey, string message, SchnorrSignature signature)
        {
            if (publicKey == null)
            {
                throw new ArgumentNullException(nameof(publicKey));
            }

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return publicKey.Verify(signature, Hash(message));
        }

        /// <summary>
        /// True when <paramref name="address"/> is a Schnorr pay-to-pubkey address whose 32-byte payload
        /// is the x-only key that signed <paramref name="message"/>.
        /// An ECDSA address, a script-hash address, or a payload that is not on the curve returns false.
        /// The network prefix is not part of the digest. A testnet address of the same key verifies a mainnet signature.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="address"/> is null. <paramref name="message"/> is null. <paramref name="signature"/> is null.</exception>
        public static bool VerifyMessage(KaspaAddress address, string message, SchnorrSignature signature)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (address is not KaspaPubKeyAddress schnorr)
            {
                return false;
            }

            var payload = schnorr.PubKey;
            PubKey publicKey;
            try
            {
                publicKey = new PubKey(payload);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (!publicKey.ToBytes().AsSpan().SequenceEqual(payload))
            {
                return false;
            }

            return VerifyMessage(publicKey, message, signature);
        }
    }
}
