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

For complete documentation, see [User Guide (English)](USER_GUIDE.md) and [Hướng dẫn sử dụng (Tiếng Việt)](USER_GUIDE_VI.md).
