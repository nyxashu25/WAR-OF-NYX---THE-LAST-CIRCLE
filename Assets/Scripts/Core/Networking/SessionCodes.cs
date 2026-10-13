using System;
using System.Text;

namespace WarOfNyx.Core.Networking
{
    /// <summary>
    /// Room codes for private test rooms and dev servers (spec 20 "join code").
    /// Codes are uppercase A–Z, 0–9 and '-', 4–16 characters. Generated codes avoid
    /// look-alike characters (0/O, 1/I/L) so they are easy to read out loud.
    /// </summary>
    public static class SessionCodes
    {
        public const int MinLength = 4;
        public const int MaxLength = 16;
        public const int GeneratedLength = 6;

        private const string ReadableAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public static bool TrySanitize(string input, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var sb = new StringBuilder(input.Length);
            foreach (char raw in input.Trim())
            {
                char c = char.ToUpperInvariant(raw);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-')
                    sb.Append(c);
                else if (c == ' ' || c == '_')
                    sb.Append('-');
                else
                    return false;
            }

            if (sb.Length < MinLength || sb.Length > MaxLength)
                return false;

            code = sb.ToString();
            return true;
        }

        public static string Generate(Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var chars = new char[GeneratedLength];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = ReadableAlphabet[random.Next(ReadableAlphabet.Length)];
            return new string(chars);
        }
    }

    /// <summary>
    /// Display names. The server sanitizes every name it receives (clients are never trusted);
    /// anything unusable becomes "NYX-&lt;id&gt;".
    /// </summary>
    public static class PlayerNames
    {
        public const int MaxLength = 16;

        public static string Sanitize(string input, int playerId)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                var sb = new StringBuilder(MaxLength);
                bool lastWasSpace = true; // trims leading spaces
                foreach (char c in input)
                {
                    if (sb.Length >= MaxLength)
                        break;

                    if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    {
                        sb.Append(c);
                        lastWasSpace = false;
                    }
                    else if (char.IsWhiteSpace(c) && !lastWasSpace)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }
                }

                string name = sb.ToString().TrimEnd();
                if (name.Length >= 2)
                    return name;
            }

            return Fallback(playerId);
        }

        public static string Fallback(int playerId) => $"NYX-{Math.Abs(playerId) % 10000:0000}";
    }
}
