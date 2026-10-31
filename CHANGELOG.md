# Changelog

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
