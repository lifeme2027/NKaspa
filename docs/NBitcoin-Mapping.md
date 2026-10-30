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
| `Key` / `PubKey` / `BitcoinSecret` | — | 计划中 | 第二阶段。曲线运算用 `NBitcoin.Secp256k1`。 |
| `Script` | `ScriptPubKey`（最简） | 已实现（最简） | 只生成标准锁定脚本。完整 `Script` 与执行在第二阶段。 |
| `Transaction` / `TxIn` / `TxOut` / `OutPoint` | — | 计划中 | 第二阶段，含 Kaspa 序列化。 |
| `TransactionBuilder` / `Coin` | — | 计划中 | 第三阶段的交易构建器。 |
| `TransactionSignature` / sighash | — | 计划中 | 第二阶段做 Kaspa sighash（Schnorr，BLAKE2b），不是比特币 SIGHASH。 |
| `uint256` | `uint256` | 已实现 | 可解析、比较、按 32 字节序列化。显示不反转字节，见下文。 |
| `uint160` / `uint128` | — | 无需实现 | 地址和交易哈希都是 32 字节。 |
| `Network` | `Network` | 已实现 | 精简为 Mainnet、Testnet、Simnet、Devnet 与地址前缀。 |
| `Block` / `BlockHeader` | — | 计划中 | Kaspa 区块是 GHOSTDAG 块 DAG，不是比特币单链头。字段要另定，不能照搬。 |
| `Money` / `FeeRate` / `Target` | — | 计划中 | 金额单位是 sompi。工作量目标与 blue score 的规则不同于比特币。 |
| `BitcoinStream` | — | 计划中 | 第二阶段随交易序列化一起做 Kaspa 的读写，而不是比特币的 witness 编码。 |

`uint256` 的约定：`ToBytes()` 与 `ToString()` 都是 Kaspa 线上顺序，也就是 BLAKE2b 摘要的原始字节，RPC 十六进制也是这个顺序。字节 0 是摘要的第一个字节。NBitcoin 的 `ToString()` 会把这 32 字节反转，让十六进制看起来像大端整数。数值比较把这 32 字节当成小端整数（字节 0 是最低有效字节），这和 Kaspa 把哈希当工作量整数比较的方向一致。

## DataEncoders

| NBitcoin | NKaspa | 状态 | 原因 |
| --- | --- | --- | --- |
| `DataEncoder` | `DataEncoder` | 已实现 | 字节与文本的基类。 |
| `Encoders` | `Encoders` | 已实现 | 目前提供 `Hex` 和 `KaspaBech32`。 |
| `HexEncoder` | `HexEncoder` | 已实现 | 小写十六进制。 |
| `Bech32Encoder` | `KaspaBech32Encoder` | 已实现 | Kaspa 用的是 cashaddr 风格，8 字符（40 位）校验和，字符表 `qpzry9x8gf2tvdw0s3jn54khce6mua7l`。不是 BIP-173，也不是 bech32m。 |
| `Base58Encoder` / `Base58CheckEncoder` | — | 无需实现 | Kaspa 地址不用 Base58。 |
| `Base32Encoder` / `Base64Encoder` / `ASCIIEncoder` | — | 无需实现 | 地址路径不需要。真要做通用编码时再加，不作为共识的一部分。 |

## Crypto

| NBitcoin | NKaspa | 状态 | 原因 |
| --- | --- | --- | --- |
| `Hashes` | `Hashes` | 已实现 | 提供 `Blake2b256`。没有双 SHA256，也没有 HASH160。 |
| （无同名类型） | `Blake2b` | 已实现 | RFC 7693。Kaspa 的交易号、区块哈希、脚本哈希都是不带密钥的 BLAKE2b-256。 |
| `Hashes.DoubleSHA256` | — | 无需实现 | 不是 Kaspa 共识哈希。 |
| `Hashes.Hash160` / RIPEMD160 | — | 无需实现 | 地址不用 HASH160。 |
| `SchnorrSignature` / `ECDSASignature` | — | 计划中 | 第二阶段。签名算法走 `NBitcoin.Secp256k1` 的 BIP-340 Schnorr，以及 ECDSA 地址所需的 ECDSA。 |
| `DeterministicECDSA` | — | 计划中 | 若 ECDSA 签名需要 RFC 6979，用 Secp256k1 包里的实现，不另写一份。 |

## Secp256k1

NBitcoin 目录 `Secp256k1/` 含 `ECPrivKey`、`ECPubKey`、`ECXOnlyPubKey`、`SecpSchnorrSignature`、`SecpECDSASignature`，以及 MuSig、adaptor、recovery。

| 状态 | 原因 |
| --- | --- |
| 无需把源码搬进 NKaspa | 椭圆曲线用 NuGet 包 `NBitcoin.Secp256k1`（MIT，这一版引用 3.1.6）。包已经在工程里，密钥和签名类型第二阶段再包一层。 |
| MuSig / adaptor / recovery | 计划外，除非 Kaspa 钱包明确需要。 |

## BIP32

`ExtKey`、`ExtPubKey`、`KeyPath`、`RootedKeyPath`、`HDFingerPrint`、`IHDKey`、`BitcoinExtKey`。

| 状态 | 原因 |
| --- | --- |
| 计划中 | 第三阶段的 HD 钱包。派生仍可沿用 BIP32 的路径和 HMAC-SHA512，地址编码换成 Kaspa。这一版没有助记词，也没有扩展密钥。 |

## BIP39

`Mnemonic`、`Wordlist`、`Language`、`KDTable`、`IWordlistSource`。

| 状态 | 原因 |
| --- | --- |
| 计划中 | 第三阶段。Kaspa 钱包使用 BIP39 助记词，但派生路径和地址前缀是 Kaspa 的。 |

## Protocol

`Node`、`Message`、`Payload`、`VersionPayload`、`TxPayload`、`BlockPayload`、`InventoryVector`、`NetworkAddress`、`VarInt`、`VarString`，以及 compact block、feefilter、wtxid relay、witness 相关载荷。

| 状态 | 原因 |
| --- | --- |
| 无需原样实现 | 这些是比特币 P2P 消息。Kaspa 节点协议不同，没有 witness、compact block 那一套比特币载荷。 |
| 计划中（另写） | 若以后做节点客户端，按 Kaspa P2P 写，不移植 `HaveWitnessPayload`、`CmpctBlockPayload` 这类类型。第三阶段先做 RPC，而不是完整 P2P 节点。 |

`VarInt` 一类长度编码如果和 Kaspa 交易序列化重合，会在第二阶段的序列化里按 Kaspa 规则实现，不从比特币 `Protocol` 目录拷贝。

## Policy

`StandardTransactionPolicy`、`MinerTransactionPolicy`、`ITransactionPolicy`。

| 状态 | 原因 |
| --- | --- |
| 无需原样实现 | 比特币标准脚本（P2PKH、P2WPKH、multisig 计数）对不上 Kaspa。 |
| 计划中 | Kaspa 自己的标准脚本类是 pubkey、pubkey ECDSA、script hash。可以在脚本引擎之后再加策略检查。 |

## RPC

`RPCClient`、`RPCClient.Wallet`、`RestClient`，以及 `GetBlockRPCResponse`、`ListUnspentOptions`、`UnspentCoin` 等响应类型。

| 状态 | 原因 |
| --- | --- |
| 计划中 | 第三阶段做 Kaspa RPC / wRPC 客户端。方法名不会照搬 `getblock`、`fundrawtransaction` 这类比特币 RPC。 |
| PSBT 相关的 RPC 参数 | 无需实现。见 BIP174。 |

## Payment

`BitcoinUrlBuilder` 以及内部的 URI 辅助类型。

| 状态 | 原因 |
| --- | --- |
| 计划中 | 可以以后做 `kaspa:` 支付 URI。比特币 BIP21 的查询参数不能直接拿来用。 |

## BIP174 / BIP370

`PartiallySignedTransaction`、`PSBTInput`、`PSBTOutput`、`PSBTCoin`，以及 BIP370 的 PSBT v2。

| 状态 | 原因 |
| --- | --- |
| 无需实现 | Kaspa 没有原生 PSBT。交易构造走 Kaspa 的输入、输出和 sighash。若将来要一种未签名交易交换格式，应单独设计，而不是移植 BIP174。 |

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
| `JsonConverters` | 各 `JsonConverter` | 计划中 | 等交易和 RPC 类型稳定后再加序列化转换器。 |
| `WalletPolicies` / miniscript | 策略描述 | 无需实现 | 比特币 miniscript 与输出描述符对不上当前 Kaspa 脚本。 |
| `BuilderExtensions` | 交易构建扩展 | 计划中 | 随第三阶段交易构建器。 |
| `Socks` | `SocksHelper` | 计划外 | 与共识无关。做节点连接时再考虑。 |
| `Logging` | `Logs` | 无需实现 | 库不绑定 NBitcoin 的日志门面。 |
| `BouncyCastle` | 摘要与大整数 | 无需实现 | 不把这套源码带进来。哈希用自带 BLAKE2b，曲线用 Secp256k1 包。 |

## 当前进度

0.1.0-alpha，第一阶段：

- 解决方案：`NKaspa.sln`，类库 `src/NKaspa`（`net8.0`、`netstandard2.1`），测试 `tests/NKaspa.Tests`（xUnit，`net8.0`）。
- 已实现类型：`DataEncoder`、`Encoders`、`HexEncoder`、`KaspaBech32Encoder`、`Blake2b`、`Hashes`、`uint256`、`Network`、`ScriptPubKey`、`KaspaAddress`、`KaspaPubKeyAddress`、`KaspaPubKeyEcdsaAddress`、`KaspaScriptHashAddress`、`KaspaAddressVersion`。
- 地址测试使用 kaswallet / kaspadotnet 里的已知 Kaspa 地址，并包含 rusty-kaspa 的短 cashaddr 向量。BLAKE2b 测试包含 RFC 7693 的 BLAKE2b-512("abc") 以及公开的 BLAKE2b-256 向量。
- 依赖：`NBitcoin.Secp256k1` 3.1.6 已引用，这一阶段还没有密钥或签名代码去调用它。

还没有做：密钥与 Schnorr/ECDSA 签名、完整脚本、交易与 sighash、BIP32/BIP39、交易构建器、RPC。

## 下一步计划

第二阶段：

- `Key` / `PubKey`，Schnorr（BIP-340，经 `NBitcoin.Secp256k1`）与 ECDSA 签名。
- `Script` / 完整的 `ScriptPubKey` 解析与标准脚本识别。
- `Transaction`、`TxIn`、`TxOut`、`OutPoint` 与 Kaspa 序列化。
- Kaspa sighash。

第三阶段：

- BIP32 / BIP39 HD 钱包。
- 交易构建器。
- Kaspa RPC 客户端。
