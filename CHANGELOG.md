# Changelog

## 0.1.11-alpha

Borsh wRPC client beside the existing JSON client.

- `KaspaWrpcBorshClient` speaks rusty-kaspa workflow-rpc Borsh on a binary WebSocket. The layout follows rusty-kaspa `4edcbbccb449f84832afb1ca157904b35f37d006` and workflow-rs `7e2958098bbe0647adcfcc0dfc174e438a60699e` (workflow-rpc and workflow-serializer 0.19.0). A request is a `BorshReqHeader` (optional u64 id, `RpcApiOps` as one byte) plus a length-prefixed versioned serializer body. A success response is a Borsh `Result` around that body. borsh 1.8.1 writes `Ok` as `1u8` and `Err` as `0u8`. A notification is a length-prefixed `Notification` serializer, not a `Result`.
- `KaspaRpcClient.Create(url, encoding = Auto)` returns a `KaspaRpcConnection` and does not connect. Auto selects Borsh for 17110 (mainnet), 17210 (testnet), 17510 (simnet), and 17610 (devnet), and JSON for 18110, 18210, 18510, and 18610. Simnet is 17510 and 18510. It is not 17310 or 18310. An unknown port throws. `KaspaRpcEncoding.Borsh` or `KaspaRpcEncoding.Json` overrides the port.
- The Borsh socket uses the same connect and request timeouts, reconnect policy, 16 MiB frame cap, `State`, `StateChanged`, and `Reconnected` behavior as the JSON socket.
- `IKaspaRpcClient` adds `GetBalancesByAddressesAsync`, `NotifyUtxosChangedAsync`, `NotifyVirtualDaaScoreChangedAsync`, and `NotifyBlockAddedAsync`. The JSON client implements them too. Subscriptions send workflow-rpc `Subscribe` (op 3) and `Unsubscribe` (op 4) with a `Scope`. The node router does not register the older `Notify*` ops. Disposing the `KaspaSubscription` stops that scope.
- Fee-estimate buckets are confirmed in rusty-kaspa `rpc/core/src/model/feerate_estimate.rs`: a required `priorityBucket`, then `normalBuckets` and `lowBuckets`. Each bucket is `feerate` and `estimatedSeconds` as two little-endian f64 values.
- Tests cover hand-computed Borsh frames for each request and response (cargo was not available), a fake Borsh WebSocket round trip, a subscription notification, and port auto-selection. `SubmitTransaction` is sent only to that fake server.

## 0.1.10-alpha

Review follow-up on local main.

- Toccata activation DAA scores: mainnet 474165565 and testnet-10 467579632, from rusty-kaspa `90dbf074` `consensus/core/src/config/params.rs` lines 724 and 785. Current master removed the field.
- Pre-Toccata minimum relay fee is 1 sompi/gram (`LEGACY_MINIMUM_RELAY_TRANSACTION_FEE` at `90dbf074` `mining/src/mempool/config.rs` line 28). Post-Toccata default is 100. `FeeRate.MinimumRelay` and `MassCalculator.MinimumFeeRate` stay 100.
- Forcing pre-Toccata mode (`MassPolicy.PreToccata`, `toccataActive: false`, or a DAA below the activation score) yields 1 sompi/gram, which current (post-Toccata) nodes may reject; use the default policy for live networks.
- Earlier 0.1.10 review work: BIP39 checksum refusal on derive, folded-change cap, WebSocket frame bound, disconnect tests that reset the managed socket, and a single name for folded change.

### Breaking changes vs 0.1.9-alpha

- `Mnemonic.Words` is now `IReadOnlyList<string>` (read-only).
- `Mnemonic.ToString()` and `ExtKey.ToString()` no longer output plaintext secrets; use `ToPhrase()` / `ExportXprv()`.
- Derivation from a mnemonic with an invalid checksum now throws unless `allowInvalidChecksum: true` is passed.
- Default JSON serialization of `Key`, `ExtKey` and `Mnemonic` is refused; only `NKaspaJsonSerializer` with `allowPrivateKeys: true` serializes them.
- Mnemonics are normalized (NFKD, case-insensitive, zero-width characters/BOM stripped) before parsing.
- Change folded into the fee is capped at min(max(MinimumChange, 0.1 KAS), 100x fee); larger amounts throw. `ChangeFoldedIntoFee` replaces `ChangeFolded` / `FoldedChange`.
- With `toccataActive: false` the minimum relay fee rate is 1 sompi/gram.
