# Mol.Format

`Mol.Format` provides a dependency-free Markdown Object Language serializer and deserializer for .NET.

```csharp
using Mol.Format;

var value = MolSerializer.Deserialize<AppConfig>(molText);
var mol = MolSerializer.Serialize(value);
```

The API follows `System.Text.Json` conventions:

- `MolSerializer.Deserialize<T>()`
- `MolSerializer.Serialize()`
- `MolSerializerOptions`
- key naming policies for `identity`, `camelCase`, `PascalCase`, `snake_case`, and `kebab-case`
