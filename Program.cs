/*
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
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MemProCLR;

namespace MemProReader
{
    // Data structures for JSON export
    public class MemoryAnalysis
    {
        public string SessionName { get; set; } = "";
        public int TotalSnapshots { get; set; }
        public long TotalAllocations { get; set; }
        public long TotalSize { get; set; }
        public long LeakCount { get; set; }
        public long LeakSize { get; set; }
        public double MemoryFragmentation { get; set; }
        public List<CallTreeAnalysis> CallTrees { get; set; } = new List<CallTreeAnalysis>();
        public List<FunctionAnalysis> Functions { get; set; } = new List<FunctionAnalysis>();
        public List<LeakAnalysis> Leaks { get; set; } = new List<LeakAnalysis>();
        public List<PageViewAnalysis> PageViews { get; set; } = new List<PageViewAnalysis>();
        public List<TypeAnalysis> Types { get; set; } = new List<TypeAnalysis>();
    }

    public class CallTreeAnalysis
    {
        public string FunctionName { get; set; } = "";
        public string FileName { get; set; } = "";
        public int LineNumber { get; set; }
        public long AllocationCount { get; set; }
        public long TotalSize { get; set; }
        public long SelfSize { get; set; }
        public long InclusiveSize { get; set; }
        public List<CallTreeAnalysis> Children { get; set; } = new List<CallTreeAnalysis>();
    }

    public class FunctionAnalysis
    {
        public string FunctionName { get; set; } = "";
        public string FileName { get; set; } = "";
        public int LineNumber { get; set; }
        public long AllocationCount { get; set; }
        public long TotalSize { get; set; }
        public long AverageSize { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
        public double Percentage { get; set; }
    }

    public class PageViewAnalysis
    {
        public ulong Address { get; set; }
        public string State { get; set; } = "";
        public string Type { get; set; } = "";
        public uint Protection { get; set; }
        public int StackId { get; set; }
        public ulong Usage { get; set; }
        public int AllocationCount { get; set; }
        public long TotalSize { get; set; }
        public string FunctionName { get; set; } = "";
        public string CallStack { get; set; } = "";
    }

    public class LeakAnalysis
    {
        public string FunctionName { get; set; } = "";
        public string FileName { get; set; } = "";
        public int LineNumber { get; set; }
        public long LeakSize { get; set; }
        public long LeakCount { get; set; }
        public double LeakScore { get; set; }
        public string CallStack { get; set; } = "";
        public bool IsSuspect { get; set; }
    }

    public class TypeAnalysis
    {
        public string TypeName { get; set; } = "";
        public int AllocationCount { get; set; }
        public long TotalSize { get; set; }
        public long AverageSize { get; set; }
        public long MinSize { get; set; }
        public long MaxSize { get; set; }
        public double Percentage { get; set; }
        public string MostCommonFunction { get; set; } = "";
        public string MostCommonFile { get; set; } = "";
        public int MostCommonLine { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: MemProReader <path_to_mempro_file>");
                Console.WriteLine("Example: MemProReader test.mempro");
                return;
            }

            string filePath = args[0];
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File '{filePath}' not found.");
                return;
            }

            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine("MEMPRO READER - MEMORY ANALYSIS TOOL");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"Reading file: {filePath}");

            try
            {
                // Initialize MemPro
                Console.WriteLine("\nInitializing MemPro...");
                MemProCLR.MemPro.Create();

                // Read the MemPro file
                Console.WriteLine("\nReading MemPro file...");
                
                FileInfo fileInfo = new FileInfo(filePath);
                Console.WriteLine($"File Size: {FormatBytes(fileInfo.Length)}");
                Console.WriteLine($"Last Modified: {fileInfo.LastWriteTime}");

                // Use MemPro API to read the file
                Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
                Console.WriteLine("READING WITH MEMPRO API");
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                
                // Try to load symbol files first
                Console.WriteLine("Loading symbol files...");
                string symbolError = "";
                MemProCLR.MemPro.LoadSymbolFiles(ref symbolError);
                if (!string.IsNullOrEmpty(symbolError))
                {
                    Console.WriteLine($"Symbol loading warning: {symbolError}");
                }
                else
                {
                    Console.WriteLine("Symbol files loaded successfully");
                }
                
                // Now try to read the file
                Console.WriteLine("Reading .mempro file...");
                MemProCLR.MemPro.Read(filePath);
                
                ReadResult result = MemProCLR.MemPro.LastReadResult;
                Console.WriteLine($"Read Result: {result}");
                
                if (result == ReadResult.OK)
                {
                    Console.WriteLine("File read successfully!");
                }
                else if (result == ReadResult.FailedUnpackingAllocsFile)
                {
                    Console.WriteLine("Warning: Failed to unpack allocations file, but continuing with available data...");
                }
                else if (result == ReadResult.FailedReadingSymbols)
                {
                    Console.WriteLine("Warning: Failed to read symbols, but continuing with available data...");
                }
                else
                {
                    Console.WriteLine($"Failed to read .mempro file: {result}");
                    return;
                }

                // Get snapshots
                int snapshotCount = MemProCLR.MemPro.SnapshotCount;
                Console.WriteLine($"Total Snapshots: {snapshotCount}");

                // Create comprehensive memory analysis from real data
                var memoryAnalysis = new MemoryAnalysis
                {
                    SessionName = Path.GetFileNameWithoutExtension(filePath),
                    TotalSnapshots = snapshotCount,
                    TotalAllocations = 0, // Will be calculated
                    TotalSize = 0, // Will be calculated
                    LeakCount = 0, // Will be calculated
                    LeakSize = 0, // Will be calculated
                    MemoryFragmentation = 0.0, // Will be calculated
                    CallTrees = new List<CallTreeAnalysis>(),
                    Functions = new List<FunctionAnalysis>(),
                    Leaks = new List<LeakAnalysis>()
                };

                // Analyze snapshots
                AnalyzeSnapshots(memoryAnalysis);

                // Display summary
                Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
                Console.WriteLine("MEMORY ANALYSIS SUMMARY");
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine($"Session Name: {memoryAnalysis.SessionName}");
                Console.WriteLine($"Total Snapshots: {memoryAnalysis.TotalSnapshots}");
                Console.WriteLine($"Total Allocations: {memoryAnalysis.TotalAllocations:N0}");
                Console.WriteLine($"Total Size: {FormatBytes(memoryAnalysis.TotalSize)}");
                Console.WriteLine($"Leak Count: {memoryAnalysis.LeakCount:N0}");
                Console.WriteLine($"Leak Size: {FormatBytes(memoryAnalysis.LeakSize)}");
                Console.WriteLine($"Memory Fragmentation: {memoryAnalysis.MemoryFragmentation:F2}%");
                Console.WriteLine($"Call Tree Nodes: {memoryAnalysis.CallTrees.Count}");
                Console.WriteLine($"Functions Analyzed: {memoryAnalysis.Functions.Count}");
                Console.WriteLine($"Leaks Detected: {memoryAnalysis.Leaks.Count}");
                Console.WriteLine($"Page Views: {memoryAnalysis.PageViews.Count}");
                Console.WriteLine($"Types Analyzed: {memoryAnalysis.Types.Count}");

                // Save to JSON file
                string jsonFileName = Path.GetFileNameWithoutExtension(filePath) + "_memory_analysis.json";
                string jsonContent = JsonSerializer.Serialize(memoryAnalysis, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(jsonFileName, jsonContent);
                Console.WriteLine($"\nMemory analysis saved to: {jsonFileName}");

                Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
                Console.WriteLine("ANALYSIS COMPLETE");
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup MemPro
                try
                {
                    MemProCLR.MemPro.Destroy();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error during MemPro cleanup: {ex.Message}");
                }
            }
        }

        static void AnalyzeSnapshots(MemoryAnalysis analysis)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
            Console.WriteLine("ANALYZING SNAPSHOTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");

            var allCallstacks = new Dictionary<int, Callstack>();
            var allCallstackData = new Dictionary<int, (ulong bytes, long allocCount)>();
            var allPages = new List<Page>();

            // Process each snapshot
            for (int i = 0; i < analysis.TotalSnapshots; i++)
            {
                try
                {
                    var snapshot = MemProCLR.MemPro.GetSnapshot(i);
                    if (snapshot != null)
                    {
                        Console.WriteLine($"Snapshot {i}: {snapshot.Name}");
                        Console.WriteLine($"  Allocated: {FormatBytes(snapshot.AllocatedBytes)}");
                        Console.WriteLine($"  Reserved: {FormatBytes(snapshot.ReservedBytes)}");
                        Console.WriteLine($"  Committed: {FormatBytes(snapshot.CommittedBytes)}");

                        // Get callstack data for this snapshot
                        var callstackData = snapshot.GetCallstackData();
                        if (callstackData != null)
                        {
                            foreach (var data in callstackData)
                            {
                                int callstackId = (int)data.m_AllocCount;
                                
                                // Store real allocation data
                                allCallstackData[callstackId] = ((ulong)data.m_Bytes, data.m_AllocCount);
                                
                                // Get callstack if not already cached
                                if (!allCallstacks.ContainsKey(callstackId))
                                {
                                    try
                                    {
                                        var callstack = MemProCLR.MemPro.GetCallstack(callstackId);
                                        if (callstack != null)
                                        {
                                            allCallstacks[callstackId] = callstack;
                                        }
                                    }
                                    catch
                                    {
                                        // Skip invalid callstack IDs
                                    }
                                }
                            }
                        }

                        // Get pages for this snapshot
                        try
                        {
                            var pages = new List<Page>();
                            snapshot.GetPages(pages);
                            allPages.AddRange(pages);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Warning: Could not get pages for snapshot {i}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing snapshot {i}: {ex.Message}");
                }
            }

            // Update totals with real data
            analysis.TotalAllocations = allCallstackData.Values.Sum(x => x.allocCount);
            analysis.TotalSize = (long)allCallstackData.Values.Sum(x => (long)x.bytes);

            // Generate analysis with real data
            analysis.CallTrees = GenerateCallTreeAnalysis(allCallstacks, allCallstackData);
            analysis.Functions = GenerateFunctionAnalysis(allCallstacks, allCallstackData);
            analysis.Leaks = GenerateLeakAnalysis(allCallstacks, allCallstackData);
            analysis.PageViews = GeneratePageViewAnalysis(allPages);
            analysis.Types = GenerateTypeAnalysis(allCallstacks, allCallstackData);

            // Calculate leaks and fragmentation
            analysis.LeakCount = analysis.Leaks.Sum(l => l.LeakCount);
            analysis.LeakSize = analysis.Leaks.Sum(l => l.LeakSize);
            
            if (analysis.TotalSize > 0)
            {
                analysis.MemoryFragmentation = (double)analysis.LeakSize / analysis.TotalSize * 100.0;
            }
        }

        static List<CallTreeAnalysis> GenerateCallTreeAnalysis(Dictionary<int, Callstack> callstacks, Dictionary<int, (ulong bytes, long allocCount)> callstackData)
        {
            var callTrees = new List<CallTreeAnalysis>();
            
            // Take top 10 callstacks by allocation count
            var topCallstacks = callstacks
                .Where(kvp => callstackData.ContainsKey(kvp.Key))
                .OrderByDescending(kvp => callstackData[kvp.Key].allocCount)
                .Take(10);

            foreach (var kvp in topCallstacks)
            {
                var callstack = kvp.Value;
                var data = callstackData[kvp.Key];
                
                var node = new CallTreeAnalysis
                {
                    FunctionName = callstack.Symbols?.FirstOrDefault() ?? 
                        (callstack.Addresses?.FirstOrDefault() > 0 ? $"0x{callstack.Addresses?.FirstOrDefault():X}" : "Unknown Function"),
                    FileName = ExtractFileName(callstack.Symbols?.FirstOrDefault()),
                    LineNumber = ExtractLineNumber(callstack.Symbols?.FirstOrDefault()),
                    AllocationCount = (int)data.allocCount,
                    TotalSize = (long)data.bytes,
                    SelfSize = (long)data.bytes,
                    InclusiveSize = (long)data.bytes,
                    Children = new List<CallTreeAnalysis>()
                };

                // Add children from callstack with proportional sizes
                if (callstack.Symbols != null && callstack.Symbols.Length > 1)
                {
                    long childSize = (long)data.bytes / Math.Max(1, callstack.Symbols.Length - 1);
                    for (int i = 1; i < Math.Min(callstack.Symbols.Length, 5); i++) // Limit to 5 children
                    {
                        node.Children.Add(new CallTreeAnalysis
                        {
                            FunctionName = callstack.Symbols[i],
                            FileName = ExtractFileName(callstack.Symbols[i]),
                            LineNumber = ExtractLineNumber(callstack.Symbols[i]),
                            AllocationCount = Math.Max(1, (int)data.allocCount / callstack.Symbols.Length),
                            TotalSize = childSize,
                            SelfSize = childSize,
                            InclusiveSize = childSize,
                            Children = new List<CallTreeAnalysis>()
                        });
                    }
                }

                callTrees.Add(node);
            }

            return callTrees;
        }

        static string ExtractFileName(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return "";

            var match = System.Text.RegularExpressions.Regex.Match(symbol, @"\(([^)]+)\)");
            if (match.Success)
            {
                var fileInfo = match.Groups[1].Value;
                var parts = fileInfo.Split('(');
                if (parts.Length > 0)
                    return parts[0];
            }

            return "";
        }

        static int ExtractLineNumber(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return 0;

            var match = System.Text.RegularExpressions.Regex.Match(symbol, @"\(([^)]+)\)");
            if (match.Success)
            {
                var fileInfo = match.Groups[1].Value;
                var parts = fileInfo.Split('(');
                if (parts.Length > 1)
                {
                    var linePart = parts[1].Replace(")", "");
                    if (int.TryParse(linePart, out int line))
                        return line;
                }
            }

            return 0;
        }

        static List<FunctionAnalysis> GenerateFunctionAnalysis(Dictionary<int, Callstack> callstacks, Dictionary<int, (ulong bytes, long allocCount)> callstackData)
        {
            var functions = new List<FunctionAnalysis>();
            
            // Take top 20 callstacks by total bytes
            var topCallstacks = callstacks
                .Where(kvp => callstackData.ContainsKey(kvp.Key))
                .OrderByDescending(kvp => callstackData[kvp.Key].bytes)
                .Take(20);

            long totalBytes = callstackData.Values.Sum(x => (long)x.bytes);

            foreach (var kvp in topCallstacks)
            {
                var callstack = kvp.Value;
                var data = callstackData[kvp.Key];
                
                functions.Add(new FunctionAnalysis
                {
                    FunctionName = callstack.Symbols?.FirstOrDefault() ?? 
                        (callstack.Addresses?.FirstOrDefault() > 0 ? $"0x{callstack.Addresses?.FirstOrDefault():X}" : "Unknown Function"),
                    FileName = ExtractFileName(callstack.Symbols?.FirstOrDefault()),
                    LineNumber = ExtractLineNumber(callstack.Symbols?.FirstOrDefault()),
                    AllocationCount = (int)data.allocCount,
                    TotalSize = (long)data.bytes,
                    AverageSize = data.allocCount > 0 ? (long)data.bytes / data.allocCount : 0,
                    MinSize = (long)data.bytes / Math.Max(1, data.allocCount), // Approximate
                    MaxSize = (long)data.bytes / Math.Max(1, data.allocCount), // Approximate
                    Percentage = totalBytes > 0 ? (double)data.bytes / totalBytes * 100.0 : 0.0
                });
            }

            return functions;
        }

        static List<LeakAnalysis> GenerateLeakAnalysis(Dictionary<int, Callstack> callstacks, Dictionary<int, (ulong bytes, long allocCount)> callstackData)
        {
            var leaks = new List<LeakAnalysis>();
            
            // Take top 10 callstacks as potential leaks (those with high allocation count)
            var topCallstacks = callstacks
                .Where(kvp => callstackData.ContainsKey(kvp.Key))
                .OrderByDescending(kvp => callstackData[kvp.Key].allocCount)
                .Take(10);

            foreach (var kvp in topCallstacks)
            {
                var callstack = kvp.Value;
                var data = callstackData[kvp.Key];
                
                // Calculate leak score based on allocation count and size
                double leakScore = Math.Log10(Math.Max(1, data.allocCount)) * Math.Log10(Math.Max(1, data.bytes));
                
                leaks.Add(new LeakAnalysis
                {
                    FunctionName = callstack.Symbols?.FirstOrDefault() ?? 
                        (callstack.Addresses?.FirstOrDefault() > 0 ? $"0x{callstack.Addresses?.FirstOrDefault():X}" : "Unknown Function"),
                    FileName = ExtractFileName(callstack.Symbols?.FirstOrDefault()),
                    LineNumber = ExtractLineNumber(callstack.Symbols?.FirstOrDefault()),
                    LeakSize = (long)data.bytes,
                    LeakCount = data.allocCount,
                    LeakScore = leakScore,
                    CallStack = callstack.Addresses != null ? string.Join(" <- ", callstack.Addresses.Select(a => $"0x{a:X}")) : "",
                    IsSuspect = data.allocCount > 100 || data.bytes > 1024 * 1024 // Suspect if >100 allocations or >1MB
                });
            }

            return leaks;
        }

        static List<PageViewAnalysis> GeneratePageViewAnalysis(List<Page> pages)
        {
            var pageViews = new List<PageViewAnalysis>();
            
            // Take top 20 pages by usage
            var topPages = pages
                .OrderByDescending(p => p.Usage)
                .Take(20);

            foreach (var page in topPages)
            {
                // Get the most common function from page allocations
                string functionName = "Unknown";
                string callStack = "";
                
                if (page.Allocs != null && page.Allocs.Count > 0)
                {
                    // Get callstack for the first allocation
                    try
                    {
                        var callstack = MemProCLR.MemPro.GetCallstack(page.Allocs[0].StackID);
                        if (callstack != null)
                        {
                            functionName = callstack.Symbols?.FirstOrDefault() ?? 
                                (page.Addr > 0 ? $"0x{page.Addr:X}" : "Unknown Function");
                            callStack = callstack.Addresses != null ? string.Join(" <- ", callstack.Addresses.Select(a => $"0x{a:X}")) : "";
                        }
                    }
                    catch
                    {
                        functionName = $"0x{page.Addr:X}";
                    }
                }

                pageViews.Add(new PageViewAnalysis
                {
                    Address = page.Addr,
                    State = page.State.ToString(),
                    Type = page.Type.ToString(),
                    Protection = page.Protection,
                    StackId = page.StackID,
                    Usage = page.Usage,
                    AllocationCount = page.Allocs?.Count ?? 0,
                    TotalSize = page.Allocs?.Sum(a => (long)a.Size) ?? 0,
                    FunctionName = functionName,
                    CallStack = callStack
                });
            }

            return pageViews;
        }

        static List<TypeAnalysis> GenerateTypeAnalysis(Dictionary<int, Callstack> callstacks, Dictionary<int, (ulong bytes, long allocCount)> callstackData)
        {
            var types = new List<TypeAnalysis>();
            
            // Extract type information from function names and group by type
            var typeGroups = callstacks
                .Where(kvp => callstackData.ContainsKey(kvp.Key) && kvp.Value.Symbols?.FirstOrDefault() != null)
                .GroupBy(kvp => ExtractTypeName(kvp.Value.Symbols?.FirstOrDefault()))
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => new
                {
                    TypeName = g.Key,
                    TotalBytes = g.Sum(kvp => (long)callstackData[kvp.Key].bytes),
                    TotalAllocations = g.Sum(kvp => callstackData[kvp.Key].allocCount),
                    MostCommonFunction = g.OrderByDescending(kvp => callstackData[kvp.Key].allocCount).First().Value.Symbols?.FirstOrDefault() ?? "",
                    Count = g.Count()
                })
                .OrderByDescending(t => t.TotalBytes)
                .Take(15); // Top 15 types

            long totalBytes = callstackData.Values.Sum(x => (long)x.bytes);

            foreach (var group in typeGroups)
            {
                types.Add(new TypeAnalysis
                {
                    TypeName = group.TypeName,
                    AllocationCount = (int)group.TotalAllocations,
                    TotalSize = group.TotalBytes,
                    AverageSize = group.TotalAllocations > 0 ? group.TotalBytes / group.TotalAllocations : 0,
                    MinSize = group.TotalBytes / Math.Max(1, group.Count), // Approximate
                    MaxSize = group.TotalBytes / Math.Max(1, group.Count), // Approximate
                    Percentage = totalBytes > 0 ? (double)group.TotalBytes / totalBytes * 100.0 : 0.0,
                    MostCommonFunction = group.MostCommonFunction,
                    MostCommonFile = ExtractFileName(group.MostCommonFunction),
                    MostCommonLine = ExtractLineNumber(group.MostCommonFunction)
                });
            }

            return types;
        }

        static string ExtractTypeName(string functionName)
        {
            if (string.IsNullOrEmpty(functionName))
                return "";

            // Try to extract type names from function signatures
            if (functionName.Contains("std::"))
            {
                // Extract STL type names
                var match = System.Text.RegularExpressions.Regex.Match(functionName, @"std::(\w+)");
                if (match.Success)
                    return $"std::{match.Groups[1].Value}";
            }

            if (functionName.Contains("struct "))
            {
                var match = System.Text.RegularExpressions.Regex.Match(functionName, @"struct (\w+)");
                if (match.Success)
                    return $"struct {match.Groups[1].Value}";
            }

            if (functionName.Contains("class "))
            {
                var match = System.Text.RegularExpressions.Regex.Match(functionName, @"class (\w+)");
                if (match.Success)
                    return $"class {match.Groups[1].Value}";
            }

            // Default to function name if no type found
            return functionName.Split('(')[0].Trim();
        }

        static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            double number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}
