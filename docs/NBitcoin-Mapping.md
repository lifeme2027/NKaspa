# NBitcoin 与 NKaspa 对照

对照的 NBitcoin 树是只读参考 `01NBitcoin/NBitcoin-master/NBitcoin`（本文件描述的是该树里的核心目录和主要类型）。NKaspa 的类型名尽量沿用对应名字，XML 注释里写 `Kaspa counterpart of NBitcoin.X`。行为按 Kaspa，不按比特币。

状态只用三种：

- **已实现**：这一版（0.1.0-alpha）里有对应类型，范围以代码为准。
- **计划中**：后面阶段要做，或只做 Kaspa 自己的等价物，不照搬比特币协议。
- **无需实现**：Kaspa 没有这套机制，或它不该出现在这个库里。

## 根上的主要类型

| NBitcoin | NKaspa | 状态 | 原因 |
| --- | --- | --- | --- |
| `BitcoinAddress` | `KaspaAddress` | 已实现 | 抽象地址。解析、校验、`ToString`、`ScriptPubKey`。 |
| `BitcoinPubKeyAddress` | `KaspaPubKeyEcdsaAddress` | 已实现 | ECDSA 角色。Kaspa 载荷是 33 字节压缩公钥（版本 `0x01`），不是 HASH160。 |
| `BitcoinScriptAddress` | `KaspaScriptHashAddress` | 已实现 | P2SH。哈希是 BLAKE2b-256（版本 `0x08`），不是 HASH160。 |
| `BitcoinWitPubKeyAddress` / `BitcoinSegwitAddress` | `KaspaPubKeyAddress` | 已实现（角色对应） | 默认公钥地址。Kaspa 没有 SegWit。载荷是 32 字节 Schnorr x-only（版本 `0x00`）。 |
| `BitcoinWitScriptAddress` | — | 无需实现 | 没有 witness v0 脚本程序。脚本哈希走 `KaspaScriptHashAddress`。 |
| `TaprootAddress` 及 Taproot 脚本树 | — | 无需实现 | Kaspa 的 Schnorr 不是比特币 BIP341 Taproot。 |
| `Key` / `PubKey` / `BitcoinSecret` | `Key` / `PubKey` / `PubKeyECDSA` | 已实现 | `PubKey` 是 32 字节 Schnorr x-only。33 字节压缩公钥是 `PubKeyECDSA`。没有比特币 WIF / `BitcoinSecret`。 |
| `Script` | `Script` / `ScriptPubKey` | 已实现 | 解析、构建、`ToString`、标准模板和签名脚本。不执行脚本。 |
| `Transaction` / `TxIn` / `TxOut` / `OutPoint` | 同名 | 已实现 | 版本 0 序列化、交易号和交易哈希。版本 1 的 BLAKE3 交易号未做。 |
| `TransactionBuilder` / `Coin` | `TransactionBuilder` / `Coin` | 已实现 | 精简构建器：选币、Schnorr P2PK 签名、找零、显式费用或按 mass 估价。`Coin` 是 `OutPoint` 加 `UtxoEntry`。版本 0。 |
| `TransactionSignature` / sighash | `SchnorrSignature` / `ECDSASignature` / `SigHashType` | 已实现 | Kaspa sighash。`SigHashType.Single` 是 `0x04`，比特币的 SINGLE 是 `0x03`。 |
| `uint256` | `uint256` | 已实现 | 可解析、比较、按 32 字节序列化。显示不反转字节，见下文。 |
| `uint160` / `uint128` | — | 无需实现 | 地址和交易哈希都是 32 字节。 |
| `Network` | `Network` | 已实现 | 精简为 Mainnet、Testnet、Simnet、Devnet 与地址前缀。 |
| `Block` / `BlockHeader` | — | 计划中 | Kaspa 区块是 GHOSTDAG 块 DAG，不是比特币单链头。字段要另定，不能照搬。 |
| `Money` | `Money` | 已实现 | 单位是 sompi。1 KAS = 100,000,000 sompi。协议金额是 u64。 |
| `FeeRate` | `FeeRate` | 已实现 | 单位是 sompi/gram。最低中继费率 100。比特币的 `Target`、工作量目标和 blue score 留在区块层，这一版没有。 |
| `BitcoinStream` | （内部读写） | 已实现 | 公开入口是 `Transaction.ToBytes` / `FromBytes`。长度是 u64，不是比特币 compact size。没有单独的公开流类型。 |

`uint256` 的约定：`ToBytes()` 与 `ToString()` 都是 Kaspa 线上顺序，也就是 BLAKE2b 摘要的原始字节，RPC 十六进制也是这个顺序。字节 0 是摘要的第一个字节。NBitcoin 的 `ToString()` 会把这 32 字节反转，让十六进制看起来像大端整数。数值比较把这 32 字节当成小端整数（字节 0 是最低有效字节），这和 Kaspa 把哈希当工作量整数比较的方向一致。

## DataEncoders

| NBitcoin | NKaspa | 状态 | 原因 |
| --- | --- | --- | --- |
| `DataEncoder` | `DataEncoder` | 已实现 | 字节与文本的基类。 |
| `Encoders` | `Encoders` | 已实现 | `Hex`、`KaspaBech32`、`Base58`、`Base58Check`。 |
| `HexEncoder` | `HexEncoder` | 已实现 | 小写十六进制。 |
| `Bech32Encoder` | `KaspaBech32Encoder` | 已实现 | Kaspa 用的是 cashaddr 风格，8 字符（40 位）校验和，字符表 `qpzry9x8gf2tvdw0s3jn54khce6mua7l`。不是 BIP-173，也不是 bech32m。 |
| `Base58Encoder` / `Base58CheckEncoder` | 同名 | 已实现 | 扩展密钥要 Base58Check。地址仍然不用 Base58。 |
| `Base32Encoder` / `Base64Encoder` / `ASCIIEncoder` | — | 无需实现 | 地址路径不需要。真要做通用编码时再加，不作为共识的一部分。 |

## Crypto

| NBitcoin | NKaspa | 状态 | 原因 |
| --- | --- | --- | --- |
| `Hashes` | `Hashes` | 已实现 | 共识哈希是 `Blake2b256`。另有 `SHA256` 和 `Hash160`，只给 BIP32 指纹和 Base58Check 用，不进入地址或交易号。 |
| （无同名类型） | `Blake2b` | 已实现 | RFC 7693。脚本哈希是不带密钥的 BLAKE2b-256。交易号、交易哈希和 sighash 是带域分离密钥的 BLAKE2b-256（`TransactionID`、`TransactionHash`、`TransactionSigningHash`）。 |
| `Hashes.DoubleSHA256` | — | 无需实现 | 不是 Kaspa 共识哈希。 |
| `Hashes.Hash160` / RIPEMD160 | `Hashes.Hash160`（`Ripemd160` 为内部类型） | 已实现 | 只用于 BIP32 父指纹：RIPEMD160(SHA256(33 字节压缩公钥)) 的前 4 字节。地址仍不用 HASH160。 |
| `SchnorrSignature` / `ECDSASignature` | 同名 | 已实现 | Schnorr 是 BIP-340 的 64 字节。ECDSA 是 64 字节紧凑 R\|\|S，不是 DER。sighash 字节放在签名脚本里。 |
| `DeterministicECDSA` | — | 已实现（无单独类型） | ECDSA 使用 `NBitcoin.Secp256k1` 的 RFC 6979，并规范化为 low-S。 |

## Secp256k1

NBitcoin 目录 `Secp256k1/` 含 `ECPrivKey`、`ECPubKey`、`ECXOnlyPubKey`、`SecpSchnorrSignature`、`SecpECDSASignature`，以及 MuSig、adaptor、recovery。

| 状态 | 原因 |
| --- | --- |
| 无需把源码搬进 NKaspa | 椭圆曲线用 NuGet 包 `NBitcoin.Secp256k1`（MIT，这一版引用 3.1.6）。`Key`、`PubKey`、`PubKeyECDSA` 包在这个包外面。 |
| MuSig / adaptor / recovery | 计划外，除非 Kaspa 钱包明确需要。 |

## BIP32

`ExtKey`、`ExtPubKey`、`KeyPath`、`HDFingerprint`。没有单独的 `RootedKeyPath`、`IHDKey`、`BitcoinExtKey`。按网络输出扩展密钥用 `ToString(Network)` / `ToBase58`。

| 状态 | 原因 |
| --- | --- |
| 已实现 | 主密钥是 HMAC-SHA512，密钥字符串是 BIP32 的 `Bitcoin seed`。子密钥沿用硬化与非硬化派生。指纹是 HASH160 的前 4 字节。 |

Kaspa 序列化前缀按 rusty-kaspa `wallet/bip32` 的 `Prefix`（`HdKeyVersions`）：

| 网络 | 私钥 | 公钥 |
| --- | --- | --- |
| Mainnet | `kprv` `0x038f2ef4` | `kpub` `0x038f332e` |
| Testnet、Simnet、Devnet | `ktrv` `0x03909e07` | `ktub` `0x0390a241` |

kaswallet 的 C# `NetworkKind` 给 devnet、simnet 另写过版本字节（devnet `0x038b3d80` / `0x038b41ba`，simnet `0x03904242` / `0x0390467d`）。rusty-kaspa 没有这两对，本库不用。

默认派生路径是 `m/44'/111111'/0'/{0,1}/i`。币种是 `111111`。`KeyPath.KaspaReceive` 是外部链 `0`，`KeyPath.KaspaChange` 是找零链 `1`。旧的 `m/44'/972` 不是默认路径。

解析也接受比特币 `xprv`/`xpub`、`yprv`/`ypub`、`zprv`/`zpub`、`tprv`/`tpub`，用来核对 BIP32 官方向量。`CreateFromSeed` 存的是 `kprv`。

## BIP39

`Mnemonic`、`Wordlist`、`Language`、`WordCount`。没有 `KDTable`：规范化用运行时的 NFKD。

| 状态 | 原因 |
| --- | --- |
| 已实现 | 英文词表 2048 词，内嵌资源。种子是 PBKDF2-HMAC-SHA512，2048 次，64 字节。口令盐是 `mnemonic` 加上 NFKD 口令。校验和错误仍可构造，`IsValidChecksum` 为 false，与 NBitcoin 相同。 |

英文词表从 NBitcoin 复制，许可声明在 `src/NKaspa/BIP39/WORDLIST-LICENSE.txt`（MIT，Copyright (c) 2023 Metaco SA）。词表文件本身只有单词。

## Protocol

`Node`、`Message`、`Payload`、`VersionPayload`、`TxPayload`、`BlockPayload`、`InventoryVector`、`NetworkAddress`、`VarInt`、`VarString`，以及 compact block、feefilter、wtxid relay、witness 相关载荷。

| 状态 | 原因 |
| --- | --- |
| 无需原样实现 | 这些是比特币 P2P 消息。Kaspa 节点协议不同，没有 witness、compact block 那一套比特币载荷。 |
| 计划中（另写） | 若以后做节点客户端，按 Kaspa P2P 写。这一版的客户端是 wRPC JSON，不是完整 P2P 节点。 |

交易里的长度是 u64 小端，写在 `Transaction` 的序列化里，不从比特币 `Protocol` 目录拷贝 `VarInt`。

## Policy

`StandardTransactionPolicy`、`MinerTransactionPolicy`、`ITransactionPolicy`。

| 状态 | 原因 |
| --- | --- |
| 无需原样实现 | 比特币标准脚本（P2PKH、P2WPKH、multisig 计数）对不上 Kaspa。 |
| 已实现（mass） | `MassCalculator`：版本 0 的 compute mass、KIP-13 transient mass、KIP-9 storage mass。费用 mass 是这三项归一化后的最大值。标准上限 `100_000`。币库交易这三项都是 0。版本 1 抛 `NotSupportedException`。脚本是否标准仍等脚本虚拟机。 |

## RPC

`RPCClient`、`RPCClient.Wallet`、`RestClient`，以及 `GetBlockRPCResponse`、`ListUnspentOptions`、`UnspentCoin` 等响应类型。

| 状态 | 原因 |
| --- | --- |
| 已实现（最小子集） | `IKaspaRpcClient`、`KaspaWrpcJsonClient`、`KaspaWebSocketChannel`。方法是 `getServerInfo`、`getBlockDagInfo`、`getUtxosByAddresses`、`getBalanceByAddress`、`submitTransaction`、`getFeeEstimate`。信封是 workflow-rpc：`id`、`method`、`params`。成功体在 `params`，读的时候也接受 `result`。错误是 `error.code` / `error.message`。不是 JSON-RPC 2.0。 |
| 未接真实节点 | 单元测试用内存里的 JSON 通道。`KaspaWebSocketChannel` 能发一帧文本，但没有 workflow-rpc 订阅握手，也没有对套接字或公共节点跑过。 |
| 费用估计的内层字段 | `priorityBucket`、`normalBuckets`、`lowBuckets`，以及 `feerate` / `estimatedSeconds`，对齐 kaswallet `WalletRestClient` 和公开的 Kaspa WASM `IFeeEstimate`。rusty-kaspa 里对应的 `feerate.rs` 路径没有取到（404），内层结构没有从那份 Rust 源码确认。 |
| PSBT 相关的 RPC 参数 | 无需实现。见 BIP174。Kaspa 的部分签名交易是 PSKT，还没做。 |

## Payment

`BitcoinUrlBuilder` 以及内部的 URI 辅助类型。

| 状态 | 原因 |
| --- | --- |
| 计划中 | 可以以后做 `kaspa:` 支付 URI。比特币 BIP21 的查询参数不能直接拿来用。 |

## BIP174 / BIP370

`PartiallySignedTransaction`、`PSBTInput`、`PSBTOutput`、`PSBTCoin`，以及 BIP370 的 PSBT v2。

| 状态 | 原因 |
| --- | --- |
| 无需实现 | Kaspa 没有原生 PSBT。交易构造走 `TransactionBuilder`。下一步若做未签名交易交换，做的是 PSKT（Kaspa 的部分签名交易），不移植 BIP174。 |

## BIP158

`GolombRiceFilter`、`BitStream`。

| 状态 | 原因 |
| --- | --- |
| 无需实现 | Kaspa 不用比特币 BIP158 的 Golomb-Rice 紧凑过滤器。 |

## OpenAsset

`ColoredTransaction`、`ColorMarker`、`AssetId`、`BitcoinColoredAddress` 等。

| 状态 | 原因 |
| --- | --- |
| 无需实现 | Open Assets 染色币是比特币上的协议，不适用于 Kaspa。 |

## 其他 NBitcoin 目录

| 目录 | 主要类型 | 状态 | 原因 |
| --- | --- | --- | --- |
| `BIP38` | `BitcoinEncryptedSecret` | 无需实现 | 比特币 Base58 加密私钥。Kaspa 不用这套格式。 |
| `BIP322` | `BIP322Signature` | 无需实现 | 比特币消息签名证明，绑定的是比特币地址和脚本。 |
| `BIP9Deployments` | 部署状态 | 无需实现 | 比特币软分叉版本位。Kaspa 的升级不走 BIP9。 |
| `JsonConverters` | 各 `JsonConverter` | 计划中 | wRPC 有自己的 `KaspaRpcJson`，不是通用 `JsonConverter`。通用转换器仍未做。 |
| `WalletPolicies` / miniscript | 策略描述 | 无需实现 | 比特币 miniscript 与输出描述符对不上当前 Kaspa 脚本。 |
| `BuilderExtensions` | `TransactionBuilder` | 已实现 | 构建能力在 `TransactionBuilder` 上，没有单独的扩展方法类。 |
| `Socks` | `SocksHelper` | 计划外 | 与共识无关。做节点连接时再考虑。 |
| `Logging` | `Logs` | 无需实现 | 库不绑定 NBitcoin 的日志门面。 |
| `BouncyCastle` | 摘要与大整数 | 无需实现 | 不把这套源码带进来。哈希用自带 BLAKE2b，曲线用 Secp256k1 包。 |

## 当前进度

0.1.0-alpha。第一阶段的编码器、BLAKE2b、`uint256`、网络和三种地址仍在。第二阶段的密钥、签名、脚本、版本 0 交易和 sighash 仍在。第三阶段补上 BIP39、BIP32、mass / 费率、交易构建器和最小 wRPC JSON 客户端。

- 解决方案：`NKaspa.sln`，类库 `src/NKaspa`（`net8.0`、`netstandard2.1`），测试 `tests/NKaspa.Tests`（xUnit，`net8.0`）。
- 密钥：`Key`、`PubKey`（32 字节 x-only）、`PubKeyECDSA`（33 字节压缩）。`GetAddress` 默认是 Schnorr 地址。
- 签名：`SchnorrSignature`、`ECDSASignature`、`SigHashType`（`All=0x01`、`None=0x02`、`Single=0x04`，anyone-can-pay 是 `0x80`）。
- 脚本：`OpcodeType`、`Op`、`Script`、`ScriptBuilder`。`ScriptPubKey` 仍带 u16 版本。标准模板是 Schnorr P2PK、ECDSA P2PK、P2SH。签名脚本是 66 字节的 `OP_DATA_65` 推送。不执行脚本。
- 交易：`OutPoint`、`TxIn`、`TxOut`、`Transaction`、`UtxoEntry`、`SubnetworkId`、`Money`。版本 0 的交易号用带密钥的 BLAKE2b，密钥是 `TransactionID`；交易哈希的密钥是 `TransactionHash`。长度前缀是 u64。
- Sighash：`SigHash.CalcSchnorrSignatureHash` 使用密钥 `TransactionSigningHash`。ECDSA 再套 `SHA256(SHA256("TransactionSigningHashECDSA") || schnorrHash)`。版本 1 的 sighash 会省略 sigop，并在每个输出后写一个表示“没有 covenant”的字节。版本 1 的交易号是 BLAKE3，这一版会抛 `NotSupportedException`。
- 和比特币的主要差别：`OP_CHECKSIG` 验的是 Schnorr；`OP_CHECKSIGECDSA` 是 `0xab`；`OP_BLAKE2B` 是 `0xaa`；CLTV 是 `0xb0`，CSV 是 `0xb1`。`OP_CAT` 等是启用的。`OP_LEFT`、`OP_RIGHT`、`OP_2MUL`、`OP_2DIV`、`OP_LSHIFT`、`OP_RSHIFT` 仍禁用。
- 对照向量来自 rusty-kaspa 的 `hashing/tx.rs`、`hashing/sighash.rs`、`crypto/hashes/src/hashers.rs`，以及 kaswallet 里同一组测试。BIP-340 的第 0 条签名向量用来核对 Schnorr。地址派生用 BIP-340 第 1 条公钥，对应已有的 `kaspa:qr0lr4ml...`。
- BIP39：Trezor `vectors.json` 的英文行（BIP39 写明这些种子的口令是 `TREZOR`），副本是 NBitcoin 的 `bip39_vectors.json`。另一组是 NBitcoin 的 `bip39_vectors.en.json`，口令含表意空格，用来核对 NFKD。
- BIP32：BIP-0032 的五组官方向量（含非法密钥）。Kaspa 前缀做了往返。没有找到外部钉死的「助记词 → kaspa 地址」字符串，测试只检查派生地址能通过 `KaspaAddress.IsValid`，以及账户公钥派生和私钥派生得到同一地址。
- Mass：rusty-kaspa `test_storage_mass` 经 kaswallet `MassCalculatorTests` 移植。另有三组 api.kaspa.org `/transactions/mass` 的金额样本（83333、10003、2017），不是上游单元测试。mass 尺寸含 32 字节 payload 哈希、不含 sigop 字节，所以空白交易是 94，不是 `ToBytes()` 的 62。
- 交易构建器：本地 Schnorr P2PK 端到端。找零低于 `MinimumChange` 时并进手续费。storage mass 会把费用 mass 顶过 `100_000` 时拒绝，不把找零悄悄丢掉。
- RPC：内存 JSON 样例。大整数用整数记号，不用 `double`（样本 `9007199254740993`）。

还没有做：脚本虚拟机、版本 1 交易的 BLAKE3 交易号、PSKT、示例程序，以及 NuGet 打包准备。通用 `JsonConverter` 也还没有。

## 下一步计划

- 脚本虚拟机。
- 版本 1 交易 BLAKE3。
- PSKT（Kaspa 的部分签名交易）。
- 示例程序与 NuGet 打包准备。
