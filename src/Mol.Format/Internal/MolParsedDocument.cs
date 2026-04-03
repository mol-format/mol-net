namespace Mol.Format.Internal;

internal sealed class MolParsedDocument
{
    public required List<MolSyntaxEntry> Entries { get; init; }

    public string? RootScalar { get; init; }

    public bool RootScalarWasQuoted { get; init; }
}
