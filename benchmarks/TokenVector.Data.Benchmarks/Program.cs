using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TokenVector.Data.Common;
using TokenVector.Data.Compute;
using TokenVector.Data.Core;
using TokenVector.Data.IO;
using TokenVector.Data.Relational;

namespace TokenVector.Data.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  TOKENVECTOR.DATA PERFORMANCE BENCHMARK SUITE");
        Console.WriteLine($"  Hardware: {Environment.ProcessorCount} Logical Cores | .NET 8.0 LTS CIL AOT");
        Console.WriteLine("================================================================================\n");

        RunSyntheticBenchmarks();
    }

    private static void RunSyntheticBenchmarks()
    {
        const int RowCount1M = 1_000_000;
        const int RowCount5M = 5_000_000;

        Console.WriteLine($"[1] Allocating & Generating Test Dataset ({RowCount5M:N0} rows)...");
        var rand = new Random(42);
        var colA = new double[RowCount5M];
        var colB = new double[RowCount5M];
        var categories = new[] { "IT", "Finance", "Healthcare", "Retail", "Manufacturing", "Energy", "Telecom", "Media" };
        var depts = new string[RowCount5M];

        for (int i = 0; i < RowCount5M; i++)
        {
            colA[i] = rand.NextDouble() * 1000.0;
            colB[i] = rand.NextDouble() * 50.0 + 1.0;
            depts[i] = categories[rand.Next(categories.Length)];
        }

        var df5M = new DataFrame(
            Series.FromValues("val_a", colA),
            Series.FromValues("val_b", colB),
            Series.FromStrings("department", depts)
        );

        Console.WriteLine($"    Dataset created: {df5M.RowCount:N0} rows x {df5M.ColumnCount} cols.\n");

        // Benchmark 1: Vectorized Math (colA * 2.5 + colB)
        BenchmarkWorkload("1. Vectorized SIMD Arithmetic (colA * 2.5 + colB)", RowCount5M, () =>
        {
            var sA = df5M["val_a"];
            var sB = df5M["val_b"];
            var result = (sA * 2.5) + sB;
            return result.Length;
        });

        // Benchmark 2: Fast Predicate Filter (val_a > 500.0)
        BenchmarkWorkload("2. Parallel Predicate Filter (val_a > 500.0)", RowCount5M, () =>
        {
            var mask = df5M["val_a"] > 500.0;
            var filtered = df5M[mask];
            return filtered.RowCount;
        });

        // Benchmark 3: Multi-Threaded GroupBy Aggregations (8 groups x 5M rows)
        BenchmarkWorkload("3. Parallel Radix GroupBy Aggregations (Count, Sum, Mean, Max)", RowCount5M, () =>
        {
            var grouped = df5M.GroupBy("department").Agg(
                Agg.Count("val_a", "count"),
                Agg.Sum("val_a", "sum"),
                Agg.Mean("val_a", "avg"),
                Agg.Max("val_b", "max")
            );
            return grouped.RowCount;
        });

        // Benchmark 4: Window Functions (Rolling Mean window=50 on 1M rows)
        var df1M = df5M.Head(RowCount1M);
        BenchmarkWorkload("4. Vectorized Rolling Mean (Window=50, 1M rows)", RowCount1M, () =>
        {
            var roll = df1M["val_a"].RollingMean(50);
            return roll.Length;
        });

        // Benchmark 5: Parallel Hash Join (1,000,000 rows x 50,000 rows)
        var leftJoinDf = new DataFrame(
            Series.FromValues("id", Enumerable.Range(0, 1_000_000).Select(x => x % 50_000).ToArray()),
            Series.FromValues("amount", Enumerable.Range(0, 1_000_000).Select(x => (double)x).ToArray())
        );
        var rightJoinDf = new DataFrame(
            Series.FromValues("id", Enumerable.Range(0, 50_000).ToArray()),
            Series.FromStrings("category", Enumerable.Range(0, 50_000).Select(x => $"Cat_{x % 100}").ToArray())
        );

        BenchmarkWorkload("5. Parallel Hash Join (1M rows x 50K keys)", 1_000_000, () =>
        {
            var joined = leftJoinDf.Join(rightJoinDf, "id", "id", JoinType.Inner);
            return joined.RowCount;
        });

        // Benchmark 6: High-Frequency Financial AsOf Join (500,000 trades x 200,000 quotes)
        const int TradeCount = 500_000;
        const int QuoteCount = 200_000;
        var trades = new DataFrame(
            Series.FromValues("time", Enumerable.Range(0, TradeCount).Select(i => (double)(i * 10)).ToArray()),
            Series.FromValues("qty", Enumerable.Range(0, TradeCount).Select(i => i * 100).ToArray())
        );
        var quotes = new DataFrame(
            Series.FromValues("time", Enumerable.Range(0, QuoteCount).Select(i => (double)(i * 25 + 5)).ToArray()),
            Series.FromValues("price", Enumerable.Range(0, QuoteCount).Select(i => 100.0 + i * 0.01).ToArray())
        );

        BenchmarkWorkload("6. Financial AsOf Join (500K trades x 200K quotes)", TradeCount, () =>
        {
            var matched = trades.AsOfJoin(quotes, leftOn: "time", rightOn: "time", direction: AsOfDirection.Backward);
            return matched.RowCount;
        });

        // Benchmark 7: CSV Serialization & Parallel Deserialization (500,000 rows)
        string csvTemp = Path.Combine(Path.GetTempPath(), "bench_500k.csv");
        var df500k = df5M.Head(500_000);
        FastCsvWriter.WriteFile(df500k, csvTemp);

        BenchmarkWorkload("7. Multi-Threaded CSV Parsing (500K rows, 3 cols)", 500_000, () =>
        {
            var loaded = FastCsvReader.ReadFile(csvTemp);
            return loaded.RowCount;
        });

        if (File.Exists(csvTemp)) File.Delete(csvTemp);

        // Benchmark 8: Apache Arrow IPC Feather Serialization (1,000,000 rows)
        BenchmarkWorkload("8. Arrow IPC Feather Stream Serialization (1M rows)", 1_000_000, () =>
        {
            byte[] bytes = ArrowIpcEngine.WriteToBytes(df1M);
            return bytes.Length;
        });

        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  BENCHMARK SUITE COMPLETED SUCCESSFULLY");
        Console.WriteLine("================================================================================");
    }

    private static void BenchmarkWorkload(string name, int rowCount, Func<int> action)
    {
        // Warmup
        action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memBefore = GC.GetTotalMemory(forceFullCollection: true);
        var sw = Stopwatch.StartNew();

        int iterations = 3;
        int result = 0;
        for (int i = 0; i < iterations; i++)
        {
            result = action();
        }

        sw.Stop();
        long memAfter = GC.GetTotalMemory(forceFullCollection: false);

        double totalSeconds = sw.Elapsed.TotalSeconds;
        double avgSeconds = totalSeconds / iterations;
        double avgMs = avgSeconds * 1000.0;
        double rowsPerSec = rowCount / avgSeconds;
        double mRowsPerSec = rowsPerSec / 1_000_000.0;
        long memAllocMb = Math.Max(0, memAfter - memBefore) / (1024 * 1024);

        Console.WriteLine($"▶ {name}");
        Console.WriteLine($"   Latency:     {avgMs,8:F2} ms");
        Console.WriteLine($"   Throughput:  {mRowsPerSec,8:F2} M rows/sec ({rowsPerSec,12:N0} rows/s)");
        Console.WriteLine($"   Memory Delta:{memAllocMb,8:N0} MB | Return Check: {result:N0}");
        Console.WriteLine();
    }
}
