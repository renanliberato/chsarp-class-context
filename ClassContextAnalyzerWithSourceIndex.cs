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
    private readonly IProjectMetadataExtractor _metadataExtractor;

    // Source index: maps type names to their file paths
    private readonly Dictionary<string, List<string>> _typeToFilesIndex = new();
    private bool _indexBuilt = false;
    
    // Track files from ProjectReferences to set appropriate reason
    private readonly HashSet<string> _filesFromProjectReferences = new();

    public ClassContextAnalyzerWithSourceIndex() : this(new ProjectMetadataExtractor())
    {
    }

    public ClassContextAnalyzerWithSourceIndex(IProjectMetadataExtractor metadataExtractor)
    {
        _metadataExtractor = metadataExtractor;
    }

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

        // Extract project metadata for the input file
        var csprojPath = _metadataExtractor.FindProjectFile(filePath);
        var projectMetadata = csprojPath != null ? _metadataExtractor.ExtractMetadata(csprojPath) : null;

        // Check for unsupported extraction cases before proceeding
        if (projectMetadata != null)
        {
            var errorDiagnostics = projectMetadata.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (errorDiagnostics.Any())
            {
                // Add error diagnostics to our collection
                _diagnostics.AddRange(errorDiagnostics);

                // Throw explicit exception for unsupported extraction cases
                var errorMessages = string.Join(
                    "; ",
                    errorDiagnostics.Select(d => d.Message));
                throw new InvalidOperationException(
                    $"Cannot safely extract project due to unsupported features: {errorMessages}");
            }
        }

        // Collect all directories to index (including ProjectReferences)
        var directoriesToIndex = new List<string> { rootDirectory };
        
        if (projectMetadata != null && projectMetadata.ProjectReferences.Count > 0)
        {
            var projectRefCount = projectMetadata.ProjectReferences.Count;
            _diagnostics.Add(new AnalysisDiagnostic
            {
                FilePath = csprojPath ?? filePath,
                Severity = DiagnosticSeverity.Info,
                Message = $"Found {projectRefCount} ProjectReference(s), will flatten types from referenced projects"
            });

            // Resolve ProjectReference paths and add them to index
            foreach (var projectRef in projectMetadata.ProjectReferences)
            {
                var resolvedPath = ResolveProjectReferencePath(projectRef.Include, csprojPath ?? filePath);
                if (resolvedPath != null && Directory.Exists(resolvedPath))
                {
                    directoriesToIndex.Add(resolvedPath);
                    _diagnostics.Add(new AnalysisDiagnostic
                    {
                        FilePath = csprojPath ?? filePath,
                        Severity = DiagnosticSeverity.Info,
                        Message = $"Adding ProjectReference directory to source index: {resolvedPath}"
                    });
                }
                else
                {
                    _diagnostics.Add(new AnalysisDiagnostic
                    {
                        FilePath = csprojPath ?? filePath,
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"Could not resolve ProjectReference path: {projectRef.Include}"
                    });
                }
            }
        }

        // Build the source index first (single pass through all directories)
        BuildSourceIndex(directoriesToIndex);

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
                    DefinedTypes = ExtractDefinedTypes(kvp.Value),
                    Reason = _filesFromProjectReferences.Contains(kvp.Key) 
                        ? "flattened_project_reference" 
                        : "extracted_class_dependency"
                }
            ),
            TypeReferences = new Dictionary<string, HashSet<string>>(_typeReferences),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(_dependencyRelationships),
            Diagnostics = new List<AnalysisDiagnostic>(_diagnostics),
            ProjectMetadata = projectMetadata
        };
    }

    private void BuildSourceIndex(List<string> rootDirectories)
    {
        if (_indexBuilt)
            return;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var totalFiles = 0;

        // Index files from all directories
        foreach (var rootDirectory in rootDirectories)
        {
            if (!Directory.Exists(rootDirectory))
                continue;

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
                    totalFiles++;
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
        }

        _indexBuilt = true;
        stopwatch.Stop();

        var typeCount = _typeToFilesIndex.Count;
        var dirCount = rootDirectories.Count;
        _diagnostics.Add(new AnalysisDiagnostic
        {
            FilePath = rootDirectories[0],
            Severity = DiagnosticSeverity.Info,
            Message = $"Source index built: {typeCount} types indexed " +
                $"from {totalFiles} files in {stopwatch.ElapsedMilliseconds}ms " +
                $"across {dirCount} directories"
        });
    }

    private string? ResolveProjectReferencePath(string projectReference, string referencingCsprojPath)
    {
        try
        {
            var referencingDir = Path.GetDirectoryName(referencingCsprojPath);
            if (string.IsNullOrEmpty(referencingDir))
                return null;

            // Resolve relative path
            var resolvedPath = Path.Combine(referencingDir, projectReference);
            
            // Normalize the path
            resolvedPath = Path.GetFullPath(resolvedPath);

            // If it's a .csproj file, get its directory
            if (resolvedPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = Path.GetDirectoryName(resolvedPath);
            }

            return resolvedPath;
        }
        catch
        {
            return null;
        }
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
                    // Check if this file is from a ProjectReference (different directory)
                    var fileDir = Path.GetDirectoryName(filePath);
                    var isFromProjectReference = !string.IsNullOrEmpty(fileDir) &&
                        !Path.GetFullPath(fileDir).StartsWith(
                            Path.GetFullPath(rootDirectory),
                            StringComparison.OrdinalIgnoreCase);

                    if (isFromProjectReference)
                    {
                        _filesFromProjectReferences.Add(filePath);
                        _diagnostics.Add(new AnalysisDiagnostic
                        {
                            FilePath = filePath,
                            Severity = DiagnosticSeverity.Info,
                            Message = $"Including file from ProjectReference: {typeName} defined in {filePath}"
                        });
                    }

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