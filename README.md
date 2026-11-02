# NKaspa

NKaspa is a .NET (C#) library for Kaspa: addresses, keys, BIP39/BIP32 wallets, transactions, PSKT and a wRPC client. Install: `dotnet add package NKaspa --prerelease`.

**Used by:** [KasNetWallet](https://github.com/lifeme2027/KasNetWallet), an open-source Windows wallet built on NKaspa (alpha)，[Nkaspa document](https://github.com/lifeme2027/NKaspaDoc)，[Xiangqi Arena](https://github.com/lifeme2027/xiangqi-dotnet)（[live](https://chess.kastop.com)）— verifies Kaspa personal-message signatures with NKaspa 0.1.11-alpha (`PersonalMessage`); optional 1 KAS resume-service fee path; signature vectors unit-tested; real KasWare + on-chain payment e2e not claimed.

[English](#english) · [中文](#中文)

## English

This repository is **0.1.11-alpha**. The NuGet package index lists **0.1.10-alpha** as the newest published version (`0.1.9-alpha` is also published). Snippets here track the repository. `KaspaRpcClient`, Borsh, path-based Auto encoding, gRPC-port rejection, and `KaspaRpcEncodingMismatchException` require 0.1.11-alpha. `Mnemonic.ToPhrase`, `KaspaWebSocketOptions`, `MassPolicy`, and the folded-change cap require 0.1.10-alpha.

### What's new in 0.1.11

0.1.11-alpha is on [NuGet](https://www.nuget.org/packages/NKaspa/0.1.11-alpha). The full list is in [CHANGELOG.md](CHANGELOG.md).

- wRPC Borsh client added. It coexists with the JSON client: both stay available through `KaspaRpcClient`, and existing JSON code keeps working. `KaspaRpcClient.Create(url, KaspaRpcEncoding.Auto)` picks one by reading the URL path first, then the port.
- `GetSinkBlueScoreAsync` reads the sink blue score (`getSinkBlueScore`, Borsh op 138).
- `ConsensusParams.IsCoinbaseMature` and `SpendableAtDaaScore` apply rusty-kaspa coinbase maturity in DAA score.
- `WalletKeystore` encrypts a mnemonic or a seed with PBKDF2-HMAC-SHA256 and AES-256-GCM. It is not the same format as KasNetWallet's Argon2 keystore, and neither can read the other's files.
- `FeeEstimator` estimates compute mass, KIP-9 storage mass, and the fee without private keys.
- `PersonalMessage.SignMessage` and `VerifyMessage` match rusty-kaspa `sign_message`.
- wRPC failures a wallet can branch on are `KaspaWrpcException`. `KaspaNodeUnreachableException` wraps timeouts and disconnects.
- `MassPolicy.ForNetwork(network, virtualDaaScore)` selects the mass policy from the network activation scores.
- `KaspaResolver` finds a public node through rusty-kaspa's resolver list and falls back to `DefaultFallbackEndpoints`.
- `TransactionBuilder` throws `KaspaTransactionBuilderException` with a `Reason`. The WebSocket channels throw `KaspaConnectionClosedException`, `KaspaMessageTooLargeException`, and `KaspaRpcTimeoutException`. Each extends the type it replaced, and the messages are unchanged.

### FAQ

**Is there a Kaspa SDK for C#/.NET?** Official SDKs are Rust, WASM, and Python. NKaspa is a community .NET library (MIT, see [LICENSE](LICENSE)).

**Is there a C# library for Kaspa?** Yes. NKaspa is a C# library for Kaspa on NuGet (`dotnet add package NKaspa --prerelease`). It covers addresses, keys, BIP39/BIP32 wallets, transactions, PSKT, and a wRPC client. It is alpha.

**Can I build a Kaspa wallet with .NET?** Yes. NKaspa provides mnemonics, key derivation (`m/44'/111111'/0'/0/0`), transaction building and signing, and a wRPC client for balances and broadcasting. [KasNetWallet](https://github.com/lifeme2027/KasNetWallet) is an open-source Windows wallet built on NKaspa (alpha) and serves as a working example.

**How do I install it?** `dotnet add package NKaspa --prerelease`. The library targets `net8.0` and `netstandard2.1`.

**How do I generate an address?** `Key.Create().GetAddress(Network.Mainnet)`. The longer walk-through is [Generate a Kaspa address in C#](docs/tutorials/generate-kaspa-address-csharp.md).

**How do I send KAS?** Build with `TransactionBuilder`, then broadcast with `SubmitTransactionAsync`. See [Send a Kaspa transaction in .NET](docs/tutorials/send-kaspa-transaction-dotnet.md). Nothing in the library broadcasts on its own.

**Is it like NBitcoin?** The same type names are used where Kaspa has a counterpart (`KaspaAddress` and `BitcoinAddress`). The behavior is Kaspa's. `uint256.ToString()` is the Kaspa wire order. It is not the byte-reversed hex from `NBitcoin.uint256.ToString()`. The map is [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md) and [docs/NBitcoin-Mapping.en.md](docs/NBitcoin-Mapping.en.md).

**What derivation path?** `m/44'/111111'/0'/0/0`, which is `KeyPath.KaspaReceive(0)`.

**Which RPC?** wRPC over WebSocket, both JSON and Borsh, through `KaspaRpcClient` (requires 0.1.11-alpha). The two clients coexist; pick one per connection. There is no gRPC client.

- **Borsh or JSON?** With `KaspaRpcEncoding.Auto`, the URL path is read before the port. A path ending in `/wrpc/borsh` or containing `/borsh` selects Borsh. A path ending in `/wrpc/json` or containing `/json` selects JSON. The path wins over the port. An explicit `KaspaRpcEncoding.Borsh` or `KaspaRpcEncoding.Json` overrides both.
- **Which ports?** Borsh is 17110, 17210, 17510, and 17610. JSON is 18110, 18210, 18510, and 18610. The gRPC ports 16110, 16210, 16510, and 16610 are rejected with `ArgumentException`.
- **Port 443?** `wss` on port 443 needs `/json` or `/borsh` in the path. If the port and the path are both unknown, `Create` throws. It does not default to either encoding.
- **What if the encoding is wrong?** A JSON client that receives a binary frame, or a Borsh client that receives a text frame, throws `KaspaRpcEncodingMismatchException`. Pending requests fail at once. The client does not reconnect.
- **Finding a public node?** `KaspaResolver` looks up a public node with rusty-kaspa's resolver list (`GET /v2/kaspa/{network}/{tls|any}/wrpc/{json|borsh}`) and can open that socket. Public resolvers may fail or return 404; `KaspaResolver` tries each one. If every resolver fails, it falls back to `DefaultFallbackEndpoints`: `wss://eva.kaspa.green` on mainnet, or `wss://vector-10.kaspa.green` and `wss://muon-10.kaspa.blue` on testnet-10. Those nodes are not guaranteed to be available. Pass an empty fallback list to disable them, or pass your own list to replace them.

**Is it production ready?** It is alpha. Block types are not implemented. BN254 pairing and RISC0 STARK verification are not implemented.

**Toccata support?** Version-1 transactions and the post-Toccata mass and fee policy are implemented. `SetVersion(1)` builds a version-1 payment. The minimum relay fee is 100 sompi per gram of the larger of compute mass and normalized transient mass.

NKaspa is a .NET library for [Kaspa](https://kaspa.org/). The public types follow [NBitcoin](https://github.com/MetacoSA/NBitcoin) where a Kaspa counterpart exists.

The public types follow [NBitcoin](https://github.com/MetacoSA/NBitcoin) where a Kaspa counterpart exists. `KaspaAddress` corresponds to `BitcoinAddress`. `uint256` corresponds to `NBitcoin.uint256`. `Encoders` corresponds to `NBitcoin.DataEncoders.Encoders`. The behavior is Kaspa's, not Bitcoin's: addresses use cashaddr-style bech32 (`kaspa`, `kaspatest`, `kaspasim`, `kaspadev`, 8-character checksum), and consensus hashes are unkeyed BLAKE2b-256.

NBitcoin is MIT licensed. Copyright (c) .NET Foundation and Contributors / Nicolas Dorier. See [Acknowledgement](#acknowledgement) and [NOTICE](NOTICE). Elliptic-curve operations use the `NBitcoin.Secp256k1` package. BLAKE2b is implemented in this repository from [RFC 7693](https://www.rfc-editor.org/rfc/rfc7693). BLAKE3 is implemented from the BLAKE3 specification. No extra hash package was added.

### Status

Alpha. This release contains encoders (hex, Kaspa bech32, Base58Check), BLAKE2b-256, BLAKE3-256, `uint256`, the four networks, and addresses (Schnorr public key, ECDSA public key, script hash). It also contains keys, BIP-340 Schnorr and compact ECDSA signatures, script parsing and the standard pay-to-pubkey / pay-to-script-hash templates, version-0 and Toccata version-1 transactions and sighash, BIP39 mnemonics, BIP32 extended keys, a transaction builder, Kaspa mass and fee rates, wRPC JSON and Borsh clients, PSKT (partially signed Kaspa transactions), and System.Text.Json converters in `NKaspa.JsonConverters`. PSKT follows rusty-kaspa `wallet/pskt`: the roles Creator, Constructor, Updater, Signer, Combiner, Finalizer, and Extractor; Schnorr and ECDSA partial signatures; combine; finalize for single-sig P2PK and multisig P2SH; and extract. `TransactionBuilder.BuildPSKT` covers version 0 and version 1. The hex form is the prefix `PSKT` plus lowercase hex of the compact JSON. A bundle uses `PSKB`. The builder can force every named coin into the transaction, prefer one coin when that coin covers the payment, and emit a version-1 payment. The script engine executes version-0 scripts, including pay-to-script-hash redeem scripts. `Script.VerifyScript`, `Transaction.Verify`, and `TransactionBuilder.Verify` check every input against its sigop count or version-1 compute budget. `OP_ZKPRECOMPILE` enforces the finalized tag, cost, and layout checks. The BN254 pairing and the RISC0 STARK stay unimplemented and do not push true. `OP_CHAINBLOCKSEQCOMMIT` needs a caller-supplied block accessor. `WrpcSeqCommitAccessor` answers from read-only `getBlock` and `getBlockDagInfo`, using the DAG sink as its point of view. `ConsensusSeqCommitAccessor` walks the selected-parent chain from those same read-only calls. It freezes a chain sink and its blue score, steps the viewpoint back 20 selected parents by default, checks the KIP-21 window against that snapshot, and treats a later sink as ordinary growth when the new selected-parent chain still contains the snapshot. A tip more than 32 selected parents ahead is not chased block by block: the snapshot blocks are read again and kept when isChainBlock stays true. It throws if the node data is incomplete, inconsistent, or still reorged after a few retries. A light client can only approach consensus. Matching it exactly requires a running node: the walk stops at a request limit and cannot see pruned reachability. Nothing in this library broadcasts a transaction on its own. The console samples under `samples/` print transactions or query a node. `samples/Spend` submits a transaction only when `--broadcast` is set and the environment variable `NKASPA_MNEMONIC` is set. The JSON wRPC client is covered by canned JSON and a loopback WebSocket. The Borsh client is covered by hand-computed frames, a fake binary WebSocket, and a subscription notification. `GetSinkBlueScoreAsync` reads the sink blue score (`getSinkBlueScore`, Borsh op 138). `GetBlockDagInfo` JSON may carry `blueScore`. The Borsh response does not, so a live node leaves `RpcBlockDagInfo.BlueScore` at 0 unless that JSON field is present. Header `blueWork` is 24 little-endian bytes (`BlueWorkType` / `Uint192`). `ConsensusParams.IsCoinbaseMature` applies rusty-kaspa coinbase maturity in DAA score. `WalletKeystore` encrypts a mnemonic or a seed with PBKDF2-HMAC-SHA256 and AES-256-GCM. Argon2 is not in the .NET base class library. `samples/NodeQuery` is the read-only call to a public node. The map of NBitcoin areas is in [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md) ([English](docs/NBitcoin-Mapping.en.md)). Tutorials: [generate an address](docs/tutorials/generate-kaspa-address-csharp.md), [send a transaction](docs/tutorials/send-kaspa-transaction-dotnet.md).

### Usage

```csharp
using NKaspa;
using NKaspa.Policy;
using NKaspa.RPC;

var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
using var master = mnemonic.DeriveExtKey();
using var receive = master.Derive(KeyPath.KaspaReceive(0));
var address = receive.PrivateKey.GetAddress(Network.Mainnet);

var fee = FeeRate.MinimumRelay.GetFee(2_036);

// Change below 0.02 KAS is added to the fee.
// A larger change is folded only when that output cannot be built, the amount is at most max(MinimumChange, MaxFoldedChange)
// (MaxFoldedChange defaults to 0.1 KAS), and it is within MaxFoldedFeeFactor of the no-change fee (default 100).
// MaxFoldedChange and MaxFoldedFeeFactor are public. A caller can raise them. They are a wallet safety limit, not consensus.
// Raising them can burn more sompi into the fee. A larger unbuildable change throws.
// After a successful build, ChangeFoldedIntoFee is the sompi removed from change and added to the fee.
// It is 0 when change is kept or the build throws. It is not the total fee. TransactionMass does not repeat it.
// The default mass ceiling is the post-Toccata block limit (compute and storage 500000, transient 1000000).
// Pass `new TransactionBuilder(Network.Mainnet, toccataActive: false)` to keep the old 100000 cap and 1 sompi/gram relay rate.
// `new TransactionBuilder(network, virtualDaaScore)` selects that policy from the network activation score.
// Network.IsToccataActive(daaScore) is that comparison.
var tx = new TransactionBuilder()
    .AddKeys(receive.PrivateKey)
    .AddCoins(coin)
    .Send(payee, Money.Coins(1))
    .SetChange(address)
    .SendEstimatedFees(FeeRate.MinimumRelay)
    .BuildTransaction(sign: true);

IKaspaRpcClient rpc = new KaspaWrpcJsonClient(channel);
var info = await rpc.GetServerInfoAsync();
```

```csharp
// Requires 0.1.11-alpha.
// Auto reads the path, then the port. The path wins.
// A path ending in /wrpc/borsh or containing /borsh is Borsh. A path ending in /wrpc/json or containing /json is JSON.
// Borsh ports are 17110, 17210, 17510, and 17610. JSON ports are 18110, 18210, 18510, and 18610.
// wss on port 443 with no /borsh or /json hint throws. NKaspa does not default to either encoding.
// gRPC ports 16110, 16210, 16510, and 16610 throw. NKaspa speaks wRPC, not gRPC.
// Pass KaspaRpcEncoding.Borsh or KaspaRpcEncoding.Json to override the path and a wRPC port.
// A binary frame on a JSON client, or a text frame on a Borsh client, throws KaspaRpcEncodingMismatchException.
// Create does not connect. The same timeouts, reconnect, 16 MiB cap, State, and Reconnected apply to both encodings.
await using var node = KaspaRpcClient.Create("ws://127.0.0.1:17110");
await node.ConnectAsync();
RpcServerInfo server = await node.GetServerInfoAsync();
RpcBlockDagInfo dag = await node.GetBlockDagInfoAsync();
// GetBlockDagInfoResponse has no blueScore. The sink blue score is this call.
ulong sinkBlueScore = await node.GetSinkBlueScoreAsync();
RpcFeeEstimate fees = await node.GetFeeEstimateAsync();
Money balance = await node.GetBalanceByAddressAsync(address.ToString());
IReadOnlyList<RpcUtxo> utxos = await node.GetUtxosByAddressesAsync(new[] { address.ToString() });
IReadOnlyList<RpcAddressBalance> balances = await node.GetBalancesByAddressesAsync(new[] { address.ToString() });
await using KaspaSubscription watch = await node.NotifyUtxosChangedAsync(
    new[] { address.ToString() },
    change => { });
// NotifyVirtualDaaScoreChangedAsync and NotifyBlockAddedAsync use the same subscription type.
// SubmitTransactionAsync is on IKaspaRpcClient. This snippet does not call it.
```

`coin` is an `OutPoint` plus the `UtxoEntry` it spends. `payee` is a `KaspaAddress`. `channel` is an `IKaspaJsonChannel`. `KaspaWebSocketChannel` can open a socket. Pass `KaspaWebSocketOptions` for the 15 second connect timeout, the 30 second request timeout, optional reconnect, and `MaxMessageBytes` (default 16 MiB). A larger frame fails the read and does not grow an unbounded buffer. `State` reports Closed, Connecting, Open, or Reconnecting. `Reconnected` fires after a later socket opens, not after the first connect. A drop fails requests that are still waiting. The tests do not dial a public node. `KeyPath.KaspaReceive(0)` is `m/44'/111111'/0'/0/0`. Extended keys on mainnet use the `kprv` / `kpub` prefix. Testnet, simnet, and devnet use `ktrv` / `ktub`. `ExtKey.ToString()` returns `NKaspa.ExtKey`. Export the private extended key with `ExportXprv()`, `ExportXprv(network)`, `GetWif(network)`, `ToString(network)`, or `ToBase58()`. `Mnemonic.ToString()` returns `NKaspa.Mnemonic`. The sentence is `ToPhrase()`. `Words` is an `IReadOnlyList<string>`. `Parse` and `ParseValidated` throw `FormatException` (`BIP39 checksum does not match.`) when the checksum is wrong. The lenient constructor still returns an object with `IsValidChecksum` false. `DeriveSeed` and `DeriveExtKey` throw on that object unless `allowInvalidChecksum` is true. Case-insensitivity and removal of zero-width characters (including a leading BOM and marks inside a word) are NKaspa extensions to BIP39, together with NFKD. `ToPhrase()` stores the canonical lowercase wordlist words, so the derived seed matches other wallets given that canonical phrase. A phrase with uppercase or full-width characters typed verbatim into another wallet may fail. Default `JsonSerializer.Serialize` refuses `Key`, `ExtKey`, and `Mnemonic`. Opt in with `NKaspaJsonSerializer` and `allowPrivateKeys: true`. `FeeRate` is sompi per gram of mass.

The minimum relay fee is 100 sompi times the larger of compute mass and normalized transient mass. Storage mass is not part of that floor. A standard one-input Schnorr payment with change is 2036 grams, 203600 sompi, about 0.002 KAS. `SendEstimatedFees` multiplies a rate by the fee mass, which also includes normalized storage. `SendMinimumRelayFee` pays the relay floor. `MustSpend` and `SpendAllCoins` put every named coin in the transaction and sign each input. Coins that share a script are not merged. `SetVersion(1)` is a Toccata payment. The default compute budget is 10 per input. wRPC JSON for version 1 includes both `sigOpCount` and `computeBudget`.

```csharp
var floor = TransactionBuilder.EstimateFee(FeeRate.MinimumRelay, MassCalculator.StandardSchnorrPaymentMass);

var forced = new TransactionBuilder()
    .AddKeys(receive.PrivateKey)
    .AddCoins(firstCoin, secondCoin)
    .MustSpend(firstCoin, secondCoin)
    .Send(payee, Money.Coins(0.2m))
    .SetChange(address)
    .SendMinimumRelayFee()
    .BuildTransaction(sign: true);

var toccata = new TransactionBuilder()
    .SetVersion(1)
    .AddKeys(receive.PrivateKey)
    .AddCoins(coin)
    .Send(payee, Money.Coins(0.1m))
    .SetChange(address)
    .SendMinimumRelayFee()
    .BuildTransaction(sign: true);
```

`firstCoin` and `secondCoin` are `Coin` values, the same kind of value as `coin`. `floor` is 0.002036 KAS.

```csharp
var spent = new[] { coin.Utxo };
bool inputOk = Script.VerifyScript(tx.Inputs[0].SignatureScript, coin.Utxo.ScriptPublicKey, tx, 0, spent);
bool allOk = tx.Verify(spent);
```

`inputOk` is one input. `allOk` is every input. A version-0 input may spend `sigOpCount * 100000 + 9999` script units. A version-1 input may spend `computeBudget * 10000 + 9999`. The engine also stops a script longer than 1000000 bytes, a stack element longer than 1000000 bytes, a combined stack deeper than 244, or more than 1000000 non-push opcodes.

```csharp
using NKaspa.PSKT;

var pskt = new TransactionBuilder()
    .AddKeys(receive.PrivateKey)
    .AddCoins(coin)
    .Send(payee, Money.Coins(1))
    .SetChange(address)
    .SendMinimumRelayFee()
    .BuildPSKT()
    .Sign(receive.PrivateKey);

var carried = PSKT.FromHex(pskt.ToHex());
var tx = carried.Finalizer().Finalize().ExtractTransaction();
```

`BuildPSKT` returns a signer-role PSKT for the same unsigned payment as `BuildTransaction(false)`. `Sign` adds a Schnorr or ECDSA partial signature for each input that key can spend. `ToHex` is `PSKT` plus the compact JSON. `FromHex` comes back as a combiner. `Finalize` fills a single-sig P2PK script or a P2SH multisig redeem script, then `ExtractTransaction` copies those scripts onto the unsigned transaction. Pass two PSKTs to `Combine` before `Finalize` for a 2-of-2. Schnorr auxiliary randomness is random unless you pass 32 bytes to `PSKT.Sign`. Inside `namespace NKaspa`, write `NKaspa.PSKT.PSKT`, because the name `PSKT` is the namespace. A version-1 compute budget stays on the in-memory input and is not part of the JSON. This path does not broadcast.

`uint256` display hex is Kaspa wire order (the same bytes BLAKE2b emits). It is not reversed the way `NBitcoin.uint256.ToString()` reverses them. Numeric comparison still treats byte 0 as the least significant byte.

### JSON

`NKaspa.JsonConverters.NKaspaJsonSerializer` is the counterpart of `NBitcoin.JsonConverters.Serializer`. It uses `System.Text.Json` (package 8.0.5, already referenced, so `net8.0` and `netstandard2.1` share it). There is no Newtonsoft.Json adapter.

Amounts are sompi integers. Names inside Kaspa values are camelCase. A transaction is the same compact object as `KaspaRpcJson.WriteTransaction`, including version 0 and version 1 (`sigOpCount`, and `computeBudget` on version 1) and an optional output `covenant`. `scriptPublicKey` is written as `{ "version", "script" }`. Reading also accepts `scriptPublicKey` on that object, and the rusty-kaspa hex string whose first two bytes are the big-endian version. `AddNKaspaConverters(network)` registers the converters and requires that network for addresses and extended keys.

`Key` and `ExtKey` throw unless `allowPrivateKeys` is true. That option writes secret bytes. Do not turn it on for logs or RPC.

```csharp
using System.Text.Json;
using NKaspa.JsonConverters;

var json = NKaspaJsonSerializer.Serialize(tx, Network.Mainnet);
var copy = NKaspaJsonSerializer.Deserialize<Transaction>(json, Network.Mainnet);

var options = new JsonSerializerOptions();
options.AddNKaspaConverters(Network.Mainnet);
```

`tx` is a `Transaction`. `copy` is the same transaction. The options object is for `JsonSerializer` calls that are not `NKaspaJsonSerializer`.

### Samples

Console samples live in `samples/` and are part of `NKaspa.sln`. They use the same calls as the snippets above. `NodeQuery` is read-only. The others run offline. `samples/smoke.ps1` runs those offline programs. It uses `dotnet` on `PATH`, or `dotnet.exe` under `DOTNET_ROOT` when that variable is set. `SampleSmokeTests` runs the same checks under `dotnet test`.

```bash
dotnet run --project samples/Mnemonic
dotnet run --project samples/Mnemonic -- abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about
dotnet run --project samples/Address
dotnet run --project samples/NodeQuery
dotnet run --project samples/NodeQuery -- --url wss://eva.kaspa.green/kaspa/mainnet/wrpc/json --address kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva
dotnet run --project samples/Spend
dotnet run --project samples/PsktMultisig
dotnet run --project samples/SignMessage
dotnet run --project samples/SignMessage -- NKaspa
dotnet run --project samples/Json
dotnet run --project samples/Tutorials
powershell -File samples/smoke.ps1
```

`Mnemonic` draws a 12-word English mnemonic, or uses the words you pass, and derives `m/44'/111111'/0'/0/0` through `2` on mainnet (`kaspa`, `kpub`) and testnet (`kaspatest`, `ktub`). `Address` parses Schnorr, ECDSA, and script-hash addresses and prints `ScriptPubKey`. `NodeQuery` opens the wRPC JSON WebSocket, then calls `getServerInfo`, `getBlockDagInfo`, `getBalanceByAddress`, and `getUtxosByAddresses`. The default URL is `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json`. It stays on JSON. The Borsh client is the `KaspaRpcClient.Create` snippet above. `smoke.ps1` does not open a socket. `Json` prints compact JSON for the public types, including a version-1 transaction with `computeBudget` and `covenant`, and refuses to print a private key.

`Spend` builds three signed payments from public test keys and fake coins: version 0, version 0 with `MustSpend` on two inputs, and version 1. Each pays `SendMinimumRelayFee`. It prints the transaction hex and the wRPC JSON. It does not open a socket.

**Warning:** `dotnet run --project samples/Spend -- --broadcast` submits a transaction. It runs only when `--broadcast` is set and `NKASPA_MNEMONIC` is set. You also pass `--to` and `--amount`. Optional flags are `--url`, `--network`, `--version`, `--must-spend`, `--from`, and `--max-fee-sompi`. Before `submitTransaction`, the sample checks that an output pays `--amount` to `--to`, that the fee is the minimum relay fee, and that `Transaction.Verify` succeeds. `--from` must match the derived receive address when it is set. `--max-fee-sompi` refuses a fee above that sompi count. The sample does not read a secrets directory. Do not set that variable unless you intend to spend.

`PsktMultisig` is a 2-of-2 Schnorr PSKT: create, both parties sign, combine, finalize, extract. `SignMessage` signs a UTF-8 string with `PersonalMessage.SignMessage`. The digest is BLAKE2b-256 keyed with `PersonalMessageSigningHash`, then BIP-340. It checks the Schnorr public key and the Schnorr address. That is rusty-kaspa `sign_message`, not Bitcoin's message prefix and not BIP322. `samples/Tutorials` is the compilable form of the two English tutorials. It stays offline.

### Build

The repository pins the .NET SDK in `global.json` (8.0.427, roll forward to the latest feature band).

```bash
dotnet build NKaspa.sln
dotnet test NKaspa.sln
```

Projects:

- `src/NKaspa` — class library, `net8.0` and `netstandard2.1`
- `tests/NKaspa.Tests` — xUnit, `net8.0`
- `samples/` — console samples, `net8.0`

Package id `NKaspa`. Repository: https://github.com/lifeme2027/NKaspa

### License

[MIT](LICENSE). Copyright (c) 2026 NKaspa contributors.

### Acknowledgement

NKaspa's API design references [NBitcoin](https://github.com/MetacoSA/NBitcoin). NBitcoin is licensed under the MIT License. Copyright (c) .NET Foundation and Contributors / Nicolas Dorier.

NKaspa does not include the NBitcoin source tree. Elliptic-curve operations use the `NBitcoin.Secp256k1` package (MIT). The BIP39 English wordlist in `src/NKaspa/BIP39/english.txt` is the list shipped with NBitcoin; its notice is `src/NKaspa/BIP39/WORDLIST-LICENSE.txt`. Details are in [NOTICE](NOTICE).

## 中文

NKaspa 是 Kaspa 的 .NET（C#）基础函数库：地址、密钥、BIP39/BIP32 钱包、交易、PSKT，以及 wRPC 客户端。安装：`dotnet add package NKaspa --prerelease`。

本仓库是 **0.1.11-alpha**。NuGet 上已发布的最新版本是 **0.1.10-alpha**（另有 **0.1.9-alpha**）。这里的片段跟仓库走。`KaspaRpcClient`、Borsh、按路径选择的 Auto、拒绝 gRPC 端口，以及 `KaspaRpcEncodingMismatchException` 需要 0.1.11-alpha。`Mnemonic.ToPhrase`、`KaspaWebSocketOptions`、`MassPolicy` 和找零并入上限需要 0.1.10-alpha。英文教程：[生成地址](docs/tutorials/generate-kaspa-address-csharp.md)，[发送交易](docs/tutorials/send-kaspa-transaction-dotnet.md)。英文对照表：[docs/NBitcoin-Mapping.en.md](docs/NBitcoin-Mapping.en.md)。

公开类型尽量与 [NBitcoin](https://github.com/MetacoSA/NBitcoin) 一一对应，但语义按 Kaspa。地址是 cashaddr 风格的 bech32（前缀 `kaspa` / `kaspatest` / `kaspasim` / `kaspadev`，8 字符校验和）。共识哈希是不带密钥的 BLAKE2b-256，不是比特币的双 SHA256。

NBitcoin 采用 MIT 许可。Copyright (c) .NET Foundation and Contributors / Nicolas Dorier。致谢见 [致谢](#致谢) 和 [NOTICE](NOTICE)。椭圆曲线使用 NuGet 包 `NBitcoin.Secp256k1`（MIT）。BLAKE2b 按 RFC 7693 在本库实现。BLAKE3 按规范在本库实现，没有额外的哈希包。

### 状态

Alpha。这一版包含编码器（十六进制、Kaspa bech32、Base58Check）、BLAKE2b-256、BLAKE3-256、`uint256`、四个网络，以及地址（Schnorr 公钥、ECDSA 公钥、脚本哈希）。同时包含密钥、BIP-340 Schnorr 与紧凑 ECDSA 签名、脚本解析和标准 P2PK / P2SH 模板、版本 0 与 Toccata 版本 1 交易和 sighash、BIP39 助记词、BIP32 扩展密钥、交易构建器、Kaspa mass 与费率、wRPC JSON 与 Borsh 客户端、PSKT（部分签名的 Kaspa 交易），以及 `NKaspa.JsonConverters` 里的 System.Text.Json 转换器。PSKT 对齐 rusty-kaspa 的 `wallet/pskt`：角色 Creator、Constructor、Updater、Signer、Combiner、Finalizer、Extractor；Schnorr 与 ECDSA 部分签名；合并；单签 P2PK 与多签 P2SH 的终结；提取交易。`TransactionBuilder.BuildPSKT` 覆盖版本 0 和版本 1。十六进制是前缀 `PSKT` 加上紧凑 JSON 的小写十六进制。Bundle 用 `PSKB`。构建器可以强制花掉指定的币，在一枚币够付时只用一枚，并直接产出版本 1 付款。脚本引擎执行版本 0 脚本，包括 P2SH 赎回脚本。`Script.VerifyScript`、`Transaction.Verify` 和 `TransactionBuilder.Verify` 按 sigop 数量或版本 1 的 compute budget 检查每一个输入。`OP_ZKPRECOMPILE` 执行已定稿的标签、费用和结构检查。BN254 配对和 RISC0 STARK 仍返回未实现，不压入 true。`OP_CHAINBLOCKSEQCOMMIT` 需要调用方提供区块访问器。`WrpcSeqCommitAccessor` 可以用只读的 `getBlock` 和 `getBlockDagInfo` 回答，视点是 DAG sink，不是共识的 selected parent。`ConsensusSeqCommitAccessor` 用同样的只读接口沿 selected parent 链回推。调用开始时锁定仍在选定链上的 sink，默认再沿 selected parent 退 20 步作为快照，KIP-21 窗口按这个快照计算。之后的新 sink 只要 selected parent 链仍包含该快照，就视为链向前增长并返回快照结果。新尖端比快照超前超过 32 个 selected parent 时，不再逐块追赶，而是重读快照区块，只要 isChainBlock 仍为 true 就按增长返回。节点数据不全、不一致，或重组重试耗尽时抛出异常，不静默返回。轻客户端只能尽量接近共识。完全等同需要运行节点：回推有请求上限，也看不到节点里已经剪掉的可达性数据。本库自己不广播交易。`samples/` 里的控制台示例只打印交易，或只读查询节点。`samples/Spend` 只有在传入 `--broadcast` 且环境变量 `NKASPA_MNEMONIC` 已设置时才提交交易。JSON wRPC 用预制 JSON 和本地回环 WebSocket 做过测试。Borsh 客户端用手工计算的帧、假的二进制 WebSocket 和一条订阅通知做过测试。`GetSinkBlueScoreAsync` 读取 sink 的蓝分（`getSinkBlueScore`，Borsh 操作码 138）。`GetBlockDagInfo` 的 JSON 可以带 `blueScore`。Borsh 响应没有这个字段，所以线上节点在 JSON 没给出该字段时，`RpcBlockDagInfo.BlueScore` 是 0。区块头的 `blueWork` 是 24 字节小端整数（`BlueWorkType` / `Uint192`）。`ConsensusParams.IsCoinbaseMature` 按 rusty-kaspa 的 coinbase 成熟度（DAA 分数）判断。`WalletKeystore` 用 PBKDF2-HMAC-SHA256 和 AES-256-GCM 加密助记词或种子。Argon2 不在 .NET 基础类库里，所以没有额外的包。`samples/NodeQuery` 是对公共节点的只读调用。对照表见 [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md)。

### 用法

```csharp
using NKaspa;
using NKaspa.Policy;
using NKaspa.RPC;

var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
using var master = mnemonic.DeriveExtKey();
using var receive = master.Derive(KeyPath.KaspaReceive(0));
var address = receive.PrivateKey.GetAddress(Network.Mainnet);

var fee = FeeRate.MinimumRelay.GetFee(2_036);

// 低于 0.02 KAS 的找零会并进手续费。
// 更大的找零只有在输出建不出来，金额不超过 max(MinimumChange, MaxFoldedChange)（MaxFoldedChange 默认 0.1 KAS），
// 并且不超过无找零交易所需手续费的 MaxFoldedFeeFactor 倍（默认 100）时才并进手续费。
// MaxFoldedChange 和 MaxFoldedFeeFactor 是公开属性，调用方可以放宽。它们是钱包安全上限，不是共识规则。放宽之后，更多 sompi 会进手续费。
// 超过上限会抛异常，不把整笔找零烧掉。成功构建后 ChangeFoldedIntoFee 是从找零拿掉并加进手续费的 sompi。
// 找零保留或构建抛出时它是 0。它不是总手续费。TransactionMass 不重复这个数。
// 默认 mass 上限是 Toccata 之后的块限额（compute 与 storage 500000，transient 1000000）。
// `new TransactionBuilder(Network.Mainnet, toccataActive: false)` 保留旧的 100000 上限和 1 sompi/gram 中继费率。
// `new TransactionBuilder(network, virtualDaaScore)` 按该网络的激活分数选择。
// `Network.IsToccataActive(daaScore)` 就是这个比较。
var tx = new TransactionBuilder()
    .AddKeys(receive.PrivateKey)
    .AddCoins(coin)
    .Send(payee, Money.Coins(1))
    .SetChange(address)
    .SendEstimatedFees(FeeRate.MinimumRelay)
    .BuildTransaction(sign: true);

IKaspaRpcClient rpc = new KaspaWrpcJsonClient(channel);
var info = await rpc.GetServerInfoAsync();
```

```csharp
// 需要 0.1.11-alpha。
// Auto 先看路径，再看端口。路径优先。
// 以 /wrpc/borsh 结尾或包含 /borsh 的是 Borsh。以 /wrpc/json 结尾或包含 /json 的是 JSON。
// Borsh 端口是 17110、17210、17510、17610。JSON 端口是 18110、18210、18510、18610。
// 443 上的 wss，路径里没有 /borsh 或 /json 时抛异常。不会默认成某一种编码。
// gRPC 端口 16110、16210、16510、16610 会抛异常。本库说的是 wRPC，不是 gRPC。
// 传入 KaspaRpcEncoding.Borsh 或 Json 可以覆盖路径和 wRPC 端口，不能把 gRPC 端口变成 wRPC。
// JSON 客户端收到二进制帧，或 Borsh 客户端收到文本帧，抛 KaspaRpcEncodingMismatchException。
// KaspaResolver 用 rusty-kaspa 的解析器名单查找公共节点（GET /v2/kaspa/{network}/{tls|any}/wrpc/{json|borsh}），并可以打开那个套接字。
// 解析器全部失败后，主网备用是 wss://eva.kaspa.green，测试网 10 备用是 wss://vector-10.kaspa.green 和 wss://muon-10.kaspa.blue。这些节点不保证可用（not guaranteed to be available）。空名单关闭备用，自定义名单替换默认值。
// Create 不会连接。两种编码共用同一套超时、重连、16 MiB 上限、State 和 Reconnected。
await using var node = KaspaRpcClient.Create("ws://127.0.0.1:17110");
await node.ConnectAsync();
RpcServerInfo server = await node.GetServerInfoAsync();
RpcBlockDagInfo dag = await node.GetBlockDagInfoAsync();
// GetBlockDagInfoResponse has no blueScore. The sink blue score is this call.
ulong sinkBlueScore = await node.GetSinkBlueScoreAsync();
RpcFeeEstimate fees = await node.GetFeeEstimateAsync();
Money balance = await node.GetBalanceByAddressAsync(address.ToString());
IReadOnlyList<RpcUtxo> utxos = await node.GetUtxosByAddressesAsync(new[] { address.ToString() });
IReadOnlyList<RpcAddressBalance> balances = await node.GetBalancesByAddressesAsync(new[] { address.ToString() });
await using KaspaSubscription watch = await node.NotifyUtxosChangedAsync(
    new[] { address.ToString() },
    change => { });
// NotifyVirtualDaaScoreChangedAsync 和 NotifyBlockAddedAsync 返回同一种订阅。
// SubmitTransactionAsync 在 IKaspaRpcClient 上。这段示例不调用它。
```

`coin` 是 `OutPoint` 加上它花费的 `UtxoEntry`。`payee` 是 `KaspaAddress`。`channel` 是 `IKaspaJsonChannel`。`KaspaWebSocketChannel` 可以打开套接字。`KaspaWebSocketOptions` 设置 15 秒连接超时、30 秒请求超时、可选的自动重连，以及 `MaxMessageBytes`（默认 16 MiB）。更大的帧会让这次读取失败，缓冲区不会无限增长。`State` 是 Closed、Connecting、Open 或 Reconnecting。`Reconnected` 在后来的套接字打开后触发，第一次连接不触发。断开时，还在等待的请求会失败。测试用本地回环服务器，没有连过真实节点。`KeyPath.KaspaReceive(0)` 是 `m/44'/111111'/0'/0/0`。主网扩展密钥前缀是 `kprv` / `kpub`。测试网、模拟网和开发网是 `ktrv` / `ktub`。`ExtKey.ToString()` 返回 `NKaspa.ExtKey`。导出扩展私钥用 `ExportXprv()`、`ExportXprv(network)`、`GetWif(network)`、`ToString(network)` 或 `ToBase58()`。`Mnemonic.ToString()` 返回 `NKaspa.Mnemonic`。句子用 `ToPhrase()`。`Words` 是 `IReadOnlyList<string>`。`Parse` 和 `ParseValidated` 在校验和错误时抛 `FormatException`（`BIP39 checksum does not match.`）。宽松构造仍返回 `IsValidChecksum == false` 的对象。对这种对象调用 `DeriveSeed` 或 `DeriveExtKey` 会抛异常，除非 `allowInvalidChecksum` 为 true。大小写不敏感，以及去掉零宽字符（含词内标记和句首 BOM），是 NKaspa 相对 BIP39 的扩展，另外还有 NFKD。`ToPhrase()` 保存词表里的规范小写词，因此用这句规范词派生的种子与其他钱包相同。把含大写或全角字符的句子原样输入其他钱包，可能会失败。默认的 `JsonSerializer.Serialize` 拒绝 `Key`、`ExtKey` 和 `Mnemonic`。要写出秘密，用 `NKaspaJsonSerializer` 并传 `allowPrivateKeys: true`。`FeeRate` 的单位是 sompi/gram。

最低中继费 = 100 sompi × max(compute mass, 归一化 transient mass)。storage mass 不计入这个下限。标准单输入 Schnorr 付款（带一笔找零）是 2036 gram，即 203600 sompi，约 0.002 KAS。`SendEstimatedFees` 用费率乘以 fee mass，fee mass 含归一化后的 storage。`SendMinimumRelayFee` 只付中继下限。`MustSpend` 和 `SpendAllCoins` 让指定的每一枚币都进入交易，并且每笔输入单独签名。相同脚本的币不会被并成一笔输入。`SetVersion(1)` 是 Toccata 付款。每个输入的默认 compute budget 是 10。版本 1 的 wRPC JSON 同时写出 `sigOpCount` 和 `computeBudget`。

```csharp
var floor = TransactionBuilder.EstimateFee(FeeRate.MinimumRelay, MassCalculator.StandardSchnorrPaymentMass);

var forced = new TransactionBuilder()
    .AddKeys(receive.PrivateKey)
    .AddCoins(firstCoin, secondCoin)
    .MustSpend(firstCoin, secondCoin)
    .Send(payee, Money.Coins(0.2m))
    .SetChange(address)
    .SendMinimumRelayFee()
    .BuildTransaction(sign: true);

var toccata = new TransactionBuilder()
    .SetVersion(1)
    .AddKeys(receive.PrivateKey)
    .AddCoins(coin)
    .Send(payee, Money.Coins(0.1m))
    .SetChange(address)
    .SendMinimumRelayFee()
    .BuildTransaction(sign: true);
```

`firstCoin` 和 `secondCoin` 与 `coin` 一样，都是 `Coin`。`floor` 是 0.002036 KAS。

```csharp
var spent = new[] { coin.Utxo };
bool inputOk = Script.VerifyScript(tx.Inputs[0].SignatureScript, coin.Utxo.ScriptPublicKey, tx, 0, spent);
bool allOk = tx.Verify(spent);
```

`inputOk` 是单个输入。`allOk` 是全部输入。版本 0 每个输入可以花 `sigOpCount * 100000 + 9999` 个脚本单位。版本 1 可以花 `computeBudget * 10000 + 9999`。脚本长于 1000000 字节、栈元素长于 1000000 字节、数据栈和备用栈合计深于 244，或非推送操作码多于 1000000，引擎都会停下来。

```csharp
using NKaspa.PSKT;

var pskt = new TransactionBuilder()
    .AddKeys(receive.PrivateKey)
    .AddCoins(coin)
    .Send(payee, Money.Coins(1))
    .SetChange(address)
    .SendMinimumRelayFee()
    .BuildPSKT()
    .Sign(receive.PrivateKey);

var carried = PSKT.FromHex(pskt.ToHex());
var tx = carried.Finalizer().Finalize().ExtractTransaction();
```

`BuildPSKT` 给出的是签名者角色的 PSKT，未签名付款与 `BuildTransaction(false)` 相同。`Sign` 为这把密钥能花的每个输入写入 Schnorr 或 ECDSA 部分签名。`ToHex` 是 `PSKT` 加上紧凑 JSON。`FromHex` 读回来是合并者角色。`Finalize` 填上单签 P2PK 或 P2SH 多签赎回脚本，`ExtractTransaction` 把这些脚本抄到未签名交易上。2-of-2 先对两份 PSKT 调用 `Combine`，再 `Finalize`。Schnorr 的辅助随机数默认随机，除非给 `PSKT.Sign` 传入 32 字节。写在 `namespace NKaspa` 里面时要用 `NKaspa.PSKT.PSKT`，因为裸名 `PSKT` 是命名空间。版本 1 的 compute budget 只留在内存里的输入上，不进入 JSON。这条路径不广播。

`uint256` 的十六进制是 Kaspa 线上字节序（与 BLAKE2b 输出一致），不会像 `NBitcoin.uint256.ToString()` 那样把字节反转后再显示。数值比较仍然把第 0 字节当作最低有效字节。

### JSON

`NKaspa.JsonConverters.NKaspaJsonSerializer` 对标 `NBitcoin.JsonConverters.Serializer`。实现是 `System.Text.Json`（已经引用的 8.0.5，所以 `net8.0` 和 `netstandard2.1` 共用）。没有 Newtonsoft.Json 适配。

金额是 sompi 整数。Kaspa 值内部的字段名是驼峰。交易对象与 `KaspaRpcJson.WriteTransaction` 的紧凑 JSON 相同，包括版本 0 和版本 1（`sigOpCount`，版本 1 还有 `computeBudget`），输出可以带 `covenant`。`scriptPublicKey` 写成 `{ "version", "script" }`。读取还接受对象里的 `scriptPublicKey`，以及 rusty-kaspa 那种前两字节是大端版本的十六进制。`AddNKaspaConverters(network)` 注册转换器，地址和扩展密钥必须属于这个网络。

`Key` 和 `ExtKey` 默认抛出。只有 `allowPrivateKeys` 为 true 才写出私钥字节。不要对日志或 RPC 打开这个开关。

```csharp
using System.Text.Json;
using NKaspa.JsonConverters;

var json = NKaspaJsonSerializer.Serialize(tx, Network.Mainnet);
var copy = NKaspaJsonSerializer.Deserialize<Transaction>(json, Network.Mainnet);

var options = new JsonSerializerOptions();
options.AddNKaspaConverters(Network.Mainnet);
```

`tx` 是 `Transaction`。`copy` 是同一笔交易。`options` 给不经过 `NKaspaJsonSerializer` 的 `JsonSerializer` 调用使用。

### 示例

示例在 `samples/`，已加入 `NKaspa.sln`。`NodeQuery` 只读。其余示例离线运行。`samples/smoke.ps1` 会跑这些离线程序。它用 `PATH` 上的 `dotnet`；如果设置了 `DOTNET_ROOT`，就用那个目录里的 `dotnet.exe`。`SampleSmokeTests` 在 `dotnet test` 里做同样的检查。

```bash
dotnet run --project samples/Mnemonic
dotnet run --project samples/Mnemonic -- abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about
dotnet run --project samples/Address
dotnet run --project samples/NodeQuery
dotnet run --project samples/NodeQuery -- --url wss://eva.kaspa.green/kaspa/mainnet/wrpc/json --address kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva
dotnet run --project samples/Spend
dotnet run --project samples/PsktMultisig
dotnet run --project samples/SignMessage
dotnet run --project samples/SignMessage -- NKaspa
dotnet run --project samples/Json
dotnet run --project samples/Tutorials
powershell -File samples/smoke.ps1
```

`Mnemonic` 生成 12 个英文词，或使用传入的助记词，并派生 `m/44'/111111'/0'/0/0` 到 `2`。主网地址前缀是 `kaspa`，扩展公钥是 `kpub`。测试网是 `kaspatest` 和 `ktub`。`Address` 解析 Schnorr、ECDSA 和脚本哈希地址，并打印 `ScriptPubKey`。`NodeQuery` 打开 wRPC JSON WebSocket，然后调用 `getServerInfo`、`getBlockDagInfo`、`getBalanceByAddress`、`getUtxosByAddresses`。默认地址是 `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json`。它仍然只用 JSON。Borsh 客户端是上面的 `KaspaRpcClient.Create` 示例。`smoke.ps1` 不开套接字。`Json` 打印公开类型的紧凑 JSON，包括带 `computeBudget` 和 `covenant` 的版本 1 交易，并拒绝打印私钥。

`Spend` 用公开测试密钥和本地假币签三笔付款：版本 0、版本 0 的双输入 `MustSpend`、版本 1。费用都是 `SendMinimumRelayFee`。它打印交易十六进制和 wRPC JSON，不开套接字。

**警告：** `dotnet run --project samples/Spend -- --broadcast` 会提交交易。只有同时传入 `--broadcast` 并且设置了 `NKASPA_MNEMONIC` 才会提交。还要传入 `--to` 和 `--amount`。可选参数是 `--url`、`--network`、`--version`、`--must-spend`、`--from`、`--max-fee-sompi`。调用 `submitTransaction` 之前，示例会检查有一笔输出把 `--amount` 付给 `--to`、手续费是最低中继费，并且 `Transaction.Verify` 通过。设置了 `--from` 时，它必须等于派生出的收款地址。`--max-fee-sompi` 会拒绝更高的手续费。示例不读密钥目录。不打算花钱就不要设置那个变量。

`PsktMultisig` 是 2-of-2 Schnorr PSKT：创建、双方签名、合并、完成、提取。`SignMessage` 用 `PersonalMessage.SignMessage` 签 UTF-8 字符串。摘要是以 `PersonalMessageSigningHash` 为密钥的 BLAKE2b-256，签名是 BIP-340。它同时核对 Schnorr 公钥和 Schnorr 地址。这是 rusty-kaspa 的 `sign_message`，不是比特币的消息前缀，也不是 BIP322。`samples/Tutorials` 是两篇英文教程里的代码，只做离线检查。

### 构建

SDK 写在 `global.json`（8.0.427，`rollForward` 为 `latestFeature`）。

```bash
dotnet build NKaspa.sln
dotnet test NKaspa.sln
```

- `src/NKaspa`：类库，`net8.0` 与 `netstandard2.1`
- `tests/NKaspa.Tests`：xUnit，`net8.0`
- `samples/`：控制台示例，`net8.0`

包名 `NKaspa`。仓库：https://github.com/lifeme2027/NKaspa

### 许可证

[MIT](LICENSE)。Copyright (c) 2026 NKaspa contributors.

### 致谢

NKaspa 的 API 设计参考 [NBitcoin](https://github.com/MetacoSA/NBitcoin)。NBitcoin 采用 MIT 许可。Copyright (c) .NET Foundation and Contributors / Nicolas Dorier。

NKaspa 不包含 NBitcoin 源码树。椭圆曲线使用 `NBitcoin.Secp256k1` 包（MIT）。`src/NKaspa/BIP39/english.txt` 是随 NBitcoin 发布的 BIP39 英文词表，声明见 `src/NKaspa/BIP39/WORDLIST-LICENSE.txt`。详情见 [NOTICE](NOTICE)。
