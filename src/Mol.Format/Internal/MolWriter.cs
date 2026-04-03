using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Mol.Format.Internal;

internal static class MolWriter
{
    public static string Write(JsonNode? node, MolSerializerOptions options)
    {
        var builder = new StringBuilder();
        WriteRoot(builder, node, options);
        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static void WriteRoot(StringBuilder builder, JsonNode? node, MolSerializerOptions options)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                WriteObjectMembers(builder, jsonObject, 0, options);
                break;
            case JsonArray jsonArray:
                WriteRootArray(builder, jsonArray, options);
                break;
            default:
                builder.Append(FormatScalar(node, preferQuotedStrings: true));
                builder.AppendLine();
                break;
        }
    }

    private static void WriteRootArray(StringBuilder builder, JsonArray array, MolSerializerOptions options)
    {
        for (var i = 0; i < array.Count; i++)
        {
            builder.Append("# ");
            builder.Append(options.RootArrayItemHeading);
            builder.AppendLine();

            var item = array[i];
            switch (item)
            {
                case JsonObject jsonObject:
                    WriteObjectMembers(builder, jsonObject, 0, options);
                    break;
                case JsonArray nestedArray:
                    WriteNamedNode(builder, options.RootArrayItemHeading, nestedArray, 0, options);
                    break;
                default:
                    builder.Append(FormatScalar(item, preferQuotedStrings: true));
                    builder.AppendLine();
                    break;
            }

            if (i < array.Count - 1)
            {
                builder.AppendLine();
            }
        }
    }

    private static void WriteObjectMembers(StringBuilder builder, JsonObject jsonObject, int indentLevel, MolSerializerOptions options)
    {
        foreach (var property in jsonObject)
        {
            WriteNamedNode(builder, property.Key, property.Value, indentLevel, options);
        }
    }

    private static void WriteNamedNode(StringBuilder builder, string key, JsonNode? node, int indentLevel, MolSerializerOptions options)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                WriteNamedNode(builder, key, item, indentLevel, options);
            }

            return;
        }

        if (node is JsonObject jsonObject)
        {
            WriteObjectNode(builder, key, jsonObject, indentLevel, options);
            return;
        }

        AppendIndent(builder, indentLevel, options);
        builder.Append(key);
        builder.Append(": ");
        builder.Append(FormatScalar(node, preferQuotedStrings: false));
        builder.AppendLine();
    }

    private static void WriteObjectNode(StringBuilder builder, string key, JsonObject jsonObject, int indentLevel, MolSerializerOptions options)
    {
        var properties = jsonObject.ToList();
        var valueProperty = properties.FirstOrDefault(property => property.Key == options.ValuePropertyName);
        var hasInlineValue = valueProperty.Key is not null && CanWriteInline(valueProperty.Value);
        var childProperties = properties.Where(property => property.Key != options.ValuePropertyName).ToList();

        AppendIndent(builder, indentLevel, options);
        builder.Append(key);

        if (jsonObject.Count == 0)
        {
            builder.Append(':');
            builder.AppendLine();
            return;
        }

        if (hasInlineValue)
        {
            builder.Append(": ");
            builder.Append(FormatScalar(valueProperty.Value, preferQuotedStrings: false));
            builder.AppendLine();
        }
        else
        {
            builder.Append(':');
            builder.AppendLine();

            if (valueProperty.Key is not null)
            {
                WriteNamedNode(builder, options.ValuePropertyName, valueProperty.Value, indentLevel + 1, options);
            }
        }

        foreach (var property in childProperties)
        {
            WriteNamedNode(builder, property.Key, property.Value, indentLevel + 1, options);
        }
    }

    private static bool CanWriteInline(JsonNode? node)
    {
        return node is not JsonObject and not JsonArray;
    }

    private static string FormatScalar(JsonNode? node, bool preferQuotedStrings)
    {
        if (node is null)
        {
            return "null";
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var booleanValue))
        {
            return booleanValue ? "true" : "false";
        }

        if (node is JsonValue jsonLongValue && jsonLongValue.TryGetValue<long>(out var longValue))
        {
            return longValue.ToString(CultureInfo.InvariantCulture);
        }

        if (node is JsonValue jsonDecimalValue && jsonDecimalValue.TryGetValue<decimal>(out var decimalValue))
        {
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        }

        if (node is JsonValue jsonDoubleValue && jsonDoubleValue.TryGetValue<double>(out var doubleValue))
        {
            return doubleValue.ToString("R", CultureInfo.InvariantCulture);
        }

        if (node is JsonValue jsonStringValue && jsonStringValue.TryGetValue<string>(out var stringValue))
        {
            if (!preferQuotedStrings && IsSafeBareString(stringValue))
            {
                return stringValue;
            }

            return QuoteString(stringValue);
        }

        return QuoteString(node.ToJsonString());
    }

    private static bool IsSafeBareString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Contains('\n') || value.Contains('\r') || value.Contains('\t'))
        {
            return false;
        }

        if (value.Contains('"') || value.Contains('\'') || value.Contains('\\'))
        {
            return false;
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (value == "true" || value == "false" || value == "null")
        {
            return false;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        return true;
    }

    private static string QuoteString(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => character.ToString(),
            });
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static void AppendIndent(StringBuilder builder, int indentLevel, MolSerializerOptions options)
    {
        for (var i = 0; i < indentLevel; i++)
        {
            builder.Append(options.Indentation);
        }
    }
}
