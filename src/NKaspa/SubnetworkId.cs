namespace NKaspa
{
    /// <summary>
    /// 20-byte Kaspa subnetwork id.
    /// Native is 20 zero bytes. Coinbase is byte 0 = 1. Registry is byte 0 = 2.
    /// The remaining bytes of those three ids are zero. This matches rusty-kaspa <c>SubnetworkId::from_byte</c>.
    /// </summary>
    public readonly struct SubnetworkId : IEquatable<SubnetworkId>
    {
        /// <summary>Length of a subnetwork id.</summary>
        public const int Length = 20;

        private readonly byte[]? _bytes;

        /// <summary>Copies 20 bytes.</summary>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        public SubnetworkId(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != Length)
            {
                throw new ArgumentException("Subnetwork id must be 20 bytes.", nameof(bytes));
            }

            _bytes = bytes.ToArray();
        }

        /// <summary>The native subnetwork. All 20 bytes are zero.</summary>
        public static SubnetworkId Native
        {
            get { return default; }
        }

        /// <summary>Coinbase subnetwork. The first byte is 1.</summary>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        public static SubnetworkId Coinbase
        {
            get { return FromByte(1); }
        }

        /// <summary>Registry subnetwork. The first byte is 2.</summary>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        public static SubnetworkId Registry
        {
            get { return FromByte(2); }
        }

        /// <summary>An id whose first byte is <paramref name="first"/> and whose other bytes are zero.</summary>
        /// <exception cref="System.ArgumentException">Subnetwork id must be 20 bytes.</exception>
        public static SubnetworkId FromByte(byte first)
        {
            var bytes = new byte[Length];
            bytes[0] = first;
            return new SubnetworkId(bytes);
        }

        /// <summary>True when every byte is zero. A default value is native.</summary>
        public bool IsNative
        {
            get
            {
                if (_bytes == null)
                {
                    return true;
                }

                for (var i = 0; i < _bytes.Length; i++)
                {
                    if (_bytes[i] != 0)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>A copy of the 20 bytes. A default value returns 20 zeros.</summary>
        public byte[] ToBytes()
        {
            if (_bytes == null)
            {
                return new byte[Length];
            }

            return (byte[])_bytes.Clone();
        }

        /// <inheritdoc />
        public bool Equals(SubnetworkId other)
        {
            var left = _bytes;
            var right = other._bytes;
            for (var i = 0; i < Length; i++)
            {
                var a = left == null ? (byte)0 : left[i];
                var b = right == null ? (byte)0 : right[i];
                if (a != b)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is SubnetworkId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = 17;
            var bytes = _bytes;
            for (var i = 0; i < Length; i++)
            {
                hash = (hash * 31) + (bytes == null ? 0 : bytes[i]);
            }

            return hash;
        }

        /// <summary>Lowercase hex of the 20 bytes.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public override string ToString()
        {
            return DataEncoders.Encoders.Hex.EncodeData(ToBytes());
        }
    }
}
