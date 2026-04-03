using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mol.Format.Internal;

internal static partial class MolProjector
{
    public static JsonNode? Project(MolParsedDocument document, MolSerializerOptions options)
    {
        if (document.RootScalar is not null)
        {
            return CreateScalarNode(document.RootScalar, document.RootScalarWasQuoted);
        }

        var rootHeadings = document.Entries.Where(entry => entry.IsRootHeading).ToList();
        var nonRootEntries = document.Entries.Where(entry => !entry.IsRootHeading).ToList();

        if (rootHeadings.Count > 1 && nonRootEntries.Count == 0)
        {
            var array = new JsonArray();
            foreach (var heading in rootHeadings)
            {
                array.Add(ProjectRootHeading(heading, options));
            }

            return array;
        }

        if (rootHeadings.Count == 1)
        {
            var rootNode = ProjectRootHeading(rootHeadings[0], options);
            if (nonRootEntries.Count == 0)
            {
                return rootNode;
            }

            if (rootNode is not JsonObject rootObject)
            {
                throw new InvalidOperationException("A scalar root heading cannot be merged with sibling entries.");
            }

            MergeEntriesIntoObject(rootObject, nonRootEntries, options);
            return rootObject;
        }

        return ProjectEntries(document.Entries, options);
    }

    private static JsonNode? ProjectRootHeading(MolSyntaxEntry entry, MolSerializerOptions options)
    {
        if (entry.Children.Count > 0)
        {
            return ProjectEntries(entry.Children, options);
        }

        if (entry.TextBody is not null)
        {
            return CreateScalarNode(entry.TextBody, quoted: false);
        }

        return new JsonObject();
    }

    private static JsonObject ProjectEntries(IReadOnlyList<MolSyntaxEntry> entries, MolSerializerOptions options)
    {
        var jsonObject = new JsonObject();
        MergeEntriesIntoObject(jsonObject, entries, options);
        return jsonObject;
    }

    private static void MergeEntriesIntoObject(JsonObject jsonObject, IReadOnlyList<MolSyntaxEntry> entries, MolSerializerOptions options)
    {
        foreach (var entry in entries)
        {
            var name = options.KeyNamingPolicy.ConvertName(entry.Key);
            var value = ProjectEntryValue(entry, options);

            if (!jsonObject.TryGetPropertyValue(name, out var existing))
            {
                jsonObject.Add(name, value);
                continue;
            }

            if (existing is JsonArray array)
            {
                array.Add(value);
                continue;
            }

            var duplicateArray = new JsonArray();
            duplicateArray.Add(existing?.DeepClone());
            duplicateArray.Add(value);
            jsonObject[name] = duplicateArray;
        }
    }

    private static JsonNode? ProjectEntryValue(MolSyntaxEntry entry, MolSerializerOptions options)
    {
        if (entry.Children.Count == 0)
        {
            if (entry.TextBody is not null)
            {
                return CreateScalarNode(entry.TextBody, quoted: false);
            }

            if (entry.InlineValue is not null)
            {
                return CreateScalarNode(entry.InlineValue, entry.InlineValueWasQuoted);
            }

            return new JsonObject();
        }

        var jsonObject = new JsonObject();

        if (entry.InlineValue is not null)
        {
            jsonObject.Add(options.ValuePropertyName, CreateScalarNode(entry.InlineValue, entry.InlineValueWasQuoted));
        }

        MergeEntriesIntoObject(jsonObject, entry.Children, options);
        return jsonObject;
    }

    private static JsonNode? CreateScalarNode(string value, bool quoted)
    {
        if (quoted)
        {
            return JsonValue.Create(value);
        }

        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            return JsonValue.Create(value);
        }

        if (trimmed == "null")
        {
            return null;
        }

        if (trimmed == "true")
        {
            return JsonValue.Create(true);
        }

        if (trimmed == "false")
        {
            return JsonValue.Create(false);
        }

        if (IntegerPattern().IsMatch(trimmed) &&
            long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
        {
            return JsonValue.Create(integerValue);
        }

        if (DecimalPattern().IsMatch(trimmed))
        {
            if (!trimmed.Contains('e') && !trimmed.Contains('E') &&
                decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            {
                return JsonValue.Create(decimalValue);
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return JsonValue.Create(doubleValue);
            }
        }

        return JsonValue.Create(value);
    }

    [GeneratedRegex(@"^-?(0|[1-9]\d*)$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^-?(0|[1-9]\d*)(\.\d+)?([eE][+\-]?\d+)?$")]
    private static partial Regex DecimalPattern();
}
