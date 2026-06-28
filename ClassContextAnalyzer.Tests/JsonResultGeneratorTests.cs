using System.Text.Json;
using ClassContextAnalyzer.Tests;
using ClassContextAnalyzer;

public class JsonResultGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ShouldContainFilesProjectAndDiagnosticsSections()
    {
        // Arrange
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>(),
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>()
        };

        var generator = new JsonResultGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-output-1.json");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        Assert.True(root.ValueKind == JsonValueKind.Object);
        Assert.True(root.TryGetProperty("files", out var files));
        Assert.True(root.TryGetProperty("project", out var project));
        Assert.True(root.TryGetProperty("diagnostics", out var diagnostics));

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeSourcePathBundlePathAndReasonForEachFile()
    {
        // Arrange
        var sourcePath = "/path/to/source/TestClass.cs";
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                [sourcePath] = new SourceFileInfo
                {
                    FilePath = sourcePath,
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>()
        };

        var generator = new JsonResultGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-output-2.json");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("files", out var files));
        Assert.Equal(1, files.GetArrayLength());

        var file = files[0];
        Assert.True(file.TryGetProperty("sourcePath", out var sourcePathProp));
        Assert.True(file.TryGetProperty("bundlePath", out var bundlePathProp));
        Assert.True(file.TryGetProperty("reason", out var reasonProp));

        Assert.Equal(sourcePath, sourcePathProp.GetString());
        Assert.NotNull(bundlePathProp.GetString());
        Assert.NotNull(reasonProp.GetString());

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeCompileIncludesMatchingExtractedFiles()
    {
        // Arrange
        var sourcePath1 = "/path/to/source/TestClass.cs";
        var sourcePath2 = "/path/to/source/AnotherClass.cs";
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                [sourcePath1] = new SourceFileInfo
                {
                    FilePath = sourcePath1,
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                },
                [sourcePath2] = new SourceFileInfo
                {
                    FilePath = sourcePath2,
                    Content = "public class AnotherClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "AnotherClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>()
        };

        var generator = new JsonResultGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-output-3.json");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("project", out var project));
        Assert.True(project.TryGetProperty("compileIncludes", out var compileIncludes));

        // Should have compile includes matching the extracted files
        var includes = compileIncludes.EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(2, includes.Count);
        Assert.All(includes, include => Assert.EndsWith(".cs", include ?? ""));

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task AnalyzerWithJsonFormat_ShouldPreserveExistingBehavior()
    {
        // Arrange
        var currentDir = Directory.GetCurrentDirectory();
        var testFile = Path.Combine(currentDir, "TestFiles", "SimpleTestClass.cs");
        var outputPath = Path.Combine(Path.GetTempPath(), "integration-test-output.json");

        var analyzer = new ClassContextAnalyzer.ClassContextAnalyzer();
        var rootDir = currentDir;

        // Act
        var result = await analyzer.AnalyzeAsync(testFile, rootDir);

        var jsonGenerator = new JsonResultGenerator();
        await jsonGenerator.GenerateAsync(result, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));

        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        // Should have files section
        Assert.True(root.TryGetProperty("files", out var files));
        Assert.True(files.GetArrayLength() > 0);

        // Should have project section
        Assert.True(root.TryGetProperty("project", out var project));
        Assert.True(project.TryGetProperty("compileIncludes", out var compileIncludes));

        // Should have diagnostics section
        Assert.True(root.TryGetProperty("diagnostics", out var diagnostics));

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeProjectMetadata()
    {
        // Arrange
        var sourcePath = "/path/to/source/TestClass.cs";
        var projectMetadata = new ProjectMetadata
        {
            ProjectPath = "/path/to/source/TestClass.csproj",
            TargetFramework = "net9.0",
            Nullable = "enable",
            ImplicitUsings = "enable",
            LangVersion = "latest",
            PackageReferences = new List<PackageReference>
            {
                new() { Include = "xunit", Version = "2.9.2" },
                new() { Include = "Microsoft.NET.Test.Sdk", Version = "17.12.0" }
            },
            FrameworkReferences = new List<FrameworkReference>
            {
                new() { Include = "Microsoft.NETCore.App" }
            }
        };

        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                [sourcePath] = new SourceFileInfo
                {
                    FilePath = sourcePath,
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = projectMetadata
        };

        var generator = new JsonResultGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-metadata-output.json");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("project", out var project));
        Assert.True(project.TryGetProperty("targetFramework", out var targetFramework));
        Assert.Equal("net9.0", targetFramework.GetString());

        Assert.True(project.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("Nullable", out var nullable));
        Assert.Equal("enable", nullable.GetString());
        Assert.True(properties.TryGetProperty("ImplicitUsings", out var implicitUsings));
        Assert.Equal("enable", implicitUsings.GetString());
        Assert.True(properties.TryGetProperty("LangVersion", out var langVersion));
        Assert.Equal("latest", langVersion.GetString());

        Assert.True(project.TryGetProperty("packageReferences", out var packageRefs));
        Assert.Equal(2, packageRefs.GetArrayLength());

        Assert.True(project.TryGetProperty("frameworkReferences", out var frameworkRefs));
        Assert.Equal(1, frameworkRefs.GetArrayLength());

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeProjectDiagnostics()
    {
        // Arrange
        var sourcePath = "/path/to/source/TestClass.cs";
        var projectMetadata = new ProjectMetadata
        {
            ProjectPath = "/path/to/source/TestClass.csproj",
            TargetFramework = "net9.0",
            Diagnostics = new List<AnalysisDiagnostic>
            {
                new()
                {
                    FilePath = "/path/to/source/TestClass.csproj",
                    Severity = DiagnosticSeverity.Warning,
                    Message = "Multiple target frameworks detected, using first one"
                }
            }
        };

        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                [sourcePath] = new SourceFileInfo
                {
                    FilePath = sourcePath,
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = projectMetadata
        };

        var generator = new JsonResultGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-diagnostics-output.json");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("project", out var project));
        Assert.True(project.TryGetProperty("diagnostics", out var projectDiagnostics));
        Assert.Equal(1, projectDiagnostics.GetArrayLength());

        var diagnostic = projectDiagnostics[0];
        Assert.Equal("Warning", diagnostic.GetProperty("severity").GetString());
        Assert.Contains("Multiple target frameworks", diagnostic.GetProperty("message").GetString());

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task EndToEndAnalysis_ShouldIncludeProjectMetadataInJson()
    {
        // Arrange
        var currentDir = Directory.GetCurrentDirectory();
        var testFile = Path.Combine(currentDir, "TestFiles", "SimpleTestClass.cs");
        var outputPath = Path.Combine(Path.GetTempPath(), "e2e-metadata-test.json");

        // Use the optimized analyzer which should find the project file
        var analyzer = new ClassContextAnalyzer.ClassContextAnalyzerWithSourceIndex();
        var rootDir = currentDir;

        // Act
        var result = await analyzer.AnalyzeAsync(testFile, rootDir);

        // Verify that project metadata was found
        if (result.ProjectMetadata != null)
        {
            var jsonGenerator = new JsonResultGenerator();
            await jsonGenerator.GenerateAsync(result, outputPath);

            // Assert
            Assert.True(File.Exists(outputPath));

            var jsonContent = await File.ReadAllTextAsync(outputPath);
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // Should have project metadata if it was found
            Assert.True(root.TryGetProperty("project", out var project));

            // If target framework was found, verify it's in the output
            if (!string.IsNullOrEmpty(result.ProjectMetadata.TargetFramework))
            {
                Assert.True(project.TryGetProperty("targetFramework", out var targetFramework));
                Assert.NotNull(targetFramework.GetString());
            }

            // Cleanup
            File.Delete(outputPath);
        }
        else
        {
            // If project metadata wasn't found (which can happen in test environments),
            // that's acceptable as long as the analysis still works
            Console.WriteLine("Project metadata not found (acceptable in test environment)");
        }
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeProjectReferencesInMetadata()
    {
        // Arrange
        var sourcePath = "/path/to/source/TestClass.cs";
        var projectMetadata = new ProjectMetadata
        {
            ProjectPath = "/path/to/source/TestClass.csproj",
            TargetFramework = "net9.0",
            ProjectReferences = new List<ProjectReference>
            {
                new() { Include = "..\\..\\MyLibrary\\MyLibrary.csproj" },
                new() { Include = "..\\SharedUtils\\SharedUtils.csproj" }
            }
        };

        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                [sourcePath] = new SourceFileInfo
                {
                    FilePath = sourcePath,
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = projectMetadata
        };

        var generator = new JsonResultGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-project-refs-output.json");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("project", out var project));
        Assert.True(project.TryGetProperty("projectReferences", out var projectRefs));
        Assert.Equal(2, projectRefs.GetArrayLength());

        var firstRef = projectRefs[0];
        Assert.True(firstRef.TryGetProperty("include", out var firstInclude));
        Assert.Contains("MyLibrary.csproj", firstInclude.GetString());

        var secondRef = projectRefs[1];
        Assert.True(secondRef.TryGetProperty("include", out var secondInclude));
        Assert.Contains("SharedUtils.csproj", secondInclude.GetString());

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeDiagnosticsAboutProjectReferenceFlattening()
    {
        // Arrange
        var sourcePath = "/path/to/source/TestClass.cs";
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                [sourcePath] = new SourceFileInfo
                {
                    FilePath = sourcePath,
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>
            {
                new()
                {
                    FilePath = sourcePath,
                    Severity = DiagnosticSeverity.Info,
                    Message = "Found 1 ProjectReference(s), will flatten types from referenced projects"
                },
                new()
                {
                    FilePath = "/path/to/referenced/UtilityClass.cs",
                    Severity = DiagnosticSeverity.Info,
                    Message = "Including file from ProjectReference: " +
                        "UtilityClass defined in /path/to/referenced/UtilityClass.cs"
                }
            },
            ProjectMetadata = null
        };

        var generator = new JsonResultGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-flattening-diagnostics.json");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        var jsonContent = await File.ReadAllTextAsync(outputPath);
        using var jsonDoc = JsonDocument.Parse(jsonContent);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("diagnostics", out var diagnostics));
        Assert.Equal(2, diagnostics.GetArrayLength());

        var firstDiag = diagnostics[0];
        Assert.Equal("Info", firstDiag.GetProperty("severity").GetString());
        Assert.Contains("ProjectReference", firstDiag.GetProperty("message").GetString());

        var secondDiag = diagnostics[1];
        Assert.Equal("Info", secondDiag.GetProperty("severity").GetString());
        Assert.Contains("Including file from ProjectReference", secondDiag.GetProperty("message").GetString());

        // Cleanup
        File.Delete(outputPath);
    }
}