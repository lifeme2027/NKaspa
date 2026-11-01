namespace NKaspa.RPC
{
    /// <summary>
    /// wRPC Borsh client. The wire format is rusty-kaspa <c>4edcbbccb449f84832afb1ca157904b35f37d006</c>
    /// over workflow-rpc <c>0.19.0</c> (workflow-rs <c>7e2958098bbe0647adcfcc0dfc174e438a60699e</c>).
    /// This type does not open a socket by itself. Pass a channel, or use <see cref="KaspaRpcClient.Create"/>.
    /// Subscriptions call <c>Subscribe</c> / <c>Unsubscribe</c> (ops 3 and 4). The server in that commit
    /// does not register <c>NotifyUtxosChanged</c>, <c>NotifyVirtualDaaScoreChanged</c>, or <c>NotifyBlockAdded</c>.
    /// </summary>
    public sealed class KaspaWrpcBorshClient : IKaspaRpcClient
    {
        private readonly IKaspaBorshChannel _channel;
        private readonly object _gate = new object();
        private readonly List<Action<byte[]>> _listeners = new List<Action<byte[]>>();
        private long _nextId;
        private bool _hooked;

        /// <summary>Creates a client over <paramref name="channel"/>.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="channel"/> is null.</exception>
        public KaspaWrpcBorshClient(IKaspaBorshChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <inheritdoc />
        public Task EnsureReadyAsync(Network network, bool requireSynced = true, bool requireUtxoIndex = true, CancellationToken cancellationToken = default)
        {
            return KaspaWrpcErrors.Ensure(this, network, requireSynced, requireUtxoIndex, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
        {
            return CallAsync(KaspaBorshCodec.OpGetServerInfo, KaspaBorshCodec.EmptyPayload(), KaspaBorshCodec.ReadServerInfo, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcBlockDagInfo> GetBlockDagInfoAsync(CancellationToken cancellationToken = default)
        {
            return CallAsync(KaspaBorshCodec.OpGetBlockDagInfo, KaspaBorshCodec.EmptyPayload(), KaspaBorshCodec.ReadBlockDagInfo, cancellationToken);
        }

        /// <inheritdoc />
        public Task<ulong> GetSinkBlueScoreAsync(CancellationToken cancellationToken = default)
        {
            return CallAsync(KaspaBorshCodec.OpGetSinkBlueScore, KaspaBorshCodec.EmptyPayload(), KaspaBorshCodec.ReadSinkBlueScore, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcVirtualChain> GetVirtualChainFromBlockAsync(uint256 startHash, bool includeAcceptedTransactionIds = false, ulong? minConfirmationCount = null, CancellationToken cancellationToken = default)
        {
            if (startHash == null)
            {
                throw new ArgumentNullException(nameof(startHash));
            }

            var payload = KaspaBorshCodec.VirtualChainPayload(startHash, includeAcceptedTransactionIds, minConfirmationCount);
            return CallAsync(KaspaBorshCodec.OpGetVirtualChainFromBlock, payload, KaspaBorshCodec.ReadVirtualChain, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<RpcUtxo>> GetUtxosByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            return CallAsync(KaspaBorshCodec.OpGetUtxosByAddresses, KaspaBorshCodec.AddressesPayload(addresses), KaspaBorshCodec.ReadUtxos, cancellationToken);
        }

        /// <inheritdoc />
        public Task<Money> GetBalanceByAddressAsync(string address, CancellationToken cancellationToken = default)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            return CallBalanceAsync(address, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<RpcAddressBalance>> GetBalancesByAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            return CallAsync(KaspaBorshCodec.OpGetBalancesByAddresses, KaspaBorshCodec.AddressesPayload(addresses), KaspaBorshCodec.ReadBalances, cancellationToken);
        }

        /// <inheritdoc />
        public Task<uint256> SubmitTransactionAsync(Transaction transaction, bool allowOrphan = false, CancellationToken cancellationToken = default)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            var id = NextId();
            var frame = KaspaBorshCodec.SubmitRequest(id, transaction, allowOrphan);
            return CallFrameAsync(frame, KaspaBorshCodec.ReadTransactionId, cancellationToken);
        }

        /// <inheritdoc />
        public Task<RpcFeeEstimate> GetFeeEstimateAsync(CancellationToken cancellationToken = default)
        {
            return CallAsync(KaspaBorshCodec.OpGetFeeEstimate, KaspaBorshCodec.EmptyPayload(), KaspaBorshCodec.ReadFeeEstimate, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<RpcChainBlock?> GetBlockAsync(uint256 hash, bool includeTransactions = false, CancellationToken cancellationToken = default)
        {
            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            byte[] body;
            try
            {
                var id = NextId();
                body = await ExchangeAsync(KaspaBorshCodec.GetBlockRequest(id, hash, includeTransactions), cancellationToken).ConfigureAwait(false);
            }
            catch (KaspaRpcException)
            {
                return null;
            }

            return KaspaBorshCodec.ReadGetBlock(body, hash);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyUtxosChangedAsync(IReadOnlyList<string> addresses, Action<RpcUtxosChanged> onChanged, CancellationToken cancellationToken = default)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            if (onChanged == null)
            {
                throw new ArgumentNullException(nameof(onChanged));
            }

            var scope = KaspaBorshCodec.ScopePayload(KaspaBorshCodec.ScopeUtxosChanged, addresses);
            return SubscribeAsync(scope, body =>
            {
                var note = KaspaBorshCodec.TryReadUtxosChanged(body);
                if (note != null)
                {
                    onChanged(note);
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyVirtualDaaScoreChangedAsync(Action<RpcVirtualDaaScoreChanged> onChanged, CancellationToken cancellationToken = default)
        {
            if (onChanged == null)
            {
                throw new ArgumentNullException(nameof(onChanged));
            }

            var scope = KaspaBorshCodec.ScopePayload(KaspaBorshCodec.ScopeVirtualDaaScoreChanged, null);
            return SubscribeAsync(scope, body =>
            {
                var score = KaspaBorshCodec.TryReadVirtualDaa(body);
                if (score.HasValue)
                {
                    onChanged(new RpcVirtualDaaScoreChanged(score.Value));
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public Task<KaspaSubscription> NotifyBlockAddedAsync(Action<RpcBlockAdded> onAdded, CancellationToken cancellationToken = default)
        {
            if (onAdded == null)
            {
                throw new ArgumentNullException(nameof(onAdded));
            }

            var scope = KaspaBorshCodec.ScopePayload(KaspaBorshCodec.ScopeBlockAdded, null);
            return SubscribeAsync(scope, body =>
            {
                var block = KaspaBorshCodec.TryReadBlockAdded(body);
                if (block != null)
                {
                    onAdded(block);
                }
            }, cancellationToken);
        }

        private async Task<Money> CallBalanceAsync(string address, CancellationToken cancellationToken)
        {
            var id = NextId();
            var body = await ExchangeAsync(KaspaBorshCodec.BalanceRequest(id, address), cancellationToken).ConfigureAwait(false);
            return KaspaBorshCodec.ReadBalance(body);
        }

        private Task<T> CallAsync<T>(byte op, byte[] payload, Func<byte[], T> read, CancellationToken cancellationToken)
        {
            var frame = KaspaBorshCodec.EncodeRequest(NextId(), op, payload);
            return CallFrameAsync(frame, read, cancellationToken);
        }

        private async Task<T> CallFrameAsync<T>(byte[] frame, Func<byte[], T> read, CancellationToken cancellationToken)
        {
            var body = await ExchangeAsync(frame, cancellationToken).ConfigureAwait(false);
            return read(body);
        }

        private async Task<byte[]> ExchangeAsync(byte[] frame, CancellationToken cancellationToken)
        {
            var id = KaspaBorshCodec.ReadRequestId(frame);
            byte[] response;
            try
            {
                response = await _channel.ExchangeAsync(frame, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                var mapped = KaspaWrpcErrors.Transport(exception);
                if (ReferenceEquals(mapped, exception))
                {
                    throw;
                }

                throw mapped;
            }

            var incoming = KaspaBorshCodec.DecodeFrame(response);
            if (incoming.Error != null)
            {
                throw KaspaWrpcErrors.FromRpc(incoming.Error);
            }

            if (incoming.Id != id)
            {
                throw new KaspaRpcException(0, "RPC response id does not match the request.");
            }

            return incoming.Body;
        }

        private async Task<KaspaSubscription> SubscribeAsync(byte[] scope, Action<byte[]> handler, CancellationToken cancellationToken)
        {
            EnsureHook();
            lock (_gate)
            {
                _listeners.Add(handler);
            }

            try
            {
                await CallAsync(KaspaBorshCodec.OpSubscribe, scope, static body => body, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_gate)
                {
                    _listeners.Remove(handler);
                }

                throw;
            }

            return new KaspaSubscription(token =>
            {
                lock (_gate)
                {
                    _listeners.Remove(handler);
                }

                return CallAsync(KaspaBorshCodec.OpUnsubscribe, scope, static body => body, token);
            });
        }

        private void EnsureHook()
        {
            lock (_gate)
            {
                if (_hooked)
                {
                    return;
                }

                _channel.NotificationReceived += OnNotification;
                _hooked = true;
            }
        }

        private void OnNotification(byte[] frame)
        {
            BorshIncoming incoming;
            try
            {
                incoming = KaspaBorshCodec.DecodeFrame(frame);
            }
            catch (FormatException)
            {
                return;
            }
            catch (ArgumentNullException)
            {
                return;
            }

            if (incoming.Kind != KaspaBorshCodec.KindNotification || incoming.Error != null)
            {
                return;
            }

            Action<byte[]>[] listeners;
            lock (_gate)
            {
                listeners = _listeners.ToArray();
            }

            for (var i = 0; i < listeners.Length; i++)
            {
                try
                {
                    listeners[i](incoming.Body);
                }
                catch (Exception)
                {
                }
            }
        }

        private ulong NextId()
        {
            return (ulong)Interlocked.Increment(ref _nextId);
        }
    }
}
