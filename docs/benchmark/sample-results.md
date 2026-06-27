# C# Class Context Analyzer Benchmark Report

**Generated:** 2026-06-27 19:58:04
**Test ID:** 20260627_195803

## Test Configuration

- **Class Count:** 50
- **Iterations:** 2
- **Dependency Pattern:** Chain + cross-references
- **Baseline Skipped:** False

## Results Summary

| Metric | Baseline (Original) | Optimized (Source Index) | Improvement |
|--------|-------------------|------------------------|-------------|
| Average Time | 1179.50ms | 12.50ms | 98.94% |
| Min Time | 665ms | 12ms | - |
| Max Time | 1694ms | 13ms | - |
| Speedup | - | 94.36x | - |

**Overall Performance Improvement:** 98.94% (94.36x faster)

## Detailed Results

### Baseline (Original Implementation)

| Iteration | Time (ms) | Files Analyzed | Type References | Success |
|-----------|-----------|----------------|------------------|---------|
| 1 | 1694 | 26 | 206 | ✓ |
| 2 | 665 | 26 | 206 | ✓ |

### Optimized (Source Index Implementation)

| Iteration | Time (ms) | Files Analyzed | Type References | Success |
|-----------|-----------|----------------|------------------|---------|
| 1 | 12 | 26 | 206 | ✓ |
| 2 | 13 | 26 | 206 | ✓ |

## Identified Bottlenecks

The following bottlenecks were identified in the original implementation:

1. **Repeated Full-Tree Scans**: For each type reference, `FindAndAnalyzeTypeDefinition` performs
   a full syntax tree scan of all unprocessed files to locate type definitions.

2. **Linear File Search**: Each type reference triggers a linear search through all files in
   the project directory using `Directory.GetFiles(..., SearchOption.AllDirectories)`.

3. **Multiple File Reads**: Files are read and parsed multiple times - once for initial analysis
   and potentially again for each type reference search.

4. **No Type Definition Cache**: There's no index mapping type names to their file locations,
   forcing repeated expensive searches.

## Optimization Strategy

The source index implementation addresses these bottlenecks by:

1. **Single-Pass Indexing**: Building a comprehensive type→file mapping in one pass through
   all source files.

2. **O(1) Type Lookup**: Direct dictionary lookup for type definitions instead of O(n) search.

3. **Reduced File I/O**: Each file is read and parsed exactly once during index building.

4. **Preserved Output Behavior**: The optimization maintains the same analysis results and
   output format while improving performance.

