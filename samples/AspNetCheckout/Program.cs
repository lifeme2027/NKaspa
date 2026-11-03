using Microsoft.Extensions.Options;
using NKaspa.Samples.AspNetCheckout;
using NKaspa.Samples.AspNetCheckout.Addresses;
using NKaspa.Samples.AspNetCheckout.Endpoints;
using NKaspa.Samples.AspNetCheckout.Orders;
using NKaspa.Samples.AspNetCheckout.Payments;

// Testnet-only, watch-only checkout sample. No keys that can spend, no signing, no broadcasting.
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CheckoutOptions>(builder.Configuration.GetSection(CheckoutOptions.Section));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<CheckoutOptions>>().Value;
    o.Validate();
    return new AddressDeriver(o.Ktub);
});
builder.Services.AddSingleton<IOrderStore>(sp =>
{
    var s = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    var start = sp.GetRequiredService<IOptions<CheckoutOptions>>().Value.StartIndex;
    return string.Equals(s.Provider, "InMemory", StringComparison.OrdinalIgnoreCase)
        ? new InMemoryOrderStore(start)
        : new SqliteOrderStore(s.Path, start);
});
builder.Services.AddSingleton<IRpcFactory>(sp => new KaspaRpcFactory(sp.GetRequiredService<IOptions<CheckoutOptions>>().Value.NodeUrl));
builder.Services.AddSingleton<PaymentMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PaymentMonitor>());

var app = builder.Build();

// Fail at startup, before listening, when the key is missing, mainnet (kpub), private, or a recovery phrase.
app.Services.GetRequiredService<AddressDeriver>();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOrderEndpoints();
app.Run();

/// <summary>Entry point type, public for WebApplicationFactory in the tests.</summary>
public partial class Program
{
}
