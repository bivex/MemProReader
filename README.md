 * MemPro Reader - Memory Analysis Tool for .mempro Files
 * 
 * BRIEF DESCRIPTION:
 * Program reads MemPro (.mempro) files and creates detailed memory analysis
 * with focus on memory leaks, allocations, and performance. Generates JSON files:
 * 1. Memory analysis - detailed information about memory usage
 * 2. Allocations analysis - summary statistics for all allocations
 * 
 * USAGE:
 * dotnet run --project MemProReader "path/to/file.mempro"
 * 
 * OUTPUT FILES:
 * - filename_memory_analysis.json    - memory analysis (large file)
 * - filename_allocations_analysis.json - allocations analysis (compact file)
 * 
 * KEY METRICS:
 * - TotalAllocations, TotalSize, AvgSizePerAllocation
 * - LeakCount, LeakSize, MemoryFragmentation
 * - AllocationPatterns, MemoryHotspots
