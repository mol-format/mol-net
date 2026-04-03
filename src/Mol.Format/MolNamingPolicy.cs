using System.Text;
using System.Text.Json;

namespace Mol.Format;

public abstract class MolNamingPolicy : JsonNamingPolicy
{
    public static JsonNamingPolicy Identity { get; } = new IdentityPolicy();

    public new static JsonNamingPolicy CamelCase { get; } = new DelimitedWordPolicy(WordCasing.CamelCase, separator: null);

    public static JsonNamingPolicy PascalCase { get; } = new DelimitedWordPolicy(WordCasing.PascalCase, separator: null);

    public static JsonNamingPolicy SnakeCase { get; } = new DelimitedWordPolicy(WordCasing.LowerCase, "_");

    public static JsonNamingPolicy KebabCase { get; } = new DelimitedWordPolicy(WordCasing.LowerCase, "-");

    private sealed class IdentityPolicy : MolNamingPolicy
    {
        public override string ConvertName(string name) => name;
    }

    private enum WordCasing
    {
        CamelCase,
        PascalCase,
        LowerCase,
    }

    private sealed class DelimitedWordPolicy : MolNamingPolicy
    {
        private readonly WordCasing _wordCasing;
        private readonly string? _separator;

        public DelimitedWordPolicy(WordCasing wordCasing, string? separator)
        {
            _wordCasing = wordCasing;
            _separator = separator;
        }

        public override string ConvertName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var words = SplitWords(name);
            if (words.Count == 0)
            {
                return string.Empty;
            }

            return _wordCasing switch
            {
                WordCasing.CamelCase => BuildCamelCase(words),
                WordCasing.PascalCase => BuildPascalCase(words),
                _ => BuildDelimited(words, _separator!),
            };
        }

        private static string BuildCamelCase(IReadOnlyList<string> words)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < words.Count; i++)
            {
                var word = words[i].ToLowerInvariant();
                builder.Append(i == 0 ? word : Capitalize(word));
            }

            return builder.ToString();
        }

        private static string BuildPascalCase(IReadOnlyList<string> words)
        {
            var builder = new StringBuilder();

            foreach (var word in words)
            {
                builder.Append(Capitalize(word.ToLowerInvariant()));
            }

            return builder.ToString();
        }

        private static string BuildDelimited(IReadOnlyList<string> words, string separator)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < words.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(separator);
                }

                builder.Append(words[i].ToLowerInvariant());
            }

            return builder.ToString();
        }

        private static string Capitalize(string value)
        {
            if (value.Length == 0)
            {
                return value;
            }

            if (value.Length == 1)
            {
                return value.ToUpperInvariant();
            }

            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        private static List<string> SplitWords(string value)
        {
            var words = new List<string>();
            var current = new StringBuilder();

            static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character);

            for (var i = 0; i < value.Length; i++)
            {
                var currentCharacter = value[i];

                if (!IsWordCharacter(currentCharacter))
                {
                    FlushCurrentWord(words, current);
                    continue;
                }

                if (current.Length > 0 &&
                    char.IsUpper(currentCharacter) &&
                    current.Length > 0 &&
                    !char.IsUpper(current[^1]))
                {
                    FlushCurrentWord(words, current);
                }

                current.Append(currentCharacter);
            }

            FlushCurrentWord(words, current);
            return words;
        }

        private static void FlushCurrentWord(List<string> words, StringBuilder current)
        {
            if (current.Length == 0)
            {
                return;
            }

            words.Add(current.ToString());
            current.Clear();
        }
    }
}
