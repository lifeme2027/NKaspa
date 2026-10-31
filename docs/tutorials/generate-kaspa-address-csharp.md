# Generate a Kaspa address in C#

How to Generate a Kaspa Address in C# (.NET) with NKaspa.

To generate a Kaspa address in C#, install the NKaspa NuGet package and call `Key.Create().GetAddress(Network.Mainnet)`. For an HD wallet, derive `KeyPath.KaspaReceive(0)` (`m/44'/111111'/0'/0/0`) from `new Mnemonic(...).DeriveExtKey()`. Addresses look like `kaspa:qr…` on mainnet and `kaspatest:` on testnet.

This repository is **0.1.11-alpha**. The NuGet index lists **0.1.10-alpha** as the newest published package (`0.1.9-alpha` is also published). The snippets below track the repository. `Key.Create` and `GetAddress` are older than 0.1.10. `Mnemonic.ToPhrase`, `Mnemonic.Parse`, `ParseValidated`, `ExtKey.ExportXprv`, and `GetWif` require 0.1.10-alpha. `KaspaRpcClient` is not used here. It requires 0.1.11-alpha and is covered in [Send a Kaspa transaction in .NET](send-kaspa-transaction-dotnet.md).

The same methods are compiled by `samples/Tutorials` (`AddressTutorial`). `dotnet run --project samples/Tutorials` runs them offline. Output from that command is pasted below.

## Install

```bash
dotnet add package NKaspa --prerelease
```

The library targets `net8.0` and `netstandard2.1`.

## Address from a new random key

`Key` implements `IDisposable`. Dispose it when the secret is no longer needed.

```csharp
using NKaspa;

using var key = Key.Create();
KaspaPubKeyAddress address = key.GetAddress(Network.Mainnet);
Console.WriteLine(address);
Console.WriteLine(KaspaAddress.IsValid(address.ToString()));
```

`GetAddress` returns a Schnorr public-key address (`KaspaPubKeyAddress`). The payload is the 32-byte x-only key. `Key.Create` draws a fresh secret, so the printed address changes every run. `samples/Tutorials` only prints the prefix and whether `IsValid` accepts it:

```text
random-prefix: kaspa
random-valid: true
```

## Address from a 12-word or 24-word mnemonic

Kaspa's default account path is BIP44 with coin type `111111`: `m/44'/111111'/account'/chain/index`. `KeyPath.KaspaReceive(index)` is chain `0`. `KeyPath.KaspaChange(index)` is chain `1`. The account argument defaults to `0`.

`WordCount.Twelve` and `WordCount.TwentyFour` both exist. The run below uses the public BIP39 test sentence `abandon … about` (12 words). A 24-word mnemonic is `new Mnemonic(Wordlist.English, WordCount.TwentyFour)`. This page does not print a 24-word address, because that sentence would be a new secret.

`ToPhrase()` requires 0.1.10-alpha. `Mnemonic.Words` is an `IReadOnlyList<string>` in that version. `DeriveExtKey` and `DeriveSeed` throw when the checksum is wrong, unless you pass `allowInvalidChecksum: true`. `Parse` and `ParseValidated` throw `FormatException` with the message `BIP39 checksum does not match.`

```csharp
using NKaspa;

var mnemonic = new Mnemonic(
    "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
    Wordlist.English);
Console.WriteLine(mnemonic.ToPhrase());
using var master = mnemonic.DeriveExtKey();
using var receive = master.Derive(KeyPath.KaspaReceive(0));
using var change = master.Derive(KeyPath.KaspaChange(0));
var address = receive.PrivateKey.GetAddress(Network.Mainnet);
var changeAddress = change.PrivateKey.GetAddress(Network.Mainnet);
Console.WriteLine(KeyPath.KaspaReceive(0));
Console.WriteLine(KeyPath.KaspaChange(0));
Console.WriteLine(address);
Console.WriteLine(changeAddress);
```

`dotnet run --project samples/Tutorials` printed:

```text
phrase: abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about
path: m/44'/111111'/0'/0/0
change-path: m/44'/111111'/0'/1/0
mainnet: kaspa:qqd6e65yefepe9wk0m9vuxdufxd80sphy67gwwd0vdaumzdt4tc9s3qt0lqeh
change: kaspa:qqnkyg4hha30lnjystpugz42yh3paurdylxfrsshk3jfstgy95elu2a8l3jcs
```

The same sentence through `samples/Mnemonic` also prints indexes 1 and 2, plus the master extended public keys (`kpub` on mainnet, `ktub` on testnet). That `kpub` is the master key after `Neuter()`, not the account key in the next section. Command:

```bash
dotnet run --project samples/Mnemonic -- abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about
```

Output of that command:

```text
mnemonic: abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about
kpub: kpub2C2CKMtB3F5r4C5suJvJMDExAKqjbCGY25Lg237FgAcBfqnNJf6QNpmYUhecDXVfZorNvF3FAPfq5AYpoLsGkhb3rWeVnf1Vc7bSYxmcFfp
ktub: ktub1vi816jr1DomEFJjj57Bfy3uQyixVT1ZuKdQYCRZqxmAN8omyW23x3daG8kcJUs9QbUqt1yqBUpknF3PKgMvDjmQrv6DUxJuBrn19nqCYe1
path[0]: m/44'/111111'/0'/0/0
mainnet[0]: kaspa:qqd6e65yefepe9wk0m9vuxdufxd80sphy67gwwd0vdaumzdt4tc9s3qt0lqeh
testnet[0]: kaspatest:qqd6e65yefepe9wk0m9vuxdufxd80sphy67gwwd0vdaumzdt4tc9ssxd5s7gn
path[1]: m/44'/111111'/0'/0/1
mainnet[1]: kaspa:qp6r0d88yj4fazlj057wc35245jfgs87n9jn6nahfg223996dfukvgpgq6pcp
testnet[1]: kaspatest:qp6r0d88yj4fazlj057wc35245jfgs87n9jn6nahfg223996dfukvf8wm4lf9
path[2]: m/44'/111111'/0'/0/2
mainnet[2]: kaspa:qrjld004k4aa5m4savjndusd56jk739pl5d90y6j4xuw2dyyjqrx6aqflp9ak
testnet[2]: kaspatest:qrjld004k4aa5m4savjndusd56jk739pl5d90y6j4xuw2dyyjqrx6ux0ywmvj
ok
```

These are NKaspa's own strings for that public test sentence. The repository has no pinned external vector of the form "this mnemonic equals this kaspa address" for Kaspium, KDX, or the Kaspa NG wallet. Do not treat the lines above as a claim that those wallets print the same address.

## Watch-only

Hardened steps (`44'`, `111111'`, `account'`) stay on `ExtKey`. `Neuter()` on the master, then `Derive(KeyPath.KaspaReceive(0))`, throws because that path is hardened. Derive the account with the private key, neuter that account key, then derive the public path `m/0/0` or `m/1/index`.

`ExtPubKey.ToString(Network)` and `ExportXprv` / `GetWif` require 0.1.10-alpha for the export helpers named in the version note. `Neuter` and `GetAddress` on `ExtPubKey` are the watch-only calls.

```csharp
using NKaspa;

var mnemonic = new Mnemonic(
    "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
    Wordlist.English);
using var master = mnemonic.DeriveExtKey();
using var account = master.Derive(KeyPath.KaspaAccount(0));
ExtPubKey watch = account.Neuter();
ExtPubKey child = watch.Derive(new KeyPath("m/0/0"));
var fromPublic = child.GetAddress(Network.Mainnet);
Console.WriteLine(fromPublic);
Console.WriteLine(watch.ToString(Network.Mainnet));
Console.WriteLine(watch.ToString(Network.Testnet));
```

`samples/Tutorials` checks that this address equals the private `KaspaReceive(0)` address and prints:

```text
watch: kaspa:qqd6e65yefepe9wk0m9vuxdufxd80sphy67gwwd0vdaumzdt4tc9s3qt0lqeh
kpub: kpub2JXpdok4yzBUdLiywgMbE36ixpEnffDhG31kJpRextvhpBQgBJVPcQJ7A99JryUvi8JNBcnzGoiu4qbGcvCfpY1T2RWndCaUjJqhwDSUNw8
ktub: ktub23DkKYbjwxuPoPwqmSYUYnugDU81Zuxj9HJUpyjy8h5gWUS5r9R3BdA8waFJwvrQYuvq9PjaHtspmv5q9FhKHaBp2pxWKVstK42GY4kByCU
```

That `kpub` is account `0`, `m/44'/111111'/0'`. It is not the master `kpub` from `samples/Mnemonic`.

## Schnorr, ECDSA, and script-hash addresses

| Call | Type | Payload |
| --- | --- | --- |
| `Key.GetAddress(network)` | `KaspaPubKeyAddress` | 32-byte Schnorr x-only, version 0 |
| `Key.GetEcdsaAddress(network)` | `KaspaPubKeyEcdsaAddress` | 33-byte compressed ECDSA key |
| `KaspaScriptHashAddress` | script hash | BLAKE2b-256, used for P2SH |

The public BIP-340 test secret `b7e151628aed2a6abf7158809cf4f3c762e7160f38b4da56a784d9045190cfef` is the secret whose x-only key is pinned by `KeyTests`. `GetAddress(Network.Mainnet)` returns the address also printed by `samples/Address` and shared with the kaswallet and kaspadotnet address vectors:

`kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva`

```csharp
using NKaspa;

using var key = Key.Parse("b7e151628aed2a6abf7158809cf4f3c762e7160f38b4da56a784d9045190cfef");
KaspaPubKeyAddress schnorr = key.GetAddress(Network.Mainnet);
KaspaPubKeyEcdsaAddress ecdsa = key.GetEcdsaAddress(Network.Testnet);
var scriptHash = KaspaAddress.Parse("kaspa:pqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqlmtfk4dg");
Console.WriteLine(schnorr);
Console.WriteLine(ecdsa);
Console.WriteLine(scriptHash.GetType().Name);
```

`samples/Tutorials` printed:

```text
schnorr: kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva
ecdsa: kaspatest:qypdluwh0u4xw8zlxcvrwfkmydqmuk874cw69hkwmppjgrmm2q46vkg538dsecl
script-hash-type: KaspaScriptHashAddress
```

`samples/Address` uses a different ECDSA test address, `kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02`. That string is not `GetEcdsaAddress` of the `b7e15162…` key.

A Kaspa address is cashaddr-style bech32: a prefix, a colon, a 5-bit payload, and an 8-character checksum. The charset is `qpzry9x8gf2tvdw0s3jn54khce6mua7l`. It is not Base58, not BIP-173 bech32, and not HASH160. The Schnorr script is `OP_DATA_32` plus the x-only key plus `OP_CHECKSIG`. Bitcoin's P2PKH and P2WPKH addresses are a different encoding of a different payload, so the same secret does not produce a matching Bitcoin address.

## Mainnet, testnet, simnet, and devnet

| Network | Prefix |
| --- | --- |
| `Network.Mainnet` | `kaspa` |
| `Network.Testnet` | `kaspatest` |
| `Network.Simnet` | `kaspasim` |
| `Network.Devnet` | `kaspadev` |

```csharp
using var key = Key.Parse("b7e151628aed2a6abf7158809cf4f3c762e7160f38b4da56a784d9045190cfef");
Console.WriteLine(key.GetAddress(Network.Mainnet).Network.AddressPrefix);
Console.WriteLine(key.GetAddress(Network.Testnet).Network.AddressPrefix);
Console.WriteLine(key.GetAddress(Network.Simnet).Network.AddressPrefix);
Console.WriteLine(key.GetAddress(Network.Devnet).Network.AddressPrefix);
```

Printed by `samples/Tutorials`:

```text
prefix-mainnet: kaspa
prefix-testnet: kaspatest
prefix-simnet: kaspasim
prefix-devnet: kaspadev
```

A testnet address is `key.GetAddress(Network.Testnet)`.

Extended-key text prefixes are separate from the address prefix. Mainnet is `kprv` / `kpub`. Testnet, simnet, and devnet are `ktrv` / `ktub`.

## Parse and validate

`KaspaAddress.Parse(string)` accepts any known network. `Parse(string, Network)` and `TryParse(string, Network, out …)` reject a prefix that is not that network. `TryParse` returns false instead of throwing. `IsValid` is the predicate. `ScriptPubKey` is the locking script. `Network` is the network implied by the prefix.

```csharp
using NKaspa;

const string text = "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva";
if (!KaspaAddress.IsValid(text) || !KaspaAddress.TryParse(text, out var parsed) || parsed == null)
{
    return;
}

var again = KaspaAddress.Parse(text);
var onMainnet = KaspaAddress.Parse(text, Network.Mainnet);
Console.WriteLine(again);
Console.WriteLine(onMainnet.Network.Name);
Console.WriteLine(parsed.ScriptPubKey.ToHex());
Console.WriteLine(KaspaAddress.TryParse(text, Network.Testnet, out _));
```

`samples/Tutorials` printed:

```text
parsed: kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva
network: mainnet
script-hex: 20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac
testnet-parse: false
```

`samples/Address` also rejects a bad checksum. Its printed rejection is `Kaspa address checksum does not match the prefix and payload.` The same run reports `network-mismatch: kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02 is not mainnet`. Full output of `dotnet run --project samples/Address`:

```text
address: kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva
network: mainnet
type: KaspaPubKeyAddress
version: PubKey
script-version: 0
script: OP_DATA_32 dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659 OP_CHECKSIG
script-hex: 20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac
valid: true
address: kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02
network: testnet
type: KaspaPubKeyEcdsaAddress
version: PubKeyECDSA
script-version: 0
script: OP_DATA_33 02ba01fc5f4e9d9879599c69a3dafd835a725e5f2e934e9322ecd3af190ab0f60e OP_CHECKSIGECDSA
script-hex: 2102ba01fc5f4e9d9879599c69a3dafd835a725e5f2e934e9322ecd3af190ab0f60eab
valid: true
address: kaspa:pqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqlmtfk4dg
network: mainnet
type: KaspaScriptHashAddress
version: ScriptHash
script-version: 0
script: OP_BLAKE2B OP_DATA_32 0000000000000000000000000000000000000000000000000000000000000000 OP_EQUAL
script-hex: aa20000000000000000000000000000000000000000000000000000000000000000087
valid: true
address: kaspa:qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqkx9awp4l
valid: false
rejected: Kaspa address checksum does not match the prefix and payload.
network-mismatch: kaspatest:qypt5q0uta8fmxretxwxng76lkp45uj7tuhfxn5nytkd8tcep2c0vrsxarh9t02 is not mainnet
ok
```

To check a user-entered address for one network, call `KaspaAddress.TryParse(text, expectedNetwork, out var address)` and also `IsValid` if you only need a boolean. A `kaspatest:` string fails `Parse(text, Network.Mainnet)`.

## Security notes

Wrap `Key` and `ExtKey` in `using`. `ExtKey.ToString()` returns `NKaspa.ExtKey`. The extended private key is `ExportXprv()`, `ExportXprv(network)`, `GetWif(network)`, `ToString(network)`, or `ToBase58()`. `Mnemonic.ToString()` returns `NKaspa.Mnemonic`. The sentence is `ToPhrase()`.

Default `JsonSerializer.Serialize` refuses `Key`, `ExtKey`, and `Mnemonic`. The exception does not contain the secret. Opt in only with `NKaspaJsonSerializer` and `allowPrivateKeys: true`, and do not turn that on for logs. `samples/Tutorials` prints `json-key: refused` for `JsonSerializer.Serialize` on the public test key.

## FAQ

**Is there an official Kaspa C# SDK?** Official SDKs are Rust, WASM, and Python. NKaspa is a community .NET library (MIT, see [LICENSE](../../LICENSE)). It is alpha.

**Can I derive addresses without the private key?** Yes. Derive the hardened account with `ExtKey`, call `Neuter()`, then `ExtPubKey.Derive` for `m/0/index` or `m/1/index`, then `GetAddress`.

**What are kprv and kpub?** They are the mainnet extended-key prefixes. Testnet, simnet, and devnet use `ktrv` and `ktub`.

**Which derivation path does Kaspa use?** `m/44'/111111'/0'/0/0`, which is `KeyPath.KaspaReceive(0)`.

**Will this match Kaspium, KDX, or Kaspa NG for the same mnemonic?** This repository does not pin that comparison. The addresses above are what NKaspa prints. The `qr0lr4…` string is pinned for the public key scalar `b7e15162…`, including the shared kaswallet and kaspadotnet address vector. It is not a mnemonic vector.

## Related

- [README](../../README.md) usage and FAQ
- [samples/Address](../../samples/Address)
- [samples/Mnemonic](../../samples/Mnemonic)
- [samples/Tutorials](../../samples/Tutorials)
- [Send a Kaspa transaction in .NET](send-kaspa-transaction-dotnet.md)
- [NBitcoin mapping](../NBitcoin-Mapping.md) and [English](../NBitcoin-Mapping.en.md)
