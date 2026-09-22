# TokenVector.Data - Release Notes

[ 🇬🇧 English ](RELEASE_NOTES.md) | [ 🇻🇳 Tiếng Việt ](RELEASE_NOTES_VI.md)

---

## ⚡ Version 1.0.3-dev (2026-09-22) - CSV datetime & encoding (tvsrc v1.6.3)

**Internal tvsrc library version v1.6.3** (compiler tkvc unchanged; DLL rebuild pending).

### ✨ New APIs
* **`parse_dates` on all CSV readers** — `csv_read_ex`, `csv_read_chunks`, `csv_read_chunks_file` now take a trailing `parse_dates: "list[str]"`; listed string columns are converted to i64 epoch-ms via `series_dt_parse` (module datetime) with the column name preserved; unknown names are skipped safely (pandas-like). Signature change: callers must append `[]` when no date columns.
* **`csv_read_chunks_file_enc(..., encoding, parse_dates)`** — encoding-aware chunked CSV read: `"latin-1"`/`"latin1"` reads the whole file byte-per-char via `_read_all_enc` then delegates to `csv_read_chunks`; `"utf-8"`/`""`/other stream for real (chunksize+1 lines in RAM). UTF-8 BOM (EF BB BF) is stripped automatically by the runtime file-open layer (probed for all three open modes).

### 🔎 Runtime facts (probed, not guessed)
* TKV strings are byte-per-char: a raw U+FEFF literal in source reads as 3 chars, and `write_file` double-encodes it — real-BOM fixtures cannot be built from string APIs; BOM handling therefore lives in the file-open layer.
* `f.readline()` returns lines WITHOUT the trailing `
` (unlike Python); `_read_all_enc` re-joins lines with `
` (caught by the new suite as a real bug).
* The compiler rejects passing file handles into helper functions (untyped params) and method calls on direct function results — both shaped the final design.

### 🧪 Verification
* New `pd_check.tkv` suite (15 checks: parse_dates across ex/chunks/file/enc, name preservation, epoch correctness, latin-1/default encoding, unknown-column tolerance) — `FAILS= 0`.
* Full regression green: base, vec, comp, apply, strings, rel, core, num, arr, feat, stat, stats, v15, io, dt, csv2.

---

## ⚡ Version 1.0.2 (2026-09-19) - Performance & Feature Upgrade

**`TokenVector.Data`** v1.0.2 focuses on algorithmic upgrades and data-hygiene APIs, all verified by **11/11 green check suites**.

### 🚀 Performance Upgrades
* **`DataFrame.sort_by` O(n log n)** — stable merge sort (`_ms_rows`) replaces the O(n²) insertion sort; 100k rows now sort in a fraction of the previous time.
* **`asof_join` O(n log m)** — the right frame is sorted once by timestamp (stable merge sort `_ms_ts`), then each left row is matched by binary search; **unsorted right input is now supported**; ties resolve to the earliest qualifying timestamp.
* **Rolling O(n)** — `win_rolling_std` rewritten as a single-pass rolling sum/sum² recurrence (was O(n·window) two-pass); `win_rolling_mean` builds its validity mask inline (was a second O(n·window) pass).
* **All-valid fast paths for vector math (2026-09-19, second pass)** — `vec_add`, `vec_sub`, `vec_mul`, `vec_div`, `vec_add_scalar`, `vec_mul_scalar`, `vec_abs`, `vec_sqrt`, `vec_exp`, `vec_log`, `vec_pow` and the 12 comparison ops (`vec_gt/ge/lt/le/eq/ne[_scalar]`) now scan the validity mask once (cached in `Mask.all1()`) and hoist dtype/null branches out of the loop; the output mask is built by pre-sized init instead of per-element `set()`. Measured at 5M rows: `vec_add_scalar`+`vec_mul` chain **~286 ms → ~182 ms (−36%)**; B1 500k 20.4→17.7 ms. Remaining gap to numpy is the output-append cost (~10 ns/elt) — needs compiler pre-allocation/SIMD to close further.
* **CSV reader single-pass v2 (2026-09-20)** — `csv_read_string` splits each line exactly once straight into the flat grid (drops the US-join → US-split round-trip), strips `` per line without a full-line `replace`, infers dtypes with short-circuiting (a pure-digit cell skips the float/bool re-checks; a failed int check classifies float/bool in the same step), and parses ints via the `int()` builtin instead of the manual per-character loop. Standalone probe: 100k×3 **409 → 208 ms (−49%)**; in-bench B5 363 → 292 ms.
* **`make_series` null-mask fix (2026-09-20)** — the Col→Series wrapper previously dropped the validity mask, so every CSV/JSON-parsed numeric/bool column was silently all-valid; the wrapper now preserves the mask when it contains nulls.
* **`sort_by` inline compare (2026-09-20)** — `_ms_rows` compares `RowKey` fields inline instead of calling `_rk_before` per comparison (~1.7M calls at n=100k); measured **neutral** (123 → 122 ms) since record-copy constants dominate — next win needs value-kind keys or compiler support.
* **Join materialization fast path (2026-09-20)** — `_materialize_joined` takes a branchless per-cell copy path when the join produced no unmatched rows (single O(total) `-1` scan) and the source column is all-valid (`Mask.all1()` cache); the output mask is created all-valid in O(1) instead of per-element `set()`. Applies to every join flavor (inner/left/right/full/multi/asof) with correct fallback: left/right/full/asof keep the two-branch slow path with full null semantics. B4 (100k×100k inner → 10M rows × 4 cols): **1,789 → ~1,660 ms (−7%)**.
* **Arrow mask elision (2026-09-20)** — `arrow_write_string` emits a single `-1` sentinel instead of one stream line per validity bit for all-valid numeric/bool columns; `arrow_read_string` recognizes the sentinel while remaining backward-compatible with explicit-bit files. B6 write+read 100k: **403 → ~330 ms (−18%)**.
* **B3 warm measurement (2026-09-20)** — `groupby_agg` (3 exprs, 500k rows, 3 groups) re-runs at **~36 ms warm** vs pandas 33.4 ms — effectively on par; `_compute_agg` already had an all-valid fast path (probe: the agg loop is ~1 ms of the 36 ms), and the dict-hash grouping phase (~40 ms) is the language runtime floor.
* **Join pairs-list refactor — deliberately skipped (2026-09-20)** — the 2×10M pair-build measures ~126 ms and the per-element append floor dominates afterwards; touching 6 call sites (each with its own suffix/skip/null semantics) was judged not worth the risk for the remaining ~7%. Revisit once compiler-side pre-allocation exists.
* **Statistics single-pass Welford (2026-09-20)** — `series_variance` and `agg_variance` rewritten as one-pass Welford (previously 2–3 passes: a mean pass plus a squared-deviation pass through per-element `is_null`/`get_f64` calls); `series_sum`/`series_min`/`series_max` read typed buffers directly with dtype/null branches hoisted and the non-null count taken from the cached mask pop-count; `_compute_agg` MIN/MAX no longer pay a dead SUM pass, and the with-null f64/i64 branches compute SUM/MEAN/MIN/MAX/FIRST/LAST/STD in one fused pass instead of materializing an intermediate valid-values list. Measured at 5M rows: `series_variance` **~115 → ~52 ms per call (−55%)**, `agg_variance` **~133 → ~50 ms (−62%)**. New `stat_check.tkv` suite (22 checks incl. Welford vs two-pass parity, null semantics, i64/bool targets, groupby STD).
* New `Mask.all1()` — O(1) when the pop-count cache is valid, otherwise a single early-exit scan that caches its result (`set()`/`set_all()` invalidate).

### 🛡️ Null Propagation (VectorMath)
* `vec_add`, `vec_sub`, `vec_mul`, `vec_div`, `vec_add_scalar`, `vec_mul_scalar`, `vec_abs`, `vec_sqrt`, `vec_exp`, `vec_log`, `vec_pow` now propagate nulls: any null input row produces a null output row (null in → null out).

### ✨ New APIs
* **Series** — `ffill`, `bfill`, `str_map_contains`, `str_map_startswith`, `str_map_endswith`, `str_map_upper`, `str_map_lower`, `str_map_replace`, `between(lo, hi, inclusive)`, `is_in_f64(candidates)`, `is_in_str(candidates)`.
* **DataFrame** — `drop_duplicates(subset)`, `duplicated_mask(subset)`, `value_counts(name)` (sorted by count desc, stable).
* Helpers — `str_startswith`, `str_endswith`.

### 🧪 Verification
* New `feat_check.tkv` suite covering all v1.0.2 features including null propagation.
* New `csv2_check.tkv` suite: i64/f64/bool inference, quoted fields with embedded delimiter and doubled quotes, null preservation (end-to-end through `make_series`), CRLF handling, no-header mode, and a 100k-row perf probe.
* New `_fastcheck.tkv`-style coverage retained as part of the compute suite: fast-path vs slow-path (null-preserving) parity for the rewritten vec ops, i64/f64, plus filter-through-mask integration (37 checks).
* `join_multi` multi-key and `groupby_agg` multi-key use composite string keys via O(1) dict lookup (nested dicts are not codegen-compatible in multi-module builds; note: the DLL build merges all modules into one, so this limitation only applies to standalone `.tkv` module compiles).
* All suites green: `base_check`, `comp_check`, `core_check`, `rel_check`, `io_check`, `num_check`, `arr_check`, `feat_check`, `vec_check`, `csv2_check`, `stat_check` (+ bench run).

### 📦 Artifacts
* `TokenVector.Data.dll` — rebuilt from `tokenvector_data_all.tkv` (tkvc → IL → ilasm), verified via reflection smoke test.
* `TokenVector.Data.1.0.2.nupkg` — version bumped, release notes updated.

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
