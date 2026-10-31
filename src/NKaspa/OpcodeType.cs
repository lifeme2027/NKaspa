namespace NKaspa
{
    /// <summary>
    /// Kaspa txscript opcode. Kaspa counterpart of NBitcoin.OpcodeType.
    /// Values match rusty-kaspa <c>crypto/txscript/src/opcodes/mod.rs</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bytes 0x01 through 0x4b push that many following bytes. <see cref="OP_0"/> pushes empty.
    /// <see cref="OP_1"/> through <see cref="OP_16"/> and <see cref="OP_1NEGATE"/> push small integers.
    /// </para>
    /// <para>
    /// Differences from Bitcoin: <see cref="OP_CAT"/>, <see cref="OP_SUBSTR"/>, <see cref="OP_INVERT"/>,
    /// <see cref="OP_AND"/>, <see cref="OP_OR"/>, <see cref="OP_XOR"/>, <see cref="OP_MUL"/>,
    /// <see cref="OP_DIV"/>, and <see cref="OP_MOD"/> are enabled. Bitcoin disables them.
    /// <see cref="OP_LEFT"/>, <see cref="OP_RIGHT"/>, <see cref="OP_2MUL"/>, <see cref="OP_2DIV"/>,
    /// <see cref="OP_LSHIFT"/>, and <see cref="OP_RSHIFT"/> stay disabled, as in Bitcoin.
    /// </para>
    /// <para>
    /// 0xa6 is <see cref="OP_ZKPRECOMPILE"/> (Bitcoin OP_RIPEMD160).
    /// 0xa7 is <see cref="OP_BLAKE2BWITHKEY"/> (Bitcoin OP_SHA1).
    /// 0xa9 is <see cref="OP_CHECKMULTISIGECDSA"/> (Bitcoin OP_HASH160).
    /// 0xaa is <see cref="OP_BLAKE2B"/> (Bitcoin OP_HASH256).
    /// 0xab is <see cref="OP_CHECKSIGECDSA"/> (Bitcoin OP_CODESEPARATOR).
    /// 0xac <see cref="OP_CHECKSIG"/> verifies BIP-340 Schnorr, not ECDSA.
    /// 0xae <see cref="OP_CHECKMULTISIG"/> is the Schnorr multisig opcode.
    /// <see cref="OP_CHECKLOCKTIMEVERIFY"/> is 0xb0 (Bitcoin NOP1; Bitcoin CLTV is 0xb1).
    /// <see cref="OP_CHECKSEQUENCEVERIFY"/> is 0xb1 (Bitcoin CLTV; Bitcoin CSV is 0xb2).
    /// 0xb2 and above are Kaspa introspection and covenant opcodes, not Bitcoin NOP4 through NOP10.
    /// There is no witness or Taproot opcode.
    /// </para>
    /// <example>
    /// A 65-byte Schnorr signature push uses <see cref="OP_DATA_65"/>, then the signature and sighash byte.
    /// </example>
    /// </remarks>
    public enum OpcodeType : byte
    {
        /// <summary>Pushes an empty array. The same byte as <see cref="OP_FALSE"/>.</summary>
        OP_0 = 0x00,
        /// <summary>Pushes an empty array, which scripts treat as false. The same byte as <see cref="OP_0"/>.</summary>
        OP_FALSE = 0x00,
        /// <summary>Pushes the next 1 byte.</summary>
        OP_DATA_1 = 0x01,
        /// <summary>Pushes the next 2 bytes.</summary>
        OP_DATA_2 = 0x02,
        /// <summary>Pushes the next 3 bytes.</summary>
        OP_DATA_3 = 0x03,
        /// <summary>Pushes the next 4 bytes.</summary>
        OP_DATA_4 = 0x04,
        /// <summary>Pushes the next 5 bytes.</summary>
        OP_DATA_5 = 0x05,
        /// <summary>Pushes the next 6 bytes.</summary>
        OP_DATA_6 = 0x06,
        /// <summary>Pushes the next 7 bytes.</summary>
        OP_DATA_7 = 0x07,
        /// <summary>Pushes the next 8 bytes.</summary>
        OP_DATA_8 = 0x08,
        /// <summary>Pushes the next 9 bytes.</summary>
        OP_DATA_9 = 0x09,
        /// <summary>Pushes the next 10 bytes.</summary>
        OP_DATA_10 = 0x0a,
        /// <summary>Pushes the next 11 bytes.</summary>
        OP_DATA_11 = 0x0b,
        /// <summary>Pushes the next 12 bytes.</summary>
        OP_DATA_12 = 0x0c,
        /// <summary>Pushes the next 13 bytes.</summary>
        OP_DATA_13 = 0x0d,
        /// <summary>Pushes the next 14 bytes.</summary>
        OP_DATA_14 = 0x0e,
        /// <summary>Pushes the next 15 bytes.</summary>
        OP_DATA_15 = 0x0f,
        /// <summary>Pushes the next 16 bytes.</summary>
        OP_DATA_16 = 0x10,
        /// <summary>Pushes the next 17 bytes.</summary>
        OP_DATA_17 = 0x11,
        /// <summary>Pushes the next 18 bytes.</summary>
        OP_DATA_18 = 0x12,
        /// <summary>Pushes the next 19 bytes.</summary>
        OP_DATA_19 = 0x13,
        /// <summary>Pushes the next 20 bytes.</summary>
        OP_DATA_20 = 0x14,
        /// <summary>Pushes the next 21 bytes.</summary>
        OP_DATA_21 = 0x15,
        /// <summary>Pushes the next 22 bytes.</summary>
        OP_DATA_22 = 0x16,
        /// <summary>Pushes the next 23 bytes.</summary>
        OP_DATA_23 = 0x17,
        /// <summary>Pushes the next 24 bytes.</summary>
        OP_DATA_24 = 0x18,
        /// <summary>Pushes the next 25 bytes.</summary>
        OP_DATA_25 = 0x19,
        /// <summary>Pushes the next 26 bytes.</summary>
        OP_DATA_26 = 0x1a,
        /// <summary>Pushes the next 27 bytes.</summary>
        OP_DATA_27 = 0x1b,
        /// <summary>Pushes the next 28 bytes.</summary>
        OP_DATA_28 = 0x1c,
        /// <summary>Pushes the next 29 bytes.</summary>
        OP_DATA_29 = 0x1d,
        /// <summary>Pushes the next 30 bytes.</summary>
        OP_DATA_30 = 0x1e,
        /// <summary>Pushes the next 31 bytes.</summary>
        OP_DATA_31 = 0x1f,
        /// <summary>Pushes the next 32 bytes.</summary>
        OP_DATA_32 = 0x20,
        /// <summary>Pushes the next 33 bytes.</summary>
        OP_DATA_33 = 0x21,
        /// <summary>Pushes the next 34 bytes.</summary>
        OP_DATA_34 = 0x22,
        /// <summary>Pushes the next 35 bytes.</summary>
        OP_DATA_35 = 0x23,
        /// <summary>Pushes the next 36 bytes.</summary>
        OP_DATA_36 = 0x24,
        /// <summary>Pushes the next 37 bytes.</summary>
        OP_DATA_37 = 0x25,
        /// <summary>Pushes the next 38 bytes.</summary>
        OP_DATA_38 = 0x26,
        /// <summary>Pushes the next 39 bytes.</summary>
        OP_DATA_39 = 0x27,
        /// <summary>Pushes the next 40 bytes.</summary>
        OP_DATA_40 = 0x28,
        /// <summary>Pushes the next 41 bytes.</summary>
        OP_DATA_41 = 0x29,
        /// <summary>Pushes the next 42 bytes.</summary>
        OP_DATA_42 = 0x2a,
        /// <summary>Pushes the next 43 bytes.</summary>
        OP_DATA_43 = 0x2b,
        /// <summary>Pushes the next 44 bytes.</summary>
        OP_DATA_44 = 0x2c,
        /// <summary>Pushes the next 45 bytes.</summary>
        OP_DATA_45 = 0x2d,
        /// <summary>Pushes the next 46 bytes.</summary>
        OP_DATA_46 = 0x2e,
        /// <summary>Pushes the next 47 bytes.</summary>
        OP_DATA_47 = 0x2f,
        /// <summary>Pushes the next 48 bytes.</summary>
        OP_DATA_48 = 0x30,
        /// <summary>Pushes the next 49 bytes.</summary>
        OP_DATA_49 = 0x31,
        /// <summary>Pushes the next 50 bytes.</summary>
        OP_DATA_50 = 0x32,
        /// <summary>Pushes the next 51 bytes.</summary>
        OP_DATA_51 = 0x33,
        /// <summary>Pushes the next 52 bytes.</summary>
        OP_DATA_52 = 0x34,
        /// <summary>Pushes the next 53 bytes.</summary>
        OP_DATA_53 = 0x35,
        /// <summary>Pushes the next 54 bytes.</summary>
        OP_DATA_54 = 0x36,
        /// <summary>Pushes the next 55 bytes.</summary>
        OP_DATA_55 = 0x37,
        /// <summary>Pushes the next 56 bytes.</summary>
        OP_DATA_56 = 0x38,
        /// <summary>Pushes the next 57 bytes.</summary>
        OP_DATA_57 = 0x39,
        /// <summary>Pushes the next 58 bytes.</summary>
        OP_DATA_58 = 0x3a,
        /// <summary>Pushes the next 59 bytes.</summary>
        OP_DATA_59 = 0x3b,
        /// <summary>Pushes the next 60 bytes.</summary>
        OP_DATA_60 = 0x3c,
        /// <summary>Pushes the next 61 bytes.</summary>
        OP_DATA_61 = 0x3d,
        /// <summary>Pushes the next 62 bytes.</summary>
        OP_DATA_62 = 0x3e,
        /// <summary>Pushes the next 63 bytes.</summary>
        OP_DATA_63 = 0x3f,
        /// <summary>Pushes the next 64 bytes.</summary>
        OP_DATA_64 = 0x40,
        /// <summary>Pushes the next 65 bytes.</summary>
        OP_DATA_65 = 0x41,
        /// <summary>Pushes the next 66 bytes.</summary>
        OP_DATA_66 = 0x42,
        /// <summary>Pushes the next 67 bytes.</summary>
        OP_DATA_67 = 0x43,
        /// <summary>Pushes the next 68 bytes.</summary>
        OP_DATA_68 = 0x44,
        /// <summary>Pushes the next 69 bytes.</summary>
        OP_DATA_69 = 0x45,
        /// <summary>Pushes the next 70 bytes.</summary>
        OP_DATA_70 = 0x46,
        /// <summary>Pushes the next 71 bytes.</summary>
        OP_DATA_71 = 0x47,
        /// <summary>Pushes the next 72 bytes.</summary>
        OP_DATA_72 = 0x48,
        /// <summary>Pushes the next 73 bytes.</summary>
        OP_DATA_73 = 0x49,
        /// <summary>Pushes the next 74 bytes.</summary>
        OP_DATA_74 = 0x4a,
        /// <summary>Pushes the next 75 bytes.</summary>
        OP_DATA_75 = 0x4b,
        /// <summary>The next byte is the number of data bytes to push.</summary>
        OP_PUSHDATA1 = 0x4c,
        /// <summary>The next two little-endian bytes are the number of data bytes to push.</summary>
        OP_PUSHDATA2 = 0x4d,
        /// <summary>The next four little-endian bytes are the number of data bytes to push.</summary>
        OP_PUSHDATA4 = 0x4e,
        /// <summary>Pushes the small integer -1.</summary>
        OP_1NEGATE = 0x4f,
        /// <summary>Reserved opcode <c>0x50</c>. Executing it fails the script.</summary>
        OP_RESERVED = 0x50,
        /// <summary>Pushes the small integer 1. The same byte as <see cref="OP_TRUE"/>.</summary>
        OP_1 = 0x51,
        /// <summary>Pushes the small integer 1, which scripts treat as true. The same byte as <see cref="OP_1"/>.</summary>
        OP_TRUE = 0x51,
        /// <summary>Pushes the small integer 2.</summary>
        OP_2 = 0x52,
        /// <summary>Pushes the small integer 3.</summary>
        OP_3 = 0x53,
        /// <summary>Pushes the small integer 4.</summary>
        OP_4 = 0x54,
        /// <summary>Pushes the small integer 5.</summary>
        OP_5 = 0x55,
        /// <summary>Pushes the small integer 6.</summary>
        OP_6 = 0x56,
        /// <summary>Pushes the small integer 7.</summary>
        OP_7 = 0x57,
        /// <summary>Pushes the small integer 8.</summary>
        OP_8 = 0x58,
        /// <summary>Pushes the small integer 9.</summary>
        OP_9 = 0x59,
        /// <summary>Pushes the small integer 10.</summary>
        OP_10 = 0x5a,
        /// <summary>Pushes the small integer 11.</summary>
        OP_11 = 0x5b,
        /// <summary>Pushes the small integer 12.</summary>
        OP_12 = 0x5c,
        /// <summary>Pushes the small integer 13.</summary>
        OP_13 = 0x5d,
        /// <summary>Pushes the small integer 14.</summary>
        OP_14 = 0x5e,
        /// <summary>Pushes the small integer 15.</summary>
        OP_15 = 0x5f,
        /// <summary>Pushes the small integer 16.</summary>
        OP_16 = 0x60,
        /// <summary>Does nothing.</summary>
        OP_NOP = 0x61,
        /// <summary>Reserved opcode <c>0x62</c>. Executing it fails the script.</summary>
        OP_VER = 0x62,
        /// <summary>Runs the following statements when the top value is true, then pops that value.</summary>
        OP_IF = 0x63,
        /// <summary>Runs the following statements when the top value is false, then pops that value.</summary>
        OP_NOTIF = 0x64,
        /// <summary>Reserved opcode <c>0x65</c>. Executing it fails the script.</summary>
        OP_VERIF = 0x65,
        /// <summary>Reserved opcode <c>0x66</c>. Executing it fails the script.</summary>
        OP_VERNOTIF = 0x66,
        /// <summary>Switches the active branch of <see cref="OP_IF"/> or <see cref="OP_NOTIF"/>.</summary>
        OP_ELSE = 0x67,
        /// <summary>Ends the active <see cref="OP_IF"/> or <see cref="OP_NOTIF"/>.</summary>
        OP_ENDIF = 0x68,
        /// <summary>Fails the script when the top value is false, and pops it when the value is true.</summary>
        OP_VERIFY = 0x69,
        /// <summary>Fails the script immediately.</summary>
        OP_RETURN = 0x6a,
        /// <summary>Moves the top main-stack item onto the alt stack.</summary>
        OP_TOALTSTACK = 0x6b,
        /// <summary>Moves the top alt-stack item onto the main stack.</summary>
        OP_FROMALTSTACK = 0x6c,
        /// <summary>Pops the top two stack items.</summary>
        OP_2DROP = 0x6d,
        /// <summary>Duplicates the top pair of stack items.</summary>
        OP_2DUP = 0x6e,
        /// <summary>Duplicates the top three stack items.</summary>
        OP_3DUP = 0x6f,
        /// <summary>Copies the pair under the top pair onto the top of the stack.</summary>
        OP_2OVER = 0x70,
        /// <summary>Rotates the top three pairs of stack items.</summary>
        OP_2ROT = 0x71,
        /// <summary>Swaps the top two pairs of stack items.</summary>
        OP_2SWAP = 0x72,
        /// <summary>Duplicates the top item when that item is true.</summary>
        OP_IFDUP = 0x73,
        /// <summary>Pushes the number of items on the main stack.</summary>
        OP_DEPTH = 0x74,
        /// <summary>Pops the top stack item.</summary>
        OP_DROP = 0x75,
        /// <summary>Duplicates the top stack item.</summary>
        OP_DUP = 0x76,
        /// <summary>Removes the second stack item.</summary>
        OP_NIP = 0x77,
        /// <summary>Copies the second stack item to the top.</summary>
        OP_OVER = 0x78,
        /// <summary>Copies the stack item selected by the index on top.</summary>
        OP_PICK = 0x79,
        /// <summary>Moves the stack item selected by the index on top to the top.</summary>
        OP_ROLL = 0x7a,
        /// <summary>Rotates the top three stack items.</summary>
        OP_ROT = 0x7b,
        /// <summary>Swaps the top two stack items.</summary>
        OP_SWAP = 0x7c,
        /// <summary>Copies the top item underneath the second item.</summary>
        OP_TUCK = 0x7d,
        /// <summary>Concatenates the top two byte strings. Enabled in Kaspa.</summary>
        OP_CAT = 0x7e,
        /// <summary>Replaces the top byte string with a substring. Enabled in Kaspa.</summary>
        OP_SUBSTR = 0x7f,
        /// <summary>Disabled. Executing it fails the script.</summary>
        OP_LEFT = 0x80,
        /// <summary>Disabled. Executing it fails the script.</summary>
        OP_RIGHT = 0x81,
        /// <summary>Pushes the length of the top item and leaves that item in place.</summary>
        OP_SIZE = 0x82,
        /// <summary>Bitwise NOT of the top item. Enabled in Kaspa.</summary>
        OP_INVERT = 0x83,
        /// <summary>Bitwise AND of the top two items. Enabled in Kaspa.</summary>
        OP_AND = 0x84,
        /// <summary>Bitwise OR of the top two items. Enabled in Kaspa.</summary>
        OP_OR = 0x85,
        /// <summary>Bitwise XOR of the top two items. Enabled in Kaspa.</summary>
        OP_XOR = 0x86,
        /// <summary>Pushes true when the top two items are equal.</summary>
        OP_EQUAL = 0x87,
        /// <summary><see cref="OP_EQUAL"/> followed by <see cref="OP_VERIFY"/>.</summary>
        OP_EQUALVERIFY = 0x88,
        /// <summary>Reserved opcode <c>0x89</c>. Executing it fails the script.</summary>
        OP_RESERVED1 = 0x89,
        /// <summary>Reserved opcode <c>0x8a</c>. Executing it fails the script.</summary>
        OP_RESERVED2 = 0x8a,
        /// <summary>Adds one to the top script number.</summary>
        OP_1ADD = 0x8b,
        /// <summary>Subtracts one from the top script number.</summary>
        OP_1SUB = 0x8c,
        /// <summary>Disabled. Executing it fails the script.</summary>
        OP_2MUL = 0x8d,
        /// <summary>Disabled. Executing it fails the script.</summary>
        OP_2DIV = 0x8e,
        /// <summary>Negates the top script number.</summary>
        OP_NEGATE = 0x8f,
        /// <summary>Replaces the top script number with its absolute value.</summary>
        OP_ABS = 0x90,
        /// <summary>Pushes true when the top script number is zero.</summary>
        OP_NOT = 0x91,
        /// <summary>Pushes true when the top script number is not zero.</summary>
        OP_0NOTEQUAL = 0x92,
        /// <summary>Adds the top two script numbers.</summary>
        OP_ADD = 0x93,
        /// <summary>Subtracts the top script number from the second.</summary>
        OP_SUB = 0x94,
        /// <summary>Multiplies the top two script numbers. Enabled in Kaspa.</summary>
        OP_MUL = 0x95,
        /// <summary>Divides the second script number by the top number. Enabled in Kaspa.</summary>
        OP_DIV = 0x96,
        /// <summary>Pushes the remainder of the second script number divided by the top number. Enabled in Kaspa.</summary>
        OP_MOD = 0x97,
        /// <summary>Disabled. Executing it fails the script.</summary>
        OP_LSHIFT = 0x98,
        /// <summary>Disabled. Executing it fails the script.</summary>
        OP_RSHIFT = 0x99,
        /// <summary>Pushes true when both script numbers are non-zero.</summary>
        OP_BOOLAND = 0x9a,
        /// <summary>Pushes true when either script number is non-zero.</summary>
        OP_BOOLOR = 0x9b,
        /// <summary>Pushes true when the top two script numbers are equal.</summary>
        OP_NUMEQUAL = 0x9c,
        /// <summary><see cref="OP_NUMEQUAL"/> followed by <see cref="OP_VERIFY"/>.</summary>
        OP_NUMEQUALVERIFY = 0x9d,
        /// <summary>Pushes true when the top two script numbers differ.</summary>
        OP_NUMNOTEQUAL = 0x9e,
        /// <summary>Pushes true when the second script number is less than the top number.</summary>
        OP_LESSTHAN = 0x9f,
        /// <summary>Pushes true when the second script number is greater than the top number.</summary>
        OP_GREATERTHAN = 0xa0,
        /// <summary>Pushes true when the second script number is less than or equal to the top number.</summary>
        OP_LESSTHANOREQUAL = 0xa1,
        /// <summary>Pushes true when the second script number is greater than or equal to the top number.</summary>
        OP_GREATERTHANOREQUAL = 0xa2,
        /// <summary>Pushes the smaller of the top two script numbers.</summary>
        OP_MIN = 0xa3,
        /// <summary>Pushes the larger of the top two script numbers.</summary>
        OP_MAX = 0xa4,
        /// <summary>Pushes true when the third number is at least the second and strictly less than the top.</summary>
        OP_WITHIN = 0xa5,
        /// <summary>Runs the finalized tag, cost, and layout checks for a zero-knowledge precompile. The BN254 pairing and the RISC0 STARK stay unimplemented and do not push true. Bitcoin uses this byte for OP_RIPEMD160.</summary>
        OP_ZKPRECOMPILE = 0xa6,
        /// <summary>Keyed BLAKE2b of the stack message. Bitcoin uses this byte for OP_SHA1.</summary>
        OP_BLAKE2BWITHKEY = 0xa7,
        /// <summary>Pushes SHA-256 of the top item. Kaspa addresses and transaction ids do not use this hash.</summary>
        OP_SHA256 = 0xa8,
        /// <summary>Checks an ECDSA m-of-n signature. Bitcoin uses this byte for OP_HASH160.</summary>
        OP_CHECKMULTISIGECDSA = 0xa9,
        /// <summary>Pushes unkeyed BLAKE2b-256 of the top item. Bitcoin uses this byte for OP_HASH256.</summary>
        OP_BLAKE2B = 0xaa,
        /// <summary>Checks one compact ECDSA signature. Bitcoin uses this byte for OP_CODESEPARATOR.</summary>
        OP_CHECKSIGECDSA = 0xab,
        /// <summary>Checks one BIP-340 Schnorr signature. This is not Bitcoin ECDSA.</summary>
        OP_CHECKSIG = 0xac,
        /// <summary><see cref="OP_CHECKSIG"/> followed by <see cref="OP_VERIFY"/>.</summary>
        OP_CHECKSIGVERIFY = 0xad,
        /// <summary>Checks a Schnorr m-of-n signature.</summary>
        OP_CHECKMULTISIG = 0xae,
        /// <summary><see cref="OP_CHECKMULTISIG"/> followed by <see cref="OP_VERIFY"/>.</summary>
        OP_CHECKMULTISIGVERIFY = 0xaf,
        /// <summary>Fails unless the transaction lock time is at least the top number. The byte is <c>0xb0</c>.</summary>
        OP_CHECKLOCKTIMEVERIFY = 0xb0,
        /// <summary>Fails unless the input sequence meets the relative lock on top. The byte is <c>0xb1</c>.</summary>
        OP_CHECKSEQUENCEVERIFY = 0xb1,
        /// <summary>Pushes the transaction version. Legal only while verifying an input.</summary>
        OP_TXVERSION = 0xb2,
        /// <summary>Pushes the number of transaction inputs.</summary>
        OP_TXINPUTCOUNT = 0xb3,
        /// <summary>Pushes the number of transaction outputs.</summary>
        OP_TXOUTPUTCOUNT = 0xb4,
        /// <summary>Pushes the transaction lock time.</summary>
        OP_TXLOCKTIME = 0xb5,
        /// <summary>Pushes the transaction subnetwork id.</summary>
        OP_TXSUBNETID = 0xb6,
        /// <summary>Pushes the transaction gas field.</summary>
        OP_TXGAS = 0xb7,
        /// <summary>Pushes a byte range taken from the transaction payload.</summary>
        OP_TXPAYLOADSUBSTR = 0xb8,
        /// <summary>Pushes the index of the input currently being verified.</summary>
        OP_TXINPUTINDEX = 0xb9,
        /// <summary>Pushes the outpoint transaction id of a selected input.</summary>
        OP_OUTPOINTTXID = 0xba,
        /// <summary>Pushes the outpoint index of a selected input.</summary>
        OP_OUTPOINTINDEX = 0xbb,
        /// <summary>Pushes a byte range taken from a selected input signature script.</summary>
        OP_TXINPUTSCRIPTSIGSUBSTR = 0xbc,
        /// <summary>Pushes the sequence of a selected input.</summary>
        OP_TXINPUTSEQ = 0xbd,
        /// <summary>Pushes the sompi amount of a selected input.</summary>
        OP_TXINPUTAMOUNT = 0xbe,
        /// <summary>Pushes the script public key of a selected input.</summary>
        OP_TXINPUTSPK = 0xbf,
        /// <summary>Pushes the DAA score recorded on a selected input.</summary>
        OP_TXINPUTDAASCORE = 0xc0,
        /// <summary>Pushes whether a selected input was created by a coinbase.</summary>
        OP_TXINPUTISCOINBASE = 0xc1,
        /// <summary>Pushes the sompi amount of a selected output.</summary>
        OP_TXOUTPUTAMOUNT = 0xc2,
        /// <summary>Pushes the script public key of a selected output.</summary>
        OP_TXOUTPUTSPK = 0xc3,
        /// <summary>Pushes the length of the transaction payload.</summary>
        OP_TXPAYLOADLEN = 0xc4,
        /// <summary>Pushes the length of a selected input script public key.</summary>
        OP_TXINPUTSPKLEN = 0xc5,
        /// <summary>Pushes a byte range taken from a selected input script public key.</summary>
        OP_TXINPUTSPKSUBSTR = 0xc6,
        /// <summary>Pushes the length of a selected output script public key.</summary>
        OP_TXOUTPUTSPKLEN = 0xc7,
        /// <summary>Pushes a byte range taken from a selected output script public key.</summary>
        OP_TXOUTPUTSPKSUBSTR = 0xc8,
        /// <summary>Pushes the length of a selected input signature script.</summary>
        OP_TXINPUTSCRIPTSIGLEN = 0xc9,
        /// <summary>Unassigned opcode <c>0xca</c>. Executing it fails the script.</summary>
        OP_UNKNOWN202 = 0xca,
        /// <summary>Pushes how many outputs are authorized by the input selected by the index on top.</summary>
        OP_AUTHOUTPUTCOUNT = 0xcb,
        /// <summary>Pops an input index and an authorizing-output slot, then pushes that output's transaction index.</summary>
        OP_AUTHOUTPUTIDX = 0xcc,
        /// <summary>Encodes a script number as a fixed-width byte string. The width on top must be at most 8.</summary>
        OP_NUM2BIN = 0xcd,
        /// <summary>Decodes the top byte string as a script number and pushes that number.</summary>
        OP_BIN2NUM = 0xce,
        /// <summary>Pushes the covenant id of the input selected by the index on top. An input with no covenant id pushes 32 zero bytes.</summary>
        OP_INPUTCOVENANTID = 0xcf,
        /// <summary>Pops a covenant id and pushes how many inputs carry that id.</summary>
        OP_COVINPUTCOUNT = 0xd0,
        /// <summary>Pops a slot and a covenant id, then pushes the transaction-input index of that covenant input.</summary>
        OP_COVINPUTIDX = 0xd1,
        /// <summary>Pops a covenant id and pushes how many outputs carry that id.</summary>
        OP_COVOUTPUTCOUNT = 0xd2,
        /// <summary>Pops a slot and a covenant id, then pushes the transaction-output index of that covenant output.</summary>
        OP_COVOUTPUTIDX = 0xd3,
        /// <summary>Pushes a KIP-21 sequence commitment. Requires an <see cref="ISeqCommitAccessor"/>. Without one, executing it is illegal.</summary>
        OP_CHAINBLOCKSEQCOMMIT = 0xd4,
        /// <summary>Pushes the covenant id of a selected output.</summary>
        OP_OUTPUTCOVENANTID = 0xd5,
        /// <summary>Pushes the authorizing input index stored on a selected output covenant.</summary>
        OP_OUTPUTAUTHORIZINGINPUT = 0xd6,
        /// <summary>Checks a BIP-340 Schnorr signature over a message taken from the stack.</summary>
        OP_CHECKSIGFROMSTACK = 0xd7,
        /// <summary>Checks a compact ECDSA signature over a message taken from the stack.</summary>
        OP_CHECKSIGFROMSTACKECDSA = 0xd8,
        /// <summary>Pushes unkeyed BLAKE3-256 of the top item.</summary>
        OP_BLAKE3 = 0xd9,
        /// <summary>Pushes keyed BLAKE3 of the stack message.</summary>
        OP_BLAKE3WITHKEY = 0xda,
        /// <summary>Unassigned opcode <c>0xdb</c>. Executing it fails the script.</summary>
        OP_UNKNOWN219 = 0xdb,
        /// <summary>Unassigned opcode <c>0xdc</c>. Executing it fails the script.</summary>
        OP_UNKNOWN220 = 0xdc,
        /// <summary>Unassigned opcode <c>0xdd</c>. Executing it fails the script.</summary>
        OP_UNKNOWN221 = 0xdd,
        /// <summary>Unassigned opcode <c>0xde</c>. Executing it fails the script.</summary>
        OP_UNKNOWN222 = 0xde,
        /// <summary>Unassigned opcode <c>0xdf</c>. Executing it fails the script.</summary>
        OP_UNKNOWN223 = 0xdf,
        /// <summary>Unassigned opcode <c>0xe0</c>. Executing it fails the script.</summary>
        OP_UNKNOWN224 = 0xe0,
        /// <summary>Unassigned opcode <c>0xe1</c>. Executing it fails the script.</summary>
        OP_UNKNOWN225 = 0xe1,
        /// <summary>Unassigned opcode <c>0xe2</c>. Executing it fails the script.</summary>
        OP_UNKNOWN226 = 0xe2,
        /// <summary>Unassigned opcode <c>0xe3</c>. Executing it fails the script.</summary>
        OP_UNKNOWN227 = 0xe3,
        /// <summary>Unassigned opcode <c>0xe4</c>. Executing it fails the script.</summary>
        OP_UNKNOWN228 = 0xe4,
        /// <summary>Unassigned opcode <c>0xe5</c>. Executing it fails the script.</summary>
        OP_UNKNOWN229 = 0xe5,
        /// <summary>Unassigned opcode <c>0xe6</c>. Executing it fails the script.</summary>
        OP_UNKNOWN230 = 0xe6,
        /// <summary>Unassigned opcode <c>0xe7</c>. Executing it fails the script.</summary>
        OP_UNKNOWN231 = 0xe7,
        /// <summary>Unassigned opcode <c>0xe8</c>. Executing it fails the script.</summary>
        OP_UNKNOWN232 = 0xe8,
        /// <summary>Unassigned opcode <c>0xe9</c>. Executing it fails the script.</summary>
        OP_UNKNOWN233 = 0xe9,
        /// <summary>Unassigned opcode <c>0xea</c>. Executing it fails the script.</summary>
        OP_UNKNOWN234 = 0xea,
        /// <summary>Unassigned opcode <c>0xeb</c>. Executing it fails the script.</summary>
        OP_UNKNOWN235 = 0xeb,
        /// <summary>Unassigned opcode <c>0xec</c>. Executing it fails the script.</summary>
        OP_UNKNOWN236 = 0xec,
        /// <summary>Unassigned opcode <c>0xed</c>. Executing it fails the script.</summary>
        OP_UNKNOWN237 = 0xed,
        /// <summary>Unassigned opcode <c>0xee</c>. Executing it fails the script.</summary>
        OP_UNKNOWN238 = 0xee,
        /// <summary>Unassigned opcode <c>0xef</c>. Executing it fails the script.</summary>
        OP_UNKNOWN239 = 0xef,
        /// <summary>Unassigned opcode <c>0xf0</c>. Executing it fails the script.</summary>
        OP_UNKNOWN240 = 0xf0,
        /// <summary>Unassigned opcode <c>0xf1</c>. Executing it fails the script.</summary>
        OP_UNKNOWN241 = 0xf1,
        /// <summary>Unassigned opcode <c>0xf2</c>. Executing it fails the script.</summary>
        OP_UNKNOWN242 = 0xf2,
        /// <summary>Unassigned opcode <c>0xf3</c>. Executing it fails the script.</summary>
        OP_UNKNOWN243 = 0xf3,
        /// <summary>Unassigned opcode <c>0xf4</c>. Executing it fails the script.</summary>
        OP_UNKNOWN244 = 0xf4,
        /// <summary>Unassigned opcode <c>0xf5</c>. Executing it fails the script.</summary>
        OP_UNKNOWN245 = 0xf5,
        /// <summary>Unassigned opcode <c>0xf6</c>. Executing it fails the script.</summary>
        OP_UNKNOWN246 = 0xf6,
        /// <summary>Unassigned opcode <c>0xf7</c>. Executing it fails the script.</summary>
        OP_UNKNOWN247 = 0xf7,
        /// <summary>Unassigned opcode <c>0xf8</c>. Executing it fails the script.</summary>
        OP_UNKNOWN248 = 0xf8,
        /// <summary>Unassigned opcode <c>0xf9</c>. Executing it fails the script.</summary>
        OP_UNKNOWN249 = 0xf9,
        /// <summary>Script-template marker <c>0xfa</c>, not an executable opcode. Executing it fails the script.</summary>
        OP_SMALLINTEGER = 0xfa,
        /// <summary>Script-template marker <c>0xfb</c>, not an executable opcode. Executing it fails the script.</summary>
        OP_PUBKEYS = 0xfb,
        /// <summary>Unassigned opcode <c>0xfc</c>. Executing it fails the script.</summary>
        OP_UNKNOWN252 = 0xfc,
        /// <summary>Script-template marker <c>0xfd</c>, not an executable opcode. Kaspa addresses do not use HASH160. Executing it fails the script.</summary>
        OP_PUBKEYHASH = 0xfd,
        /// <summary>Script-template marker <c>0xfe</c>, not an executable opcode. Executing it fails the script.</summary>
        OP_PUBKEY = 0xfe,
        /// <summary>Invalid opcode <c>0xff</c>. Executing it fails the script.</summary>
        OP_INVALIDOPCODE = 0xff,
    }
}
