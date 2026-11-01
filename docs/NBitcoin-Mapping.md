# NBitcoin 与 NKaspa 对照

英文版：[NBitcoin and NKaspa](NBitcoin-Mapping.en.md)。

对照的 NBitcoin 树是只读参考 `01NBitcoin/NBitcoin-master/NBitcoin`（本文件描述的是该树里的核心目录和主要类型）。NKaspa 的类型名尽量沿用对应名字，XML 注释里写 `Kaspa counterpart of NBitcoin.X`。行为按 Kaspa，不按比特币。

状态只用三种：

- **已实现**：这一版（0.1.11-alpha）里有对应类型，范围以代码为准。
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
| `ScriptEvaluationContext` | `ScriptEvaluationContext` / `TxScriptEngine` | 已实现 | 对标 NBitcoin 的执行上下文，规则对齐 rusty-kaspa `crypto/txscript` 的 `TxScriptEngine`。版本 0 预算是 `sigOpCount * 100000 + 9999`。版本 1 预算是 `computeBudget * 10000 + 9999`。脚本最长 1000000 字节，元素最长 1000000 字节，数据栈加备用栈最深 244，非推送操作码最多 1000000。`OP_ZKPRECOMPILE` 执行已定稿的标签、脚本单位、字段长度、BN254 标量范围、Groth16 元数与 gamma 计量，并且 RISC0 只接受 Poseidon2。BN254 配对、曲线点检查和 RISC0 STARK 仍返回未实现，不压入 true。没有 `ISeqCommitAccessor` 时 `OP_CHAINBLOCKSEQCOMMIT` 是非法操作码。`WrpcSeqCommitAccessor` 只读 `getBlock` 与 `getBlockDagInfo`，视点是 DAG sink，不是共识的 selected parent。`ConsensusSeqCommitAccessor` 沿 selected parent 链从固定的 sink 快照回推；链向前增长不是重组，真正的重组会有限次重试。数据不全、不一致或重试后仍重组时抛异常。轻客户端只能尽量接近共识，完全等同需要运行节点。 |
| `Transaction` / `TxIn` / `TxOut` / `OutPoint` | 同名 | 已实现 | 版本 0 与 Toccata 版本 1 的序列化、交易号和交易哈希。版本 1 交易号是 BLAKE3。`Transaction.Verify` 对每个输入跑脚本引擎。已确定规则的内省和 covenant 操作码会执行。 |
| `TransactionBuilder` / `Coin` | `TransactionBuilder` / `Coin` | 已实现 | 精简构建器：选币、Schnorr 与 ECDSA P2PK 签名、找零、显式费用、按费率估价，或只付最低中继费。`Verify` 检查每一个输入的脚本。`Coin` 是 `OutPoint` 加 `UtxoEntry`。默认版本 0。`SetVersion(1)` 产出版本 1，默认 compute budget 是 10。`MustSpend` / `SpendAllCoins` 强制每一枚指定的币进入交易并单独签名。 |
| `TransactionSignature` / sighash | `SchnorrSignature` / `ECDSASignature` / `SigHashType` | 已实现 | Kaspa sighash。`SigHashType.Single` 是 `0x04`，比特币的 SINGLE 是 `0x03`。 |
| `uint256` | `uint256` | 已实现 | 可解析、比较、按 32 字节序列化。显示不反转字节，见下文。 |
| `uint160` / `uint128` | — | 无需实现 | 地址和交易哈希都是 32 字节。 |
| `Network` | `Network` | 已实现 | 精简为 Mainnet、Testnet、Simnet、Devnet 与地址前缀。 |
| `Block` / `BlockHeader` | — | 计划中 | Kaspa 区块是 GHOSTDAG 块 DAG，不是比特币单链头。字段要另定，不能照搬。 |
| `Money` | `Money` | 已实现 | 单位是 sompi。1 KAS = 100,000,000 sompi。协议金额是 u64。 |
| `FeeRate` | `FeeRate` | 已实现 | 单位是 sompi/gram。Toccata 之后的最低中继费 = 100 sompi × max(compute mass, 归一化 transient mass)。storage 不计入。标准单输入 Schnorr 付款约 0.002 KAS（2036 gram，203600 sompi）。`GetFee` 按费率乘 gram。`MassCalculator.EstimateFee` 同样按费率，并拒绝低于 100 的费率。`EstimateMinimumRelayFee` 按上面的 mass 公式估价。构建器的下限是 `MinimumRelayFeeRate`：默认策略是 100。`MassPolicy.PreToccata` 是 1（rusty-kaspa `90dbf074` `mining/src/mempool/config.rs` 第 28 行 `LEGACY_MINIMUM_RELAY_TRANSACTION_FEE = 1_000` sompi/kg，即 1 sompi/gram）。比特币的 `Target`、工作量目标和 blue score 留在区块层，这一版没有。 |
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

`ExtKey`、`ExtPubKey`、`KeyPath`、`HDFingerprint`。没有单独的 `RootedKeyPath`、`IHDKey`、`BitcoinExtKey`。按网络输出扩展私钥用 `ExportXprv()`、`ExportXprv(Network)`、`GetWif(Network)`、`ToString(Network)` 或 `ToBase58`。无参 `ExtKey.ToString()` 只返回类型名 `NKaspa.ExtKey`，与 NBitcoin 一样不把 `kprv` 写进日志。`ExportXprv()` 与 `ToBase58()` 相同。`ExportXprv(Network)`、`GetWif` 与 `ToString(Network)` 是同一串文本。NBitcoin 的 `GetWif` 返回 `BitcoinExtKey`。

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
| 已实现 | 英文词表 2048 词，内嵌资源。种子是 PBKDF2-HMAC-SHA512，2048 次，64 字节。口令盐是 `mnemonic` 加上 NFKD 口令。宽松构造仍允许错误校验和，`IsValidChecksum` 为 false。`Parse`、`ParseValidated` 和 `requireValidChecksum: true` 抛 `FormatException`（`BIP39 checksum does not match.`）。错误校验和不能 `DeriveSeed` / `DeriveExtKey`，除非 `allowInvalidChecksum: true`。`Words` 是 `IReadOnlyList<string>`。无参 `ToString` 返回 `NKaspa.Mnemonic`，句子用 `ToPhrase()`。 |

英文词表从 NBitcoin 复制，许可声明在 `src/NKaspa/BIP39/WORDLIST-LICENSE.txt`（MIT，Copyright (c) 2023 Metaco SA）。词表文件本身只有单词。

大小写不敏感，以及去掉零宽字符（含词内的零宽标记和句首 BOM），是 NKaspa 相对 BIP39 的扩展，另外还有 NFKD。`ToPhrase()` 保存的是词表里的规范小写词，因此用这句规范词派生的种子与其他钱包相同。把含大写或全角字符的句子原样输入其他钱包，可能会被拒绝。口令只做 NFKD，不去零宽，也不改大小写。

Case-insensitivity and zero-width removal (a leading BOM and marks inside a word) are NKaspa extensions to BIP39, together with NFKD. The stored words are the canonical wordlist entries, so the derived seed matches other wallets given that canonical phrase. A phrase with uppercase or full-width characters typed verbatim into another wallet may fail. The passphrase is NFKD only.

## Protocol

`Node`、`Message`、`Payload`、`VersionPayload`、`TxPayload`、`BlockPayload`、`InventoryVector`、`NetworkAddress`、`VarInt`、`VarString`，以及 compact block、feefilter、wtxid relay、witness 相关载荷。

| 状态 | 原因 |
| --- | --- |
| 无需原样实现 | 这些是比特币 P2P 消息。Kaspa 节点协议不同，没有 witness、compact block 那一套比特币载荷。 |
| 计划中（另写） | 若以后做节点客户端，按 Kaspa P2P 写。这一版的客户端是 wRPC JSON 与 Borsh，不是完整 P2P 节点。 |

交易里的长度是 u64 小端，写在 `Transaction` 的序列化里，不从比特币 `Protocol` 目录拷贝 `VarInt`。

## Policy

`StandardTransactionPolicy`、`MinerTransactionPolicy`、`ITransactionPolicy`。

| 状态 | 原因 |
| --- | --- |
| 无需原样实现 | 比特币标准脚本（P2PKH、P2WPKH、multisig 计数）对不上 Kaspa。 |
| 已实现（mass） | `MassCalculator`：版本 0 与版本 1 的 compute mass、KIP-13 transient mass、KIP-9 storage mass。费用 mass 是这三项归一化后的最大值。块限额是 compute 500000、storage 500000、transient 1000000。旧的单笔 `100_000` 仍保留为可选策略。币库交易这三项都是 0。脚本执行在 `TxScriptEngine`。标准性策略仍是已有的模板和 mass，没有单独的 `StandardTransactionPolicy`。`FeeEstimator` 不需要私钥：按输入脚本种类和输出金额算出与已签名交易相同的 mass 和手续费。版本 0、1 个 Schnorr 输入、2 个 34 字节输出的 compute mass 是 2036。协议最低费率常量是 100 sompi/gram。`MinimumFeerate` 取 `getFeeEstimate` 里最低的正有限桶并向上取整，`PayableFeerate` 把它抬到 100。 |

## RPC

`RPCClient`、`RPCClient.Wallet`、`RestClient`，以及 `GetBlockRPCResponse`、`ListUnspentOptions`、`UnspentCoin` 等响应类型。

| 状态 | 原因 |
| --- | --- |
| 已实现（最小子集） | `IKaspaRpcClient` 同时由 `KaspaWrpcJsonClient` 和 `KaspaWrpcBorshClient` 实现。`KaspaRpcClient.Create(url, encoding = Auto)` 返回 `KaspaRpcConnection`，本身不连接。Auto 先看 URL 路径，再看端口，路径优先。路径以 `/wrpc/borsh` 结尾或包含 `/borsh` 时选 Borsh；以 `/wrpc/json` 结尾或包含 `/json` 时选 JSON。比较不区分大小写。结尾检查会去掉末尾斜杠。两条都匹配时先测 Borsh，所以同时含 `/json` 和 `/borsh` 的路径选 Borsh。端口仍按 rusty-kaspa `4edcbbcc` `consensus/core/src/network.rs`：Borsh 是 17110、17210、17510、17610，JSON 是 18110、18210、18510、18610。Simnet 是 17510 与 18510，不是 17310 或 18310。路径和端口都对不上时抛 `ArgumentException`，不会默认成某一种编码。例如 443 端口的 `wss`，路径里没有 `/json` 或 `/borsh`。gRPC 端口 16110、16210、16510、16610 也抛 `ArgumentException`，说明本库说的是 wRPC（上面的 Borsh 或 JSON 端口），不是 gRPC。显式传入 `KaspaRpcEncoding.Borsh` 或 `Json` 可以覆盖路径和 wRPC 端口，不能把 gRPC 端口变成 wRPC。JSON 客户端收到二进制帧，或 Borsh 客户端收到文本帧，抛 `KaspaRpcEncodingMismatchException`。Borsh 客户端收到 JSON 握手文本时 `HandshakeFailed` 为 true。进行中的请求以这个异常失败，不等待请求超时，也不重连。`KaspaWrpcPorts` 记下四个 gRPC 端口，并提供 `TrySelectPath`。方法是 `getServerInfo`、`getBlockDagInfo`、`getUtxosByAddresses`、`getBalanceByAddress`、`getBalancesByAddresses`、`submitTransaction`、`getFeeEstimate`、`getBlock`，以及 `NotifyUtxosChanged`、`NotifyVirtualDaaScoreChanged`、`NotifyBlockAdded`。JSON 信封是 workflow-rpc：`id`、`method`、`params`。成功体在 `params`，读的时候也接受 `result`。错误是 `error.code` / `error.message`。不是 JSON-RPC 2.0。Borsh 是同一套 workflow-rpc 二进制帧：可选 u64 id、一个字节的 `RpcApiOps`，再加上带版本头的 serializer。订阅在线上是 `Subscribe`（op 3）和 `Unsubscribe`（op 4），载荷是 `Scope`。节点没有注册旧的 `Notify*` 操作码。 |
| 单元测试不连节点 | 单元测试用内存里的 JSON 通道和本地回环 WebSocket。`samples/NodeQuery` 在 2026-10-30 对 `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json` 做过一次只读查询：`getServerInfo` 返回 rusty-kaspa 2.1.0、mainnet、已同步、有 UTXO 索引；`getBlockDagInfo`、`getBalanceByAddress`、`getUtxosByAddresses` 都有响应。没有调用 `submitTransaction`。 |
| 费用估计的内层字段 | `priorityBucket`、`normalBuckets`、`lowBuckets`，以及每桶的 `feerate` / `estimatedSeconds`（两个小端 f64）。已按 rusty-kaspa `4edcbbcc` `rpc/core/src/model/feerate_estimate.rs` 确认。`priorityBucket` 必填。响应是 serializer 版本 u16，再套一层 `serialize!(RpcFeeEstimate)`。JSON 形状仍与 kaswallet `WalletRestClient` 和 Kaspa WASM `IFeeEstimate` 一致。早先 `feerate.rs` 的 404 是路径不对，不是字段未定。 |
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
| `Key` / `ExtKey` / `Mnemonic` | 默认拒绝，包括类型上的 `JsonConverter`，所以 `JsonSerializer.Serialize` 不带 options 也会拒绝。异常文本带 WARNING，不含秘密。`allowPrivateKeys: true` 才写出十六进制私钥、`kprv` / `ktrv`，或 `ToPhrase()` 的句子。 |

紧凑、不缩进的交易 JSON 与 `KaspaRpcJson.WriteTransaction` 是同一串。2026-10-30 主网三笔已接受交易（`5fd8f860…`、`2febe6ca…`、`4d1c6a5e…`）按 wRPC 形状读入后，交易号与链上一致。浏览器的 `mass` 是中继 mass，没有写进 `storageMass`。

## 当前进度

0.1.11-alpha 增加 Borsh wRPC，见下文「0.1.11 Borsh wRPC」。0.1.10-alpha 的 API 变更见下文「0.1.10 审查修复」。

0.1.9-alpha。这一版让 `ConsensusSeqCommitAccessor` 在回推开始时固定 sink 及其蓝分。主网大约每秒 10 块，回推期间 `getBlockDagInfo` 会看到新的 sink。新 sink 的 selected parent 链如果仍包含该快照，视为链向前增长，按快照返回结果，不抛 `Reorg`。只有快照视点或回推路径上的区块离开选定链（`isChainBlock` 变为 false，或新链不经过它）才算重组。`StableDepth` 默认 20，大约是 10 块每秒下的 2 秒，可以把视点沿 selected parent 退到尖端之后。设为 0 则视点就是锁定的 sink。真正的重组默认自动重试 3 次，请求计数不归零，耗尽才抛 `Reorg`。默认请求上限是 256，含回推期间新长出来的块；到顶就停，不扫整个 finality 窗口。新 sink 比快照超前超过 32 个 selected parent，或预算不够把新链走完时，重读快照区块，用最新的 `isChainBlock` 判断它是否还在选定链上。`WrpcSeqCommitAccessor` 的视点仍是 DAG sink。0.1.8-alpha 加上了可选的 `ConsensusSeqCommitAccessor`：用只读的 `getBlock` 与 `getBlockDagInfo` 沿 selected parent 链回推，确定共识使用的区块和 seq commitment，并核对 selected parent、蓝分和 `isChainBlock`。数据不全、不一致或重组时抛异常，不静默当成剪枝或过深。轻客户端只能尽量接近共识，完全等同需要运行节点。0.1.7-alpha 补上 `OP_ZKPRECOMPILE` 已定稿的结构检查、内省与 covenant 构造助手、示例脚本，以及只读的 `WrpcSeqCommitAccessor`。0.1.6 起执行脚本。0.1.5 的通用 JSON 转换器还在。0.1.4 的行为还在：`samples/` 里的控制台示例。第一阶段的编码器、BLAKE2b、`uint256`、网络和三种地址仍在。第二阶段的密钥、签名、脚本、版本 0 交易和 sighash 仍在。第三阶段补上 BIP39、BIP32、mass / 费率、交易构建器和最小 wRPC JSON 客户端。0.1.1 补上 Toccata 版本 1 交易号、找零与 mass 策略，以及 wRPC 帧分发。0.1.2 让构建器强制花掉指定输入、在一枚币够付时不拉小币，并直接产出版本 1 付款。0.1.3 加上 PSKT：部分签名、合并、单签与 P2SH 多签终结、提取，以及 `TransactionBuilder.BuildPSKT`。

- 解决方案：`NKaspa.sln`，类库 `src/NKaspa`（`net8.0`、`netstandard2.1`），测试 `tests/NKaspa.Tests`（xUnit，`net8.0`），示例 `samples/`（八个控制台项目，`net8.0`，含 `samples/Tutorials`）。对照见下文「示例」。
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
- 交易构建器：本地 Schnorr 与 ECDSA P2PK 端到端。默认 `MinimumChange` 是 0.02 KAS。低于这个数的找零并进手续费。更大的找零只有在输出建不出来，并且金额不超过 `max(MinimumChange, MaxFoldedChange)`（`MaxFoldedChange` 默认 0.1 KAS），同时不超过无找零交易所需手续费的 `MaxFoldedFeeFactor` 倍（默认 100）时，才并进手续费。超过上限抛 `InvalidOperationException`，说明找零会超过 mass 限额，建议拆分付款或增加输入。成功并入后 `ChangeFoldedIntoFee` 是从找零输出拿掉、加进手续费的 sompi，不是总手续费。没有并入或构建失败时是 0。`TransactionMass` 只报告 mass，不重复这个数。默认费用 mass 上限是块的 compute 限额 500000。`toccataActive: false` 或 `MassPolicy.PreToccata` 把单笔上限恢复到 100000，中继费率是 1 sompi/gram，此时 `MinimumChange` 是 0.1 KAS。`MaxFoldedChange` 和 `MaxFoldedFeeFactor` 是公开属性，调用方可以放宽。它们是钱包安全上限，不是共识规则。放宽之后，更多 sompi 会进手续费。显式手续费低于 `MinimumRelayFeeRate` 会拒绝，除非 `AllowBelowMinimumRelayFee`。默认最低中继费 = 100 sompi × max(compute mass, 归一化 transient mass)，不含 storage。标准单输入是 2036 gram，203600 sompi，约 0.002 KAS。`SendEstimatedFees` 按费率乘 fee mass（三项里最大的那个）。`SendMinimumRelayFee` 只付中继下限。`EstimateFee` 与 `EstimateMinimumRelayFee` 是对应的静态估算。选币按 outpoint 去重，不按脚本合并。一枚够付时只用最小的那一枚。`MustSpend` 与 `SpendAllCoins` 强制全部指定输入进入交易，每笔输入单独签名。`SetVersion(1)` 用版本 1 sighash，默认每输入 compute budget 10（1000 compute gram，相当于版本 0 的一次签名）。版本 1 的 wRPC JSON 同时写 `sigOpCount` 和 `computeBudget`。
- RPC：内存 JSON 样例，加上本地回环 WebSocket。大整数用整数记号，不用 `double`（样本 `9007199254740993`）。`getBlockDagInfo` 的 `network` 接受字符串或 `{networkType, suffix}`。`samples/NodeQuery` 对公共主网端点做过一次只读查询，见上文。0.1.11 另有手工计算的 Borsh 帧、假的二进制 WebSocket 往返，以及订阅通知测试。这些测试不连真实节点。

## Toccata 兼容性

对照的是 2026-10-30 读取的 rusty-kaspa `master`，不是一台在线节点。

主网激活 DAA 分数 474165565 来自 rusty-kaspa 提交 `90dbf074275d60c1fe74a3491883196f110970c0` 的 `consensus/core/src/config/params.rs` 第 724 行：`toccata_activation: ForkActivation::new(474_165_565)`。测试网 467579632 是同文件第 785 行：`toccata_activation: ForkActivation::new(467_579_632)`。当前 master 已删除该字段。`constants.rs` 不再按这个分数分支。`params.rs` 里 mainnet、testnet、simnet、devnet 的 `block_mass_limits` 都直接写成同一组数字。

分叉后的块限额是 compute 500000、storage 500000、transient 1000000。`TRANSIENT_BYTE_TO_MASS_FACTOR` 仍是 4。单笔标准 mass 上限已在 PR 1086（提交 `02dcf44`）去掉。未合并的 PR 1141 想把 transient 改成每字节 1、限额 250000；本库不采用未合并的改动。

版本 1 交易号：`id = BLAKE3("TransactionV1Id", payload_digest || rest_digest)`。空 payload 使用 `ZERO_PAYLOAD_DIGEST`。rest 排除 payload、签名脚本和 mass。交易哈希仍是 BLAKE2b `TransactionHash`，版本 1 总是写入 storage mass，并在不排除 mass 时写入每个输入的 compute budget。SigopCount 变体在版本 1 哈希里写成 budget 0。

版本 1 mass：compute budget 每单位 100 gram。估算尺寸每个版本 1 输入加 2。covenant 存在时加 authorizing input 的 2 字节和 32 字节 id，不加那 1 字节存在标志。UTXO plurality 在有 covenant id 时加 32 字节。

最低中继费按升级说明：`100 * max(compute, 归一化 transient)`。默认限额下，归一化 transient 等于 `2 * 估算字节数`。storage mass 不进入这个下限。

`KaspaRpcHandler::handshake` 在 `rpc/wrpc/server/src/service.rs` 里仍是 TODO，当前节点不发送握手问候。通道会识别 method 为 `handshake` 或带 `handshake` 字段、且没有请求 id 的文本帧，其余无 id 帧当作通知。响应按请求 id 匹配。

脚本虚拟机按 rusty-kaspa `master`（`4edcbbccb449f84832afb1ca157904b35f37d006`）的 `crypto/txscript` 执行。`script_tests.json` 955 条通过，0 条因未实现操作码跳过，23 条注释不计入。`opcodes.rs` 的 50 个表驱动测试和 `scriptPubKey` 字符串往返已经移植。`Script.ToRustString` 用上游操作码名和 `0x` 十六进制；`ToString` 仍是原来的写法。

`OP_ZKPRECOMPILE` 的标签、费用和结构检查已按上游定稿规则执行。Groth16 标签 `0x20` 收费 14000000 脚本单位，RISC0 标签 `0x21` 收费 25000000。未知标签在收费前以 `ZkIntegrity` 拒绝。元数吻合之后，每个 `gamma_abc_g1` 元素再收 250000。BN254 配对、点是否在曲线上、子群检查，以及 RISC0 的 succinct STARK，仍返回未实现，不压入 true。长度正确但点不在曲线上时，上游会在 gamma 计费之前拒绝；本库做不到这个检查，所以会先计费，再以未实现失败关闭。证明长度不是 128 字节时按过短或尾随字节拒绝，这是长度检查，不是点解码。

`ISeqCommitAccessor` 说明 KIP-21 的窗口：`X.blue_score + F > P.blue_score`。10 块每秒时 `F` 是 432000。祖先未知返回 `BlockAlreadyPruned`，不在链上返回 `BlockNotSelected`，超出窗口返回 `BlockIsTooDeep`。对照 rusty-kaspa master 的 `seq_commit_within_threshold` 和 `Header::post_toccata_chainblock_selected_parent`：视点 `P` 是正在验证的链块的 selected parent，链块的第一个直接父块就是 selected parent。Toccata 清理之后，master 不再按 DAA 分数切换这支访问器。`WrpcSeqCommitAccessor` 用 `getBlock` 和 `getBlockDagInfo`，视点是 DAG sink 的蓝分，不是正在验证的块的 selected parent，所以它不是共识实现。它不调用 `submitTransaction`，也不自己打开套接字。`ConsensusSeqCommitAccessor` 用同样两个只读方法沿 selected parent 回推。默认视点是 sink，也就是区块模板的 virtual selected parent。设置 `ChainBlock` 时，视点改成该链块的 selected parent。窗口按这个 `P` 的蓝分计算。它核对 `selectedParentHash` 与第一个直接父块、header 与 verbose 的蓝分、`isChainBlock` 与回推结果是否一致，并要求蓝分沿链严格下降。缺块、缺父块或字段缺失抛 `IncompleteNodeData`，不当作已剪枝。调用开始时把 sink 及其蓝分固定为快照，回推和窗口都按这个快照。之后看到新 sink，若新 sink 的 selected parent 链仍包含该快照，视为向前增长，返回快照结果。只有快照视点或回推路径上的区块离开 selected chain（`isChainBlock` 变为 false，或新链不经过它）才算重组。`StableDepth` 默认 20，可让视点从锁定的 sink 沿 selected parent 后退；设为 0 则用这个 sink。开始时如果 sink 的 `isChainBlock` 已经是 false，会先改读更新的 sink，再冻结快照。真正的重组默认重试 3 次，耗尽才抛 `Reorg`。调用者指定的链块不在选定链上也会抛 `Reorg`，这次调用不返回结果。rusty-kaspa 的 `is_chain_block(hash)` 是 `is_chain_ancestor_of(hash, sink)`，所以 sink 自己一定是链块。回推走到的块如果 `isChainBlock` 为 false，会再读一次 `getBlockDagInfo`：sink 未变则抛 `InconsistentData`；sink 已变，且该块已不在新的 selected chain 上，则是重组。新链超前超过 32 步，或请求预算不够走完时，重读快照的 `isChainBlock`，不再逐块追尖端。请求数达到上限抛 `RequestLimit`。缓存命中不再请求。轻客户端只能尽量接近共识：完整 finality 窗口有几十万步 selected parent，剪枝后的可达性只在节点里。完全等同需要运行节点。

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

## 0.1.10 审查修复

只读审查提了七条。1、3、4、5 改了代码。2、6、7 只做评估。

`ExtKey` 的无参 `ToString` 原先调用 `ToBase58()`，日志里会出现 `kprv`。NBitcoin 的 `ExtKey` 不重写无参 `ToString`，输出只有类型名。0.1.10 起无参 `ToString` 返回 `NKaspa.ExtKey`，不读取密钥材料。导出用 `ExportXprv()`、`ExportXprv(Network)`、`GetWif(Network)`、`ToString(Network)` 或 `ToBase58()`。`ExportXprv()` 与 `ToBase58()` 相同。`ExportXprv(Network)` 与 `ToString(Network)`、`GetWif` 是同一串文本。`ExtPubKey.ToString()` 仍是扩展公钥。允许私钥的 JSON 转换器写出 `ExportXprv(Network)`。示例不打印无参 `ToString`。

BIP39 构造时去掉零宽字符（U+200B、U+200C、U+200D、U+FEFF、U+2060、U+180E、U+200E、U+200F），整句做 NFKD，按 Unicode 空白切分（含全角空格 U+3000、NBSP U+00A0 和连续空格），再 NFKD 并转小写，然后对照英文词表。存下的是词表里的规范词。同一组规范词得到同一个种子，变体不会变成另一个钱包。零宽字符不是分隔符；只把空格换成 U+200B 会失败。`ToPhrase()` 是官方句子。无参 `ToString()` 只返回 `NKaspa.Mnemonic`。`DeriveSeed` 对 `ToPhrase()` 和口令做 NFKD 后再做 PBKDF2。口令保持大小写，不去零宽。

`KaspaWebSocketOptions`：连接超时默认 15 秒，请求超时默认 30 秒。`Timeout.InfiniteTimeSpan` 关闭对应超时。超时和调用方的 `CancellationToken` 链在一起。调用方取消仍是 `OperationCanceledException`，到点是 `TimeoutException`。一次 `ExchangeAsync` 只有一个超时窗口，覆盖发送和等待。`AutoReconnect` 默认 false。曾经连上之后断开，进行中的请求以 `InvalidOperationException`（文本含 `WebSocket disconnected.`）失败，不自动重发。重连使用新的 `ClientWebSocket`，延迟从 `ReconnectInitialDelay`（默认 200 毫秒）起按 `ReconnectBackoffFactor`（默认 2）增长，上限 `ReconnectMaxDelay`（默认 10 秒），次数 `MaxReconnectAttempts`（默认 5）。`IKaspaJsonChannel` 的签名不变。

Mass：当前 rusty-kaspa master 的 mainnet、testnet（suffix 10）、simnet、devnet 在 `params.rs` 里写的是同一组块限额，compute 500000、storage 500000、transient 1000000。审查里「测试网和开发网会拒绝按 Toccata 限额构造的交易」对当前这四套公共参数不成立。本库原先只有主网激活分，构建器默认对所有网络都用 Toccata 之后的单笔上限和费率。0.1.10 增加 `MassPolicy`。`Network.MainnetToccataActivationDaaScore` 是 474165565，出处是 rusty-kaspa `90dbf074` `consensus/core/src/config/params.rs` 第 724 行。`TestnetToccataActivationDaaScore` 是 467579632，同文件第 785 行（tn10-toc2 第 783 行、tn10-toc3 第 784 行作为次要出处；master 已删除该字段，可用 `WithToccataActivationDaaScore` 覆盖）。Simnet 和 Devnet 的激活分是 0，任何 DAA 分数都视为已激活，与当前 master 一致。未升级的旧 devnet 要显式传入 `MassPolicy.PreToccata`、`toccataActive: false`，或一个低于记录分数的 DAA。两组策略使用同一组块限额，不另造 Toccata 之前的三元组。激活之后单笔上限 500000，费率 100 sompi/gram，`MinimumChange` 0.02 KAS。之前单笔上限 100000，费率 1 sompi/gram（`90dbf074` `mining/src/mempool/config.rs` 第 28 行 `LEGACY_MINIMUM_RELAY_TRANSACTION_FEE = 1_000` sompi/kg），`MinimumChange` 是 `C / 100000`（0.1 KAS）。强制走 Toccata 之前的策略会得到 1 sompi/gram，当前节点可能拒绝；线上网络用默认策略。`TransactionBuilder` 增加 `(Network, ulong virtualDaaScore)` 和 `(Network, MassPolicy)`。`FeeRate.MinimumRelay` 与 `MassCalculator.MinimumFeeRate` 仍是 100。

评估，没有改对应行为：

- 助记词内部仍保存词表字符串。规范词是静态词表里的引用，秘密是词序。`.NET` 字符串不能擦除。公开的 `Words` 已改成只读视图，外部不能改元素。`GetWords()` 返回副本。NBitcoin 同样保存句子字符串。没有提供假装能擦除字符串的接口。
- 低于 0.02 KAS 的找零并进手续费，这是 KIP-9：`C / storage limit`，一枚 plurality 为 1 的输出，其 storage mass 等于 500000 的块存储限额。rusty-kaspa 钱包的 `MassCalculator::is_dust` 是另一条更小的启发式，大约 44400 sompi，注释写明内存池不按这个阈值拒绝。本库的地板按 KIP-9，更严。更大的找零不再无条件并进手续费，见下一节。
- 客户端已有 `getServerInfo`、`getBlockDagInfo`、`getUtxosByAddresses`、`getBalanceByAddress`、`submitTransaction`、`getFeeEstimate`、`getBlock`。通道已经保存原始通知 JSON。Kaspa 节点没有可查询的历史交易索引，区块会剪枝。类型化订阅可以放在现有套接字上：`notifyUtxosChanged`、`notifyBlockAdded`、`notifyVirtualChainChanged` 以及对应的停止方法，再加上事件。带重组处理的持久历史是更高一层。类型化订阅大约一到两天。历史存储大约一到两周。这一版不实现。

## 0.1.11 Borsh wRPC

`KaspaWrpcBorshClient` 与原来的 `KaspaWrpcJsonClient` 都实现 `IKaspaRpcClient`。`KaspaRpcClient.Create` 在 `KaspaRpcEncoding.Auto` 下先按路径、再按端口选择编码，路径优先。路径和端口都未知时抛异常，不默认。gRPC 端口 16110、16210、16510、16610 被拒绝。显式的 `Borsh` 或 `Json` 覆盖路径和 wRPC 端口，不覆盖 gRPC 端口。编码对不上时抛 `KaspaRpcEncodingMismatchException`。Borsh 套接字的超时、重连、16 MiB 上限、`State`、`StateChanged`、`Reconnected` 与 JSON 套接字相同。第一次 `ConnectAsync` 不触发 `Reconnected`。

线上格式对照 rusty-kaspa `4edcbbccb449f84832afb1ca157904b35f37d006` 和 workflow-rs `7e2958098bbe0647adcfcc0dfc174e438a60699e`（workflow-rpc / workflow-serializer 0.19.0）。请求是 `BorshReqHeader`（可选 u64 id、一个字节的 `RpcApiOps`）拼上带 u32 长度的 serializer 正文。成功响应的载荷是 Borsh `Result` 再加那个 serializer 字节串。borsh 1.8.1 里 `Ok` 是 `1u8`，`Err` 是 `0u8`。协议层 kind 仍是 Success。通知的载荷是 `Serializable<Notification>`，不是 `Result`。节点并不发送握手问候（`KaspaRpcHandler::handshake` 仍是 TODO），本库也不发送握手。JSON 客户端收到二进制帧，或 Borsh 客户端收到文本帧，立即抛 `KaspaRpcEncodingMismatchException`。进行中的请求一起失败，不等待请求超时，也不重连。Borsh 客户端收到 JSON 握手文本时 `HandshakeFailed` 为 true。

`NetworkType` 的 Borsh 顺序是 Mainnet、Testnet、Devnet、Simnet。地址前缀的顺序是 Mainnet、Testnet、Simnet、Devnet。两套编号不一样。

订阅的公开方法名是 `NotifyUtxosChangedAsync`、`NotifyVirtualDaaScoreChangedAsync`、`NotifyBlockAddedAsync`。线上操作码是 `Subscribe` = 3 和 `Unsubscribe` = 4，载荷是 `Serializable<Scope>`。节点路由没有注册 `NotifyUtxosChanged`（12）那一组。空地址列表表示全部地址。JSON 的 `subscribe` 参数是 serde 外部标签，例如 `{"UtxosChanged":{"addresses":[...]}}`。`KaspaSubscription` 释放时只停自己的 scope。

`SubmitTransaction` 只在假服务器测试里发送。这一节没有对真实网络提交交易。

## 0.1.11 蓝分、coinbase 成熟度、密钥库

版本号仍是 0.1.11-alpha。

蓝分。rusty-kaspa `GetBlockDagInfoResponse`（`rpc/core/src/model/message.rs`）没有 `blueScore`。Borsh 字段顺序是 `network`、`blockCount`、`headerCount`、`tipHashes`、`difficulty`、`pastMedianTime`、`virtualParentHashes`、`pruningPointHash`、`virtualDaaScore`、`sink`。本库不在这条 Borsh 记录末尾多读一个 u64。JSON 可以多一个 `blueScore`。缺这个字段时 `RpcBlockDagInfo.BlueScore` 是 0。`GetBlockDagInfoAsync` 不会顺手调用 `getSinkBlueScore`。sink 的蓝分是 `GetSinkBlueScoreAsync`（JSON 方法 `getSinkBlueScore`，Borsh 操作码 138）。响应是 serializer 版本 1，然后一个 u64 `blueScore`。这是 virtual selected parent（sink）的蓝分，旧名是 `GetVirtualSelectedParentBlueScore`。`GetVirtualChainFromBlockAsync`（操作码 128）的请求是 serializer 版本 2：`startHash`、`includeAcceptedTransactionIds`、可选的 `minConfirmationCount`。响应是版本 1：`removedChainBlockHashes`、`addedChainBlockHashes`、`acceptedTransactionIds`。这条响应没有蓝分。带区块头的 `GetVirtualChainFromBlockV2`（操作码 151）不读。区块头本来就有 `blueScore`。`blueWork` 是 `header.rs` 的 `BlueWorkType`（`Uint192`），24 字节小端。JSON 十六进制可以不补前导零，奇数长度会在左边补一个半字节。`RpcChainBlock.BlueWork` 和 `RpcBlockAdded.BlueWork` 在字段存在时保存这 24 字节。

Coinbase 成熟度。`ConsensusParams` 按网络给出 DAA 分数上的成熟度，出处是 rusty-kaspa `consensus/core/src/config/params.rs`、`bps.rs` 的 `Bps::coinbase_maturity`（`BPS * COINBASE_MATURITY_SECONDS`）和 `constants.rs` 里的 100 秒。主网 Crescendo 激活分是 110165000，之前 1 块每秒，成熟度 100，之后 1000。测试网 suffix 10（`Network.Testnet`）激活分是 88657000，规则相同。Simnet 和 Devnet 是 `ForkActivation::always()`，激活前也是 10 块每秒，所以任何分数的成熟度都是 1000。当前 master 没有 `TESTNET11_PARAMS`。历史上的 testnet-11 是 10 BPS 网络，`kaspawallet` 提交 `25c2dd86709b5a0b889785e7e6b0c00c8827a8b7` 在网络名 `kaspa-testnet-11` 时用成熟度 1000。`ConsensusParams.Testnet11` 记下这个数。`ForNetwork` 不把名字 `testnet` 映射到它。`IsCoinbaseMature(utxoEntry, virtualDaaScore, network)` 和 `SpendableAtDaaScore` 按这个时刻表判断。非 coinbase 直接成熟。coinbase 在 `virtualDaaScore - blockDaaScore` 达到该视点的成熟度时成熟，相等算成熟。视点低于区块分数时不成熟。减法不环绕。当前节点的 `TransactionValidator` 只存 `params.coinbase_maturity()`，也就是激活后的 1000。这个助手仍按分叉时刻表计算，方便测激活前的边界。

2026-11-01 对 testnet-10 做了只读核对，没有提交交易。REST `https://api-tn10.kaspa.org/info/blockdag` 的 `networkName` 是 `kaspa-testnet-10`，`virtualDaaScore` 是 `611687351`，`sink` 是 `12e70b94db857ebfa30d9bdfb5872c9e78299f698b703ba79fc4a5ab009f3004`，响应里没有 `blueScore`。同一 sink 的区块 REST 里，header 与 verboseData 的 `blueScore` 都是 `600053776`，`blueWork` 是未补齐的 `7cdd01645b922`。随后用本库连上公开 `wss://electron-10.kaspa.stream/kaspa/testnet-10/wrpc/json`（只读）。`getServerInfo` 是 rusty-kaspa 2.1.0、`testnet-10`。`getBlockDagInfo` 的 `virtualDaaScore` 是 `611688197`，`BlueScore` 是 0。`getSinkBlueScore` 是 `600054623`，与该 sink 的 header 和 verbose `blueScore` 相同。`blueWork` 解析成 24 字节小端。`getVirtualChainFromBlock` 从该 sink 返回 0 个移除块和 4 个新增块。没有调用 `submitTransaction`。

密钥库。`WalletKeystore` 用口令加密助记词或种子。KDF 是 PBKDF2-HMAC-SHA256。`.NET` 基础类库没有 Argon2，所以用 `Rfc2898DeriveBytes.Pbkdf2`（.NET Standard 2.1 上是同算法的实例方法），默认 600000 次，次数写进文件。密文是 AES-256-GCM（`System.Security.Cryptography.AesGcm`）。盐和 nonce 随机，除非调用方传入固定长度的值。JSON 字段顺序是 `version`、`kdf`、`kdfParams`、`cipher`、`salt`、`nonce`、`tag`、`ciphertext`。版本是 1，`kdf` 是 `pbkdf2-sha256`，`cipher` 是 `aes-256-gcm`。关联数据把版本、KDF、次数、哈希、密钥长度和算法绑在一起。口令错误，或密文、tag、nonce、次数被改，抛 `WrongKeystorePasswordException`，消息正好是 `Wrong keystore password.`。版本、KDF、哈希、密钥长度或算法对不上时，在解密前抛 `KeystoreException`。`KeystoreSecret.ToString` 和 `WalletKeystoreParameters.ToString` 不打印秘密。能清零的缓冲区用 `CryptographicOperations.ZeroMemory`。`.NET` 字符串清不掉。

## 0.1.11 无密钥手续费估算

`FeeEstimator.Estimate` 用占位签名脚本构造一笔未签名交易，再交给 `MassCalculator`。Schnorr 和 ECDSA 的占位签名脚本都是 66 字节（`OP_DATA_65 || 64 个零 || sighash`），版本 0 计 1 个 sigop，版本 1 在未指定时用 `TransactionBuilder.DefaultComputeBudget`（10）。不读取私钥。1 输入 2 输出的 Schnorr P2PK 与已签名交易的 compute、transient、storage mass 相同，compute mass 是 2036，按 100 sompi/gram 的中继费是 203600 sompi。输入 100000000、付出 20000000 时 storage mass 是 52531，`Quote.Fee` 是 5253100，中继费仍是 203600。输入 5675155239、输出 500000000 与 5171555239 时 storage mass 是 2017，低于 compute mass，`Quote.Fee` 与中继费都是 203600。两输入是 3154，版本 1 且预算 10 是 2038。`Quote` 的 `RelayFee` 乘的是中继 mass（不含 storage）。`Fee` 乘的是 `FeeMass`，storage mass 更大时手续费高于中继费。KIP-9 样例输入 5675155239、输出 12000000 与 5654655239 的 storage mass 是 83333，费用是 8333300 sompi，中继费仍是 203600。

`DefaultMinimumFeerateSompiPerGram` 是 100，与 `MassCalculator.MinimumFeeRate` 相同。`MinimumFeerate(RpcFeeEstimate)` 在 `priorityBucket`、`normalBuckets`、`lowBuckets` 里取最低的正有限 `feerate`，用 `Math.Ceiling` 收成整数 sompi。NaN、无穷、小于等于 0 的桶跳过。一个桶都没有时返回 100。节点可以给出低于 100 的桶，这个方法原样返回（向上取整之后）。`PayableFeerate` 把更低的桶抬到调用方给出的协议下限，默认 100。`Quote(RpcFeeEstimate)` 用的是 `PayableFeerate`。`ReadMinimumFeerateAsync` 调用 `getFeeEstimate`，返回的是没有抬升的 `MinimumFeerate`。显式费率低于下限抛 `ArgumentOutOfRangeException`。下限 0 抛 `ArgumentOutOfRangeException`，消息是 `The protocol minimum feerate must be non-zero.`。DAA 相关的费率仍在 `MassPolicy`，这一节的默认值是 Toccata 之后的 100。

## 0.1.10 后续修复

Linux 审查（对照 `f41c86a`，测试者 测试1）和钱包组 R1/R10 的后续改动。没有改版本号，仍是 0.1.10-alpha。

校验和：12 个 `abandon` 的校验和是错的。宽松构造 `new Mnemonic(sentence)` 仍不抛，`IsValidChecksum` 为 false，方便检查用户输入。`Mnemonic.Parse` 与 `Mnemonic.ParseValidated` 抛 `FormatException`，消息正好是 `BIP39 checksum does not match.`。`requireValidChecksum: true` 同样抛。`DeriveSeed` 和 `DeriveExtKey` 在校验和错误时抛 `InvalidOperationException`，消息含 `allowInvalidChecksum: true`。显式传入 `allowInvalidChecksum: true` 仍会从这些词派生，这不是可恢复的钱包。种子的口令是 `ToPhrase()` 的 NFKD，不是已经脱敏的 `ToString()`。

变体：大写、U+3000、零宽字符、BOM、NBSP、全角字母在能对上词表时，存的是规范词，种子与规范句子相同，不会静默变成另一个钱包。大小写不敏感和去掉零宽字符是 NKaspa 对 BIP39 的扩展。规范句子派生的种子与其他钱包兼容。把大写或全角原样输入其他钱包可能失败。零宽字符单独当分隔符、未知词、非法词数仍抛 `FormatException`。口令不做这些清理：口令里的零宽字符会改变种子。这是 BIP39 对口令的要求。

脱敏：`Mnemonic.ToString()` 返回 `NKaspa.Mnemonic`。句子用 `ToPhrase()`，词的副本用 `GetWords()`。`ExtKey.ToString()` 返回 `NKaspa.ExtKey`。扩展私钥用 `ExportXprv()` 或 `ExportXprv(Network)`。`Words` 是 `IReadOnlyList<string>`（`ReadOnlyCollection`），改元素抛 `NotSupportedException`。`Key`、`ExtKey`、`Mnemonic` 都带拒绝型 `JsonConverter`。不传 `allowPrivateKeys: true` 时，`JsonSerializer.Serialize` 和 `NKaspaJsonSerializer` 都抛 `InvalidOperationException`，文本带 WARNING，不含词或 `kprv`。`NKaspaJsonSerializer` 会把包在 `JsonException` 里的转换器异常解出来。在当前的 .NET 8 上，直接调用 `JsonSerializer.Serialize` 写出这三种类型时，拒绝本身就是 `InvalidOperationException`，消息里没有词或 `kprv`。

找零：Toccata 之后，1 KAS 输入、按 `FeeRate.MinimumRelay` 付款，剩余 250 万、500 万、1000 万 sompi 时，找零输出的 storage mass 所需手续费大于剩余金额，构建器丢掉找零并把它并进手续费。`ChangeFoldedIntoFee` 等于剩余，也就是从找零拿掉的 sompi，不是「所需手续费再加上找零」。剩余 3000 万和 5000 万 sompi 时找零输出留得下，`ChangeFoldedIntoFee` 是 0。并入还有上限：金额不超过 `max(MinimumChange, MaxFoldedChange)`，并且不超过无找零交易所需手续费的 `MaxFoldedFeeFactor` 倍。默认 `MaxFoldedChange` 是 0.1 KAS（1000 万 sompi），`MaxFoldedFeeFactor` 是 100。这两个属性是公开的，调用方可以放宽。它们是钱包安全上限，不是共识规则。放宽之后，更多 sompi 会进手续费。低于 `MinimumChange` 的 dust 仍按原规则并入，不受这个倍数限制。超过上限抛 `InvalidOperationException`：`Change would exceed the mass limit. Folding it into the fee would burn more than MaxFoldedChange allows. Split the send or add inputs so the change output fits.` 失败时 `ChangeFoldedIntoFee` 是 0。`TransactionMass` 不记录这个数。

50 KAS 的一笔 UTXO 付出 0.1 KAS、费率 150 sompi/gram：默认构建器是 Toccata 之后，storage mass 大约 10 万，低于 50 万的块限额，所需手续费大约 0.15 KAS，找零大约 49.75 KAS，会保留找零，不会烧掉 49.9 KAS。同一组数字在 `toccataActive: false`（单笔 mass 上限 100000）下，带找零的交易超过上限，找零远大于 0.1 KAS，因此抛上面的异常，不把 49.9 KAS 并进手续费。

WebSocket：`KaspaWebSocketOptions.MaxMessageBytes` 默认 `DefaultMaxMessageBytes`（16 MiB）。一个消息的所有分片加在一起，等于上限可以，多一个字节就失败。等待中的 `ExchangeAsync` 收到 `InvalidOperationException`，消息写明上限字节数，不含载荷。这种失败不自动重连。`RequestTimeout` 默认 30 秒，`ConnectTimeout` 默认 15 秒，`AutoReconnect` 默认 false，这些在上一轮已经有了。这一轮补上 `State`（Closed、Connecting、Open、Reconnecting）、`StateChanged` 和 `Reconnected`。第一次成功的 `ConnectAsync` 不触发 `Reconnected`。回调抛出的异常被忽略，避免挡住 `FailPending`。

testnet-10 的 Toccata：2026-10-31 对 `https://api-tn10.kaspa.org/info/blockdag` 做了只读 GET，没有提交交易，也没有打开 WebSocket。响应里 `networkName` 是 `kaspa-testnet-10`，`virtualDaaScore` 是 `611024731`，`blockCount` 和 `headerCount` 都是 `1503250`（这是剪枝窗口里的计数，不是链高），`pastMedianTime` 是 `1793437188174`，`sink` 是 `dd0dac7ba6b9ee3e727a9c133ea08c16da49c5e46149685bc5320d3884906cb9`。本库的测试网 Toccata 激活分是 `Network.TestnetToccataActivationDaaScore` = 467579632，出处是 rusty-kaspa `90dbf074` `params.rs` 第 785 行（tn10-toc2 / tn10-toc3 作为次要出处）。当前 master 已删除该字段。`Network.TestnetZkHardeningActivationDaaScore` = 476232000 是 tn10-toc3 的 `zk_hardening_activation`，不是 Toccata 激活分，`Network.Testnet` 不用它。611024731 高于这两个分数，所以按记录的 Toccata 分数，testnet-10 已经激活。`Network.Testnet.IsToccataActive(611024731)` 为 true。rusty-kaspa master 的 PR 1089 去掉了 `Params::toccata_activation`，mainnet、testnet、simnet、devnet 的块限额都是 compute 500000、storage 500000、transient 1000000。主网激活分是 474165565（`90dbf074` `params.rs` 第 724 行）。Simnet 和 Devnet 的激活分是 0。

## 0.1.10 再测

对照 2026-10-31 的只读克隆 `https://github.com/kaspanet/rusty-kaspa`。没有改版本号，仍是 0.1.10-alpha。库的助记词解析没有改。WebSocket 库的断开处理没有改；测试里的假服务器会真正复位套接字。

费率。rusty-kaspa `90dbf074275d60c1fe74a3491883196f110970c0` 的 `mining/src/mempool/config.rs` 第 20–23 行：`DEFAULT_MINIMUM_RELAY_TRANSACTION_FEE` 是 `100_000` sompi/kg，即 100 sompi/gram。同文件第 25–28 行：`LEGACY_MINIMUM_RELAY_TRANSACTION_FEE` 是 `1_000` sompi/kg，即 1 sompi/gram，注释写明这是 Toccata 激活之前的下限。因此 `PostToccataMinimumRelayFeeRate` 是 100，`PreToccataMinimumRelayFeeRate` 是 1。`FeeRate.MinimumRelay` 与 `MassCalculator.MinimumFeeRate` 仍是 100。强制走 Toccata 之前的策略（`MassPolicy.PreToccata`、`toccataActive: false`，或低于激活分的 DAA）会得到 1 sompi/gram，当前节点可能拒绝；线上网络用默认策略。

激活分。主网 `474165565` 来自 `90dbf074` 的 `consensus/core/src/config/params.rs` 第 724 行：`toccata_activation: ForkActivation::new(474_165_565)`。测试网 `467579632` 是同文件第 785 行：`toccata_activation: ForkActivation::new(467_579_632)`。tn10-toc2 第 783 行与 tn10-toc3 第 784 行是次要出处。当前 master 已删除该字段，第 796–800 行拒绝未知字段 `toccata_activation`，所以 `Network.WithToccataActivationDaaScore` 可以换掉这一分数。前缀、端口和扩展密钥版本不变。分数同时选择单笔 mass 上限和中继费率。`476232000` 是 tn10-toc3 `params.rs` 第 786 行的 `zk_hardening_activation: ForkActivation::new(476_232_000)`，不是 Toccata。`Network.TestnetZkHardeningActivationDaaScore` 记下这个数。`Network.Testnet` 不用它。要把它当成策略开关，传给 `WithToccataActivationDaaScore`。Simnet 和 Devnet 的激活分仍是 0。

WebSocket。托管 `HttpListener`（Linux）的 `NetworkStream` 不拥有 TCP 套接字，只调用 `WebSocket.Abort` 不会断开连接。假服务器在 `_connection` / `_socket` 存在时设置 `LingerOption(true, 0)` 并 `Close(0)`，再 `Abort`。Windows 的 HTTP.sys 没有这个字段，原有的 `Abort` 仍然关闭那一层。

找零。唯一记录并入手续费的属性是 `TransactionBuilder.ChangeFoldedIntoFee`。`TransactionMass` 不重复它。

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
| 构建并签名 | `samples/Spend` | `TransactionBuilder` | 版本 0、版本 0 的 `MustSpend` 双输入、版本 1，费用是 `SendMinimumRelayFee`。默认打印交易十六进制和 wRPC JSON。`--broadcast` 且环境变量 `NKASPA_MNEMONIC` 都有，并且还传入 `--to` 与 `--amount` 时才提交。提交前检查付款输出、最低中继费和 `Transaction.Verify`。`--from` 与 `--max-fee-sompi` 可选。 |
| PSKT 2-of-2 | `samples/PsktMultisig` | BIP174 `PartiallySignedTransaction`（不移植） | `NKaspa.PSKT.PSKT`：创建、双方 `Sign`、`Combine`、`Finalize`、`ExtractTransaction`。 |
| 离线消息签名 | `samples/SignMessage` | `Key.SignMessage`。BIP322 仍无需实现 | 对 UTF-8 的 BLAKE2b-256 调用 `Key.SignSchnorr` / `Key.SignECDSA`，再用 `PubKey.Verify` / `PubKeyECDSA.Verify`。不是比特币消息前缀。 |
| 教程片段 | `samples/Tutorials` | 短示例 | `AddressTutorial` 与 `SendTutorial.RunOffline`，对应 `docs/tutorials/`。编译这些片段，不开套接字，不调用 `SubmitTransactionAsync`。`SendOnTestnetAsync` 只为编译广播路径，默认测试网，示例入口不调用它。 |
| JSON | `samples/Json` | `NBitcoin.JsonConverters.Serializer` | `NKaspaJsonSerializer` 与 `AddNKaspaConverters(network)`。版本 0 / 版本 1（`computeBudget`、`covenant`）、`scriptPublicKey` 的版本前缀十六进制。私钥默认拒绝。 |

## 仍未完成

- `OP_ZKPRECOMPILE` 的 BN254 Groth16 配对、压缩点的曲线与子群检查，以及 RISC0 succinct STARK。结构检查已经执行。长度正确但不在曲线上的点会先计 gamma 费，再返回未实现。引擎不接受证明。
- 与正在运行的 rusty-kaspa 做 PSKT 字节对照。上游测试没有钉死向量。
- 版本 1 compute budget 进入 PSKT。rusty-kaspa 的 `unsigned_tx` 这里仍是 TODO。
- NuGet 发布。这一版只改版本号，不推包。
- Newtonsoft.Json 适配。序列化只走 System.Text.Json，没有加这个包。
- 未合并的 transient mass 改动（PR 1141）以及未能从源码确认的测试网激活 DAA。
- 示例里的 `--broadcast` 已在公共主网节点提交过一笔。wRPC JSON 必须写出 `verboseData` 和没有 covenant 时的 `covenant: null`，否则 `submitTransaction` 会被拒绝。提交前会检查派生地址、付款输出、最低中继费和 `Transaction.Verify`。
- `ConsensusSeqCommitAccessor` 仍然不是节点。默认请求上限 256，到顶就抛异常，不会扫完整个 finality 窗口，也不会把缺块当成 `BlockAlreadyPruned`。链向前增长不再误判为重组。超前超过 32 步时用最新的 `isChainBlock`，不逐块追尖端。`StableDepth` 默认 20。重组重试默认 3 次。`WrpcSeqCommitAccessor` 的视点仍是 DAG sink。

## 下一步计划

- 有可用的验证器之后，再补 BN254 配对和 RISC0 STARK。在此之前保持未实现。
- 有上游向量之后，钉死多公钥 map 和多 xpub 的 JSON 键序。
- NuGet 打包准备。示例已经在 `samples/`，默认不广播。
