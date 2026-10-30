using NBitcoin.Secp256k1;
using NKaspa.Crypto;
using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// 32-byte BIP-340 x-only public key. Kaspa counterpart of the public key
    /// used by a Schnorr address. This is not NBitcoin.PubKey, which is the
    /// 33-byte compressed SEC key. That role belongs to <see cref="PubKeyECDSA"/>.
    /// </summary>
    public sealed class PubKey : IEquatable<PubKey>
    {
        /// <summary>Length of the x-only key.</summary>
        public const int Length = 32;

        private readonly byte[] _xOnly;

        /// <summary>Copies a 32-byte x coordinate and checks that it is on secp256k1.</summary>
        public PubKey(ReadOnlySpan<byte> xOnly)
        {
            if (xOnly.Length != Length)
            {
                throw new ArgumentException("Schnorr x-only public key must be 32 bytes.", nameof(xOnly));
            }

            if (!ECXOnlyPubKey.TryCreate(xOnly, out _))
            {
                throw new ArgumentException("Schnorr x-only public key is not on the curve.", nameof(xOnly));
            }

            _xOnly = xOnly.ToArray();
        }

        /// <summary>Parses a 64-character hex x-only key.</summary>
        public static PubKey Parse(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            return new PubKey(Encoders.Hex.DecodeData(hex));
        }

        /// <summary>A copy of the 32-byte x-only key.</summary>
        public byte[] ToBytes()
        {
            return (byte[])_xOnly.Clone();
        }

        /// <summary>Writes the 32-byte key into <paramref name="destination"/>.</summary>
        public void WriteTo(Span<byte> destination)
        {
            if (destination.Length != Length)
            {
                throw new ArgumentException("Destination must be 32 bytes.", nameof(destination));
            }

            _xOnly.CopyTo(destination);
        }

        /// <summary>Lowercase hex of the x-only key.</summary>
        public override string ToString()
        {
            return Encoders.Hex.EncodeData(_xOnly);
        }

        /// <summary>
        /// Schnorr pay-to-pubkey address on <paramref name="network"/>.
        /// Kaspa counterpart of NBitcoin.PubKey.GetAddress for the default public-key address.
        /// </summary>
        public KaspaPubKeyAddress GetAddress(Network network)
        {
            return new KaspaPubKeyAddress(_xOnly, network);
        }

        /// <summary>Version-0 Schnorr pay-to-pubkey script.</summary>
        public ScriptPubKey GetScriptPubKey()
        {
            return ScriptPubKey.PayToPubKey(_xOnly);
        }

        /// <summary>Verifies a BIP-340 signature over a 32-byte message.</summary>
        public bool Verify(SchnorrSignature signature, ReadOnlySpan<byte> hash32)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (hash32.Length != 32)
            {
                return false;
            }

            if (!ECXOnlyPubKey.TryCreate(_xOnly, out var key) || key == null)
            {
                return false;
            }

            if (!SecpSchnorrSignature.TryCreate(signature.ToBytes(), out var parsed) || parsed == null)
            {
                return false;
            }

            return key.SigVerifyBIP340(parsed, hash32);
        }

        /// <inheritdoc />
        public bool Equals(PubKey? other)
        {
            return other is not null && _xOnly.AsSpan().SequenceEqual(other._xOnly);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as PubKey);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = 17;
            for (var i = 0; i < _xOnly.Length; i++)
            {
                hash = (hash * 31) + _xOnly[i];
            }

            return hash;
        }
    }
}
