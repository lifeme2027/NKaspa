using NKaspa;
using NKaspa.Crypto;
using Xunit;

namespace NKaspa.Tests
{
    /// <summary>
    /// Official rusty-kaspa sign_message vectors.
    /// File: wallet/core/src/message.rs, tests test_sign_and_verify_test_case_0 through _3.
    /// Commit 8fe466311ba179e74fc8a52f13b36140bb6b9a4e (last commit that changed that file).
    /// https://github.com/kaspanet/rusty-kaspa/blob/8fe466311ba179e74fc8a52f13b36140bb6b9a4e/wallet/core/src/message.rs
    /// The hash key is PersonalMessageSigningHash in crypto/hashes/src/hashers.rs at the same commit.
    /// </summary>
    public class PersonalMessageTests
    {
        private const string SecretA = "0000000000000000000000000000000000000000000000000000000000000003";
        private const string SecretB = "B7E151628AED2A6ABF7158809CF4F3C762E7160F38B4DA56A784D9045190CFEF";
        private const string PubA = "F9308A019258C31049344F85F89D5229B531C845836F99B08601F113BCE036F9";
        private const string PubB = "DFF1D77F2A671C5F36183726DB2341BE58FEAE1DA2DECED843240F7B502BA659";
        private const string LongMessage =
            "Lorem ipsum dolor sit amet. Aut omnis amet id voluptatem eligendi sit accusantium dolorem 33 corrupti necessitatibus hic consequatur quod et maiores alias non molestias suscipit? Est voluptatem magni qui odit eius est eveniet cupiditate id eius quae aut molestiae nihil eum excepturi voluptatem qui nisi architecto?\n\n"
            + "Et aliquid ipsa ut quas enim et dolorem deleniti ut eius dicta non praesentium neque est velit numquam. Ut consectetur amet ut error veniam et officia laudantium ea velit nesciunt est explicabo laudantium sit totam aperiam.\n\n"
            + "Ut omnis magnam et accusamus earum rem impedit provident eum commodi repellat qui dolores quis et voluptate labore et adipisci deleniti. Est nostrum explicabo aut quibusdam labore et molestiae voluptate. Qui omnis nostrum At libero deleniti et quod quia.";

        [Fact]
        public void Official_vectors_match_sign_message()
        {
            AssertVector("Hello Kaspa!", SecretA, 0, PubA, "40B9BB2BE0AE02607279EDA64015A8D86E3763279170340B8243F7CE5344D77AFF1191598BAF2FD26149CAC3B4B12C2C433261C00834DB6098CB172AA48EF522");
            AssertVector("Hello Kaspa!", SecretB, 1, PubB, "EB9E8A3C547EB91B6A7592644F328F0648BDD21ABA3CD44787D429D4D790AA8B962745691F3B472ED8D65F3B770ECB4F777BD17B1D309100919B53E0E206B4C6");
            AssertVector("こんにちは世界", SecretB, 1, PubB, "810653D5F80206DB519672362ADD6C98DAD378844E5BA4D89A22C9F0C7092E8CECBA734FFF7922B656B4BE3F4B1F098899C95CB5C1023DCE3519208AFAFB59BC");
            AssertVector(LongMessage, SecretB, 1, PubB, "40CBBD3938867B10076BB14835557C062F5BF6A4682995FC8B0A1CD2ED986EEDAAA00CFE04F6C9E5A9546B860732E5B903CC82780228647D5375BEC3D2A4983A");
        }

        [Fact]
        public void Address_verification_requires_the_schnorr_payload()
        {
            var aux = new byte[32];
            using var key = Key.Parse(SecretA);
            using var other = Key.Parse(SecretB);
            var signature = PersonalMessage.SignMessage(key, "Hello Kaspa!", aux);
            var parsed = SchnorrSignature.Parse("40B9BB2BE0AE02607279EDA64015A8D86E3763279170340B8243F7CE5344D77AFF1191598BAF2FD26149CAC3B4B12C2C433261C00834DB6098CB172AA48EF522");
            Assert.Equal(parsed, signature);

            Assert.True(PersonalMessage.VerifyMessage(key.PubKey, "Hello Kaspa!", signature));
            Assert.True(PersonalMessage.VerifyMessage(key.GetAddress(Network.Mainnet), "Hello Kaspa!", signature));
            Assert.True(PersonalMessage.VerifyMessage(key.GetAddress(Network.Testnet), "Hello Kaspa!", signature));
            Assert.False(PersonalMessage.VerifyMessage(other.PubKey, "Hello Kaspa!", signature));
            Assert.False(PersonalMessage.VerifyMessage(other.GetAddress(Network.Mainnet), "Hello Kaspa!", signature));
            Assert.False(PersonalMessage.VerifyMessage(key.GetEcdsaAddress(Network.Mainnet), "Hello Kaspa!", signature));
            Assert.False(PersonalMessage.VerifyMessage(new KaspaScriptHashAddress(key.PubKey.ToBytes(), Network.Mainnet), "Hello Kaspa!", signature));
            Assert.False(PersonalMessage.VerifyMessage(new KaspaPubKeyAddress(new byte[32], Network.Mainnet), "Hello Kaspa!", signature));
            Assert.False(PersonalMessage.VerifyMessage(key.PubKey, "Not Hello Kaspa!", signature));
            Assert.False(PersonalMessage.VerifyMessage(key.PubKey, "Hello Kaspa!", new SchnorrSignature(new byte[64])));

            using var fresh = Key.Parse(SecretA);
            var random = PersonalMessage.SignMessage(fresh, "");
            Assert.True(PersonalMessage.VerifyMessage(fresh.PubKey, "", random));
            Assert.NotEqual(PersonalMessage.SignMessage(fresh, "", new byte[32]).ToString(), random.ToString());
        }

        [Fact]
        public void Null_arguments_throw()
        {
            using var key = Key.Parse(SecretA);
            var signature = new SchnorrSignature(new byte[64]);
            Assert.Throws<ArgumentNullException>(() => PersonalMessage.Hash(null!));
            Assert.Throws<ArgumentNullException>(() => PersonalMessage.SignMessage(null!, "Hello Kaspa!"));
            Assert.Throws<ArgumentNullException>(() => PersonalMessage.SignMessage(key, null!));
            Assert.Throws<ArgumentNullException>(() => PersonalMessage.VerifyMessage((PubKey)null!, "Hello Kaspa!", signature));
            Assert.Throws<ArgumentNullException>(() => PersonalMessage.VerifyMessage(key.PubKey, null!, signature));
            Assert.Throws<ArgumentNullException>(() => PersonalMessage.VerifyMessage(key.PubKey, "Hello Kaspa!", null!));
            Assert.Throws<ArgumentNullException>(() => PersonalMessage.VerifyMessage((KaspaAddress)null!, "Hello Kaspa!", signature));
            Assert.Throws<ArgumentException>(() => PersonalMessage.SignMessage(key, "Hello Kaspa!", new byte[3]));
        }

        private static void AssertVector(string message, string secretHex, byte auxLast, string publicKeyHex, string signatureHex)
        {
            var aux = new byte[32];
            aux[31] = auxLast;
            using var key = Key.Parse(secretHex);
            Assert.Equal(publicKeyHex, key.PubKey.ToString(), ignoreCase: true);
            var signature = PersonalMessage.SignMessage(key, message, aux);
            Assert.Equal(signatureHex, signature.ToString(), ignoreCase: true);
            var publicKey = PubKey.Parse(publicKeyHex);
            Assert.True(PersonalMessage.VerifyMessage(publicKey, message, signature));
            Assert.True(PersonalMessage.VerifyMessage(publicKey.GetAddress(Network.Mainnet), message, signature));
        }
    }
}
