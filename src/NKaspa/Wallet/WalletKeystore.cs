using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NKaspa.DataEncoders;
using NKaspa.JsonConverters;

namespace NKaspa
{
    /// <summary>
    /// Encrypts a mnemonic or a seed with a password.
    /// The KDF is PBKDF2-HMAC-SHA256 (<c>Rfc2898DeriveBytes.Pbkdf2</c> on .NET 5 and later, <c>Rfc2898DeriveBytes.GetBytes</c> on .NET Standard 2.1).
    /// Argon2 is not in the .NET base class library, so this type does not use it and does not take an extra package.
    /// The cipher is AES-256-GCM (<see cref="AesGcm"/>). Salt and nonce are random unless <see cref="WalletKeystoreParameters"/> supplies them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The JSON object fields, in order, are <c>version</c>, <c>kdf</c>, <c>kdfParams</c>, <c>cipher</c>, <c>salt</c>, <c>nonce</c>, <c>tag</c>, and <c>ciphertext</c>.
    /// <c>kdf</c> is <c>pbkdf2-sha256</c>. <c>kdfParams</c> holds <c>iterations</c>, <c>hash</c> (<c>sha256</c>), and <c>keyLength</c> (32).
    /// <c>cipher</c> is <c>aes-256-gcm</c>. The binary fields are base64. Version is 1.
    /// </para>
    /// <para>
    /// Associated data is the UTF-8 header <c>nkaspa-keystore-v1|pbkdf2-sha256|{iterations}|sha256|32|aes-256-gcm</c>.
    /// <see cref="AesGcm"/> checks the tag in constant time. A wrong password, a changed ciphertext, tag, or nonce,
    /// or a changed iteration count, all fail as <see cref="WrongKeystorePasswordException"/>. The message does not contain the secret.
    /// A wrong version, KDF name, hash, key length, or cipher name fails as <see cref="KeystoreException"/> before decryption.
    /// </para>
    /// <para>
    /// <see cref="DefaultIterations"/> is 600000. That is the OWASP PBKDF2-HMAC-SHA256 iteration count stored in the file.
    /// A test may pass a smaller positive count. Production calls should keep the default.
    /// Key bytes and plaintext buffers are cleared with <see cref="CryptographicOperations.ZeroMemory"/> where this method still owns them.
    /// A .NET string cannot be cleared. <see cref="KeystoreSecret.ToString"/> returns <c>NKaspa.KeystoreSecret</c>.
    /// </para>
    /// </remarks>
    public static class WalletKeystore
    {
        /// <summary>JSON <c>version</c>. The only version this type reads and writes.</summary>
        public const int Version = 1;

        /// <summary>JSON <c>kdf</c>.</summary>
        public const string KdfName = "pbkdf2-sha256";

        /// <summary>JSON <c>cipher</c>.</summary>
        public const string CipherName = "aes-256-gcm";

        /// <summary>JSON <c>kdfParams.hash</c>.</summary>
        public const string HashName = "sha256";

        /// <summary>PBKDF2-HMAC-SHA256 iterations written when the caller does not pass <see cref="WalletKeystoreParameters"/>.</summary>
        public const int DefaultIterations = 600_000;

        /// <summary>Salt length in bytes.</summary>
        public const int SaltLength = 16;

        /// <summary>AES-GCM nonce length in bytes.</summary>
        public const int NonceLength = 12;

        /// <summary>AES-GCM tag length in bytes.</summary>
        public const int TagLength = 16;

        /// <summary>Derived AES-256 key length in bytes.</summary>
        public const int KeyLength = 32;

        /// <summary>Encrypts a UTF-8 secret. The returned JSON does not contain the secret.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="secret"/> is null. <paramref name="password"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Iterations are below 1. Salt is not 16 bytes. Nonce is not 12 bytes.</exception>
        public static string Encrypt(string secret, string password, WalletKeystoreParameters? parameters = null)
        {
            if (secret == null)
            {
                throw new ArgumentNullException(nameof(secret));
            }

            var bytes = Encoding.UTF8.GetBytes(secret);
            try
            {
                return Encrypt(bytes, password, parameters);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        /// <summary>Encrypts <paramref name="secret"/>. The bytes are not retained.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="secret"/> is null. <paramref name="password"/> is null.</exception>
        /// <exception cref="System.ObjectDisposedException"><paramref name="secret"/> has been disposed.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Iterations are below 1. Salt is not 16 bytes. Nonce is not 12 bytes.</exception>
        public static string Encrypt(KeystoreSecret secret, string password, WalletKeystoreParameters? parameters = null)
        {
            if (secret == null)
            {
                throw new ArgumentNullException(nameof(secret));
            }

            var bytes = secret.CopyBytes();
            try
            {
                return Encrypt(bytes, password, parameters);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        /// <summary>Encrypts raw secret bytes, such as a BIP39 seed.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="password"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Iterations are below 1. Salt is not 16 bytes. Nonce is not 12 bytes.</exception>
        public static string Encrypt(ReadOnlySpan<byte> secret, string password, WalletKeystoreParameters? parameters = null)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var iterations = parameters == null ? DefaultIterations : parameters.Iterations;
            if (iterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(parameters), "Iterations must be positive.");
            }

            var salt = CopyOrRandom(parameters == null ? null : parameters.Salt, SaltLength, nameof(parameters));
            var nonce = CopyOrRandom(parameters == null ? null : parameters.Nonce, NonceLength, nameof(parameters));
            var ciphertext = new byte[secret.Length];
            var tag = new byte[TagLength];
            var key = DeriveKey(password, salt, iterations);
            try
            {
                using var gcm = CreateGcm(key);
                gcm.Encrypt(nonce, secret, ciphertext, tag, HeaderAad(iterations));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            var json = WriteJson(iterations, salt, nonce, tag, ciphertext);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(nonce);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
            return json;
        }

        /// <summary>Decrypts JSON produced by <see cref="Encrypt(string, string, WalletKeystoreParameters?)"/> as a UTF-8 string.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="json"/> is null. <paramref name="password"/> is null.</exception>
        /// <exception cref="NKaspa.KeystoreException">The JSON, version, KDF, cipher, or field lengths are not this format. The plaintext is not UTF-8.</exception>
        /// <exception cref="NKaspa.WrongKeystorePasswordException">The password is wrong, or the ciphertext, tag, nonce, or iteration count was changed.</exception>
        public static string DecryptToString(string json, string password)
        {
            var bytes = Decrypt(json, password);
            try
            {
                try
                {
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
                }
                catch (ArgumentException)
                {
                    throw new KeystoreException("Keystore plaintext is not UTF-8.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        /// <summary>
        /// Decrypts JSON produced by <see cref="Encrypt(ReadOnlySpan{byte}, string, WalletKeystoreParameters?)"/>.
        /// The caller owns the returned bytes and should clear them.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="json"/> is null. <paramref name="password"/> is null.</exception>
        /// <exception cref="NKaspa.KeystoreException">The JSON, version, KDF, cipher, or field lengths are not this format.</exception>
        /// <exception cref="NKaspa.WrongKeystorePasswordException">The password is wrong, or the ciphertext, tag, nonce, or iteration count was changed.</exception>
        public static byte[] Decrypt(string json, string password)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var file = ReadJson(json);
            var key = DeriveKey(password, file.Salt, file.Iterations);
            var plaintext = new byte[file.Ciphertext.Length];
            try
            {
                using var gcm = CreateGcm(key);
                gcm.Decrypt(file.Nonce, file.Ciphertext, file.Tag, plaintext, HeaderAad(file.Iterations));
                return plaintext;
            }
#if NET5_0_OR_GREATER
            catch (AuthenticationTagMismatchException)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new WrongKeystorePasswordException();
            }
            catch (CryptographicException)
            {
                CryptographicOperations.ZeroMemory(plaintext);
                throw new KeystoreException("Keystore decryption failed.");
            }
#else
            catch (CryptographicException)
            {
                // netstandard2.1 AesGcm reports a rejected tag as CryptographicException.
                CryptographicOperations.ZeroMemory(plaintext);
                throw new WrongKeystorePasswordException();
            }
#endif
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(file.Salt);
                CryptographicOperations.ZeroMemory(file.Nonce);
                CryptographicOperations.ZeroMemory(file.Tag);
                CryptographicOperations.ZeroMemory(file.Ciphertext);
            }
        }

        /// <summary>
        /// Decrypts into a <see cref="KeystoreSecret"/> whose <see cref="object.ToString"/> is redacted.
        /// The secret is opaque bytes. Use <see cref="DecryptToString"/> for a mnemonic sentence.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="json"/> is null. <paramref name="password"/> is null.</exception>
        /// <exception cref="NKaspa.KeystoreException">The JSON, version, KDF, cipher, or field lengths are not this format.</exception>
        /// <exception cref="NKaspa.WrongKeystorePasswordException">The password is wrong, or the ciphertext, tag, nonce, or iteration count was changed.</exception>
        public static KeystoreSecret DecryptSecret(string json, string password)
        {
            var bytes = Decrypt(json, password);
            return KeystoreSecret.FromBytes(bytes);
        }

        /// <summary>PBKDF2-HMAC-SHA256. Tests use this for the RFC 7914 vectors. The keystore itself asks for <see cref="KeyLength"/> bytes.</summary>
        internal static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int iterations, int length)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            if (salt == null)
            {
                throw new ArgumentNullException(nameof(salt));
            }

            if (iterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }

            if (length < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            // Rfc2898DeriveBytes.Pbkdf2 is .NET 5+. netstandard2.1 uses the instance method with the same HMAC-SHA256.
#if NET5_0_OR_GREATER
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
#else
            using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(length);
#endif
        }

        private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                return Pbkdf2Sha256(passwordBytes, salt, iterations, KeyLength);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }

        private static AesGcm CreateGcm(byte[] key)
        {
#if NET5_0_OR_GREATER
            return new AesGcm(key, TagLength);
#else
            return new AesGcm(key);
#endif
        }

        private static byte[] HeaderAad(int iterations)
        {
            return Encoding.UTF8.GetBytes(
                "nkaspa-keystore-v1|" + KdfName + "|" + iterations.ToString(CultureInfo.InvariantCulture) + "|" + HashName + "|" + KeyLength.ToString(CultureInfo.InvariantCulture) + "|" + CipherName);
        }

        private static byte[] CopyOrRandom(byte[]? supplied, int length, string parameterName)
        {
            if (supplied == null)
            {
                var created = new byte[length];
                RandomNumberGenerator.Fill(created);
                return created;
            }

            if (supplied.Length != length)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            var copy = new byte[length];
            Buffer.BlockCopy(supplied, 0, copy, 0, length);
            return copy;
        }

        private static string WriteJson(int iterations, byte[] salt, byte[] nonce, byte[] tag, byte[] ciphertext)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteNumber("version", Version);
                writer.WriteString("kdf", KdfName);
                writer.WritePropertyName("kdfParams");
                writer.WriteStartObject();
                writer.WriteNumber("iterations", iterations);
                writer.WriteString("hash", HashName);
                writer.WriteNumber("keyLength", KeyLength);
                writer.WriteEndObject();
                writer.WriteString("cipher", CipherName);
                writer.WriteString("salt", Convert.ToBase64String(salt));
                writer.WriteString("nonce", Convert.ToBase64String(nonce));
                writer.WriteString("tag", Convert.ToBase64String(tag));
                writer.WriteString("ciphertext", Convert.ToBase64String(ciphertext));
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static KeystoreFile ReadJson(string json)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                throw new KeystoreException("Keystore JSON is not valid.");
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new KeystoreException("Keystore JSON is not valid.");
                }

                if (!root.TryGetProperty("version", out var versionElement) || !versionElement.TryGetInt32(out var version))
                {
                    throw new KeystoreException("Unsupported keystore version.");
                }

                if (version != Version)
                {
                    throw new KeystoreException("Unsupported keystore version.");
                }

                if (!root.TryGetProperty("kdf", out var kdfElement) || kdfElement.GetString() != KdfName)
                {
                    throw new KeystoreException("Unsupported keystore KDF.");
                }

                if (!root.TryGetProperty("cipher", out var cipherElement) || cipherElement.GetString() != CipherName)
                {
                    throw new KeystoreException("Unsupported keystore cipher.");
                }

                if (!root.TryGetProperty("kdfParams", out var kdfParams) || kdfParams.ValueKind != JsonValueKind.Object)
                {
                    throw new KeystoreException("Unsupported keystore KDF.");
                }

                if (!kdfParams.TryGetProperty("hash", out var hashElement) || hashElement.GetString() != HashName)
                {
                    throw new KeystoreException("Unsupported keystore hash.");
                }

                if (!kdfParams.TryGetProperty("keyLength", out var keyLengthElement) || !keyLengthElement.TryGetInt32(out var keyLength) || keyLength != KeyLength)
                {
                    throw new KeystoreException("Keystore key length must be 32.");
                }

                if (!kdfParams.TryGetProperty("iterations", out var iterationsElement) || !iterationsElement.TryGetInt32(out var iterations) || iterations < 1)
                {
                    throw new KeystoreException("Keystore iterations must be positive.");
                }

                var salt = ReadField(root, "salt", SaltLength);
                var nonce = ReadField(root, "nonce", NonceLength);
                var tag = ReadField(root, "tag", TagLength);
                var ciphertext = ReadField(root, "ciphertext", null);
                return new KeystoreFile(iterations, salt, nonce, tag, ciphertext);
            }
        }

        private static byte[] ReadField(JsonElement root, string name, int? length)
        {
            if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            {
                throw new KeystoreException("Keystore field " + name + " is missing.");
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(element.GetString() ?? string.Empty);
            }
            catch (FormatException)
            {
                throw new KeystoreException("Keystore field " + name + " is not valid base64.");
            }

            if (length.HasValue && bytes.Length != length.Value)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw new KeystoreException("Keystore field " + name + " has the wrong length.");
            }

            return bytes;
        }

        private readonly struct KeystoreFile
        {
            public KeystoreFile(int iterations, byte[] salt, byte[] nonce, byte[] tag, byte[] ciphertext)
            {
                Iterations = iterations;
                Salt = salt;
                Nonce = nonce;
                Tag = tag;
                Ciphertext = ciphertext;
            }

            public int Iterations { get; }

            public byte[] Salt { get; }

            public byte[] Nonce { get; }

            public byte[] Tag { get; }

            public byte[] Ciphertext { get; }
        }
    }

    /// <summary>Password and layout failures for <see cref="WalletKeystore"/>.</summary>
    public class KeystoreException : Exception
    {
        /// <summary>Creates an exception whose message does not contain key material.</summary>
        public KeystoreException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// The password did not open the keystore.
    /// Ciphertext, tag, nonce, and iteration-count changes fail the same way. The message is fixed and contains no secret.
    /// </summary>
    public sealed class WrongKeystorePasswordException : KeystoreException
    {
        /// <summary>Creates the exception. The message is <c>Wrong keystore password.</c></summary>
        public WrongKeystorePasswordException()
            : base("Wrong keystore password.")
        {
        }
    }

    /// <summary>
    /// Iteration count, and optional fixed salt and nonce, for <see cref="WalletKeystore.Encrypt(ReadOnlySpan{byte}, string, WalletKeystoreParameters?)"/>.
    /// A null salt or nonce means a random value. <see cref="ToString"/> does not print the salt or the nonce.
    /// </summary>
    public sealed class WalletKeystoreParameters
    {
        /// <summary>Creates parameters. <paramref name="iterations"/> defaults to <see cref="WalletKeystore.DefaultIterations"/>.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="iterations"/> is below 1. <paramref name="salt"/> is not 16 bytes. <paramref name="nonce"/> is not 12 bytes.</exception>
        public WalletKeystoreParameters(int iterations = WalletKeystore.DefaultIterations, byte[]? salt = null, byte[]? nonce = null)
        {
            if (iterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }

            if (salt != null && salt.Length != WalletKeystore.SaltLength)
            {
                throw new ArgumentOutOfRangeException(nameof(salt));
            }

            if (nonce != null && nonce.Length != WalletKeystore.NonceLength)
            {
                throw new ArgumentOutOfRangeException(nameof(nonce));
            }

            Iterations = iterations;
            Salt = salt == null ? null : (byte[])salt.Clone();
            Nonce = nonce == null ? null : (byte[])nonce.Clone();
        }

        /// <summary>PBKDF2 iteration count stored in the file.</summary>
        public int Iterations { get; }

        /// <summary>Salt, or null when encryption should draw a random salt. The array is a copy.</summary>
        public byte[]? Salt { get; }

        /// <summary>Nonce, or null when encryption should draw a random nonce. The array is a copy.</summary>
        public byte[]? Nonce { get; }

        /// <summary>Type name only (<c>NKaspa.WalletKeystoreParameters</c>). Safe to log.</summary>
        public override string ToString()
        {
            return "NKaspa.WalletKeystoreParameters";
        }
    }

    /// <summary>
    /// A mnemonic sentence or seed bytes held so that <see cref="ToString"/> cannot print them.
    /// <see cref="Reveal"/> is the explicit text form. <see cref="Clear"/> and <see cref="Dispose"/> zero the buffer.
    /// A .NET string returned by <see cref="Reveal"/> or <see cref="GetPhrase"/> cannot be cleared.
    /// System.Text.Json refuses this type. <c>allowPrivateKeys</c> does not unlock it.
    /// Newtonsoft.Json is not referenced. <see cref="ShouldSerializeIsPhrase"/> throws if that serializer is used.
    /// </summary>
    [DebuggerDisplay("NKaspa.KeystoreSecret")]
    [JsonConverter(typeof(KeystoreSecretJsonConverter))]
    public sealed class KeystoreSecret : IDisposable
    {
        private byte[]? _bytes;
        private readonly bool _phrase;
        private bool _disposed;

        private KeystoreSecret(byte[] bytes, bool phrase)
        {
            _bytes = bytes;
            _phrase = phrase;
        }

        /// <summary>Copies <paramref name="phrase"/> as UTF-8. The string itself cannot be cleared.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="phrase"/> is null.</exception>
        public static KeystoreSecret FromPhrase(string phrase)
        {
            if (phrase == null)
            {
                throw new ArgumentNullException(nameof(phrase));
            }

            return new KeystoreSecret(Encoding.UTF8.GetBytes(phrase), phrase: true);
        }

        /// <summary>Copies <paramref name="seed"/>. The caller can clear its own buffer.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="seed"/> is null.</exception>
        public static KeystoreSecret FromSeed(byte[] seed)
        {
            if (seed == null)
            {
                throw new ArgumentNullException(nameof(seed));
            }

            return new KeystoreSecret((byte[])seed.Clone(), phrase: false);
        }

        /// <summary>Takes ownership of <paramref name="bytes"/>. The array is not copied.</summary>
        internal static KeystoreSecret FromBytes(byte[] bytes)
        {
            return new KeystoreSecret(bytes, phrase: false);
        }

        /// <summary>True when this value was created from a phrase.</summary>
        public bool IsPhrase
        {
            get { return _phrase; }
        }

        /// <summary>The UTF-8 phrase. This is the secret.</summary>
        /// <exception cref="System.ObjectDisposedException">This value has been disposed.</exception>
        /// <exception cref="System.InvalidOperationException">This value holds seed bytes, not a phrase.</exception>
        public string GetPhrase()
        {
            if (!_phrase)
            {
                throw new InvalidOperationException("Keystore secret is not a phrase.");
            }

            return Encoding.UTF8.GetString(Bytes());
        }

        /// <summary>A copy of the secret bytes. Clear the copy when finished.</summary>
        /// <exception cref="System.ObjectDisposedException">This value has been disposed.</exception>
        public byte[] CopyBytes()
        {
            var source = Bytes();
            var copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }

        /// <summary>
        /// The secret as text. A phrase is the UTF-8 sentence. Seed bytes are lowercase hex.
        /// <see cref="ToString"/> does not return this value. The returned string cannot be cleared.
        /// A secret opened by <see cref="WalletKeystore.DecryptSecret"/> is seed bytes, so this method returns hex. Use <see cref="WalletKeystore.DecryptToString"/> for the sentence.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">This value has been disposed.</exception>
        public string Reveal()
        {
            if (_phrase)
            {
                return GetPhrase();
            }

            return Encoders.Hex.EncodeData(Bytes());
        }

        /// <summary>Zeros the secret buffer. Same as <see cref="Dispose"/>. A second call does nothing.</summary>
        public void Clear()
        {
            Dispose();
        }

        /// <summary>
        /// Newtonsoft.Json is not a dependency. Its default contract resolver calls this method before writing <see cref="IsPhrase"/>, and the method throws.
        /// System.Text.Json uses <see cref="KeystoreSecretJsonConverter"/> and throws as well, including when <c>allowPrivateKeys</c> is true.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Refusing to convert KeystoreSecret.</exception>
        public bool ShouldSerializeIsPhrase()
        {
            throw new InvalidOperationException(KeystoreSecretJsonConverter.Refusal);
        }

        /// <summary>Clears the secret buffer. A second call does nothing.</summary>
        public void Dispose()
        {
            var bytes = Interlocked.Exchange(ref _bytes, null);
            if (bytes != null)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }

            _disposed = true;
        }

        /// <summary>Type name only (<c>NKaspa.KeystoreSecret</c>). Safe to log. Use <see cref="Reveal"/> for the secret.</summary>
        public override string ToString()
        {
            return "NKaspa.KeystoreSecret";
        }

        private byte[] Bytes()
        {
            var bytes = _bytes;
            if (_disposed || bytes == null)
            {
                throw new ObjectDisposedException("NKaspa.KeystoreSecret");
            }

            return bytes;
        }
    }
}
