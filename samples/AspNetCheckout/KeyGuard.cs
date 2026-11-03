namespace NKaspa.Samples.AspNetCheckout;

/// <summary>
/// Startup guard for the configured key. This service is watch-only and testnet-only.
/// NKaspa's <c>ExtPubKey.Parse</c> accepts both mainnet and testnet public versions and the parsed
/// object does not say which network it belongs to, so the network is checked by text prefix here.
/// </summary>
public static class KeyGuard
{
    private const string TestnetPublicPrefix = "ktub";
    private const string MainnetPublicPrefix = "kpub";

    /// <summary>Throws <see cref="InvalidOperationException"/> unless <paramref name="value"/> looks like a testnet account public key.</summary>
    public static void EnsureTestnetAccountKey(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0)
        {
            throw new InvalidOperationException("Checkout:Ktub is required. Only a testnet extended public key (ktub...) is accepted.");
        }

        if (LooksLikeWordList(text))
        {
            throw new InvalidOperationException("Checkout:Ktub looks like a recovery phrase. Never give this service a recovery phrase. Only ktub... is accepted.");
        }

        // Private extended keys end their 4-char prefix in "rv" (Kaspa mainnet/testnet, BIP32 x/t variants).
        if (text.Length >= 4 && string.Equals(text.Substring(2, 2), "rv", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Checkout:Ktub is a private extended key. This service is watch-only. Only ktub... is accepted.");
        }

        if (text.StartsWith(MainnetPublicPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Checkout:Ktub is a mainnet key (kpub...). This sample is testnet-only. Only ktub... is accepted.");
        }

        if (!text.StartsWith(TestnetPublicPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Checkout:Ktub must start with 'ktub' (testnet extended public key).");
        }
    }

    private static bool LooksLikeWordList(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 12;
    }
}
