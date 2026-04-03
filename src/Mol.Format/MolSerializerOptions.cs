using System.Text.Json;

namespace Mol.Format;

public sealed class MolSerializerOptions
{
    public JsonNamingPolicy KeyNamingPolicy { get; set; } = MolNamingPolicy.Identity;

    public JsonSerializerOptions JsonSerializerOptions { get; } = CreateDefaultJsonSerializerOptions();

    public string Indentation { get; set; } = "\t";

    public string ValuePropertyName { get; set; } = "$value";

    public string RootArrayItemHeading { get; set; } = "Item";

    internal static MolSerializerOptions Default { get; } = new();

    private static JsonSerializerOptions CreateDefaultJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }
}
