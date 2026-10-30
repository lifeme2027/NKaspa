namespace NKaspa
{
    /// <summary>
    /// Kaspa counterpart of NBitcoin.Network, reduced to the identity an address needs.
    /// The four networks are mainnet, testnet, simnet, and devnet.
    /// </summary>
    /// <remarks>
    /// Address prefixes match rusty-kaspa and kaswallet: <c>kaspa</c>, <c>kaspatest</c>,
    /// <c>kaspasim</c>, and <c>kaspadev</c>. <see cref="Testnet"/> is the public test network
    /// that uses <c>kaspatest</c> (testnet-10 in kaspad's network id). Default P2P ports follow
    /// kaspad: mainnet 16111, testnet-10 16211, simnet 16511, devnet 16611.
    /// </remarks>
    public sealed class Network
    {
        private Network(string name, string addressPrefix, int p2pPort, uint extPubKeyVersion, uint extPrivKeyVersion, Policy.BlockMassLimits massLimits)
        {
            Name = name;
            AddressPrefix = addressPrefix;
            P2PPort = p2pPort;
            ExtPubKeyVersion = extPubKeyVersion;
            ExtPrivKeyVersion = extPrivKeyVersion;
            MassLimits = massLimits;
        }

        /// <summary>
        /// Mainnet Toccata activation DAA score from the rusty-kaspa upgrade guide,
        /// commit <c>90dbf074275d60c1fe74a3491883196f110970c0</c>, about 2026-06-30 16:15 UTC.
        /// Current rusty-kaspa master no longer branches on this score.
        /// params.rs sets the same post-fork block mass limits on mainnet, testnet, simnet, and devnet.
        /// Those networks' activation scores are not in that file.
        /// </summary>
        public const ulong MainnetToccataActivationDaaScore = 474_165_565;

        /// <summary>Mainnet. Address prefix <c>kaspa</c>. Extended keys use <c>kpub</c> and <c>kprv</c>.</summary>
        public static Network Mainnet { get; } = new Network("mainnet", "kaspa", 16111, HdKeyVersions.Kpub, HdKeyVersions.Kprv, Policy.BlockMassLimits.Default);

        /// <summary>Public testnet. Address prefix <c>kaspatest</c>. Extended keys use <c>ktub</c> and <c>ktrv</c>.</summary>
        public static Network Testnet { get; } = new Network("testnet", "kaspatest", 16211, HdKeyVersions.Ktub, HdKeyVersions.Ktrv, Policy.BlockMassLimits.Default);

        /// <summary>Simulation network. Address prefix <c>kaspasim</c>. Extended keys use <c>ktub</c> and <c>ktrv</c>.</summary>
        public static Network Simnet { get; } = new Network("simnet", "kaspasim", 16511, HdKeyVersions.Ktub, HdKeyVersions.Ktrv, Policy.BlockMassLimits.Default);

        /// <summary>Development network. Address prefix <c>kaspadev</c>. Extended keys use <c>ktub</c> and <c>ktrv</c>.</summary>
        public static Network Devnet { get; } = new Network("devnet", "kaspadev", 16611, HdKeyVersions.Ktub, HdKeyVersions.Ktrv, Policy.BlockMassLimits.Default);

        /// <summary>Short network name: mainnet, testnet, simnet, or devnet.</summary>
        public string Name { get; }

        /// <summary>Address prefix without the colon.</summary>
        public string AddressPrefix { get; }

        /// <summary>Default kaspad P2P port for this network.</summary>
        public int P2PPort { get; }

        /// <summary>
        /// Base58Check version of an extended public key.
        /// Mainnet is <c>kpub</c> (<c>0x038f332e</c>). Testnet, simnet, and devnet are <c>ktub</c> (<c>0x0390a241</c>).
        /// </summary>
        public uint ExtPubKeyVersion { get; }

        /// <summary>
        /// Base58Check version of an extended private key.
        /// Mainnet is <c>kprv</c> (<c>0x038f2ef4</c>). Testnet, simnet, and devnet are <c>ktrv</c> (<c>0x03909e07</c>).
        /// </summary>
        public uint ExtPrivKeyVersion { get; }

        /// <summary>
        /// Block mass limits used for fee normalization.
        /// Every built-in network uses the post-Toccata limits: compute 500000, storage 500000, transient 1000000.
        /// </summary>
        public Policy.BlockMassLimits MassLimits { get; }

        /// <summary>The four built-in networks.</summary>
        public static IReadOnlyList<Network> All { get; } = new[]
        {
            Mainnet,
            Testnet,
            Simnet,
            Devnet,
        };

        /// <summary>Finds a network by name or address prefix.</summary>
        public static Network GetNetwork(string name)
        {
            if (!TryGetNetwork(name, out var network) || network == null)
            {
                throw new ArgumentException("Unknown Kaspa network '" + name + "'.", nameof(name));
            }

            return network;
        }

        /// <summary>
        /// Accepts mainnet/kaspa, testnet/testnet-10/testnet10/kaspatest,
        /// simnet/kaspasim, and devnet/kaspadev. Matching is case-insensitive.
        /// </summary>
        public static bool TryGetNetwork(string? name, out Network? network)
        {
            network = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            switch (name.Trim().ToLowerInvariant())
            {
                case "mainnet":
                case "kaspa":
                    network = Mainnet;
                    return true;
                case "testnet":
                case "testnet-10":
                case "testnet10":
                case "kaspatest":
                    network = Testnet;
                    return true;
                case "simnet":
                case "kaspasim":
                    network = Simnet;
                    return true;
                case "devnet":
                case "kaspadev":
                    network = Devnet;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Network whose address prefix is exactly <paramref name="prefix"/>.</summary>
        public static bool TryFromAddressPrefix(string? prefix, out Network? network)
        {
            network = null;
            if (prefix == null)
            {
                return false;
            }

            for (var i = 0; i < All.Count; i++)
            {
                if (All[i].AddressPrefix == prefix)
                {
                    network = All[i];
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Name;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is Network other && AddressPrefix == other.AddressPrefix;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return AddressPrefix.GetHashCode();
        }
    }
}
