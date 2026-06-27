using System.Diagnostics;
using System.Text;

namespace ClassContextAnalyzer;

public class BenchmarkRunner
{
    private readonly string _benchmarkProjectPath;

    public BenchmarkRunner(string benchmarkProjectPath)
    {
        _benchmarkProjectPath = benchmarkProjectPath;
    }

    public async Task<BenchmarkResult> GenerateTestProjectAsync(int classCount, int dependencyDepth = 3)
    {
        var stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"Generating {classCount} classes with interdependencies...");

        // Create benchmark directory
        if (Directory.Exists(_benchmarkProjectPath))
        {
            Directory.Delete(_benchmarkProjectPath, recursive: true);
        }
        Directory.CreateDirectory(_benchmarkProjectPath);

        // Generate classes
        var classNames = new List<string>();
        var generatedFiles = new List<string>();

        for (int i = 0; i < classCount; i++)
        {
            var className = $"TestClass{i:0000}";
            classNames.Add(className);

            var dependencies = GenerateDependencies(className, classNames, i, dependencyDepth);
            var fileContent = GenerateClassContent(className, dependencies);
            var fileName = $"{className}.cs";
            var filePath = Path.Combine(_benchmarkProjectPath, fileName);

            await File.WriteAllTextAsync(filePath, fileContent);
            generatedFiles.Add(filePath);
        }

        // Generate .csproj file
        var csprojContent = GenerateCsprojContent(classCount);
        var csprojPath = Path.Combine(_benchmarkProjectPath, "BenchmarkProject.csproj");
        await File.WriteAllTextAsync(csprojPath, csprojContent);

        stopwatch.Stop();

        return new BenchmarkResult
        {
            GenerationTime = stopwatch.ElapsedMilliseconds,
            ClassCount = classCount,
            FilesGenerated = generatedFiles.Count
        };
    }

    public async Task<PerformanceMetrics> RunBenchmarkAsync(string targetFile, string rootDir)
    {
        var metrics = new PerformanceMetrics();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var analyzer = new ClassContextAnalyzer();
            var result = await analyzer.AnalyzeAsync(targetFile, rootDir);

            stopwatch.Stop();

            metrics.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.FilesAnalyzed = result.SourceFiles.Count;
            metrics.TypeReferencesFound = result.TypeReferences.Values.Sum(set => set.Count);
            metrics.Success = true;

            Console.WriteLine($"Benchmark completed in {metrics.TotalTimeMs}ms");
            Console.WriteLine($"Files analyzed: {metrics.FilesAnalyzed}");
            Console.WriteLine($"Type references found: {metrics.TypeReferencesFound}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.Success = false;
            metrics.ErrorMessage = ex.Message;
            Console.Error.WriteLine($"Benchmark failed: {ex.Message}");
        }

        return metrics;
    }

    public async Task<PerformanceMetrics> RunBenchmarkWithSourceIndexAsync(string targetFile, string rootDir)
    {
        var metrics = new PerformanceMetrics();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var analyzer = new ClassContextAnalyzerWithSourceIndex();
            var result = await analyzer.AnalyzeAsync(targetFile, rootDir);

            stopwatch.Stop();

            metrics.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.FilesAnalyzed = result.SourceFiles.Count;
            metrics.TypeReferencesFound = result.TypeReferences.Values.Sum(set => set.Count);
            metrics.Success = true;

            Console.WriteLine($"Benchmark with source index completed in {metrics.TotalTimeMs}ms");
            Console.WriteLine($"Files analyzed: {metrics.FilesAnalyzed}");
            Console.WriteLine($"Type references found: {metrics.TypeReferencesFound}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.TotalTimeMs = stopwatch.ElapsedMilliseconds;
            metrics.Success = false;
            metrics.ErrorMessage = ex.Message;
            Console.Error.WriteLine($"Benchmark with source index failed: {ex.Message}");
        }

        return metrics;
    }

    private List<string> GenerateDependencies(string className, List<string> allClassNames, int currentIndex, int depth)
    {
        var dependencies = new List<string>();

        // Create a web of dependencies by referencing previous classes
        if (currentIndex > 0)
        {
            // Reference the previous class (creates a chain)
            dependencies.Add(allClassNames[currentIndex - 1]);

            // Add some cross-references to create a graph
            if (currentIndex >= 5)
            {
                dependencies.Add(allClassNames[currentIndex - 5]);
            }

            // Add some random references
            if (currentIndex >= 10 && currentIndex % 3 == 0)
            {
                dependencies.Add(allClassNames[currentIndex / 2]);
            }
        }

        return dependencies.Distinct().ToList();
    }

    private string GenerateClassContent(string className, List<string> dependencies)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"using System;");
        sb.AppendLine($"using System.Collections.Generic;");
        sb.AppendLine();

        // Generate class declaration
        sb.AppendLine($"public class {className}");
        sb.AppendLine($"{{");

        // Generate fields for dependencies
        foreach (var dep in dependencies)
        {
            sb.AppendLine($"    private {dep} _{dep.ToLower()};");
        }

        sb.AppendLine();

        // Generate constructor
        sb.AppendLine($"    public {className}({string.Join(", ", dependencies.Select(d => $"{d} {d.ToLower()}"))})");
        sb.AppendLine($"    {{");
        foreach (var dep in dependencies)
        {
            sb.AppendLine($"        _{dep.ToLower()} = {dep.ToLower()};");
        }
        sb.AppendLine($"    }}");
        sb.AppendLine();

        // Generate method 1
        sb.AppendLine($"    public void Execute{className}()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        Console.WriteLine(\"Processing {className}\");");
        if (dependencies.Any())
        {
            sb.AppendLine($"        _{dependencies[0].ToLower()}?.Execute{dependencies[0]}();");
        }
        sb.AppendLine($"    }}");
        sb.AppendLine();

        // Generate method 2
        sb.AppendLine($"    public int Compute{className}()");
        sb.AppendLine($"    {{");
        sb.AppendLine($"        return {dependencies.Count * 42};");
        sb.AppendLine($"    }}");

        sb.AppendLine($"}}");

        return sb.ToString();
    }

    private string GenerateCsprojContent(int classCount)
    {
        return $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>";
    }
}

public class BenchmarkResult
{
    public long GenerationTime { get; set; }
    public int ClassCount { get; set; }
    public int FilesGenerated { get; set; }
}

public class PerformanceMetrics
{
    public long TotalTimeMs { get; set; }
    public int FilesAnalyzed { get; set; }
    public int TypeReferencesFound { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public void SaveToFile(string filePath)
    {
        var lines = new[]
        {
            $"Benchmark completed: {(Success ? "SUCCESS" : "FAILED")}",
            $"Total time: {TotalTimeMs}ms",
            $"Files analyzed: {FilesAnalyzed}",
            $"Type references found: {TypeReferencesFound}",
            $"",
            $"{(ErrorMessage != null ? $"Error: {ErrorMessage}" : "")}"
        };

        File.WriteAllLines(filePath, lines);
    }
}