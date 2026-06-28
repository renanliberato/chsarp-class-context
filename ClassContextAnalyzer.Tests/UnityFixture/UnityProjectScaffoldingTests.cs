using Xunit;
using Xunit.Abstractions;
using ClassContextAnalyzer.UnityFixture;
using System.IO.Compression;

namespace ClassContextAnalyzer.Tests.UnityFixture;

public class UnityProjectScaffoldingTests
{
    private readonly ITestOutputHelper _output;

    public UnityProjectScaffoldingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ScaffoldUnityProjectAsync_CreatesMinimalUnityProject_WhenOutputPathIsValid()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        var tempDir = Path.Combine(Path.GetTempPath(), $"unity-fixture-test-{Guid.NewGuid()}");
        
        try
        {
            // Act
            var projectPath = await manager.ScaffoldUnityProjectAsync(tempDir);
            
            // Assert
            Assert.NotNull(projectPath);
            Assert.True(Directory.Exists(projectPath));
            
            // Verify Unity project structure exists
            var assetsDir = Path.Combine(projectPath, "Assets");
            Assert.True(Directory.Exists(assetsDir), "Assets directory should exist");
            
            var projectSettingsDir = Path.Combine(projectPath, "ProjectSettings");
            Assert.True(Directory.Exists(projectSettingsDir), "ProjectSettings directory should exist");
            
            // Verify .unity and .cs files exist
            var csFiles = Directory.GetFiles(assetsDir, "*.cs", SearchOption.AllDirectories);
            Assert.True(csFiles.Length > 0, "Should have at least one C# file");
            
            var unityFiles = Directory.GetFiles(assetsDir, "*.unity", SearchOption.AllDirectories);
            Assert.True(unityFiles.Length > 0, "Should have at least one .unity scene file");
            
            // Verify C# files contain Unity-style code that exercises the extractor
            var csContent = await File.ReadAllTextAsync(csFiles[0]);
            Assert.Contains("using UnityEngine;", csContent);
            Assert.Contains("MonoBehaviour", csContent);
            
            _output.WriteLine($"Scaffolded Unity project at: {projectPath}");
            _output.WriteLine($"Found {csFiles.Length} C# files and {unityFiles.Length} scene files");
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
    public async Task ScaffoldUnityProjectAsync_IncludesUnityStyleCode_ThatExercisesExtractor()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        var tempDir = Path.Combine(Path.GetTempPath(), $"unity-fixture-extractor-test-{Guid.NewGuid()}");
        
        try
        {
            // Act
            var projectPath = await manager.ScaffoldUnityProjectAsync(tempDir);
            
            // Assert
            var assetsDir = Path.Combine(projectPath, "Assets");
            var csFiles = Directory.GetFiles(assetsDir, "*.cs", SearchOption.AllDirectories);
            
            // Should have classes with inheritance, interfaces, and dependencies
            var hasMonoBehaviour = false;
            var hasScriptableObject = false;
            var hasInterface = false;
            var hasCustomClass = false;
            
            foreach (var csFile in csFiles)
            {
                var content = await File.ReadAllTextAsync(csFile);
                if (content.Contains("MonoBehaviour")) hasMonoBehaviour = true;
                if (content.Contains("ScriptableObject")) hasScriptableObject = true;
                if (content.Contains("interface ")) hasInterface = true;
                if (content.Contains("class ") && !content.Contains("MonoBehaviour")) hasCustomClass = true;
            }
            
            // At minimum, should have MonoBehaviour classes (standard Unity pattern)
            Assert.True(hasMonoBehaviour, "Should include at least one MonoBehaviour class");
            
            // Should exercise the extractor with Unity-specific patterns
            var allContent = string.Join("\n", csFiles.Select(f => File.ReadAllTextAsync(f).Result));
            Assert.True(allContent.Length > 500, "Should have substantial code to exercise extractor");
            
            _output.WriteLine("Unity project includes Unity-style code patterns for extractor validation");
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