using Xunit;
using Xunit.Abstractions;
using ClassContextAnalyzer.UnityFixture;

namespace ClassContextAnalyzer.Tests.UnityFixture;

public class UnityWebGlBuildTests
{
    private readonly ITestOutputHelper _output;

    public UnityWebGlBuildTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task BuildWebGlAsync_ThrowsWithDiagnostic_WhenUnityUnavailable()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        var status = await manager.CheckUnityToolingAvailabilityAsync();
        
        if (status.IsAvailable)
        {
            _output.WriteLine("Unity available - skipping controlled skip test");
            return; // Skip this test when Unity is available
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"unity-build-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => manager.BuildWebGlAsync(tempDir, Path.Combine(tempDir, "Build")));
            
            Assert.NotNull(ex.Message);
            Assert.Contains("unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
            _output.WriteLine($"Controlled skip diagnostic: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task BuildWebGlAsync_ThrowsForNonExistentProject()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        var nonexistentPath = $"/nonexistent/path-{Guid.NewGuid()}";
        
        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => manager.BuildWebGlAsync(nonexistentPath, "/tmp/output"));
    }
}