using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NKaspa.Samples.AspNetCheckout.Endpoints;
using NKaspa.Samples.AspNetCheckout.Payments;
using Xunit;

namespace NKaspa.Samples.AspNetCheckout.Tests;

public class OrderEndpointsTests
{
    private static WebApplicationFactory<Program> Factory(string ktub) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Checkout:NodeUrl", "ws://127.0.0.1:17210");
            b.UseSetting("Checkout:Ktub", ktub);
            b.UseSetting("Storage:Provider", "InMemory");
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IRpcFactory>();
                s.AddSingleton<IRpcFactory>(new FakeRpcFactory());
            });
        });

    [Fact]
    public async Task Create_then_get()
    {
        using var f = Factory(Vectors.Ktub);
        var http = f.CreateClient();
        var resp = await http.PostAsJsonAsync("/orders", new { amountKas = "1.5" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = (await resp.Content.ReadFromJsonAsync<CreateOrderResponse>())!;
        Assert.Equal(Vectors.Address0, created.Address);
        Assert.Equal(150_000_000UL, created.AmountSompi);
        Assert.Equal("pending", created.Status);
        Assert.Equal("testnet", created.Network);

        var status = (await http.GetFromJsonAsync<OrderStatusResponse>("/orders/" + created.Id))!;
        Assert.Equal(created.Address, status.Address);
        Assert.Equal(100UL, status.RequiredConfirmations);

        var second = (await (await http.PostAsJsonAsync("/orders", new { amountKas = "2" })).Content.ReadFromJsonAsync<CreateOrderResponse>())!;
        Assert.Equal(Vectors.Address1, second.Address);
    }

    [Fact]
    public async Task Unknown_order_is_404()
    {
        using var f = Factory(Vectors.Ktub);
        var resp = await f.CreateClient().GetAsync("/orders/nope");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1000.1")]
    [InlineData("0.000000001")]
    public async Task Bad_amount_is_400(string amount)
    {
        using var f = Factory(Vectors.Ktub);
        var resp = await f.CreateClient().PostAsJsonAsync("/orders", new { amountKas = amount });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public void Startup_fails_for_mainnet_kpub()
    {
        using var f = Factory(Vectors.Kpub);
        var ex = Assert.ThrowsAny<Exception>(() => f.CreateClient());
        Assert.Contains("mainnet", Flatten(ex));
    }

    [Fact]
    public void Startup_fails_for_word_list()
    {
        using var f = Factory(string.Join(" ", Enumerable.Range(1, 12).Select(i => "word" + i)));
        var ex = Assert.ThrowsAny<Exception>(() => f.CreateClient());
        Assert.Contains("recovery phrase", Flatten(ex));
    }

    private static string Flatten(Exception ex)
    {
        var s = "";
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            s += e.Message + "\n";
        }

        return s;
    }
}
