# NBitcoin and NKaspa

Chinese: [NBitcoin 与 NKaspa 对照](NBitcoin-Mapping.md).

The NBitcoin tree used for this map is the read-only reference `01NBitcoin/NBitcoin-master/NBitcoin` (its core directories and main types). NKaspa keeps the counterpart's name where one exists. XML comments say `Kaspa counterpart of NBitcoin.X`. The behavior is Kaspa's.

Status values:

- **Implemented**: this release (0.1.11-alpha) has the type. The code is the limit of the claim.
- **Planned**: a later phase, or a Kaspa-specific equivalent that does not copy Bitcoin.
- **Not applicable**: Kaspa has no such mechanism, or it does not belong in this library.

## Root types

| NBitcoin | NKaspa | Status | Why |
| --- | --- | --- | --- |
| `BitcoinAddress` | `KaspaAddress` | Implemented | Abstract address. Parse, validate, `ToString`, `ScriptPubKey`. |
| `BitcoinPubKeyAddress` | `KaspaPubKeyEcdsaAddress` | Implemented | ECDSA role. The payload is a 33-byte compressed key (version `0x01`), not HASH160. |
| `BitcoinScriptAddress` | `KaspaScriptHashAddress` | Implemented | P2SH. The hash is BLAKE2b-256 (version `0x08`), not HASH160. |
| `BitcoinWitPubKeyAddress` / `BitcoinSegwitAddress` | `KaspaPubKeyAddress` | Implemented (role) | Default public-key address. Kaspa has no SegWit. The payload is a 32-byte Schnorr x-only key (version `0x00`). |
| `BitcoinWitScriptAddress` | — | Not applicable | No witness v0 script program. Script hashes use `KaspaScriptHashAddress`. |
| `TaprootAddress` and Taproot script trees | — | Not applicable | Kaspa Schnorr is not Bitcoin BIP341 Taproot. |
| `Key` / `PubKey` / `BitcoinSecret` | `Key` / `PubKey` / `PubKeyECDSA` | Implemented | `PubKey` is 32-byte Schnorr x-only. A 33-byte compressed key is `PubKeyECDSA`. There is no Bitcoin WIF / `BitcoinSecret`. |
| `Script` | `Script` / `ScriptPubKey` | Implemented | Parse, build, `ToString`, standard templates, and signature scripts. `Script.VerifyScript` runs version-0 scripts, including P2SH redeem scripts. |
| `ScriptEvaluationContext` | `ScriptEvaluationContext` / `TxScriptEngine` | Implemented | Execution context aligned with rusty-kaspa `crypto/txscript` `TxScriptEngine`. A version-0 budget is `sigOpCount * 100000 + 9999`. A version-1 budget is `computeBudget * 10000 + 9999`. A script is at most 1000000 bytes, an element at most 1000000 bytes, the data stack plus the alt stack at most 244 deep, and non-push opcodes at most 1000000. `OP_ZKPRECOMPILE` enforces the finalized tags, script units, field lengths, BN254 scalar range, Groth16 arity, and gamma metering, and RISC0 accepts only Poseidon2. BN254 pairing, curve-point checks, and the RISC0 STARK still return unimplemented and do not push true. Without an `ISeqCommitAccessor`, `OP_CHAINBLOCKSEQCOMMIT` is an illegal opcode. `WrpcSeqCommitAccessor` reads `getBlock` and `getBlockDagInfo` from the DAG sink, not the consensus selected parent. `ConsensusSeqCommitAccessor` walks the selected-parent chain from a frozen sink snapshot. Forward growth of that chain is not a reorg. A real reorg retries a limited number of times. Incomplete, inconsistent, or still-reorged data throws. A light client can only approach consensus. Matching it exactly requires a running node. |
| `Transaction` / `TxIn` / `TxOut` / `OutPoint` | same names | Implemented | Version 0 and Toccata version 1 serialization, transaction id, and transaction hash. A version-1 id is BLAKE3. `Transaction.Verify` runs the script engine on every input. Introspection and covenant opcodes whose rules are fixed do execute. |
| `TransactionBuilder` / `Coin` | `TransactionBuilder` / `Coin` | Implemented | A small builder: coin selection, Schnorr and ECDSA P2PK signing, change, an explicit fee, a fee-rate estimate, or the minimum relay fee only. `Verify` checks every input script. `Coin` is an `OutPoint` plus a `UtxoEntry`. The default version is 0. `SetVersion(1)` emits version 1 with a default compute budget of 10. `MustSpend` / `SpendAllCoins` force every named coin into the transaction and sign each input. |
| `TransactionSignature` / sighash | `SchnorrSignature` / `ECDSASignature` / `SigHashType` | Implemented | Kaspa sighash. `SigHashType.Single` is `0x04`. Bitcoin SINGLE is `0x03`. |
| `uint256` | `uint256` | Implemented | Parse, compare, and serialize 32 bytes. Display does not reverse the bytes. See below. |
| `uint160` / `uint128` | — | Not applicable | Addresses and transaction hashes are 32 bytes. |
| `Network` | `Network` | Implemented | Mainnet, Testnet, Simnet, Devnet, and address prefixes. |
| `Block` / `BlockHeader` | — | Planned | A Kaspa block is a GHOSTDAG block DAG, not a Bitcoin header. The fields have to be specified separately. |
| `Money` | `Money` | Implemented | Sompi. 1 KAS = 100,000,000 sompi. Protocol amounts are u64. |
| `FeeRate` | `FeeRate` | Implemented | Sompi per gram. After Toccata the minimum relay fee is 100 sompi times the larger of compute mass and normalized transient mass. Storage is not part of that floor. A standard one-input Schnorr payment is about 0.002 KAS (2036 grams, 203600 sompi). `GetFee` multiplies the rate by grams. `MassCalculator.EstimateFee` does the same and rejects a rate below 100. `EstimateMinimumRelayFee` uses the mass formula above. The builder floor is `MinimumRelayFeeRate`: 100 on the default policy. `MassPolicy.PreToccata` is 1 (rusty-kaspa `90dbf074` `mining/src/mempool/config.rs` line 28, `LEGACY_MINIMUM_RELAY_TRANSACTION_FEE = 1_000` sompi/kg, which is 1 sompi/gram). Bitcoin `Target`, work targets, and blue score stay at the block layer. This release does not have them. |
| `DefaultCoinSelector` | `DefaultCoinSelector` | Implemented | Coin selection. Coins are not grouped by `scriptPubKey`. When one coin covers the payment, the fee, and the minimum change, only the smallest such coin is used. When it covers the payment and the fee but change would fall below the minimum, that one coin is still the only input and the change is folded into the fee. Coins are combined only when no single coin is enough, and unused small coins are not pulled in. NBitcoin may spend several coins when a combination is closer to the target. Here one sufficient coin is enough. |
| `BitcoinStream` | (internal readers) | Implemented | The public entry points are `Transaction.ToBytes` / `FromBytes`. Lengths are u64, not Bitcoin compact size. There is no separate public stream type. |

`uint256`: `ToBytes()` and `ToString()` are the Kaspa wire order, the raw BLAKE2b digest bytes. RPC hex uses that order. Byte 0 is the first digest byte. NBitcoin `ToString()` reverses those 32 bytes so the hex looks like a big-endian integer. Numeric comparison treats the 32 bytes as a little-endian integer (byte 0 is the least significant byte), which matches how Kaspa compares a hash as a work integer.

## DataEncoders

| NBitcoin | NKaspa | Status | Why |
| --- | --- | --- | --- |
| `DataEncoder` | `DataEncoder` | Implemented | Base type for bytes and text. |
| `Encoders` | `Encoders` | Implemented | `Hex`, `KaspaBech32`, `Base58`, `Base58Check`. |
| `HexEncoder` | `HexEncoder` | Implemented | Lowercase hex. |
| `Bech32Encoder` | `KaspaBech32Encoder` | Implemented | Cashaddr style, 8-character (40-bit) checksum, charset `qpzry9x8gf2tvdw0s3jn54khce6mua7l`. Not BIP-173 and not bech32m. |
| `Base58Encoder` / `Base58CheckEncoder` | same names | Implemented | Extended keys use Base58Check. Addresses do not. |
| `Base32Encoder` / `Base64Encoder` / `ASCIIEncoder` | — | Not applicable | Not on the address path. Add them later only as general encoders, not as consensus. |

## Crypto

| NBitcoin | NKaspa | Status | Why |
| --- | --- | --- | --- |
| `Hashes` | `Hashes` | Implemented | The consensus hash is `Blake2b256`. `SHA256` and `Hash160` exist only for the BIP32 fingerprint and Base58Check. They do not enter addresses or transaction ids. |
| (no same-named type) | `Blake2b` | Implemented | RFC 7693. A script hash is unkeyed BLAKE2b-256. Transaction id, transaction hash, and sighash are keyed BLAKE2b-256 (`TransactionID`, `TransactionHash`, `TransactionSigningHash`). |
| `Hashes.DoubleSHA256` | — | Not applicable | Not a Kaspa consensus hash. |
| `Hashes.Hash160` / RIPEMD160 | `Hashes.Hash160` (`Ripemd160` is internal) | Implemented | BIP32 parent fingerprint only: the first 4 bytes of RIPEMD160(SHA256(33-byte compressed key)). Addresses do not use HASH160. |
| `SchnorrSignature` / `ECDSASignature` | same names | Implemented | Schnorr is the 64-byte BIP-340 encoding. ECDSA is 64-byte compact R\|\|S, not DER. The sighash byte lives in the signature script. |
| `DeterministicECDSA` | — | Implemented (no separate type) | ECDSA uses RFC 6979 from `NBitcoin.Secp256k1`, normalized to low S. |

## Secp256k1

NBitcoin's `Secp256k1/` directory contains `ECPrivKey`, `ECPubKey`, `ECXOnlyPubKey`, `SecpSchnorrSignature`, `SecpECDSASignature`, plus MuSig, adaptor signatures, and recovery.

| Status | Why |
| --- | --- |
| The sources are not copied into NKaspa | The curve is the NuGet package `NBitcoin.Secp256k1` (MIT, this release references 3.1.6). `Key`, `PubKey`, and `PubKeyECDSA` wrap it. |
| MuSig / adaptor / recovery | Out of plan unless a Kaspa wallet needs them. |

## BIP32

`ExtKey`, `ExtPubKey`, `KeyPath`, `HDFingerprint`. There is no separate `RootedKeyPath`, `IHDKey`, or `BitcoinExtKey`. Export a private extended key with `ExportXprv()`, `ExportXprv(Network)`, `GetWif(Network)`, `ToString(Network)`, or `ToBase58`. Parameterless `ExtKey.ToString()` returns the type name `NKaspa.ExtKey` and does not write `kprv` into a log, matching NBitcoin. `ExportXprv()` and `ToBase58()` are the same text. `ExportXprv(Network)`, `GetWif`, and `ToString(Network)` are the same text. NBitcoin's `GetWif` returns a `BitcoinExtKey`.

| Status | Why |
| --- | --- |
| Implemented | The master key is HMAC-SHA512 with the BIP32 key string `Bitcoin seed`. Children use hardened and non-hardened derivation. The fingerprint is the first 4 bytes of HASH160. |

Kaspa serialization prefixes follow rusty-kaspa `wallet/bip32` `Prefix` (`HdKeyVersions`):

| Network | Private | Public |
| --- | --- | --- |
| Mainnet | `kprv` `0x038f2ef4` | `kpub` `0x038f332e` |
| Testnet, Simnet, Devnet | `ktrv` `0x03909e07` | `ktub` `0x0390a241` |

kaswallet's C# `NetworkKind` once used other version bytes for devnet and simnet (devnet `0x038b3d80` / `0x038b41ba`, simnet `0x03904242` / `0x0390467d`). rusty-kaspa does not have those pairs. This library does not use them.

The default derivation path is `m/44'/111111'/0'/{0,1}/i`. The coin type is `111111`. `KeyPath.KaspaReceive` is external chain `0`. `KeyPath.KaspaChange` is change chain `1`. The old `m/44'/972` path is not the default.

Parsing also accepts Bitcoin `xprv`/`xpub`, `yprv`/`ypub`, `zprv`/`zpub`, and `tprv`/`tpub`, so the official BIP32 vectors can be checked. `CreateFromSeed` stores `kprv`.

## BIP39

`Mnemonic`, `Wordlist`, `Language`, `WordCount`. There is no `KDTable`. Normalization is runtime NFKD.

| Status | Why |
| --- | --- |
| Implemented | The English wordlist is 2048 words, embedded. The seed is PBKDF2-HMAC-SHA512, 2048 rounds, 64 bytes. The passphrase salt is `mnemonic` plus the NFKD passphrase. The lenient constructor still allows a bad checksum, with `IsValidChecksum` false. `Parse`, `ParseValidated`, and `requireValidChecksum: true` throw `FormatException` (`BIP39 checksum does not match.`). A bad checksum cannot `DeriveSeed` / `DeriveExtKey` unless `allowInvalidChecksum: true`. `Words` is an `IReadOnlyList<string>`. Parameterless `ToString` returns `NKaspa.Mnemonic`. The sentence is `ToPhrase()`. |

The English wordlist is copied from NBitcoin. The notice is `src/NKaspa/BIP39/WORDLIST-LICENSE.txt` (MIT, Copyright (c) 2023 Metaco SA). The wordlist file itself is only words.

Case-insensitivity and removal of zero-width characters (a leading BOM and marks inside a word) are NKaspa extensions to BIP39, together with NFKD. `ToPhrase()` stores the canonical lowercase wordlist words, so the derived seed matches other wallets given that canonical phrase. A phrase with uppercase or full-width characters typed verbatim into another wallet may fail. The passphrase is NFKD only. It is not stripped of zero-width characters and case is preserved.

## Protocol

`Node`, `Message`, `Payload`, `VersionPayload`, `TxPayload`, `BlockPayload`, `InventoryVector`, `NetworkAddress`, `VarInt`, `VarString`, and the compact-block, feefilter, wtxid-relay, and witness payloads.

| Status | Why |
| --- | --- |
| Not copied as-is | Those are Bitcoin P2P messages. The Kaspa node protocol is different and has no witness or compact-block payload of that kind. |
| Planned (written separately) | A future node client would follow Kaspa P2P. This client's wire protocol is wRPC JSON and Borsh, not a full P2P node. |

Lengths inside a transaction are little-endian u64, written by `Transaction` serialization. Bitcoin `VarInt` is not copied from the `Protocol` directory.

## Policy

`StandardTransactionPolicy`, `MinerTransactionPolicy`, `ITransactionPolicy`.

| Status | Why |
| --- | --- |
| Not copied as-is | Bitcoin standard scripts (P2PKH, P2WPKH, multisig counts) do not match Kaspa. |
| Implemented (mass) | `MassCalculator`: version 0 and version 1 compute mass, KIP-13 transient mass, and KIP-9 storage mass. Fee mass is the maximum of the three after normalization. Block limits are compute 500000, storage 500000, and transient 1000000. The old per-transaction `100_000` remains an optional policy. A coinbase is 0 on all three. Script execution is `TxScriptEngine`. Standardness is the existing templates plus mass. There is no separate `StandardTransactionPolicy`. `FeeEstimator` needs no private key. It computes the same mass and fee as the signed transaction from the input script kind and the output amounts. A version-0 payment with one Schnorr input and two 34-byte outputs has compute mass 2036. The protocol minimum rate constant is 100 sompi/gram. `MinimumFeerate` takes the lowest positive finite `getFeeEstimate` bucket and rounds it up. `PayableFeerate` raises that bucket to 100. |

## RPC

`RPCClient`, `RPCClient.Wallet`, `RestClient`, and response types such as `GetBlockRPCResponse`, `ListUnspentOptions`, and `UnspentCoin`.

| Status | Why |
| --- | --- |
| Implemented (minimal subset) | `IKaspaRpcClient` is implemented by both `KaspaWrpcJsonClient` and `KaspaWrpcBorshClient`. `KaspaRpcClient.Create(url, encoding = Auto)` returns a `KaspaRpcConnection` and does not connect. Auto reads the URL path first, then the port. The path wins. A path ending in `/wrpc/borsh` or containing `/borsh` selects Borsh. A path ending in `/wrpc/json` or containing `/json` selects JSON. Comparison is ordinal and case-insensitive. Trailing slashes are ignored for the ending check. Borsh is tested first, so a path that contains both `/json` and `/borsh` selects Borsh. Ports still follow rusty-kaspa `4edcbbcc` `consensus/core/src/network.rs`: Borsh 17110, 17210, 17510, 17610 and JSON 18110, 18210, 18510, 18610. Simnet is 17510 and 18510, not 17310 or 18310. When the path and the port are both unknown, `Create` throws `ArgumentException` and does not default to either encoding. `wss` on port 443 with no `/json` or `/borsh` in the path is that case. gRPC ports 16110, 16210, 16510, and 16610 also throw `ArgumentException`: NKaspa speaks wRPC, not gRPC. An explicit `KaspaRpcEncoding.Borsh` or `Json` overrides the path and a wRPC port. It does not turn a gRPC port into wRPC. A JSON client that receives a binary frame, or a Borsh client that receives a text frame, throws `KaspaRpcEncodingMismatchException`. `HandshakeFailed` is true when a Borsh client receives a JSON handshake text frame. Pending requests fail with that exception. The client does not wait for the request timeout and does not reconnect. `KaspaWrpcPorts` records the four gRPC ports and `TrySelectPath`. Methods are `getServerInfo`, `getBlockDagInfo`, `getUtxosByAddresses`, `getBalanceByAddress`, `getBalancesByAddresses`, `submitTransaction`, `getFeeEstimate`, `getBlock`, plus `NotifyUtxosChanged`, `NotifyVirtualDaaScoreChanged`, and `NotifyBlockAdded`. The JSON envelope is workflow-rpc: `id`, `method`, `params`. The success body is in `params`. Readers also accept `result`. An error is `error.code` / `error.message`. It is not JSON-RPC 2.0. Borsh is the same workflow-rpc as a binary frame: optional u64 id, one-byte `RpcApiOps`, then a versioned serializer. Subscriptions on the wire are `Subscribe` (op 3) and `Unsubscribe` (op 4) with a `Scope`. The node does not register the older `Notify*` opcodes. |
| Unit tests do not dial a node | Tests use an in-memory JSON channel and a loopback WebSocket, including a fake socket that delivers the wrong frame type. On 2026-10-30 `samples/NodeQuery` did one read-only query of `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json`: `getServerInfo` returned rusty-kaspa 2.1.0, mainnet, synced, with a UTXO index. `getBlockDagInfo`, `getBalanceByAddress`, and `getUtxosByAddresses` also answered. `submitTransaction` was not called. |
| Inner fee-estimate fields | `priorityBucket`, `normalBuckets`, `lowBuckets`, and each bucket's `feerate` / `estimatedSeconds` (two little-endian f64 values). Confirmed against rusty-kaspa `4edcbbcc` `rpc/core/src/model/feerate_estimate.rs`. `priorityBucket` is required. The response is a serializer version u16 around `serialize!(RpcFeeEstimate)`. The JSON shape still matches kaswallet `WalletRestClient` and the Kaspa WASM `IFeeEstimate`. An earlier 404 on `feerate.rs` was the wrong path, not an unsettled field. |
| PSBT RPC parameters | Not applicable. See BIP174. PSKT is exchanged inside the library and is not submitted through those RPC parameters. |

## Payment

`BitcoinUrlBuilder` and the internal URI helpers.

| Status | Why |
| --- | --- |
| Planned | A `kaspa:` payment URI can come later. Bitcoin BIP21 query parameters cannot be reused as-is. |

## BIP174 / BIP370

`PartiallySignedTransaction`, `PSBTInput`, `PSBTOutput`, `PSBTCoin`, and BIP370 PSBT v2.

| Status | Why |
| --- | --- |
| Not applicable (BIP174) | Kaspa has no native PSBT. `PartiallySignedTransaction` is not ported. |
| Implemented (PSKT) | `NKaspa.PSKT` follows rusty-kaspa `wallet/pskt`. Types are `PSKT`, `PSKTInput`, `PSKTOutput`, and `PSKTBundle`. Roles are Creator, Constructor, Updater, Signer, Combiner, Finalizer, and Extractor. The role itself is not in the JSON. The wire form is compact JSON, then lowercase hex with the prefix `PSKT`. A bundle's JSON is an array with the prefix `PSKB`. The Schnorr partial-signature JSON key is `schnorr` (64 bytes). The ECDSA key is `eCDSA` (DER). The script still uses the 64-byte compact form. `Finalize` covers single-sig Schnorr P2PK, ECDSA P2PK, and m-of-n (1 to 16) on a P2SH redeem script. `TransactionBuilder.BuildPSKT` and `Sign` cover version 0 and version 1. Nothing is broadcast. |

Empty PSKT JSON is pinned in serde field order. rusty-kaspa's PSKT tests and the multisig example use random keys and print the result. They do not pin a hex vector. This library has not started a rusty-kaspa process for a byte comparison. An xpub is written with the Bitcoin `xpub` prefix (`ExtendedPublicKey::to_string(None)`), not Kaspa `kpub`. A map of several public keys is ordered by uncompressed X\|\|Y. Several xpubs are ordered by the Base58 string, which may not match Rust `Ord`. There is no upstream multi-xpub vector. A version-1 compute budget stays in memory and is not written to JSON. Rusty's `unsigned_tx` still has a TODO for the compute budget, and the version-1 sighash does not write that budget either. `ExtractTransaction` writes KIP-9 storage mass. That step does not execute scripts and does not take the maximum of storage, compute, and transient. The extracted transaction is checked with `Transaction.Verify`.

## BIP158

`GolombRiceFilter`, `BitStream`.

| Status | Why |
| --- | --- |
| Not applicable | Kaspa does not use Bitcoin's BIP158 Golomb-Rice compact filters. |

## OpenAsset

`ColoredTransaction`, `ColorMarker`, `AssetId`, `BitcoinColoredAddress`, and the rest of that directory.

| Status | Why |
| --- | --- |
| Not applicable | Open Assets colored coins are a Bitcoin protocol. They do not apply to Kaspa. |

## Other NBitcoin directories

| Directory | Main types | Status | Why |
| --- | --- | --- | --- |
| `BIP38` | `BitcoinEncryptedSecret` | Not applicable | Bitcoin Base58 encrypted secrets. Kaspa does not use that format. |
| `BIP322` | `BIP322Signature` | Not applicable | A Bitcoin message-signing proof bound to a Bitcoin address and script. |
| `BIP9Deployments` | deployment state | Not applicable | Bitcoin soft-fork version bits. Kaspa upgrades do not use BIP9. |
| `JsonConverters` | `NKaspa.JsonConverters` | Implemented | `NKaspaJsonSerializer` is the counterpart of `Serializer`. It uses System.Text.Json, not Newtonsoft. See JsonConverters below. |
| `WalletPolicies` / miniscript | policy text | Not applicable | Bitcoin miniscript and output descriptors do not match current Kaspa script. |
| `BuilderExtensions` | `TransactionBuilder` | Implemented | Building lives on `TransactionBuilder`. There is no separate extension-method class. Forced spends are `MustSpend` and `SpendAllCoins`, not a `SendAll` that sweeps an amount into one output. Unsigned exchange is `BuildPSKT` and `Sign`. |
| `Socks` | `SocksHelper` | Out of plan | Unrelated to consensus. Revisit when connecting to nodes. |
| `Logging` | `Logs` | Not applicable | The library does not bind NBitcoin's logging facade. |
| `BouncyCastle` | digests and big integers | Not applicable | Those sources are not imported. Hashes use the in-tree BLAKE2b. The curve uses the Secp256k1 package. |

## JsonConverters

`NKaspa.JsonConverters` corresponds to `NBitcoin.JsonConverters`. NBitcoin's `Serializer` uses Newtonsoft.Json. `NKaspaJsonSerializer` uses System.Text.Json, because `net8.0` and `netstandard2.1` can both use the referenced System.Text.Json 8.0.5. No Newtonsoft package and no Newtonsoft adapter were added.

`AddNKaspaConverters(network)` adds the converters to `JsonSerializerOptions`. If the naming policy is still null, it becomes camel case. Addresses, extended public keys, and extended private keys after an explicit opt-in are read and written with that network's prefixes.

| Type | JSON |
| --- | --- |
| `Money` | Sompi integer. A number or a numeric string. A decimal such as `1 KAS` is rejected. |
| `uint256` | 64 hex characters in Kaspa wire order. An optional `0x` is accepted. |
| `KaspaAddress` | Address string. The prefix must belong to the network in the options. |
| `Network` | `mainnet` / `testnet` / `simnet` / `devnet`. Also `testnet-10` and `{networkType, suffix}`. |
| `Script` | Hex of the script bytes, the same form as `signatureScript`. |
| `ScriptPubKey` | Written as `{version, script}`. Reading also accepts `scriptPublicKey` on that object, and hex whose first bytes are the version. |
| `OutPoint` | `{transactionId, index}`. Reading also accepts `transactionId:index`. |
| `TxIn` / `TxOut` / `Transaction` | Same as `KaspaRpcJson`. A version-1 transaction writes `computeBudget`. A lone `TxIn` writes it only when a budget is present. An output may carry `covenant`. |
| `UtxoEntry` | `{amount, scriptPublicKey, blockDaaScore, isCoinbase}`. A covenant id writes `covenantId`. |
| `Coin` | `{outpoint, utxoEntry}`. Same shape as a `getUtxosByAddresses` entry. An address field is ignored. |
| `PubKey` / `PubKeyECDSA` | 32-byte x-only, or a 33-byte compressed key, lowercase hex. |
| `ExtPubKey` | That network's `kpub` or `ktub`. |
| `FeeRate` | Integer sompi per gram. |
| `PSKT` | The compact object from `PSKT.ToJson()`. |
| `Key` / `ExtKey` / `Mnemonic` | Refused by default, including the `JsonConverter` on the type, so `JsonSerializer.Serialize` without options also refuses. The exception text carries WARNING and does not contain the secret. `allowPrivateKeys: true` writes the hex secret, `kprv` / `ktrv`, or the `ToPhrase()` sentence. |

Compact transaction JSON is the same string as `KaspaRpcJson.WriteTransaction`. On 2026-10-30 three accepted mainnet transactions (`5fd8f860…`, `2febe6ca…`, `4d1c6a5e…`) were read in wRPC shape and their ids matched the chain. The explorer `mass` is relay mass. It was not written into `storageMass`.

## Where this release stands

0.1.11-alpha adds Borsh wRPC. See "0.1.11 Borsh wRPC" below. API changes from 0.1.10-alpha are under "0.1.10 review fixes".

0.1.9-alpha freezes the sink and its blue score when `ConsensusSeqCommitAccessor` starts a walk. Mainnet is about 10 blocks per second, so `getBlockDagInfo` can show a new sink during the walk. If the new sink's selected-parent chain still contains that snapshot, the chain grew forward and the snapshot result is returned. It is not a `Reorg`. A reorg is only when the snapshot viewpoint, or a block on the walk, leaves the selected chain (`isChainBlock` becomes false, or the new chain does not pass through it). `StableDepth` defaults to 20, about 2 seconds at 10 blocks per second, and steps the viewpoint back along selected parents. Zero means the viewpoint is the locked sink. A real reorg retries 3 times by default. The request count is not reset. When the retries are exhausted it throws `Reorg`. The default request cap is 256, including blocks that appear during the walk. Hitting the cap stops the walk. It does not scan the whole finality window. If the new sink is more than 32 selected parents ahead of the snapshot, or the budget cannot finish the new chain, the snapshot block is read again and the latest `isChainBlock` decides whether it is still on the selected chain. `WrpcSeqCommitAccessor` still uses the DAG sink as its viewpoint. 0.1.8-alpha added the optional `ConsensusSeqCommitAccessor`: read-only `getBlock` and `getBlockDagInfo`, a walk along selected parents, the consensus block and seq commitment, and checks of selected parent, blue score, and `isChainBlock`. Incomplete, inconsistent, or reorged data throws. It is not treated as pruned or too deep. A light client can only approach consensus. Matching it exactly requires a running node. 0.1.7-alpha added the finalized structural checks for `OP_ZKPRECOMPILE`, introspection and covenant builders, example scripts, and the read-only `WrpcSeqCommitAccessor`. Script execution started in 0.1.6. The general JSON converters from 0.1.5 are still here. The 0.1.4 behavior is still here: console samples under `samples/`. Phase one (encoders, BLAKE2b, `uint256`, networks, and the three address types) is still here. Phase two (keys, signatures, script, version-0 transactions, and sighash) is still here. Phase three added BIP39, BIP32, mass and fee rates, the transaction builder, and a minimal wRPC JSON client. 0.1.1 added the Toccata version-1 transaction id, change and mass policy, and wRPC frame dispatch. 0.1.2 made the builder force named inputs, avoid extra small coins when one coin is enough, and emit a version-1 payment directly. 0.1.3 added PSKT: partial signatures, combine, single-sig and P2SH multisig finalize, extract, and `TransactionBuilder.BuildPSKT`.

- Solution: `NKaspa.sln`, library `src/NKaspa` (`net8.0` and `netstandard2.1`), tests `tests/NKaspa.Tests` (xUnit, `net8.0`), samples `samples/` (eight console projects, `net8.0`, including `samples/Tutorials`). See Samples below.
- Keys: `Key`, `PubKey` (32-byte x-only), `PubKeyECDSA` (33-byte compressed). `GetAddress` is the Schnorr address. `Key.Create` clears temporary buffers. `ExtKey.PrivateKey` returns a copy. Disposing the copy does not dispose the parent.
- Signatures: `SchnorrSignature`, `ECDSASignature`, `SigHashType` (`All=0x01`, `None=0x02`, `Single=0x04`, anyone-can-pay `0x80`).
- Script: `OpcodeType`, `Op`, `Script`, `ScriptBuilder`, `ScriptEvaluationContext`, `TxScriptEngine`. `ScriptPubKey` still carries a u16 version. Standard templates are Schnorr P2PK, ECDSA P2PK, and P2SH. A signature script is a 66-byte `OP_DATA_65` push. Version-0 scripts execute, and P2SH runs the redeem script. Implemented: pushes, control flow, stack, bitwise, arithmetic, Schnorr and ECDSA `OP_CHECKSIG` / `OP_CHECKMULTISIG`, `OP_BLAKE2B`, `OP_BLAKE3`, `OP_SHA256`, `OP_CHECKLOCKTIMEVERIFY`, `OP_CHECKSEQUENCEVERIFY`, introspection opcodes `0xb2`–`0xc9`, covenant opcodes `0xcb`–`0xd6`, `OP_NUM2BIN`, `OP_BIN2NUM`, and `OP_CHECKSIGFROMSTACK`. `OP_ZKPRECOMPILE` runs the finalized structural checks. BN254 pairing, curve-point checks, and the RISC0 STARK still return unimplemented and do not push true. `ScriptBuilder` has helpers for `0xb2`–`0xc9` and `0xcb`–`0xd6`. `CovenantScripts` has three examples the engine can execute. They are not broadcast. Without an `ISeqCommitAccessor`, `OP_CHAINBLOCKSEQCOMMIT` is illegal. Numbers are little-endian. The sign bit is the high bit of the last byte. Arithmetic width is at most 8 bytes.
- Transactions: `OutPoint`, `TxIn`, `TxOut`, `Transaction`, `UtxoEntry`, `SubnetworkId`, `Money`, `CovenantBinding`. A version-0 id uses keyed BLAKE2b with key `TransactionID`. The transaction hash key is `TransactionHash`. A version-1 id is BLAKE3 over domains `PayloadDigest`, `TransactionRest`, and `TransactionV1Id`. Length prefixes are u64.
- Sighash: `SigHash.CalcSchnorrSignatureHash` uses the key `TransactionSigningHash`. ECDSA then applies `SHA256(SHA256("TransactionSigningHashECDSA") || schnorrHash)`. Version 1 omits sigops. A covenant writes a presence flag, the authorizing input, and the covenant id. No covenant writes 0.
- Main differences from Bitcoin: `OP_CHECKSIG` verifies Schnorr. `OP_CHECKSIGECDSA` is `0xab`. `OP_BLAKE2B` is `0xaa`. CLTV is `0xb0`. CSV is `0xb1`. `OP_CAT` and similar opcodes are enabled. `OP_LEFT`, `OP_RIGHT`, `OP_2MUL`, `OP_2DIV`, `OP_LSHIFT`, and `OP_RSHIFT` stay disabled.
- Vectors come from rusty-kaspa `hashing/tx.rs`, `hashing/sighash.rs`, and `crypto/hashes/src/hashers.rs`, and from the same tests in kaswallet. BLAKE3 uses the official `test_vectors.json`. BIP-340 signature vector 0 checks Schnorr. Address derivation uses BIP-340 public key 1, the existing `kaspa:qr0lr4ml...` string.
- BIP39: English rows of the Trezor `vectors.json` (BIP39 states the passphrase for those seeds is `TREZOR`), copied as NBitcoin's `bip39_vectors.json`. Another set is NBitcoin's `bip39_vectors.en.json`, whose passphrase contains ideographic spaces, used to check NFKD.
- BIP32: the five official BIP-0032 vectors, including an illegal key. Kaspa prefixes round-trip. No external pinned string of the form "mnemonic to kaspa address" was found. Tests check that a derived address passes `KaspaAddress.IsValid`, and that account-public derivation and private derivation yield the same address.
- Mass: rusty-kaspa `test_storage_mass` ported through kaswallet `MassCalculatorTests`. Three amount samples from api.kaspa.org `/transactions/mass` (83333, 10003, 2017) are not upstream unit tests. The mass size includes a 32-byte payload hash and excludes the sigop byte and the covenant presence byte. An empty transaction is 94, not the 62 bytes of `ToBytes()`. Version 1 adds 2 bytes of compute budget per input. A covenant adds 34 bytes on the output.
- Transaction builder: local Schnorr and ECDSA P2PK end to end. Default `MinimumChange` is 0.02 KAS. Change below that is folded into the fee. A larger change is folded only when the output cannot be built, the amount is at most `max(MinimumChange, MaxFoldedChange)` (`MaxFoldedChange` defaults to 0.1 KAS), and it is within `MaxFoldedFeeFactor` times the no-change fee (default 100). Above that cap the builder throws `InvalidOperationException` and says the change would exceed the mass limit, suggesting a split payment or more inputs. After a successful fold, `ChangeFoldedIntoFee` is the sompi removed from the change output and added to the fee. It is not the total fee. It is 0 when nothing was folded or the build failed. `TransactionMass` reports mass only and does not repeat that number. The default fee-mass ceiling is the block compute limit, 500000. `toccataActive: false` or `MassPolicy.PreToccata` restores the per-transaction cap of 100000 and a relay rate of 1 sompi/gram, and `MinimumChange` is then 0.1 KAS. `MaxFoldedChange` and `MaxFoldedFeeFactor` are public properties. A caller can raise them. They are a wallet safety limit, not consensus. Raising them moves more sompi into the fee. An explicit fee below `MinimumRelayFeeRate` is rejected unless `AllowBelowMinimumRelayFee`. The default minimum relay fee is 100 sompi times the larger of compute mass and normalized transient mass, excluding storage. A standard one-input payment is 2036 grams, 203600 sompi, about 0.002 KAS. `SendEstimatedFees` multiplies the rate by fee mass (the largest of the three normalized terms). `SendMinimumRelayFee` pays only the relay floor. `EstimateFee` and `EstimateMinimumRelayFee` are the matching static estimates. Coin selection deduplicates by outpoint and does not merge by script. When one coin is enough, only the smallest such coin is used. `MustSpend` and `SpendAllCoins` force every named input into the transaction and sign each input. `SetVersion(1)` uses version-1 sighash. The default compute budget is 10 per input (1000 compute grams, the same order as one version-0 signature). Version-1 wRPC JSON writes both `sigOpCount` and `computeBudget`.
- RPC: in-memory JSON samples plus a loopback WebSocket. Large integers use integer tokens, not `double` (sample `9007199254740993`). `getBlockDagInfo` `network` accepts a string or `{networkType, suffix}`. `samples/NodeQuery` did one read-only query of a public mainnet endpoint. See above. 0.1.11 adds hand-computed Borsh frames, a fake binary WebSocket round trip, a subscription notification test, and encoding-mismatch tests. Those tests do not dial a real node.

## Toccata compatibility

The comparison is rusty-kaspa `master` as read on 2026-10-30, not a live node.

The mainnet activation DAA score 474165565 is rusty-kaspa commit `90dbf074275d60c1fe74a3491883196f110970c0`, `consensus/core/src/config/params.rs` line 724: `toccata_activation: ForkActivation::new(474_165_565)`. Testnet 467579632 is line 785 of the same file: `toccata_activation: ForkActivation::new(467_579_632)`. Current master deleted that field. `constants.rs` no longer branches on the score. In `params.rs`, mainnet, testnet, simnet, and devnet `block_mass_limits` are written as the same triple.

Post-fork block limits are compute 500000, storage 500000, and transient 1000000. `TRANSIENT_BYTE_TO_MASS_FACTOR` is still 4. The per-transaction standard mass cap was removed in PR 1086 (commit `02dcf44`). Unmerged PR 1141 would change transient to 1 per byte and a limit of 250000. This library does not take unmerged changes.

Version-1 id: `id = BLAKE3("TransactionV1Id", payload_digest || rest_digest)`. An empty payload uses `ZERO_PAYLOAD_DIGEST`. The rest excludes payload, signature scripts, and mass. The transaction hash is still BLAKE2b `TransactionHash`. Version 1 always writes storage mass, and writes each input's compute budget when mass is not excluded. The sigop-count variant writes budget 0 in the version-1 hash.

Version-1 mass: 100 grams per compute-budget unit. The estimated size adds 2 per version-1 input. A covenant adds 2 bytes for the authorizing input and 32 bytes for the id, and does not add the 1-byte presence flag. UTXO plurality adds 32 bytes when a covenant id is present.

The minimum relay fee follows the upgrade note: `100 * max(compute, normalized transient)`. Under the default limits, normalized transient equals `2 * estimated bytes`. Storage mass is not part of that floor.

`KaspaRpcHandler::handshake` in `rpc/wrpc/server/src/service.rs` is still a TODO. Current nodes do not send a handshake greeting, and this client does not send one. The channel recognizes a text frame whose method is `handshake`, or which carries a `handshake` field and has no request id. Other id-less frames are notifications. Responses match on the request id. A binary frame on a JSON client, or a text frame on a Borsh client, throws `KaspaRpcEncodingMismatchException` immediately.

The script VM follows rusty-kaspa `master` (`4edcbbccb449f84832afb1ca157904b35f37d006`) `crypto/txscript`. 955 rows of `script_tests.json` pass, 0 are skipped for an unimplemented opcode, and 23 commented rows are not counted. The 50 table-driven tests in `opcodes.rs` and the `scriptPubKey` string round trip are ported. `Script.ToRustString` uses upstream opcode names and `0x` hex. `ToString` keeps the earlier spelling.

`OP_ZKPRECOMPILE` tags, costs, and layout follow the finalized upstream rules. Groth16 tag `0x20` costs 14000000 script units. RISC0 tag `0x21` costs 25000000. An unknown tag is rejected with `ZkIntegrity` before the charge. After the arities match, each `gamma_abc_g1` element costs another 250000. BN254 pairing, on-curve and subgroup checks, and the RISC0 succinct STARK still return unimplemented and do not push true. Upstream rejects a point that is not on the curve before gamma billing. This library cannot do that check, so it bills first and then fails closed as unimplemented. A proof whose length is not 128 bytes is rejected as short or as trailing bytes. That is a length check, not a point decode.

`ISeqCommitAccessor` is the KIP-21 window: `X.blue_score + F > P.blue_score`. At 10 blocks per second, `F` is 432000. An unknown ancestor returns `BlockAlreadyPruned`. A block that is not on the chain returns `BlockNotSelected`. Past the window returns `BlockIsTooDeep`. Compared with rusty-kaspa master `seq_commit_within_threshold` and `Header::post_toccata_chainblock_selected_parent`: viewpoint `P` is the selected parent of the chain block being validated, and a chain block's first direct parent is the selected parent. After the Toccata cleanup, master no longer switches this accessor by DAA score. `WrpcSeqCommitAccessor` uses `getBlock` and `getBlockDagInfo`. Its viewpoint is the DAG sink's blue score, not the selected parent of the block being validated, so it is not a consensus implementation. It does not call `submitTransaction` and it does not open a socket itself. `ConsensusSeqCommitAccessor` uses the same two read-only methods and walks selected parents. The default viewpoint is the sink, which is the virtual selected parent of a block template. When `ChainBlock` is set, the viewpoint becomes that chain block's selected parent. The window uses that `P`'s blue score. It checks that `selectedParentHash` matches the first direct parent, that header and verbose blue scores agree, that `isChainBlock` agrees with the walk, and that blue score strictly decreases along the chain. A missing block, missing parent, or missing field throws `IncompleteNodeData`. It is not treated as pruned. At the start of the call the sink and its blue score are frozen as a snapshot. The walk and the window use that snapshot. A later sink whose selected-parent chain still contains the snapshot is forward growth, and the snapshot result is returned. A reorg is only when the snapshot viewpoint, or a block on the walk, leaves the selected chain. `StableDepth` defaults to 20 and can step the viewpoint back from the locked sink along selected parents. Zero uses that sink. If the sink's `isChainBlock` is already false at the start, a newer sink is read and then the snapshot is frozen. A real reorg retries 3 times by default and then throws `Reorg`. A caller-specified chain block that is not on the selected chain also throws `Reorg`, and that call returns no result. rusty-kaspa `is_chain_block(hash)` is `is_chain_ancestor_of(hash, sink)`, so the sink itself is always a chain block. If a walked block has `isChainBlock` false, `getBlockDagInfo` is read again: an unchanged sink throws `InconsistentData`. A changed sink, with the block no longer on the new selected chain, is a reorg. When the new chain is more than 32 steps ahead, or the request budget cannot finish it, the snapshot's `isChainBlock` is read again. The tip is not chased block by block. Hitting the request cap throws `RequestLimit`. A cache hit does not issue a request. A light client can only approach consensus: a full finality window is hundreds of thousands of selected-parent steps, and pruned reachability exists only inside the node. Matching it exactly requires a running node.

### Review items

| Item | Finding | What changed |
| --- | --- | --- |
| 1. `MinimumChange` defaulted to 0, and a small change blew up storage mass | True | Default threshold `C / storage limit` (0.02 KAS). Change that would exceed the limit is folded into the fee. A test that pays 0.99 KAS from 1 KAS no longer throws. A tiny payment output is still rejected. |
| 2. The per-transaction standard cap was still 100000 | True in this library. rusty-kaspa master removed the per-transaction cap. Block limits are 500000 / 500000 / 1000000 | The builder defaults to the block limits. `toccataActive: false` or `MaximumMass = MaxStandardTransactionMass` keeps 100000. A transaction whose storage mass is between 100001 and 500000 can be built. |
| 3. Version-1 transactions | True, and previously missing | BLAKE3, `TxIn.ComputeBudget`, `TxOut.Covenant`, `FromBytes` / `ToBytes`, sighash writes the covenant, version-1 mass. Checked against `tx.rs` tests 10–14. Since 0.1.6 the script engine meters by compute budget and executes covenant opcodes whose rules are fixed. |
| 4. The WebSocket did not match by id, so a handshake or a notification could block a response | Partly true | The channel matches by id and stores notifications separately. The node handshake is still empty in rusty-kaspa. This library only recognizes that frame. It does not invent a node greeting. Tests use a local `HttpListener`. A mismatched frame type fails pending calls with `KaspaRpcEncodingMismatchException`. |
| 5. `getBlockDagInfo` network accepted only a string | True | Both a string and `{networkType, suffix}` are accepted. |
| 6. `SendFees` did not check 100 sompi/gram | Partly true | `SendEstimatedFees` already rejected a rate below 100. `SendFees` now checks an explicit fee against compute and normalized transient. `AllowBelowMinimumRelayFee` can relax that. |
| 7. A repeated outpoint became a double-spend input | True | `AddCoins` keeps the first coin for an outpoint. |
| 8. `Key.Create` did not clear a temporary buffer, and `ExtKey.PrivateKey` returned the inner object | True | `Create` / `TakeSecret` zero the caller's buffer. `PrivateKey` returns a copy. |

## 0.1.10 review fixes

A read-only review raised seven items. 1, 3, 4, and 5 changed code. 2, 6, and 7 were assessments only.

Parameterless `ExtKey.ToString` used to call `ToBase58()`, so a log could contain `kprv`. NBitcoin's `ExtKey` does not override parameterless `ToString`, so the output is only the type name. Since 0.1.10, parameterless `ToString` returns `NKaspa.ExtKey` and does not read key material. Export with `ExportXprv()`, `ExportXprv(Network)`, `GetWif(Network)`, `ToString(Network)`, or `ToBase58()`. `ExportXprv()` matches `ToBase58()`. `ExportXprv(Network)` matches `ToString(Network)` and `GetWif`. `ExtPubKey.ToString()` is still the extended public key. The private-key JSON converter writes `ExportXprv(Network)`. Samples do not print parameterless `ToString`.

BIP39 construction strips zero-width characters (U+200B, U+200C, U+200D, U+FEFF, U+2060, U+180E, U+200E, U+200F), applies NFKD to the whole sentence, splits on Unicode whitespace (including ideographic space U+3000, NBSP U+00A0, and repeated spaces), then NFKD and lowercase, and looks the words up in the English list. The stored words are the canonical wordlist entries. The same canonical words produce the same seed. A variant does not silently become another wallet. A zero-width character is not a separator. Replacing spaces with U+200B fails. `ToPhrase()` is the canonical sentence. Parameterless `ToString()` returns `NKaspa.Mnemonic`. `DeriveSeed` applies NFKD to `ToPhrase()` and to the passphrase before PBKDF2. The passphrase keeps its case and its zero-width characters.

`KaspaWebSocketOptions`: connect timeout defaults to 15 seconds, request timeout to 30 seconds. `Timeout.InfiniteTimeSpan` disables that timeout. The timeout is linked with the caller's `CancellationToken`. Caller cancellation is still `OperationCanceledException`. Expiry is `TimeoutException`. One `ExchangeAsync` has one timeout window, covering send and wait. `AutoReconnect` defaults to false. After a connection that had succeeded drops, in-flight requests fail with `InvalidOperationException` (the text contains `WebSocket disconnected.`) and are not resent. Reconnect uses a new `ClientWebSocket`. Delay starts at `ReconnectInitialDelay` (default 200 milliseconds), grows by `ReconnectBackoffFactor` (default 2), caps at `ReconnectMaxDelay` (default 10 seconds), and stops after `MaxReconnectAttempts` (default 5). The `IKaspaJsonChannel` signature is unchanged.

Mass: current rusty-kaspa master writes the same block limits for mainnet, testnet (suffix 10), simnet, and devnet in `params.rs`: compute 500000, storage 500000, transient 1000000. The review claim that testnet and devnet would reject a transaction built to the Toccata limits does not hold for those four public parameter sets. This library previously had only the mainnet activation score, and the builder used the post-Toccata per-transaction cap and rate for every network. 0.1.10 added `MassPolicy`. `Network.MainnetToccataActivationDaaScore` is 474165565, from rusty-kaspa `90dbf074` `consensus/core/src/config/params.rs` line 724. `TestnetToccataActivationDaaScore` is 467579632, line 785 of the same file (tn10-toc2 line 783 and tn10-toc3 line 784 are secondary sources; master deleted the field, and `WithToccataActivationDaaScore` can replace it). Simnet and Devnet activation scores are 0, so any DAA score counts as active, matching current master. An old devnet that has not upgraded must pass `MassPolicy.PreToccata`, `toccataActive: false`, or a DAA below the recorded score. Both policies use the same block-limit triple. There is no invented pre-Toccata triple. After activation the per-transaction cap is 500000, the rate is 100 sompi/gram, and `MinimumChange` is 0.02 KAS. Before that the cap is 100000, the rate is 1 sompi/gram (`90dbf074` `mining/src/mempool/config.rs` line 28, `LEGACY_MINIMUM_RELAY_TRANSACTION_FEE = 1_000` sompi/kg), and `MinimumChange` is `C / 100000` (0.1 KAS). Forcing the pre-Toccata policy yields 1 sompi/gram, which a current node may reject. Live networks use the default policy. `TransactionBuilder` gained `(Network, ulong virtualDaaScore)` and `(Network, MassPolicy)`. `FeeRate.MinimumRelay` and `MassCalculator.MinimumFeeRate` stay 100.

Assessed, with no matching behavior change:

- The mnemonic still stores wordlist strings. Canonical words are references into the static list. The secret is the order. A .NET string cannot be wiped. Public `Words` is a read-only view, so callers cannot replace elements. `GetWords()` returns a copy. NBitcoin also stores the sentence. There is no API that pretends a string can be erased.
- Change below 0.02 KAS is folded into the fee. That is KIP-9: `C / storage limit`, one output whose plurality is 1, whose storage mass equals the 500000 block storage limit. rusty-kaspa wallet `MassCalculator::is_dust` is a smaller heuristic, about 44400 sompi, and its comment says the mempool does not reject on that threshold. This library's floor follows KIP-9 and is stricter. Larger change is no longer folded unconditionally. See the next section.
- The client already had `getServerInfo`, `getBlockDagInfo`, `getUtxosByAddresses`, `getBalanceByAddress`, `submitTransaction`, `getFeeEstimate`, and `getBlock`. The channel already kept raw notification JSON. A Kaspa node has no queryable historical transaction index, and blocks are pruned. Typed subscriptions can sit on the existing socket: `notifyUtxosChanged`, `notifyBlockAdded`, `notifyVirtualChainChanged`, the matching stop methods, and events. Durable history with reorg handling is a higher layer. Typed subscriptions were estimated at one or two days. History storage was estimated at one or two weeks. That version did not implement them. 0.1.11 added the typed subscriptions named in the RPC section.

## 0.1.11 Borsh wRPC

`KaspaWrpcBorshClient` and the existing `KaspaWrpcJsonClient` both implement `IKaspaRpcClient`. With `KaspaRpcEncoding.Auto`, `KaspaRpcClient.Create` selects the encoding from the path and then from the port. The path wins. An unknown path and port throw. There is no silent default. gRPC ports 16110, 16210, 16510, and 16610 are rejected. Explicit `Borsh` or `Json` overrides the path and a wRPC port, and does not override a gRPC port. A frame of the wrong type throws `KaspaRpcEncodingMismatchException`. The Borsh socket uses the same timeouts, reconnect, 16 MiB cap, `State`, `StateChanged`, and `Reconnected` behavior as the JSON socket. The first successful `ConnectAsync` does not raise `Reconnected`.

The wire format follows rusty-kaspa `4edcbbccb449f84832afb1ca157904b35f37d006` and workflow-rs `7e2958098bbe0647adcfcc0dfc174e438a60699e` (workflow-rpc / workflow-serializer 0.19.0). A request is a `BorshReqHeader` (optional u64 id, one-byte `RpcApiOps`) plus a serializer body with a u32 length. A success payload is a Borsh `Result` around that serializer byte string. In borsh 1.8.1, `Ok` is `1u8` and `Err` is `0u8`. The protocol-level kind is still Success. A notification payload is `Serializable<Notification>`, not a `Result`. The node does not send a handshake greeting (`KaspaRpcHandler::handshake` is still a TODO), and this library does not send one. A JSON client that receives a binary frame, or a Borsh client that receives a text frame, throws `KaspaRpcEncodingMismatchException` at once. In-flight requests fail with it. The client does not wait for the request timeout and does not reconnect. `HandshakeFailed` is true when a Borsh client receives a JSON handshake text frame.

Borsh `NetworkType` order is Mainnet, Testnet, Devnet, Simnet. Address-prefix order is Mainnet, Testnet, Simnet, Devnet. The two numberings differ.

Public subscription methods are `NotifyUtxosChangedAsync`, `NotifyVirtualDaaScoreChangedAsync`, and `NotifyBlockAddedAsync`. Wire opcodes are `Subscribe` = 3 and `Unsubscribe` = 4. The payload is `Serializable<Scope>`. The node router does not register the `NotifyUtxosChanged` (12) group. An empty address list means every address. JSON `subscribe` parameters use a serde external tag, for example `{"UtxosChanged":{"addresses":[...]}}`. Disposing a `KaspaSubscription` stops only that scope.

`SubmitTransaction` is sent only in the fake-server tests. This section did not submit a transaction to a real network.

## 0.1.11 blue score, coinbase maturity, and keystore

The version stays 0.1.11-alpha.

Blue score. rusty-kaspa `GetBlockDagInfoResponse` (`rpc/core/src/model/message.rs`) has no `blueScore`. Borsh field order is `network`, `blockCount`, `headerCount`, `tipHashes`, `difficulty`, `pastMedianTime`, `virtualParentHashes`, `pruningPointHash`, `virtualDaaScore`, `sink`. This library does not read an extra u64 at the end of that Borsh record. JSON may add `blueScore`. When the field is absent, `RpcBlockDagInfo.BlueScore` is 0. `GetBlockDagInfoAsync` does not call `getSinkBlueScore`. The sink blue score is `GetSinkBlueScoreAsync` (JSON method `getSinkBlueScore`, Borsh op 138). The response is serializer version 1, then one u64 `blueScore`. That is the blue score of the virtual selected parent (the sink). The old name was `GetVirtualSelectedParentBlueScore`. `GetVirtualChainFromBlockAsync` (op 128) sends serializer version 2: `startHash`, `includeAcceptedTransactionIds`, and optional `minConfirmationCount`. The response is version 1: `removedChainBlockHashes`, `addedChainBlockHashes`, `acceptedTransactionIds`. That response has no blue score. `GetVirtualChainFromBlockV2` (op 151), which returns headers, is not read. Block headers already carry `blueScore`. `blueWork` is `BlueWorkType` (`Uint192`) in `header.rs`: 24 little-endian bytes. JSON hex may drop leading zeros. An odd length is padded by one nibble on the left. `RpcChainBlock.BlueWork` and `RpcBlockAdded.BlueWork` store those 24 bytes when the field is present.

Coinbase maturity. `ConsensusParams` is the maturity in DAA score for each network. The sources are rusty-kaspa `consensus/core/src/config/params.rs`, `Bps::coinbase_maturity` in `bps.rs` (`BPS * COINBASE_MATURITY_SECONDS`), and the 100 seconds in `constants.rs`. Mainnet Crescendo activation is DAA 110165000. Before that, at 1 block per second, maturity is 100. After it, maturity is 1000. Testnet suffix 10 (`Network.Testnet`) activates at 88657000 with the same rule. Simnet and devnet use `ForkActivation::always()` and a 10 BPS target before Crescendo, so maturity is 1000 at every score. Current master has no `TESTNET11_PARAMS`. Historical testnet-11 was the 10 BPS network. `kaspawallet` commit `25c2dd86709b5a0b889785e7e6b0c00c8827a8b7` used maturity 1000 when the network name was `kaspa-testnet-11`. `ConsensusParams.Testnet11` records that. `ForNetwork` does not map the name `testnet` to it. `IsCoinbaseMature(utxoEntry, virtualDaaScore, network)` and `SpendableAtDaaScore` follow this schedule. A non-coinbase entry is mature. A coinbase entry is mature when `virtualDaaScore - blockDaaScore` reaches the maturity at that viewpoint. Equality is mature. A viewpoint below the block score is immature. The subtraction does not wrap. A live `TransactionValidator` stores one `params.coinbase_maturity()`, the post-blockrate 1000. This helper still applies the fork schedule so the pre-activation boundary can be tested.

On 2026-11-01 a read-only testnet-10 check was made. No transaction was submitted. REST `https://api-tn10.kaspa.org/info/blockdag` returned `networkName` `kaspa-testnet-10`, `virtualDaaScore` `611687351`, and sink `12e70b94db857ebfa30d9bdfb5872c9e78299f698b703ba79fc4a5ab009f3004`. The body had no `blueScore`. The block REST for that sink had header and verboseData `blueScore` `600053776` and unpadded `blueWork` `7cdd01645b922`. This library then connected, read-only, to public `wss://electron-10.kaspa.stream/kaspa/testnet-10/wrpc/json`. `getServerInfo` was rusty-kaspa 2.1.0 on `testnet-10`. `getBlockDagInfo` reported `virtualDaaScore` `611688197` and `BlueScore` 0. `getSinkBlueScore` was `600054623`, the same value as that sink's header and verbose `blueScore`. `blueWork` parsed to 24 little-endian bytes. `getVirtualChainFromBlock` from that sink returned 0 removed hashes and 4 added hashes. `submitTransaction` was not called.

Keystore. `WalletKeystore` encrypts a mnemonic or a seed with a password. The KDF is PBKDF2-HMAC-SHA256. The .NET base class library has no Argon2, so the implementation uses `Rfc2898DeriveBytes.Pbkdf2` (the same algorithm through the instance method on .NET Standard 2.1). The default is 600000 iterations, and the count is stored in the file. The cipher is AES-256-GCM (`System.Security.Cryptography.AesGcm`). Salt and nonce are random unless the caller passes values of the required length. JSON field order is `version`, `kdf`, `kdfParams`, `cipher`, `salt`, `nonce`, `tag`, `ciphertext`. Version is 1, `kdf` is `pbkdf2-sha256`, and `cipher` is `aes-256-gcm`. Associated data binds the version, KDF, iteration count, hash, key length, and cipher. A wrong password, or a changed ciphertext, tag, nonce, or iteration count, throws `WrongKeystorePasswordException` with the message exactly `Wrong keystore password.` A mismatched version, KDF, hash, key length, or cipher throws `KeystoreException` before decryption. `KeystoreSecret.ToString` and `WalletKeystoreParameters.ToString` do not print the secret. Buffers this type still owns are cleared with `CryptographicOperations.ZeroMemory`. A .NET string cannot be cleared.

## 0.1.11 fee estimate without keys

`FeeEstimator.Estimate` builds an unsigned transaction with a placeholder signature script and passes it to `MassCalculator`. The Schnorr and ECDSA placeholder is 66 bytes (`OP_DATA_65 || 64 zero bytes || sighash`). Version 0 charges 1 sigop. Version 1 uses `TransactionBuilder.DefaultComputeBudget` (10) when the caller does not pass a budget. No private key is read. A 1-input 2-output Schnorr pay-to-pubkey payment has the same compute, transient, and storage mass as the signed transaction. Compute mass is 2036. At 100 sompi/gram the relay fee is 203600 sompi. An input of 100000000 that pays 20000000 has storage mass 52531, so `Quote.Fee` is 5253100 while the relay fee stays 203600. An input of 5675155239 with outputs 500000000 and 5171555239 has storage mass 2017, below compute mass, so `Quote.Fee` and the relay fee are both 203600. Two inputs are 3154. Version 1 with budget 10 is 2038. `Quote.RelayFee` multiplies relay mass, which excludes storage. `Quote.Fee` multiplies `FeeMass`, so a larger storage mass costs more than the relay fee. The KIP-9 sample with input 5675155239 and outputs 12000000 and 5654655239 has storage mass 83333, a fee of 8333300 sompi, and a relay fee that stays 203600.

`DefaultMinimumFeerateSompiPerGram` is 100, the same value as `MassCalculator.MinimumFeeRate`. `MinimumFeerate(RpcFeeEstimate)` takes the lowest positive finite `feerate` among `priorityBucket`, `normalBuckets`, and `lowBuckets`, then rounds up with `Math.Ceiling`. NaN, infinity, and values that are not positive are skipped. When no bucket remains, the result is 100. A node may advertise a bucket below 100. This method returns that bucket after rounding up. `PayableFeerate` raises a lower bucket to the caller's protocol floor, which defaults to 100. `Quote(RpcFeeEstimate)` uses `PayableFeerate`. `ReadMinimumFeerateAsync` calls `getFeeEstimate` and returns `MinimumFeerate` without that raise. An explicit rate below the floor throws `ArgumentOutOfRangeException`. A floor of 0 throws `ArgumentOutOfRangeException` with the message `The protocol minimum feerate must be non-zero.` DAA-aware rates stay on `MassPolicy`. The default in this section is the post-Toccata 100.

## 0.1.11 secret text

`KeystoreSecret` remains the holder for a mnemonic or a seed. There is no second redacting type. `ToString` is `NKaspa.KeystoreSecret`. `Reveal` returns the UTF-8 sentence when `IsPhrase` is true, and lowercase hex for seed bytes. `WalletKeystore.DecryptSecret` returns seed bytes, so `Reveal` is hex. The sentence is `DecryptToString`. `Clear` is the same as `Dispose` and uses `CryptographicOperations.ZeroMemory`. A returned .NET string cannot be cleared.

System.Text.Json refuses reads and writes through `KeystoreSecretJsonConverter`. The message is exactly `Refusing to convert KeystoreSecret.` `allowPrivateKeys: true` does not unlock this type. `Key`, `ExtKey`, and `Mnemonic` keep the old rule and write a secret only when that flag is set. Newtonsoft.Json is not a package reference, and this library does not add it. Newtonsoft's default contract resolver calls `ShouldSerializeIsPhrase` before writing `IsPhrase`. That method throws the same refusal.

`Key.ToString` is `NKaspa.Key`. `Key.Reveal` copies the 32 bytes into a `KeystoreSecret`. `Key.Clear` zeros that key. `Mnemonic.Reveal` copies the canonical sentence. The wordlist strings are shared and cannot be wiped. `ExtKey.Reveal` copies `ExportXprv()`. `ExtKey.Clear` zeros the extended key and the chain code.

## 0.1.11 typed wRPC errors

The common base is `KaspaWrpcException`. It does not derive from `KaspaRpcException`. `KaspaRpcEncodingMismatchException` stays outside this family. The JSON client, the Borsh client, and `KaspaRpcConnection` share one mapping.

`KaspaNodeUnreachableException` means the socket could not be opened or it dropped. The message is `Kaspa node is unreachable.` The inner exception is the original `TimeoutException`, `WebSocketException`, `IOException`, or an `InvalidOperationException` whose text contains `disconnected`. `KaspaWebSocketChannel` still throws `TimeoutException` itself. Caller cancellation, disposal, `WebSocket is not connected`, and an encoding mismatch are not wrapped. Borsh server error code 0 is exactly `connection is closed` (workflow-rs `ServerError`). That text is kept, and the inner exception is `KaspaRpcException`. `RPC call timed out` means the node was reached and stays `KaspaRpcException`.

`KaspaUtxoIndexNotEnabledException` is rusty-kaspa `RpcError::NoUtxoIndex` in `rpc/core/src/error.rs`. The display text is `Method unavailable. Run the node with the --utxoindex argument.` A message is mapped when it contains `--utxoindex`, ignoring case, and the original text is kept. `KaspaNodeNotSyncedException` is `RpcError::ConsensusInTransitionalIbdState` in the same file. The sentence must be exactly `consensus is currently in a transitional ibd state`, ignoring case. Extra words stay a generic `KaspaRpcException`. `KaspaWrongNetworkException` is `NetworkIdError` and `NetworkTypeError` in `consensus/core/src/network.rs`. The prefixes are `Invalid network type:`, `Invalid network id:`, `Invalid network name prefix:`, `Invalid network suffix:`, `Missing network suffix:`, `Network suffix required for network type:`, and `Unexpected extra token:`. An RPC text error leaves `ReportedNetworkId` and `ExpectedNetwork` null. A generic error such as `nope` stays `KaspaRpcException`.

`EnsureReadyAsync` reads `getServerInfo` first. A different network throws `KaspaWrongNetworkException` with `Node reported network '{id}'. Expected '{name}'.`. `testnet-10` matches `Network.Testnet`. When `requireSynced` is true and `isSynced` is false, the message is `Node is not synced.` When `requireUtxoIndex` is true and `hasUtxoIndex` is false, the message is `Node does not have the UTXO index enabled.` Both flags false returns once the network matches. `GetServerInfoAsync` does not apply those checks. An unknown network id is a wrong-network error.

## 0.1.11 personal message

`PersonalMessage` is rusty-kaspa `sign_message` and `verify_message` (`wallet/core/src/message.rs`, commit `8fe466311ba179e74fc8a52f13b36140bb6b9a4e`). `Hash` is BLAKE2b-256 of the UTF-8 message. The BLAKE2b key is the ASCII string `PersonalMessageSigningHash` (`crypto/hashes/src/hashers.rs`, `blake2b_hasher!`). `SignMessage(Key, message)` returns a BIP-340 `SchnorrSignature`. An empty auxiliary span draws 32 random bytes. Thirty-two explicit bytes, including all zeros, are passed through. All zeros are test case 0. They are not `sign_schnorr_no_aux_rand`.

`VerifyMessage(PubKey, message, signature)` checks that BIP-340 signature. `VerifyMessage(KaspaAddress, message, signature)` accepts only a `KaspaPubKeyAddress`. The 32-byte payload must be that x-only key, and the key must lie on the curve. An ECDSA address or a script-hash address returns false even when the payload bytes match a key. A payload that is not on the curve returns false. The address prefix is not hashed, so the same key verifies on mainnet and on testnet. `samples/SignMessage` calls this API. The old sample hashed the UTF-8 bytes with unkeyed BLAKE2b-256 and also signed ECDSA. That digest does not match rusty-kaspa.

## Later 0.1.10 fixes

Follow-up from a Linux review (against `f41c86a`) and wallet notes R1/R10. The version stayed 0.1.10-alpha.

Checksum: twelve `abandon` words have a bad checksum. The lenient constructor `new Mnemonic(sentence)` still does not throw, and `IsValidChecksum` is false, so user input can be inspected. `Mnemonic.Parse` and `Mnemonic.ParseValidated` throw `FormatException` with the message exactly `BIP39 checksum does not match.`. `requireValidChecksum: true` throws the same way. `DeriveSeed` and `DeriveExtKey` throw `InvalidOperationException` on a bad checksum. The message contains `allowInvalidChecksum: true`. Passing `allowInvalidChecksum: true` still derives from those words. That is not a recoverable wallet. The seed passphrase is the NFKD of `ToPhrase()`, not the redacted `ToString()`.

Variants: uppercase, U+3000, zero-width characters, a BOM, NBSP, and full-width letters are stored as the canonical words when they match the list. The seed matches the canonical sentence. It does not silently become another wallet. Case-insensitivity and zero-width removal are NKaspa extensions to BIP39. A seed derived from the canonical sentence is what other wallets accept for that sentence. Typing uppercase or full-width text verbatim into another wallet may fail. A zero-width character used alone as a separator, an unknown word, or an illegal word count still throws `FormatException`. The passphrase is not cleaned that way: a zero-width character in the passphrase changes the seed. That is what BIP39 requires of a passphrase.

Redaction: `Mnemonic.ToString()` returns `NKaspa.Mnemonic`. The sentence is `ToPhrase()`. A copy of the words is `GetWords()`. `ExtKey.ToString()` returns `NKaspa.ExtKey`. The extended private key is `ExportXprv()` or `ExportXprv(Network)`. `Words` is an `IReadOnlyList<string>` (`ReadOnlyCollection`). Replacing an element throws `NotSupportedException`. `Key`, `ExtKey`, and `Mnemonic` carry a refusing `JsonConverter`. Without `allowPrivateKeys: true`, both `JsonSerializer.Serialize` and `NKaspaJsonSerializer` throw `InvalidOperationException`. The text carries WARNING and does not contain the words or `kprv`. `NKaspaJsonSerializer` unwraps a converter exception that was wrapped in `JsonException`. On the current .NET 8, a direct `JsonSerializer.Serialize` of these three types throws `InvalidOperationException` itself, and the message has no words and no `kprv`.

Change: after Toccata, a 1 KAS input paying at `FeeRate.MinimumRelay` with 2.5 million, 5 million, or 10 million sompi left cannot pay the storage mass of a change output from what remains. The builder drops the change and folds it into the fee. `ChangeFoldedIntoFee` equals the remainder, the sompi taken from change, not "the required fee plus the change". At 30 million and 50 million sompi the change output fits, and `ChangeFoldedIntoFee` is 0. Folding also has a cap: the amount is at most `max(MinimumChange, MaxFoldedChange)`, and at most `MaxFoldedFeeFactor` times the fee the no-change transaction needs. Default `MaxFoldedChange` is 0.1 KAS (10 million sompi). Default `MaxFoldedFeeFactor` is 100. Both properties are public. A caller can raise them. They are a wallet safety limit, not consensus. Raising them moves more sompi into the fee. Dust below `MinimumChange` still folds under the old rule and is not limited by that factor. Above the cap the builder throws `InvalidOperationException`: `Change would exceed the mass limit. Folding it into the fee would burn more than MaxFoldedChange allows. Split the send or add inputs so the change output fits.` On failure, `ChangeFoldedIntoFee` is 0. `TransactionMass` does not record the number.

One 50 KAS UTXO paying 0.1 KAS at 150 sompi/gram: the default builder is post-Toccata, storage mass is about 100 thousand, under the 500 thousand block limit, the required fee is about 0.15 KAS, and change is about 49.75 KAS. Change is kept. 49.9 KAS is not burned. The same numbers with `toccataActive: false` (per-transaction mass cap 100000) make the transaction with change exceed the cap. Change is far above 0.1 KAS, so the exception above is thrown. 49.9 KAS is not folded into the fee.

WebSocket: `KaspaWebSocketOptions.MaxMessageBytes` defaults to `DefaultMaxMessageBytes` (16 MiB). All fragments of one message may add up to the cap. One more byte fails. A waiting `ExchangeAsync` receives `InvalidOperationException`. The message names the cap in bytes and does not include the payload. That failure does not reconnect. `RequestTimeout` defaults to 30 seconds, `ConnectTimeout` to 15 seconds, and `AutoReconnect` to false. Those already existed. This round added `State` (Closed, Connecting, Open, Reconnecting), `StateChanged`, and `Reconnected`. The first successful `ConnectAsync` does not raise `Reconnected`. An exception thrown by a callback is ignored so it cannot block `FailPending`.

testnet-10 Toccata: on 2026-10-31 a read-only GET of `https://api-tn10.kaspa.org/info/blockdag` was made. No transaction was submitted and no WebSocket was opened. The response `networkName` was `kaspa-testnet-10`, `virtualDaaScore` was `611024731`, `blockCount` and `headerCount` were both `1503250` (counts inside the pruning window, not chain height), `pastMedianTime` was `1793437188174`, and `sink` was `dd0dac7ba6b9ee3e727a9c133ea08c16da49c5e46149685bc5320d3884906cb9`. This library's testnet Toccata activation score is `Network.TestnetToccataActivationDaaScore` = 467579632, from rusty-kaspa `90dbf074` `params.rs` line 785 (tn10-toc2 / tn10-toc3 as secondary sources). Current master deleted that field. `Network.TestnetZkHardeningActivationDaaScore` = 476232000 is tn10-toc3 `zk_hardening_activation`. It is not the Toccata activation score, and `Network.Testnet` does not use it. 611024731 is above both scores, so by the recorded Toccata score testnet-10 is active. `Network.Testnet.IsToccataActive(611024731)` is true. rusty-kaspa master PR 1089 removed `Params::toccata_activation`. Block limits for mainnet, testnet, simnet, and devnet are compute 500000, storage 500000, and transient 1000000. The mainnet activation score is 474165565 (`90dbf074` `params.rs` line 724). Simnet and Devnet activation scores are 0.

## 0.1.10 re-check

Compared with a read-only clone of `https://github.com/kaspanet/rusty-kaspa` on 2026-10-31. The version number did not change. It stayed 0.1.10-alpha. Mnemonic parsing in the library did not change. WebSocket disconnect handling in the library did not change. The fake server in the tests really resets the socket.

Fee rate. rusty-kaspa `90dbf074275d60c1fe74a3491883196f110970c0` `mining/src/mempool/config.rs` lines 20–23: `DEFAULT_MINIMUM_RELAY_TRANSACTION_FEE` is `100_000` sompi/kg, which is 100 sompi/gram. Lines 25–28 of the same file: `LEGACY_MINIMUM_RELAY_TRANSACTION_FEE` is `1_000` sompi/kg, which is 1 sompi/gram. The comment says that is the floor before Toccata activation. So `PostToccataMinimumRelayFeeRate` is 100 and `PreToccataMinimumRelayFeeRate` is 1. `FeeRate.MinimumRelay` and `MassCalculator.MinimumFeeRate` stay 100. Forcing the pre-Toccata policy (`MassPolicy.PreToccata`, `toccataActive: false`, or a DAA below the activation score) yields 1 sompi/gram, which a current node may reject. Live networks use the default policy.

Activation scores. Mainnet `474165565` is `90dbf074` `consensus/core/src/config/params.rs` line 724: `toccata_activation: ForkActivation::new(474_165_565)`. Testnet `467579632` is line 785: `toccata_activation: ForkActivation::new(467_579_632)`. tn10-toc2 line 783 and tn10-toc3 line 784 are secondary sources. Current master deleted the field. Lines 796–800 reject an unknown `toccata_activation` field, so `Network.WithToccataActivationDaaScore` can replace the score. Prefixes, ports, and extended-key versions are unchanged. The score selects both the per-transaction mass cap and the relay rate. `476232000` is tn10-toc3 `params.rs` line 786, `zk_hardening_activation: ForkActivation::new(476_232_000)`. It is not Toccata. `Network.TestnetZkHardeningActivationDaaScore` records it. `Network.Testnet` does not use it. To treat it as a policy switch, pass it to `WithToccataActivationDaaScore`. Simnet and Devnet activation scores stay 0.

WebSocket. A managed `HttpListener` (Linux) `NetworkStream` does not own the TCP socket. `WebSocket.Abort` alone does not disconnect. The fake server, when `_connection` / `_socket` exists, sets `LingerOption(true, 0)`, calls `Close(0)`, then `Abort`. Windows HTTP.sys does not have that field. The existing `Abort` still closes that layer.

Change. The only property that records sompi folded into the fee is `TransactionBuilder.ChangeFoldedIntoFee`. `TransactionMass` does not repeat it.

## 0.1.2 integration fixes

During a mainnet integration, multi-input and version-1 payments were assembled by hand outside the library. This version put both paths back on `TransactionBuilder`. The regression uses the shape of transactions that were already on chain. The tests have no keys and do not broadcast:

| Transaction | Version | Inputs | Amount | Fee | Relay mass |
| --- | --- | --- | --- | --- | --- |
| `5fd8f8600950c36b1d9195d9afab0bbf5bde94594fd8246d2c2419c64f703868` | 0 | 1 | 0.2 KAS | 203600 | 2036 |
| `2febe6cafd5f78f8ce21636d48f060560b39a7529b00646a00cf5d62098aa3d4` | 0 | 2 | 0.2 KAS | 315400 | 3154 |
| `4d1c6a5e1b127278945673b3ed116786110b2b1ca44090c9f810a50e12350714` | 1 | 1, compute budget 10 | 0.1 KAS | 203800 | 2038 |

The version-1 wRPC JSON has both `sigOpCount` and `computeBudget`, matching the `d5f68e7` spelling.

## Samples

Console projects under `samples/`, the short examples in the NBitcoin docs. They do not broadcast by default. Offline samples are covered by `tests/NKaspa.Tests/SampleSmokeTests.cs` and `samples/smoke.ps1`. `NodeQuery` dials a public node and is not part of the unit tests. `Json` only prints JSON and does not connect.

| Sample | Project | NBitcoin | NKaspa |
| --- | --- | --- | --- |
| Mnemonic and derived addresses | `samples/Mnemonic` | `Mnemonic`, `ExtKey`, `KeyPath` | `Mnemonic.DeriveExtKey`, `KeyPath.KaspaReceive(i)` = `m/44'/111111'/0'/0/i`. Mainnet `kpub`, testnet `ktub`. |
| Address and ScriptPubKey | `samples/Address` | `BitcoinAddress.Create`, `ScriptPubKey` | `KaspaAddress.Parse` / `TryParse` / `IsValid`, and `KaspaAddress.ScriptPubKey`. |
| Read-only node query | `samples/NodeQuery` | read-only `RPCClient` calls | `KaspaWebSocketChannel` + `KaspaWrpcJsonClient`. Methods are `getServerInfo`, `getBlockDagInfo`, `getBalanceByAddress`, `getUtxosByAddresses`. Default `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json`. Does not call `submitTransaction`. |
| Build and sign | `samples/Spend` | `TransactionBuilder` | Version 0, version 0 with `MustSpend` on two inputs, and version 1. The fee is `SendMinimumRelayFee`. By default it prints transaction hex and wRPC JSON. It submits only when `--broadcast` is set, `NKASPA_MNEMONIC` is set, and `--to` and `--amount` are passed. Before submit it checks the payment output, the minimum relay fee, and `Transaction.Verify`. `--from` and `--max-fee-sompi` are optional. |
| PSKT 2-of-2 | `samples/PsktMultisig` | BIP174 `PartiallySignedTransaction` (not ported) | `NKaspa.PSKT.PSKT`: create, both parties `Sign`, `Combine`, `Finalize`, `ExtractTransaction`. |
| Offline message signing | `samples/SignMessage` | `Key.SignMessage`. BIP322 stays not applicable | `PersonalMessage.SignMessage` / `VerifyMessage`. BLAKE2b-256 keyed with `PersonalMessageSigningHash`, then BIP-340. The address overload accepts only a Schnorr pay-to-pubkey payload. Not the Bitcoin message prefix. |
| Tutorial snippets | `samples/Tutorials` | short examples | `AddressTutorial` and `SendTutorial.RunOffline`, matching `docs/tutorials/`. Those snippets compile, open no socket, and do not call `SubmitTransactionAsync`. `SendOnTestnetAsync` exists so the broadcast path compiles. It defaults to testnet. The sample entry point does not call it. |
| JSON | `samples/Json` | `NBitcoin.JsonConverters.Serializer` | `NKaspaJsonSerializer` and `AddNKaspaConverters(network)`. Version 0 and version 1 (`computeBudget`, `covenant`), and version-prefixed hex for `scriptPublicKey`. Private keys are refused by default. |

## Still open

- BN254 Groth16 pairing inside `OP_ZKPRECOMPILE`, on-curve and subgroup checks of compressed points, and the RISC0 succinct STARK. Structural checks already run. A point with a valid length that is not on the curve is gamma-billed and then returns unimplemented. The engine does not accept the proof.
- A PSKT byte comparison against a running rusty-kaspa. Upstream tests do not pin a vector.
- Version-1 compute budget inside PSKT. rusty-kaspa `unsigned_tx` is still a TODO there.
- A NuGet publish. This version only changes the version number. The package is not pushed.
- A Newtonsoft.Json adapter. Serialization is System.Text.Json only. That package was not added.
- The unmerged transient-mass change (PR 1141), and a testnet activation DAA that could not be confirmed from source.
- `--broadcast` in the samples has submitted once to a public mainnet node. wRPC JSON must write `verboseData` and `covenant: null` when there is no covenant, or `submitTransaction` is rejected. Before submit the sample checks the derived address, the payment output, the minimum relay fee, and `Transaction.Verify`.
- `ConsensusSeqCommitAccessor` is still not a node. The default request cap is 256. Hitting it throws. It does not scan the whole finality window, and it does not treat a missing block as `BlockAlreadyPruned`. Forward chain growth is no longer mistaken for a reorg. More than 32 steps ahead uses the latest `isChainBlock` and does not chase the tip block by block. `StableDepth` defaults to 20. Reorg retries default to 3. `WrpcSeqCommitAccessor` still uses the DAG sink as its viewpoint.

## Next

- Add BN254 pairing and the RISC0 STARK when a verifier is available. Until then they stay unimplemented.
- Pin multi-public-key map order and multi-xpub JSON key order when an upstream vector exists.
- Prepare the NuGet package. Samples already live under `samples/` and do not broadcast by default.

English tutorials: [Generate a Kaspa address in C#](tutorials/generate-kaspa-address-csharp.md), [Send a Kaspa transaction in .NET](tutorials/send-kaspa-transaction-dotnet.md).
