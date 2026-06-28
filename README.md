# 🧩 C# Class Context Analyzer

A powerful command-line tool that analyzes C# code to find all dependencies and references for a given class, generating comprehensive markdown documentation with complete context.

## ✨ Features

- **🔍 Deep Dependency Analysis** - Recursively discovers all type references, base classes, interfaces, and dependencies
- **📝 Markdown Generation** - Creates beautifully formatted documentation with syntax highlighting
- **⚡ Fast & Efficient** - Uses Roslyn syntax analysis for lightning-fast parsing
- **🎯 Smart Type Detection** - Intelligently identifies custom types while filtering built-in types and keywords
- **🌳 Recursive Discovery** - Automatically finds and analyzes all related files in your codebase

## 🚀 Quick Start

### Installation

```bash
git clone <repository-url>
cd csharp-class-context
dotnet build
```

### Usage

```bash
# Analyze a single file
dotnet run -- --file path/to/your/class.cs

# Specify output file
dotnet run -- --file path/to/your/class.cs --output documentation.md

# Search in specific root directory
dotnet run -- --file path/to/your/class.cs --root-dir ./src
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--file` | C# file to analyze (required) | - |
| `--output` | Output markdown file path | `output.md` |
| `--root-dir` | Root directory to search for dependencies | Current directory |

## 📊 What It Analyzes

The tool discovers and documents:

- **Class Declarations** - Base classes and inherited interfaces
- **Method Signatures** - Return types and parameter types
- **Properties** - Property types and accessors
- **Fields** - Field types and declarations
- **Constructors** - Parameter types
- **Generic Types** - Generic type arguments
- **Type References** - All custom type usage throughout the code

## 🎯 Example Output

<pre>
// /src/Models/User.cs
```csharp
public class User : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public OrderList Orders { get; set; }
    
    public User(string name)
    {
        Name = name;
        Orders = new OrderList();
    }
}
```

// /src/Interfaces/IEntity.cs
```csharp
public interface IEntity
{
    int Id { get; set; }
}
```

// /src/Models/Order.cs
```csharp
public class OrderList
{
}
```
</pre>

## 🛠️ Technology Stack

- **.NET 8.0** - Modern, high-performance runtime
- **Microsoft.CodeAnalysis** - Powerful Roslyn syntax analysis
- **System.CommandLine** - Professional CLI argument parsing

## 🧠 How It Works

1. **Syntax Parsing** - Uses Roslyn to parse C# syntax trees
2. **Type Discovery** - Walks the syntax tree to find all type references
3. **Recursive Analysis** - Searches for type definitions across the codebase
4. **Smart Filtering** - Excludes built-in types and C# keywords
5. **Markdown Generation** - Creates comprehensive documentation with all related files

## 🔍 Linting

This project uses [CovenantCheck](https://github.com/renanliberato/CovenantCheck) for code quality linting. CovenantCheck is a C# linter that enforces best practices and helps maintain code quality.

### Running the Linter Locally

To run the linter locally:

```bash
./lint.sh
```

This script will:
- Exclude generated files (`bin/`, `obj/`, `.robot/`)
- Report any code quality issues
- Exit with code 1 if issues are found, 0 if clean

### What CovenantCheck Checks

- **CC-NUMBERS-SIZE-001**: Functions should not exceed 50 lines
- **CC-NUMBERS-SIZE-002**: Lines should not exceed 120 characters
- **CC-SAFETY-CTRL-002**: Boolean conditions should not be overly complex (>1 logical operator)
- **CC-SAFETY-INIT-001**: Object initializers should not assign null

## 📈 Use Cases

- **Code Reviews** - Get complete context for any class
- **Documentation** - Auto-generate technical documentation
- **Refactoring** - Understand impact before making changes
- **Onboarding** - Help new developers understand code relationships
- **Architecture Analysis** - Visualize dependency graphs