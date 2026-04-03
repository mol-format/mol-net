namespace Mol.Format.Internal;

internal sealed class MolSyntaxEntry
{
    public MolSyntaxEntry(string key)
    {
        Key = key;
    }

    public string Key { get; }

    public bool IsRootHeading { get; init; }

    public string? InlineValue { get; set; }

    public bool InlineValueWasQuoted { get; set; }

    public string? TextBody { get; set; }

    public List<MolSyntaxEntry> Children { get; } = new();
}
