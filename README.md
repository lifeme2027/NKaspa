# NKaspa

[English](#english) · [中文](#中文)

## English

NKaspa is a .NET library for [Kaspa](https://kaspa.org/). Version **0.1.3-alpha**.

The public types follow [NBitcoin](https://github.com/MetacoSA/NBitcoin) where a Kaspa counterpart exists. `KaspaAddress` corresponds to `BitcoinAddress`. `uint256` corresponds to `NBitcoin.uint256`. `Encoders` corresponds to `NBitcoin.DataEncoders.Encoders`. The behavior is Kaspa's, not Bitcoin's: addresses use cashaddr-style bech32 (`kaspa`, `kaspatest`, `kaspasim`, `kaspadev`, 8-character checksum), and consensus hashes are unkeyed BLAKE2b-256.

NBitcoin is MIT licensed. See [NOTICE](NOTICE). Elliptic-curve operations use the `NBitcoin.Secp256k1` package. BLAKE2b is implemented in this repository from [RFC 7693](https://www.rfc-editor.org/rfc/rfc7693). BLAKE3 is implemented from the BLAKE3 specification. No extra hash package was added.

### Status

Alpha. This release contains encoders (hex, Kaspa bech32, Base58Check), BLAKE2b-256, BLAKE3-256, `uint256`, the four networks, and addresses (Schnorr public key, ECDSA public key, script hash). It also contains keys, BIP-340 Schnorr and compact ECDSA signatures, script parsing and the standard pay-to-pubkey / pay-to-script-hash templates, version-0 and Toccata version-1 transactions and sighash, BIP39 mnemonics, BIP32 extended keys, a transaction builder, Kaspa mass and fee rates, a minimal wRPC JSON client, and PSKT (partially signed Kaspa transactions). PSKT follows rusty-kaspa `wallet/pskt`: the roles Creator, Constructor, Updater, Signer, Combiner, Finalizer, and Extractor; Schnorr and ECDSA partial signatures; combine; finalize for single-sig P2PK and multisig P2SH; and extract. `TransactionBuilder.BuildPSKT` covers version 0 and version 1. The hex form is the prefix `PSKT` plus lowercase hex of the compact JSON. A bundle uses `PSKB`. The builder can force every named coin into the transaction, prefer one coin when that coin covers the payment, and emit a version-1 payment. The script engine does not execute scripts. Covenant and zero-knowledge execution are not implemented. Nothing in this library broadcasts a transaction. The wRPC client is covered by canned JSON and a loopback WebSocket. It has not been run against a live node. The map of NBitcoin areas is in [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md).

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

// Change below 0.02 KAS, or change that would push mass over the block limit, is added to the fee.
// The default mass ceiling is the post-Toccata block limit (compute and storage 500000, transient 1000000).
// Pass `new TransactionBuilder(Network.Mainnet, toccataActive: false)` to keep the old 100000 cap.
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

`coin` is an `OutPoint` plus the `UtxoEntry` it spends. `payee` is a `KaspaAddress`. `channel` is an `IKaspaJsonChannel`. `KaspaWebSocketChannel` can open a socket. The tests do not. `KeyPath.KaspaReceive(0)` is `m/44'/111111'/0'/0/0`. Extended keys on mainnet use the `kprv` / `kpub` prefix. Testnet, simnet, and devnet use `ktrv` / `ktub`. `FeeRate` is sompi per gram of mass.

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

### Build

The repository pins the .NET SDK in `global.json` (8.0.427, roll forward to the latest feature band).

```bash
dotnet build NKaspa.sln
dotnet test NKaspa.sln
```

Projects:

- `src/NKaspa` — class library, `net8.0` and `netstandard2.1`
- `tests/NKaspa.Tests` — xUnit, `net8.0`

Package id `NKaspa`. Repository placeholder: https://github.com/lifeme2027/NKaspa

### License

[MIT](LICENSE). Copyright (c) 2026 NKaspa contributors.

## 中文

NKaspa 是 Kaspa 的 .NET 基础函数库，版本 **0.1.3-alpha**。

公开类型尽量与 [NBitcoin](https://github.com/MetacoSA/NBitcoin) 一一对应，但语义按 Kaspa。地址是 cashaddr 风格的 bech32（前缀 `kaspa` / `kaspatest` / `kaspasim` / `kaspadev`，8 字符校验和）。共识哈希是不带密钥的 BLAKE2b-256，不是比特币的双 SHA256。

NBitcoin 采用 MIT 许可，致谢见 [NOTICE](NOTICE)。椭圆曲线使用 NuGet 包 `NBitcoin.Secp256k1`（MIT）。BLAKE2b 按 RFC 7693 在本库实现。BLAKE3 按规范在本库实现，没有额外的哈希包。

### 状态

Alpha。这一版包含编码器（十六进制、Kaspa bech32、Base58Check）、BLAKE2b-256、BLAKE3-256、`uint256`、四个网络，以及地址（Schnorr 公钥、ECDSA 公钥、脚本哈希）。同时包含密钥、BIP-340 Schnorr 与紧凑 ECDSA 签名、脚本解析和标准 P2PK / P2SH 模板、版本 0 与 Toccata 版本 1 交易和 sighash、BIP39 助记词、BIP32 扩展密钥、交易构建器、Kaspa mass 与费率、最小的 wRPC JSON 客户端，以及 PSKT（部分签名的 Kaspa 交易）。PSKT 对齐 rusty-kaspa 的 `wallet/pskt`：角色 Creator、Constructor、Updater、Signer、Combiner、Finalizer、Extractor；Schnorr 与 ECDSA 部分签名；合并；单签 P2PK 与多签 P2SH 的终结；提取交易。`TransactionBuilder.BuildPSKT` 覆盖版本 0 和版本 1。十六进制是前缀 `PSKT` 加上紧凑 JSON 的小写十六进制。Bundle 用 `PSKB`。构建器可以强制花掉指定的币，在一枚币够付时只用一枚，并直接产出版本 1 付款。脚本只解析和构建，不执行。Covenant 与零知识证明也不执行。本库不广播交易。wRPC 用预制 JSON 和本地回环 WebSocket 做过测试，没有连过真实节点。对照表见 [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md)。

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

// 低于 0.02 KAS 的找零，或会把 mass 顶过块限额的找零，会并进手续费。
// 默认 mass 上限是 Toccata 之后的块限额（compute 与 storage 500000，transient 1000000）。
// `new TransactionBuilder(Network.Mainnet, toccataActive: false)` 保留旧的 100000 上限。
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

`coin` 是 `OutPoint` 加上它花费的 `UtxoEntry`。`payee` 是 `KaspaAddress`。`channel` 是 `IKaspaJsonChannel`。`KaspaWebSocketChannel` 可以打开套接字。测试用本地回环服务器，没有连过真实节点。`KeyPath.KaspaReceive(0)` 是 `m/44'/111111'/0'/0/0`。主网扩展密钥前缀是 `kprv` / `kpub`。测试网、模拟网和开发网是 `ktrv` / `ktub`。`FeeRate` 的单位是 sompi/gram。

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

### 构建

SDK 写在 `global.json`（8.0.427，`rollForward` 为 `latestFeature`）。

```bash
dotnet build NKaspa.sln
dotnet test NKaspa.sln
```

- `src/NKaspa`：类库，`net8.0` 与 `netstandard2.1`
- `tests/NKaspa.Tests`：xUnit，`net8.0`

包名 `NKaspa`。仓库地址暂为 https://github.com/lifeme2027/NKaspa

### 许可证

[MIT](LICENSE)。Copyright (c) 2026 NKaspa contributors.
