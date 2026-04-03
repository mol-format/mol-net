using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Xunit;

namespace Mol.Format.Tests;

public sealed class SerializerTests
{
    public static TheoryData<string> IdentityFixtures => FixtureTests.IdentityFixtures;

    [Theory]
    [MemberData(nameof(IdentityFixtures))]
    public void Serialize_round_trips_identity_fixture_json(string molPath)
    {
        var expected = FixtureTests.ReadJsonFixture(molPath, "identity");
        var mol = MolSerializer.Serialize(expected);
        var actual = MolSerializer.DeserializeToNode(mol);

        FixtureTests.AssertNodesEqual(expected, actual);
    }

    [Fact]
    public void Deserialize_can_bind_to_typed_models()
    {
        var mol = """
            Id: 10
            Username: "janedoe"
            Active: true
            Password:
                Hash: "abc123"
                Version: 19
            Role:
                Name: admin
            Role:
                Name: author
            """;

        var model = MolSerializer.Deserialize<AccountRecord>(mol);

        Assert.NotNull(model);
        Assert.Equal(10, model!.Id);
        Assert.Equal("janedoe", model.Username);
        Assert.True(model.Active);
        Assert.Equal("abc123", model.Password.Hash);
        Assert.Equal(19, model.Password.Version);
        Assert.Equal(["admin", "author"], model.Role.Select(static role => role.Name).ToArray());
    }

    [Fact]
    public void Serialize_can_inline_value_objects_using_value_property_name()
    {
        var node = new JsonObject
        {
            ["Property"] = new JsonObject
            {
                ["$value"] = "length",
                ["Type"] = "number",
                ["Enabled"] = true,
            },
        };

        var mol = MolSerializer.Serialize(node);
        var roundTrip = MolSerializer.DeserializeToNode(mol);

        FixtureTests.AssertNodesEqual(node, roundTrip);
    }

    public sealed class AccountRecord
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public bool Active { get; set; }

        public PasswordRecord Password { get; set; } = new();

        [JsonPropertyName("Role")]
        public List<RoleRecord> Role { get; set; } = [];
    }

    public sealed class PasswordRecord
    {
        public string Hash { get; set; } = string.Empty;

        public int Version { get; set; }
    }

    public sealed class RoleRecord
    {
        public string Name { get; set; } = string.Empty;
    }
}
