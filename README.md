# NKaspa

[English](#english) · [中文](#中文)

## English

NKaspa is a .NET library for [Kaspa](https://kaspa.org/). Version **0.1.8-alpha**.

The public types follow [NBitcoin](https://github.com/MetacoSA/NBitcoin) where a Kaspa counterpart exists. `KaspaAddress` corresponds to `BitcoinAddress`. `uint256` corresponds to `NBitcoin.uint256`. `Encoders` corresponds to `NBitcoin.DataEncoders.Encoders`. The behavior is Kaspa's, not Bitcoin's: addresses use cashaddr-style bech32 (`kaspa`, `kaspatest`, `kaspasim`, `kaspadev`, 8-character checksum), and consensus hashes are unkeyed BLAKE2b-256.

NBitcoin is MIT licensed. See [NOTICE](NOTICE). Elliptic-curve operations use the `NBitcoin.Secp256k1` package. BLAKE2b is implemented in this repository from [RFC 7693](https://www.rfc-editor.org/rfc/rfc7693). BLAKE3 is implemented from the BLAKE3 specification. No extra hash package was added.

### Status

Alpha. This release contains encoders (hex, Kaspa bech32, Base58Check), BLAKE2b-256, BLAKE3-256, `uint256`, the four networks, and addresses (Schnorr public key, ECDSA public key, script hash). It also contains keys, BIP-340 Schnorr and compact ECDSA signatures, script parsing and the standard pay-to-pubkey / pay-to-script-hash templates, version-0 and Toccata version-1 transactions and sighash, BIP39 mnemonics, BIP32 extended keys, a transaction builder, Kaspa mass and fee rates, a minimal wRPC JSON client, PSKT (partially signed Kaspa transactions), and System.Text.Json converters in `NKaspa.JsonConverters`. PSKT follows rusty-kaspa `wallet/pskt`: the roles Creator, Constructor, Updater, Signer, Combiner, Finalizer, and Extractor; Schnorr and ECDSA partial signatures; combine; finalize for single-sig P2PK and multisig P2SH; and extract. `TransactionBuilder.BuildPSKT` covers version 0 and version 1. The hex form is the prefix `PSKT` plus lowercase hex of the compact JSON. A bundle uses `PSKB`. The builder can force every named coin into the transaction, prefer one coin when that coin covers the payment, and emit a version-1 payment. The script engine executes version-0 scripts, including pay-to-script-hash redeem scripts. `Script.VerifyScript`, `Transaction.Verify`, and `TransactionBuilder.Verify` check every input against its sigop count or version-1 compute budget. `OP_ZKPRECOMPILE` enforces the finalized tag, cost, and layout checks. The BN254 pairing and the RISC0 STARK stay unimplemented and do not push true. `OP_CHAINBLOCKSEQCOMMIT` needs a caller-supplied block accessor. `WrpcSeqCommitAccessor` answers from read-only `getBlock` and `getBlockDagInfo`, using the DAG sink as its point of view. `ConsensusSeqCommitAccessor` walks the selected-parent chain from those same read-only calls, checks the KIP-21 window against the selected parent, and throws if the node data is incomplete, inconsistent, or reorged. A light client can only approach consensus. Matching it exactly requires a running node: the walk stops at a request limit and cannot see pruned reachability. Nothing in this library broadcasts a transaction on its own. The console samples under `samples/` print transactions or query a node. `samples/Spend` submits a transaction only when `--broadcast` is set and the environment variable `NKASPA_MNEMONIC` is set. The wRPC client is covered by canned JSON and a loopback WebSocket. `samples/NodeQuery` is the read-only call to a public node. The map of NBitcoin areas is in [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md).

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
powershell -File samples/smoke.ps1
```

`Mnemonic` draws a 12-word English mnemonic, or uses the words you pass, and derives `m/44'/111111'/0'/0/0` through `2` on mainnet (`kaspa`, `kpub`) and testnet (`kaspatest`, `ktub`). `Address` parses Schnorr, ECDSA, and script-hash addresses and prints `ScriptPubKey`. `NodeQuery` opens the wRPC JSON WebSocket, then calls `getServerInfo`, `getBlockDagInfo`, `getBalanceByAddress`, and `getUtxosByAddresses`. The default URL is `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json`. `Json` prints compact JSON for the public types, including a version-1 transaction with `computeBudget` and `covenant`, and refuses to print a private key.

`Spend` builds three signed payments from public test keys and fake coins: version 0, version 0 with `MustSpend` on two inputs, and version 1. Each pays `SendMinimumRelayFee`. It prints the transaction hex and the wRPC JSON. It does not open a socket.

**Warning:** `dotnet run --project samples/Spend -- --broadcast` submits a transaction. It runs only when `--broadcast` is set and `NKASPA_MNEMONIC` is set. You also pass `--to` and `--amount`. Optional flags are `--url`, `--network`, `--version`, and `--must-spend`. The sample does not read a secrets directory. Do not set that variable unless you intend to spend.

`PsktMultisig` is a 2-of-2 Schnorr PSKT: create, both parties sign, combine, finalize, extract. `SignMessage` signs a UTF-8 string with Schnorr and ECDSA over its BLAKE2b-256 digest and verifies both. That is not Bitcoin's message-signing prefix and it is not BIP322.

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

Package id `NKaspa`. Repository placeholder: https://github.com/lifeme2027/NKaspa

### License

[MIT](LICENSE). Copyright (c) 2026 NKaspa contributors.

## 中文

NKaspa 是 Kaspa 的 .NET 基础函数库，版本 **0.1.8-alpha**。

公开类型尽量与 [NBitcoin](https://github.com/MetacoSA/NBitcoin) 一一对应，但语义按 Kaspa。地址是 cashaddr 风格的 bech32（前缀 `kaspa` / `kaspatest` / `kaspasim` / `kaspadev`，8 字符校验和）。共识哈希是不带密钥的 BLAKE2b-256，不是比特币的双 SHA256。

NBitcoin 采用 MIT 许可，致谢见 [NOTICE](NOTICE)。椭圆曲线使用 NuGet 包 `NBitcoin.Secp256k1`（MIT）。BLAKE2b 按 RFC 7693 在本库实现。BLAKE3 按规范在本库实现，没有额外的哈希包。

### 状态

Alpha。这一版包含编码器（十六进制、Kaspa bech32、Base58Check）、BLAKE2b-256、BLAKE3-256、`uint256`、四个网络，以及地址（Schnorr 公钥、ECDSA 公钥、脚本哈希）。同时包含密钥、BIP-340 Schnorr 与紧凑 ECDSA 签名、脚本解析和标准 P2PK / P2SH 模板、版本 0 与 Toccata 版本 1 交易和 sighash、BIP39 助记词、BIP32 扩展密钥、交易构建器、Kaspa mass 与费率、最小的 wRPC JSON 客户端、PSKT（部分签名的 Kaspa 交易），以及 `NKaspa.JsonConverters` 里的 System.Text.Json 转换器。PSKT 对齐 rusty-kaspa 的 `wallet/pskt`：角色 Creator、Constructor、Updater、Signer、Combiner、Finalizer、Extractor；Schnorr 与 ECDSA 部分签名；合并；单签 P2PK 与多签 P2SH 的终结；提取交易。`TransactionBuilder.BuildPSKT` 覆盖版本 0 和版本 1。十六进制是前缀 `PSKT` 加上紧凑 JSON 的小写十六进制。Bundle 用 `PSKB`。构建器可以强制花掉指定的币，在一枚币够付时只用一枚，并直接产出版本 1 付款。脚本引擎执行版本 0 脚本，包括 P2SH 赎回脚本。`Script.VerifyScript`、`Transaction.Verify` 和 `TransactionBuilder.Verify` 按 sigop 数量或版本 1 的 compute budget 检查每一个输入。`OP_ZKPRECOMPILE` 执行已定稿的标签、费用和结构检查。BN254 配对和 RISC0 STARK 仍返回未实现，不压入 true。`OP_CHAINBLOCKSEQCOMMIT` 需要调用方提供区块访问器。`WrpcSeqCommitAccessor` 可以用只读的 `getBlock` 和 `getBlockDagInfo` 回答，视点是 DAG sink，不是共识的 selected parent。`ConsensusSeqCommitAccessor` 用同样的只读接口沿 selected parent 链回推，按 KIP-21 窗口核对 selected parent 的蓝分，节点数据不全、不一致或发生重组时抛出异常，不静默返回。轻客户端只能尽量接近共识。完全等同需要运行节点：回推有请求上限，也看不到节点里已经剪掉的可达性数据。本库自己不广播交易。`samples/` 里的控制台示例只打印交易，或只读查询节点。`samples/Spend` 只有在传入 `--broadcast` 且环境变量 `NKASPA_MNEMONIC` 已设置时才提交交易。wRPC 用预制 JSON 和本地回环 WebSocket 做过测试。`samples/NodeQuery` 是对公共节点的只读调用。对照表见 [docs/NBitcoin-Mapping.md](docs/NBitcoin-Mapping.md)。

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
powershell -File samples/smoke.ps1
```

`Mnemonic` 生成 12 个英文词，或使用传入的助记词，并派生 `m/44'/111111'/0'/0/0` 到 `2`。主网地址前缀是 `kaspa`，扩展公钥是 `kpub`。测试网是 `kaspatest` 和 `ktub`。`Address` 解析 Schnorr、ECDSA 和脚本哈希地址，并打印 `ScriptPubKey`。`NodeQuery` 打开 wRPC JSON WebSocket，然后调用 `getServerInfo`、`getBlockDagInfo`、`getBalanceByAddress`、`getUtxosByAddresses`。默认地址是 `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json`。`Json` 打印公开类型的紧凑 JSON，包括带 `computeBudget` 和 `covenant` 的版本 1 交易，并拒绝打印私钥。

`Spend` 用公开测试密钥和本地假币签三笔付款：版本 0、版本 0 的双输入 `MustSpend`、版本 1。费用都是 `SendMinimumRelayFee`。它打印交易十六进制和 wRPC JSON，不开套接字。

**警告：** `dotnet run --project samples/Spend -- --broadcast` 会提交交易。只有同时传入 `--broadcast` 并且设置了 `NKASPA_MNEMONIC` 才会提交。还要传入 `--to` 和 `--amount`。可选参数是 `--url`、`--network`、`--version`、`--must-spend`。示例不读密钥目录。不打算花钱就不要设置那个变量。

`PsktMultisig` 是 2-of-2 Schnorr PSKT：创建、双方签名、合并、完成、提取。`SignMessage` 对 UTF-8 字符串做 BLAKE2b-256，再分别做 Schnorr 和 ECDSA 签名并验证。这不是比特币的消息签名前缀，也不是 BIP322。

### 构建

SDK 写在 `global.json`（8.0.427，`rollForward` 为 `latestFeature`）。

```bash
dotnet build NKaspa.sln
dotnet test NKaspa.sln
```

- `src/NKaspa`：类库，`net8.0` 与 `netstandard2.1`
- `tests/NKaspa.Tests`：xUnit，`net8.0`
- `samples/`：控制台示例，`net8.0`

包名 `NKaspa`。仓库地址暂为 https://github.com/lifeme2027/NKaspa

### 许可证

[MIT](LICENSE)。Copyright (c) 2026 NKaspa contributors.
