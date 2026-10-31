using System.Security.Cryptography;
using System.Text;
using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// BIP32 extended private key. Kaspa counterpart of NBitcoin.ExtKey.
    /// The master key is HMAC-SHA512 with the key <c>Bitcoin seed</c>.
    /// Child keys use the parent chain code. Hardened children hash the private key.
    /// <see cref="ToString(Network)"/> uses the Kaspa prefix for that network (<c>kprv</c> or <c>ktrv</c>).
    /// </summary>
    public sealed class ExtKey : IDisposable, IEquatable<ExtKey>
    {
        private const int PayloadLength = 78;
        private static readonly byte[] SeedKey = Encoding.ASCII.GetBytes("Bitcoin seed");

        private readonly Key _key;
        private readonly byte[] _chainCode;
        private readonly byte _depth;
        private readonly HDFingerprint _parentFingerprint;
        private readonly uint _child;
        private readonly uint _version;
        private bool _disposed;

        private ExtKey(Key key, byte[] chainCode, byte depth, HDFingerprint parentFingerprint, uint child, uint version)
        {
            _key = key;
            _chainCode = chainCode;
            _depth = depth;
            _parentFingerprint = parentFingerprint;
            _child = child;
            _version = version;
        }

        /// <summary>
        /// Master key from a 16- to 64-byte seed.
        /// The stored version is Kaspa mainnet <c>kprv</c>. Pass another version to <see cref="ToBase58(uint)"/> to print <c>xprv</c>.
        /// </summary>
        /// <exception cref="System.ArgumentException">BIP32 seed must be between 16 and 64 bytes. Private key is zero or not in the secp256k1 range. Private key must be 32 bytes.</exception>
        /// <exception cref="System.InvalidOperationException">BIP32 master key is invalid.</exception>
        public static ExtKey CreateFromSeed(ReadOnlySpan<byte> seed)
        {
            if (seed.Length < 16 || seed.Length > 64)
            {
                throw new ArgumentException("BIP32 seed must be between 16 and 64 bytes.", nameof(seed));
            }

            var mac = Bip32Curve.HmacSha512(SeedKey, seed.ToArray());
            try
            {
                var secret = mac.AsSpan(0, 32).ToArray();
                var chain = mac.AsSpan(32, 32).ToArray();
                try
                {
                    return new ExtKey(new Key(secret), chain, 0, default, 0, HdKeyVersions.Kprv);
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidOperationException("BIP32 master key is invalid.", exception);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(secret);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(mac);
            }
        }

        /// <summary>Parses a Base58Check extended private key. Unknown versions and bad keys throw <see cref="FormatException"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="text"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Private key is zero or not in the secp256k1 range. Private key must be 32 bytes. An HD fingerprint is 4 bytes.</exception>
        /// <exception cref="System.FormatException">
        /// Extended private key is not in the secp256k1 range. Extended private key prefix byte is not 0x00. A
        /// master extended key must have a zero parent fingerprint and a zero child index. Extended key version
        /// is not a private prefix, or it does not match the key type. An extended key payload is 78 bytes.
        /// Invalid Base58Check checksum. Base58Check payload is shorter than the checksum. Base58 value does
        /// not fit in the decode buffer. Invalid Base58 character.
        /// </exception>
        public static ExtKey Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            byte[] payload;
            try
            {
                payload = Base58CheckEncoder.Decode(text.Trim());
            }
            catch (FormatException)
            {
                throw;
            }

            if (payload.Length != PayloadLength)
            {
                throw new FormatException("An extended key payload is 78 bytes.");
            }

            var version = ReadU32BE(payload.AsSpan(0, 4));
            if (!HdKeyVersions.IsPrivate(version))
            {
                throw new FormatException("Extended key version is not a private prefix, or it does not match the key type.");
            }

            var depth = payload[4];
            var fingerprint = new HDFingerprint(payload.AsSpan(5, 4));
            var child = ReadU32BE(payload.AsSpan(9, 4));
            if (depth == 0 && (!fingerprint.IsZero || child != 0))
            {
                throw new FormatException("A master extended key must have a zero parent fingerprint and a zero child index.");
            }

            var chain = payload.AsSpan(13, 32).ToArray();
            if (payload[45] != 0x00)
            {
                throw new FormatException("Extended private key prefix byte is not 0x00.");
            }

            var secret = payload.AsSpan(46, 32).ToArray();
            try
            {
                return new ExtKey(new Key(secret), chain, depth, fingerprint, child, version);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Extended private key is not in the secp256k1 range.", exception);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
                CryptographicOperations.ZeroMemory(payload);
            }
        }

        /// <summary>
        /// A copy of the private key at this node.
        /// Disposing the returned key does not dispose this extended key.
        /// </summary>
        /// <exception cref="System.ArgumentException">Private key is zero or not in the secp256k1 range. Private key must be 32 bytes.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public Key PrivateKey
        {
            get
            {
                AssertNotDisposed();
                var secret = _key.ToBytes();
                try
                {
                    return new Key(secret);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(secret);
                }
            }
        }

        /// <summary>A copy of the 32-byte chain code.</summary>
        /// <exception cref="System.ObjectDisposedException">The ExtKey has been disposed.</exception>
        public byte[] ChainCode
        {
            get
            {
                AssertNotDisposed();
                return (byte[])_chainCode.Clone();
            }
        }

        /// <summary>Depth. A master key is 0.</summary>
        public byte Depth
        {
            get { return _depth; }
        }

        /// <summary>Parent fingerprint. A master key is zero.</summary>
        public HDFingerprint ParentFingerprint
        {
            get { return _parentFingerprint; }
        }

        /// <summary>Child index, including the hardened bit.</summary>
        public uint Child
        {
            get { return _child; }
        }

        /// <summary>Version prefix stored with this key. A key created from a seed uses Kaspa mainnet <c>kprv</c>.</summary>
        public uint Version
        {
            get { return _version; }
        }

        /// <summary>True when this node itself was derived with the hardened bit.</summary>
        public bool IsHardened
        {
            get { return (_child & 0x80000000u) != 0; }
        }

        /// <summary>Extended public key at this node. The version becomes the matching public prefix.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// Unknown extended private key version. ECDSA public key is not on the curve. ECDSA public key must be
        /// compressed (0x02 or 0x03). ECDSA public key must be 33 bytes.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">Stored private key is no longer valid.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public ExtPubKey Neuter()
        {
            AssertNotDisposed();
            return new ExtPubKey(
                _key.PubKeyECDSA,
                _chainCode,
                _depth,
                _parentFingerprint,
                _child,
                HdKeyVersions.ToPublic(_version));
        }

        /// <summary>Derives the child at <paramref name="index"/>. The high bit means hardened.</summary>
        /// <exception cref="System.ArgumentException">
        /// Private key is zero or not in the secp256k1 range. Private key must be 32 bytes. Compressed public
        /// key must be 33 bytes. An HD fingerprint is 4 bytes. ECDSA public key is not on the curve. ECDSA
        /// public key must be compressed (0x02 or 0x03). ECDSA public key must be 33 bytes.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// BIP32 child private key is invalid. Cannot derive a child at depth 255. Stored private key is no
        /// longer valid. BIP32 parent private key is invalid.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public ExtKey Derive(uint index)
        {
            AssertNotDisposed();
            if (_depth == byte.MaxValue)
            {
                throw new InvalidOperationException("Cannot derive a child at depth 255.");
            }

            var data = new byte[37];
            var secret = _key.ToBytes();
            try
            {
                if ((index & 0x80000000u) != 0)
                {
                    secret.CopyTo(data, 1);
                }
                else
                {
                    _key.PubKeyECDSA.ToBytes().CopyTo(data, 0);
                }

                WriteU32BE(data.AsSpan(33), index);
                var mac = Bip32Curve.HmacSha512(_chainCode, data);
                try
                {
                    var tweak = mac.AsSpan(0, 32);
                    byte[] childSecret;
                    try
                    {
                        childSecret = Bip32Curve.TweakPrivate(secret, tweak);
                    }
                    catch (InvalidOperationException exception)
                    {
                        throw new InvalidOperationException("BIP32 child private key is invalid.", exception);
                    }

                    try
                    {
                        var chain = mac.AsSpan(32, 32).ToArray();
                        var fingerprint = HDFingerprint.FromCompressedPublicKey(_key.PubKeyECDSA.ToBytes());
                        return new ExtKey(new Key(childSecret), chain, (byte)(_depth + 1), fingerprint, index, _version);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(childSecret);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(mac);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
                CryptographicOperations.ZeroMemory(data);
            }
        }

        /// <summary>Derives each index in <paramref name="path"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="path"/> is null.</exception>
        /// <exception cref="System.ArgumentException">
        /// Private key is zero or not in the secp256k1 range. Private key must be 32 bytes. Compressed public
        /// key must be 33 bytes. An HD fingerprint is 4 bytes. ECDSA public key is not on the curve. ECDSA
        /// public key must be compressed (0x02 or 0x03). ECDSA public key must be 33 bytes.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// BIP32 child private key is invalid. Cannot derive a child at depth 255. Stored private key is no
        /// longer valid. BIP32 parent private key is invalid.
        /// </exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public ExtKey Derive(KeyPath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var current = this;
            for (var i = 0; i < path.Indexes.Count; i++)
            {
                var next = current.Derive(path.Indexes[i]);
                if (!ReferenceEquals(current, this))
                {
                    current.Dispose();
                }

                current = next;
            }

            return current;
        }

        /// <summary>Base58Check using the version stored on this key.</summary>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public string ToBase58()
        {
            return ToBase58(_version);
        }

        /// <summary>Base58Check using <paramref name="version"/> as the 4-byte prefix.</summary>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public string ToBase58(uint version)
        {
            AssertNotDisposed();
            var payload = new byte[PayloadLength];
            try
            {
                WriteU32BE(payload.AsSpan(0, 4), version);
                payload[4] = _depth;
                _parentFingerprint.ToBytes().CopyTo(payload, 5);
                WriteU32BE(payload.AsSpan(9, 4), _child);
                _chainCode.CopyTo(payload, 13);
                payload[45] = 0x00;
                _key.ToBytes().CopyTo(payload, 46);
                return Base58CheckEncoder.Encode(payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }

        /// <summary>Base58Check with the Kaspa private prefix of <paramref name="network"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="network"/> is null.</exception>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public string ToString(Network network)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            return ToBase58(network.ExtPrivKeyVersion);
        }

        /// <summary>Base58Check with the stored version.</summary>
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public override string ToString()
        {
            return ToBase58();
        }

        /// <summary>Zeros the private key.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _key.Dispose();
            CryptographicOperations.ZeroMemory(_chainCode);
            _disposed = true;
        }

        /// <inheritdoc />
        public bool Equals(ExtKey? other)
        {
            if (other is null || _disposed || other._disposed)
            {
                return false;
            }

            return _depth == other._depth
                && _child == other._child
                && _parentFingerprint.Equals(other._parentFingerprint)
                && _key.Equals(other._key)
                && _chainCode.AsSpan().SequenceEqual(other._chainCode);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as ExtKey);
        }

        /// <inheritdoc />
        /// <exception cref="System.ObjectDisposedException">The Key has been disposed. The ExtKey has been disposed.</exception>
        public override int GetHashCode()
        {
            AssertNotDisposed();
            return (_depth * 397) ^ (int)_child ^ _key.GetHashCode();
        }

        internal static uint ReadU32BE(ReadOnlySpan<byte> bytes)
        {
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        internal static void WriteU32BE(Span<byte> destination, uint value)
        {
            destination[0] = (byte)(value >> 24);
            destination[1] = (byte)(value >> 16);
            destination[2] = (byte)(value >> 8);
            destination[3] = (byte)value;
        }

        private void AssertNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ExtKey));
            }
        }
    }
}
