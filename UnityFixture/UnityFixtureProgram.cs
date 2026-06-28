using System.CommandLine;

namespace ClassContextAnalyzer.UnityFixture;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var manager = new UnityWebGlFixtureManager();

        var scaffoldCommand = new Command("scaffold", "Create a minimal Unity project fixture");
        var outputArgument = new Argument<string>("output-path", "Directory where Unity project will be created")
        {
            Arity = ArgumentArity.ExactlyOne
        };
        scaffoldCommand.AddArgument(outputArgument);
        
        scaffoldCommand.SetHandler<string>(async (outputPath) =>
        {
            try
            {
                var path = await manager.ScaffoldUnityProjectAsync(outputPath);
                Console.WriteLine($"✓ Unity project scaffolded at: {path}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"✗ Scaffold failed: {ex.Message}");
                Environment.Exit(1);
            }
        }, outputArgument);

        var buildCommand = new Command("build", "Build Unity project for WebGL");
        var projectPathArgument = new Argument<string>("project-path", "Path to Unity project")
        {
            Arity = ArgumentArity.ExactlyOne
        };
        var outputPathArgument = new Argument<string>("output-path", "Directory for WebGL build output")
        {
            Arity = ArgumentArity.ExactlyOne
        };
        buildCommand.AddArgument(projectPathArgument);
        buildCommand.AddArgument(outputPathArgument);
        
        buildCommand.SetHandler<string, string>(async (projectPath, outputPath) =>
        {
            try
            {
                await manager.BuildWebGlAsync(projectPath, outputPath);
                Console.WriteLine($"✓ WebGL build complete at: {outputPath}");
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"⚠ {ex.Message}");
                Environment.Exit(0); // Exit cleanly with diagnostic
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"✗ Build failed: {ex.Message}");
                Environment.Exit(1);
            }
        }, projectPathArgument, outputPathArgument);

        var serveCommand = new Command("serve", "Serve WebGL build locally");
        var buildPathArgument = new Argument<string>("build-path", "Path to WebGL build directory")
        {
            Arity = ArgumentArity.ExactlyOne
        };
        var portOption = new Option<int>("--port", () => 8080, "Port to serve on");
        serveCommand.AddArgument(buildPathArgument);
        serveCommand.AddOption(portOption);
        
        serveCommand.SetHandler<string, int>(async (buildPath, port) =>
        {
            try
            {
                await manager.ServeWebGlAsync(buildPath, port);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"⚠ {ex.Message}");
                Environment.Exit(0); // Exit cleanly with diagnostic
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"✗ Serve failed: {ex.Message}");
                Environment.Exit(1);
            }
        }, buildPathArgument, portOption);

        var rootCommand = new RootCommand("Unity WebGL Fixture - Automated Unity project scaffolding, building, and serving");
        rootCommand.AddCommand(scaffoldCommand);
        rootCommand.AddCommand(buildCommand);
        rootCommand.AddCommand(serveCommand);

        return await rootCommand.InvokeAsync(args);
    }
}