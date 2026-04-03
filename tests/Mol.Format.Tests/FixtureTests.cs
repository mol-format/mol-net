using System.Text.Json.Nodes;
using Xunit;

namespace Mol.Format.Tests;

public sealed class FixtureTests
{
    public static TheoryData<string> IdentityFixtures => CreateFixtureData("identity");

    public static TheoryData<string> CamelCaseFixtures => CreateFixtureData("camelCase");

    [Theory]
    [MemberData(nameof(IdentityFixtures))]
    public void DeserializeToNode_matches_identity_fixture(string molPath)
    {
        var mol = File.ReadAllText(molPath);
        var actual = MolSerializer.DeserializeToNode(mol);
        var expected = ReadJsonFixture(molPath, "identity");

        AssertNodesEqual(expected, actual);
    }

    [Theory]
    [MemberData(nameof(CamelCaseFixtures))]
    public void DeserializeToNode_matches_camel_case_fixture(string molPath)
    {
        var mol = File.ReadAllText(molPath);
        var actual = MolSerializer.DeserializeToNode(
            mol,
            new MolSerializerOptions
            {
                KeyNamingPolicy = MolNamingPolicy.CamelCase,
            });

        var expected = ReadJsonFixture(molPath, "camelCase");
        AssertNodesEqual(expected, actual);
    }

    private static TheoryData<string> CreateFixtureData(string mode)
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(GetFixturesRoot(), "*.mol", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.Ordinal))
        {
            var expectedJsonPath = Path.ChangeExtension(path, $"{mode}.json");
            if (File.Exists(expectedJsonPath))
            {
                data.Add(path);
            }
        }

        return data;
    }

    internal static JsonNode? ReadJsonFixture(string molPath, string mode)
    {
        var jsonPath = Path.ChangeExtension(molPath, $"{mode}.json");
        return JsonNode.Parse(File.ReadAllText(jsonPath));
    }

    internal static string GetFixturesRoot()
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures");
    }

    internal static void AssertNodesEqual(JsonNode? expected, JsonNode? actual)
    {
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Expected:\n{expected}\n\nActual:\n{actual}");
    }
}
