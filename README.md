# NKaspa

[English](#english) · [中文](#中文)

## English

NKaspa is a .NET library for [Kaspa](https://kaspa.org/). Version **0.1.0-alpha**.

The public types follow [NBitcoin](https://github.com/MetacoSA/NBitcoin) where a Kaspa counterpart exists. `KaspaAddress` corresponds to `BitcoinAddress`. `uint256` corresponds to `NBitcoin.uint256`. `Encoders` corresponds to `NBitcoin.DataEncoders.Encoders`. The behavior is Kaspa's, not Bitcoin's: addresses use cashaddr-style bech32 (`kaspa`, `kaspatest`, `kaspasim`, `kaspadev`, 8-character checksum), and consensus hashes are unkeyed BLAKE2b-256.

NBitcoin is MIT licensed. See [NOTICE](NOTICE). Elliptic-curve operations use the `NBitcoin.Secp256k1` package. BLAKE2b is implemented in this repository from [RFC 7693](https://www.rfc-editor.org/rfc/rfc7693) because no small MIT package was selected for it.

### Status

Alpha. This release contains encoders (hex, Kaspa bech32, Base58Check), BLAKE2b-256, `uint256`, the four networks, and addresses (Schnorr public key, ECDSA public key, script hash). It also contains keys, BIP-340 Schnorr and compact ECDSA signatures, script parsing and the standard pay-to-pubkey / pay-to-script-hash templates, version-0 transactions and sighash, BIP39 mnemonics, BIP32 extended keys, a transaction builder, Kaspa mass and fee rates, and a minimal wRPC JSON client. The script engine does not execute scripts. Version-1 transaction ids use BLAKE3 and are not implemented. The wRPC client is covered by canned JSON tests. It has not been run against a live node. The map of NBitcoin areas is in [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md).

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

NKaspa 是 Kaspa 的 .NET 基础函数库，版本 **0.1.0-alpha**。

公开类型尽量与 [NBitcoin](https://github.com/MetacoSA/NBitcoin) 一一对应，但语义按 Kaspa。地址是 cashaddr 风格的 bech32（前缀 `kaspa` / `kaspatest` / `kaspasim` / `kaspadev`，8 字符校验和）。共识哈希是不带密钥的 BLAKE2b-256，不是比特币的双 SHA256。

NBitcoin 采用 MIT 许可，致谢见 [NOTICE](NOTICE)。椭圆曲线使用 NuGet 包 `NBitcoin.Secp256k1`（MIT）。BLAKE2b 按 RFC 7693 在本库实现。

### 状态

Alpha。这一版包含编码器（十六进制、Kaspa bech32、Base58Check）、BLAKE2b-256、`uint256`、四个网络，以及地址（Schnorr 公钥、ECDSA 公钥、脚本哈希）。同时包含密钥、BIP-340 Schnorr 与紧凑 ECDSA 签名、脚本解析和标准 P2PK / P2SH 模板、版本 0 交易和 sighash、BIP39 助记词、BIP32 扩展密钥、交易构建器、Kaspa mass 与费率，以及最小的 wRPC JSON 客户端。脚本只解析和构建，不执行。版本 1 交易号是 BLAKE3，这一版会抛 `NotSupportedException`。wRPC 只用预制 JSON 做过测试，没有连过真实节点。对照表见 [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md)。

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

`coin` 是 `OutPoint` 加上它花费的 `UtxoEntry`。`payee` 是 `KaspaAddress`。`channel` 是 `IKaspaJsonChannel`。`KaspaWebSocketChannel` 可以打开套接字，测试没有这么做。`KeyPath.KaspaReceive(0)` 是 `m/44'/111111'/0'/0/0`。主网扩展密钥前缀是 `kprv` / `kpub`。测试网、模拟网和开发网是 `ktrv` / `ktub`。`FeeRate` 的单位是 sompi/gram。

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
