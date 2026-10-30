using System.Globalization;

namespace NKaspa
{
    /// <summary>
    /// Finalized <c>OP_ZKPRECOMPILE</c> rules from rusty-kaspa <c>crypto/txscript/src/zk_precompiles</c>
    /// at <c>4edcbbccb449f84832afb1ca157904b35f37d006</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tag byte, the script-unit price, the stack order, field length, the BN254 scalar range,
    /// Groth16 verifying-key arity, gamma metering, trailing bytes, and the RISC0 Poseidon2-only
    /// rejection are consensus checks. They run here.
    /// </para>
    /// <para>
    /// The BN254 Groth16 pairing and the RISC0 succinct STARK are not implemented.
    /// After the structural checks succeed the opcode returns <see cref="ScriptErrorCode.Unimplemented"/>
    /// and does not push true. A compressed point that has the right length but is not on the curve
    /// is also reported as unimplemented: rusty-kaspa rejects that point during ark deserialization,
    /// before the gamma charge. This engine cannot make that check, so a well-sized invalid point
    /// can be charged and then fail closed.
    /// </para>
    /// </remarks>
    public static class ZkPrecompile
    {
        /// <summary>Groth16 tag. Rusty-kaspa <c>ZkTag::Groth16</c>.</summary>
        public const byte Groth16Tag = 0x20;

        /// <summary>RISC0 succinct tag. Rusty-kaspa <c>ZkTag::R0Succinct</c>.</summary>
        public const byte R0SuccinctTag = 0x21;

        /// <summary>Script units for one Groth16 tag. <c>Gram(1000 * 140)</c> times 100.</summary>
        public const ulong Groth16ScriptUnits = 14_000_000;

        /// <summary>Script units for one RISC0 succinct tag. <c>Gram(1000 * 250)</c> times 100. This is also the unknown-tag upper bound.</summary>
        public const ulong R0SuccinctScriptUnits = 25_000_000;

        /// <summary>Extra script units per <c>gamma_abc_g1</c> element, charged only after the public-input arity matches.</summary>
        public const ulong GammaAbcElementScriptUnits = 250_000;

        /// <summary>Uncompressed BN254 scalar length.</summary>
        public const int FrBytes = 32;

        /// <summary>Compressed Groth16 verifying-key header: G1, three G2 points, and a little-endian <c>u64</c> count. 32 + 64 + 64 + 64 + 8.</summary>
        public const int Groth16VerifyingKeyHeaderBytes = 232;

        /// <summary>Compressed Groth16 proof: G1 + G2 + G1.</summary>
        public const int Groth16ProofBytes = 128;

        /// <summary>Poseidon2 control-root Merkle depth. Blake2b and SHA256 are rejected before this limit is used.</summary>
        public const int Poseidon2ControlMerkleDepth = 8;

        private const int DigestBytes = 32;

        // BN254 scalar modulus, little-endian limbs, low limb first.
        // 0x30644e72e131a029b85045b68181585d2833e84879a9709143e1f593f0000001
        private static readonly ulong[] FrModulus =
        {
            0x43e1f593f0000001UL,
            0x2833e84879a97091UL,
            0xb85045b68181585dUL,
            0x30644e72e131a029UL,
        };

        /// <summary>
        /// Script units charged for <paramref name="tag"/> during execution.
        /// An unknown tag uses <see cref="R0SuccinctScriptUnits"/>, which is the mass upper bound, not an execution price.
        /// Execution rejects an unknown tag before charging.
        /// </summary>
        public static ulong Cost(byte tag)
        {
            if (tag == Groth16Tag)
            {
                return Groth16ScriptUnits;
            }

            if (tag == R0SuccinctTag)
            {
                return R0SuccinctScriptUnits;
            }

            return R0SuccinctScriptUnits;
        }

        internal static ScriptError? Execute(ScriptStack stack, RuntimeResourceMeter meter)
        {
            var popped = stack.Pop(out var tagBytes);
            if (popped != null)
            {
                return popped;
            }

            if (tagBytes.Length == 0)
            {
                return ScriptError.ZkIntegrity("Tag byte is missing");
            }

            if (tagBytes.Length != 1)
            {
                return ScriptError.ZkIntegrity("Tag byte length " + tagBytes.Length.ToString(CultureInfo.InvariantCulture) + " is invalid");
            }

            var tag = tagBytes[0];
            if (tag != Groth16Tag && tag != R0SuccinctTag)
            {
                return ScriptError.ZkIntegrity("Unknown tag: " + tag.ToString(CultureInfo.InvariantCulture));
            }

            var charged = meter.Consume(Cost(tag));
            if (charged != null)
            {
                return charged;
            }

            if (tag == Groth16Tag)
            {
                return VerifyGroth16(stack, meter);
            }

            return VerifyR0(stack);
        }

        private static ScriptError? VerifyGroth16(ScriptStack stack, RuntimeResourceMeter meter)
        {
            var error = PopOne(stack, out var verifyingKey);
            if (error != null)
            {
                return WrapGroth(error);
            }

            error = PopOne(stack, out var proof);
            if (error != null)
            {
                return WrapGroth(error);
            }

            error = PopOne(stack, out var countBytes);
            if (error != null)
            {
                return WrapGroth(error);
            }

            error = ScriptNum.TryDeserializeI32(countBytes, out var count);
            if (error != null)
            {
                return WrapGroth(error);
            }

            if (count < 0)
            {
                return ScriptError.ZkIntegrity("Groth16 error: Kaspa txscript error: " + count.ToString(CultureInfo.InvariantCulture) + " cannot be used as an array index");
            }

            for (var i = 0; i < count; i++)
            {
                error = PopOne(stack, out var field);
                if (error != null)
                {
                    return WrapGroth(error);
                }

                if (field.Length != FrBytes)
                {
                    return ScriptError.ZkIntegrity(
                        "Groth16 error: Kaspa txscript error: ZK Integrity: Invalid Fr length: expected 32 bytes, got "
                        + field.Length.ToString(CultureInfo.InvariantCulture));
                }

                if (!FrInRange(field))
                {
                    return ScriptError.ZkIntegrity(
                        "Groth16 error: Kaspa txscript error: ZK Integrity: ARK serialization error: invalid data: integer is larger than the field modulus");
                }
            }

            if (verifyingKey.Length < Groth16VerifyingKeyHeaderBytes)
            {
                return ScriptError.ZkIntegrity("Groth16 error: Groth16 verifying key bytes are too short to read gamma_abc_g1 length");
            }

            var gammaCount = ReadU64Le(verifyingKey, 224);
            if (gammaCount == 0)
            {
                return ScriptError.ZkIntegrity("Groth16 error: Groth16 verifying key has empty gamma_abc_g1");
            }

            if ((ulong)count + 1UL != gammaCount)
            {
                return ScriptError.ZkIntegrity("Groth16 error: ARK R1CS error: arity mismatch");
            }

            var gammaCost = gammaCount > ulong.MaxValue / GammaAbcElementScriptUnits
                ? ulong.MaxValue
                : gammaCount * GammaAbcElementScriptUnits;
            error = meter.Consume(gammaCost);
            if (error != null)
            {
                return ScriptError.ZkIntegrity(
                    "Groth16 error: Kaspa txscript error: script units exceeded the amount committed in the input: used="
                    + DescribeExceeded(error));
            }

            ulong gammaBytes;
            if (gammaCount > (ulong)(int.MaxValue - Groth16VerifyingKeyHeaderBytes) / 32UL)
            {
                gammaBytes = ulong.MaxValue;
            }
            else
            {
                gammaBytes = Groth16VerifyingKeyHeaderBytes + (gammaCount * 32UL);
            }

            if ((ulong)verifyingKey.Length < gammaBytes)
            {
                return ScriptError.ZkIntegrity("Groth16 error: ARK serialization error: verifying key ended inside gamma_abc_g1");
            }

            if ((ulong)verifyingKey.Length > gammaBytes)
            {
                return ScriptError.ZkIntegrity("Groth16 error: Groth16 verifying key has trailing bytes");
            }

            if (proof.Length < Groth16ProofBytes)
            {
                return ScriptError.ZkIntegrity("Groth16 error: ARK serialization error: proof is shorter than the compressed Groth16 proof");
            }

            if (proof.Length > Groth16ProofBytes)
            {
                return ScriptError.ZkIntegrity("Groth16 error: Groth16 proof has trailing bytes");
            }

            return ScriptError.Unimplemented("OP_ZKPRECOMPILE Groth16 pairing");
        }

        private static ScriptError? VerifyR0(ScriptStack stack)
        {
            var error = stack.PopRaw(8, out var items);
            if (error != null)
            {
                return ScriptError.ZkIntegrity("R0 error: Txscript error: " + DescribeStack(error));
            }

            var claim = items[0];
            var controlIndex = items[1];
            var controlDigests = items[2];
            var seal = items[3];
            var journal = items[4];
            var imageId = items[5];
            var controlId = items[6];
            var hashFn = items[7];

            if (controlId.Length != DigestBytes)
            {
                return R0("Invalid digest length: " + controlId.Length.ToString(CultureInfo.InvariantCulture));
            }

            if ((seal.Length & 3) != 0)
            {
                return R0("Invalid seal length: " + seal.Length.ToString(CultureInfo.InvariantCulture));
            }

            if (claim.Length != DigestBytes)
            {
                return R0("Invalid digest length: " + claim.Length.ToString(CultureInfo.InvariantCulture));
            }

            if (hashFn.Length != 1)
            {
                return R0("Invalid hash function encoding length: " + hashFn.Length.ToString(CultureInfo.InvariantCulture));
            }

            string hashName;
            if (hashFn[0] == 0)
            {
                hashName = "Blake2b";
            }
            else if (hashFn[0] == 1)
            {
                hashName = "Poseidon2";
            }
            else if (hashFn[0] == 2)
            {
                hashName = "Sha256";
            }
            else
            {
                return R0("Invalid hash function id: " + hashFn[0].ToString(CultureInfo.InvariantCulture));
            }

            if (hashFn[0] != 1)
            {
                return R0("Unsupported hash function: " + hashName);
            }

            if (controlIndex.Length != 4)
            {
                return R0("Invalid merkle index length: " + controlIndex.Length.ToString(CultureInfo.InvariantCulture));
            }

            if ((controlDigests.Length % DigestBytes) != 0)
            {
                return R0("Invalid digest list length: " + controlDigests.Length.ToString(CultureInfo.InvariantCulture));
            }

            var digestCount = controlDigests.Length / DigestBytes;
            if (digestCount > Poseidon2ControlMerkleDepth)
            {
                return R0(
                    "Control inclusion proof length "
                    + digestCount.ToString(CultureInfo.InvariantCulture)
                    + " exceeds maximum "
                    + Poseidon2ControlMerkleDepth.ToString(CultureInfo.InvariantCulture));
            }

            if (imageId.Length != DigestBytes)
            {
                return R0("Invalid digest length: " + imageId.Length.ToString(CultureInfo.InvariantCulture));
            }

            if (journal.Length != DigestBytes)
            {
                return R0("Invalid digest length: " + journal.Length.ToString(CultureInfo.InvariantCulture));
            }

            return ScriptError.Unimplemented("OP_ZKPRECOMPILE RISC0 succinct receipt");
        }

        private static ScriptError R0(string message)
        {
            return ScriptError.ZkIntegrity("R0 error: " + message);
        }

        private static ScriptError WrapGroth(ScriptError inner)
        {
            var detail = inner.Code == ScriptErrorCode.InvalidStackOperation
                ? DescribeStack(inner)
                : inner.Message;
            return ScriptError.ZkIntegrity("Groth16 error: Kaspa txscript error: " + detail);
        }

        private static string DescribeStack(ScriptError error)
        {
            return error.Message.Replace("stack operation needed ", "opcode requires at least ").Replace(" available ", " but stack has only ");
        }

        private static string DescribeExceeded(ScriptError error)
        {
            var message = error.Message;
            var usedAt = message.IndexOf("used ", StringComparison.Ordinal);
            var limitAt = message.IndexOf(" limit ", StringComparison.Ordinal);
            if (usedAt < 0 || limitAt < 0)
            {
                return message;
            }

            var used = message.Substring(usedAt + 5, limitAt - (usedAt + 5));
            var limit = message.Substring(limitAt + 7);
            return used + ", limit=" + limit;
        }

        private static ScriptError? PopOne(ScriptStack stack, out byte[] item)
        {
            var error = stack.PopRaw(1, out var items);
            item = error == null ? items[0] : Array.Empty<byte>();
            return error;
        }

        private static bool FrInRange(byte[] value)
        {
            for (var limb = 3; limb >= 0; limb--)
            {
                var word = ReadU64Le(value, limb * 8);
                var modulus = FrModulus[limb];
                if (word < modulus)
                {
                    return true;
                }

                if (word > modulus)
                {
                    return false;
                }
            }

            return false;
        }

        private static ulong ReadU64Le(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (var i = 0; i < 8; i++)
            {
                value |= (ulong)bytes[offset + i] << (8 * i);
            }

            return value;
        }
    }
}
