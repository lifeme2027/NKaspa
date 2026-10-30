namespace NKaspa
{
    /// <summary>
    /// Little-endian writer for Kaspa transaction and sighash bytes.
    /// Lengths are u64, not Bitcoin compact size.
    /// </summary>
    internal sealed class KaspaWriter
    {
        private byte[] _buffer = new byte[256];
        private int _length;

        public void U8(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void U16(ushort value)
        {
            Ensure(2);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
        }

        public void U32(uint value)
        {
            Ensure(4);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
            _buffer[_length++] = (byte)(value >> 16);
            _buffer[_length++] = (byte)(value >> 24);
        }

        public void U64(ulong value)
        {
            Ensure(8);
            for (var i = 0; i < 8; i++)
            {
                _buffer[_length++] = (byte)(value >> (8 * i));
            }
        }

        public void Raw(ReadOnlySpan<byte> data)
        {
            data.CopyTo(Ensure(data.Length).AsSpan(_length));
            _length += data.Length;
        }

        public void VarBytes(ReadOnlySpan<byte> data)
        {
            U64((ulong)data.Length);
            Raw(data);
        }

        public byte[] ToArray()
        {
            var copy = new byte[_length];
            _buffer.AsSpan(0, _length).CopyTo(copy);
            return copy;
        }

        private byte[] Ensure(int extra)
        {
            var needed = _length + extra;
            if (needed > _buffer.Length)
            {
                var grown = new byte[Math.Max(needed, _buffer.Length * 2)];
                _buffer.AsSpan(0, _length).CopyTo(grown);
                _buffer = grown;
            }

            return _buffer;
        }
    }

    /// <summary>Little-endian reader for a version-0 transaction.</summary>
    internal ref struct KaspaReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _offset;

        public KaspaReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _offset = 0;
        }

        public bool IsEnd
        {
            get { return _offset == _data.Length; }
        }

        public byte U8()
        {
            if (_offset >= _data.Length)
            {
                throw new FormatException("Transaction is truncated.");
            }

            return _data[_offset++];
        }

        public ushort U16()
        {
            if (_data.Length - _offset < 2)
            {
                throw new FormatException("Transaction is truncated.");
            }

            var value = (ushort)(_data[_offset] | (_data[_offset + 1] << 8));
            _offset += 2;
            return value;
        }

        public uint U32()
        {
            if (_data.Length - _offset < 4)
            {
                throw new FormatException("Transaction is truncated.");
            }

            uint value = _data[_offset]
                | ((uint)_data[_offset + 1] << 8)
                | ((uint)_data[_offset + 2] << 16)
                | ((uint)_data[_offset + 3] << 24);
            _offset += 4;
            return value;
        }

        public ulong U64()
        {
            if (_data.Length - _offset < 8)
            {
                throw new FormatException("Transaction is truncated.");
            }

            ulong value = 0;
            for (var i = 0; i < 8; i++)
            {
                value |= (ulong)_data[_offset + i] << (8 * i);
            }

            _offset += 8;
            return value;
        }

        public byte[] Take(int length)
        {
            if (length < 0 || _data.Length - _offset < length)
            {
                throw new FormatException("Transaction is truncated.");
            }

            var copy = _data.Slice(_offset, length).ToArray();
            _offset += length;
            return copy;
        }

        public byte[] VarBytes()
        {
            var length = U64();
            if (length > int.MaxValue)
            {
                throw new FormatException("Transaction field is too long.");
            }

            return Take((int)length);
        }
    }
}
