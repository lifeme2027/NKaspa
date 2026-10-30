using NBitcoin.Secp256k1;
using NKaspa.Crypto;
using NKaspa.DataEncoders;

namespace NKaspa
{
    /// <summary>
    /// 33-byte compressed secp256k1 public key.
    /// Kaspa counterpart of NBitcoin.PubKey for the ECDSA address role.
    /// The first byte is 0x02 or 0x03.
    /// </summary>
    public sealed class PubKeyECDSA : IEquatable<PubKeyECDSA>
    {
        /// <summary>Length of the compressed key.</summary>
        public const int Length = 33;

        private readonly byte[] _compressed;

        /// <summary>Copies a 33-byte compressed key and checks that it is on secp256k1.</summary>
        public PubKeyECDSA(ReadOnlySpan<byte> compressed)
        {
            if (compressed.Length != Length)
            {
                throw new ArgumentException("ECDSA public key must be 33 bytes.", nameof(compressed));
            }

            if (compressed[0] != 0x02 && compressed[0] != 0x03)
            {
                throw new ArgumentException("ECDSA public key must be compressed (0x02 or 0x03).", nameof(compressed));
            }

            if (!ECPubKey.TryCreate(compressed, null, out _, out _))
            {
                throw new ArgumentException("ECDSA public key is not on the curve.", nameof(compressed));
            }

            _compressed = compressed.ToArray();
        }

        /// <summary>Parses a 66-character hex compressed key.</summary>
        public static PubKeyECDSA Parse(string hex)
        {
            if (hex == null)
            {
                throw new ArgumentNullException(nameof(hex));
            }

            return new PubKeyECDSA(Encoders.Hex.DecodeData(hex));
        }

        /// <summary>A copy of the 33-byte compressed key.</summary>
        public byte[] ToBytes()
        {
            return (byte[])_compressed.Clone();
        }

        /// <summary>The 32-byte x coordinate. The parity byte is not included.</summary>
        public byte[] XOnly
        {
            get
            {
                var x = new byte[32];
                _compressed.AsSpan(1).CopyTo(x);
                return x;
            }
        }

        /// <summary>Lowercase hex of the compressed key.</summary>
        public override string ToString()
        {
            return Encoders.Hex.EncodeData(_compressed);
        }

        /// <summary>ECDSA pay-to-pubkey address on <paramref name="network"/>.</summary>
        public KaspaPubKeyEcdsaAddress GetAddress(Network network)
        {
            return new KaspaPubKeyEcdsaAddress(_compressed, network);
        }

        /// <summary>Version-0 ECDSA pay-to-pubkey script.</summary>
        public ScriptPubKey GetScriptPubKey()
        {
            return ScriptPubKey.PayToPubKeyEcdsa(_compressed);
        }

        /// <summary>Verifies a compact ECDSA signature over a 32-byte message.</summary>
        public bool Verify(ECDSASignature signature, ReadOnlySpan<byte> hash32)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (hash32.Length != 32)
            {
                return false;
            }

            if (!ECPubKey.TryCreate(_compressed, null, out _, out var key) || key == null)
            {
                return false;
            }

            if (!SecpECDSASignature.TryCreateFromCompact(signature.ToBytes(), out var parsed) || parsed == null)
            {
                return false;
            }

            return key.SigVerify(parsed, hash32);
        }

        /// <inheritdoc />
        public bool Equals(PubKeyECDSA? other)
        {
            return other is not null && _compressed.AsSpan().SequenceEqual(other._compressed);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return Equals(obj as PubKeyECDSA);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = 17;
            for (var i = 0; i < _compressed.Length; i++)
            {
                hash = (hash * 31) + _compressed[i];
            }

            return hash;
        }
    }
}
