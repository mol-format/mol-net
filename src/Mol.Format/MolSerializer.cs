using System.Text.Json;
using System.Text.Json.Nodes;
using Mol.Format.Internal;

namespace Mol.Format;

public static class MolSerializer
{
    public static T? Deserialize<T>(string mol, MolSerializerOptions? options = null)
    {
        var node = DeserializeToNode(mol, options);
        if (node is null)
        {
            return default;
        }

        return node.Deserialize<T>(ResolveOptions(options).JsonSerializerOptions);
    }

    public static object? Deserialize(string mol, Type returnType, MolSerializerOptions? options = null)
    {
        var node = DeserializeToNode(mol, options);
        if (node is null)
        {
            return null;
        }

        return node.Deserialize(returnType, ResolveOptions(options).JsonSerializerOptions);
    }

    public static JsonNode? DeserializeToNode(string mol, MolSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mol);

        var resolvedOptions = ResolveOptions(options);
        var document = MolParser.Parse(mol);
        return MolProjector.Project(document, resolvedOptions);
    }

    public static string Serialize<T>(T value, MolSerializerOptions? options = null)
    {
        var resolvedOptions = ResolveOptions(options);
        var node = value as JsonNode ?? JsonSerializer.SerializeToNode(value, resolvedOptions.JsonSerializerOptions);
        return MolWriter.Write(node, resolvedOptions);
    }

    private static MolSerializerOptions ResolveOptions(MolSerializerOptions? options) => options ?? MolSerializerOptions.Default;
}
