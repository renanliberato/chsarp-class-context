using ClassContextAnalyzer.Tests;
using ClassContextAnalyzer;

public class CsprojMetadataTests
{
    private readonly ProjectMetadataExtractor _extractor = new();

    [Fact]
    public void FindProjectFile_ForFileInProjectDirectory_ShouldReturnProjectPath()
    {
        // Arrange
        var currentDir = Directory.GetCurrentDirectory();
        var testFile = Path.Combine(currentDir, "TestFiles", "SimpleTestClass.cs");

        // Act
        var result = _extractor.FindProjectFile(testFile);

        // Assert
        Assert.NotNull(result);
        Assert.True(Path.IsPathRooted(result));
        Assert.EndsWith("ClassContextAnalyzer.Tests.csproj", result.Replace("\\", "/"));
    }

    [Fact]
    public void FindProjectFile_ForNonExistentFile_ShouldReturnNull()
    {
        // Arrange
        var nonExistentFile = "/path/to/nonexistent/File.cs";

        // Act
        var result = _extractor.FindProjectFile(nonExistentFile);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExtractMetadata_ShouldExtractTargetFramework()
    {
        // Arrange - find the csproj file from the current directory
        var csprojPath = FindCsprojFile("ClassContextAnalyzer.Tests.csproj");
        Assert.True(File.Exists(csprojPath), $"csproj file not found at: {csprojPath}");

        // Act
        var result = _extractor.ExtractMetadata(csprojPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("net9.0", result.TargetFramework);
    }

    [Fact]
    public void ExtractMetadata_ShouldExtractNullableAndImplicitUsings()
    {
        // Arrange - find the csproj file from the current directory
        var csprojPath = FindCsprojFile("ClassContextAnalyzer.Tests.csproj");
        Assert.True(File.Exists(csprojPath), $"csproj file not found at: {csprojPath}");

        // Act
        var result = _extractor.ExtractMetadata(csprojPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("enable", result.Nullable);
        Assert.Equal("enable", result.ImplicitUsings);
    }

    private string FindCsprojFile(string fileName)
    {
        // Search upward from current directory for the csproj file
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (currentDir != null)
        {
            var csprojFile = currentDir.GetFiles(fileName).FirstOrDefault();
            if (csprojFile != null)
            {
                return csprojFile.FullName;
            }
            currentDir = currentDir.Parent;
        }
        throw new FileNotFoundException($"Could not find {fileName}");
    }

    [Fact]
    public void ExtractMetadata_ShouldExtractPackageReferences()
    {
        // Arrange - find the csproj file from the current directory
        var csprojPath = FindCsprojFile("ClassContextAnalyzer.Tests.csproj");
        Assert.True(File.Exists(csprojPath), $"csproj file not found at: {csprojPath}");

        // Act
        var result = _extractor.ExtractMetadata(csprojPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.PackageReferences.Count > 0);

        // Check for known package references in the test project
        var xunitPackage = result.PackageReferences.FirstOrDefault(p => p.Include == "xunit");
        Assert.NotNull(xunitPackage);
        Assert.Equal("2.9.2", xunitPackage.Version);

        var testSdkPackage = result.PackageReferences.FirstOrDefault(p => p.Include == "Microsoft.NET.Test.Sdk");
        Assert.NotNull(testSdkPackage);
        Assert.Equal("17.12.0", testSdkPackage.Version);
    }

    [Fact]
    public void ExtractMetadata_ShouldHandleMissingPackageReferences()
    {
        // Arrange - create a minimal csproj without PackageReference
        var tempCsproj = Path.Combine(Path.GetTempPath(), "minimal.csproj");
        File.WriteAllText(tempCsproj, @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>");

        // Act
        var result = _extractor.ExtractMetadata(tempCsproj);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.PackageReferences);

        // Cleanup
        File.Delete(tempCsproj);
    }

    [Fact]
    public void ExtractMetadata_ShouldReportDiagnosticsForMalformedCsproj()
    {
        // Arrange - create a malformed csproj
        var tempCsproj = Path.Combine(Path.GetTempPath(), "malformed.csproj");
        File.WriteAllText(tempCsproj, @"<Project><NotValid></Project>");

        // Act
        var result = _extractor.ExtractMetadata(tempCsproj);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Diagnostics.Count > 0);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);

        // Cleanup
        File.Delete(tempCsproj);
    }

    [Fact]
    public void ExtractMetadata_ShouldHandleTargetFrameworksMultipleValues()
    {
        // Arrange - create a csproj with TargetFrameworks
        var tempCsproj = Path.Combine(Path.GetTempPath(), "multi-target.csproj");
        File.WriteAllText(tempCsproj, @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0</TargetFrameworks>
  </PropertyGroup>
</Project>");

        // Act
        var result = _extractor.ExtractMetadata(tempCsproj);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.TargetFramework); // Should be null when TargetFrameworks is used
        Assert.Equal(2, result.TargetFrameworks.Count);
        Assert.Contains("net8.0", result.TargetFrameworks);
        Assert.Contains("net9.0", result.TargetFrameworks);

        // Cleanup
        File.Delete(tempCsproj);
    }

    [Fact]
    public void ExtractMetadata_ShouldReturnNullForNonExistentFile()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent/project.csproj";

        // Act
        var result = _extractor.ExtractMetadata(nonExistentPath);

        // Assert
        Assert.Null(result);
    }
}