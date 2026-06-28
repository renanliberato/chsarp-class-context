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
}