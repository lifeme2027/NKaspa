using System.Globalization;
using Microsoft.Extensions.Options;
using NKaspa.Samples.AspNetCheckout.Addresses;
using NKaspa.Samples.AspNetCheckout.Orders;
using NKaspa.Samples.AspNetCheckout.Payments;

namespace NKaspa.Samples.AspNetCheckout.Endpoints;

public sealed record CreateOrderRequest(string? AmountKas);

public sealed record CreateOrderResponse(string Id, string Address, string AmountKas, ulong AmountSompi, string Status, DateTimeOffset ExpiresAt, string Network);

public sealed record OrderStatusResponse(string Id, string Address, ulong AmountSompi, ulong ReceivedSompi, string Status, ulong Confirmations, ulong RequiredConfirmations);

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", CreateAsync);
        app.MapGet("/orders/{id}", GetAsync);
        app.MapGet("/health", (PaymentMonitor monitor) => Results.Ok(new { rpcState = monitor.RpcState }));
        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateOrderRequest? request,
        IOrderStore store,
        AddressDeriver deriver,
        PaymentMonitor monitor,
        IOptions<CheckoutOptions> options,
        TimeProvider time,
        CancellationToken ct)
    {
        var opts = options.Value;
        if (request?.AmountKas is null
            || !decimal.TryParse(request.AmountKas, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var kas))
        {
            return Results.BadRequest(new { error = "amountKas must be a decimal string, e.g. \"1.5\"." });
        }

        if (kas <= 0 || kas > opts.MaxAmountKas)
        {
            return Results.BadRequest(new { error = $"amountKas must be > 0 and <= {opts.MaxAmountKas.ToString(CultureInfo.InvariantCulture)}." });
        }

        Money amount;
        try
        {
            amount = Money.Coins(kas);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            return Results.BadRequest(new { error = "amountKas has more than 8 decimal places." });
        }

        var now = time.GetUtcNow();
        Order order;
        try
        {
            order = await store.CreateAsync(amount.Sompi, deriver.Derive, now, ct);
        }
        catch (AddressIndexExhaustedException)
        {
            return Results.Problem("No receive index left for new orders.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        monitor.Watch(order);
        var body = new CreateOrderResponse(
            order.Id,
            order.Address,
            kas.ToString(CultureInfo.InvariantCulture),
            order.AmountSompi,
            order.Status.ToText(),
            order.CreatedAt + opts.OrderTtl,
            "testnet");
        return Results.Created($"/orders/{order.Id}", body);
    }

    private static async Task<IResult> GetAsync(string id, IOrderStore store, IOptions<CheckoutOptions> options, CancellationToken ct)
    {
        var order = await store.GetAsync(id, ct);
        if (order is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new OrderStatusResponse(
            order.Id,
            order.Address,
            order.AmountSompi,
            order.ReceivedSompi,
            order.Status.ToText(),
            order.Confirmations,
            options.Value.RequiredConfirmations));
    }
}
