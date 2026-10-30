namespace NKaspa.DataEncoders
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.DataEncoders.DataEncoder.
    /// Turns raw bytes into text and back.
    /// </summary>
    public abstract class DataEncoder
    {
        /// <summary>Encodes the whole buffer.</summary>
        public string EncodeData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return EncodeData(data, 0, data.Length);
        }

        /// <summary>Encodes <paramref name="count"/> bytes starting at <paramref name="offset"/>.</summary>
        public abstract string EncodeData(byte[] data, int offset, int count);

        /// <summary>Encodes a span. The default copies the span.</summary>
        public virtual string EncodeData(ReadOnlySpan<byte> data)
        {
            return EncodeData(data.ToArray(), 0, data.Length);
        }

        /// <summary>Decodes text produced by <see cref="EncodeData(byte[])"/>.</summary>
        public abstract byte[] DecodeData(string encoded);
    }
}
