using System.Text.Json;
using NKaspa;

namespace NKaspa.Samples.Tutorials
{
    /// <summary>
    /// Code from docs/tutorials/generate-kaspa-address-csharp.md.
    /// The sentence and the key hex are public test vectors.
    /// </summary>
    public static class AddressTutorial
    {
        public const string KnownKey = "b7e151628aed2a6abf7158809cf4f3c762e7160f38b4da56a784d9045190cfef";
        public const string KnownSchnorr = "kaspa:qr0lr4ml9fn3chekrqmjdkergxl93l4wrk3dankcgvjq776s9wn9jkdskewva";
        public const string Abandon = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

        public static int Run(TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (!RandomKey(writer) || !FromMnemonic(writer) || !WatchOnly(writer) || !Kinds(writer) || !Networks(writer) || !Validate(writer) || !JsonRefusesKey(writer))
            {
                return 1;
            }

            return 0;
        }

        /// <summary>Requires 0.1.10-alpha for nothing here. <c>Key.Create</c> and <c>GetAddress</c> are older. This method does not print the secret.</summary>
        public static bool RandomKey(TextWriter writer)
        {
            using var key = Key.Create();
            KaspaPubKeyAddress address = key.GetAddress(Network.Mainnet);
            writer.WriteLine("random-prefix: " + address.Network.AddressPrefix);
            writer.WriteLine("random-valid: " + (KaspaAddress.IsValid(address.ToString()) ? "true" : "false"));
            return address.Network == Network.Mainnet && address.ToString().StartsWith("kaspa:", StringComparison.Ordinal);
        }

        /// <summary>Requires 0.1.10-alpha (<c>Mnemonic.ToPhrase</c>).</summary>
        public static bool FromMnemonic(TextWriter writer)
        {
            var mnemonic = new Mnemonic(Abandon, Wordlist.English);
            writer.WriteLine("phrase: " + mnemonic.ToPhrase());
            using var master = mnemonic.DeriveExtKey();
            using var receive = master.Derive(KeyPath.KaspaReceive(0));
            using var change = master.Derive(KeyPath.KaspaChange(0));
            var address = receive.PrivateKey.GetAddress(Network.Mainnet);
            var changeAddress = change.PrivateKey.GetAddress(Network.Mainnet);
            writer.WriteLine("path: " + KeyPath.KaspaReceive(0));
            writer.WriteLine("change-path: " + KeyPath.KaspaChange(0));
            writer.WriteLine("mainnet: " + address);
            writer.WriteLine("change: " + changeAddress);
            return address.ToString() != changeAddress.ToString()
                && KeyPath.KaspaReceive(0).ToString() == "m/44'/111111'/0'/0/0";
        }

        /// <summary>
        /// Requires 0.1.10-alpha. Hardened steps stay on <c>ExtKey</c>.
        /// <c>Neuter</c> is called on the account key. <c>ExtPubKey</c> then derives <c>0/0</c>.
        /// </summary>
        public static bool WatchOnly(TextWriter writer)
        {
            var mnemonic = new Mnemonic(Abandon, Wordlist.English);
            using var master = mnemonic.DeriveExtKey();
            using var account = master.Derive(KeyPath.KaspaAccount(0));
            ExtPubKey watch = account.Neuter();
            ExtPubKey child = watch.Derive(new KeyPath("m/0/0"));
            var fromPublic = child.GetAddress(Network.Mainnet);
            using var receive = master.Derive(KeyPath.KaspaReceive(0));
            var fromPrivate = receive.PrivateKey.GetAddress(Network.Mainnet);
            writer.WriteLine("watch: " + fromPublic);
            writer.WriteLine("kpub: " + watch.ToString(Network.Mainnet));
            writer.WriteLine("ktub: " + watch.ToString(Network.Testnet));
            return fromPublic.ToString() == fromPrivate.ToString();
        }

        public static bool Kinds(TextWriter writer)
        {
            using var key = Key.Parse(KnownKey);
            KaspaPubKeyAddress schnorr = key.GetAddress(Network.Mainnet);
            KaspaPubKeyEcdsaAddress ecdsa = key.GetEcdsaAddress(Network.Testnet);
            var scriptHash = KaspaAddress.Parse("kaspa:pqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqlmtfk4dg");
            writer.WriteLine("schnorr: " + schnorr);
            writer.WriteLine("ecdsa: " + ecdsa);
            writer.WriteLine("script-hash-type: " + scriptHash.GetType().Name);
            return schnorr.ToString() == KnownSchnorr && scriptHash is KaspaScriptHashAddress;
        }

        public static bool Networks(TextWriter writer)
        {
            using var key = Key.Parse(KnownKey);
            writer.WriteLine("prefix-mainnet: " + key.GetAddress(Network.Mainnet).Network.AddressPrefix);
            writer.WriteLine("prefix-testnet: " + key.GetAddress(Network.Testnet).Network.AddressPrefix);
            writer.WriteLine("prefix-simnet: " + key.GetAddress(Network.Simnet).Network.AddressPrefix);
            writer.WriteLine("prefix-devnet: " + key.GetAddress(Network.Devnet).Network.AddressPrefix);
            return Network.Mainnet.AddressPrefix == "kaspa"
                && Network.Testnet.AddressPrefix == "kaspatest"
                && Network.Simnet.AddressPrefix == "kaspasim"
                && Network.Devnet.AddressPrefix == "kaspadev";
        }

        public static bool Validate(TextWriter writer)
        {
            var text = KnownSchnorr;
            if (!KaspaAddress.IsValid(text) || !KaspaAddress.TryParse(text, out var parsed) || parsed == null)
            {
                return false;
            }

            var again = KaspaAddress.Parse(text);
            var onMainnet = KaspaAddress.Parse(text, Network.Mainnet);
            var script = parsed.ScriptPubKey;
            writer.WriteLine("parsed: " + again);
            writer.WriteLine("network: " + onMainnet.Network.Name);
            writer.WriteLine("script-hex: " + script.ToHex());
            var wrongNetwork = KaspaAddress.TryParse(text, Network.Testnet, out _);
            writer.WriteLine("testnet-parse: " + (wrongNetwork ? "true" : "false"));
            return !wrongNetwork && script.ToHex() == "20dff1d77f2a671c5f36183726db2341be58feae1da2deced843240f7b502ba659ac";
        }

        public static bool JsonRefusesKey(TextWriter writer)
        {
            using var key = Key.Parse(KnownKey);
            try
            {
                var json = JsonSerializer.Serialize(key);
                writer.WriteLine("json-key: " + json);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                writer.WriteLine("json-key: refused");
                return ex.Message.IndexOf(KnownKey, StringComparison.OrdinalIgnoreCase) < 0;
            }
        }
    }
}
