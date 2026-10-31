# NBitcoin 与 NKaspa 对照

对照的 NBitcoin 树是只读参考 `01NBitcoin/NBitcoin-master/NBitcoin`（本文件描述的是该树里的核心目录和主要类型）。NKaspa 的类型名尽量沿用对应名字，XML 注释里写 `Kaspa counterpart of NBitcoin.X`。行为按 Kaspa，不按比特币。

状态只用三种：

- **已实现**：这一版（0.1.8-alpha）里有对应类型，范围以代码为准。
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
| `Script` | `Script` / `ScriptPubKey` | 已实现 | 解析、构建、`ToString`、标准模板和签名脚本。`Script.VerifyScript` 执行版本 0 脚本，包括 P2SH 赎回脚本。 |
| `ScriptEvaluationContext` | `ScriptEvaluationContext` / `TxScriptEngine` | 已实现 | 对标 NBitcoin 的执行上下文，规则对齐 rusty-kaspa `crypto/txscript` 的 `TxScriptEngine`。版本 0 预算是 `sigOpCount * 100000 + 9999`。版本 1 预算是 `computeBudget * 10000 + 9999`。脚本最长 1000000 字节，元素最长 1000000 字节，数据栈加备用栈最深 244，非推送操作码最多 1000000。`OP_ZKPRECOMPILE` 执行已定稿的标签、脚本单位、字段长度、BN254 标量范围、Groth16 元数与 gamma 计量，并且 RISC0 只接受 Poseidon2。BN254 配对、曲线点检查和 RISC0 STARK 仍返回未实现，不压入 true。没有 `ISeqCommitAccessor` 时 `OP_CHAINBLOCKSEQCOMMIT` 是非法操作码。`WrpcSeqCommitAccessor` 只读 `getBlock` 与 `getBlockDagInfo`，视点是 DAG sink，不是共识的 selected parent。`ConsensusSeqCommitAccessor` 沿 selected parent 链回推并做一致性核对；数据不全、不一致或重组时抛异常。轻客户端只能尽量接近共识，完全等同需要运行节点。 |
| `Transaction` / `TxIn` / `TxOut` / `OutPoint` | 同名 | 已实现 | 版本 0 与 Toccata 版本 1 的序列化、交易号和交易哈希。版本 1 交易号是 BLAKE3。`Transaction.Verify` 对每个输入跑脚本引擎。已确定规则的内省和 covenant 操作码会执行。 |
| `TransactionBuilder` / `Coin` | `TransactionBuilder` / `Coin` | 已实现 | 精简构建器：选币、Schnorr 与 ECDSA P2PK 签名、找零、显式费用、按费率估价，或只付最低中继费。`Verify` 检查每一个输入的脚本。`Coin` 是 `OutPoint` 加 `UtxoEntry`。默认版本 0。`SetVersion(1)` 产出版本 1，默认 compute budget 是 10。`MustSpend` / `SpendAllCoins` 强制每一枚指定的币进入交易并单独签名。 |
| `TransactionSignature` / sighash | `SchnorrSignature` / `ECDSASignature` / `SigHashType` | 已实现 | Kaspa sighash。`SigHashType.Single` 是 `0x04`，比特币的 SINGLE 是 `0x03`。 |
| `uint256` | `uint256` | 已实现 | 可解析、比较、按 32 字节序列化。显示不反转字节，见下文。 |
| `uint160` / `uint128` | — | 无需实现 | 地址和交易哈希都是 32 字节。 |
| `Network` | `Network` | 已实现 | 精简为 Mainnet、Testnet、Simnet、Devnet 与地址前缀。 |
| `Block` / `BlockHeader` | — | 计划中 | Kaspa 区块是 GHOSTDAG 块 DAG，不是比特币单链头。字段要另定，不能照搬。 |
| `Money` | `Money` | 已实现 | 单位是 sompi。1 KAS = 100,000,000 sompi。协议金额是 u64。 |
| `FeeRate` | `FeeRate` | 已实现 | 单位是 sompi/gram。最低中继费 = 100 sompi × max(compute mass, 归一化 transient mass)。storage 不计入。标准单输入 Schnorr 付款约 0.002 KAS（2036 gram，203600 sompi）。`GetFee` 按费率乘 gram。`MassCalculator.EstimateFee` 同样按费率，并拒绝低于 100 的费率。`EstimateMinimumRelayFee` 按上面的 mass 公式估价。比特币的 `Target`、工作量目标和 blue score 留在区块层，这一版没有。 |
| `DefaultCoinSelector` | `DefaultCoinSelector` | 已实现 | 选币。不按 `scriptPubKey` 把多枚币并成一组。一枚币够付付款、手续费和最小找零时，只用最小的那一枚。只够付款和手续费、找零会低于最小值时，仍只用这一枚，找零并进手续费。没有任何一枚够付时才合并，并且不再把用不到的小币拉进来。NBitcoin 在多枚组合更接近目标时可能改花多枚；这里只要有一枚够付就不用多枚。 |
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
| 已实现（mass） | `MassCalculator`：版本 0 与版本 1 的 compute mass、KIP-13 transient mass、KIP-9 storage mass。费用 mass 是这三项归一化后的最大值。块限额是 compute 500000、storage 500000、transient 1000000。旧的单笔 `100_000` 仍保留为可选策略。币库交易这三项都是 0。脚本执行在 `TxScriptEngine`。标准性策略仍是已有的模板和 mass，没有单独的 `StandardTransactionPolicy`。 |

## RPC

`RPCClient`、`RPCClient.Wallet`、`RestClient`，以及 `GetBlockRPCResponse`、`ListUnspentOptions`、`UnspentCoin` 等响应类型。

| 状态 | 原因 |
| --- | --- |
| 已实现（最小子集） | `IKaspaRpcClient`、`KaspaWrpcJsonClient`、`KaspaWebSocketChannel`。方法是 `getServerInfo`、`getBlockDagInfo`、`getUtxosByAddresses`、`getBalanceByAddress`、`submitTransaction`、`getFeeEstimate`。信封是 workflow-rpc：`id`、`method`、`params`。成功体在 `params`，读的时候也接受 `result`。错误是 `error.code` / `error.message`。不是 JSON-RPC 2.0。 |
| 单元测试不连节点 | 单元测试用内存里的 JSON 通道和本地回环 WebSocket。`samples/NodeQuery` 在 2026-10-30 对 `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json` 做过一次只读查询：`getServerInfo` 返回 rusty-kaspa 2.1.0、mainnet、已同步、有 UTXO 索引；`getBlockDagInfo`、`getBalanceByAddress`、`getUtxosByAddresses` 都有响应。没有调用 `submitTransaction`。 |
| 费用估计的内层字段 | `priorityBucket`、`normalBuckets`、`lowBuckets`，以及 `feerate` / `estimatedSeconds`，对齐 kaswallet `WalletRestClient` 和公开的 Kaspa WASM `IFeeEstimate`。rusty-kaspa 里对应的 `feerate.rs` 路径没有取到（404），内层结构没有从那份 Rust 源码确认。 |
| PSBT 相关的 RPC 参数 | 无需实现。见 BIP174。PSKT 在库内交换，不通过这些 RPC 参数提交或广播。 |

## Payment

`BitcoinUrlBuilder` 以及内部的 URI 辅助类型。

| 状态 | 原因 |
| --- | --- |
| 计划中 | 可以以后做 `kaspa:` 支付 URI。比特币 BIP21 的查询参数不能直接拿来用。 |

## BIP174 / BIP370

`PartiallySignedTransaction`、`PSBTInput`、`PSBTOutput`、`PSBTCoin`，以及 BIP370 的 PSBT v2。

| 状态 | 原因 |
| --- | --- |
| 无需实现（BIP174） | Kaspa 没有原生 PSBT，不移植 `PartiallySignedTransaction`。 |
| 已实现（PSKT） | `NKaspa.PSKT` 对齐 rusty-kaspa `wallet/pskt`。类型是 `PSKT`、`PSKTInput`、`PSKTOutput`、`PSKTBundle`。角色是 Creator、Constructor、Updater、Signer、Combiner、Finalizer、Extractor，角色本身不进 JSON。传输是紧凑 JSON，再加前缀 `PSKT` 的小写十六进制；Bundle 的 JSON 是数组，前缀 `PSKB`。Schnorr 部分签名的 JSON 键是 `schnorr`（64 字节）。ECDSA 的键是 `eCDSA`（DER），脚本里仍用 64 字节紧凑形式。`Finalize` 覆盖单签 Schnorr P2PK、ECDSA P2PK，以及 P2SH 赎回脚本上的 m-of-n（1 到 16）。`TransactionBuilder.BuildPSKT` 和 `Sign` 覆盖版本 0 与版本 1。不广播。 |

空 PSKT 的 JSON 按 serde 字段顺序钉死。rusty-kaspa 的 pskt 测试和多签示例用随机密钥并打印结果，没有钉死的十六进制向量。本库没有启动 rusty-kaspa 进程做字节对照。xpub 写成比特币 `xpub` 前缀（`ExtendedPublicKey::to_string(None)`），不是 Kaspa `kpub`。多个公钥的 map 按未压缩 X||Y 排序。多个 xpub 按 base58 字符串排序，可能和 rust 的 `Ord` 不完全一致，没有多 xpub 的上游向量可对。版本 1 的 compute budget 只留在内存里，不进 JSON；rusty 的 `unsigned_tx` 对 compute budget 仍是 TODO，版本 1 的 sighash 也不写入这笔预算。`ExtractTransaction` 写的是 KIP-9 storage mass，这一步本身不执行脚本，也不取 storage、compute、transient 的最大值。提出来的交易用 `Transaction.Verify` 检查。

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
| `JsonConverters` | `NKaspa.JsonConverters` | 已实现 | `NKaspaJsonSerializer` 对标 `Serializer`，用 System.Text.Json，不用 Newtonsoft。见下文「JsonConverters」。 |
| `WalletPolicies` / miniscript | 策略描述 | 无需实现 | 比特币 miniscript 与输出描述符对不上当前 Kaspa 脚本。 |
| `BuilderExtensions` | `TransactionBuilder` | 已实现 | 构建能力在 `TransactionBuilder` 上，没有单独的扩展方法类。强制花费是 `MustSpend` 和 `SpendAllCoins`，不是 `SendAll` 那种把金额扫进一个输出。未签名交换走 `BuildPSKT` 和 `Sign`。 |
| `Socks` | `SocksHelper` | 计划外 | 与共识无关。做节点连接时再考虑。 |
| `Logging` | `Logs` | 无需实现 | 库不绑定 NBitcoin 的日志门面。 |
| `BouncyCastle` | 摘要与大整数 | 无需实现 | 不把这套源码带进来。哈希用自带 BLAKE2b，曲线用 Secp256k1 包。 |

## JsonConverters

`NKaspa.JsonConverters` 对标 `NBitcoin.JsonConverters`。NBitcoin 的 `Serializer` 用 Newtonsoft.Json。NKaspa 的 `NKaspaJsonSerializer` 用 System.Text.Json，因为 `net8.0` 和 `netstandard2.1` 都能用已经引用的 System.Text.Json 8.0.5。没有再加 Newtonsoft 包，也没有 Newtonsoft 适配层。

`AddNKaspaConverters(network)` 把转换器加进 `JsonSerializerOptions`。调用前如果命名策略还是空的，会设成驼峰。地址、扩展公钥、以及显式打开之后的扩展私钥，都按这个网络的前缀读写。

| 类型 | JSON |
| --- | --- |
| `Money` | sompi 整数。数字或数字字符串。不接受 `1 KAS` 这种小数。 |
| `uint256` | 64 位十六进制，Kaspa 线上字节序。可选 `0x`。 |
| `KaspaAddress` | 地址字符串。前缀必须属于 options 里的网络。 |
| `Network` | `mainnet` / `testnet` / `simnet` / `devnet`。也接受 `testnet-10` 和 `{networkType, suffix}`。 |
| `Script` | 脚本字节的十六进制，与 `signatureScript` 相同。 |
| `ScriptPubKey` | 写出 `{version, script}`。读入还接受对象里的 `scriptPublicKey`，以及版本在前的十六进制。 |
| `OutPoint` | `{transactionId, index}`。读入也接受 `transactionId:index`。 |
| `TxIn` / `TxOut` / `Transaction` | 与 `KaspaRpcJson` 相同。版本 1 的交易写出 `computeBudget`。单独的 `TxIn` 只在有预算时写出。输出可带 `covenant`。 |
| `UtxoEntry` | `{amount, scriptPublicKey, blockDaaScore, isCoinbase}`。有 covenant id 时写出 `covenantId`。 |
| `Coin` | `{outpoint, utxoEntry}`。与 `getUtxosByAddresses` 的条目一致。地址字段被忽略。 |
| `PubKey` / `PubKeyECDSA` | 32 字节 x-only，或 33 字节压缩公钥，小写十六进制。 |
| `ExtPubKey` | 该网络的 `kpub` 或 `ktub`。 |
| `FeeRate` | sompi/gram 的整数。 |
| `PSKT` | `PSKT.ToJson()` 的紧凑对象。 |
| `Key` / `ExtKey` | 默认拒绝，异常文本带 WARNING。`allowPrivateKeys: true` 才写出十六进制私钥或 `kprv` / `ktrv`。 |

紧凑、不缩进的交易 JSON 与 `KaspaRpcJson.WriteTransaction` 是同一串。2026-10-30 主网三笔已接受交易（`5fd8f860…`、`2febe6ca…`、`4d1c6a5e…`）按 wRPC 形状读入后，交易号与链上一致。浏览器的 `mass` 是中继 mass，没有写进 `storageMass`。

## 当前进度

0.1.8-alpha。这一版加上可选的 `ConsensusSeqCommitAccessor`：用只读的 `getBlock` 与 `getBlockDagInfo` 沿 selected parent 链回推，确定共识使用的区块和 seq commitment，并核对 selected parent、蓝分和 `isChainBlock`。数据不全、不一致或重组时抛异常，不静默当成剪枝或过深。默认请求上限是 64。`WrpcSeqCommitAccessor` 保持 DAG sink 视点，行为不变。轻客户端只能尽量接近共识，完全等同需要运行节点。0.1.7-alpha 补上 `OP_ZKPRECOMPILE` 已定稿的结构检查、内省与 covenant 构造助手、示例脚本，以及只读的 `WrpcSeqCommitAccessor`。0.1.6 起执行脚本。0.1.5 的通用 JSON 转换器还在。0.1.4 的行为还在：`samples/` 里的控制台示例。第一阶段的编码器、BLAKE2b、`uint256`、网络和三种地址仍在。第二阶段的密钥、签名、脚本、版本 0 交易和 sighash 仍在。第三阶段补上 BIP39、BIP32、mass / 费率、交易构建器和最小 wRPC JSON 客户端。0.1.1 补上 Toccata 版本 1 交易号、找零与 mass 策略，以及 wRPC 帧分发。0.1.2 让构建器强制花掉指定输入、在一枚币够付时不拉小币，并直接产出版本 1 付款。0.1.3 加上 PSKT：部分签名、合并、单签与 P2SH 多签终结、提取，以及 `TransactionBuilder.BuildPSKT`。

- 解决方案：`NKaspa.sln`，类库 `src/NKaspa`（`net8.0`、`netstandard2.1`），测试 `tests/NKaspa.Tests`（xUnit，`net8.0`），示例 `samples/`（七个控制台项目，`net8.0`）。对照见下文「示例」。
- 密钥：`Key`、`PubKey`（32 字节 x-only）、`PubKeyECDSA`（33 字节压缩）。`GetAddress` 默认是 Schnorr 地址。`Key.Create` 会清零临时缓冲区。`ExtKey.PrivateKey` 返回副本，处置副本不会让父密钥失效。
- 签名：`SchnorrSignature`、`ECDSASignature`、`SigHashType`（`All=0x01`、`None=0x02`、`Single=0x04`，anyone-can-pay 是 `0x80`）。
- 脚本：`OpcodeType`、`Op`、`Script`、`ScriptBuilder`、`ScriptEvaluationContext`、`TxScriptEngine`。`ScriptPubKey` 仍带 u16 版本。标准模板是 Schnorr P2PK、ECDSA P2PK、P2SH。签名脚本是 66 字节的 `OP_DATA_65` 推送。版本 0 脚本会执行，P2SH 会再跑赎回脚本。推送、流程控制、栈、位运算、算术、Schnorr 与 ECDSA 的 `OP_CHECKSIG` / `OP_CHECKMULTISIG`、`OP_BLAKE2B`、`OP_BLAKE3`、`OP_SHA256`、`OP_CHECKLOCKTIMEVERIFY`、`OP_CHECKSEQUENCEVERIFY`、内省操作码 `0xb2`–`0xc9`、covenant 操作码 `0xcb`–`0xd6`、`OP_NUM2BIN`、`OP_BIN2NUM`、`OP_CHECKSIGFROMSTACK` 都已实现。`OP_ZKPRECOMPILE` 执行已定稿的结构检查；BN254 配对、曲线点检查和 RISC0 STARK 仍返回未实现，不压入 true。`ScriptBuilder` 给 `0xb2`–`0xc9` 和 `0xcb`–`0xd6` 提供构造助手。`CovenantScripts` 里有三条例子，引擎可以执行，不广播。没有 `ISeqCommitAccessor` 时 `OP_CHAINBLOCKSEQCOMMIT` 是非法操作码。数字是小端，符号在末字节高位，算术宽度最多 8 字节。
- 交易：`OutPoint`、`TxIn`、`TxOut`、`Transaction`、`UtxoEntry`、`SubnetworkId`、`Money`、`CovenantBinding`。版本 0 的交易号用带密钥的 BLAKE2b，密钥是 `TransactionID`；交易哈希的密钥是 `TransactionHash`。版本 1 的交易号是 BLAKE3，域是 `PayloadDigest`、`TransactionRest`、`TransactionV1Id`。长度前缀是 u64。
- Sighash：`SigHash.CalcSchnorrSignatureHash` 使用密钥 `TransactionSigningHash`。ECDSA 再套 `SHA256(SHA256("TransactionSigningHashECDSA") || schnorrHash)`。版本 1 省略 sigop。有 covenant 时写入存在标志、授权输入和 covenant id；没有 covenant 时写 0。
- 和比特币的主要差别：`OP_CHECKSIG` 验的是 Schnorr；`OP_CHECKSIGECDSA` 是 `0xab`；`OP_BLAKE2B` 是 `0xaa`；CLTV 是 `0xb0`，CSV 是 `0xb1`。`OP_CAT` 等是启用的。`OP_LEFT`、`OP_RIGHT`、`OP_2MUL`、`OP_2DIV`、`OP_LSHIFT`、`OP_RSHIFT` 仍禁用。
- 对照向量来自 rusty-kaspa 的 `hashing/tx.rs`、`hashing/sighash.rs`、`crypto/hashes/src/hashers.rs`，以及 kaswallet 里同一组测试。BLAKE3 用官方 `test_vectors.json`。BIP-340 的第 0 条签名向量用来核对 Schnorr。地址派生用 BIP-340 第 1 条公钥，对应已有的 `kaspa:qr0lr4ml...`。
- BIP39：Trezor `vectors.json` 的英文行（BIP39 写明这些种子的口令是 `TREZOR`），副本是 NBitcoin 的 `bip39_vectors.json`。另一组是 NBitcoin 的 `bip39_vectors.en.json`，口令含表意空格，用来核对 NFKD。
- BIP32：BIP-0032 的五组官方向量（含非法密钥）。Kaspa 前缀做了往返。没有找到外部钉死的「助记词 → kaspa 地址」字符串，测试只检查派生地址能通过 `KaspaAddress.IsValid`，以及账户公钥派生和私钥派生得到同一地址。
- Mass：rusty-kaspa `test_storage_mass` 经 kaswallet `MassCalculatorTests` 移植。另有三组 api.kaspa.org `/transactions/mass` 的金额样本（83333、10003、2017），不是上游单元测试。mass 尺寸含 32 字节 payload 哈希、不含 sigop 字节，也不含 covenant 存在字节。空白交易是 94，不是 `ToBytes()` 的 62。版本 1 每个输入再加 2 字节 compute budget；有 covenant 时输出再加 34 字节。
- 交易构建器：本地 Schnorr 与 ECDSA P2PK 端到端。默认 `MinimumChange` 是 0.02 KAS。找零低于这个数，或找零会让 mass 超限时，并进手续费。默认费用 mass 上限是块的 compute 限额 500000。`toccataActive: false` 恢复 100000。显式手续费低于最低中继费会拒绝，除非 `AllowBelowMinimumRelayFee`。最低中继费 = 100 sompi × max(compute mass, 归一化 transient mass)，不含 storage。标准单输入是 2036 gram，203600 sompi，约 0.002 KAS。`SendEstimatedFees` 按费率乘 fee mass（三项里最大的那个）。`SendMinimumRelayFee` 只付中继下限。`EstimateFee` 与 `EstimateMinimumRelayFee` 是对应的静态估算。选币按 outpoint 去重，不按脚本合并。一枚够付时只用最小的那一枚。`MustSpend` 与 `SpendAllCoins` 强制全部指定输入进入交易，每笔输入单独签名。`SetVersion(1)` 用版本 1 sighash，默认每输入 compute budget 10（1000 compute gram，相当于版本 0 的一次签名）。版本 1 的 wRPC JSON 同时写 `sigOpCount` 和 `computeBudget`。
- RPC：内存 JSON 样例，加上本地回环 WebSocket。大整数用整数记号，不用 `double`（样本 `9007199254740993`）。`getBlockDagInfo` 的 `network` 接受字符串或 `{networkType, suffix}`。`samples/NodeQuery` 对公共主网端点做过一次只读查询，见上文。

## Toccata 兼容性

对照的是 2026-10-30 读取的 rusty-kaspa `master`，不是一台在线节点。

主网激活 DAA 分数 474165565 来自升级说明提交 `90dbf074275d60c1fe74a3491883196f110970c0`，大约 2026-06-30 16:15 UTC。当前 master 的 `constants.rs` 不再按这个分数分支。`consensus/core/src/config/params.rs` 里 mainnet、testnet、simnet、devnet 的 `block_mass_limits` 都直接写成同一组数字。这四个网络的激活 DAA 没有写在这份 params 里。

分叉后的块限额是 compute 500000、storage 500000、transient 1000000。`TRANSIENT_BYTE_TO_MASS_FACTOR` 仍是 4。单笔标准 mass 上限已在 PR 1086（提交 `02dcf44`）去掉。未合并的 PR 1141 想把 transient 改成每字节 1、限额 250000；本库不采用未合并的改动。

版本 1 交易号：`id = BLAKE3("TransactionV1Id", payload_digest || rest_digest)`。空 payload 使用 `ZERO_PAYLOAD_DIGEST`。rest 排除 payload、签名脚本和 mass。交易哈希仍是 BLAKE2b `TransactionHash`，版本 1 总是写入 storage mass，并在不排除 mass 时写入每个输入的 compute budget。SigopCount 变体在版本 1 哈希里写成 budget 0。

版本 1 mass：compute budget 每单位 100 gram。估算尺寸每个版本 1 输入加 2。covenant 存在时加 authorizing input 的 2 字节和 32 字节 id，不加那 1 字节存在标志。UTXO plurality 在有 covenant id 时加 32 字节。

最低中继费按升级说明：`100 * max(compute, 归一化 transient)`。默认限额下，归一化 transient 等于 `2 * 估算字节数`。storage mass 不进入这个下限。

`KaspaRpcHandler::handshake` 在 `rpc/wrpc/server/src/service.rs` 里仍是 TODO，当前节点不发送握手问候。通道会识别 method 为 `handshake` 或带 `handshake` 字段、且没有请求 id 的文本帧，其余无 id 帧当作通知。响应按请求 id 匹配。

脚本虚拟机按 rusty-kaspa `master`（`4edcbbccb449f84832afb1ca157904b35f37d006`）的 `crypto/txscript` 执行。`script_tests.json` 955 条通过，0 条因未实现操作码跳过，23 条注释不计入。`opcodes.rs` 的 50 个表驱动测试和 `scriptPubKey` 字符串往返已经移植。`Script.ToRustString` 用上游操作码名和 `0x` 十六进制；`ToString` 仍是原来的写法。

`OP_ZKPRECOMPILE` 的标签、费用和结构检查已按上游定稿规则执行。Groth16 标签 `0x20` 收费 14000000 脚本单位，RISC0 标签 `0x21` 收费 25000000。未知标签在收费前以 `ZkIntegrity` 拒绝。元数吻合之后，每个 `gamma_abc_g1` 元素再收 250000。BN254 配对、点是否在曲线上、子群检查，以及 RISC0 的 succinct STARK，仍返回未实现，不压入 true。长度正确但点不在曲线上时，上游会在 gamma 计费之前拒绝；本库做不到这个检查，所以会先计费，再以未实现失败关闭。证明长度不是 128 字节时按过短或尾随字节拒绝，这是长度检查，不是点解码。

`ISeqCommitAccessor` 说明 KIP-21 的窗口：`X.blue_score + F > P.blue_score`。10 块每秒时 `F` 是 432000。祖先未知返回 `BlockAlreadyPruned`，不在链上返回 `BlockNotSelected`，超出窗口返回 `BlockIsTooDeep`。对照 rusty-kaspa master 的 `seq_commit_within_threshold` 和 `Header::post_toccata_chainblock_selected_parent`：视点 `P` 是正在验证的链块的 selected parent，链块的第一个直接父块就是 selected parent。Toccata 清理之后，master 不再按 DAA 分数切换这支访问器。`WrpcSeqCommitAccessor` 用 `getBlock` 和 `getBlockDagInfo`，视点是 DAG sink 的蓝分，不是正在验证的块的 selected parent，所以它不是共识实现。它不调用 `submitTransaction`，也不自己打开套接字。`ConsensusSeqCommitAccessor` 用同样两个只读方法沿 selected parent 回推。默认视点是 sink，也就是区块模板的 virtual selected parent。设置 `ChainBlock` 时，视点改成该链块的 selected parent。窗口按这个 `P` 的蓝分计算。它核对 `selectedParentHash` 与第一个直接父块、header 与 verbose 的蓝分、`isChainBlock` 与回推结果是否一致，并要求蓝分沿链严格下降。缺块、缺父块或字段缺失抛 `IncompleteNodeData`，不当作已剪枝。同一次调用里 sink 变化，或调用者指定的链块已经不在 selected parent 链上，抛 `Reorg`，这次调用不返回结果。rusty-kaspa 的 `is_chain_block(hash)` 是 `is_chain_ancestor_of(hash, sink)`，所以 sink 自己一定是链块。回推走到的块如果 `isChainBlock` 为 false，会再读一次 `getBlockDagInfo`：sink 已变则抛 `Reorg`（10 BPS 上虚拟选定父块可以在两次只读请求之间离开选定链），sink 未变则抛 `InconsistentData`。请求数达到上限抛 `RequestLimit`。缓存命中不再请求。轻客户端只能尽量接近共识：完整 finality 窗口有几十万步 selected parent，剪枝后的可达性只在节点里。完全等同需要运行节点。

### 审查条目

| 条目 | 结论 | 处理 |
| --- | --- | --- |
| 1. `MinimumChange` 默认为 0，小找零把 storage mass 顶爆 | 属实 | 默认阈值 `C / storage 限额`（0.02 KAS）。找零导致超限时再并进手续费。1 KAS 付 0.99 KAS 的测试不再抛出。极小的付款输出仍然拒绝。 |
| 2. 单笔标准上限仍是 100000 | 属实（本库）。rusty-kaspa master 已去掉单笔上限，块限额为 500000 / 500000 / 1000000 | 构建器默认用块限额。`toccataActive: false` 或把 `MaximumMass` 设为 `MaxStandardTransactionMass` 保留 100000。storage mass 落在 100001 到 500000 的交易可以构建。 |
| 3. 版本 1 交易 | 属实，原先未实现 | BLAKE3、`TxIn.ComputeBudget`、`TxOut.Covenant`、`FromBytes` / `ToBytes`、sighash 写入 covenant、版本 1 mass。用 `tx.rs` 测试 10–14 核对。0.1.6 起脚本引擎按 compute budget 计量，并执行已确定规则的 covenant 操作码。 |
| 4. WebSocket 不按 id 匹配，握手和通知会挡住响应 | 部分属实 | 通道按 id 匹配，通知单独保存。节点握手在 rusty-kaspa 里仍是空实现，本库只识别这种帧，不编造节点问候。测试用本地 `HttpListener`。 |
| 5. `getBlockDagInfo` 的 network 只接受字符串 | 属实 | 字符串和 `{networkType, suffix}` 都接受。 |
| 6. `SendFees` 不检查 100 sompi/gram | 部分属实 | `SendEstimatedFees` 本来就拒绝低于 100 的费率。`SendFees` 现在按 compute 与归一化 transient 检查显式手续费。`AllowBelowMinimumRelayFee` 可以放开。 |
| 7. 重复 outpoint 会变成双花输入 | 属实 | `AddCoins` 按 outpoint 保留第一次。 |
| 8. `Key.Create` 不清理临时缓冲区；`ExtKey.PrivateKey` 返回内部对象 | 属实 | `Create` / `TakeSecret` 清零调用方缓冲区。`PrivateKey` 返回副本。 |

## 0.1.2 联调修复

主网联调里，多输入和版本 1 付款是在库外手工组装的。这一版把这两条路径放回 `TransactionBuilder`。回归用的是已上链交易的形状，测试里没有密钥，也没有广播：

| 交易 | 版本 | 输入 | 金额 | 手续费 | 中继 mass |
| --- | --- | --- | --- | --- | --- |
| `5fd8f8600950c36b1d9195d9afab0bbf5bde94594fd8246d2c2419c64f703868` | 0 | 1 | 0.2 KAS | 203600 | 2036 |
| `2febe6cafd5f78f8ce21636d48f060560b39a7529b00646a00cf5d62098aa3d4` | 0 | 2 | 0.2 KAS | 315400 | 3154 |
| `4d1c6a5e1b127278945673b3ed116786110b2b1ca44090c9f810a50e12350714` | 1 | 1，compute budget 10 | 0.1 KAS | 203800 | 2038 |

版本 1 那笔的 wRPC JSON 同时有 `sigOpCount` 和 `computeBudget`，与 `d5f68e7` 的写法一致。

## 示例

示例是 `samples/` 下的控制台项目，对标 NBitcoin 文档里的短示例。默认不广播。离线示例由 `tests/NKaspa.Tests/SampleSmokeTests.cs` 和 `samples/smoke.ps1` 覆盖。`NodeQuery` 要连公共节点，不进单元测试。`Json` 只打印 JSON，不连节点。

| 示例 | 项目 | NBitcoin | NKaspa |
| --- | --- | --- | --- |
| 助记词与派生地址 | `samples/Mnemonic` | `Mnemonic`、`ExtKey`、`KeyPath` | `Mnemonic.DeriveExtKey`、`KeyPath.KaspaReceive(i)` = `m/44'/111111'/0'/0/i`。主网 `kpub`，测试网 `ktub`。 |
| 地址与 ScriptPubKey | `samples/Address` | `BitcoinAddress.Create`、`ScriptPubKey` | `KaspaAddress.Parse` / `TryParse` / `IsValid`，以及 `KaspaAddress.ScriptPubKey`。 |
| 只读节点查询 | `samples/NodeQuery` | `RPCClient` 的只读调用 | `KaspaWebSocketChannel` + `KaspaWrpcJsonClient`。方法是 `getServerInfo`、`getBlockDagInfo`、`getBalanceByAddress`、`getUtxosByAddresses`。默认 `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json`。不调用 `submitTransaction`。 |
| 构建并签名 | `samples/Spend` | `TransactionBuilder` | 版本 0、版本 0 的 `MustSpend` 双输入、版本 1，费用是 `SendMinimumRelayFee`。默认打印交易十六进制和 wRPC JSON。`--broadcast` 且环境变量 `NKASPA_MNEMONIC` 都有，并且还传入 `--to` 与 `--amount` 时才提交。 |
| PSKT 2-of-2 | `samples/PsktMultisig` | BIP174 `PartiallySignedTransaction`（不移植） | `NKaspa.PSKT.PSKT`：创建、双方 `Sign`、`Combine`、`Finalize`、`ExtractTransaction`。 |
| 离线消息签名 | `samples/SignMessage` | `Key.SignMessage`。BIP322 仍无需实现 | 对 UTF-8 的 BLAKE2b-256 调用 `Key.SignSchnorr` / `Key.SignECDSA`，再用 `PubKey.Verify` / `PubKeyECDSA.Verify`。不是比特币消息前缀。 |
| JSON | `samples/Json` | `NBitcoin.JsonConverters.Serializer` | `NKaspaJsonSerializer` 与 `AddNKaspaConverters(network)`。版本 0 / 版本 1（`computeBudget`、`covenant`）、`scriptPublicKey` 的版本前缀十六进制。私钥默认拒绝。 |

## 仍未完成

- `OP_ZKPRECOMPILE` 的 BN254 Groth16 配对、压缩点的曲线与子群检查，以及 RISC0 succinct STARK。结构检查已经执行。长度正确但不在曲线上的点会先计 gamma 费，再返回未实现。引擎不接受证明。
- 与正在运行的 rusty-kaspa 做 PSKT 字节对照。上游测试没有钉死向量。
- 版本 1 compute budget 进入 PSKT。rusty-kaspa 的 `unsigned_tx` 这里仍是 TODO。
- NuGet 发布。这一版只改版本号，不推包。
- Newtonsoft.Json 适配。序列化只走 System.Text.Json，没有加这个包。
- 未合并的 transient mass 改动（PR 1141）以及未能从源码确认的测试网激活 DAA。
- 示例里的 `--broadcast` 路径没有对公共节点执行过。
- `ConsensusSeqCommitAccessor` 仍然不是节点。默认请求上限 64，到顶就抛异常，不会扫完整个 finality 窗口，也不会把缺块当成 `BlockAlreadyPruned`。`WrpcSeqCommitAccessor` 的视点仍是 DAG sink。

## 下一步计划

- 有可用的验证器之后，再补 BN254 配对和 RISC0 STARK。在此之前保持未实现。
- 有上游向量之后，钉死多公钥 map 和多 xpub 的 JSON 键序。
- NuGet 打包准备。示例已经在 `samples/`，默认不广播。
