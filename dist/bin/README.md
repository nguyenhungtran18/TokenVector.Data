# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![.NET 8](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![C# 12](https://img.shields.io/badge/C%23-12.0-blue.svg)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-51%2F51%20Passed-brightgreen.svg)]()

**`TokenVector.Data.dll`** is an ultra-high-performance columnar DataFrame and tabular data processing library written in **C# 12 / .NET 8 LTS** specifically engineered for the **TokenVector** native programming language ecosystem and .NET CIL AOT.

The library implements Apache Arrow-aligned contiguous memory layouts, true No-GIL CPU parallelization, 64-bit Bitboard null masks (`BitmapMask`), SIMD hardware acceleration, high-frequency financial `AsOfJoin`, multi-threaded CSV/JSON parsing, Feather/Arrow IPC streaming, and zero-copy bridges to `TokenVector.Numerics.NDArray<T>` and `TokenVector.Numerics.Autograd.Tensor<T>`.

---

## 📋 Architectural Highlights

| Feature | TokenVector.Data (.NET 8 / C# 12) |
| :--- | :--- |
| **Columnar Memory Storage** | Contiguous unmanaged buffers (`Column<T>`), Arrow-aligned UTF-8 variable-length buffer + offset array (`StringColumn`) |
| **Null Representation** | 64-bit Bitboard `BitmapMask` utilizing hardware POPCNT and SIMD bitwise logic |
| **No-GIL Multithreading** | Parallel execution across 100% CPU cores via `Parallel.For` and TPL in GroupBy, Hash Join, CSV Parsing, and Filtering |
| **Hardware Acceleration** | Vectorized SIMD kernels (AVX2, SSE4, FMA) for arithmetic, window functions, and statistics |
| **Relational Operations** | Multi-key Parallel Radix Hash Join (`Inner`, `Left`, `Right`, `FullOuter`, `Cross`) and high-frequency financial **`AsOfJoin`** |
| **Tabular Reshaping** | `Pivot` (long-to-wide), `Melt` (wide-to-long), `ConcatVertical`, `ConcatHorizontal` |
| **Streaming & I/O** | Multi-threaded CSV parser with schema inference, streaming NDJSON reader, and Apache Arrow IPC Feather streaming |
| **Out-Of-Core Processing** | Memory-mapped DataFrame (`OutOfCoreDataFrame`) for querying datasets larger than physical RAM |
| **Zero-Copy Tensor Bridge** | Instant, zero-copy conversion between `DataFrame`/`Series` and `TokenVector.Numerics` `NDArray<T>` / `Tensor<T>` |

---

## 📊 Architectural Competitor Comparison Matrix

| Feature | TokenVector.Data | Polars | DuckDB | Pandas 2.x | MS.Data.Analysis |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Language** | C# 12 / .NET 8 AOT | Rust Native | C++11 Native | Python / C | C# (.NET Core) |
| **Memory** | Arrow Columnar | Arrow Columnar | Vector Chunked | Row/Col Block | Object / Array |
| **Threads** | **No-GIL (100% Cores)** | Rayon Stealing | Vector Pipes | **GIL (1 Core)** | Limited |
| **Null Mask** | **64-bit Bitboard** | Arrow Validity | Validity Mask | `NaN` / Byte Mask | BitArray |
| **SIMD** | AVX2 / SSE4 / FMA | AVX2 / AVX-512 | Vector Chunks | NumPy / C | Partial |
| **AsOf Join** | **Native Built-in** | `join_asof` | SQL Engine | `merge_asof` | ❌ No |
| **Tensor/AI** | **Zero-Copy Bridge** | FFI Copy | Arrow Export | Copy / View | ❌ No |
| **Out-Of-Core** | **MemoryMappedFiles** | Streaming | Disk Spilling | ❌ No | ❌ No |
| **Binary I/O** | Arrow IPC Feather | Arrow / Parquet | Parquet / DuckDB | Feather / Parquet | Arrow |

---

## ⚡ Performance Benchmarks (5,000,000 Rows)

Tested on 16 Logical CPU Cores, .NET 8.0 LTS Release Mode (see full details in [BENCHMARKS.md](BENCHMARKS.md)):

| Workload Benchmark | Dataset Size | Latency (ms) | Throughput (Rows/sec) | Memory Overhead |
| :--- | :--- | :--- | :--- | :--- |
| **Vectorized SIMD Arithmetic** (`colA * 2.5 + colB`) | 5,000,000 rows | **11.2 ms** | **~446.4 M rows/sec** | Minimal (Zero GC) |
| **Parallel Predicate Filtering** (`val_a > 500.0`) | 5,000,000 rows | **18.5 ms** | **~270.2 M rows/sec** | Bitboard mask (625 KB) |
| **Multi-Column Radix GroupBy** (8 groups x 4 aggs) | 5,000,000 rows | **42.1 ms** | **~118.7 M rows/sec** | Zero allocation |
| **Vectorized Rolling Mean** (Window=50, O(N)) | 1,000,000 rows | **8.4 ms** | **~119.0 M rows/sec** | Output column buffer |
| **Parallel Radix Hash Join** (1M rows x 50K keys) | 1,000,000 rows | **28.6 ms** | **~34.9 M rows/sec** | Hash lookup table |
| **Financial AsOf Time-Series Join** | 500,000 rows | **14.2 ms** | **~35.2 M rows/sec** | Minimal |
| **Multi-Threaded CSV Parsing** (3 columns, quotes) | 500,000 rows | **31.5 ms** | **~15.8 M rows/sec** | Streaming buffer |
| **Arrow IPC Feather Serialization** | 1,000,000 rows | **9.1 ms** | **~109.8 M rows/sec** | Contiguous payload |

---

## 🧪 Verification & Quality Assurance

All **51/51 unit tests** pass with 100% green status in Release mode:
```powershell
dotnet test TokenVector.Data.sln -c Release
```
```text
Passed!  - Failed: 0, Passed: 51, Skipped: 0, Total: 51, Duration: 47 ms - TokenVector.Data.Tests.dll (net8.0)
```

---

## 🚀 Quick Start (C#)

```csharp
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Data.Compute;
using TokenVector.Data.Relational;
using TokenVector.Data.Interop;

// 1. Create a DataFrame
var df = new DataFrame(
    Series.FromValues("user_id", new[] { 1, 2, 3, 4, 5 }),
    Series.FromStrings("dept", new[] { "IT", "HR", "IT", "Sales", "HR" }),
    Series.FromValues("salary", new[] { 75000.0, 52000.0, 88000.0, 61000.0, 58000.0 })
);

// 2. Parallel GroupBy & Aggregation
var summary = df.GroupBy("dept").Agg(
    Agg.Count("salary", "headcount"),
    Agg.Mean("salary", "avg_salary"),
    Agg.Max("salary", "max_salary")
);

// 3. High-Frequency AsOf Join
var matched = trades.AsOfJoin(quotes, leftOn: "timestamp", rightOn: "timestamp", direction: AsOfDirection.Backward);

// 4. Zero-Copy bridge to TokenVector.Numerics NDArray
var numericMatrix = df.ToNDArray<double>(new[] { "user_id", "salary" });
```

---

## 💻 Quick Start (TokenVector Native Language)

```tokenvector
import tv.data as td
from tv.data import Agg

# Read CSV and perform grouped analytics
df = td.read_csv("employees.csv")
report = df.groupby("department").agg(
    Agg.mean("salary", out_name="avg_salary"),
    Agg.count("id", out_name="headcount")
)
print(report)
```

For complete documentation, see [User Guide (English)](USER_GUIDE.md), [Benchmarks](BENCHMARKS.md), and [Hướng dẫn sử dụng (Tiếng Việt)](USER_GUIDE_VI.md).
