using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AreaStructureIndexTool;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0].Equals("self-test", StringComparison.OrdinalIgnoreCase))
            {
                RunSelfTest();
                Console.WriteLine("structure_index_csharp_self_test: pass");
                return 0;
            }

            if (args.Length == 7 &&
                args[0].Equals("index", StringComparison.OrdinalIgnoreCase) &&
                args[1] == "--project-root" &&
                args[3] == "--manifest" &&
                args[5] == "--output")
            {
                IndexFiles(args[2], args[4], args[6]);
                return 0;
            }

            throw new ArgumentException(
                "Usage: AreaStructureIndexTool index --project-root <path> --manifest <path> --output <path> | self-test");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"structure_index_csharp_error: {exception.Message}");
            return 1;
        }
    }

    private static void IndexFiles(string projectRoot, string manifestPath, string outputPath)
    {
        var paths = File.ReadAllLines(manifestPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entries = paths.Select(path => ParseFile(projectRoot, path)).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(entries, JsonOptions));
        Console.WriteLine($"structure_index_csharp_files: {entries.Length}");
    }

    private static CSharpFileEntry ParseFile(string projectRoot, string path)
    {
        var text = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(
            text,
            new CSharpParseOptions(LanguageVersion.Preview),
            path);
        var root = tree.GetCompilationUnitRoot();
        var fileInfo = new FileInfo(path);

        var types = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Select(CreateTypeEntry)
            .Concat(root.DescendantNodes().OfType<DelegateDeclarationSyntax>().Select(CreateDelegateEntry))
            .OrderBy(entry => entry.Line)
            .ToArray();

        var references = root.DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .GroupBy(node => node.Identifier.ValueText, StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .Select(group => new ReferenceEntry
            {
                Name = group.Key,
                FirstLine = GetLine(group.First()),
                Count = group.Count()
            })
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();

        var menuItems = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attribute => GetSimpleName(attribute.Name).Equals("MenuItem", StringComparison.Ordinal))
            .Select(attribute => new MenuItemEntry
            {
                Path = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                    ? literal.Token.ValueText
                    : attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression.ToString() ?? string.Empty,
                Line = GetLine(attribute)
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .OrderBy(entry => entry.Line)
            .ToArray();

        var diagnostics = tree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => new DiagnosticEntry
            {
                Line = diagnostic.Location.IsInSource
                    ? diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1
                    : 0,
                Message = diagnostic.GetMessage()
            })
            .Take(10)
            .ToArray();

        return new CSharpFileEntry
        {
            Path = NormalizeRelativePath(projectRoot, path),
            Language = "CSharp",
            Length = fileInfo.Length,
            LastWriteUtc = fileInfo.LastWriteTimeUtc.ToString("O"),
            Namespaces = root.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(node => node.Name.ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            Usings = root.Usings
                .Select(node => node.Name?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            Types = types,
            MenuItems = menuItems,
            References = references,
            ParseErrors = diagnostics
        };
    }

    private static TypeEntry CreateTypeEntry(BaseTypeDeclarationSyntax node)
    {
        var bases = node.BaseList?.Types.Select(type => type.Type.ToString()).ToArray() ?? [];
        var members = node switch
        {
            TypeDeclarationSyntax typeDeclaration => typeDeclaration.Members
                .SelectMany(CreateMemberEntries)
                .OrderBy(entry => entry.Line)
                .ToArray(),
            EnumDeclarationSyntax enumDeclaration => enumDeclaration.Members
                .Select(member => new MemberEntry
                {
                    Kind = "enum-member",
                    Name = member.Identifier.ValueText,
                    Signature = Normalize(member.ToString()),
                    Accessibility = "public",
                    Line = GetLine(member)
                })
                .ToArray(),
            _ => []
        };

        return new TypeEntry
        {
            Name = node.Identifier.ValueText,
            FullName = GetFullTypeName(node),
            Kind = GetTypeKind(node),
            Accessibility = GetAccessibility(node.Modifiers, node.Parent is BaseTypeDeclarationSyntax ? "private" : "internal"),
            Modifiers = node.Modifiers.Select(token => token.ValueText).ToArray(),
            Bases = bases,
            UnityKind = GetUnityKind(bases),
            Line = GetLine(node),
            Members = members
        };
    }

    private static TypeEntry CreateDelegateEntry(DelegateDeclarationSyntax node)
    {
        return new TypeEntry
        {
            Name = node.Identifier.ValueText,
            FullName = GetFullTypeName(node),
            Kind = "delegate",
            Accessibility = GetAccessibility(node.Modifiers, node.Parent is BaseTypeDeclarationSyntax ? "private" : "internal"),
            Modifiers = node.Modifiers.Select(token => token.ValueText).ToArray(),
            Bases = [],
            Line = GetLine(node),
            Members =
            [
                new MemberEntry
                {
                    Kind = "delegate-signature",
                    Name = node.Identifier.ValueText,
                    Signature = Normalize($"{node.ReturnType} {node.Identifier}{node.TypeParameterList}{node.ParameterList}"),
                    Accessibility = GetAccessibility(node.Modifiers, "internal"),
                    Line = GetLine(node)
                }
            ]
        };
    }

    private static IEnumerable<MemberEntry> CreateMemberEntries(MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case MethodDeclarationSyntax method:
                yield return new MemberEntry
                {
                    Kind = "method",
                    Name = method.Identifier.ValueText,
                    Signature = Normalize($"{method.Modifiers} {method.ReturnType} {method.ExplicitInterfaceSpecifier}{method.Identifier}{method.TypeParameterList}{method.ParameterList}"),
                    Accessibility = GetAccessibility(method.Modifiers, "private"),
                    Line = GetLine(method)
                };
                break;
            case ConstructorDeclarationSyntax constructor:
                yield return new MemberEntry
                {
                    Kind = "constructor",
                    Name = constructor.Identifier.ValueText,
                    Signature = Normalize($"{constructor.Modifiers} {constructor.Identifier}{constructor.ParameterList}"),
                    Accessibility = GetAccessibility(constructor.Modifiers, "private"),
                    Line = GetLine(constructor)
                };
                break;
            case PropertyDeclarationSyntax property:
                yield return new MemberEntry
                {
                    Kind = "property",
                    Name = property.Identifier.ValueText,
                    Signature = Normalize($"{property.Modifiers} {property.Type} {property.ExplicitInterfaceSpecifier}{property.Identifier} {GetAccessorSummary(property.AccessorList)}"),
                    Accessibility = GetAccessibility(property.Modifiers, "private"),
                    Line = GetLine(property)
                };
                break;
            case FieldDeclarationSyntax field:
                foreach (var variable in field.Declaration.Variables)
                {
                    yield return new MemberEntry
                    {
                        Kind = "field",
                        Name = variable.Identifier.ValueText,
                        Signature = Normalize($"{field.Modifiers} {field.Declaration.Type} {variable.Identifier}"),
                        Accessibility = GetAccessibility(field.Modifiers, "private"),
                        Serialized = HasAttribute(field.AttributeLists, "SerializeField") ||
                                     field.Modifiers.Any(SyntaxKind.PublicKeyword),
                        Line = GetLine(variable)
                    };
                }
                break;
            case EventFieldDeclarationSyntax eventField:
                foreach (var variable in eventField.Declaration.Variables)
                {
                    yield return new MemberEntry
                    {
                        Kind = "event",
                        Name = variable.Identifier.ValueText,
                        Signature = Normalize($"{eventField.Modifiers} event {eventField.Declaration.Type} {variable.Identifier}"),
                        Accessibility = GetAccessibility(eventField.Modifiers, "private"),
                        Line = GetLine(variable)
                    };
                }
                break;
            case EventDeclarationSyntax eventDeclaration:
                yield return new MemberEntry
                {
                    Kind = "event",
                    Name = eventDeclaration.Identifier.ValueText,
                    Signature = Normalize($"{eventDeclaration.Modifiers} event {eventDeclaration.Type} {eventDeclaration.Identifier}"),
                    Accessibility = GetAccessibility(eventDeclaration.Modifiers, "private"),
                    Line = GetLine(eventDeclaration)
                };
                break;
            case IndexerDeclarationSyntax indexer:
                yield return new MemberEntry
                {
                    Kind = "indexer",
                    Name = "this",
                    Signature = Normalize($"{indexer.Modifiers} {indexer.Type} this{indexer.ParameterList} {GetAccessorSummary(indexer.AccessorList)}"),
                    Accessibility = GetAccessibility(indexer.Modifiers, "private"),
                    Line = GetLine(indexer)
                };
                break;
            case OperatorDeclarationSyntax op:
                yield return new MemberEntry
                {
                    Kind = "operator",
                    Name = $"operator {op.OperatorToken.ValueText}",
                    Signature = Normalize($"{op.Modifiers} {op.ReturnType} operator {op.OperatorToken}{op.ParameterList}"),
                    Accessibility = GetAccessibility(op.Modifiers, "public"),
                    Line = GetLine(op)
                };
                break;
            case ConversionOperatorDeclarationSyntax conversion:
                yield return new MemberEntry
                {
                    Kind = "operator",
                    Name = $"operator {conversion.Type}",
                    Signature = Normalize($"{conversion.Modifiers} {conversion.ImplicitOrExplicitKeyword} operator {conversion.Type}{conversion.ParameterList}"),
                    Accessibility = GetAccessibility(conversion.Modifiers, "public"),
                    Line = GetLine(conversion)
                };
                break;
        }
    }

    private static string GetFullTypeName(SyntaxNode node)
    {
        var namespaceParts = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse()
            .Select(part => part.Name.ToString());
        var typeParts = node.Ancestors()
            .OfType<BaseTypeDeclarationSyntax>()
            .Reverse()
            .Select(part => part.Identifier.ValueText)
            .Concat(node switch
            {
                BaseTypeDeclarationSyntax type => [type.Identifier.ValueText],
                DelegateDeclarationSyntax declaration => [declaration.Identifier.ValueText],
                _ => []
            });
        return string.Join(".", namespaceParts.Concat(typeParts));
    }

    private static string GetTypeKind(BaseTypeDeclarationSyntax node) => node switch
    {
        ClassDeclarationSyntax => "class",
        StructDeclarationSyntax => "struct",
        InterfaceDeclarationSyntax => "interface",
        RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => "record-struct",
        RecordDeclarationSyntax => "record",
        EnumDeclarationSyntax => "enum",
        _ => node.Kind().ToString()
    };

    private static string? GetUnityKind(IEnumerable<string> bases)
    {
        foreach (var baseName in bases.Select(GetRightmostName))
        {
            if (baseName is "MonoBehaviour" or "ScriptableObject" or "Editor" or "EditorWindow")
            {
                return baseName;
            }
        }
        return null;
    }

    private static string GetRightmostName(string value)
    {
        var withoutGeneric = value.Split('<')[0];
        return withoutGeneric.Split('.').Last();
    }

    private static string GetAccessibility(SyntaxTokenList modifiers, string fallback)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword)) return "public";
        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword)) return "protected internal";
        if (modifiers.Any(SyntaxKind.PrivateKeyword) && modifiers.Any(SyntaxKind.ProtectedKeyword)) return "private protected";
        if (modifiers.Any(SyntaxKind.PrivateKeyword)) return "private";
        if (modifiers.Any(SyntaxKind.ProtectedKeyword)) return "protected";
        if (modifiers.Any(SyntaxKind.InternalKeyword)) return "internal";
        return fallback;
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string expectedName)
    {
        return lists.SelectMany(list => list.Attributes)
            .Any(attribute => GetSimpleName(attribute.Name).Equals(expectedName, StringComparison.Ordinal));
    }

    private static string GetSimpleName(NameSyntax name)
    {
        var text = name.ToString();
        var rightmost = text.Split('.').Last();
        return rightmost.EndsWith("Attribute", StringComparison.Ordinal)
            ? rightmost[..^"Attribute".Length]
            : rightmost;
    }

    private static string GetAccessorSummary(AccessorListSyntax? accessorList)
    {
        if (accessorList is null) return "=>";
        return "{ " + string.Join(" ", accessorList.Accessors.Select(accessor => $"{accessor.Keyword.ValueText};")) + " }";
    }

    private static int GetLine(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string Normalize(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeRelativePath(string projectRoot, string path) =>
        Path.GetRelativePath(Path.GetFullPath(projectRoot), Path.GetFullPath(path)).Replace('\\', '/');

    private static void RunSelfTest()
    {
        const string fixture = """
            namespace Demo;
            public sealed class Sample : MonoBehaviour
            {
                [SerializeField] private int count;
                public string Name { get; private set; }
                public void Run(int amount) { count += amount; }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(fixture, new CSharpParseOptions(LanguageVersion.Preview));
        var root = tree.GetCompilationUnitRoot();
        var type = CreateTypeEntry(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single());
        if (type.FullName != "Demo.Sample" ||
            type.UnityKind != "MonoBehaviour" ||
            type.Members.Single(member => member.Name == "count").Serialized != true ||
            type.Members.Single(member => member.Name == "Run").Signature != "public void Run(int amount)")
        {
            throw new InvalidOperationException("Roslyn structure extraction contract failed.");
        }
    }

    private sealed class CSharpFileEntry
    {
        public required string Path { get; init; }
        public required string Language { get; init; }
        public long Length { get; init; }
        public required string LastWriteUtc { get; init; }
        public required string[] Namespaces { get; init; }
        public required string[] Usings { get; init; }
        public required TypeEntry[] Types { get; init; }
        public required MenuItemEntry[] MenuItems { get; init; }
        public required ReferenceEntry[] References { get; init; }
        public required DiagnosticEntry[] ParseErrors { get; init; }
    }

    private sealed class TypeEntry
    {
        public required string Name { get; init; }
        public required string FullName { get; init; }
        public required string Kind { get; init; }
        public required string Accessibility { get; init; }
        public required string[] Modifiers { get; init; }
        public required string[] Bases { get; init; }
        public string? UnityKind { get; init; }
        public int Line { get; init; }
        public required MemberEntry[] Members { get; init; }
    }

    private sealed class MemberEntry
    {
        public required string Kind { get; init; }
        public required string Name { get; init; }
        public required string Signature { get; init; }
        public required string Accessibility { get; init; }
        public bool Serialized { get; init; }
        public int Line { get; init; }
    }

    private sealed class MenuItemEntry
    {
        public required string Path { get; init; }
        public int Line { get; init; }
    }

    private sealed class ReferenceEntry
    {
        public required string Name { get; init; }
        public int FirstLine { get; init; }
        public int Count { get; init; }
    }

    private sealed class DiagnosticEntry
    {
        public int Line { get; init; }
        public required string Message { get; init; }
    }
}
