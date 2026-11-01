using System.Net.WebSockets;

namespace NKaspa.RPC
{
    /// <summary>
    /// A wRPC failure a wallet can branch on.
    /// <see cref="KaspaRpcException"/> stays the generic node error.
    /// <see cref="KaspaRpcEncodingMismatchException"/> is not part of this family.
    /// </summary>
    public class KaspaWrpcException : Exception
    {
        /// <summary>Creates an exception.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaWrpcException(string message)
            : this(message, null)
        {
        }

        /// <summary>Creates an exception with <paramref name="innerException"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaWrpcException(string message, Exception? innerException)
            : base(message ?? throw new ArgumentNullException(nameof(message)), innerException)
        {
        }
    }

    /// <summary>
    /// The socket could not be opened, or it dropped.
    /// The message is <c>Kaspa node is unreachable.</c> when the failure is a transport exception.
    /// A Borsh server error <c>connection is closed</c> uses that text.
    /// <see cref="KaspaWebSocketChannel"/> still throws <see cref="TimeoutException"/> itself. This type wraps that timeout at the client.
    /// </summary>
    public sealed class KaspaNodeUnreachableException : KaspaWrpcException
    {
        /// <summary>Transport failure message.</summary>
        public const string DefaultMessage = "Kaspa node is unreachable.";

        /// <summary>Creates an exception.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaNodeUnreachableException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// The node is a different network than the caller asked for, or the RPC error is a rusty-kaspa network-id error.
    /// Those display strings come from <c>consensus/core/src/network.rs</c> <c>NetworkIdError</c> and <c>NetworkTypeError</c>.
    /// </summary>
    public sealed class KaspaWrongNetworkException : KaspaWrpcException
    {
        /// <summary>Creates an exception. <paramref name="reportedNetworkId"/> and <paramref name="expectedNetwork"/> may be null for an RPC text error.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaWrongNetworkException(string message, string? reportedNetworkId, string? expectedNetwork, Exception? innerException = null)
            : base(message, innerException)
        {
            ReportedNetworkId = reportedNetworkId;
            ExpectedNetwork = expectedNetwork;
        }

        /// <summary>Network id from the node, when <see cref="KaspaWrpcErrors.Ensure"/> read <c>getServerInfo</c>.</summary>
        public string? ReportedNetworkId { get; }

        /// <summary>Network the caller passed to <see cref="KaspaWrpcErrors.Ensure"/>, when that check failed.</summary>
        public string? ExpectedNetwork { get; }
    }

    /// <summary>
    /// The node is not synced.
    /// <see cref="KaspaWrpcErrors.Ensure"/> uses <c>Node is not synced.</c>
    /// The RPC text <c>consensus is currently in a transitional ibd state</c> is rusty-kaspa <c>RpcError::ConsensusInTransitionalIbdState</c> in <c>rpc/core/src/error.rs</c>.
    /// </summary>
    public sealed class KaspaNodeNotSyncedException : KaspaWrpcException
    {
        /// <summary>Message used by <see cref="KaspaWrpcErrors.Ensure"/>.</summary>
        public const string DefaultMessage = "Node is not synced.";

        /// <summary>Creates an exception.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaNodeNotSyncedException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// The node was started without <c>--utxoindex</c>.
    /// rusty-kaspa <c>RpcError::NoUtxoIndex</c> in <c>rpc/core/src/error.rs</c> displays
    /// <c>Method unavailable. Run the node with the --utxoindex argument.</c>
    /// <see cref="KaspaWrpcErrors.Ensure"/> uses <c>Node does not have the UTXO index enabled.</c> when <c>hasUtxoIndex</c> is false.
    /// </summary>
    public sealed class KaspaUtxoIndexNotEnabledException : KaspaWrpcException
    {
        /// <summary>Message used by <see cref="KaspaWrpcErrors.Ensure"/>.</summary>
        public const string DefaultMessage = "Node does not have the UTXO index enabled.";

        /// <summary>Official <c>RpcError::NoUtxoIndex</c> display text.</summary>
        public const string NodeMessage = "Method unavailable. Run the node with the --utxoindex argument.";

        /// <summary>Creates an exception.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="message"/> is null.</exception>
        public KaspaUtxoIndexNotEnabledException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }

    /// <summary>Maps node errors and checks <c>getServerInfo</c>. <see cref="IKaspaRpcClient.GetServerInfoAsync"/> does not call this.</summary>
    public static class KaspaWrpcErrors
    {
        /// <summary>
        /// Reads <see cref="IKaspaRpcClient.GetServerInfoAsync"/> and throws when the node is the wrong network, unsynced, or missing the UTXO index.
        /// <paramref name="requireSynced"/> and <paramref name="requireUtxoIndex"/> default to true at the interface.
        /// <c>testnet-10</c> matches <see cref="Network.Testnet"/>.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="client"/> is null. <paramref name="network"/> is null.</exception>
        /// <exception cref="NKaspa.RPC.KaspaWrongNetworkException">Node reported network '{id}'. Expected '{name}'.</exception>
        /// <exception cref="NKaspa.RPC.KaspaNodeNotSyncedException">Node is not synced.</exception>
        /// <exception cref="NKaspa.RPC.KaspaUtxoIndexNotEnabledException">Node does not have the UTXO index enabled.</exception>
        /// <exception cref="NKaspa.RPC.KaspaNodeUnreachableException">Kaspa node is unreachable.</exception>
        /// <exception cref="NKaspa.RPC.KaspaRpcException">RPC response has no params. RPC response id does not match the request. RPC error.</exception>
        public static async Task Ensure(IKaspaRpcClient client, Network network, bool requireSynced, bool requireUtxoIndex, CancellationToken cancellationToken)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            var info = await client.GetServerInfoAsync(cancellationToken).ConfigureAwait(false);
            if (!Network.TryGetNetwork(info.NetworkId, out var reported) || reported == null || !reported.Equals(network))
            {
                throw new KaspaWrongNetworkException(
                    "Node reported network '" + (info.NetworkId ?? string.Empty) + "'. Expected '" + network.Name + "'.",
                    info.NetworkId,
                    network.Name);
            }

            if (requireSynced && !info.IsSynced)
            {
                throw new KaspaNodeNotSyncedException(KaspaNodeNotSyncedException.DefaultMessage);
            }

            if (requireUtxoIndex && !info.HasUtxoIndex)
            {
                throw new KaspaUtxoIndexNotEnabledException(KaspaUtxoIndexNotEnabledException.DefaultMessage);
            }
        }

        internal static Exception FromRpc(KaspaRpcException error)
        {
            var message = error.Message ?? string.Empty;
            if (message.IndexOf("--utxoindex", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new KaspaUtxoIndexNotEnabledException(message, error);
            }

            if (string.Equals(message, "consensus is currently in a transitional ibd state", StringComparison.OrdinalIgnoreCase))
            {
                return new KaspaNodeNotSyncedException(message, error);
            }

            if (IsNetworkIdError(message))
            {
                return new KaspaWrongNetworkException(message, null, null, error);
            }

            if (string.Equals(message, "connection is closed", StringComparison.Ordinal))
            {
                return new KaspaNodeUnreachableException(message, error);
            }

            return error;
        }

        internal static Exception Transport(Exception exception)
        {
            if (exception is KaspaWrpcException or KaspaRpcException or KaspaRpcEncodingMismatchException or OperationCanceledException or ObjectDisposedException)
            {
                return exception;
            }

            if (exception is InvalidOperationException invalid)
            {
                var text = invalid.Message ?? string.Empty;
                if (text.IndexOf("disconnected", StringComparison.Ordinal) >= 0)
                {
                    return new KaspaNodeUnreachableException(KaspaNodeUnreachableException.DefaultMessage, exception);
                }

                return exception;
            }

            if (exception is TimeoutException or WebSocketException or IOException)
            {
                return new KaspaNodeUnreachableException(KaspaNodeUnreachableException.DefaultMessage, exception);
            }

            return exception;
        }

        private static bool IsNetworkIdError(string message)
        {
            return Starts(message, "Invalid network type:")
                || Starts(message, "Invalid network id:")
                || Starts(message, "Invalid network name prefix:")
                || Starts(message, "Invalid network suffix:")
                || Starts(message, "Missing network suffix:")
                || Starts(message, "Network suffix required for network type:")
                || Starts(message, "Unexpected extra token:");
        }

        private static bool Starts(string message, string prefix)
        {
            return message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
