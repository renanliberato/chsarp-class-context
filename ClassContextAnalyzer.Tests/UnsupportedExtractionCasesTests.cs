using ClassContextAnalyzer;
using Xunit;

public class UnsupportedExtractionCasesTests
{
    [Fact]
    public async Task ExtractMetadata_ShouldDetectSourceGenerators_AndReportExplicitError()
    {
        // Arrange - Create a .csproj file with source generators
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-sg-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <CompilerVisibleProperty Include=""GenerateDocumentationFile"" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include=""..\SourceGenerator\SourceGenerator.csproj"" 
                      OutputItemType=""Analyzer"" 
                      ReferenceOutputAssembly=""false"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var extractor = new ProjectMetadataExtractor();

        // Act
        var metadata = extractor.ExtractMetadata(csprojPath);

        // Assert - Should detect source generator and produce error diagnostic
        Assert.NotNull(metadata);
        var sourceGeneratorDiags = metadata.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error &&
                       d.Message.Contains("source generator", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(sourceGeneratorDiags);
        Assert.Contains("SourceGenerator.csproj", sourceGeneratorDiags[0].Message);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ExtractMetadata_ShouldDetectCustomMsBuildTargets_AndReportExplicitError()
    {
        // Arrange - Create a .csproj file with custom MSBuild targets
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-custom-targets-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <Import Project=""..\CustomTargets\CustomBuild.targets"" />
  
  <Target Name=""CustomCompileStep"" BeforeTargets=""Compile"">
    <Message Text=""Running custom compilation step"" Importance=""high"" />
  </Target>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var extractor = new ProjectMetadataExtractor();

        // Act
        var metadata = extractor.ExtractMetadata(csprojPath);

        // Assert - Should detect custom MSBuild targets and produce error diagnostic
        Assert.NotNull(metadata);
        var customTargetDiags = metadata.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error &&
                       (d.Message.Contains("custom target", StringComparison.OrdinalIgnoreCase) ||
                        d.Message.Contains("Import", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.NotEmpty(customTargetDiags);
        Assert.Contains("CustomBuild.targets", customTargetDiags[0].Message);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ExtractMetadata_ShouldDetectAnalyzerPackages_AndReportExplicitError()
    {
        // Arrange - Create a .csproj file with analyzer packages
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-analyzer-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.CodeAnalysis.Analyzers"" Version=""3.3.4"" />
    <PackageReference Include=""SonarAnalyzer.CSharp"" Version=""9.0.0.62888"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var extractor = new ProjectMetadataExtractor();

        // Act
        var metadata = extractor.ExtractMetadata(csprojPath);

        // Assert - Should detect analyzer packages and produce error diagnostic
        Assert.NotNull(metadata);
        var analyzerDiags = metadata.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error &&
                       d.Message.Contains("analyzer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(analyzerDiags);
        Assert.Contains("Microsoft.CodeAnalysis.Analyzers", analyzerDiags[0].Message);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ExtractMetadata_ShouldDetectUnsafeConditionalMetadata_AndReportExplicitError()
    {
        // Arrange - Create a .csproj file with unsafe conditional metadata
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-conditional-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <DefineConstants>$(DefineConstants);CUSTOM_CONDITION</DefineConstants>
  </PropertyGroup>

  <PropertyGroup Condition="" '$(BuildConfiguration)' == 'Release' "">
    <Optimize>true</Optimize>
  </PropertyGroup>

  <ItemGroup Condition="" '$(RuntimeIdentifier)' == 'win-x64' "">
    <PackageReference Include=""WindowsOnlyPackage"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var extractor = new ProjectMetadataExtractor();

        // Act
        var metadata = extractor.ExtractMetadata(csprojPath);

        // Assert - Should detect unsafe conditional metadata and produce error diagnostic
        Assert.NotNull(metadata);
        var errorDiags = metadata.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        var conditionalDiags = errorDiags
            .Where(d => d.Message.Contains("conditional", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasConditional = conditionalDiags.Any() ||
            errorDiags.Any(d => d.Message.Contains("BuildConfiguration", StringComparison.OrdinalIgnoreCase)) ||
            errorDiags.Any(d => d.Message.Contains("RuntimeIdentifier", StringComparison.OrdinalIgnoreCase));
        var allConditionalDiags = hasConditional
            ? errorDiags.Where(d =>
                d.Message.Contains("conditional", StringComparison.OrdinalIgnoreCase) ||
                d.Message.Contains("BuildConfiguration", StringComparison.OrdinalIgnoreCase) ||
                d.Message.Contains("RuntimeIdentifier", StringComparison.OrdinalIgnoreCase))
            : conditionalDiags;
        var finalDiags = allConditionalDiags.ToList();

        Assert.NotEmpty(finalDiags);

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldThrowExplicitException_WhenSourceGeneratorsPresent()
    {
        // Arrange - Create a test file and csproj with source generators
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-throw-sg-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""..\SourceGenerator\SourceGenerator.csproj"" 
                      OutputItemType=""Analyzer"" 
                      ReferenceOutputAssembly=""false"" />
  </ItemGroup>
</Project>";

        var csprojPath = Path.Combine(tempDir, "TestProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        var testFileContent = @"public class TestClass { }";
        var testFilePath = Path.Combine(tempDir, "TestClass.cs");
        await File.WriteAllTextAsync(testFilePath, testFileContent);

        var analyzer = new ClassContextAnalyzer.ClassContextAnalyzer();

        // Act & Assert - Should throw explicit exception
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => analyzer.AnalyzeAsync(testFilePath, tempDir));

        Assert.Contains("source generator", exception.Message, StringComparison.OrdinalIgnoreCase);

        // Cleanup
        Directory.Delete(tempDir, true);
    }
}