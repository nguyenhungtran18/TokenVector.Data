# TokenVector.Data - Release Notes

[ 🇬🇧 English ](RELEASE_NOTES.md) | [ 🇻🇳 Tiếng Việt ](RELEASE_NOTES_VI.md)

---

## 🚀 Version 1.0.0 (2026-09-13) - Initial Production Release

**`TokenVector.Data`** v1.0.0 is the foundational release of the ultra-high-performance columnar DataFrame and tabular data processing library, engineered specifically for the **TokenVector** programming language ecosystem and .NET 8 LTS CIL AOT compiler.

---

### 🌟 Key Highlights & Architectural Features

#### 1. Columnar Memory Storage (Apache Arrow-Aligned)
* **Typed Contiguous Columns (`Column<T>`):** Flat unmanaged memory vectors for numeric and boolean primitives maximizing CPU L1/L2/L3 cache locality.
* **Arrow UTF-8 Variable-Length Binary Layout (`StringColumn`):** Flat contiguous `byte[]` buffer + monotonically increasing `int[]` offset vectors, eliminating GC heap fragmentation.
* **Multi-Chunk Memory Arrays (`ChunkedArray`):** Zero-allocation concatenation and streaming data batching.

#### 2. 64-bit Bitboard Validity Tracking (`BitmapMask`)
* Word-aligned 64-bit bitboard representation for null tracking (bit `1` = valid, bit `0` = null).
* Hardware-accelerated bit counting via POPCNT (`BitOperations.PopCount`) and bitwise operations (`&`, `|`, `^`, `~`).
* Fast $O(N)$ set-bit extraction (`ToIndices()`) with bit-skipping trailing-zeros optimization.

#### 3. True No-GIL Parallelism & Hardware SIMD Acceleration
* **SIMD Vector Math (`VectorMath`):** Vectorized arithmetic (`+`, `-`, `*`, `/`, `%`, `Pow`), unary functions (`Abs`, `Sqrt`, `Exp`, `Log`), and comparisons (`>`, `>=`, `<`, `<=`, `==`, `!=`).
* **High-Throughput Window Functions (`WindowFunctions`):** Multi-threaded rolling mean, rolling sum, rolling std, lag/lead (`Shift`), discrete differences (`Diff`), cumulative sum (`CumSum`), and numerical ranking (`Rank`).
* **Statistical Aggregations (`Aggregations`):** Vectorized `Sum`, `Mean`, `Min`, `Max`, `Median`, `Std`, `Variance`, `Quantile` with zero-allocation null skipping.

#### 4. Advanced Relational & Reshaping Engines
* **Parallel Radix Hash Join (`JoinEngine`):** Multi-key joins supporting `Inner`, `Left`, `Right`, `FullOuter`, and `Cross` joins.
* **High-Frequency Financial `AsOfJoin`:** Fast inexact time-series matching supporting `Backward`, `Forward`, and `Nearest` directions with configurable timestamp tolerance and group-by partitions.
* **Multi-Column Parallel GroupBy (`GroupByEngine`):** Hash-partitioned grouping with multi-expression parallel aggregations (`Agg.Sum`, `Agg.Mean`, `Agg.Count`, `Agg.Min`, `Agg.Max`, `Agg.Std`, `Agg.First`, `Agg.Last`).
* **Tabular Reshaping (`ReshapeEngine`):** Fast multi-threaded `Pivot` (long-to-wide), `Melt` (wide-to-long), `ConcatVertical`, and `ConcatHorizontal`.

#### 5. High-Throughput I/O & Out-Of-Core Processing
* **Multi-Threaded CSV Reader (`FastCsvReader`):** Chunk-based parallel worker threads scanning line breaks, RFC 4180 quotes, and automatic schema inference.
* **Buffered CSV Writer (`FastCsvWriter`):** Stream writer with UTF-8 byte buffering and automatic RFC 4180 escaping.
* **Streaming NDJSON / JSON Lines Reader (`FastJsonReader`):** Memory-efficient parsing of newline-delimited JSON and JSON tabular records.
* **Apache Arrow IPC Feather Engine (`ArrowIpcEngine`):** Zero-copy binary serialization and deserialization conforming to Arrow IPC Stream and Feather standards.
* **Out-Of-Core Storage (`OutOfCoreDataFrame`):** Memory-Mapped File (MMF) storage engine for querying datasets exceeding physical RAM capacity.

#### 6. Zero-Copy Bridge to `TokenVector.Numerics`
* Instant zero-copy extraction from 1D `Series` and 2D numeric `DataFrame` to `TokenVector.Numerics.Core.NDArray<T>`.
* Direct conversion to dynamic computational graph `TokenVector.Numerics.Autograd.Tensor<T>` for deep learning and autograd pipelines.
* Bi-directional factory methods `DataFrame.FromNDArray<T>` and `DataFrame.FromTensor<T>`.

#### 7. Pure TokenVector Language Module & Syntax Specification
* Native TokenVector module (`tv_data.tkv`) providing `import tv.data as td`, `td.DataFrame`, `td.Series`, `td.read_csv`, `td.read_feather`, and `td.concat`.
* Formal syntax specifications: `TOKENVECTOR_SYNTAX_SPEC.md` and `TOKENVECTOR_SYNTAX_SPEC_VI.md`.

---

### 🧪 Verification & Test Suite

* **Total Automated Tests:** 51
* **Passed:** 51 (100% Green)
* **Failed:** 0
* **Skipped:** 0
* **Execution Duration:** 47 ms on .NET 8.0 LTS Release build.

---

### 📦 Artifacts & Distribution Packages

* `TokenVector.Data.dll` (.NET 8.0 LTS AOT-compatible assembly)
* `TokenVector.Data.1.0.0.nupkg` (Standard NuGet package)
* `TokenVector.Data.1.0.0.snupkg` (Symbol NuGet package)
* `TokenVector.Data-v1.0.0-Release.zip` (Complete release distribution archive)
