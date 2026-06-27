# C# Class Context Analyzer - Benchmark Results

This directory contains sample benchmark results demonstrating the performance improvements from issue #4.

## Performance Summary

### Small Scale (100 Classes)
- **Baseline (original):** ~3.4s average
- **Optimized (source index):** 23ms average
- **Improvement:** 99.32% (147x speedup)

### Medium Scale (200 Classes)
- **Baseline (original):** ~11.5s average
- **Optimized (source index):** 50ms average
- **Improvement:** 99.56% (227x speedup)

### Large Scale (1000 Classes)
- **Baseline (original):** Too slow for practical use (>2 minutes, O(n²) complexity)
- **Optimized (source index):** 844ms average
- **Improvement:** Not measurable due to baseline timeout, but estimated >99%

## Running Benchmarks

### Quick Test (50 classes)
```bash
dotnet run --project ClassContextAnalyzer.csproj -- --class-count 50 --iterations 2
```

### Medium Test (200 classes)
```bash
dotnet run --project ClassContextAnalyzer.csproj -- --class-count 200 --iterations 2
```

### Full 1000-Class Test (optimized only)
```bash
dotnet run --project ClassContextAnalyzer.csproj -- --class-count 1000 --iterations 1 --skip-baseline
```

## Identified Bottlenecks

The following bottlenecks were identified and fixed in the original implementation:

1. **Repeated Full-Tree Scans**: For each type reference, `FindAndAnalyzeTypeDefinition` performs a full syntax tree scan of all unprocessed files to locate type definitions.

2. **Linear File Search**: Each type reference triggers a linear search through all files in the project directory using `Directory.GetFiles(..., SearchOption.AllDirectories)`.

3. **Multiple File Reads**: Files are read and parsed multiple times - once for initial analysis and potentially again for each type reference search.

4. **No Type Definition Cache**: There's no index mapping type names to their file locations, forcing repeated expensive searches.

## Optimization Strategy

The source index implementation addresses these bottlenecks by:

1. **Single-Pass Indexing**: Building a comprehensive type→file mapping in one pass through all source files.

2. **O(1) Type Lookup**: Direct dictionary lookup for type definitions instead of O(n) search.

3. **Reduced File I/O**: Each file is read and parsed exactly once during index building.

4. **Preserved Output Behavior**: The optimization maintains the same analysis results and output format while improving performance.