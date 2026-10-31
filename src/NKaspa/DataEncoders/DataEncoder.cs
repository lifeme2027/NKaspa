namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.DataEncoders.DataEncoder.
    /// Turns raw bytes into text and back.
    /// </summary>
    public abstract class DataEncoder
    {
        /// <summary>Encodes the whole buffer.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="data"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public string EncodeData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return EncodeData(data, 0, data.Length);
        }

        /// <summary>Encodes <paramref name="count"/> bytes starting at <paramref name="offset"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="data"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> is outside the allowed range.</exception>
        public abstract string EncodeData(byte[] data, int offset, int count);

        /// <summary>Encodes a span. The default copies the span.</summary>
        /// <exception cref="System.ArgumentNullException">A required value is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">A value is outside the allowed range.</exception>
        public virtual string EncodeData(ReadOnlySpan<byte> data)
        {
            return EncodeData(data.ToArray(), 0, data.Length);
        }

        /// <summary>Decodes text produced by <see cref="EncodeData(byte[])"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="encoded"/> is null.</exception>
        /// <exception cref="System.FormatException">
        /// Invalid Base58Check checksum. Base58Check payload is shorter than the checksum. Base58 value does
        /// not fit in the decode buffer. Invalid Base58 character. Hex string contains a non-hex character. Hex
        /// string length must be even.
        /// </exception>
        public abstract byte[] DecodeData(string encoded);
    }
}
