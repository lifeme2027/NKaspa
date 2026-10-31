# Send a Kaspa transaction in .NET

How to Send a Kaspa (KAS) Transaction in C# / .NET.

In .NET, build a Kaspa transaction with NKaspa's `TransactionBuilder`: add keys and coins (UTXOs from `GetUtxosByAddressesAsync`), call `Send(address, Money.Coins(x))`, `SetChange`, `SendMinimumRelayFee()`, then `BuildTransaction(true)`, and broadcast with `SubmitTransactionAsync` on `IKaspaRpcClient`. A standard one-input Schnorr payment with change costs 203,600 sompi (0.002036 KAS) at the minimum relay rate. The mass of that shape is 2036 grams.

Nothing in the library broadcasts by itself. The offline program below does not open a socket. The broadcast method is compiled so the calls stay honest, and `samples/Tutorials` does not call it. Do not pass `--broadcast` to `samples/Spend` unless you intend to spend.

This repository is **0.1.11-alpha**. NuGet's newest published package is **0.1.10-alpha** (`0.1.9-alpha` is also published). `KaspaRpcClient`, Borsh, path-based Auto encoding, gRPC-port rejection, and `KaspaRpcEncodingMismatchException` require 0.1.11-alpha. `KaspaWebSocketOptions`, `MassPolicy`, `Network.IsToccataActive`, `MassPolicyAt`, `TransactionBuilder(network, virtualDaaScore)`, the `toccataActive:` constructor, and the folded-change cap require 0.1.10-alpha. The basic `AddKeys` / `AddCoins` / `Send` / `SetChange` / `SendMinimumRelayFee` / `BuildTransaction` chain is older.

`IKaspaRpcClient` is declared in `src/NKaspa/RPC/RpcModels.cs`, namespace `NKaspa.RPC`.

## Prerequisites

- The NKaspa package: `dotnet add package NKaspa --prerelease` (`net8.0` and `netstandard2.1`).
- A funded key. Use testnet first.
- A wRPC endpoint. `samples/Spend` documents `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json` as `DefaultUrl`. That host is a third-party public node. This page does not promise that it stays up. NKaspa speaks wRPC JSON and wRPC Borsh over WebSocket. It does not speak gRPC.

The tutorial broadcast path defaults to `Network.Testnet`. A mainnet override prints a warning and still requires `NKASPA_ALLOW_MAINNET=yes`. **Mainnet spends real KAS.**

## Offline build and sign

This is the shape in `samples/Tutorials` (`SendTutorial.RunOffline`). The secrets are public test scalars already used by the unit tests (`000…0003` and the BIP-340 vector `b7e15162…`). The coins are local fakes. The addresses are mainnet strings so the script bytes match the known vectors. The method never connects, and it prints `broadcast: no`.

```csharp
using NKaspa;
using NKaspa.Policy;

using var payer = Key.Parse("0000000000000000000000000000000000000000000000000000000000000003");
using var payeeKey = Key.Parse("b7e151628aed2a6abf7158809cf4f3c762e7160f38b4da56a784d9045190cfef");
var payee = payeeKey.GetAddress(Network.Mainnet);
var change = payer.GetAddress(Network.Mainnet);
var coin = new Coin(
    new OutPoint(new uint256(70), 0),
    new UtxoEntry(100_000_000UL, payer.PubKey.GetScriptPubKey(), 1, false));

var tx = new TransactionBuilder(Network.Mainnet)
    .AddKeys(payer)
    .AddCoins(coin)
    .Send(payee, Money.Coins(0.2m))
    .SetChange(change)
    .SendMinimumRelayFee()
    .BuildTransaction(true);

var spent = new[] { coin.Utxo };
bool inputOk = Script.VerifyScript(tx.Inputs[0].SignatureScript, coin.Utxo.ScriptPublicKey, tx, 0, spent);
bool allOk = tx.Verify(spent);
```

Two more shapes in the same method: `MustSpend` on two 0.5 KAS coins (outpoints 71 and 72), and `SetVersion(1)` paying 0.1 KAS. `dotnet run --project samples/Tutorials` printed:

```text
broadcast: no
paid: true
input-ok: true
verified: true
fee-sompi: 203600
relay-mass: 2036
floor: 0.002036 KAS
must-spend-inputs: 2
version: 1
offline-ok
ok
```

`floor` is `TransactionBuilder.EstimateFee(FeeRate.MinimumRelay, MassCalculator.StandardSchnorrPaymentMass)`.

`samples/Spend` builds the same three fee shapes with its own fake outpoints and prints hex and wRPC JSON. Run it with no `--broadcast`:

```bash
dotnet run --project samples/Spend
```

One run of that command printed the text below. A second run printed the same `id`, `fee-sompi`, `fee`, `relay-mass`, `version`, `inputs`, `must-spend`, and `compute-budget` lines. `Key.SignSchnorr` draws fresh auxiliary bytes when the caller does not pass 32 bytes, so `hex` and `signatureScript` are from this run. They are not a pinned signature vector. The transaction id stayed the same on the second run.

```text
broadcast: no
sample: version-0
version: 0
inputs: 1
must-spend: false
fee-sompi: 203600
fee: 0.002036 KAS
relay-mass: 2036
compute-budget: none
id: 51bcd913759477876d98cfaa314f49d777c6b382ad6f10ad6b11c7dacbc99814
hex: 00000100000000000000460000000000000000000000000000000000000000000000000000000000000000000000420000000000000041f0b5e0f9da666b1d94be0b761a4c121c68c44eb4355cda605546381fa731e79b5c46a5052f79f24c2cbd43c651032e2b25ddd0be0d46148617e16e18b8d1870e010100000000000000000200000000000000002d3101000000000000220000000000000020dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659acb098c104000000000000220000000000000020f9308a019258c31049344f85f89d5229b531c845836f99b08601f113bce036f9ac0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
json: {"version":0,"inputs":[{"previousOutpoint":{"transactionId":"4600000000000000000000000000000000000000000000000000000000000000","index":0},"signatureScript":"41f0b5e0f9da666b1d94be0b761a4c121c68c44eb4355cda605546381fa731e79b5c46a5052f79f24c2cbd43c651032e2b25ddd0be0d46148617e16e18b8d1870e01","sequence":0,"sigOpCount":1,"verboseData":null}],"outputs":[{"value":20000000,"scriptPublicKey":{"version":0,"script":"20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac"},"covenant":null,"verboseData":null},{"value":79796400,"scriptPublicKey":{"version":0,"script":"20f9308a019258c31049344f85f89d5229b531c845836f99b08601f113bce036f9ac"},"covenant":null,"verboseData":null}],"lockTime":0,"subnetworkId":"0000000000000000000000000000000000000000","gas":0,"payload":"","mass":52531,"storageMass":52531,"verboseData":null}
verified: true
sample: must-spend
version: 0
inputs: 2
must-spend: true
fee-sompi: 315400
fee: 0.003154 KAS
relay-mass: 3154
compute-budget: none
id: d4aa8ead5289404ef6ca46eb878a4c097082e3963d52071fdde5adea8e699631
hex: 000002000000000000004700000000000000000000000000000000000000000000000000000000000000000000004200000000000000411f7525c5e1e910da04482034b1976dd71a3015488f56911fab494f2de5168696320f112bc408f61524d42b68e04cd0ec611b221d5e2499d95f75f68ff9910a2501010000000000000000480000000000000000000000000000000000000000000000000000000000000000000000420000000000000041e24bc87905e08f1c5c1567ee8f7db310d180f9ed448b89ad87a1225f6a94d57d6eb7e6780bdcff71436278d1e3826f0065661c1fde8cdcd2ec1c01c6f8476f37010100000000000000000200000000000000002d3101000000000000220000000000000020dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659acf8e3bf04000000000000220000000000000020f9308a019258c31049344f85f89d5229b531c845836f99b08601f113bce036f9ac0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
json: {"version":0,"inputs":[{"previousOutpoint":{"transactionId":"4700000000000000000000000000000000000000000000000000000000000000","index":0},"signatureScript":"411f7525c5e1e910da04482034b1976dd71a3015488f56911fab494f2de5168696320f112bc408f61524d42b68e04cd0ec611b221d5e2499d95f75f68ff9910a2501","sequence":0,"sigOpCount":1,"verboseData":null},{"previousOutpoint":{"transactionId":"4800000000000000000000000000000000000000000000000000000000000000","index":0},"signatureScript":"41e24bc87905e08f1c5c1567ee8f7db310d180f9ed448b89ad87a1225f6a94d57d6eb7e6780bdcff71436278d1e3826f0065661c1fde8cdcd2ec1c01c6f8476f3701","sequence":0,"sigOpCount":1,"verboseData":null}],"outputs":[{"value":20000000,"scriptPublicKey":{"version":0,"script":"20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac"},"covenant":null,"verboseData":null},{"value":79684600,"scriptPublicKey":{"version":0,"script":"20f9308a019258c31049344f85f89d5229b531c845836f99b08601f113bce036f9ac"},"covenant":null,"verboseData":null}],"lockTime":0,"subnetworkId":"0000000000000000000000000000000000000000","gas":0,"payload":"","mass":22549,"storageMass":22549,"verboseData":null}
verified: true
sample: version-1
version: 1
inputs: 1
must-spend: false
fee-sompi: 203800
fee: 0.002038 KAS
relay-mass: 2038
compute-budget: 10
id: 8261da9c11f698f4ce56fb1518b241be6467594f7f83fcd1574238ebfcb1474b
hex: 010001000000000000004900000000000000000000000000000000000000000000000000000000000000000000004200000000000000412dea455dad949813be8f54fecdfc9ad53d52d567fa43610325a3926da6db32ca740a7bdc11cb70fb62d3ef1e05a0768c284da1ba3f2f601d7648f044368a25440100000000000000000a00020000000000000080969800000000000000220000000000000020dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac00682e5a05000000000000220000000000000020f9308a019258c31049344f85f89d5229b531c845836f99b08601f113bce036f9ac000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000108b010000000000
json: {"version":1,"inputs":[{"previousOutpoint":{"transactionId":"4900000000000000000000000000000000000000000000000000000000000000","index":0},"signatureScript":"412dea455dad949813be8f54fecdfc9ad53d52d567fa43610325a3926da6db32ca740a7bdc11cb70fb62d3ef1e05a0768c284da1ba3f2f601d7648f044368a254401","sequence":0,"sigOpCount":0,"computeBudget":10,"verboseData":null}],"outputs":[{"value":10000000,"scriptPublicKey":{"version":0,"script":"20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac"},"covenant":null,"verboseData":null},{"value":89796200,"scriptPublicKey":{"version":0,"script":"20f9308a019258c31049344f85f89d5229b531c845836f99b08601f113bce036f9ac"},"covenant":null,"verboseData":null}],"lockTime":0,"subnetworkId":"0000000000000000000000000000000000000000","gas":0,"payload":"","mass":101136,"storageMass":101136,"verboseData":null}
verified: true
ok
```

## Connect to a node

Requires 0.1.11-alpha. `KaspaRpcClient.Create` returns a connection and does not connect. Call `ConnectAsync`. Dispose it with `await using`.

Auto reads the URL path, then the port. The path wins. A path ending in `/wrpc/borsh` or containing `/borsh` selects Borsh. A path ending in `/wrpc/json` or containing `/json` selects JSON. Borsh ports are 17110, 17210, 17510, and 17610. JSON ports are 18110, 18210, 18510, and 18610. `wss://eva.kaspa.green/kaspa/mainnet/wrpc/json` selects JSON because of the path, even though the port is 443. If the port and the path are both unknown, `Create` throws. It does not default to either encoding. gRPC ports 16110, 16210, 16510, and 16610 throw `ArgumentException`. An explicit `KaspaRpcEncoding` does not turn a gRPC port into wRPC.

A JSON client that receives a binary frame, or a Borsh client that receives a text frame, throws `KaspaRpcEncodingMismatchException`. Pending requests fail with that exception. The client does not wait out the request timeout and does not reconnect. `HandshakeFailed` is true when a Borsh client receives a JSON handshake text frame. The node handshake in rusty-kaspa is still a TODO. NKaspa does not send one.

`KaspaWebSocketOptions` (requires 0.1.10-alpha) sets the connect timeout (default 15 seconds), the request timeout (default 30 seconds), reconnect, and `MaxMessageBytes` (default 16 MiB).

```csharp
await using var rpc = KaspaRpcClient.Create(url);
await rpc.ConnectAsync(cancellationToken);
```

## Fetch UTXOs

`GetUtxosByAddressesAsync` returns `RpcUtxo` values. Each one has `Outpoint` and `Entry`. `Coin` is that pair.

```csharp
var listed = await rpc.GetUtxosByAddressesAsync(new[] { own.ToString() }, cancellationToken);
var coins = new Coin[listed.Count];
for (var i = 0; i < listed.Count; i++)
{
    coins[i] = new Coin(listed[i].Outpoint, listed[i].Entry);
}
```

`GetBalanceByAddressAsync` returns the sompi total for one address. `GetBalancesByAddressesAsync` returns one row per address. `GetFeeEstimateAsync` returns live buckets (`priorityBucket`, `normalBuckets`, `lowBuckets`).

## Build and sign

Read the mnemonic from the environment. Do not hard-code a wallet sentence. `samples/Spend` and `SendTutorial.SendOnTestnetAsync` use `NKASPA_MNEMONIC`.

```csharp
var words = Environment.GetEnvironmentVariable("NKASPA_MNEMONIC");
var mnemonic = new Mnemonic(words, Wordlist.English);
using var master = mnemonic.DeriveExtKey();
using var receive = master.Derive(KeyPath.KaspaReceive(0));
using var changeKey = master.Derive(KeyPath.KaspaChange(0));
using var spendKey = receive.PrivateKey;
var own = spendKey.GetAddress(network);
var changeAddress = changeKey.PrivateKey.GetAddress(network);

var builder = new TransactionBuilder(network)
    .AddKeys(spendKey)
    .AddCoins(coins);
var tx = builder
    .Send(payee, amount)
    .SetChange(changeAddress)
    .SendMinimumRelayFee()
    .BuildTransaction(true);
```

`SendEstimatedFees(FeeRate)` multiplies a rate by the fee mass, which includes normalized storage. `SendMinimumRelayFee` pays the relay floor only. `TransactionBuilder.EstimateFee(FeeRate.MinimumRelay, MassCalculator.StandardSchnorrPaymentMass)` is the static 0.002036 KAS figure.

### Change

The default `MinimumChange` is 0.02 KAS. Change below that is added to the fee. A larger change is folded only when that output cannot be built, the amount is at most `max(MinimumChange, MaxFoldedChange)` (`MaxFoldedChange` defaults to 0.1 KAS on the builder instance), and it is within `MaxFoldedFeeFactor` of the no-change fee (default 100). Those two properties are public instance properties. Raising them can move more sompi into the fee. They are a wallet limit, not a consensus rule. After a successful build, `builder.ChangeFoldedIntoFee` is the sompi removed from change. It is 0 when change is kept.

### Version 1 (Toccata)

`SetVersion(1)` builds a version-1 payment. The default compute budget is 10 per input. The offline sample's version-1 shape is 2038 grams and 203,800 sompi (0.002038 KAS).

```csharp
var version1 = new TransactionBuilder(network)
    .SetVersion(1)
    .AddKeys(spendKey)
    .AddCoins(coins)
    .Send(payee, amount)
    .SetChange(changeAddress)
    .SendMinimumRelayFee()
    .BuildTransaction(true);
```

Forcing pre-Toccata mode (`MassPolicy.PreToccata`, `toccataActive: false`, or a DAA score below the activation score) uses 1 sompi per gram. A current node can reject that fee. Use the default policy on live networks. Recorded activation scores are mainnet DAA 474165565 and testnet 467579632. `Network.IsToccataActive(daaScore)` is that comparison. `new TransactionBuilder(network, virtualDaaScore)` selects the policy from the network score (requires 0.1.10-alpha).

## Verify before broadcast

Match the checks in `samples/Spend` and in `SendTutorial.SendOnTestnetAsync`:

- One output pays the requested amount to the payee script.
- Input sompi is at least output sompi.
- `Script.VerifyScript` on each input, or `tx.Verify(entries)` for every input.
- The fee equals `MassCalculator.MinimumRelayFee(MassCalculator.Calculate(tx, entries))`.

If any check fails, do not call `SubmitTransactionAsync`.

## Broadcast

**Warning:** `SubmitTransactionAsync` sends the signed transaction to the node. The sample method defaults to testnet. It refuses mainnet unless `NKASPA_ALLOW_MAINNET` is `yes`. It refuses to run when `NKASPA_MNEMONIC`, `NKASPA_WRPC_URL`, `NKASPA_TO`, or `NKASPA_AMOUNT` is missing. It was not executed while writing this page. No transaction was broadcast.

```csharp
var id = await rpc.SubmitTransactionAsync(tx, false, cancellationToken);
```

The return value is the transaction id (`uint256`). `false` is the `allowOrphan` argument used by the tutorial sample.

`SpendAllCoins` and `MustSpend` put every named coin into the transaction and sign each input. Coins that share a script are not merged into one input. The offline must-spend shape is two inputs, 3154 grams, 315,400 sompi (0.003154 KAS).

## Fees and mass

After Toccata, the minimum relay fee is 100 sompi per gram times the larger of compute mass and normalized transient mass. Storage mass is not part of that floor. Under the default limits, normalized transient mass is `2 * estimated bytes`.

| Shape | Relay mass | Minimum relay fee |
| --- | --- | --- |
| One Schnorr input, payment, and change | 2036 grams | 203600 sompi (0.002036 KAS) |
| Two inputs, `MustSpend` | 3154 grams | 315400 sompi (0.003154 KAS) |
| Version 1, compute budget 10 | 2038 grams | 203800 sompi (0.002038 KAS) |

`SendEstimatedFees` can be higher because fee mass also considers normalized storage. The `mass` field in the Spend JSON above is storage mass from the transaction object. It is not the relay mass. The relay mass is the `relay-mass` line.

Block ceilings used by the builder are compute 500000, storage 500000, and transient 1000000.

## Sign offline or with PSKT

You can build and sign on a machine that never opens a socket, which is what both samples do. `TransactionBuilder.BuildPSKT` returns a partially signed Kaspa transaction for version 0 or version 1. Roles, combine, finalize, and extract are covered by [samples/PsktMultisig](../../samples/PsktMultisig) and the README. PSKT hex is the prefix `PSKT` plus lowercase hex of the compact JSON.

## Troubleshooting

**The node rejected the fee.** A pre-Toccata rate of 1 sompi/gram is below the post-Toccata floor of 100 sompi/gram. Mass above the block ceiling, a wrong network prefix, or a signature over the wrong script also fails verification before you should submit.

**Encoding mismatch.** A binary frame on a JSON client, or a text frame on a Borsh client, throws `KaspaRpcEncodingMismatchException` on the call that is in flight. Fix the URL path or pass `KaspaRpcEncoding.Json` or `KaspaRpcEncoding.Borsh`. Do not point the client at a gRPC port.

**No UTXO.** `GetUtxosByAddressesAsync` returned an empty list. The tutorial method prints that and does not submit.

**Change disappeared.** An amount under 0.02 KAS was folded into the fee. Read `ChangeFoldedIntoFee` on the builder instance.

## FAQ

**Does NKaspa broadcast automatically?** No. Only an explicit `SubmitTransactionAsync` broadcasts. `samples/Spend` does it only with `--broadcast` plus `NKASPA_MNEMONIC`.

**gRPC or wRPC?** wRPC over WebSocket, JSON and Borsh, through `KaspaRpcClient`. There is no gRPC client.

**Can I sign offline?** Yes. Build and sign offline, or use `BuildPSKT`.

**Is it safe to test?** Use `Network.Testnet` and the offline samples. Do not set a mnemonic variable unless you intend to spend. The public node URL is operated by someone else.

## Related

- [Generate a Kaspa address in C#](generate-kaspa-address-csharp.md)
- [README](../../README.md) and [CHANGELOG](../../CHANGELOG.md)
- [samples/Spend](../../samples/Spend) and [samples/Tutorials](../../samples/Tutorials)
- [NBitcoin mapping](../NBitcoin-Mapping.en.md)
