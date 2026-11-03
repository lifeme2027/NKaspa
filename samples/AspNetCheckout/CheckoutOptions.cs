namespace NKaspa.Samples.AspNetCheckout;

/// <summary>Where the "current score" for confirmation counting comes from.</summary>
public enum ConfirmationSource
{
    /// <summary>Virtual DAA score (default). Same scale as <c>UtxoEntry.BlockDaaScore</c>.</summary>
    DaaScore,

    /// <summary>Sink blue score from <c>GetSinkBlueScoreAsync</c>. Rough distance only: not the same scale as DAA score.</summary>
    SinkBlueScore,
}

/// <summary>Strongly typed <c>Checkout</c> section.</summary>
public sealed class CheckoutOptions
{
    public const string Section = "Checkout";

    /// <summary>wRPC URL of a testnet node with --utxoindex, for example ws://127.0.0.1:17210.</summary>
    public string NodeUrl { get; set; } = "";

    /// <summary>Account-level testnet extended public key (m/44'/111111'/0'). Must start with "ktub".</summary>
    public string Ktub { get; set; } = "";

    /// <summary>Required score distance between the UTXO block and the current score. Demo value, not a safety recommendation.</summary>
    public ulong RequiredConfirmations { get; set; } = 100;

    public ConfirmationSource ConfirmationSource { get; set; } = ConfirmationSource.DaaScore;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan OrderTtl { get; set; } = TimeSpan.FromMinutes(30);

    public uint StartIndex { get; set; }

    public decimal MaxAmountKas { get; set; } = 1000m;

    /// <summary>Throws <see cref="InvalidOperationException"/> with a clear message when the configuration is unusable.</summary>
    public void Validate()
    {
        KeyGuard.EnsureTestnetAccountKey(Ktub);
        if (string.IsNullOrWhiteSpace(NodeUrl))
        {
            throw new InvalidOperationException("Checkout:NodeUrl is required (testnet wRPC, e.g. ws://127.0.0.1:17210).");
        }

        if (PollInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Checkout:PollInterval must be positive.");
        }

        if (OrderTtl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Checkout:OrderTtl must be positive.");
        }

        if (MaxAmountKas <= 0)
        {
            throw new InvalidOperationException("Checkout:MaxAmountKas must be positive.");
        }

        if (StartIndex > AddressDeriverLimits.MaxIndex)
        {
            throw new InvalidOperationException("Checkout:StartIndex must be below 0x80000000 (non-hardened).");
        }
    }
}

/// <summary>Storage section.</summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Sqlite (default) or InMemory.</summary>
    public string Provider { get; set; } = "Sqlite";

    public string Path { get; set; } = "checkout.db";
}

internal static class AddressDeriverLimits
{
    public const uint MaxIndex = 0x7FFFFFFF;
}
