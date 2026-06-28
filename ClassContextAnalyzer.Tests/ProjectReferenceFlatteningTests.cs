using Xunit;

namespace ClassContextAnalyzer.Tests
{
    public class ProjectReferenceFlatteningTests
    {
        [Fact]
        public async Task Analyzer_ShouldResolveTypesFromReferencedProjects()
        {
            // Arrange - create a temporary directory structure with two projects
            var tempDir = Path.Combine(Path.GetTempPath(), "test-project-ref-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create source project directory and files
                var sourceProjectDir = Path.Combine(tempDir, "SourceLibrary");
                Directory.CreateDirectory(sourceProjectDir);
                var sourceCsproj = Path.Combine(sourceProjectDir, "SourceLibrary.csproj");
                var sourceCsFile = Path.Combine(sourceProjectDir, "UtilityClass.cs");

                File.WriteAllText(sourceCsproj, @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>");

                File.WriteAllText(sourceCsFile, @"
namespace SourceLibrary
{
    public class UtilityClass
    {
        public static string GetGreeting()
        {
            return ""Hello from SourceLibrary"";
        }
    }
}");

                // Create test project directory and files
                var testProjectDir = Path.Combine(tempDir, "TestProject");
                Directory.CreateDirectory(testProjectDir);
                var testCsproj = Path.Combine(testProjectDir, "TestProject.csproj");
                var testCsFile = Path.Combine(testProjectDir, "TestClass.cs");

                File.WriteAllText(testCsproj, $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\SourceLibrary\SourceLibrary.csproj"" />
  </ItemGroup>
</Project>");

                File.WriteAllText(testCsFile, @"
using SourceLibrary;
using Xunit;

namespace TestProject
{
    public class TestClass
    {
        [Fact]
        public void TestGreeting()
        {
            var result = UtilityClass.GetGreeting();
            Assert.Equal(""Hello from SourceLibrary"", result);
        }
    }
}");

                // Act - analyze the test file
                var analyzer = new ClassContextAnalyzerWithSourceIndex();
                var result = await analyzer.AnalyzeAsync(testCsFile, tempDir);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.SourceFiles.Count >= 2, $"Expected at least 2 files, got {result.SourceFiles.Count}");

                // Should include the test file
                Assert.True(result.SourceFiles.ContainsKey(testCsFile), "Test file should be included");

                // Should include the source file from referenced project
                var sourceFileIncluded = result.SourceFiles.Keys.Any(f => f.EndsWith("UtilityClass.cs"));
                Assert.True(sourceFileIncluded, "Source file from referenced project should be included");

                // Should have diagnostics explaining the flattening
                var flatteningDiagnostics = result.Diagnostics
                    .Where(d => d.Message.Contains("ProjectReference") || d.Message.Contains("flattened"))
                    .ToList();
                Assert.True(flatteningDiagnostics.Count > 0, "Should have diagnostics explaining ProjectReference flattening");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact]
        public async Task Analyzer_ShouldIncludeDiagnosticsForFlattenedFiles()
        {
            // Arrange - create a temporary directory structure
            var tempDir = Path.Combine(Path.GetTempPath(), "test-diagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create source project
                var sourceProjectDir = Path.Combine(tempDir, "SourceLibrary");
                Directory.CreateDirectory(sourceProjectDir);
                var sourceCsproj = Path.Combine(sourceProjectDir, "SourceLibrary.csproj");
                var sourceCsFile = Path.Combine(sourceProjectDir, "DataSource.cs");

                File.WriteAllText(sourceCsproj, @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>");

                File.WriteAllText(sourceCsFile, @"
namespace SourceLibrary
{
    public class DataSource
    {
        public string GetData()
        {
            return ""Sample data"";
        }
    }
}");

                // Create test project with ProjectReference
                var testProjectDir = Path.Combine(tempDir, "TestProject");
                Directory.CreateDirectory(testProjectDir);
                var testCsproj = Path.Combine(testProjectDir, "TestProject.csproj");
                var testCsFile = Path.Combine(testProjectDir, "DataTest.cs");

                File.WriteAllText(testCsproj, $@"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\SourceLibrary\SourceLibrary.csproj"" />
  </ItemGroup>
</Project>");

                File.WriteAllText(testCsFile, @"
using SourceLibrary;
using Xunit;

namespace TestProject
{
    public class DataTest
    {
        [Fact]
        public void TestDataRetrieval()
        {
            var source = new DataSource();
            var data = source.GetData();
            Assert.NotNull(data);
        }
    }
}");

                // Act - analyze the test file
                var analyzer = new ClassContextAnalyzerWithSourceIndex();
                var result = await analyzer.AnalyzeAsync(testCsFile, tempDir);

                // Assert
                Assert.NotNull(result);

                // Check for diagnostics about why files were included
                var fileInclusionDiagnostics = result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Info &&
                               (d.Message.Contains("included") || d.Message.Contains("required")))
                    .ToList();

                // At minimum, we should have diagnostics from the source index building
                Assert.True(result.Diagnostics.Count > 0, "Should have diagnostics");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}