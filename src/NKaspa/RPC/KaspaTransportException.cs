using System.Globalization;

namespace NKaspa.RPC
{
    /// <summary>
    /// The WebSocket dropped, or a send found it already closed.
    /// This type extends <see cref="InvalidOperationException"/>. The message is <c>WebSocket disconnected.</c>
    /// </summary>
    public class KaspaConnectionClosedException : InvalidOperationException
    {
        /// <summary>Creates an exception.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaConnectionClosedException(string message)
            : base(message ?? throw new ArgumentNullException(nameof(message)))
        {
        }

        /// <summary>Creates an exception with <paramref name="innerException"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null. <paramref name="innerException"/> is null.</exception>
        public KaspaConnectionClosedException(string message, Exception innerException)
            : base(
                message ?? throw new ArgumentNullException(nameof(message)),
                innerException ?? throw new ArgumentNullException(nameof(innerException)))
        {
        }
    }

    /// <summary>
    /// One WebSocket message is larger than <see cref="KaspaWebSocketOptions.MaxMessageBytes"/>.
    /// This type extends <see cref="InvalidOperationException"/>. The message names <see cref="LimitBytes"/> and does not include the payload.
    /// </summary>
    public class KaspaMessageTooLargeException : InvalidOperationException
    {
        /// <summary>Creates an exception for <paramref name="limitBytes"/>.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="limitBytes"/> is not positive.</exception>
        public KaspaMessageTooLargeException(int limitBytes)
            : base("WebSocket message exceeds the maximum of " + limitBytes.ToString(CultureInfo.InvariantCulture) + " bytes.")
        {
            if (limitBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limitBytes), "Max message size must be positive.");
            }

            LimitBytes = limitBytes;
        }

        /// <summary>The <see cref="KaspaWebSocketOptions.MaxMessageBytes"/> cap, in bytes.</summary>
        public int LimitBytes { get; }
    }

    /// <summary>
    /// A connect or request exceeded its timeout.
    /// This type extends <see cref="TimeoutException"/>. Caller cancellation stays <see cref="OperationCanceledException"/>.
    /// </summary>
    public class KaspaRpcTimeoutException : TimeoutException
    {
        /// <summary>Creates an exception. <paramref name="timeout"/> is the limit that elapsed.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaRpcTimeoutException(string message, TimeSpan timeout)
            : base(message ?? throw new ArgumentNullException(nameof(message)))
        {
            Timeout = timeout;
        }

        /// <summary>The connect or request limit that elapsed.</summary>
        public TimeSpan Timeout { get; }
    }
}
