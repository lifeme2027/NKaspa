using System.Security.Cryptography;
using System.Text.Json.Serialization;
using NBitcoin.Secp256k1;
using NKaspa.Crypto;
using NKaspa.DataEncoders;
using NKaspa.JsonConverters;

namespace NKaspa
{
    /// <summary>
    /// 32-byte secp256k1 private key. Kaspa counterpart of NBitcoin.Key.
    /// </summary>
    /// <remarks>
    /// <see cref="PubKey"/> is the BIP-340 x-only key. <see cref="PubKeyECDSA"/> is the
    /// compressed SEC key. <see cref="GetAddress"/> returns the Schnorr address, which is
    /// the default Kaspa public-key address. Schnorr signing uses BIP-340. ECDSA signing
    /// uses RFC 6979 and the low-S form.
    /// System.Text.Json refuses this type unless the caller opts in through
    /// <see cref="NKaspaJsonSerializer"/> with <c>allowPrivateKeys: true</c>.
    /// </remarks>
    [JsonConverter(typeof(KeyJsonConverter))]
    public sealed class Key : IDisposable, IEquatable<Key>
    {
        /// <summary>Length of the secret.</summary>
        public const int Length = 32;

        private readonly byte[] _secret;
        private bool _disposed;

        private Key(byte[] secret)
        {
            _secret = secret;
        }

        /// <summary>Imports a 32-byte secret. The buffer is copied. The value must be in range.</summary>
        /// <exception cref="System.ArgumentException">Private key is zero or not in the secp256k1 range. Private key must be 32 bytes.</exception>
        public Key(ReadOnlySpan<byte> secret)
            : this(CopyValid(secret))
        {
        }

        /// <summary>Draws a secret from <see cref="RandomNumberGenerator"/> until it is in range.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">Private key is zero or not in the secp256k1 range. Private key must be 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Could not draw a secp256k1 private key.</exception>
        public static Key Create()
        {
            var secret = new byte[Length];
            try
            {
                for (var attempt = 0; attempt < 64; attempt++)
                {
                    RandomNumberGenerator.Fill(secret);
                    if (ECPrivKey.TryCreate(secret, out var key) && key != null)
                    {
                        key.Dispose();
                        return TakeSecret(secret);
                    }
                }

                throw new InvalidOperationException("Could not draw a secp256k1 private key.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        /// <summary>
        /// Copies <paramref name="secret"/> into a new key and zeros <paramref name="secret"/>.
        /// The buffer must be a 32-byte in-range secret.
        /// </summary>
        internal static Key TakeSecret(byte[] secret)
        {
            if (secret == null)
            {
                throw new ArgumentNullException(nameof(secret));
            }

            try
            {
                return new Key(secret.AsSpan());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        /// <summary>Parses a 64-character hex secret.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="hex"/> is null.</exception>
        /// <exception cref="System.FormatException">Hex string contains a non-hex character. Hex string length must be even.</exception>
        public static Key Parse(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            return new Key(Encoders.Hex.DecodeData(hex));
        }

        /// <summary>BIP-340 x-only public key.</summary>
        /// <exception cref="System.ArgumentException">Schnorr x-only public key is not on the curve. Schnorr x-only public key must be 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public PubKey PubKey
        {
            get
            {
                AssertNotDisposed();
                return new PubKey(XOnly());
            }
        }

        /// <summary>Compressed ECDSA public key.</summary>
        /// <exception cref="System.ArgumentException">ECDSA public key is not on the curve. ECDSA public key must be compressed (0x02 or 0x03). ECDSA public key must be 33 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public PubKeyECDSA PubKeyECDSA
        {
            get
            {
                AssertNotDisposed();
                using var priv = CreatePriv();
                return new PubKeyECDSA(priv.CreatePubKey().ToBytes(true));
            }
        }

        /// <summary>Schnorr address for <paramref name="network"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// Address prefix must not contain a colon. Address prefix is required. {name} must be {size} bytes.
        /// Schnorr x-only public key is not on the curve. Schnorr x-only public key must be 32 bytes.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">8-to-5 conversion produced an unexpected length. Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public KaspaPubKeyAddress GetAddress(Network network)
        {
            return PubKey.GetAddress(network);
        }

        /// <summary>ECDSA address for <paramref name="network"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// Address prefix must not contain a colon. Address prefix is required. ECDSA public key must be 33
        /// bytes. ECDSA public key is not on the curve. ECDSA public key must be compressed (0x02 or 0x03).
        /// </exception>
        /// <exception cref="System.InvalidOperationException">8-to-5 conversion produced an unexpected length. Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public KaspaPubKeyEcdsaAddress GetEcdsaAddress(Network network)
        {
            return PubKeyECDSA.GetAddress(network);
        }

        /// <summary>
        /// BIP-340 signature of a 32-byte message.
        /// An empty <paramref name="auxRand32"/> draws 32 fresh auxiliary bytes.
        /// Pass 32 explicit bytes, including zeros, to make the signature deterministic.
        /// </summary>
        /// <exception cref="System.ArgumentException">BIP-340 auxiliary randomness must be empty or 32 bytes. Schnorr message must be 32 bytes. Schnorr signature must be 64 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public SchnorrSignature SignSchnorr(ReadOnlySpan<byte> hash32, ReadOnlySpan<byte> auxRand32 = default)
        {
            if (hash32.Length != 32)
            {
                throw new ArgumentException("Schnorr message must be 32 bytes.", nameof(hash32));
            }

            byte[] aux;
            if (auxRand32.IsEmpty)
            {
                aux = new byte[32];
                RandomNumberGenerator.Fill(aux);
            }
            else if (auxRand32.Length == 32)
            {
                aux = auxRand32.ToArray();
            }
            else
            {
                throw new ArgumentException("BIP-340 auxiliary randomness must be empty or 32 bytes.", nameof(auxRand32));
            }

            AssertNotDisposed();
            using var priv = CreatePriv();
            var signature = priv.SignBIP340(hash32, aux);
            return new SchnorrSignature(signature.ToBytes());
        }

        /// <summary>RFC 6979 ECDSA signature of a 32-byte message, normalized to low S.</summary>
        /// <exception cref="System.ArgumentException">ECDSA message must be 32 bytes. ECDSA signature must be 64 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public ECDSASignature SignECDSA(ReadOnlySpan<byte> hash32)
        {
            if (hash32.Length != 32)
            {
                throw new ArgumentException("ECDSA message must be 32 bytes.", nameof(hash32));
            }

            AssertNotDisposed();
            using var priv = CreatePriv();
            var signature = priv.SignECDSARFC6979(hash32);
            signature.TryNormalize(out var lowS);
            var compact = new byte[ECDSASignature.Length];
            lowS.WriteCompactToSpan(compact);
            return new ECDSASignature(compact);
        }

        /// <summary>A copy of the 32-byte secret.</summary>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public byte[] ToBytes()
        {
            AssertNotDisposed();
            return (byte[])_secret.Clone();
        }

        /// <summary>Lowercase hex of the secret.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public string ToHex()
        {
            AssertNotDisposed();
            return Encoders.Hex.EncodeData(_secret);
        }

        /// <summary>Zeros the secret.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CryptographicOperations.ZeroMemory(_secret);
            _disposed = true;
        }

        /// <inheritdoc />
        public bool Equals(Key? other)
        {
            if (other is null || _disposed || other._disposed)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(_secret, other._secret);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as Key);
        }

        /// <inheritdoc />
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed.</exception>
        public override int GetHashCode()
        {
            AssertNotDisposed();
            var hash = 17;
            for (var i = 0; i < _secret.Length; i++)
            {
                hash = (hash * 31) + _secret[i];
            }

            return hash;
        }

        private byte[] XOnly()
        {
            using var priv = CreatePriv();
            return priv.CreateXOnlyPubKey().ToBytes();
        }

        private ECPrivKey CreatePriv()
        {
            if (!ECPrivKey.TryCreate(_secret, out var priv) || priv == null)
            {
                throw new InvalidOperationException("Stored private key is no longer valid.");
            }

            return priv;
        }

        private void AssertNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Key));
            }
        }

        private static byte[] CopyValid(ReadOnlySpan<byte> secret)
        {
            if (secret.Length != Length)
            {
                throw new ArgumentException("Private key must be 32 bytes.", nameof(secret));
            }

            var copy = secret.ToArray();
            if (!ECPrivKey.TryCreate(copy, out var priv) || priv == null)
            {
                CryptographicOperations.ZeroMemory(copy);
                throw new ArgumentException("Private key is zero or not in the secp256k1 range.", nameof(secret));
            }

            priv.Dispose();
            return copy;
        }
    }
}
