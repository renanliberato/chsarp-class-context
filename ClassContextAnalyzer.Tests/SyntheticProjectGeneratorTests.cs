using ClassContextAnalyzer;
using System.Xml;

public class SyntheticProjectGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ShouldUseTargetFrameworkFromMetadata()
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
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = new ProjectMetadata
            {
                ProjectPath = "/path/to/source/TestProject.csproj",
                TargetFramework = "net8.0"
            }
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAsync(result, outputPath);

            // Assert
            var csprojPath = Path.Combine(outputPath, "ExtractedClasses.csproj");
            Assert.True(File.Exists(csprojPath));

            var csprojContent = await File.ReadAllTextAsync(csprojPath);
            var doc = new XmlDocument();
            doc.LoadXml(csprojContent);

            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

            // Check that TargetFramework matches the metadata
            var targetFrameworkNode = doc.SelectSingleNode("//TargetFramework") ??
                                     doc.SelectSingleNode("//ms:TargetFramework", namespaceManager);
            
            Assert.NotNull(targetFrameworkNode);
            Assert.Equal("net8.0", targetFrameworkNode.InnerText);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludePackageReferences()
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
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = new ProjectMetadata
            {
                ProjectPath = "/path/to/source/TestProject.csproj",
                TargetFramework = "net8.0",
                PackageReferences = new List<PackageReference>
                {
                    new PackageReference { Include = "Newtonsoft.Json", Version = "13.0.3" },
                    new PackageReference { Include = "Microsoft.Extensions.Logging", Version = "8.0.0" }
                }
            }
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAsync(result, outputPath);

            // Assert
            var csprojPath = Path.Combine(outputPath, "ExtractedClasses.csproj");
            var csprojContent = await File.ReadAllTextAsync(csprojPath);
            var doc = new XmlDocument();
            doc.LoadXml(csprojContent);

            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

            // Check that PackageReferences are included
            var packageReferences = doc.SelectNodes("//PackageReference") ??
                                    doc.SelectNodes("//ms:PackageReference", namespaceManager);
            
            Assert.NotNull(packageReferences);
            Assert.Equal(2, packageReferences.Count);

            var includes = new List<string>();
            var versions = new List<string>();
            
            foreach (XmlNode packageRef in packageReferences)
            {
                var include = packageRef.Attributes?.GetNamedItem("Include")?.Value;
                var version = packageRef.Attributes?.GetNamedItem("Version")?.Value;
                if (include != null) includes.Add(include);
                if (version != null) versions.Add(version);
            }

            Assert.Contains("Newtonsoft.Json", includes);
            Assert.Contains("13.0.3", versions);
            Assert.Contains("Microsoft.Extensions.Logging", includes);
            Assert.Contains("8.0.0", versions);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeFrameworkReferences()
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
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = new ProjectMetadata
            {
                ProjectPath = "/path/to/source/TestProject.csproj",
                TargetFramework = "net8.0",
                FrameworkReferences = new List<FrameworkReference>
                {
                    new FrameworkReference { Include = "Microsoft.AspNetCore.App" }
                }
            }
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAsync(result, outputPath);

            // Assert
            var csprojPath = Path.Combine(outputPath, "ExtractedClasses.csproj");
            var csprojContent = await File.ReadAllTextAsync(csprojPath);
            var doc = new XmlDocument();
            doc.LoadXml(csprojContent);

            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

            // Check that FrameworkReferences are included
            var frameworkReferences = doc.SelectNodes("//FrameworkReference") ??
                                      doc.SelectNodes("//ms:FrameworkReference", namespaceManager);
            
            Assert.NotNull(frameworkReferences);
            Assert.Single(frameworkReferences);

            var frameworkRef = frameworkReferences[0]!;
            var include = frameworkRef.Attributes?.GetNamedItem("Include")?.Value;
            Assert.Equal("Microsoft.AspNetCore.App", include);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateAsync_ShouldUseExplicitCompileItems()
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
                },
                ["/path/to/source/HelperClass.cs"] = new SourceFileInfo
                {
                    FilePath = "/path/to/source/HelperClass.cs",
                    Content = "public class HelperClass { }",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "HelperClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = new ProjectMetadata
            {
                ProjectPath = "/path/to/source/TestProject.csproj",
                TargetFramework = "net8.0"
            }
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAsync(result, outputPath);

            // Assert
            var csprojPath = Path.Combine(outputPath, "ExtractedClasses.csproj");
            var csprojContent = await File.ReadAllTextAsync(csprojPath);
            var doc = new XmlDocument();
            doc.LoadXml(csprojContent);

            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

            // Check that explicit Compile items exist
            var compileItems = doc.SelectNodes("//Compile") ??
                               doc.SelectNodes("//ms:Compile", namespaceManager);
            
            Assert.NotNull(compileItems);
            Assert.Equal(2, compileItems.Count);

            var includes = new List<string>();
            foreach (XmlNode compileItem in compileItems)
            {
                var include = compileItem.Attributes?.GetNamedItem("Include")?.Value;
                if (include != null) includes.Add(include);
            }

            Assert.Contains("TestClass.cs", includes);
            Assert.Contains("HelperClass.cs", includes);

            // Check that default compile pattern is disabled
            var enableDefaultCompileItemsNode = doc.SelectSingleNode("//EnableDefaultCompileItems") ??
                                               doc.SelectSingleNode("//ms:EnableDefaultCompileItems", namespaceManager);
            
            Assert.NotNull(enableDefaultCompileItemsNode);
            Assert.Equal("false", enableDefaultCompileItemsNode.InnerText);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeRelevantProperties()
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
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = new ProjectMetadata
            {
                ProjectPath = "/path/to/source/TestProject.csproj",
                TargetFramework = "net8.0",
                Nullable = "enable",
                ImplicitUsings = "disable",
                LangVersion = "latest"
            }
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAsync(result, outputPath);

            // Assert
            var csprojPath = Path.Combine(outputPath, "ExtractedClasses.csproj");
            var csprojContent = await File.ReadAllTextAsync(csprojPath);
            var doc = new XmlDocument();
            doc.LoadXml(csprojContent);

            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("ms", "http://schemas.microsoft.com/developer/msbuild/2003");

            // Check Nullable property
            var nullableNode = doc.SelectSingleNode("//Nullable") ??
                               doc.SelectSingleNode("//ms:Nullable", namespaceManager);
            Assert.NotNull(nullableNode);
            Assert.Equal("enable", nullableNode.InnerText);

            // Check ImplicitUsings property
            var implicitUsingsNode = doc.SelectSingleNode("//ImplicitUsings") ??
                                     doc.SelectSingleNode("//ms:ImplicitUsings", namespaceManager);
            Assert.NotNull(implicitUsingsNode);
            Assert.Equal("disable", implicitUsingsNode.InnerText);

            // Check LangVersion property
            var langVersionNode = doc.SelectSingleNode("//LangVersion") ??
                                  doc.SelectSingleNode("//ms:LangVersion", namespaceManager);
            Assert.NotNull(langVersionNode);
            Assert.Equal("latest", langVersionNode.InnerText);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EndToEnd_GeneratedProject_ShouldRestoreWithDotnetRestore()
    {
        // Arrange
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                ["/path/to/source/SimpleClass.cs"] = new SourceFileInfo
                {
                    FilePath = "/path/to/source/SimpleClass.cs",
                    Content = @"using System;

namespace TestNamespace
{
    public class SimpleClass
    {
        public int GetValue() => 42;
    }
}",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "SimpleClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = new ProjectMetadata
            {
                ProjectPath = "/path/to/source/TestProject.csproj",
                TargetFramework = "net8.0",
                Nullable = "enable",
                ImplicitUsings = "enable"
            }
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), $"e2e-restore-{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAsync(result, outputPath);

            // Try to restore the generated project
            var restoreProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"restore \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            restoreProcess.Start();
            var output = await restoreProcess.StandardOutput.ReadToEndAsync();
            var error = await restoreProcess.StandardError.ReadToEndAsync();
            await restoreProcess.WaitForExitAsync();

            // Assert
            Assert.True(restoreProcess.ExitCode == 0, $"dotnet restore failed. Output: {output}, Error: {error}");
            // The output may say "Restore succeeded" or just succeed silently
            Assert.True(string.IsNullOrEmpty(error) || !error.Contains("error", StringComparison.OrdinalIgnoreCase), 
                       $"dotnet restore had errors: {error}");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputPath))
            {
                try
                {
                    Directory.Delete(outputPath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }

    [Fact]
    public async Task EndToEnd_GeneratedProject_ShouldBuildWithDotnetBuild()
    {
        // Arrange
        var result = new ClassContextAnalysisResult
        {
            SourceFiles = new Dictionary<string, SourceFileInfo>
            {
                ["/path/to/source/BuildableClass.cs"] = new SourceFileInfo
                {
                    FilePath = "/path/to/source/BuildableClass.cs",
                    Content = @"namespace TestNamespace
{
    public class BuildableClass
    {
        public int Add(int a, int b) => a + b;
    }
}",
                    LastModified = DateTime.Now,
                    DefinedTypes = new HashSet<string> { "BuildableClass" }
                }
            },
            TypeReferences = new Dictionary<string, HashSet<string>>(),
            DependencyRelationships = new Dictionary<string, HashSet<string>>(),
            Diagnostics = new List<AnalysisDiagnostic>(),
            ProjectMetadata = new ProjectMetadata
            {
                ProjectPath = "/path/to/source/TestProject.csproj",
                TargetFramework = "net8.0",
                Nullable = "enable",
                ImplicitUsings = "disable"
            }
        };

        var generator = new CSharpProjectGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), $"e2e-build-{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAsync(result, outputPath);

            // First restore
            var restoreProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"restore \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            restoreProcess.Start();
            await restoreProcess.WaitForExitAsync();

            // Then build
            var buildProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{outputPath}\" --no-restore",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            buildProcess.Start();
            var output = await buildProcess.StandardOutput.ReadToEndAsync();
            var error = await buildProcess.StandardError.ReadToEndAsync();
            await buildProcess.WaitForExitAsync();

            // Assert
            Assert.True(buildProcess.ExitCode == 0, $"dotnet build failed. Output: {output}, Error: {error}");
            Assert.Contains("Build succeeded", output);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(outputPath))
            {
                try
                {
                    Directory.Delete(outputPath, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }
}