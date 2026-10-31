using System.Globalization;
using System.Text.Json;
using NKaspa.RPC;
using Xunit;

namespace NKaspa.Tests
{
    public sealed class ConsensusSeqCommitAccessorTests
    {
        [Fact]
        public void Consensus_accessor_walks_the_selected_parent_chain()
        {
            var genesis = new uint256(1);
            var parent = new uint256(2);
            var sink = new uint256(3);
            var side = new uint256(4);
            var parentRoot = new uint256(20);
            var methods = new List<string>();
            var blocks = Chain(genesis, parent, sink, parentRoot);
            blocks[side.ToString()] = Block(side, 40, new uint256(40), genesis, chain: false);
            var accessor = Accessor(methods, blocks, sink, finalityDepth: 1000);

            Assert.True(accessor.IsChainAncestor(parent));
            Assert.Equal(parentRoot, accessor.SeqCommitmentWithinDepth(parent));
            Assert.False(accessor.IsChainAncestor(side));
            var offChain = Assert.Throws<ConsensusSeqCommitException>(() => accessor.SeqCommitmentWithinDepth(side));
            Assert.Equal(ConsensusSeqCommitFailure.InconsistentData, offChain.Failure);
            Assert.DoesNotContain("submitTransaction", methods);
            Assert.All(methods, method => Assert.True(method == "getBlock" || method == "getBlockDagInfo"));
            Assert.True(accessor.RequestCount <= accessor.MaxRequests);
        }

        [Fact]
        public void Consensus_accessor_uses_the_chain_block_selected_parent_for_the_window()
        {
            var genesis = new uint256(1);
            var parent = new uint256(2);
            var sink = new uint256(3);
            var parentRoot = new uint256(20);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 1, new uint256(10), null, chain: true),
                [parent.ToString()] = Block(parent, 100, parentRoot, genesis, chain: true),
                [sink.ToString()] = Block(sink, 200, new uint256(30), parent, chain: true),
            };
            var byChainBlock = Accessor(new List<string>(), blocks, sink, finalityDepth: 50);
            byChainBlock.ChainBlock = sink;
            Assert.True(byChainBlock.IsChainAncestor(parent));
            Assert.Equal(parentRoot, byChainBlock.SeqCommitmentWithinDepth(parent));
            Assert.True(byChainBlock.IsChainAncestor(genesis));
            Assert.Null(byChainBlock.SeqCommitmentWithinDepth(genesis));
            Assert.False(byChainBlock.IsChainAncestor(sink));

            var byPointOfView = Accessor(new List<string>(), blocks, sink, finalityDepth: 50);
            byPointOfView.PointOfView = parent;
            Assert.Equal(parentRoot, byPointOfView.SeqCommitmentWithinDepth(parent));
            Assert.Null(byPointOfView.SeqCommitmentWithinDepth(genesis));

            byChainBlock.PointOfView = parent;
            Assert.Throws<ArgumentException>(() => byChainBlock.IsChainAncestor(parent));
        }

        [Fact]
        public void Consensus_accessor_window_boundary_is_strict()
        {
            var genesis = new uint256(1);
            var edge = new uint256(2);
            var inside = new uint256(3);
            var sink = new uint256(4);
            var edgeRoot = new uint256(21);
            var insideRoot = new uint256(22);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 1, new uint256(10), null, chain: true),
                [edge.ToString()] = Block(edge, 900, edgeRoot, genesis, chain: true),
                [inside.ToString()] = Block(inside, 901, insideRoot, edge, chain: true),
                [sink.ToString()] = Block(sink, 1000, new uint256(30), inside, chain: true),
            };
            var accessor = Accessor(new List<string>(), blocks, sink, finalityDepth: 100);

            Assert.True(accessor.IsChainAncestor(inside));
            Assert.Equal(insideRoot, accessor.SeqCommitmentWithinDepth(inside));
            Assert.True(accessor.IsChainAncestor(edge));
            Assert.Null(accessor.SeqCommitmentWithinDepth(edge));
            Assert.Equal(new uint256(30), accessor.SeqCommitmentWithinDepth(sink));
        }

        [Fact]
        public void Consensus_accessor_throws_when_the_block_or_its_parent_is_missing()
        {
            var genesis = new uint256(1);
            var parent = new uint256(2);
            var sink = new uint256(3);
            var missing = new uint256(9);
            var blocks = Chain(genesis, parent, sink, new uint256(20));
            var rpcError = Accessor(new List<string>(), blocks, sink, missingIsError: true);
            var missingError = Assert.Throws<ConsensusSeqCommitException>(() => rpcError.IsChainAncestor(missing));
            Assert.Equal(ConsensusSeqCommitFailure.IncompleteNodeData, missingError.Failure);
            Assert.Equal(missing, missingError.Block);

            var empty = Accessor(new List<string>(), blocks, sink);
            Assert.Equal(ConsensusSeqCommitFailure.IncompleteNodeData, Assert.Throws<ConsensusSeqCommitException>(() => empty.IsChainAncestor(missing)).Failure);

            blocks[sink.ToString()] = Block(sink, 300, new uint256(30), new uint256(8), chain: true);
            var broken = Accessor(new List<string>(), blocks, sink);
            var parentMissing = Assert.Throws<ConsensusSeqCommitException>(() => broken.IsChainAncestor(parent));
            Assert.Equal(ConsensusSeqCommitFailure.IncompleteNodeData, parentMissing.Failure);
        }

        [Fact]
        public void Consensus_accessor_throws_when_the_header_disagrees_with_verbose_data()
        {
            var sink = new uint256(3);
            var parent = new uint256(2);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [sink.ToString()] = Block(sink, 10, new uint256(30), parent, chain: true, selectedParent: new uint256(7)),
            };
            var accessor = Accessor(new List<string>(), blocks, sink);
            accessor.ChainBlock = sink;
            var disagreed = Assert.Throws<ConsensusSeqCommitException>(() => accessor.IsChainAncestor(parent));
            Assert.Equal(ConsensusSeqCommitFailure.InconsistentData, disagreed.Failure);

            blocks[sink.ToString()] = Block(sink, 10, new uint256(30), null, chain: true, headerScore: 10, verboseScore: 11);
            var scores = Assert.Throws<ConsensusSeqCommitException>(() => Accessor(new List<string>(), blocks, sink).IsChainAncestor(sink));
            Assert.Equal(ConsensusSeqCommitFailure.InconsistentData, scores.Failure);

            blocks[sink.ToString()] = Block(sink, 10, new uint256(30), null, chain: true, headerHash: parent);
            var hash = Assert.Throws<ConsensusSeqCommitException>(() => Accessor(new List<string>(), blocks, sink).IsChainAncestor(sink));
            Assert.Equal(ConsensusSeqCommitFailure.InconsistentData, hash.Failure);

            blocks[sink.ToString()] = Block(sink, 10, null, null, chain: true);
            var noRoot = Accessor(new List<string>(), blocks, sink);
            Assert.True(noRoot.IsChainAncestor(sink));
            var missingRoot = Assert.Throws<ConsensusSeqCommitException>(() => noRoot.SeqCommitmentWithinDepth(sink));
            Assert.Equal(ConsensusSeqCommitFailure.IncompleteNodeData, missingRoot.Failure);
        }

        [Fact]
        public void Consensus_accessor_throws_when_the_chain_flag_disagrees_with_the_walk()
        {
            var genesis = new uint256(1);
            var parent = new uint256(2);
            var sink = new uint256(3);
            var blocks = Chain(genesis, parent, sink, new uint256(20));
            blocks[parent.ToString()] = Block(parent, 200, new uint256(20), genesis, chain: false);
            var flagged = Assert.Throws<ConsensusSeqCommitException>(() => Accessor(new List<string>(), blocks, sink).IsChainAncestor(parent));
            Assert.Equal(ConsensusSeqCommitFailure.InconsistentData, flagged.Failure);

            blocks[sink.ToString()] = Block(sink, 300, new uint256(30), parent, chain: false);
            var sinkFlag = Assert.Throws<ConsensusSeqCommitException>(() => Accessor(new List<string>(), blocks, sink).IsChainAncestor(sink));
            Assert.Equal(ConsensusSeqCommitFailure.InconsistentData, sinkFlag.Failure);

            blocks = Chain(genesis, parent, sink, new uint256(20));
            blocks[parent.ToString()] = Block(parent, 200, new uint256(20), genesis, chain: true, includeParents: false);
            var noParents = Assert.Throws<ConsensusSeqCommitException>(() => Accessor(new List<string>(), blocks, sink).IsChainAncestor(genesis));
            Assert.Equal(ConsensusSeqCommitFailure.IncompleteNodeData, noParents.Failure);
        }

        [Fact]
        public void Consensus_accessor_accepts_forward_growth_during_the_walk()
        {
            var genesis = new uint256(1);
            var edge = new uint256(2);
            var sink = new uint256(3);
            var next = new uint256(5);
            var next2 = new uint256(6);
            var edgeRoot = new uint256(21);
            var genesisRoot = new uint256(10);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 100, genesisRoot, null, chain: true),
                [edge.ToString()] = Block(edge, 250, edgeRoot, genesis, chain: true),
                [sink.ToString()] = Block(sink, 300, new uint256(30), edge, chain: true),
                [next.ToString()] = Block(next, 360, new uint256(40), sink, chain: true),
                [next2.ToString()] = Block(next2, 420, new uint256(50), next, chain: true),
            };
            var reads = 0;
            var methods = new List<string>();
            var accessor = Accessor(methods, blocks, () =>
            {
                var tip = reads < 3 ? sink : next2;
                reads++;
                return tip;
            }, finalityDepth: 100);

            // 250 + 100 > 300, so the snapshot sink still admits edge.
            // The grown tip at 420 would reject it. The answer stays on the snapshot.
            Assert.Equal(edgeRoot, accessor.SeqCommitmentWithinDepth(edge));
            Assert.Null(accessor.SeqCommitmentWithinDepth(genesis));
            Assert.DoesNotContain("submitTransaction", methods);
            Assert.All(methods, method => Assert.True(method == "getBlock" || method == "getBlockDagInfo"));
            Assert.True(accessor.RequestCount <= accessor.MaxRequests);
            Assert.Equal(ConsensusSeqCommitAccessor.DefaultReorgRetries, accessor.MaxReorgRetries);
        }

        [Fact]
        public void Consensus_accessor_treats_a_later_call_on_a_grown_chain_as_the_same_chain()
        {
            var genesis = new uint256(1);
            var parent = new uint256(2);
            var sink = new uint256(3);
            var next = new uint256(5);
            var oldRoot = new uint256(30);
            var nextRoot = new uint256(50);
            var blocks = Chain(genesis, parent, sink, new uint256(20));
            blocks[sink.ToString()] = Block(sink, 300, oldRoot, parent, chain: true);
            blocks[next.ToString()] = Block(next, 400, nextRoot, sink, chain: true);
            var current = sink;
            var accessor = Accessor(new List<string>(), blocks, () => current);

            Assert.Equal(oldRoot, accessor.SeqCommitmentWithinDepth(sink));
            current = next;
            Assert.Equal(oldRoot, accessor.SeqCommitmentWithinDepth(sink));
            Assert.Equal(nextRoot, accessor.SeqCommitmentWithinDepth(next));
        }

        [Fact]
        public void Consensus_accessor_retries_a_real_reorg_then_answers_on_the_new_chain()
        {
            var genesis = new uint256(1);
            var sink = new uint256(3);
            var rivalParent = new uint256(6);
            var rival = new uint256(5);
            var genesisRoot = new uint256(10);
            var rivalRoot = new uint256(50);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 100, genesisRoot, null, chain: true),
                [sink.ToString()] = Block(sink, 300, new uint256(30), genesis, chain: true),
                [rivalParent.ToString()] = Block(rivalParent, 220, new uint256(22), genesis, chain: true),
                [rival.ToString()] = Block(rival, 400, rivalRoot, rivalParent, chain: true),
            };
            var dagReads = 0;
            var methods = new List<string>();
            var accessor = Accessor(methods, blocks, method =>
            {
                if (method != "getBlockDagInfo")
                {
                    return dagReads <= 1 ? sink : rival;
                }

                dagReads++;
                return dagReads == 1 ? sink : rival;
            });

            Assert.Equal(rivalRoot, accessor.SeqCommitmentWithinDepth(rival));
            Assert.True(accessor.IsChainAncestor(rivalParent));
            Assert.Equal(genesisRoot, accessor.SeqCommitmentWithinDepth(genesis));
            Assert.DoesNotContain("submitTransaction", methods);
            Assert.True(methods.FindAll(method => method == "getBlockDagInfo").Count >= 3);
        }

        [Fact]
        public void Consensus_accessor_throws_when_reorg_retries_are_exhausted()
        {
            var genesis = new uint256(1);
            var tips = new[] { new uint256(2), new uint256(3), new uint256(4), new uint256(5) };
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 100, new uint256(10), null, chain: true),
            };
            for (var i = 0; i < tips.Length; i++)
            {
                blocks[tips[i].ToString()] = Block(tips[i], 1000UL + (ulong)i, new uint256(30UL + (ulong)i), genesis, chain: true);
            }

            var dagReads = 0;
            var methods = new List<string>();
            var accessor = Accessor(methods, blocks, method =>
            {
                var index = Math.Min(dagReads, tips.Length - 1);
                if (method == "getBlockDagInfo")
                {
                    var tip = tips[index];
                    dagReads++;
                    return tip;
                }

                return tips[Math.Min(dagReads, tips.Length - 1)];
            });
            accessor.MaxReorgRetries = 1;

            var reorg = Assert.Throws<ConsensusSeqCommitException>(() => accessor.SeqCommitmentWithinDepth(genesis));
            Assert.Equal(ConsensusSeqCommitFailure.Reorg, reorg.Failure);
            Assert.True(methods.FindAll(method => method == "getBlockDagInfo").Count >= 4);
            Assert.True(accessor.RequestCount >= 8);
            Assert.DoesNotContain("submitTransaction", methods);
        }

        [Fact]
        public void Consensus_accessor_treats_a_sink_that_lost_the_chain_flag_as_a_reorg()
        {
            var parent = new uint256(2);
            var oldSink = new uint256(3);
            var next = new uint256(5);
            var nextRoot = new uint256(50);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [parent.ToString()] = Block(parent, 200, new uint256(20), null, chain: true),
                [oldSink.ToString()] = Block(oldSink, 300, new uint256(30), parent, chain: false),
                [next.ToString()] = Block(next, 400, nextRoot, parent, chain: true),
            };
            var reads = 0;
            var healed = Accessor(new List<string>(), blocks, () =>
            {
                var tip = reads == 0 ? oldSink : next;
                reads++;
                return tip;
            });
            healed.MaxReorgRetries = 0;
            Assert.Equal(nextRoot, healed.SeqCommitmentWithinDepth(next));

            var stuck = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [parent.ToString()] = Block(parent, 200, new uint256(20), null, chain: true),
                [oldSink.ToString()] = Block(oldSink, 300, new uint256(30), parent, chain: false),
            };
            var same = Accessor(new List<string>(), stuck, oldSink);
            same.MaxReorgRetries = 0;
            var inconsistent = Assert.Throws<ConsensusSeqCommitException>(() => same.SeqCommitmentWithinDepth(oldSink));
            Assert.Equal(ConsensusSeqCommitFailure.InconsistentData, inconsistent.Failure);
        }

        [Fact]
        public void Consensus_accessor_treats_a_chain_flag_lost_during_the_walk_as_a_reorg()
        {
            var genesis = new uint256(1);
            var parent = new uint256(2);
            var sink = new uint256(3);
            var next = new uint256(5);
            var blocks = Chain(genesis, parent, sink, new uint256(20));
            blocks[parent.ToString()] = Block(parent, 200, new uint256(20), genesis, chain: false);
            blocks[next.ToString()] = Block(next, 400, new uint256(50), sink, chain: true);
            var reads = 0;
            var accessor = Accessor(new List<string>(), blocks, () =>
            {
                var tip = reads < 3 ? sink : next;
                reads++;
                return tip;
            });
            accessor.MaxReorgRetries = 0;

            var reorg = Assert.Throws<ConsensusSeqCommitException>(() => accessor.IsChainAncestor(parent));
            Assert.Equal(ConsensusSeqCommitFailure.Reorg, reorg.Failure);
            Assert.Equal(next, reorg.Block);
        }

        [Fact]
        public void Consensus_accessor_stable_depth_steps_the_viewpoint_back()
        {
            var genesis = new uint256(1);
            var mid = new uint256(2);
            var near = new uint256(3);
            var sink = new uint256(4);
            var genesisRoot = new uint256(10);
            var midRoot = new uint256(20);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 100, genesisRoot, null, chain: true),
                [mid.ToString()] = Block(mid, 200, midRoot, genesis, chain: true),
                [near.ToString()] = Block(near, 300, new uint256(30), mid, chain: true),
                [sink.ToString()] = Block(sink, 400, new uint256(40), near, chain: true),
            };
            var accessor = Accessor(new List<string>(), blocks, sink, finalityDepth: 150);
            accessor.StableDepth = 2;

            Assert.Equal(midRoot, accessor.SeqCommitmentWithinDepth(mid));
            Assert.Equal(genesisRoot, accessor.SeqCommitmentWithinDepth(genesis));
            Assert.False(accessor.IsChainAncestor(near));
            Assert.False(accessor.IsChainAncestor(sink));
        }

        [Fact]
        public void Consensus_accessor_throws_when_the_named_chain_block_leaves_the_chain()
        {
            var genesis = new uint256(1);
            var parent = new uint256(2);
            var sink = new uint256(3);
            var side = new uint256(4);
            var blocks = Chain(genesis, parent, sink, new uint256(20));
            blocks[side.ToString()] = Block(side, 50, new uint256(40), genesis, chain: false);
            var accessor = Accessor(new List<string>(), blocks, sink);
            accessor.ChainBlock = side;
            var error = Assert.Throws<ConsensusSeqCommitException>(() => accessor.IsChainAncestor(parent));
            Assert.Equal(ConsensusSeqCommitFailure.Reorg, error.Failure);
        }

        [Fact]
        public void Consensus_accessor_stops_at_the_request_limit_and_caches_blocks()
        {
            var sink = new uint256(3);
            var root = new uint256(30);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [sink.ToString()] = Block(sink, 10, root, null, chain: true),
            };
            var limited = Accessor(new List<string>(), blocks, sink, maxRequests: 2);
            var limit = Assert.Throws<ConsensusSeqCommitException>(() => limited.IsChainAncestor(sink));
            Assert.Equal(ConsensusSeqCommitFailure.RequestLimit, limit.Failure);
            Assert.Equal(2, limited.RequestCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConsensusSeqCommitAccessor(new KaspaWrpcJsonClient(new ScriptedChannel()), maxRequests: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => limited.StableDepth = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => limited.MaxReorgRetries = -1);
            var defaults = new ConsensusSeqCommitAccessor(new KaspaWrpcJsonClient(new ScriptedChannel()));
            Assert.Equal(ConsensusSeqCommitAccessor.DefaultStableDepth, defaults.StableDepth);
            Assert.Equal(20, defaults.StableDepth);

            var methods = new List<string>();
            var cached = Accessor(methods, blocks, sink, maxRequests: 16);
            Assert.Equal(root, cached.SeqCommitmentWithinDepth(sink));
            var afterFirst = methods.FindAll(method => method == "getBlock").Count;
            var requests = cached.RequestCount;
            Assert.Equal(root, cached.SeqCommitmentWithinDepth(sink));
            Assert.Equal(afterFirst, methods.FindAll(method => method == "getBlock").Count);
            Assert.True(cached.RequestCount > requests);
        }

        [Fact]
        public void Consensus_accessor_accepts_long_forward_growth_without_chasing_every_block()
        {
            var genesis = new uint256(1);
            var sink = new uint256(2);
            var root = new uint256(20);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 100, new uint256(10), null, chain: true),
                [sink.ToString()] = Block(sink, 200, root, genesis, chain: true),
            };
            var cursor = sink;
            var tip = sink;
            for (var i = 0; i < 80; i++)
            {
                tip = new uint256(100UL + (ulong)i);
                blocks[tip.ToString()] = Block(tip, 300UL + (ulong)i, new uint256(40), cursor, chain: true);
                cursor = tip;
            }

            var reads = 0;
            var grown = Accessor(new List<string>(), blocks, () =>
            {
                var current = reads < 2 ? sink : tip;
                reads++;
                return current;
            }, maxRequests: 64);
            Assert.Equal(root, grown.SeqCommitmentWithinDepth(sink));
            Assert.True(grown.RequestCount < 64);
            Assert.True(grown.RequestCount <= 48);

            reads = 0;
            var moved = Accessor(new List<string>(), blocks, () =>
            {
                if (reads == 4)
                {
                    blocks[sink.ToString()] = Block(sink, 200, root, genesis, chain: false);
                }

                var current = reads < 2 ? sink : tip;
                reads++;
                return current;
            }, maxRequests: 64);
            moved.MaxReorgRetries = 0;
            var reorg = Assert.Throws<ConsensusSeqCommitException>(() => moved.SeqCommitmentWithinDepth(sink));
            Assert.Equal(ConsensusSeqCommitFailure.Reorg, reorg.Failure);
            Assert.Equal(sink, reorg.Block);
        }

        [Fact]
        public void Consensus_accessor_accepts_parents_by_level_and_reaches_the_engine()
        {
            var parent = new uint256(2);
            var sink = new uint256(3);
            var root = new uint256(20);
            var blocks = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [parent.ToString()] = Block(parent, 10, root, null, chain: true),
                [sink.ToString()] = Block(sink, 20, new uint256(30), parent, chain: true, parentsByLevel: true),
            };
            var accessor = Accessor(new List<string>(), blocks, sink);
            Assert.Equal(root, accessor.SeqCommitmentWithinDepth(parent));

            var missing = new uint256(9);
            var engineAccessor = Accessor(new List<string>(), blocks, sink);
            var script = new ScriptBuilder().AddData(missing.ToBytes()).ChainBlockSeqCommit().ToScript();
            var thrown = Assert.Throws<ConsensusSeqCommitException>(() => Run(script, engineAccessor));
            Assert.Equal(ConsensusSeqCommitFailure.IncompleteNodeData, thrown.Failure);
        }

        private static Dictionary<string, string> Chain(uint256 genesis, uint256 parent, uint256 sink, uint256 parentRoot)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [genesis.ToString()] = Block(genesis, 100, new uint256(10), null, chain: true),
                [parent.ToString()] = Block(parent, 200, parentRoot, genesis, chain: true),
                [sink.ToString()] = Block(sink, 300, new uint256(30), parent, chain: true),
            };
        }

        private static ConsensusSeqCommitAccessor Accessor(
            List<string> methods,
            Dictionary<string, string> blocks,
            uint256 sink,
            ulong finalityDepth = ConsensusSeqCommitAccessor.DefaultFinalityDepth,
            int maxRequests = 32,
            bool missingIsError = false)
        {
            return Accessor(methods, blocks, () => sink, finalityDepth, maxRequests, missingIsError);
        }

        private static ConsensusSeqCommitAccessor Accessor(
            List<string> methods,
            Dictionary<string, string> blocks,
            Func<uint256> sink,
            ulong finalityDepth = ConsensusSeqCommitAccessor.DefaultFinalityDepth,
            int maxRequests = 32,
            bool missingIsError = false)
        {
            return Accessor(methods, blocks, _ => sink(), finalityDepth, maxRequests, missingIsError);
        }

        private static ConsensusSeqCommitAccessor Accessor(
            List<string> methods,
            Dictionary<string, string> blocks,
            Func<string, uint256> sinkFor,
            ulong finalityDepth = ConsensusSeqCommitAccessor.DefaultFinalityDepth,
            int maxRequests = 32,
            bool missingIsError = false)
        {
            var channel = new ScriptedChannel();
            channel.Handler = request =>
            {
                using var doc = JsonDocument.Parse(request);
                var id = doc.RootElement.GetProperty("id").GetInt64();
                var method = doc.RootElement.GetProperty("method").GetString() ?? string.Empty;
                methods.Add(method);
                var tip = sinkFor(method);
                if (method == "getBlockDagInfo")
                {
                    return "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"getBlockDagInfo\",\"params\":{\"network\":\"mainnet\",\"blockCount\":1,\"headerCount\":1,\"tipHashes\":[\"" + tip + "\"],\"difficulty\":1,\"pastMedianTime\":1,\"virtualParentHashes\":[\"" + tip + "\"],\"pruningPointHash\":\"" + tip + "\",\"virtualDaaScore\":1,\"sink\":\"" + tip + "\"}}";
                }

                if (method != "getBlock")
                {
                    return "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"" + method + "\",\"error\":{\"code\":1,\"message\":\"unexpected\"}}";
                }

                var hash = doc.RootElement.GetProperty("params").GetProperty("hash").GetString() ?? string.Empty;
                Assert.False(doc.RootElement.GetProperty("params").GetProperty("includeTransactions").GetBoolean());
                if (!blocks.TryGetValue(hash, out var block))
                {
                    if (missingIsError)
                    {
                        return "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"getBlock\",\"error\":{\"code\":1,\"message\":\"missing\"}}";
                    }

                    return "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"getBlock\",\"params\":{}}";
                }

                return "{\"id\":" + id.ToString(CultureInfo.InvariantCulture) + ",\"method\":\"getBlock\",\"params\":{\"block\":" + block + "}}";
            };
            var accessor = new ConsensusSeqCommitAccessor(new KaspaWrpcJsonClient(channel), finalityDepth, maxRequests);
            accessor.StableDepth = 0;
            return accessor;
        }

        private static string Block(
            uint256 hash,
            ulong score,
            uint256? root,
            uint256? parent,
            bool? chain,
            ulong? headerScore = null,
            ulong? verboseScore = null,
            uint256? selectedParent = null,
            bool includeParents = true,
            bool parentsByLevel = false,
            uint256? headerHash = null)
        {
            var headerScoreText = (headerScore ?? score).ToString(CultureInfo.InvariantCulture);
            var verboseScoreText = (verboseScore ?? score).ToString(CultureInfo.InvariantCulture);
            var rootText = root == null ? string.Empty : root.ToString();
            var header = "{\"blueScore\":" + headerScoreText + ",\"acceptedIdMerkleRoot\":\"" + rootText + "\",\"hash\":\"" + (headerHash ?? hash) + "\"";
            if (includeParents)
            {
                if (parentsByLevel)
                {
                    header += parent == null
                        ? ",\"parentsByLevel\":[]"
                        : ",\"parentsByLevel\":[[\"" + parent + "\"]]";
                }
                else
                {
                    header += parent == null
                        ? ",\"parents\":[]"
                        : ",\"parents\":[{\"parentHashes\":[\"" + parent + "\"]}]";
                }
            }

            header += "}";
            var verbose = "{\"hash\":\"" + hash + "\",\"blueScore\":" + verboseScoreText;
            if (chain != null)
            {
                verbose += ",\"isChainBlock\":" + (chain.Value ? "true" : "false");
            }

            var selected = selectedParent ?? parent;
            if (selected != null)
            {
                verbose += ",\"selectedParentHash\":\"" + selected + "\"";
            }

            verbose += "}";
            return "{\"header\":" + header + ",\"verboseData\":" + verbose + "}";
        }

        private static TxScriptEngine Run(Script script, ISeqCommitAccessor seq)
        {
            var tx = new Transaction(
                0,
                new[] { new TxIn(new OutPoint(uint256.Zero, 0), Script.Empty, 1, 0) },
                new[] { new TxOut(0UL, new ScriptPubKey(0, Script.Empty)) },
                0,
                SubnetworkId.Native,
                0,
                Array.Empty<byte>());
            return TxScriptEngine.Run(
                Array.Empty<byte>(),
                new ScriptPubKey(0, script),
                tx,
                0,
                new UtxoEntry?[] { new UtxoEntry(0UL, new ScriptPubKey(0, script), 0, false) },
                ulong.MaxValue,
                seq);
        }

        private sealed class ScriptedChannel : IKaspaJsonChannel
        {
            public Func<string, string> Handler { get; set; } = _ => throw new InvalidOperationException("No handler.");

            public Task<string> ExchangeAsync(string requestJson, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Handler(requestJson));
            }
        }
    }
}
