using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ClassContextAnalyzer;

public class ClassContextAnalyzerWithSourceIndex : IClassContextAnalyzer
{
    private readonly HashSet<string> _processedFiles = new();
    private readonly Dictionary<string, string> _fileContents = new();
    private readonly Dictionary<string, HashSet<string>> _typeReferences = new();
    private readonly Dictionary<string, HashSet<string>> _dependencyRelationships = new();
    private readonly List<AnalysisDiagnostic> _diagnostics = new();

    // Source index: maps type names to their file paths
    private readonly Dictionary<string, List<string>> _typeToFilesIndex = new();
    private bool _indexBuilt = false;

    public async Task<ClassContextAnalysisResult> AnalyzeAsync(string filePath, string rootDirectory)
    {
        if (!File.Exists(filePath))
        {
            _diagnostics.Add(new AnalysisDiagnostic
            {
                FilePath = filePath,
                Severity = DiagnosticSeverity.Error,
                Message = $"File not found: {filePath}"
            });
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        // Build the source index first (single pass through all files)
        BuildSourceIndex(rootDirectory);

        // Start with initial file
        await AnalyzeFileRecursive(filePath, rootDirectory);

        // Build result
        return new ClassContextAnalysisResult
        {
            SourceFiles = _fileContents.ToDictionary(
                kvp => kvp.Key,
                kvp => new SourceFileInfo
                {
                    FilePath = kvp.Key,
                    Content = kvp.Value,
                    LastModified = File.GetLastWriteTime(kvp.Key),
                    DefinedTypes = ExtractDefinedTypes(kvp.Value)
                }
            ),
            TypeReferences = new Dictionary<string, HashSet<string>>(_typeReferences),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(_dependencyRelationships),
            Diagnostics = new List<AnalysisDiagnostic>(_diagnostics)
        };
    }

    private void BuildSourceIndex(string rootDirectory)
    {
        if (_indexBuilt)
            return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Get all .cs files once
        var allFiles = Directory.GetFiles(rootDirectory, "*.cs", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(content);
                var root = tree.GetCompilationUnitRoot();

                // Collect all type definitions from this file
                var typeCollector = new TypeDefinitionCollector();
                typeCollector.Visit(root);

                // Index each type definition
                foreach (var typeName in typeCollector.DefinedTypes)
                {
                    if (!_typeToFilesIndex.ContainsKey(typeName))
                    {
                        _typeToFilesIndex[typeName] = new List<string>();
                    }
                    _typeToFilesIndex[typeName].Add(file);
                }
            }
            catch (Exception ex)
            {
                _diagnostics.Add(new AnalysisDiagnostic
                {
                    FilePath = file,
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"Error indexing file: {ex.Message}"
                });
            }
        }

        _indexBuilt = true;
        stopwatch.Stop();

        _diagnostics.Add(new AnalysisDiagnostic
        {
            FilePath = rootDirectory,
            Severity = DiagnosticSeverity.Info,
            Message = $"Source index built: {_typeToFilesIndex.Count} types indexed from {allFiles.Length} files in {stopwatch.ElapsedMilliseconds}ms"
        });
    }

    private async Task AnalyzeFileRecursive(string filePath, string rootDirectory)
    {
        if (_processedFiles.Contains(filePath))
            return;

        _processedFiles.Add(filePath);

        // Read file if not already in cache
        if (!_fileContents.ContainsKey(filePath))
        {
            _fileContents[filePath] = await File.ReadAllTextAsync(filePath);
        }

        var tree = CSharpSyntaxTree.ParseText(_fileContents[filePath]);
        var root = tree.GetCompilationUnitRoot();

        // Find all type references in this file
        var walker = new TypeReferenceWalker(filePath);
        walker.Visit(root);

        _typeReferences[filePath] = walker.ReferencedTypes;

        // Build dependency relationships
        foreach (var referencedType in walker.ReferencedTypes)
        {
            if (!_dependencyRelationships.ContainsKey(filePath))
                _dependencyRelationships[filePath] = new HashSet<string>();

            _dependencyRelationships[filePath].Add(referencedType);
        }

        // For each referenced type, find its definition file using the index
        foreach (var typeName in walker.ReferencedTypes)
        {
            await FindAndAnalyzeTypeDefinitionUsingIndex(typeName, rootDirectory);
        }
    }

    private async Task FindAndAnalyzeTypeDefinitionUsingIndex(string typeName, string rootDirectory)
    {
        // Use the source index for O(1) lookup instead of O(n) search
        if (_typeToFilesIndex.TryGetValue(typeName, out var filePaths))
        {
            foreach (var filePath in filePaths)
            {
                // Only analyze if not already processed
                if (!_processedFiles.Contains(filePath))
                {
                    await AnalyzeFileRecursive(filePath, rootDirectory);
                }
            }
        }
        // If type not found in index, it might be a built-in type or from external library
        // No need to search through files - we already know it's not in our codebase
    }

    private HashSet<string> ExtractDefinedTypes(string fileContent)
    {
        var definedTypes = new HashSet<string>();
        var tree = CSharpSyntaxTree.ParseText(fileContent);
        var root = tree.GetCompilationUnitRoot();

        var typeCollector = new TypeDefinitionCollector();
        typeCollector.Visit(root);

        foreach (var type in typeCollector.DefinedTypes)
        {
            definedTypes.Add(type);
        }

        return definedTypes;
    }
}