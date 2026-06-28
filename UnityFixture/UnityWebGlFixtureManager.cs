using System.Diagnostics;

namespace ClassContextAnalyzer.UnityFixture;

public class UnityToolingStatus
{
    public bool IsAvailable { get; set; }
    public string? UnityPath { get; set; }
    public string? Diagnostic { get; set; }
    public string? Version { get; set; }
}

public interface IUnityWebGlFixtureManager
{
    Task<UnityToolingStatus> CheckUnityToolingAvailabilityAsync();
    Task<string> ScaffoldUnityProjectAsync(string outputPath);
    Task BuildWebGlAsync(string projectPath, string outputPath);
    Task ServeWebGlAsync(string buildPath, int? port = null);
}

public class UnityWebGlFixtureManager : IUnityWebGlFixtureManager
{
    public async Task<UnityToolingStatus> CheckUnityToolingAvailabilityAsync()
    {
        var result = new UnityToolingStatus();
        
        // Check for Unity executable in common locations
        var unityPaths = new[]
        {
            "/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity",
            "/Applications/Unity/Unity.app/Contents/MacOS/Unity",
            "C:\\Program Files\\Unity\\Hub\\Editor\\*\\Editor\\Unity.exe",
            "/opt/unity-editor/Editor/Unity"
        };
        
        foreach (var pattern in unityPaths)
        {
            if (pattern.Contains('*'))
            {
                // Handle wildcard patterns
                var directory = Path.GetDirectoryName(pattern);
                var searchPattern = Path.GetFileName(pattern);
                
                if (directory != null && Directory.Exists(directory))
                {
                    var matches = Directory.GetFiles(directory, searchPattern)
                        .OrderByDescending(f => f)
                        .ToList();
                    
                    if (matches.Any())
                    {
                        result.UnityPath = matches.First();
                        result.IsAvailable = true;
                        break;
                    }
                }
            }
            else if (File.Exists(pattern))
            {
                result.UnityPath = pattern;
                result.IsAvailable = true;
                break;
            }
        }
        
        if (result.IsAvailable && result.UnityPath != null)
        {
            result.Version = await GetUnityVersionAsync(result.UnityPath);
        }
        else
        {
            result.Diagnostic = "Unity tooling not found. Install Unity Hub and Unity Editor (2022.3 LTS or later recommended) from https://unity.com/download. After installation, ensure Unity is added to PATH or installed in a standard location.";
        }
        
        return result;
    }

    private async Task<string?> GetUnityVersionAsync(string unityPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = unityPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(startInfo);
            if (process == null) return null;
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output.Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> ScaffoldUnityProjectAsync(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be empty", nameof(outputPath));

        Directory.CreateDirectory(outputPath);

        // Create Assets directory
        var assetsDir = Path.Combine(outputPath, "Assets");
        Directory.CreateDirectory(assetsDir);

        // Create ProjectSettings directory
        var projectSettingsDir = Path.Combine(outputPath, "ProjectSettings");
        Directory.CreateDirectory(projectSettingsDir);

        // Create minimal ProjectSettings/ProjectVersion.txt
        var projectVersionPath = Path.Combine(projectSettingsDir, "ProjectVersion.txt");
        await File.WriteAllTextAsync(projectVersionPath, "m_EditorVersion: 2022.3.0f1\nm_EditorVersionWithRevision: 2022.3.0f1 (fb119bb12b21)\n");

        // Create Unity-style C# files that exercise the extractor
        var playerControllerCs = Path.Combine(assetsDir, "PlayerController.cs");
        await File.WriteAllTextAsync(playerControllerCs, @"using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls player movement and interaction with the game world.
/// Exercises the extractor with MonoBehaviour inheritance and component references.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 7f;
    private Rigidbody2D rb;
    private Animator animator;
    private IInteractable currentInteraction;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxis(""Horizontal"");
        Vector2 movement = new Vector2(horizontal, 0f);
        
        if (movement.magnitude > 0.1f)
        {
            rb.velocity = new Vector2(horizontal * moveSpeed, rb.velocity.y);
            animator.SetBool(""IsWalking"", true);
        }
        else
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
            animator.SetBool(""IsWalking"", false);
        }

        if (Input.GetButtonDown(""Jump""))
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }

        if (Input.GetButtonDown(""Interact""))
        {
            currentInteraction?.Interact();
        }
    }
}

/// <summary>
/// Interface for objects that can be interacted with.
/// Exercises the extractor with interface definitions and implementations.
/// </summary>
public interface IInteractable
{
    void Interact();
    string InteractionPrompt { get; }
}

/// <summary>
/// Represents an item that can be collected in the game.
/// Exercises the extractor with custom classes and ScriptableObject pattern.
/// </summary>
[CreateAssetMenu(fileName = ""NewItem"", menuName = ""Items/Item Data"")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public string description;
    public Sprite icon;
    public int maxStackSize = 99;
}

/// <summary>
/// Base class for all game entities.
/// Exercises the extractor with class inheritance and virtual methods.
/// </summary>
public abstract class GameEntity : MonoBehaviour
{
    [SerializeField] protected int maxHealth = 100;
    protected int currentHealth;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}

/// <summary>
/// Collectible object that players can pick up.
/// Exercises the extractor with interface implementation and base class usage.
/// </summary>
public class Collectible : GameEntity, IInteractable
{
    [SerializeField] private ItemData itemData;
    public string InteractionPrompt => $""Pick up {itemData?.itemName ?? ""Item""}"";

    public void Interact()
    {
        InventoryManager.Instance.AddItem(itemData);
        Destroy(gameObject);
    }
}

/// <summary>
/// Singleton manager for player inventory.
/// Exercises the extractor with singleton pattern and generics.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }
    private List<ItemData> items = new List<ItemData>();
    private const int MaxSlots = 20;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool AddItem(ItemData item)
    {
        if (items.Count >= MaxSlots) return false;
        items.Add(item);
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        return items.Remove(item);
    }

    public IEnumerable<ItemData> GetItems() => items.AsReadOnly();
}");

        // Create a minimal scene file
        var scenePath = Path.Combine(assetsDir, "TestScene.unity");
        await File.WriteAllTextAsync(scenePath, @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 9
  m_Fog: 0
  m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}
  m_FogMode: 3
  m_FogDensity: 0.01
  m_LinearFogStart: 0
  m_LinearFogEnd: 300
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 11
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 2
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
  m_Walkable: 1
--- !u!1 &5
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 6}
  - component: {fileID: 7}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &6
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 5}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: -10}
  m_LocalScale: {x: 1, y: 1, z: 1}
--- !u!20 &7
Camera:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 5}
  m_Enabled: 1
  serializedVersion: 2
");

        return outputPath;
    }

    public async Task BuildWebGlAsync(string projectPath, string outputPath)
    {
        if (!Directory.Exists(projectPath))
            throw new DirectoryNotFoundException($"Project path not found: {projectPath}");

        var status = await CheckUnityToolingAvailabilityAsync();
        if (!status.IsAvailable)
        {
            throw new InvalidOperationException(
                $"Unity tooling unavailable for WebGL build. {status.Diagnostic}");
        }

        Directory.CreateDirectory(outputPath);

        var logPath = Path.Combine(outputPath, "build.log");
        var args = new[]
        {
            "-quit",
            "-batchmode",
            "-nographics",
            "-projectPath", projectPath,
            "-buildTarget", "WebGL",
            "-buildPath", outputPath,
            "-executeMethod", "UnityEditor.EditorApplication.Exit",
            "-logFile", logPath
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = status.UnityPath!,
            Arguments = string.Join(" ", args.Select(a => $"\"{a}\"")),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start Unity build process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var logContent = File.Exists(logPath) ? await File.ReadAllTextAsync(logPath) : "No log file generated";
            throw new InvalidOperationException($"Unity WebGL build failed with exit code {process.ExitCode}. Log: {logContent}");
        }
    }

    public async Task ServeWebGlAsync(string buildPath, int? port = null)
    {
        if (!Directory.Exists(buildPath))
            throw new DirectoryNotFoundException($"Build path not found: {buildPath}");

        var actualPort = port ?? 8080;
        
        // Try Python http.server
        var pythonPaths = new[] { "python3", "python", "python3.11", "python3.10" };
        Process? pythonProcess = null;

        foreach (var pythonPath in pythonPaths)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"-m http.server {actualPort}",
                    WorkingDirectory = buildPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                pythonProcess = Process.Start(startInfo);
                if (pythonProcess != null)
                {
                    Console.WriteLine($"WebGL build serving at http://localhost:{actualPort}");
                    Console.WriteLine("Press Ctrl+C to stop the server");
                    
                    // Wait indefinitely until cancelled
                    await pythonProcess.WaitForExitAsync();
                    return;
                }
            }
            catch
            {
                continue;
            }
        }

        throw new InvalidOperationException(
            $"Failed to start HTTP server for WebGL build. Install Python 3 or provide an HTTP server. " +
            $"Alternatively, open {buildPath} directly in a web browser.");
    }
}