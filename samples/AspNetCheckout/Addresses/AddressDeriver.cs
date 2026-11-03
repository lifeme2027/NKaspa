namespace NKaspa.Samples.AspNetCheckout.Addresses;

/// <summary>
/// Derives receive addresses <c>account/0/i</c> from an account-level ktub.
/// Parses the key once. Public derivation only.
/// </summary>
public sealed class AddressDeriver
{
    private readonly ExtPubKey _receiveChain;

    /// <exception cref="InvalidOperationException">The key is not a usable testnet account public key.</exception>
    public AddressDeriver(string ktub)
    {
        KeyGuard.EnsureTestnetAccountKey(ktub);
        var text = ktub.Trim();
        ExtPubKey account;
        try
        {
            account = ExtPubKey.Parse(text);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new InvalidOperationException("Checkout:Ktub is not a valid extended public key.", ex);
        }

        // Gap workaround: ExtPubKey has no Network property. Re-encode with the testnet version
        // and require the same text, so a key with a non-testnet version cannot slip through.
        if (!string.Equals(account.ToString(Network.Testnet), text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Checkout:Ktub does not use the testnet public version (ktub).");
        }

        _receiveChain = account.Derive(0);
    }

    /// <summary>Address for receive index <paramref name="index"/> on testnet.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is hardened (≥ 0x80000000).</exception>
    public string Derive(uint index)
    {
        if (index > AddressDeriverLimits.MaxIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Hardened indexes cannot be derived from a public key.");
        }

        return _receiveChain.Derive(index).GetAddress(Network.Testnet).ToString();
    }
}
