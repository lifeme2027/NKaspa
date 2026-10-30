using System.Globalization;
using System.Text;

namespace NKaspa
{
    /// <summary>
    /// Parser for the rusty-kaspa txscript test notation.
    /// Integers become canonical script numbers. <c>0x</c> hex is appended raw.
    /// A quoted token is a data push. Opcode names ignore underscores and case.
    /// </summary>
    internal static class ScriptShortForm
    {
        private static readonly Dictionary<string, byte> Names = BuildNames();

        internal static ScriptError? TryParse(string text, out byte[] script)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var builder = new ScriptBuilder();
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var lines = normalized.Split('\n');
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var comment = line.IndexOf('#');
                if (comment >= 0)
                {
                    line = line.Substring(0, comment);
                }

                var tokens = line.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < tokens.Length; i++)
                {
                    var token = tokens[i];
                    if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    {
                        builder.AddInt64(number);
                        continue;
                    }

                    if (token.Length >= 2 && token[0] == '0' && token[1] == 'x' && TryHex(token.Substring(2), out var raw))
                    {
                        builder.AddRaw(raw);
                        continue;
                    }

                    if (token.Length >= 2 && token[0] == '\'' && token[token.Length - 1] == '\'')
                    {
                        var quoted = Encoding.UTF8.GetBytes(token.Substring(1, token.Length - 2));
                        if (quoted.Length > ScriptLimits.MaxScriptElementSize)
                        {
                            script = Array.Empty<byte>();
                            return ScriptError.ElementTooBig(quoted.Length, ScriptLimits.MaxScriptElementSize);
                        }

                        builder.AddData(quoted);
                        continue;
                    }

                    var key = token.Replace("_", string.Empty).ToUpperInvariant();
                    if (!Names.TryGetValue(key, out var code))
                    {
                        script = Array.Empty<byte>();
                        return ScriptError.BadToken(token);
                    }

                    builder.AddRaw(new[] { code });
                }
            }

            script = builder.ToBytes();
            return null;
        }

        private static bool TryHex(string text, out byte[] bytes)
        {
            if ((text.Length & 1) != 0)
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            bytes = new byte[text.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var high = Hex(text[i * 2]);
                var low = Hex(text[(i * 2) + 1]);
                if (high < 0 || low < 0)
                {
                    bytes = Array.Empty<byte>();
                    return false;
                }

                bytes[i] = (byte)((high << 4) | low);
            }

            return true;
        }

        private static int Hex(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }

            if (value >= 'a' && value <= 'f')
            {
                return value - 'a' + 10;
            }

            if (value >= 'A' && value <= 'F')
            {
                return value - 'A' + 10;
            }

            return -1;
        }

        private static Dictionary<string, byte> BuildNames()
        {
            var names = new Dictionary<string, byte>(StringComparer.Ordinal);
            foreach (OpcodeType code in Enum.GetValues(typeof(OpcodeType)))
            {
                var full = code.ToString().Replace("_", string.Empty).ToUpperInvariant();
                names[full] = (byte)code;
            }

            foreach (OpcodeType code in Enum.GetValues(typeof(OpcodeType)))
            {
                if (!AllowsShort(code))
                {
                    continue;
                }

                var full = code.ToString().Replace("_", string.Empty).ToUpperInvariant();
                if (full.Length <= 2)
                {
                    continue;
                }

                var shortName = full.Substring(2);
                if (!names.ContainsKey(shortName))
                {
                    names[shortName] = (byte)code;
                }
            }

            return names;
        }

        private static bool AllowsShort(OpcodeType code)
        {
            var name = code.ToString();
            if (name == "OP_FALSE" || name == "OP_TRUE")
            {
                return true;
            }

            var value = (byte)code;
            return value != 0 && (value < (byte)OpcodeType.OP_1 || value > (byte)OpcodeType.OP_16);
        }
    }
}
