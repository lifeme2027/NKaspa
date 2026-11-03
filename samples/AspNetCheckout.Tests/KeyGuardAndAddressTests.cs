using NKaspa.Samples.AspNetCheckout.Addresses;
using Xunit;

namespace NKaspa.Samples.AspNetCheckout.Tests;

public class KeyGuardAndAddressTests
{
    [Fact]
    public void Derives_known_receive_addresses()
    {
        var d = new AddressDeriver(Vectors.Ktub);
        Assert.Equal(Vectors.Address0, d.Derive(0));
        Assert.Equal(Vectors.Address1, d.Derive(1));
        Assert.Equal(Vectors.Address5, d.Derive(5));
    }

    [Fact]
    public void Derived_address_parses_as_testnet()
    {
        var d = new AddressDeriver(Vectors.Ktub);
        var parsed = KaspaAddress.Parse(d.Derive(3), Network.Testnet);
        Assert.StartsWith("kaspatest:", parsed.ToString());
    }

    [Fact]
    public void Rejects_mainnet_kpub_by_prefix()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new AddressDeriver(Vectors.Kpub));
        Assert.Contains("mainnet", ex.Message);
    }

    [Theory]
    [InlineData("x" + "prv" + "NotAKeyJustFillerText")]
    [InlineData("t" + "prv" + "NotAKeyJustFillerText")]
    public void Rejects_private_extended_keys(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => KeyGuard.EnsureTestnetAccountKey(value));
        Assert.Contains("private", ex.Message);
    }

    [Fact]
    public void Rejects_kaspa_private_prefixes()
    {
        // Built at runtime so no private-key-looking literal sits in the sample sources.
        foreach (var head in new[] { "k" + "prv", "k" + "trv" })
        {
            var ex = Assert.Throws<InvalidOperationException>(() => KeyGuard.EnsureTestnetAccountKey(head + Vectors.Ktub.Substring(4)));
            Assert.Contains("private", ex.Message);
        }
    }

    [Fact]
    public void Rejects_word_lists()
    {
        var words = string.Join(" ", Enumerable.Range(1, 12).Select(i => "word" + i));
        var ex = Assert.Throws<InvalidOperationException>(() => KeyGuard.EnsureTestnetAccountKey(words));
        Assert.Contains("recovery phrase", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("hello")]
    public void Rejects_empty_or_unknown(string? value)
    {
        Assert.Throws<InvalidOperationException>(() => KeyGuard.EnsureTestnetAccountKey(value));
    }

    [Fact]
    public void Rejects_corrupted_ktub()
    {
        var bad = Vectors.Ktub.Substring(0, Vectors.Ktub.Length - 1) + (Vectors.Ktub[^1] == 'x' ? "y" : "x");
        Assert.Throws<InvalidOperationException>(() => new AddressDeriver(bad));
    }

    [Fact]
    public void Rejects_hardened_index()
    {
        var d = new AddressDeriver(Vectors.Ktub);
        Assert.Throws<ArgumentOutOfRangeException>(() => d.Derive(0x80000000));
    }
}
