using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace NKaspa.RPC
{
    /// <summary>
    /// rusty-kaspa wRPC Borsh framing and rpc-core versioned structs.
    /// rusty-kaspa <c>4edcbbccb449f84832afb1ca157904b35f37d006</c>.
    /// workflow-rpc and workflow-serializer <c>0.19.0</c>, workflow-rs <c>7e2958098bbe0647adcfcc0dfc174e438a60699e</c>.
    /// A request is <c>BorshReqHeader { id: Option&lt;Id64&gt;, op: RpcApiOps }</c> then <c>borsh(Serializable&lt;Request&gt;)</c>.
    /// <c>Serializable</c> stores the versioned <c>Serializer</c> body as a Borsh byte vector.
    /// A success payload is Borsh <c>Result</c> around that same vector.
    /// borsh 1.8.1 encodes <c>Result::Ok</c> as <c>1u8</c> and <c>Result::Err</c> as <c>0u8</c>
    /// (<c>near/borsh-rs</c> <c>impl BorshSerialize for Result</c>).
    /// A notification payload is <c>Serializable&lt;Notification&gt;</c> and is not wrapped in <c>Result</c>.
    /// <c>RpcApiOps</c> and <c>ServerMessageKind</c> use <c>#[borsh(use_discriminant = true)]</c> and fit in one byte.
    /// </summary>
    internal static class KaspaBorshCodec
    {
        internal const byte OpSubscribe = 3;
        internal const byte OpUnsubscribe = 4;
        internal const byte OpBlockAddedNotification = 60;
        internal const byte OpUtxosChangedNotification = 64;
        internal const byte OpVirtualDaaScoreChangedNotification = 66;
        internal const byte OpGetServerInfo = 114;
        internal const byte OpSubmitTransaction = 125;
        internal const byte OpGetBlock = 126;
        internal const byte OpGetBlockDagInfo = 131;
        internal const byte OpGetUtxosByAddresses = 135;
        internal const byte OpGetBalanceByAddress = 136;
        internal const byte OpGetBalancesByAddresses = 137;
        internal const byte OpGetFeeEstimate = 147;

        internal const byte KindSuccess = 0;
        internal const byte KindError = 1;
        internal const byte KindNotification = 0xff;

        internal const ushort ScopeBlockAdded = 0;
        internal const ushort ScopeUtxosChanged = 4;
        internal const ushort ScopeVirtualDaaScoreChanged = 6;

        internal static byte[] EmptyPayload()
        {
            return Wrap(VersionOnly());
        }

        internal static byte[] AddressesPayload(IReadOnlyList<string> addresses)
        {
            var body = new BorshWriter();
            body.U16(1);
            WriteAddressList(body, addresses);
            return Wrap(body.ToArray());
        }

        internal static byte[] EmptyRequest(ulong id, byte op)
        {
            return EncodeRequest(id, op, EmptyPayload());
        }

        internal static byte[] BalanceRequest(ulong id, string address)
        {
            var body = new BorshWriter();
            body.U16(1);
            WriteAddress(body, address);
            return EncodeRequest(id, OpGetBalanceByAddress, Wrap(body.ToArray()));
        }

        internal static byte[] AddressesRequest(ulong id, byte op, IReadOnlyList<string> addresses)
        {
            return EncodeRequest(id, op, AddressesPayload(addresses));
        }

        internal static byte[] SubmitRequest(ulong id, Transaction transaction, bool allowOrphan)
        {
            var body = new BorshWriter();
            body.U16(1);
            body.Payload(writer => WriteTransaction(writer, transaction));
            body.Bool(allowOrphan);
            return EncodeRequest(id, OpSubmitTransaction, Wrap(body.ToArray()));
        }

        internal static byte[] GetBlockRequest(ulong id, uint256 hash, bool includeTransactions)
        {
            var body = new BorshWriter();
            body.U16(1);
            body.Raw(hash.ToBytes());
            body.Bool(includeTransactions);
            return EncodeRequest(id, OpGetBlock, Wrap(body.ToArray()));
        }

        internal static byte[] ScopePayload(ushort scope, IReadOnlyList<string>? addresses)
        {
            // notify/src/scope.rs Serializer: u16 version, then Borsh of Scope.
            // Scope has no use_discriminant. Declaration order is the u8 index.
            // UtxosChangedScope's Borsh derive is only Vec<Address>. It does not repeat the Serializer version.
            var body = new BorshWriter();
            body.U16(1);
            body.U8((byte)scope);
            if (scope == ScopeUtxosChanged)
            {
                WriteAddressList(body, addresses ?? new List<string>());
            }

            return Wrap(body.ToArray());
        }

        internal static byte[] EncodeRequest(ulong id, byte op, byte[] payload)
        {
            var writer = new BorshWriter();
            writer.U8(1);
            writer.U64(id);
            writer.U8(op);
            writer.Raw(payload);
            return writer.ToArray();
        }

        internal static ulong ReadRequestId(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            var reader = new BorshReader(frame);
            if (reader.U8() != 1)
            {
                throw new ArgumentException("wRPC request is missing a numeric id.", nameof(frame));
            }

            return reader.U64();
        }

        internal static bool TryReadResponseId(byte[] frame, out ulong id)
        {
            id = 0;
            try
            {
                var reader = new BorshReader(frame);
                if (reader.U8() != 1)
                {
                    return false;
                }

                id = reader.U64();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        internal static BorshIncoming DecodeFrame(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            var reader = new BorshReader(frame);
            ulong? id = ReadOptionU64(reader);
            var kind = reader.U8();
            byte? op = ReadOptionU8(reader);
            if (kind == KindNotification)
            {
                return new BorshIncoming(id, kind, op, reader.VecBytes(), null);
            }

            if (kind == KindError)
            {
                return new BorshIncoming(id, kind, op, Array.Empty<byte>(), ReadServerError(reader));
            }

            if (kind != KindSuccess)
            {
                throw new FormatException("Unknown Borsh server message kind " + kind.ToString(CultureInfo.InvariantCulture) + ".");
            }

            // borsh 1.8.1: Err = 0, Ok = 1. This is the opposite of a C-style success code.
            var result = reader.U8();
            if (result == 0)
            {
                return new BorshIncoming(id, kind, op, Array.Empty<byte>(), ReadServerError(reader));
            }

            if (result != 1)
            {
                throw new FormatException("Invalid Borsh result tag.");
            }

            return new BorshIncoming(id, kind, op, reader.VecBytes(), null);
        }

        internal static RpcServerInfo ReadServerInfo(byte[] body)
        {
            var reader = Begin(body);
            var api = reader.U16();
            var revision = reader.U16();
            var version = reader.String();
            var network = ReadNetworkId(reader);
            var hasUtxo = reader.Bool();
            var synced = reader.Bool();
            var score = reader.U64();
            return new RpcServerInfo(api, revision, version, network, hasUtxo, synced, score);
        }

        internal static RpcBlockDagInfo ReadBlockDagInfo(byte[] body)
        {
            var reader = Begin(body);
            var network = ReadNetworkId(reader);
            var blocks = reader.U64();
            var headers = reader.U64();
            var tips = ReadHashList(reader);
            var difficulty = reader.F64();
            var median = reader.U64();
            var parents = ReadHashList(reader);
            var pruning = reader.Hash();
            var score = reader.U64();
            var sink = reader.Hash();
            return new RpcBlockDagInfo(network, blocks, headers, tips, difficulty, median, parents, pruning, score, sink);
        }

        internal static Money ReadBalance(byte[] body)
        {
            var reader = Begin(body);
            return new Money(reader.U64());
        }

        internal static IReadOnlyList<RpcAddressBalance> ReadBalances(byte[] body)
        {
            var reader = Begin(body);
            var entries = new List<RpcAddressBalance>();
            ReadSerializerList(reader, item =>
            {
                ExpectVersion(item.U8(), "balance entry");
                var address = ReadAddress(item);
                Money? balance = null;
                var balanceTag = item.U8();
                if (balanceTag == 1)
                {
                    balance = new Money(item.U64());
                }
                else if (balanceTag != 0)
                {
                    throw new FormatException("Invalid Borsh option tag.");
                }

                entries.Add(new RpcAddressBalance(address, balance));
            });
            return entries;
        }

        internal static IReadOnlyList<RpcUtxo> ReadUtxos(byte[] body)
        {
            var reader = Begin(body);
            var entries = new List<RpcUtxo>();
            ReadSerializerList(reader, item => entries.Add(ReadUtxoEntryRow(item)));
            return entries;
        }

        internal static uint256 ReadTransactionId(byte[] body)
        {
            var reader = Begin(body);
            return reader.Hash();
        }

        internal static RpcFeeEstimate ReadFeeEstimate(byte[] body)
        {
            var reader = Begin(body);
            RpcFeeEstimate? estimate = null;
            ReadPayload(reader, inner =>
            {
                ExpectVersion(inner.U16(), "fee estimate");
                var priority = ReadBucket(inner);
                var normal = ReadBucketList(inner);
                var low = ReadBucketList(inner);
                estimate = new RpcFeeEstimate(priority, normal, low);
            });
            if (estimate == null)
            {
                throw new FormatException("Fee estimate payload is missing.");
            }

            return estimate;
        }

        internal static RpcChainBlock? ReadGetBlock(byte[] body, uint256 requested)
        {
            var reader = Begin(body);
            BorshBlock? block = null;
            ReadPayload(reader, inner => block = ReadBlock(inner));
            if (block == null)
            {
                return null;
            }

            return new RpcChainBlock(
                requested,
                block.BlueScore,
                block.AcceptedIdMerkleRoot,
                block.IsChainBlock,
                block.SelectedParentHash,
                block.DirectParent,
                block.ParentsPresent,
                block.VerbosePresent,
                block.BlueScore,
                block.VerboseBlueScore,
                block.Hash,
                block.VerboseHash);
        }

        internal static RpcUtxosChanged? TryReadUtxosChanged(byte[] notificationBody)
        {
            if (!TryOpenNotification(notificationBody, 4, out var inner))
            {
                return null;
            }

            ExpectVersion(inner.U16(), "utxos changed notification");
            var added = new List<RpcUtxo>();
            var removed = new List<RpcUtxo>();
            ReadSerializerList(inner, item => added.Add(ReadUtxoEntryRow(item)));
            ReadSerializerList(inner, item => removed.Add(ReadUtxoEntryRow(item)));
            return new RpcUtxosChanged(added, removed);
        }

        internal static ulong? TryReadVirtualDaa(byte[] notificationBody)
        {
            if (!TryOpenNotification(notificationBody, 6, out var inner))
            {
                return null;
            }

            ExpectVersion(inner.U16(), "virtual DAA notification");
            return inner.U64();
        }

        internal static RpcBlockAdded? TryReadBlockAdded(byte[] notificationBody)
        {
            if (!TryOpenNotification(notificationBody, 0, out var inner))
            {
                return null;
            }

            ExpectVersion(inner.U16(), "block added notification");
            BorshBlock? block = null;
            ReadPayload(inner, payload => block = ReadBlock(payload));
            if (block == null)
            {
                throw new FormatException("Block notification payload is missing.");
            }

            return ToBlockAdded(block);
        }

        private static RpcBlockAdded ToBlockAdded(BorshBlock block)
        {
            return new RpcBlockAdded(
                block.Hash,
                block.Version,
                block.DaaScore,
                block.BlueScore,
                block.Timestamp,
                block.PruningPoint,
                block.AcceptedIdMerkleRoot,
                block.DirectParent,
                block.IsChainBlock,
                block.SelectedParentHash,
                block.Transactions);
        }

        private static bool TryOpenNotification(byte[] body, ushort variant, out BorshReader inner)
        {
            var reader = new BorshReader(body);
            ExpectVersion(reader.U16(), "notification");
            var found = reader.U16();
            inner = new BorshReader(Array.Empty<byte>());
            if (found != variant)
            {
                return false;
            }

            var payload = reader.VecBytes();
            inner = new BorshReader(payload);
            return true;
        }

        private static BorshBlock ReadBlock(BorshReader reader)
        {
            // rpc/core/src/model/block.rs RpcBlock Serializer, then header.rs RpcHeader Serializer.
            ExpectVersion(reader.U16(), "block");
            BorshBlock? block = null;
            ReadPayload(reader, header => block = ReadHeader(header));
            if (block == null)
            {
                throw new FormatException("Block header payload is missing.");
            }

            var transactions = new List<Transaction>();
            ReadSerializerList(reader, item => transactions.Add(ReadTransaction(item)));
            block.Transactions = transactions;
            if (ReadSerializerOption(reader, out var verbose))
            {
                ReadBlockVerbose(verbose, block);
            }

            return block;
        }

        private static BorshBlock ReadHeader(BorshReader reader)
        {
            // header.rs RpcHeader Serializer. BlueWorkType is Uint192: three little-endian u64s, 24 bytes.
            ExpectVersion(reader.U16(), "header");
            var hash = reader.Hash();
            var version = reader.U16();
            var levelCount = reader.U32();
            uint256? direct = null;
            for (uint level = 0; level < levelCount; level++)
            {
                var hashes = ReadHashList(reader);
                if (level == 0 && hashes.Count > 0)
                {
                    direct = hashes[0];
                }
            }

            reader.Hash();
            var accepted = reader.Hash();
            reader.Hash();
            var timestamp = reader.U64();
            reader.U32();
            reader.U64();
            var daa = reader.U64();
            reader.Bytes(24);
            var blue = reader.U64();
            var pruning = reader.Hash();
            return new BorshBlock
            {
                Hash = hash,
                Version = version,
                Timestamp = timestamp,
                DaaScore = daa,
                BlueScore = blue,
                PruningPoint = pruning,
                AcceptedIdMerkleRoot = accepted,
                DirectParent = direct,
                ParentsPresent = true,
                Transactions = new List<Transaction>(),
            };
        }

        private static void ReadBlockVerbose(BorshReader reader, BorshBlock block)
        {
            // block.rs RpcBlockVerboseData Serializer.
            ExpectVersion(reader.U8(), "block verbose");
            block.VerboseHash = reader.Hash();
            reader.F64();
            block.SelectedParentHash = reader.Hash();
            ReadHashList(reader);
            reader.Bool();
            block.VerboseBlueScore = reader.U64();
            ReadHashList(reader);
            ReadHashList(reader);
            ReadHashList(reader);
            block.IsChainBlock = reader.Bool();
            block.VerbosePresent = true;
        }

        private static Transaction ReadTransaction(BorshReader reader)
        {
            ExpectVersion(reader.U16(), "transaction");
            var version = reader.U16();
            var inputs = new List<TxIn>();
            ReadSerializerList(reader, item => inputs.Add(ReadInput(item)));
            var outputs = new List<TxOut>();
            ReadSerializerList(reader, item => outputs.Add(ReadOutput(item)));
            var lockTime = reader.U64();
            var subnetwork = new SubnetworkId(reader.Bytes(SubnetworkId.Length));
            var gas = reader.U64();
            var payload = reader.VecBytes();
            var mass = reader.U64();
            SkipSerializerOption(reader);
            var transaction = new Transaction(version, inputs, outputs, lockTime, subnetwork, gas, payload);
            transaction.StorageMass = mass;
            return transaction;
        }

        private static TxIn ReadInput(BorshReader reader)
        {
            var version = reader.U8();
            OutPoint? outpoint = null;
            ReadPayload(reader, item =>
            {
                item.U8();
                outpoint = new OutPoint(item.Hash(), item.U32());
            });
            if (outpoint == null)
            {
                throw new FormatException("Transaction input is missing an outpoint.");
            }

            var scriptBytes = reader.VecBytes();
            var script = scriptBytes.Length == 0 ? Script.Empty : new Script(scriptBytes);
            var sequence = reader.U64();
            var sigops = reader.U8();
            SkipSerializerOption(reader);
            ushort? budget = null;
            if (version > 1)
            {
                budget = reader.U16();
            }

            var input = new TxIn(outpoint, script, sequence, sigops);
            if (budget.HasValue)
            {
                input.ComputeBudget = budget.Value;
            }

            return input;
        }

        private static TxOut ReadOutput(BorshReader reader)
        {
            var version = reader.U8();
            var value = reader.U64();
            var script = ReadScript(reader);
            SkipSerializerOption(reader);
            CovenantBinding? covenant = null;
            if (version > 1 && ReadSerializerOption(reader, out var binding))
            {
                ExpectVersion(binding.U8(), "covenant");
                covenant = new CovenantBinding(binding.U16(), binding.Hash());
            }

            var output = new TxOut(value, script);
            output.Covenant = covenant;
            return output;
        }

        private static RpcUtxo ReadUtxoEntryRow(BorshReader reader)
        {
            // address.rs RpcUtxosByAddressesEntry Serializer.
            ExpectVersion(reader.U8(), "utxo row");
            var address = string.Empty;
            var addressTag = reader.U8();
            if (addressTag == 1)
            {
                address = ReadAddress(reader);
            }
            else if (addressTag != 0)
            {
                throw new FormatException("Invalid Borsh option tag.");
            }

            OutPoint? outpoint = null;
            ReadPayload(reader, item =>
            {
                item.U8();
                outpoint = new OutPoint(item.Hash(), item.U32());
            });
            UtxoEntry? entry = null;
            ReadPayload(reader, item => entry = ReadUtxoEntry(item));
            if (outpoint == null || entry == null)
            {
                throw new FormatException("UTXO row is missing an outpoint or entry.");
            }

            return new RpcUtxo(address, outpoint, entry);
        }

        private static UtxoEntry ReadUtxoEntry(BorshReader reader)
        {
            var version = reader.U8();
            var amount = reader.U64();
            var script = ReadScript(reader);
            var daa = reader.U64();
            var coinbase = reader.Bool();
            uint256? covenant = null;
            if (version > 1)
            {
                var covenantTag = reader.U8();
                if (covenantTag == 1)
                {
                    covenant = reader.Hash();
                }
                else if (covenantTag != 0)
                {
                    throw new FormatException("Invalid Borsh option tag.");
                }
            }

            return new UtxoEntry(amount, script, daa, coinbase, covenant);
        }

        private static ScriptPubKey ReadScript(BorshReader reader)
        {
            var version = reader.U16();
            return new ScriptPubKey(version, reader.VecBytes());
        }

        private static RpcFeeBucket ReadBucket(BorshReader reader)
        {
            return new RpcFeeBucket(reader.F64(), reader.F64());
        }

        private static List<RpcFeeBucket> ReadBucketList(BorshReader reader)
        {
            var count = reader.U32();
            var buckets = new List<RpcFeeBucket>();
            for (uint i = 0; i < count; i++)
            {
                buckets.Add(ReadBucket(reader));
            }

            return buckets;
        }

        private static List<uint256> ReadHashList(BorshReader reader)
        {
            var count = reader.U32();
            var hashes = new List<uint256>();
            for (uint i = 0; i < count; i++)
            {
                hashes.Add(reader.Hash());
            }

            return hashes;
        }

        private static string ReadNetworkId(BorshReader reader)
        {
            // consensus/core/src/network.rs NetworkType has no use_discriminant.
            // Order is Mainnet, Testnet, Devnet, Simnet. That is not the address Prefix order.
            var typeByte = reader.U8();
            string name;
            if (typeByte == 0)
            {
                name = "mainnet";
            }
            else if (typeByte == 1)
            {
                name = "testnet";
            }
            else if (typeByte == 2)
            {
                name = "devnet";
            }
            else if (typeByte == 3)
            {
                name = "simnet";
            }
            else
            {
                throw new FormatException("Unknown Borsh network type.");
            }

            var suffix = reader.U8();
            if (suffix == 1)
            {
                name = name + "-" + reader.U32().ToString(CultureInfo.InvariantCulture);
            }
            else if (suffix != 0)
            {
                throw new FormatException("Invalid Borsh option tag.");
            }

            return name;
        }

        private static void WriteTransaction(BorshWriter writer, Transaction transaction)
        {
            writer.U16(1);
            writer.U16(transaction.Version);
            writer.Payload(inputs =>
            {
                inputs.U32(Count(transaction.Inputs.Count));
                for (var i = 0; i < transaction.Inputs.Count; i++)
                {
                    var input = transaction.Inputs[i];
                    inputs.Payload(item => WriteInput(item, input));
                }
            });
            writer.Payload(outputs =>
            {
                outputs.U32(Count(transaction.Outputs.Count));
                for (var i = 0; i < transaction.Outputs.Count; i++)
                {
                    var output = transaction.Outputs[i];
                    outputs.Payload(item => WriteOutput(item, output));
                }
            });
            writer.U64(transaction.LockTime);
            writer.Raw(transaction.SubnetworkId.ToBytes());
            writer.U64(transaction.Gas);
            writer.VecBytes(transaction.Payload);
            writer.U64(transaction.StorageMass);
            writer.Payload(option => option.U8(0));
        }

        private static void WriteInput(BorshWriter writer, TxIn input)
        {
            writer.U8(2);
            writer.Payload(outpoint =>
            {
                outpoint.U8(1);
                outpoint.Raw(input.PreviousOutpoint.TransactionId.ToBytes());
                outpoint.U32(input.PreviousOutpoint.Index);
            });
            writer.VecBytes(input.SignatureScript.ToBytes());
            writer.U64(input.Sequence);
            writer.U8(input.SigOpCount);
            writer.Payload(option => option.U8(0));
            writer.U16(input.ComputeBudget ?? 0);
        }

        private static void WriteOutput(BorshWriter writer, TxOut output)
        {
            writer.U8(2);
            writer.U64(output.Value.Sompi);
            WriteScript(writer, output.ScriptPublicKey);
            writer.Payload(option => option.U8(0));
            if (output.Covenant == null)
            {
                writer.Payload(option => option.U8(0));
                return;
            }

            var covenant = output.Covenant;
            writer.Payload(option =>
            {
                option.U8(1);
                option.Payload(binding =>
                {
                    binding.U8(1);
                    binding.U16(covenant.AuthorizingInput);
                    binding.Raw(covenant.CovenantId.ToBytes());
                });
            });
        }

        private static void WriteScript(BorshWriter writer, ScriptPubKey script)
        {
            writer.U16(script.Version);
            writer.VecBytes(script.Script);
        }

        private static void WriteAddressList(BorshWriter writer, IReadOnlyList<string> addresses)
        {
            writer.U32(Count(addresses.Count));
            for (var i = 0; i < addresses.Count; i++)
            {
                WriteAddress(writer, addresses[i]);
            }
        }

        private static void WriteAddress(BorshWriter writer, string address)
        {
            // crypto/addresses Prefix use_discriminant: Mainnet 0, Testnet 1, Simnet 2, Devnet 3.
            // Version is PubKey 0, PubKeyECDSA 1, ScriptHash 8. Payload is a Borsh byte vector.
            KaspaAddress parsed;
            try
            {
                parsed = KaspaAddress.Parse(address);
            }
            catch (FormatException)
            {
                throw;
            }

            byte prefix;
            var text = parsed.Network.AddressPrefix;
            if (text == Network.Mainnet.AddressPrefix)
            {
                prefix = 0;
            }
            else if (text == Network.Testnet.AddressPrefix)
            {
                prefix = 1;
            }
            else if (text == Network.Simnet.AddressPrefix)
            {
                prefix = 2;
            }
            else if (text == Network.Devnet.AddressPrefix)
            {
                prefix = 3;
            }
            else
            {
                throw new FormatException("Unknown Kaspa address prefix.");
            }

            writer.U8(prefix);
            writer.U8((byte)parsed.Version);
            writer.VecBytes(parsed.Payload);
        }

        private static string ReadAddress(BorshReader reader)
        {
            var prefix = reader.U8();
            var version = reader.U8();
            var payload = reader.VecBytes();
            Network network;
            if (prefix == 0)
            {
                network = Network.Mainnet;
            }
            else if (prefix == 1)
            {
                network = Network.Testnet;
            }
            else if (prefix == 2)
            {
                network = Network.Simnet;
            }
            else if (prefix == 3)
            {
                network = Network.Devnet;
            }
            else
            {
                throw new FormatException("Unknown Borsh address prefix.");
            }

            KaspaAddress address;
            if (version == (byte)KaspaAddressVersion.PubKey)
            {
                address = new KaspaPubKeyAddress(payload, network);
            }
            else if (version == (byte)KaspaAddressVersion.PubKeyECDSA)
            {
                address = new KaspaPubKeyEcdsaAddress(payload, network);
            }
            else if (version == (byte)KaspaAddressVersion.ScriptHash)
            {
                address = new KaspaScriptHashAddress(payload, network);
            }
            else
            {
                throw new FormatException("Unknown Borsh address version.");
            }

            return address.ToString();
        }

        private static KaspaRpcException ReadServerError(BorshReader reader)
        {
            // workflow-rs rpc/src/error.rs ServerError. No use_discriminant, so the tag is the declaration index.
            var code = reader.U8();
            string message;
            if (code == 0)
            {
                message = "connection is closed";
            }
            else if (code == 1)
            {
                message = "RPC call timed out";
            }
            else if (code == 2)
            {
                message = "no data";
            }
            else if (code == 3)
            {
                message = "RPC method not found";
            }
            else if (code == 4)
            {
                message = "resource lock error";
            }
            else if (code == 5)
            {
                message = "not a borsh request";
            }
            else if (code == 6)
            {
                message = "not a serde request";
            }
            else if (code == 7)
            {
                message = "request serialization error";
            }
            else if (code == 8)
            {
                message = "request deserialization error";
            }
            else if (code == 9)
            {
                message = "response serialization error";
            }
            else if (code == 10)
            {
                // thiserror Display does not include the inner string. The bytes are still consumed.
                reader.String();
                message = "request deserialization error";
            }
            else if (code == 11)
            {
                reader.String();
                message = "response deserialization error";
            }
            else if (code == 12)
            {
                reader.VecBytes();
                message = "data";
            }
            else if (code == 13)
            {
                message = reader.String();
            }
            else if (code == 14)
            {
                message = "WebSocket -> " + reader.String();
            }
            else if (code == 15)
            {
                message = "Receiver channel";
            }
            else if (code == 16)
            {
                message = "Receiver channel send";
            }
            else
            {
                message = "Unknown Borsh server error " + code.ToString(CultureInfo.InvariantCulture) + ".";
            }

            return new KaspaRpcException(code, message);
        }

        private static byte[] VersionOnly()
        {
            var writer = new BorshWriter();
            writer.U16(1);
            return writer.ToArray();
        }

        private static byte[] Wrap(byte[] serializerBody)
        {
            var writer = new BorshWriter();
            writer.VecBytes(serializerBody);
            return writer.ToArray();
        }

        private static BorshReader Begin(byte[] body)
        {
            var reader = new BorshReader(body);
            ExpectVersion(reader.U16(), "response");
            return reader;
        }

        private static void ExpectVersion(ushort version, string name)
        {
            if (version != 1)
            {
                throw new FormatException("Unsupported Borsh " + name + " version " + version.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static void ExpectVersion(byte version, string name)
        {
            if (version != 1)
            {
                throw new FormatException("Unsupported Borsh " + name + " version " + version.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static ulong? ReadOptionU64(BorshReader reader)
        {
            var tag = reader.U8();
            if (tag == 0)
            {
                return null;
            }

            if (tag != 1)
            {
                throw new FormatException("Invalid Borsh id tag.");
            }

            return reader.U64();
        }

        private static byte? ReadOptionU8(BorshReader reader)
        {
            var tag = reader.U8();
            if (tag == 0)
            {
                return null;
            }

            if (tag != 1)
            {
                throw new FormatException("Invalid Borsh op tag.");
            }

            return reader.U8();
        }

        private static void ReadPayload(BorshReader reader, Action<BorshReader> read)
        {
            read(new BorshReader(reader.VecBytes()));
        }

        private static void ReadSerializerList(BorshReader reader, Action<BorshReader> readItem)
        {
            // serialize!(Vec<T>) is a length-prefixed Serializer::Vec: u32 count, then each item is itself a payload.
            ReadPayload(reader, list =>
            {
                var count = list.U32();
                for (uint i = 0; i < count; i++)
                {
                    ReadPayload(list, readItem);
                }
            });
        }

        private static bool ReadSerializerOption(BorshReader reader, out BorshReader inner)
        {
            // serialize!(Option<T>) None is a payload of 0u8. Some is 1u8 plus a payload of T.
            var option = new BorshReader(reader.VecBytes());
            var tag = option.U8();
            if (tag == 0)
            {
                inner = new BorshReader(Array.Empty<byte>());
                return false;
            }

            if (tag != 1)
            {
                throw new FormatException("Invalid Borsh option tag.");
            }

            inner = new BorshReader(option.VecBytes());
            return true;
        }

        private static void SkipSerializerOption(BorshReader reader)
        {
            ReadSerializerOption(reader, out _);
        }

        private static uint Count(int count)
        {
            if (count < 0 || count > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return (uint)count;
        }

        private sealed class BorshBlock
        {
            public uint256 Hash { get; set; } = uint256.Zero;

            public ushort Version { get; set; }

            public ulong Timestamp { get; set; }

            public ulong DaaScore { get; set; }

            public ulong BlueScore { get; set; }

            public uint256 PruningPoint { get; set; } = uint256.Zero;

            public uint256? AcceptedIdMerkleRoot { get; set; }

            public uint256? DirectParent { get; set; }

            public bool ParentsPresent { get; set; }

            public bool VerbosePresent { get; set; }

            public uint256? VerboseHash { get; set; }

            public ulong? VerboseBlueScore { get; set; }

            public bool IsChainBlock { get; set; }

            public uint256? SelectedParentHash { get; set; }

            public List<Transaction> Transactions { get; set; } = new List<Transaction>();
        }

        private sealed class BorshWriter
        {
            private readonly MemoryStream _stream = new MemoryStream();

            public void U8(byte value)
            {
                _stream.WriteByte(value);
            }

            public void Bool(bool value)
            {
                U8(value ? (byte)1 : (byte)0);
            }

            public void U16(ushort value)
            {
                var bytes = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
                Raw(bytes);
            }

            public void U32(uint value)
            {
                var bytes = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
                Raw(bytes);
            }

            public void U64(ulong value)
            {
                var bytes = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
                Raw(bytes);
            }

            public void Raw(ReadOnlySpan<byte> bytes)
            {
                var copy = bytes.ToArray();
                _stream.Write(copy, 0, copy.Length);
            }

            public void VecBytes(ReadOnlySpan<byte> bytes)
            {
                if (bytes.Length > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(bytes));
                }

                U32((uint)bytes.Length);
                Raw(bytes);
            }

            public void Payload(Action<BorshWriter> write)
            {
                var inner = new BorshWriter();
                write(inner);
                VecBytes(inner.ToArray());
            }

            public byte[] ToArray()
            {
                return _stream.ToArray();
            }
        }

        private sealed class BorshReader
        {
            private readonly byte[] _data;
            private int _offset;

            public BorshReader(byte[] data)
            {
                _data = data ?? throw new ArgumentNullException(nameof(data));
            }

            public byte U8()
            {
                if (_offset >= _data.Length)
                {
                    throw new FormatException("Borsh message is truncated.");
                }

                return _data[_offset++];
            }

            public bool Bool()
            {
                var value = U8();
                if (value == 0)
                {
                    return false;
                }

                if (value == 1)
                {
                    return true;
                }

                throw new FormatException("Invalid Borsh bool.");
            }

            public ushort U16()
            {
                return BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
            }

            public uint U32()
            {
                return BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
            }

            public ulong U64()
            {
                return BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
            }

            public double F64()
            {
                return BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(Take(8)));
            }

            public uint256 Hash()
            {
                return new uint256(Bytes(32));
            }

            public byte[] Bytes(int length)
            {
                var slice = Take(length);
                var copy = new byte[length];
                Buffer.BlockCopy(slice, 0, copy, 0, length);
                return copy;
            }

            public byte[] VecBytes()
            {
                var length = U32();
                if (length > int.MaxValue)
                {
                    throw new FormatException("Borsh message is truncated.");
                }

                return Bytes((int)length);
            }

            public string String()
            {
                try
                {
                    return Encoding.UTF8.GetString(VecBytes());
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Borsh string is not UTF-8.", exception);
                }
            }

            private byte[] Take(int length)
            {
                if (length < 0 || _offset > _data.Length - length)
                {
                    throw new FormatException("Borsh message is truncated.");
                }

                var slice = new byte[length];
                Buffer.BlockCopy(_data, _offset, slice, 0, length);
                _offset += length;
                return slice;
            }
        }
    }

    internal sealed class BorshIncoming
    {
        public BorshIncoming(ulong? id, byte kind, byte? op, byte[] body, KaspaRpcException? error)
        {
            Id = id;
            Kind = kind;
            Op = op;
            Body = body;
            Error = error;
        }

        public ulong? Id { get; }

        public byte Kind { get; }

        public byte? Op { get; }

        public byte[] Body { get; }

        public KaspaRpcException? Error { get; }
    }
}
