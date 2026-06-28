using System.Text.Json;
using ClassContextAnalyzer.Tests;
using ClassContextAnalyzer;

public class ExistingFormatsTests
{
    [Fact]
    public async Task MarkdownGenerator_ShouldStillWork()
    {
        // Arrange
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                ["/path/to/source/TestClass.cs"] = new SourceFileInfo
                {
                    FilePath = "/path/to/source/TestClass.cs",
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>()
        };

        var generator = new MarkdownGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-markdown.md");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("```csharp", content);
        Assert.Contains("TestClass", content);

        // Cleanup
        File.Delete(outputPath);
    }

    [Fact]
    public async Task CSharpProjectGenerator_ShouldStillWork()
    {
        // Arrange
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                ["/path/to/source/TestClass.cs"] = new SourceFileInfo
                {
                    FilePath = "/path/to/source/TestClass.cs",
                    Content = "public class TestClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "TestClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>()
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "test-project");

        // Act
        await generator.GenerateAsync(result, outputPath);

        // Assert
        Assert.True(Directory.Exists(outputPath));
        var csFile = Path.Combine(outputPath, "TestClass.cs");
        Assert.True(File.Exists(csFile));
        var csprojFile = Path.Combine(outputPath, "ExtractedClasses.csproj");
        Assert.True(File.Exists(csprojFile));

        // Cleanup
        Directory.Delete(outputPath, recursive: true);
    }
}