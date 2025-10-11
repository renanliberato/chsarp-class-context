using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CommandLine;

namespace ClassContextAnalyzer;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var fileOption = new Option<FileInfo>(
            name: "--file",
            description: "The C# file to analyze")
        {
            IsRequired = true
        }.ExistingOnly();

        var outputOption = new Option<FileInfo>(
            name: "--output",
            description: "Output markdown file path",
            getDefaultValue: () => new FileInfo("output.md"));

        var rootDirOption = new Option<DirectoryInfo>(
            name: "--root-dir",
            description: "Root directory to search for dependency files",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var rootCommand = new RootCommand("C# Class Context Analyzer - Find all dependencies and references");
        rootCommand.AddOption(fileOption);
        rootCommand.AddOption(rootDirOption);
        rootCommand.AddOption(outputOption);

        rootCommand.SetHandler(async (file, rootDir, output) =>
        {
            try
            {
                var analyzer = new DependencyAnalyzer();
                await analyzer.AnalyzeAndGenerateMarkdown(file.FullName, rootDir.FullName, output.FullName);
                Console.WriteLine($"Analysis complete. Output written to {output.FullName}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, fileOption, rootDirOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }
}

public class DependencyAnalyzer
{
    private readonly HashSet<string> _processedFiles = new();
    private readonly Dictionary<string, string> _fileContents = new();
    private readonly Dictionary<string, HashSet<string>> _typeReferences = new();

    public async Task AnalyzeAndGenerateMarkdown(string filePath, string rootDirectory, string outputPath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        // Start with initial file
        await AnalyzeFileRecursive(filePath, rootDirectory);

        // Generate markdown output
        await GenerateMarkdownOutput(outputPath);
    }

    private async Task AnalyzeFileRecursive(string filePath, string rootDirectory)
    {
        if (_processedFiles.Contains(filePath))
            return;

        _processedFiles.Add(filePath);
        _fileContents[filePath] = await File.ReadAllTextAsync(filePath);

        var tree = CSharpSyntaxTree.ParseText(_fileContents[filePath]);
        var root = tree.GetCompilationUnitRoot();

        // Find all type references in this file
        var walker = new TypeReferenceWalker(filePath);
        walker.Visit(root);

        _typeReferences[filePath] = walker.ReferencedTypes;

        // For each referenced type, try to find its definition file
        foreach (var typeName in walker.ReferencedTypes)
        {
            await FindAndAnalyzeTypeDefinition(typeName, rootDirectory);
        }
    }

    private async Task FindAndAnalyzeTypeDefinition(string typeName, string searchDirectory)
    {
        // Search for files that might contain this type definition
        var candidateFiles = Directory.GetFiles(searchDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !_processedFiles.Contains(f))
            .ToList();

        foreach (var candidateFile in candidateFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(candidateFile);
                var tree = CSharpSyntaxTree.ParseText(content);
                var root = tree.GetCompilationUnitRoot();

                // Check if this file contains the type definition
                var typeWalker = new TypeDefinitionWalker(typeName);
                typeWalker.Visit(root);

                if (typeWalker.FoundType)
                {
                    await AnalyzeFileRecursive(candidateFile, searchDirectory);
                    break; // Found the type, no need to search more
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing {candidateFile}: {ex.Message}");
            }
        }
    }

    private async Task GenerateMarkdownOutput(string outputPath)
    {
        using var writer = new StreamWriter(outputPath);

        foreach (var kvp in _fileContents.OrderBy(x => x.Key))
        {
            await writer.WriteLineAsync($"// {kvp.Key}");
            await writer.WriteLineAsync("```csharp");
            await writer.WriteLineAsync(kvp.Value);
            await writer.WriteLineAsync("```");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync();
        }
    }
}

public class TypeReferenceWalker : CSharpSyntaxWalker
{
    public HashSet<string> ReferencedTypes { get; } = new();
    private readonly string _filePath;

    public TypeReferenceWalker(string filePath)
    {
        _filePath = filePath;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // Add base class references
        if (node.BaseList != null)
        {
            foreach (var baseType in node.BaseList.Types)
            {
                AddTypeReference(baseType.Type);
            }
        }

        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        // Add base interface references
        if (node.BaseList != null)
        {
            foreach (var baseType in node.BaseList.Types)
            {
                AddTypeReference(baseType.Type);
            }
        }

        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Add return type reference
        AddTypeReference(node.ReturnType);

        // Add parameter type references
        foreach (var param in node.ParameterList.Parameters)
        {
            AddTypeReference(param.Type);
        }

        base.VisitMethodDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        // Add property type reference
        AddTypeReference(node.Type);

        base.VisitPropertyDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        // Add field type reference
        AddTypeReference(node.Declaration.Type);

        base.VisitFieldDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        // Add parameter type references
        foreach (var param in node.ParameterList.Parameters)
        {
            AddTypeReference(param.Type);
        }

        base.VisitConstructorDeclaration(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        // Add generic type references
        AddTypeReference(node);

        base.VisitGenericName(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Check if this identifier might be a type reference
        // This is a simplified approach - in reality you'd need semantic analysis for accuracy
        var identifier = node.Identifier.ValueText;

        // Heuristic: capitalize first letter suggests it might be a type
        if (!string.IsNullOrEmpty(identifier) && char.IsUpper(identifier[0]) &&
            !IsKeyword(identifier) && !IsBuiltInType(identifier))
        {
            ReferencedTypes.Add(identifier);
        }

        base.VisitIdentifierName(node);
    }

    private void AddTypeReference(TypeSyntax type)
    {
        if (type is IdentifierNameSyntax identifierName)
        {
            var typeName = identifierName.Identifier.ValueText;
            if (!IsKeyword(typeName) && !IsBuiltInType(typeName))
            {
                ReferencedTypes.Add(typeName);
            }
        }
        else if (type is GenericNameSyntax genericName)
        {
            var typeName = genericName.Identifier.ValueText;
            if (!IsKeyword(typeName) && !IsBuiltInType(typeName))
            {
                ReferencedTypes.Add(typeName);
            }
        }
        else if (type is QualifiedNameSyntax qualifiedName)
        {
            // Get the rightmost part of the qualified name
            var parts = qualifiedName.ToString().Split('.');
            if (parts.Length > 0)
            {
                var typeName = parts.Last();
                if (!IsKeyword(typeName) && !IsBuiltInType(typeName))
                {
                    ReferencedTypes.Add(typeName);
                }
            }
        }
    }

    private static bool IsKeyword(string identifier)
    {
        var keywords = new[] { "int", "string", "bool", "double", "float", "char", "long", "short",
                              "byte", "uint", "ulong", "ushort", "sbyte", "decimal", "object", "void",
                              "var", "dynamic", "async", "await", "yield", "return", "if", "else",
                              "for", "foreach", "while", "do", "switch", "case", "default", "break",
                              "continue", "goto", "try", "catch", "finally", "throw", "new", "this",
                              "base", "null", "true", "false", "typeof", "sizeof", "nameof", "is",
                              "as", "from", "where", "select", "group", "into", "orderby", "join",
                              "let", "in", "on", "equals", "by", "ascending", "descending" };
        return keywords.Contains(identifier);
    }

    private static bool IsBuiltInType(string identifier)
    {
        var builtInTypes = new[] { "String", "Int32", "Boolean", "Double", "Single", "Char",
                                  "Int64", "Int16", "Byte", "UInt32", "UInt64", "UInt16",
                                  "SByte", "Decimal", "Object", "Void" };
        return builtInTypes.Contains(identifier);
    }
}

public class TypeDefinitionWalker : CSharpSyntaxWalker
{
    private readonly string _targetTypeName;
    public bool FoundType { get; private set; }

    public TypeDefinitionWalker(string targetTypeName)
    {
        _targetTypeName = targetTypeName;
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == _targetTypeName)
        {
            FoundType = true;
        }

        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == _targetTypeName)
        {
            FoundType = true;
        }

        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == _targetTypeName)
        {
            FoundType = true;
        }

        base.VisitStructDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        if (node.Identifier.ValueText == _targetTypeName)
        {
            FoundType = true;
        }

        base.VisitEnumDeclaration(node);
    }
}