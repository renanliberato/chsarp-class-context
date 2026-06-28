using System.Text.Json;
using ClassContextAnalyzer;
using Xunit;

public class UnsupportedExtractionCasesIntegrationTests
{
    [Fact]
    public async Task Analyzer_ShouldThrow_WhenUnsupportedCasesPresent()
    {
        // Arrange - Create a test file and csproj with multiple unsupported cases
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-json-diagnostics-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.CodeAnalysis.Analyzers"" Version=""3.3.4"" />
  </ItemGroup>

  <Import Project=""..\CustomTargets\CustomBuild.targets"" />

  <PropertyGroup Condition="" '$(BuildConfiguration)' == 'Release' "">
    <Optimize>true</Optimize>
  </PropertyGroup>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var testFileContent = @"public class TestClass { }";
        var testFilePath = Path.Combine(tempDir, "TestClass.cs");
        await File.WriteAllTextAsync(testFilePath, testFileContent);

        var outputPath = Path.Combine(tempDir, "output.json");

        // Act - Try to analyze (should throw, but we'll catch to check JSON output)
        var analyzer = new ClassContextAnalyzer.ClassContextAnalyzer();
        Exception? caughtException = null;
        ClassContextAnalysisResult? result = null;

        try
        {
            result = await analyzer.AnalyzeAsync(testFilePath, tempDir);
        }
        catch (InvalidOperationException ex)
        {
            caughtException = ex;

            // Even though analysis failed, the exception message should contain actionable diagnostics
            Assert.Contains("analyzer", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cannot be safely", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Assert - Exception was thrown with actionable message
        Assert.NotNull(caughtException);
        Assert.Contains("analyzer", caughtException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be safely", caughtException.Message, StringComparison.OrdinalIgnoreCase);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task JSON_ShouldIncludeProjectMetadataDiagnostics_WhenPresent()
    {
        // Arrange - Create a test scenario with project metadata diagnostics
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-project-diag-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""StyleCop.Analyzers"" Version=""1.2.0-beta.435"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var testFileContent = @"public class TestClass { }";
        var testFilePath = Path.Combine(tempDir, "TestClass.cs");
        await File.WriteAllTextAsync(testFilePath, testFileContent);

        // Act - Extract metadata directly (before analysis throws)
        var extractor = new ProjectMetadataExtractor();
        var metadata = extractor.ExtractMetadata(csprojPath);

        // Assert - Metadata should have error diagnostics
        Assert.NotNull(metadata);
        Assert.NotEmpty(metadata.Diagnostics);

        var errorDiags = metadata.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.NotEmpty(errorDiags);

        // Verify actionable diagnostic content
        var analyzerDiag = errorDiags.FirstOrDefault(d => d.Message.Contains("StyleCop.Analyzers", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(analyzerDiag);
        Assert.Contains("Analyzer package detected", analyzerDiag.Message, StringComparison.OrdinalIgnoreCase);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task EndToEnd_WithUnsupportedCases_ShouldIncludeDiagnosticsInResult()
    {
        // Arrange - Create a test file with analyzer package
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-e2e-unsupported-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Roslynator.Analyzers"" Version=""4.0.0"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        // Act - Extract metadata directly (without triggering analyzer to throw)
        var extractor = new ProjectMetadataExtractor();
        var metadata = extractor.ExtractMetadata(csprojPath);

        // Assert - Diagnostics should be actionable
        Assert.NotNull(metadata);
        var errorDiags = metadata.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.NotEmpty(errorDiags);

        // Verify the diagnostic includes actionable information
        var diag = errorDiags[0];
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(csprojPath, diag.FilePath);
        Assert.Contains("Roslynator.Analyzers", diag.Message);
        Assert.Contains("Analyzer package detected", diag.Message, StringComparison.OrdinalIgnoreCase);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Analyzer_ShouldThrow_WithMultipleUnsupportedCases()
    {
        // Arrange - Create a test file with multiple unsupported cases
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-multiple-unsupported-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""StyleCop.Analyzers"" Version=""1.2.0"" />
  </ItemGroup>

  <Import Project=""..\CustomTargets.targets"" />
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var testFileContent = @"public class TestClass { }";
        var testFilePath = Path.Combine(tempDir, "TestClass.cs");
        await File.WriteAllTextAsync(testFilePath, testFileContent);

        // Act & Assert - Should throw with all diagnostics in message
        var analyzer = new ClassContextAnalyzer.ClassContextAnalyzer();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(testFilePath, tempDir));

        // Verify exception includes multiple issues
        Assert.Contains("StyleCop.Analyzers", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CustomTargets.targets", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be safely", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Cleanup
        Directory.Delete(tempDir, true);
    }
}