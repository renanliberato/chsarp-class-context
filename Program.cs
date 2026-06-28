using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CommandLine;
using System.Text.Json;

namespace ClassContextAnalyzer;

class AnalyzerProgram
{
    static async Task<int> AnalyzerMain(string[] args)
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

        var formatOption = new Option<string>(
            name: "--format",
            description: "Output format: markdown, csharp-project, or json",
            getDefaultValue: () => "markdown");

        var optimizeOption = new Option<bool>(
            name: "--optimize",
            description: "Use optimized analyzer with source index (recommended for large projects)",
            getDefaultValue: () => true);

        formatOption.AddValidator(result =>
        {
            var format = result.GetValueOrDefault<string>();
            if (format != "markdown" && format != "csharp-project" && format != "json")
            {
                result.ErrorMessage = "Format must be either 'markdown' or 'csharp-project'";
            }
        });

        var rootCommand = new RootCommand("C# Class Context Analyzer - Find all dependencies and references");
        rootCommand.AddOption(fileOption);
        rootCommand.AddOption(rootDirOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(optimizeOption);

        rootCommand.SetHandler(async (file, rootDir, output, format, optimize) =>
        {
            try
            {
                IClassContextAnalyzer analyzer = optimize
                    ? new ClassContextAnalyzerWithSourceIndex()
                    : new ClassContextAnalyzer();

                Console.WriteLine($"Using {(optimize ? "optimized" : "original")} analyzer...");
                var result = await analyzer.AnalyzeAsync(file.FullName, rootDir.FullName);

                if (format == "markdown")
                {
                    var generator = new MarkdownGenerator();
                    await generator.GenerateAsync(result, output.FullName);
                }
                else if (format == "csharp-project")
                {
                    var generator = new CSharpProjectGenerator();
                    await generator.GenerateAsync(result, output.FullName);
                }
                else if (format == "json")
                {
                    var generator = new JsonResultGenerator();
                    await generator.GenerateAsync(result, output.FullName);
                }
                Console.WriteLine($"Analysis complete. Output written to {output.FullName}");
                Console.WriteLine($"Analyzed {result.SourceFiles.Count} files with {result.TypeReferences.Count} type references");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, fileOption, rootDirOption, outputOption, formatOption, optimizeOption);

        return await rootCommand.InvokeAsync(args);
    }
}

#region Analysis Models

public class ClassContextAnalysisResult
{
    public Dictionary<string, SourceFileInfo> SourceFiles { get; set; } = new();
    public Dictionary<string, HashSet<string>> TypeReferences { get; set; } = new();
    public Dictionary<string, HashSet<string>> DependencyRelationships { get; set; } = new();
    public List<AnalysisDiagnostic> Diagnostics { get; set; } = new();
    public ProjectMetadata? ProjectMetadata { get; set; }
}

public class SourceFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public HashSet<string> DefinedTypes { get; set; } = new();
    public string Reason { get; set; } = "extracted_class_dependency";
}

public class AnalysisDiagnostic
{
    public string FilePath { get; set; } = string.Empty;
    public DiagnosticSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
}

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

#endregion

#region Analyzer Interface and Implementation

public interface IClassContextAnalyzer
{
    Task<ClassContextAnalysisResult> AnalyzeAsync(string filePath, string rootDirectory);
}

public class ClassContextAnalyzer : IClassContextAnalyzer
{
    private readonly HashSet<string> _processedFiles = new();
    private readonly Dictionary<string, string> _fileContents = new();
    private readonly Dictionary<string, HashSet<string>> _typeReferences = new();
    private readonly Dictionary<string, HashSet<string>> _dependencyRelationships = new();
    private readonly List<AnalysisDiagnostic> _diagnostics = new();
    private readonly IProjectMetadataExtractor _metadataExtractor;
    private readonly HashSet<string> _filesFromProjectReferences = new();

    public ClassContextAnalyzer() : this(new ProjectMetadataExtractor())
    {
    }

    public ClassContextAnalyzer(IProjectMetadataExtractor metadataExtractor)
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

        // Collect all directories to search (including ProjectReferences)
        var directoriesToSearch = new List<string> { rootDirectory };
        
        if (projectMetadata != null && projectMetadata.ProjectReferences.Count > 0)
        {
            _diagnostics.Add(new AnalysisDiagnostic
            {
                FilePath = csprojPath ?? filePath,
                Severity = DiagnosticSeverity.Info,
                Message = $"Found {projectMetadata.ProjectReferences.Count} ProjectReference(s), will flatten types from referenced projects"
            });

            // Resolve ProjectReference paths and add them to search
            foreach (var projectRef in projectMetadata.ProjectReferences)
            {
                var resolvedPath = ResolveProjectReferencePath(projectRef.Include, csprojPath ?? filePath);
                if (resolvedPath != null && Directory.Exists(resolvedPath))
                {
                    directoriesToSearch.Add(resolvedPath);
                    _diagnostics.Add(new AnalysisDiagnostic
                    {
                        FilePath = csprojPath ?? filePath,
                        Severity = DiagnosticSeverity.Info,
                        Message = $"Adding ProjectReference directory to search: {resolvedPath}"
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

        // Start with initial file
        await AnalyzeFileRecursive(filePath, rootDirectory, directoriesToSearch);

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

    private async Task AnalyzeFileRecursive(string filePath, string rootDirectory, List<string>? directoriesToSearch = null)
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

        // Build dependency relationships
        foreach (var referencedType in walker.ReferencedTypes)
        {
            if (!_dependencyRelationships.ContainsKey(filePath))
                _dependencyRelationships[filePath] = new HashSet<string>();

            _dependencyRelationships[filePath].Add(referencedType);
        }

        // For each referenced type, try to find its definition file
        foreach (var typeName in walker.ReferencedTypes)
        {
            await FindAndAnalyzeTypeDefinition(typeName, rootDirectory, directoriesToSearch);
        }
    }

    private async Task FindAndAnalyzeTypeDefinition(string typeName, string searchDirectory, List<string>? directoriesToSearch = null)
    {
        // Use provided directories or default to just searchDirectory
        var searchDirs = directoriesToSearch ?? new List<string> { searchDirectory };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            // Search for files that might contain this type definition
            var candidateFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
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
                        // Check if this file is from a ProjectReference (different directory)
                        var fileDir = Path.GetDirectoryName(candidateFile);
                        var isFromProjectReference = !string.IsNullOrEmpty(fileDir) && 
                                                      !Path.GetFullPath(fileDir).StartsWith(Path.GetFullPath(searchDirectory), StringComparison.OrdinalIgnoreCase);

                        if (isFromProjectReference)
                        {
                            _filesFromProjectReferences.Add(candidateFile);
                            _diagnostics.Add(new AnalysisDiagnostic
                            {
                                FilePath = candidateFile,
                                Severity = DiagnosticSeverity.Info,
                                Message = $"Including file from ProjectReference: {typeName} defined in {candidateFile}"
                            });
                        }

                        await AnalyzeFileRecursive(candidateFile, searchDirectory, directoriesToSearch);
                        return; // Found the type, no need to search more
                    }
                }
                catch (Exception ex)
                {
                    _diagnostics.Add(new AnalysisDiagnostic
                    {
                        FilePath = candidateFile,
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"Error analyzing file: {ex.Message}"
                    });
                }
            }
        }
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
}

#endregion

#region JSON Result Generator

public interface IJsonGenerator
{
    Task GenerateAsync(ClassContextAnalysisResult result, string outputPath);
}

public class JsonResultGenerator : IJsonGenerator
{
    public async Task GenerateAsync(ClassContextAnalysisResult result, string outputPath)
    {
        var files = result.SourceFiles.Select(kvp => new
        {
            sourcePath = kvp.Value.FilePath,
            bundlePath = GenerateBundlePath(kvp.Value.FilePath),
            reason = kvp.Value.Reason
        }).ToList();

        var compileIncludes = result.SourceFiles.Keys.Select(GenerateBundlePath).ToList();

        // Build project object
        object projectObject;

        // Add project metadata if available
        if (result.ProjectMetadata != null)
        {
            var metadata = result.ProjectMetadata;

            // Create properties dictionary
            var properties = new Dictionary<string, string?>();
            if (!string.IsNullOrEmpty(metadata.Nullable))
                properties["Nullable"] = metadata.Nullable;
            if (!string.IsNullOrEmpty(metadata.ImplicitUsings))
                properties["ImplicitUsings"] = metadata.ImplicitUsings;
            if (!string.IsNullOrEmpty(metadata.LangVersion))
                properties["LangVersion"] = metadata.LangVersion;

            // Build extended project object using dictionary for flexibility
            var extendedProject = new Dictionary<string, object>
            {
                ["compileIncludes"] = compileIncludes
            };

            if (!string.IsNullOrEmpty(metadata.TargetFramework))
                extendedProject["targetFramework"] = metadata.TargetFramework;

            if (metadata.TargetFrameworks.Count > 0)
                extendedProject["targetFrameworks"] = metadata.TargetFrameworks;

            if (properties.Count > 0)
                extendedProject["properties"] = properties;

            if (metadata.PackageReferences.Count > 0)
                extendedProject["packageReferences"] = metadata.PackageReferences.Select(pr => new
                {
                    include = pr.Include,
                    version = pr.Version
                });

            if (metadata.FrameworkReferences.Count > 0)
                extendedProject["frameworkReferences"] = metadata.FrameworkReferences.Select(fr => new
                {
                    include = fr.Include
                });

            if (metadata.ProjectReferences.Count > 0)
                extendedProject["projectReferences"] = metadata.ProjectReferences.Select(pr => new
                {
                    include = pr.Include
                });

            if (metadata.Diagnostics.Count > 0)
                extendedProject["diagnostics"] = metadata.Diagnostics.Select(d => new
                {
                    filePath = d.FilePath,
                    severity = d.Severity.ToString(),
                    message = d.Message,
                    lineNumber = d.LineNumber,
                    columnNumber = d.ColumnNumber
                });

            projectObject = extendedProject;
        }
        else
        {
            projectObject = new
            {
                compileIncludes = compileIncludes
            };
        }

        var jsonResult = new
        {
            files = files,
            project = projectObject,
            diagnostics = result.Diagnostics.Select(d => new
            {
                filePath = d.FilePath,
                severity = d.Severity.ToString(),
                message = d.Message,
                lineNumber = d.LineNumber,
                columnNumber = d.ColumnNumber
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(jsonResult, options);
        await File.WriteAllTextAsync(outputPath, json);
    }

    private string GenerateBundlePath(string sourcePath)
    {
        // Create a deterministic bundle path based on the source file name
        var fileName = Path.GetFileName(sourcePath);
        return Path.Combine("bundle", fileName);
    }
}

#endregion

#region Markdown Generator

public interface IMarkdownGenerator
{
    Task GenerateAsync(ClassContextAnalysisResult result, string outputPath);
}

public class MarkdownGenerator : IMarkdownGenerator
{
    public async Task GenerateAsync(ClassContextAnalysisResult result, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);

        foreach (var kvp in result.SourceFiles.OrderBy(x => x.Key))
        {
            await writer.WriteLineAsync($"// {kvp.Key}");
            await writer.WriteLineAsync("```csharp");
            await writer.WriteLineAsync(kvp.Value.Content);
            await writer.WriteLineAsync("```");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync();
        }
    }
}

#endregion

#region C# Project Generator

public interface IProjectGenerator
{
    Task GenerateAsync(ClassContextAnalysisResult result, string outputPath);
}

public class CSharpProjectGenerator : IProjectGenerator
{
    public async Task GenerateAsync(ClassContextAnalysisResult result, string outputPath)
    {
        // Create output directory if it doesn't exist
        var outputDir = new DirectoryInfo(outputPath);
        if (!outputDir.Exists)
        {
            outputDir.Create();
        }

        // Write all .cs files
        foreach (var kvp in result.SourceFiles)
        {
            var originalFileName = Path.GetFileName(kvp.Key);
            var outputFilePath = Path.Combine(outputPath, originalFileName);
            await File.WriteAllTextAsync(outputFilePath, kvp.Value.Content);
        }

        // Generate and write .csproj file
        var csprojContent = GenerateCsprojContent(result);
        var csprojPath = Path.Combine(outputPath, "ExtractedClasses.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);
    }

    private string GenerateCsprojContent(ClassContextAnalysisResult result)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine();
        sb.AppendLine("  <PropertyGroup>");
        
        // Use TargetFramework from metadata or default to net9.0
        var targetFramework = "net9.0";
        if (result.ProjectMetadata != null && !string.IsNullOrEmpty(result.ProjectMetadata.TargetFramework))
        {
            targetFramework = result.ProjectMetadata.TargetFramework;
        }
        sb.AppendLine($"    <TargetFramework>{targetFramework}</TargetFramework>");
        
        // Add other properties from metadata if available
        if (result.ProjectMetadata != null)
        {
            if (!string.IsNullOrEmpty(result.ProjectMetadata.Nullable))
            {
                sb.AppendLine($"    <Nullable>{result.ProjectMetadata.Nullable}</Nullable>");
            }
            else
            {
                sb.AppendLine("    <Nullable>enable</Nullable>");
            }
            
            if (!string.IsNullOrEmpty(result.ProjectMetadata.ImplicitUsings))
            {
                sb.AppendLine($"    <ImplicitUsings>{result.ProjectMetadata.ImplicitUsings}</ImplicitUsings>");
            }
            else
            {
                sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            }
            
            if (!string.IsNullOrEmpty(result.ProjectMetadata.LangVersion))
            {
                sb.AppendLine($"    <LangVersion>{result.ProjectMetadata.LangVersion}</LangVersion>");
            }
        }
        else
        {
            sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
            sb.AppendLine("    <Nullable>enable</Nullable>");
        }
        
        // Disable default compile items to use explicit compile includes
        sb.AppendLine("    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine();
        
        // Add PackageReferences if available
        if (result.ProjectMetadata != null && result.ProjectMetadata.PackageReferences.Count > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var packageRef in result.ProjectMetadata.PackageReferences)
            {
                if (!string.IsNullOrEmpty(packageRef.Version))
                {
                    sb.AppendLine($"    <PackageReference Include=\"{packageRef.Include}\" Version=\"{packageRef.Version}\" />");
                }
                else
                {
                    sb.AppendLine($"    <PackageReference Include=\"{packageRef.Include}\" />");
                }
            }
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();
        }
        
        // Add FrameworkReferences if available
        if (result.ProjectMetadata != null && result.ProjectMetadata.FrameworkReferences.Count > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var frameworkRef in result.ProjectMetadata.FrameworkReferences)
            {
                sb.AppendLine($"    <FrameworkReference Include=\"{frameworkRef.Include}\" />");
            }
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();
        }
        
        // Add explicit Compile items for each extracted file
        if (result.SourceFiles.Count > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var sourceFile in result.SourceFiles.Keys)
            {
                var fileName = Path.GetFileName(sourceFile);
                sb.AppendLine($"    <Compile Include=\"{fileName}\" />");
            }
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();
        }
        
        sb.AppendLine("</Project>");
        
        return sb.ToString();
    }
}

#endregion

#region Project Metadata Extraction

public interface IProjectMetadataExtractor
{
    string? FindProjectFile(string filePath);
    ProjectMetadata? ExtractMetadata(string csprojPath);
}

public class ProjectMetadataExtractor : IProjectMetadataExtractor
{
    public string? FindProjectFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        // Search upward from the file's directory for .csproj files
        var currentDir = new DirectoryInfo(directory);
        while (currentDir != null)
        {
            var csprojFiles = currentDir.GetFiles("*.csproj");
            if (csprojFiles.Length > 0)
            {
                // Return the first .csproj found
                return csprojFiles[0].FullName;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    public ProjectMetadata? ExtractMetadata(string csprojPath)
    {
        if (!File.Exists(csprojPath))
        {
            return null;
        }

        var metadata = new ProjectMetadata
        {
            ProjectPath = csprojPath
        };

        try
        {
            var content = File.ReadAllText(csprojPath);
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(content);

            var namespaceManager = new System.Xml.XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

            // Extract TargetFramework or TargetFrameworks (try without namespace first for SDK-style projects)
            var targetFrameworkNode = doc.SelectSingleNode("//TargetFramework") ??
                                     doc.SelectSingleNode("//ms:TargetFramework", namespaceManager);
            if (targetFrameworkNode != null)
            {
                metadata.TargetFramework = targetFrameworkNode.InnerText;
            }

            var targetFrameworksNode = doc.SelectSingleNode("//TargetFrameworks") ??
                                        doc.SelectSingleNode("//ms:TargetFrameworks", namespaceManager);
            if (targetFrameworksNode != null && string.IsNullOrEmpty(metadata.TargetFramework))
            {
                metadata.TargetFrameworks = targetFrameworksNode.InnerText
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }

            // Extract other properties
            var langVersionNode = doc.SelectSingleNode("//LangVersion") ??
                                   doc.SelectSingleNode("//ms:LangVersion", namespaceManager);
            if (langVersionNode != null)
            {
                metadata.LangVersion = langVersionNode.InnerText;
            }

            var nullableNode = doc.SelectSingleNode("//Nullable") ??
                                doc.SelectSingleNode("//ms:Nullable", namespaceManager);
            if (nullableNode != null)
            {
                metadata.Nullable = nullableNode.InnerText;
            }

            var implicitUsingsNode = doc.SelectSingleNode("//ImplicitUsings") ??
                                      doc.SelectSingleNode("//ms:ImplicitUsings", namespaceManager);
            if (implicitUsingsNode != null)
            {
                metadata.ImplicitUsings = implicitUsingsNode.InnerText;
            }

            // Extract PackageReference items
            var packageReferences = doc.SelectNodes("//PackageReference") ??
                                     doc.SelectNodes("//ms:PackageReference", namespaceManager);
            if (packageReferences != null)
            {
                foreach (System.Xml.XmlNode packageRef in packageReferences)
                {
                    var include = packageRef.Attributes?.GetNamedItem("Include")?.Value;
                    var version = packageRef.Attributes?.GetNamedItem("Version")?.Value;
                    if (!string.IsNullOrEmpty(include))
                    {
                        metadata.PackageReferences.Add(new PackageReference
                        {
                            Include = include,
                            Version = version
                        });
                    }
                }
            }

            // Extract FrameworkReference items
            var frameworkReferences = doc.SelectNodes("//FrameworkReference") ??
                                       doc.SelectNodes("//ms:FrameworkReference", namespaceManager);
            if (frameworkReferences != null)
            {
                foreach (System.Xml.XmlNode frameworkRef in frameworkReferences)
                {
                    var include = frameworkRef.Attributes?.GetNamedItem("Include")?.Value;
                    if (!string.IsNullOrEmpty(include))
                    {
                        metadata.FrameworkReferences.Add(new FrameworkReference
                        {
                            Include = include
                        });
                    }
                }
            }

            // Extract ProjectReference items
            var projectReferences = doc.SelectNodes("//ProjectReference") ??
                                     doc.SelectNodes("//ms:ProjectReference", namespaceManager);
            if (projectReferences != null)
            {
                foreach (System.Xml.XmlNode projectRef in projectReferences)
                {
                    var include = projectRef.Attributes?.GetNamedItem("Include")?.Value;
                    if (!string.IsNullOrEmpty(include))
                    {
                        metadata.ProjectReferences.Add(new ProjectReference
                        {
                            Include = include
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If parsing fails, return what we have so far
            metadata.Diagnostics.Add(new AnalysisDiagnostic
            {
                FilePath = csprojPath,
                Severity = DiagnosticSeverity.Warning,
                Message = $"Failed to parse project file: {ex.Message}"
            });
        }

        return metadata;
    }
}

public class ProjectMetadata
{
    public string ProjectPath { get; set; } = string.Empty;
    public string? TargetFramework { get; set; }
    public List<string> TargetFrameworks { get; set; } = new();
    public string? LangVersion { get; set; }
    public string? Nullable { get; set; }
    public string? ImplicitUsings { get; set; }
    public List<PackageReference> PackageReferences { get; set; } = new();
    public List<FrameworkReference> FrameworkReferences { get; set; } = new();
    public List<ProjectReference> ProjectReferences { get; set; } = new();
    public List<AnalysisDiagnostic> Diagnostics { get; set; } = new();
}

public class PackageReference
{
    public string Include { get; set; } = string.Empty;
    public string? Version { get; set; }
}

public class FrameworkReference
{
    public string Include { get; set; } = string.Empty;
}

public class ProjectReference
{
    public string Include { get; set; } = string.Empty;
}

#endregion

#region Legacy DependencyAnalyzer (for backward compatibility)

public class DependencyAnalyzer
{
    private readonly ClassContextAnalyzer _analyzer = new();
    private readonly MarkdownGenerator _generator = new();

    public async Task AnalyzeAndGenerateMarkdown(string filePath, string rootDirectory, string outputPath)
    {
        var result = await _analyzer.AnalyzeAsync(filePath, rootDirectory);
        await _generator.GenerateAsync(result, outputPath);
    }
}

#endregion

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
        if (node.ReturnType != null)
        {
            AddTypeReference(node.ReturnType);
        }

        // Add parameter type references
        foreach (var param in node.ParameterList.Parameters)
        {
            if (param.Type != null)
            {
                AddTypeReference(param.Type);
            }
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
            if (param.Type != null)
            {
                AddTypeReference(param.Type);
            }
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

public class TypeDefinitionCollector : CSharpSyntaxWalker
{
    public HashSet<string> DefinedTypes { get; } = new();

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        DefinedTypes.Add(node.Identifier.ValueText);
        base.VisitClassDeclaration(node);
    }

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        DefinedTypes.Add(node.Identifier.ValueText);
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        DefinedTypes.Add(node.Identifier.ValueText);
        base.VisitStructDeclaration(node);
    }

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        DefinedTypes.Add(node.Identifier.ValueText);
        base.VisitEnumDeclaration(node);
    }

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        DefinedTypes.Add(node.Identifier.ValueText);
        base.VisitRecordDeclaration(node);
    }
}

// Provide a way to run the analyzer from the benchmark
public static class AnalyzerLauncher
{
    public static async Task<int> Run(string[] args)
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

        var formatOption = new Option<string>(
            name: "--format",
            description: "Output format: markdown, csharp-project, or json",
            getDefaultValue: () => "markdown");

        var optimizeOption = new Option<bool>(
            name: "--optimize",
            description: "Use optimized analyzer with source index (recommended for large projects)",
            getDefaultValue: () => true);

        formatOption.AddValidator(result =>
        {
            var format = result.GetValueOrDefault<string>();
            if (format != "markdown" && format != "csharp-project" && format != "json")
            {
                result.ErrorMessage = "Format must be either 'markdown' or 'csharp-project'";
            }
        });

        var rootCommand = new RootCommand("C# Class Context Analyzer - Find all dependencies and references");
        rootCommand.AddOption(fileOption);
        rootCommand.AddOption(rootDirOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(optimizeOption);

        rootCommand.SetHandler(async (file, rootDir, output, format, optimize) =>
        {
            try
            {
                IClassContextAnalyzer analyzer = optimize
                    ? new ClassContextAnalyzerWithSourceIndex()
                    : new ClassContextAnalyzer();

                Console.WriteLine($"Using {(optimize ? "optimized" : "original")} analyzer...");
                var result = await analyzer.AnalyzeAsync(file.FullName, rootDir.FullName);

                if (format == "markdown")
                {
                    var generator = new MarkdownGenerator();
                    await generator.GenerateAsync(result, output.FullName);
                }
                else if (format == "csharp-project")
                {
                    var generator = new CSharpProjectGenerator();
                    await generator.GenerateAsync(result, output.FullName);
                }
                else if (format == "json")
                {
                    var generator = new JsonResultGenerator();
                    await generator.GenerateAsync(result, output.FullName);
                }

                Console.WriteLine($"Analysis complete. Output written to {output.FullName}");
                Console.WriteLine($"Analyzed {result.SourceFiles.Count} files with {result.TypeReferences.Count} type references");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, fileOption, rootDirOption, outputOption, formatOption, optimizeOption);

        return await rootCommand.InvokeAsync(args);
    }
}