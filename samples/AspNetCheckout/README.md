# AspNetCheckout: testnet checkout with one address per order

> **Alpha, unaudited, testnet only, watch-only.** NKaspa is alpha software and has not been audited.
> This sample is not a production payment system. It does not handle exchange rates, refunds, or mainnet.
> It never holds a private key, never signs, and never submits a transaction.

An ASP.NET Core Minimal API that:

- derives a fresh receive address for every order from an account-level **ktub** (`account/0/i`, i.e. the last two steps of `m/44'/111111'/0'/0/i`);
- watches each open address with `NotifyUtxosChangedAsync`, plus a `GetBalancesByAddressesAsync` / `GetUtxosByAddressesAsync` poll as a fallback;
- marks an order `seen` when the full amount arrives and `confirmed` once every counted UTXO is `RequiredConfirmations` DAA score deep.

Order states: `pending` → `seen` → `confirmed`; `underpaid` when some but not enough arrived; `expired` when nothing arrived within `OrderTtl`.

## Run it (about 5 minutes)

You need the .NET 8 SDK and a reachable **testnet** node with the UTXO index.

1. Node: for example rusty-kaspa `kaspad --testnet --utxoindex --rpclisten-borsh=0.0.0.0:17210` (check the rusty-kaspa docs for current flags), or your own testnet wRPC node. Public nodes are not guaranteed to be up.
2. Key: export the **account extended public key** (ktub…) from a wallet that supports testnet. Export the public key only. Never put a private key or recovery phrase into this service; it refuses to start if you try.
3. Run:

   ```bash
   cd samples/AspNetCheckout
   export Checkout__NodeUrl=ws://127.0.0.1:17210
   export Checkout__Ktub=ktub...
   dotnet run
   ```

   PowerShell: `$env:Checkout__NodeUrl="ws://127.0.0.1:17210"; $env:Checkout__Ktub="ktub..."`.
   You can also use `dotnet user-secrets`. `appsettings.json` only holds placeholders.
4. Open <http://localhost:5080/>, or:

   ```bash
   curl -X POST localhost:5080/orders -H 'content-type: application/json' -d '{"amountKas":"1"}'
   curl localhost:5080/orders/{id}
   ```

5. Send testnet KAS (from a testnet faucet) to the returned address and watch the status move to `seen`, then `confirmed`.
6. Tests need no node: `dotnet test ../AspNetCheckout.Tests`.

## Endpoints

| Method | Path | Notes |
|---|---|---|
| POST | `/orders` | body `{ "amountKas": "1.5" }` (decimal string, > 0, ≤ `MaxAmountKas`) → `201 { id, address, amountKas, amountSompi, status, expiresAt, network }` |
| GET | `/orders/{id}` | `{ id, address, amountSompi, receivedSompi, status, confirmations, requiredConfirmations }`, 404 if unknown |
| GET | `/health` | `{ rpcState }` |

## Configuration

| Key | Env var | Default |
|---|---|---|
| `Checkout:NodeUrl` | `Checkout__NodeUrl` | required. wRPC only (17210 Borsh / 18210 JSON on testnet); gRPC ports are rejected by `KaspaRpcClient.Create` |
| `Checkout:Ktub` | `Checkout__Ktub` | required, must start with `ktub` |
| `Checkout:RequiredConfirmations` | | `100` (DAA score distance; a demo value, **not** a safety recommendation; choose your own based on risk) |
| `Checkout:ConfirmationSource` | | `DaaScore` (or `SinkBlueScore`: rough distance only, a different scale from DAA score) |
| `Checkout:PollInterval` | | `00:00:15` |
| `Checkout:OrderTtl` | | `00:30:00` |
| `Checkout:StartIndex` | | `0` |
| `Checkout:MaxAmountKas` | | `1000` |
| `Storage:Provider` / `Storage:Path` | | `Sqlite` / `checkout.db` (`InMemory` loses everything on restart) |

Startup fails with a clear message when the key starts with `kpub` (mainnet), is a private extended key, or looks like a recovery phrase, and when the node is not on testnet, has no UTXO index, or is not synced.

## Things to know

- **Gap limit.** Wallets usually scan about 20 unused addresses ahead. Many unpaid orders in a row can push later addresses past what a wallet restore finds. Raise the wallet's gap limit or export the address list from `checkout.db`. Addresses are never reused, even for expired orders, so a late payment cannot be credited to a different order.
- **Reconnects.** NKaspa reconnects the socket automatically but does not resend subscriptions. The monitor resubscribes every open address on `Reconnected` and immediately polls to catch payments that arrived while disconnected. If the library gives up reconnecting, the monitor creates a new connection with exponential backoff.
- **Downtime.** The node only reports current UTXOs. A payment that arrives and is spent again while this service is down cannot be detected.

## How this sample works around current NKaspa API gaps (0.1.11-alpha)

1. `KaspaRpcClient.Create(...)` is a static factory returning `KaspaRpcConnection`; `Payments/ICheckoutRpc.cs` wraps it (`KaspaRpcFactory`, `KaspaCheckoutRpc`).
2. Subscriptions are not restored after an automatic reconnect: `PaymentMonitor` resubscribes and polls on `Reconnected`.
3. `ExtPubKey` does not expose its network: `KeyGuard` rejects by prefix (`kpub`, private `?prv`, word lists), and `AddressDeriver` re-encodes the parsed key with `Network.Testnet` and requires the same text.
4. `ConnectAsync`, `State`, `StateChanged`, `Reconnected` are not on `IKaspaRpcClient`: the sample's own `ICheckoutRpc` interface carries them, so tests use an offline fake.
5. Notification callbacks are synchronous `Action<T>`: callbacks only write into a `Channel<object>`; one consumer loop does all async work.
6. No "confirmations" API: `ConfirmationPolicy` computes `currentScore - UtxoEntry.BlockDaaScore` (virtual DAA score from notifications / `GetServerInfoAsync`, or `GetSinkBlueScoreAsync` when configured).

Using the package instead of the project reference: `dotnet add package NKaspa --version 0.1.11-alpha`.
