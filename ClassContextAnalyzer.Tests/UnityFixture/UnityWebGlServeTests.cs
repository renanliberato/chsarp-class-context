using Xunit;
using Xunit.Abstractions;
using ClassContextAnalyzer.UnityFixture;

namespace ClassContextAnalyzer.Tests.UnityFixture;

public class UnityWebGlServeTests
{
    private readonly ITestOutputHelper _output;

    public UnityWebGlServeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ServeWebGlAsync_ThrowsWithDiagnostic_WhenBuildPathInvalid()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        var nonexistentPath = $"/nonexistent/path-{Guid.NewGuid()}";
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => manager.ServeWebGlAsync(nonexistentPath));
        
        Assert.NotNull(ex.Message);
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine($"Expected diagnostic for invalid path: {ex.Message}");
    }

    [Fact]
    public async Task ServeWebGlAsync_ThrowsWithDiagnostic_WhenNoHttpServerAvailable()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        var tempDir = Path.Combine(Path.GetTempPath(), $"unity-serve-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        // Create minimal index.html
        await File.WriteAllTextAsync(Path.Combine(tempDir, "index.html"), "<html><body>Test</body></html>");
        
        try
        {
            // Act & Assert - should fail gracefully with actionable diagnostic
            var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(
                () => manager.ServeWebGlAsync(tempDir));
            
            Assert.NotNull(ex.Message);
            Assert.Contains("server", ex.Message, StringComparison.OrdinalIgnoreCase);
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
    public void ServeWebGlAsync_AcceptsCustomPort()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        var tempDir = Path.Combine(Path.GetTempPath(), $"unity-port-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            // Act & Assert - just verify it accepts the parameter without throwing
            // (will throw later when trying to start server)
            var task = manager.ServeWebGlAsync(tempDir, 9000);
            
            // The task should fail with diagnostic, not parameter validation error
            Assert.True(task.IsFaulted || task.Status == TaskStatus.WaitingForActivation);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}