# TokenVector.Data - Release Notes

[ 🇬🇧 English ](RELEASE_NOTES.md) | [ 🇻🇳 Tiếng Việt ](RELEASE_NOTES_VI.md)

## Version 1.0.9 (2026-09-24) - First stable of the pure-tkv line

Graduates the 1.0.4-1.0.8-dev series: SQL SELECT engine on DataFrames
(pandas-verified, `sql_check` 43/43), Excel SpreadsheetML IO (`excel_check`
33/33), Parquet honest ledger, parallel CSV (~1.1-1.8x). 21/21 suites green
on DIST tkvc; DLL smoke 54/54 symbols + functional C# calls. Dead C# era
removed (dist/src/tests/TestResults/TEST_REPORT/sln, ~3.3k lines); READMEs
rewritten for readers. No compiler change.

---

## Version 1.0.8-dev (2026-09-23) - SQL engine + Excel SpreadsheetML + Parquet ledger (tvsrc v2.1)

**Internal tvsrc library version v2.1** (compiler tkvc unchanged; DIST tkvc builds all green).

### New modules
* **tokenvector_sql.tkv**: SELECT engine on DataFrame (pandasql-style) - sql_query / sql_query2 (JOIN second table) / sql_count. WHERE (arithmetic, comparisons, AND/OR/NOT, IN numeric+str, LIKE), GROUP BY + 10 aggregates (SUM/AVG/MIN/MAX/COUNT/COUNT(*)/STD/FIRST/LAST/MEDIAN/NUNIQUE), HAVING, ORDER BY (numeric+str, ASC/DESC), LIMIT/OFFSET, DISTINCT, INNER/LEFT/RIGHT JOIN ... ON (same-name + qualified keys), 1-row global agg on empty set. Verified number-for-number against real pandas.
* **tokenvector_excel.tkv**: SpreadsheetML 2003 read/write (pure XML text, Excel opens directly) - excel_write/read_string/file. Null-mask, dtype inference (i64/f64/bool/str), XML escaping, well-formed output.
* **tokenvector_parquet.tkv**: honest ledger - API reserved (parquet_write/read_file/string + parquet_blocked_reason) but fail-fast: real Parquet needs compiler binary write + bitwise R5 (both missing).

### Verification
* sql_check 43/43, excel_check 33/33 (incl. parquet ledger), 21/21 suites green on DIST tkvc.
* Merged (all+lib) idempotent splice; DLL rebuilt + smoke 54/54 symbols + functional C# calls (SMOKEFN OK).
* v2.2 addendum (same 1.0.8-dev package): CSV library speedup - slice-based `_split_csv_line`, parallel `csv_read_par`/`csv_read_file_par` (8 workers, ~1.1-1.8x), `csv2_check` t9. DLL 306688 bytes, nupkg repacked.

### Remaining (honest ledger)
* Real Parquet/xlsx (ZIP+DEFLATE+binary) - blocked-by-compiler (binary write + bitwise R5).
* MultiIndex / labeled-index alignment - intentional architecture exclusion.

---

## ⚡ Version 1.0.6-dev (2026-09-23) - pandas-100 closure (tvsrc v1.9)

**Internal tvsrc library version v1.9** (compiler tkvc unchanged).

### ✨ New module
* **`tokenvector_p100.tkv` (21 functions)** — closes the "small utilities" group:
  * Missing data: `df_empty/df_notna`, `df_fillna_rows`, `df_dropna_rows_any/all`, `df_ffill_cols`, `series_fillna`, `series_shift` (negative periods, dtype-aware rebuild preserving narrow tag).
  * GroupBy extras: `groupby_rolling` (per-group window agg), `groupby_resample` (time-bucket × group, freq `D/h/m/s/ms`).
  * Index/merge: `df_set_index`, `df_reindex` (union, missing keys → null cells), `merge_on_index` (inner/left/right, equal-key coalescing).
  * CSV extras: `csv_write_ex` with `date_format` (datetime64 columns only) and `quote_all`.
  * JSON extras: `json_write_values/split/index` (write + read back).
* Core: `Series.get_i64` auto-routes float storage to int truncation (narrow-tag widening path).

### ✅ Verification
* `p100_check` **73/73**, `p2_check` **154/154**, **18/18** legacy suites green.
* DLL rebuilt + smoke reflection **44/44** symbols (v1.7+v1.8+v1.9).
* `TokenVector.Data.1.0.6-dev.nupkg` packed.

### 📌 Remaining (honest ledger)
* Parquet/Feather, read_sql, Excel — **blocked-by-compiler** (no bitwise R5, no binary file IO primitive).
* MultiIndex / labeled-index alignment — intentional architecture exclusion (position-based model).

---

---

## ⚡ Version 1.0.5-dev (2026-09-23) - Narrow dtypes + sort_index + groupby iteration (tvsrc v1.8)

**Internal tvsrc library version v1.8** (compiler tkvc unchanged — clean HEAD toolchain).

### ✨ New module + core changes
* **`tokenvector_dtype.tkv` (17 functions)**:
  * Narrow dtypes: `series_astype(s, dt)` / `series_to_narrow(s, spec)` — i8/i16/i32/u8/u16/u32/u64 (range clamp; pandas wraps overflow), f32, datetime64 (epoch-ms tag).
  * `dt_is_narrow`, `dt_parse_spec` (spec string -> DT_*).
  * `df_sort_index_cols` (sort_index axis=1); axis=0 is identity (TKV preserves row order).
  * `groupby_group_keys` / `groupby_group` — iterating groups (single or multi key).
* **Core `tokenvector_data.tkv`**: Series/Col gained `_is_narrow_i64()`; `length/get_f64/get_i64/get_string` auto-widen narrow dtypes; `slice_series/take/clone_*/slice_col` preserve the tag; `make_series` preserves narrow Col tags.
* **`tokenvector_io.tkv`**: `csv_read_ex`/`csv_read_chunks*` accept new `dtypes_spec` values `"i8"…"u64", "f32", "datetime64"` (ISO -> epoch ms via dt_parse, full null masks) via `_csv_narrow_spec`.

### 🔎 Runtime facts (probed)
* **Typeflow merges same-named variables across differently-typed branches in one function**: assigning `get_f64()` to `v` in the bool branch then `get_i64()` in a later branch coerced `v` to f64; appending to `list[i64]` produced zeros. Fix: per-branch variable names (`vb`, `vi`) — same root cause as v1.7's `vals_b/i/f/s` lesson.
* `float(raw)` with raw = `"2026-01-02"` does **not raise** (returns a junk value) — datetime64 columns take the `dt_parse` branch before any numeric parse.

### ✅ Verification
* `p2_check` extended: **154/154 PASS** (+44 dtype/sort_index/groups checks).
* 17/17 legacy suites green after the core patch. DLL rebuilt + smoke **28/28 symbols** (20 v1.7 + 8 v1.8). Package `TokenVector.Data.1.0.5-dev.nupkg` created.

### 📊 Parquet/Feather — decided blocked
* Requires bitwise ops (compiler gap R5) for varint/RLE/thrift plus a binary file I/O primitive (today only text `f.readline()`). Once the compiler closes both, Parquet can be built inside the library with no core changes.

---

## ⚡ Version 1.0.4-dev (2026-09-23) - Pandas parity closure (tvsrc v1.7)

**Internal tvsrc library version v1.7** (compiler tkvc unchanged — built with a clean HEAD toolchain).

### ✨ New modules
* **`tokenvector_pandas.tkv` (36 functions)** — closes the remaining pandas gaps from FUNCTION_PARITY §2:
  * Series.str: `slice_replace` (Python-style negative bounds), `fullmatch`, `extractall` (→ DataFrame of matches), `cat`.
  * Aggregations: `series_first` / `series_last` / `series_nth` / `series_mad` (median absolute deviation, null-skipping).
  * Category emulation: `series_factorize` (+ `series_factorize_uniques`), `series_category_codes`.
  * GroupBy: `groupby_first` / `groupby_last` / `groupby_nth` (dropna option), `groupby_head` / `groupby_tail` (row order preserved).
  * Reshape: `stack` / `unstack` (long↔wide via value name), `merge_ordered` (outer merge that coalesces equal keys into one row, optional `ffill`).
  * Datetime tz: `dt_utc_offset` (seconds), `dt_tz_convert`, `dt_tz_to_utc` — US Eastern DST rules post-2007, other zones fixed offsets (approximate, no tz database).
  * Expression engine: `df_eval` / `df_query` — shunting-yard evaluator with comparisons, `+ - * / % // **`, `and/or/not`, string equality, unary minus (`df.query("x > 5 and name == 'te'")`).
* **`tokenvector_read_json.tkv` (16 functions)** — native JSON:
  * `json_read_string` / `json_read_file` (orient=records; per-column dtype normalization bool>i64>f64>str, explicit JSON `null` is null-safe), `json_read_records` (coerce listed numeric/time columns to i64 epoch-ms), `jsonl_read_string` / `jsonl_read_file` (garbage lines skipped), `json_read_object` (single object → 1-row DF), `json_write_string` / `json_write_file` (records orientation, proper escaping).

### 🔎 Runtime facts (probed, not guessed)
* **Severe runtime bug found:** appending a list into a nested list through another function's parameter hangs forever (inline append is fine). The new groupby helpers therefore group via flat i64 arrays + boundary offsets — faster and safe.
* Compiler rules confirmed: `while True:` banned, `None` against records banned (use sentinel objects), no `ord()`/`chr()` builtins, module-level constants must be literals, two-level attribute chains banned, nested functions may not take record-typed params, `1e-9`-style literals rejected.
* The tkvc.exe in the compiler repo's dist was built from a dirty tree and regressed cross-module constants; a clean HEAD worktree toolchain was used for all builds this session.

### ✅ Verification
* New acceptance suite `p2_check.tkv`: **110/110 PASS** (string, factorize, agg, groupby, stack, merge_ordered, tz, query, JSON incl. roundtrip + escapes).
* All 17 existing suites re-run green (apply/arr/base/comp/core/csv2/dt/feat/io/num/pd/rel/stat/stats/strings/v15/vec).
* Merged sources refreshed idempotently (`tvsrc/_patch_merged2.py`); both `tokenvector_data_all.tkv` and `_libbuild.tkv` build clean.
* DLL rebuilt through tkvc → IL → library-IL conversion (`_mk_dll.py`) → ilasm; C# reflection smoke passes with **20/20 new v1.7 symbols** callable. Package `TokenVector.Data.1.0.4-dev.nupkg` created.

### 📊 Parity
* FUNCTION_PARITY.md updated: coverage **~80% → ~95%** of pandas for tabular/OLAP workloads. Remaining: Parquet/Feather, sort_index, groupby().resample (composable today), dtype system (i32/u8/f32/datetime64), MultiIndex (intentionally excluded).

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
* **CSV reader single-pass v2 (2026-09-20)** — `csv_read_string` splits each line exactly once straight into the flat grid (drops the US-join → US-split round-trip), strips `
` per line without a full-line `replace`, infers dtypes with short-circuiting (a pure-digit cell skips the float/bool re-checks; a failed int check classifies float/bool in the same step), and parses ints via the `int()` builtin instead of the manual per-character loop. Standalone probe: 100k×3 **409 → 208 ms (−49%)**; in-bench B5 363 → 292 ms.
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
