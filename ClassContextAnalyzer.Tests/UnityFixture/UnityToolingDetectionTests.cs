using Xunit;
using Xunit.Abstractions;
using ClassContextAnalyzer.UnityFixture;

namespace ClassContextAnalyzer.Tests.UnityFixture;

public class UnityToolingDetectionTests
{
    private readonly ITestOutputHelper _output;

    public UnityToolingDetectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CheckUnityToolingAvailability_ReturnsStatusAndDiagnostic_WhenCalled()
    {
        // Arrange
        var manager = new UnityWebGlFixtureManager();
        
        // Act
        var result = await manager.CheckUnityToolingAvailabilityAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains(result.IsAvailable, new[] { true, false }); // Either state is valid
        
        if (!result.IsAvailable)
        {
            // When unavailable, must provide actionable diagnostic
            Assert.NotNull(result.Diagnostic);
            Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
            Assert.DoesNotContain("prompt", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hang", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
            
            _output.WriteLine($"Unity unavailable: {result.Diagnostic}");
        }
        else
        {
            _output.WriteLine($"Unity available: {result.UnityPath}");
        }
    }
}