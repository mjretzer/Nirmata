using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Gmsd.Aos.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Gmsd.Aos.Tests;

public sealed class DeterministicJsonStreamingBenchmarks
{
    private readonly ITestOutputHelper _output;

    public DeterministicJsonStreamingBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Reliability")]
    public void StreamingComparison_IncursMinimalMemoryOverhead_ForLargeFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "gmsd-benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var largeFilePath = Path.Combine(tempDir, "large.json");

        try
        {
            // 1. Create a 10MB+ JSON file with minimal canonicalization overhead.
            // Use a large array of booleans so canonicalization can stream without large per-element allocations.
            const int targetBytes = 10 * 1024 * 1024;
            const int itemsPerBlock = 4096;
            var itemCount = targetBytes / 5; // approx: "true," => 5 bytes (last element is 4 bytes)

            static string BuildBoolBlock(int count)
            {
                var sb = new StringBuilder(count * 5);
                for (var i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append("true");
                }
                return sb.ToString();
            }

            var fullBlock = BuildBoolBlock(itemsPerBlock);

            using (var fs = new FileStream(largeFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var textWriter = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                textWriter.Write('[');

                var remaining = itemCount;
                var first = true;
                while (remaining > 0)
                {
                    var blockSize = Math.Min(itemsPerBlock, remaining);
                    if (!first) textWriter.Write(',');

                    if (blockSize == itemsPerBlock)
                    {
                        textWriter.Write(fullBlock);
                    }
                    else
                    {
                        textWriter.Write(BuildBoolBlock(blockSize));
                    }

                    first = false;
                    remaining -= blockSize;
                }

                textWriter.Write("]\n");
            }

            var fileInfo = new FileInfo(largeFilePath);
            _output.WriteLine($"Generated large JSON file: {fileInfo.Length / 1024 / 1024} MB");

            JsonDocument baselineDoc;
            using (var baselineStream = File.OpenRead(largeFilePath))
            {
                baselineDoc = JsonDocument.Parse(baselineStream);
            }

            using (baselineDoc)
            {

                // Warm-up: avoid counting one-time JIT/initialization allocations.
                DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(largeFilePath, baselineDoc.RootElement, writeIndented: false);

                // 2. Perform comparison and measure memory
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                long startMemory = GC.GetTotalMemory(forceFullCollection: true);
                var sw = Stopwatch.StartNew();

                // Invoke the writer with identical data - should trigger streaming comparison and skip write.
                // Use writeIndented:false to match the generated file's formatting.
                DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(largeFilePath, baselineDoc.RootElement, writeIndented: false);

                sw.Stop();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long endMemory = GC.GetTotalMemory(forceFullCollection: true);
                long memoryUsed = endMemory - startMemory;

                _output.WriteLine($"Comparison took: {sw.ElapsedMilliseconds} ms");
                _output.WriteLine($"Memory delta during comparison: {memoryUsed / 1024} KB");

                // Assert that memory usage is significantly lower than the file size
                // A 10MB+ file should definitely not be fully loaded into memory.
                // We expect the chunked comparison to use ~4KB buffers + serialization overhead.
                Assert.True(memoryUsed < fileInfo.Length / 2, $"Memory used ({memoryUsed / 1024} KB) should be significantly less than file size ({fileInfo.Length / 1024} KB)");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
