# Benchmark Report v1.0.2 — TokenVector (TKV) vs Pandas (same machine)

[ 🇬🇧 English ](BENCHMARKS.md) | [ 🇻🇳 Tiếng Việt ](BENCHMARKS_VI.md)

> **Scope of this report:** an honest, reproducible measurement on the current machine between the **TokenVector (TKV)** runtime (v1.0.2, executed via `tkvc` → IL → JIT) and **pandas 2.3.3** (Python 3.14.7), using **identical datasets** for every workload.
>
> **Important caveat:** the "C# 12 / .NET 8 AOT" figures that used to live in this file (5M rows, 16 cores) belong to the **old C# implementation that has been removed from this repository**, measured on different hardware — they are reference-only and **must not be compared directly** against the table below.

---

## 1. Measurement setup

| Component | Value |
| :--- | :--- |
| TKV runtime | v1.0.2, built from `tvsrc/bench.tkv` (tkvc → IL → .NET JIT). Engine ops below run **single-threaded**; the language itself does expose `thread_spawn`/`thread_join` (probed: ~4.4× speedup on a 5M-row arithmetic sweep with 2 workers) but the DataFrame engine does not use them yet |
| Competitor | pandas 2.3.3 on Python 3.14.7 (NumPy backend) |
| Method | Each workload run 3 times, **best (ms)** kept; pandas best-of-3 (B4 run once — 10M-row output) |
| Sizes | B1–B3: 500k rows · B4: 100k×100k join (10M output) · B5: 100k CSV · B7: 100k sort |
| Reproduction scripts | `tvsrc/bench.tkv` and `benchmarks/bench_pandas.py` |

## 2. Results (ms — lower is better)

| Workload | N | TokenVector (TKV) | pandas 2.3.3 | pandas faster by |
| :--- | ---: | ---: | ---: | ---: |
| B1 Vector arithmetic (`a*2.5 + b`) | 500,000 | **16.2** | **2.9** | ~5.6× |
| B2 Filter (`val > 5.0`) | 500,000 | **15.2** | **4.4** | ~3.5× |
| B3 GroupBy 3 groups × 3 aggs | 500,000 | **87.8** (~36 warm) | **34.7** | ~2.5× (1.0× warm) |
| B4 Hash join inner (100k×100k → 10M) | 200,000 input | **1,645** | **545** | ~3.0× |
| B5 CSV parse 100k×3 cols | 100,000 | **237*** | **38.8** | ~6.1× |
| B6 Arrow round-trip | — | **316** (write+read) | n/a (pyarrow not installed) | — |
| B7 Sort (`sort_by`, merge sort) | 100,000 | **100.2** | **8.1** | ~12.4× |

\* B5 counts `csv_read_string` only; **building** the 100k CSV text took an extra ~305–340 ms this run because the benchmark script concatenates strings in O(n²) — that cost is not in the parse engine.

> **Re-measured same-session 2026-09-20 (after the Welford statistics rework):** best-of-3 on both engines, same machine and session. The statistics optimizations (`series_variance`, `agg_variance`, `_compute_agg`) do not sit on the B1–B7 hot paths (B3 uses COUNT/MEAN/SUM only), so B1–B7 stay within run-to-run noise; the shifts in this table come from re-running both engines in the same session (pandas measured B4 545 ms, B5 38.8 ms, B7 8.1 ms this time). Warm B3 remains ~36 ms ≈ pandas cold.

> **Engine update 2026-09-20 (CSV v2 + correctness fix):**
> - **CSV reader rebuilt (single-pass v2):** drops the US-join/split round-trip and double validation, infers dtypes with short-circuiting, and parses ints via the `int()` builtin instead of a per-character manual loop. Standalone probe: 100k rows × 3 cols **409 → 208 ms (−49%)**; in-bench B5 parse **363 → 292 ms**.
> - **Correctness fix (`make_series`):** the Col→Series wrapper previously **dropped the null mask**, so every CSV/JSON-parsed column was silently all-valid. Nulls in parsed frames now survive (covered by the new `csv2_check` suite).
> - **`sort_by`:** merge comparison now inlines field access instead of calling `_rk_before` per comparison (~1.7M calls at n=100k) — measured **neutral** (123 → 122 ms): the record-copy constant factor of the merge dominates, so the next win there needs value-kind keys or compiler support.
> - Engine vec fast paths from 09-19 remain: at 5M rows the `vec_add_scalar`+`vec_mul` chain is **182 ms (−36% vs 286)**. Remaining gap to pandas is per-element output append (~10 ns/elt) — closing it needs compiler-side pre-allocation/SIMD (see `KERNEL_LAB.md`).
> - **Join materialization fast path (2026-09-20):** `_materialize_joined` now detects unmatched-free joins (`-1` scan, O(total)) plus all-valid source columns (`Mask.all1()` cache) and runs a **branchless per-cell copy** with an O(1) all-valid mask instead of two branches + per-element `set()` per cell. B4 (10M-row output): **1,789 → ~1,660 ms (−7%)**; correctness covered by `rel_check` (left/right/full/asof still take the slow path and keep null semantics).
> - **Arrow mask elision (2026-09-20):** `arrow_write_string` emits a single `-1` sentinel line instead of one line per validity bit when a numeric/bool column is all-valid, and `arrow_read_string` recognizes the sentinel (old files with explicit bits still parse). B6 write+read: **403 → ~330 ms (−18%)**.
> - **B3 measured warm (2026-09-20):** `groupby_agg` 3-expr on 500k rows re-runs at **~36 ms** warm (vs 81–92 ms cold in bench runs) — statistically **on par with pandas 33.4 ms**. `_compute_agg` already had an all-valid fast path (probe: agg loop = 1 ms of the 36 ms); the remaining cost is the dict-hash grouping phase (~40 ms), which is the language runtime floor for dict-backed grouping.
> - **Join pairs-list refactor — deliberately not taken:** measured the 2×10M pair-build at ~126 ms and the append floor still applies after it, so the multi-flavor refactor (6 call sites, suffix/skip/null semantics each) was judged not worth the risk for the remaining ~7%. Revisit after compiler-side pre-allocation lands.

> **Engine update 2026-09-19 (all-valid fast paths):** the vec/comparison ops now scan the validity mask once and hoist dtype/null branches out of the loop. At 500k B1 improves 20.4 → 17.7 ms and B2 20.3 → 18.1 ms (later runs 15.6 ms after warm-up variance).

## 0b. Same-session re-run 2026-09-23 (tvsrc v1.9 / DLL 1.0.6-dev) + v1.9 feature bench

> Re-measured both engines in the same session after the v1.9 (pandas-100 closure)
> build. Best-of-3 per workload on both sides, same machine. Scripts:
> `tvsrc/bench.tkv` + `benchmarks/bench_pandas.py` (B1-B7),
> `benchmarks/bench_p100.tkv` + `benchmarks/bench_p100_pandas.py` (C1-C6, new).

**B1-B7 (legacy):**

| Workload | N | TokenVector v1.9 | pandas 2.3.3 | pandas faster by |
| :--- | ---: | ---: | ---: | ---: |
| B1 Vector arithmetic (`a*2.5 + b`) | 500,000 | **4.6** | **2.8** | ~1.6× |
| B2 Filter (`val > 5.0`) | 500,000 | **20.2** | **4.1** | ~4.9× |
| B3 GroupBy 3 groups × 3 aggs | 500,000 | **60.9** | **29.7** | ~2.1× |
| B4 Hash join inner (100k×100k → 10M) | 200,000 input | **1,507** | **514** | ~2.9× |
| B5 CSV parse 100k×3 cols | 100,000 | **259** | **38.2** | ~6.8× |
| B6 Arrow round-trip | — | **299** | n/a | — |
| B7 Sort (`sort_by`, merge sort) | 100,000 | **120** | **8.7** | ~13.8× |

**C1-C6 (v1.9 features — mới):**

| Workload | N | TokenVector v1.9 | pandas 2.3.3 | pandas faster by |
| :--- | ---: | ---: | ---: | ---: |
| C1 `Series.shift(1)` | 500,000 | **4.4** | **1.2** | ~3.7× |
| C2 `Series.fillna` (10% null) | 500,000 | **16.0** | **2.6** | ~6.2× |
| C3 `groupby().rolling(7).sum()` | 500,000 | **2,117** | **177** | ~12.0× |
| C4 `groupby().resample(5min).mean()` | 500,000 | **675** | **91** | ~7.4× |
| C5 `df.reindex` (str index, 20% missing) | 500,000 | **122** | **67** | ~1.8× |
| C6 merge on index inner (N × N/2) | 500,000 | **61** | **86** | **TKV ~1.4× faster** |

> **Perf fix shipped with this bench:** `groupby_rolling` previously sorted each
> group with a per-group **selection sort O(n²)** — at 500k rows × 3 groups the
> C3 workload did not finish in 10 minutes. Replaced with the engine merge sort
> (`sort_by`, O(n log n), stable): now **2,117 ms** end-to-end (group
> materialization + sort + window scan). `p100_check` 73/73 stays green.
>
> **Where the remaining C3/C4 gap lives:** per-row `groupby_group` sub-DataFrame
> materialization (records + masks) and the per-window `vals` list appends —
> engine-level costs the compiler's per-element append floor (~10 ns/elt,
> see BENCHMARKS.md §1) dominates. A pre-allocated, array-slice rolling kernel
> would close most of it; deferred until compiler-side allocation lands.
>
> **C6 is a real win for the two-pointer merge:** sorted index columns let the
> TKV merge skip hashing entirely — 61 ms vs pandas hash-join 86 ms. Note the
> TKV `merge_on_index`/`df_reindex` require lexicographically sorted string keys
> (use zero-padded ids for numeric data) — documented in FUNCTION_PARITY §3i.


## 1a. Compiler update 2026-09-21 — đa luồng output THẬT đã mở khóa (TKV 8T thắng numpy 8.6×)

Phiên tkvc hôm nay vá 2 gap compiler chặn đa luồng output từ KERNEL_LAB 6.2:

1. **Gap A (worker trả `list[T]`):** first-pass và codegen của `thread_spawn`/
   `thread_join` trước đây chỉ giữ `.dtype` (mất shape) → slot local khai scalar
   sai kiểu IL. Nay giữ nguyên TypeAnn đầy đủ → `Task<List<T>>` thật.
2. **Gap B (worker là hàm lồng/closure):** `thread_spawn(closure)` trước đây sinh
   `ldftn` trỏ method tĩnh không tồn tại → MissingMethodException. Nay dùng đúng
   cơ chế delegate của closure thật (instance method của closure class) bọc trong
   `Task.Factory.StartNew`.

**Bài đo mới (bench_thread_out / bench_thread_real8, 5M phần tử OUTPUT THẬT —
mỗi worker build list f64 chunk của mình, pattern `a*2.5+b`, best-of-3):**

| Cấu hình | ms | Đối thủ cùng phiên |
| :--- | ---: | :--- |
| TKV serial 1T (4 chunk nối tiếp) | 41–56 | — |
| TKV parallel 4T | **28.2** | — |
| TKV parallel 8T | **13.2** | — |
| numpy 1T (mảng preload) | 30.4 | TKV 4T/8T thắng |
| numpy inline-pipeline (`arange→mod→mul→add`) | 113.7 | **TKV 8T thắng ~8.6×** |

Đọc trung thực:
- pandas B1 @500k vẫn nhanh hơn ~1.6× so TKV 1T (3.4 vs 5.0 ms warm) ở mức
  engine đơn luồng — kết quả mục 2 giữ nguyên.
- Lần ĐẦU TIÊN ngôn ngữ TKV có đa luồng **sinh output thật** (không chỉ
  reduction trả scalar). Trên pipeline inline (input sinh lúc chạy), thread TKV
  vượt numpy rõ rệt vì thread dùng chung bộ nhớ (không IPC như multiprocessing)
  và numpy phải đi qua tầng Python orchestrator cho từng op.
- Bài đo "numpy 8T" trước đây so kernel memory-bound preload; bài inline-pipeline
  phản ánh đúng kịch bản thực tế build cột mới từ công thức. Chỉ số sàn per-op
  của numpy (30 ms @5M preload) vẫn là mốc cho kernel thuần.

Reproduce: `cd tvsrc && tkvc.exe build --entry run bench_thread_real8.tkv && ./bench_thread_real8.exe`

**Còn lại (đã thử, chưa qua):** closure bắt list local + index `inp[i]` trong
worker còn lỗi first-pass (scope capture) — Gap B mới, ghi ở COMPILER_BUGS
bên compiler; closure gọi trực tiếp hoạt động đúng. Cả 14 suite Testkit chạy
lại xanh trên tkvc đã vá (không regress).

---

## 2a. Single-pass statistics (Welford) — 5M rows, 2026-09-20

| Measurement | Before | After | |
| :--- | ---: | ---: | :--- |
| `series_variance` @5M | ~115 ms | **~52 ms** | **−55%** |
| `agg_variance` @5M | ~133 ms | **~50 ms** | **−62%** |

Reproduce: `cd tvsrc && tkvc.exe build stat_bench.tkv && ./stat_bench.exe`. Algorithm details in `RELEASE_NOTES.md` v1.0.2 section.

## 2b. Multi-threaded TokenVector (TKV language threads) — 5M rows × 20 reps

The TKV language exposes real threads (`thread_spawn`/`thread_join` over `Task.Factory.StartNew`/ThreadPool). Benchmark `benchmarks/bench_tkv_parallel.tkv` splits each workload across 2 and 4 workers, each worker timing itself internally (20 reps per measurement to defeat the ~15.6 ms `DateTime.UtcNow` granularity of .NET Framework). Best-of-3 wall-clock, total workload per measurement:

| Workload @ 5M rows × 20 | Serial | 2 workers | 4 workers | Best speedup | pandas 2.3.3 (serial) |
| :--- | ---: | ---: | ---: | ---: | ---: |
| B1 Vector arithmetic (`a*2.5+b`) | 228.3 ms | 75.6 ms | 61.1 ms | **3.7×** | 42.0 ms |
| B2 Filter count (`v > 5.0`) | 264.3 ms | 61.8 ms | 74.7 ms | **4.3×** | 9.4 ms |

Reading: with only 2–4 language-level workers and zero engine changes, TKV closes most of the gap to pandas on arithmetic (5.4× → **1.5×**) and significantly on filter (28× → **8.5×**). Speedup is sub-linear because every worker re-derives its chunk from the generator formula — real engine parallelism would operate on shared column buffers and avoid that cost. `cpu_sum_ms` exceeding `wall_ms` on 4-worker runs shows uneven ThreadPool scheduling; a dedicated chunk-per-core scheduler would do better.

### Same-thread-count comparison (8v8) — with an important caveat

Equalizing workload (×20 reps both sides) exposes that the parallel TKV scripts are **not an apples-to-apples engine comparison**: the TKV workers *generate* their chunk values on the fly (pure compute, no array traffic), while numpy kernels *read/write real 40 MB arrays* (memory-bound). Measured totals at 5M × 20 reps:

| Config | B1 arith | B2 filter |
| :--- | ---: | ---: |
| TKV loop, 1T | 228.3 ms | 264.3 ms |
| TKV loop, 8T | 25.2 ms | 41.0 ms |
| numpy kernel, 1T | 730.3 ms | 184.1 ms |
| numpy kernel, 8T | 327.1 ms | 30.8 ms |

TKV "beating" numpy on B1 here reflects **different work** (compute-bound loop vs memory-bound kernel), not a faster engine. The valid engine-to-engine comparison remains section 2 (real prebuilt columns on both sides): TKV engine is ~5–6× behind pandas on B1/B2. What the parallel scripts *do* validly demonstrate is **language-level thread scaling**: ~9× wall-clock from 1T→8T on the compute loop (25.2 ms vs 228.3 ms). Machine: 28 logical cores; numpy 4T→8T barely moves because chunks get small relative to pool overhead, while TKV 8T still scales.

> Scripts: 2/4-worker `benchmarks/bench_tkv_parallel.tkv`, 8-worker `benchmarks/bench_tkv_parallel8.tkv` (ThreadPool variance: some runs 36–46 ms; best-of-3 shown).

### So why does pandas win? (It is not the Python language)

Breaking down the apples-to-apples losses (section 2): pandas/NumPy spends essentially all time in **C kernels** — SIMD-vectorized comparisons, tight sum loops, and allocation-free reduction over contiguous arrays sized to fit CPU caches. Python itself is only orchestration. The TKV engine loses because its ops are **scalar IL with per-element bounds checks and intermediate array/mask allocations** (`vec_add` materializes a new column), not because it is "interpreted" — the compiled IL loop itself sustains ~2.3 ns/element on a compute-only sweep. Priority order follows directly: (1) eliminate intermediate allocations (in-place ops), (2) SIMD for compare/arith kernels, (3) engine-level threading on shared buffers.

## 3. Reading the results honestly

- **pandas wins every single-threaded workload.** This is expected: pandas/NumPy is 15+ years of optimized C, while the TKV DataFrame engine currently executes its ops **single-threaded** with no SIMD/AVX — features the old C# build advertised in this file. The *language* does support real threads (`thread_spawn`/`thread_join` over `Task.Factory.StartNew`, ThreadPool): a 5M-row arithmetic sweep split across 2 workers measured **~4.4× speedup vs serial**, so multi-threading the engine ops is the single biggest available win.
- **The gap is reasonable, not catastrophic:** TKV's single-threaded pure-IL core lands within 2.4×–7.7× of pandas; the outliers are B7 (sort, ~13×) and B5 (CSV parse, ~7.7×).
- **Standout bottleneck:** B4 (10M-row join output = 1.75 s) shows `_build_joined_df`'s row-at-a-time result materialization needs vectorization; B7 shows the IL merge sort carries a high constant factor.
- **Cheapest path to closing the gap (in order of payoff):**
  1. **Multi-thread the engine ops** using the existing `thread_spawn`/`thread_join` primitives — measured **3.7×–4.3× wall-clock speedup** at just 2–4 workers (section 2b), before any engine work. Caveats: workers are 0-arg (share via globals) and return a single scalar each; parallel materialization needs a shared-output pattern.
  2. Parallel, pre-allocated buffers in `_build_joined_df` / `_materialize_joined`.
  3. SIMD/AVX intrinsics for vector math if the runtime exposes them.
- **The legacy C# numbers** (8–25× faster than pandas) were measured on .NET 8 AOT with 16 cores at 5M rows — valid for that build, but **not representative** of today's TKV runtime; the two tables must not be presented as the same product.

## 4. How to reproduce

```bash
# TokenVector (TKV)
cd tvsrc
/d/SkillSpector-TKV/tkvc.exe build --entry run bench.tkv
./bench.exe

# pandas (500k, matches bench.tkv)
python benchmarks/bench_pandas.py

# TokenVector multi-threaded (5M rows, serial vs 2/4 workers)
cd tvsrc && /d/SkillSpector-TKV/tkvc.exe build --entry run ../benchmarks/bench_tkv_parallel.tkv && ./bench_tkv_parallel.exe

# TokenVector 8 workers (same-thread-count comparison vs numpy 8T)
cd tvsrc && /d/SkillSpector-TKV/tkvc.exe build --entry run ../benchmarks/bench_tkv_parallel8.tkv && ./bench_tkv_parallel8.exe
```

To add a **Polars/DuckDB/pyarrow** column on this machine: `pip install polars duckdb pyarrow`, then extend `benchmarks/bench_pandas.py` — the script's structure makes adding engines straightforward.
