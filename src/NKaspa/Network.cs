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
        private Network(string name, string addressPrefix, int p2pPort)
        {
            Name = name;
            AddressPrefix = addressPrefix;
            P2PPort = p2pPort;
        }

        /// <summary>Mainnet. Address prefix <c>kaspa</c>.</summary>
        public static Network Mainnet { get; } = new Network("mainnet", "kaspa", 16111);

        /// <summary>Public testnet. Address prefix <c>kaspatest</c>.</summary>
        public static Network Testnet { get; } = new Network("testnet", "kaspatest", 16211);

        /// <summary>Simulation network. Address prefix <c>kaspasim</c>.</summary>
        public static Network Simnet { get; } = new Network("simnet", "kaspasim", 16511);

        /// <summary>Development network. Address prefix <c>kaspadev</c>.</summary>
        public static Network Devnet { get; } = new Network("devnet", "kaspadev", 16611);

        /// <summary>Short network name: mainnet, testnet, simnet, or devnet.</summary>
        public string Name { get; }

        /// <summary>Address prefix without the colon.</summary>
        public string AddressPrefix { get; }

        /// <summary>Default kaspad P2P port for this network.</summary>
        public int P2PPort { get; }

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
