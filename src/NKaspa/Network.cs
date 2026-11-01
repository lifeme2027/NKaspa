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
        private Network(string name, string addressPrefix, int p2pPort, uint extPubKeyVersion, uint extPrivKeyVersion, Policy.BlockMassLimits massLimits, ulong toccataActivationDaaScore)
        {
            Name = name;
            AddressPrefix = addressPrefix;
            P2PPort = p2pPort;
            ExtPubKeyVersion = extPubKeyVersion;
            ExtPrivKeyVersion = extPrivKeyVersion;
            MassLimits = massLimits;
            ToccataActivationDaaScore = toccataActivationDaaScore;
        }

        /// <summary>
        /// Mainnet Toccata activation DAA score, 474165565.
        /// rusty-kaspa commit <c>90dbf074275d60c1fe74a3491883196f110970c0</c>,
        /// <c>consensus/core/src/config/params.rs</c> line 724:
        /// <c>toccata_activation: ForkActivation::new(474_165_565)</c>.
        /// Current master removed the field. Its override parser rejects it.
        /// Block mass limits in that file are already the post-fork triple.
        /// </summary>
        public const ulong MainnetToccataActivationDaaScore = 474_165_565;

        /// <summary>
        /// Testnet-10 Toccata activation DAA score, 467579632.
        /// rusty-kaspa commit <c>90dbf074275d60c1fe74a3491883196f110970c0</c>,
        /// <c>consensus/core/src/config/params.rs</c> line 785:
        /// <c>toccata_activation: ForkActivation::new(467_579_632)</c>.
        /// Tag tn10-toc2, commit <c>97415b689462bec8a1a36f1665302529ea8a3108</c>, repeats it at line 783.
        /// Tag tn10-toc3, commit <c>1015a62359e0d06e0b3b3b7f7d06bc1bd4bf0c1b</c>, repeats it at line 784.
        /// Current master removed the field.
        /// <see cref="WithToccataActivationDaaScore"/> replaces this score for one network value.
        /// </summary>
        public const ulong TestnetToccataActivationDaaScore = 467_579_632;

        /// <summary>
        /// Testnet-10 ZK-hardening DAA score, 476232000. This is not the Toccata activation.
        /// rusty-kaspa tag tn10-toc3, commit <c>1015a62359e0d06e0b3b3b7f7d06bc1bd4bf0c1b</c>,
        /// <c>consensus/core/src/config/params.rs</c> line 786:
        /// <c>zk_hardening_activation: ForkActivation::new(476_232_000)</c>.
        /// Master <c>4edcbbccb449f84832afb1ca157904b35f37d006</c> does not contain this field.
        /// <see cref="Testnet"/> does not use it. Pass it to <see cref="WithToccataActivationDaaScore"/>
        /// only when that checkpoint should be the policy switch.
        /// </summary>
        public const ulong TestnetZkHardeningActivationDaaScore = 476_232_000;

        /// <summary>Mainnet. Address prefix <c>kaspa</c>. Extended keys use <c>kpub</c> and <c>kprv</c>.</summary>
        public static Network Mainnet { get; } = new Network("mainnet", "kaspa", 16111, HdKeyVersions.Kpub, HdKeyVersions.Kprv, Policy.BlockMassLimits.Default, MainnetToccataActivationDaaScore);

        /// <summary>Public testnet. Address prefix <c>kaspatest</c>. Extended keys use <c>ktub</c> and <c>ktrv</c>.</summary>
        public static Network Testnet { get; } = new Network("testnet", "kaspatest", 16211, HdKeyVersions.Ktub, HdKeyVersions.Ktrv, Policy.BlockMassLimits.Default, TestnetToccataActivationDaaScore);

        /// <summary>
        /// Simulation network. Address prefix <c>kaspasim</c>. Extended keys use <c>ktub</c> and <c>ktrv</c>.
        /// Activation score 0: current rusty-kaspa simnet is post-Toccata at every DAA score.
        /// </summary>
        public static Network Simnet { get; } = new Network("simnet", "kaspasim", 16511, HdKeyVersions.Ktub, HdKeyVersions.Ktrv, Policy.BlockMassLimits.Default, 0);

        /// <summary>
        /// Development network. Address prefix <c>kaspadev</c>. Extended keys use <c>ktub</c> and <c>ktrv</c>.
        /// Activation score 0 matches current rusty-kaspa master, which turned devnet on in pull request 1082.
        /// A devnet node from before that cleanup is still pre-Toccata. Pass <see cref="Policy.MassPolicy.PreToccata"/> to the builder for that node.
        /// </summary>
        public static Network Devnet { get; } = new Network("devnet", "kaspadev", 16611, HdKeyVersions.Ktub, HdKeyVersions.Ktrv, Policy.BlockMassLimits.Default, 0);

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
        /// Current rusty-kaspa <c>params.rs</c> writes that same triple on mainnet, testnet, simnet, and devnet.
        /// The per-transaction cap is separate. See <see cref="MassPolicyAt"/>.
        /// </summary>
        public Policy.BlockMassLimits MassLimits { get; }

        /// <summary>
        /// DAA score at which <see cref="MassPolicyAt"/> returns <see cref="Policy.MassPolicy.PostToccata"/>.
        /// Zero means every score is post-Toccata. That is simnet, and devnet on current rusty-kaspa master.
        /// Replace it with <see cref="WithToccataActivationDaaScore"/>.
        /// </summary>
        public ulong ToccataActivationDaaScore { get; }

        /// <summary>
        /// Same network identity with a different <see cref="ToccataActivationDaaScore"/>.
        /// Address prefix, ports, and extended-key versions stay.
        /// The score selects the per-transaction mass cap and the relay rate (1 sompi per gram before, 100 after).
        /// </summary>
        public Network WithToccataActivationDaaScore(ulong daaScore)
        {
            return new Network(Name, AddressPrefix, P2PPort, ExtPubKeyVersion, ExtPrivKeyVersion, MassLimits, daaScore);
        }

        /// <summary>True when <paramref name="virtualDaaScore"/> has reached <see cref="ToccataActivationDaaScore"/>.</summary>
        public bool IsToccataActive(ulong virtualDaaScore)
        {
            return virtualDaaScore >= ToccataActivationDaaScore;
        }

        /// <summary>
        /// Mass cap and relay rate at <paramref name="virtualDaaScore"/>.
        /// Below the activation score this is <see cref="Policy.MassPolicy.PreToccata"/> (cap 100000, relay rate 1).
        /// At and above it, this is <see cref="Policy.MassPolicy.PostToccata"/> (cap 500000, relay rate 100).
        /// Both of those values report the storage-mass commitment as required.
        /// <see cref="Policy.MassPolicy.ForNetwork"/> is the entry point that also turns that commitment off below Crescendo.
        /// </summary>
        public Policy.MassPolicy MassPolicyAt(ulong virtualDaaScore)
        {
            return IsToccataActive(virtualDaaScore) ? Policy.MassPolicy.PostToccata : Policy.MassPolicy.PreToccata;
        }

        /// <summary>The four built-in networks.</summary>
        public static IReadOnlyList<Network> All { get; } = new[]
        {
            Mainnet,
            Testnet,
            Simnet,
            Devnet,
        };

        /// <summary>Finds a network by name or address prefix.</summary>
        /// <exception cref="System.ArgumentException">Unknown Kaspa network '{name}'.</exception>
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
