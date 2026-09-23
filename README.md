# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![Language](https://img.shields.io/badge/Language-TokenVector%20(tkv)-purple.svg)]()
[![Target](https://img.shields.io/badge/Target-.NET%20CIL%20DLL-blue.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Checks-21%2F21%20Suites%20Passed-brightgreen.svg)]()

**TokenVector.Data** is a high-performance columnar DataFrame and tabular data processing library **natively implemented in the TokenVector programming language (tkv)** and compiled to a .NET CIL assembly (`TokenVector.Data.dll`) via `tkvc` + `ilasm`.

Version **1.0.8-dev** (library v2.1–v2.2): SQL SELECT engine on DataFrames, Excel SpreadsheetML read/write, Parquet honest-ledger stubs, and multi-threaded CSV parsing — on top of the full pandas-parity core (group-by, joins incl. AsOf, window/ewm, strings, datetime, narrow dtypes). Earlier history: **1.0.1** ported 22 C# files to native tkv; **1.0.2** added O(n log n) sort, O(n log m) AsOf join and null propagation.

---

## 📦 Modules

| Module (tkv) | Contents |
| :--- | :--- |
| `tokenvector_data.tkv` | `DataTypes`, `Schema`, `Mask` (null bitmap), `Col`, `StrCol`, `Series`, `DataFrame`, `ChunkedArray` helpers |
| `tokenvector_compute.tkv` | `VectorMath` (arith/compare/scalar, rounding, isin, interpolate/where/mask), `FilterEngine`, `Aggregations` (sum/mean/min/max/std/var/quantile/mode/sem), `WindowFunctions` (rolling/expanding/ewm/pct_change/cumsum/rank, `win_apply`) |
| `tokenvector_relational.tkv` | `GroupByEngine` (multi-key, multi-agg, size/transform/filter/apply/head/tail/nth, rolling/resample), `JoinEngine` (Inner/Left/Right/Full/Cross + financial `AsOfJoin` + `merge_on_index`), `ReshapeEngine` (Pivot/pivot_table, Melt, Concat, stack/unstack, explode, crosstab, `merge_ordered`) |
| `tokenvector_sort.tkv` | Multi-key stable sort (`sort_by_multi`), `nlargest`/`nsmallest` |
| `tokenvector_stats.tkv` | `corr`/`cov` (Welford 1-pass), `skew`/`kurt` (adjusted Fisher-Pearson), `nunique`, correlation matrix |
| `tokenvector_datetime.tkv` | ISO-8601 `dt_parse`/`dt_format`, 11 accessors, `dt_range`, `resample`, UTC epoch-ms basis |
| `tokenvector_strings.tkv` | ~34 `Series.str` ops + full .NET regex (`extract`/`extractall`, `fullmatch`, `slice_replace`, `cat`, `pad`…) |
| `tokenvector_apply.tkv` | `series_apply`, `df_apply_col`/`transform`/`filter_rows` (top-level funcs and lambdas) |
| `tokenvector_dtype.tkv` | Narrow dtypes (`series_astype`: i8…u64, f32, datetime64 tags), `sort_index`, group iteration |
| `tokenvector_pandas.tkv` | pandas-closure helpers (`factorize`, first/last/nth/mad, `df_eval`/`df_query`, stack/unstack, tz convert) |
| `tokenvector_read_json.tkv` | JSON readers/writers (records orient, JSONL, single object) |
| `tokenvector_p100.tkv` | Missing-data frames, `groupby_rolling`/`resample`, positional index ops, CSV/JSON extras |
| `tokenvector_sql.tkv` | SQL SELECT engine (`sql_query`/`sql_query2`/`sql_count`): WHERE/GROUP BY/HAVING/ORDER BY/LIMIT/OFFSET/DISTINCT, INNER/LEFT/RIGHT JOIN, IN/LIKE, 10 aggregates |
| `tokenvector_excel.tkv` | SpreadsheetML 2003 read/write (`excel_write/read_string/file`) — Excel opens it directly |
| `tokenvector_parquet.tkv` | Honest ledger: API reserved, fail-fast until the compiler gains binary write |
| `tokenvector_io.tkv` | CSV engine (quotes, dtype inference, chunks streaming, encodings, `parse_dates`, parallel `csv_read_par`), JSON/NDJSON, CSV/JSON writers |
| `tokenvector_numerics.tkv` | `NumericsInterop` — `Series`/`DataFrame` ⇄ `Mat` (NDArray/Tensor model) bridge with **zero-copy** f64 view and `requires_grad` flag |
| `tokenvector_arrow.tkv` | `ArrowIpcEngine` (ARROW1 stream round-trip preserving null masks) + `OutOfCoreDataFrame` (persist / open / read batch) |

---

## ✨ Highlights

* **Pure TokenVector implementation** — no C# sources remain; the whole engine lives in 18 `.tkv` modules (~440 KB source).
* **Familiar DataFrame API** — Series/DataFrame with schema, per-column null masks, full group-by, 4-way joins + AsOf + merge-on-index, pivot/melt/stack, window/ewm matching pandas digit-for-digit.
* **SQL on DataFrames** — `sql_query(df, "SELECT cat, SUM(v) AS s FROM df GROUP BY cat HAVING s > 0 ORDER BY s DESC LIMIT 5")` verified number-for-number against real pandas.
* **Robust I/O** — CSV with quotes & type inference (serial, chunked-streaming and 8-thread parallel), NDJSON/JSON readers, Excel SpreadsheetML round-trips, writer round-trips, file and string based.
* **Arrow-style interchange** — `ARROW1`-framed stream format that survives null masks (validity bitmaps) through serialization.
* **Numerics bridge** — convert numeric columns to a 1D/2D `Mat` (flat f64 buffer + shape + `requires_grad`); f64 columns with no nulls are wrapped **zero-copy** (mutations through the Mat are visible in the Series).
* **Real multi-threading** — the TKV language spawns true 8-thread workers (8T CSV parse, 8T output sweep beating NumPy ~2.3×); the engine stays single-threaded where determinism matters.
* **.NET interop** — compiled to a plain CIL DLL (~300 KB); callable from any .NET language via reflection or direct references to the `TKVApp` class.

---

## 🧪 Verification & Quality Assurance

21 native check suites, all green on stock `tkvc` (best-of runs):

```text
base_check    (DataTypes/Schema/Mask/Column/StringColumn)   SUCCESS
comp_check    (VectorMath/Filter/Agg/Window)                SUCCESS
core_check    (Series/DataFrame/ChunkedArray)               SUCCESS
rel_check     (GroupBy/Join/AsOf/Pivot/Melt/Concat)         SUCCESS
io_check      (CSV/NDJSON/JSON array/TSV/file IO)           SUCCESS
num_check    (NumericsInterop)                              ALL PASS
arr_check    (ArrowIpc/OutOfCore)                           ALL PASS
feat_check   (ffill/bfill, string maps, dedup, …)           SUCCESS
vec_check    (vec math edge cases)                          FAILS= 0
strings_check (Series.str + regex, 10 groups)               10/10 PASS
stat_check   (aggregates)                                   FAILS= 0
stats_check  (corr/cov/skew/kurt, 7 groups)                 7/7 PASS
dt_check     (datetime, 11 checks)                          11/11 PASS
csv2_check   (CSV flags/chunks/parallel, incl. t9)          ALL PASS
pd_check     (parse_dates/encoding, 15 checks)              FAILS= 0
p2_check     (pandas-closure acceptance, 154 checks)        154/154 PASS
p100_check   (missing-data/index/merge extras, 74 checks)   74/74 PASS
v15_check    (rounding/groupby-complete/pivot/JSON)         ALL OK
sql_check    (SQL engine, 43 checks)                        FAILS= 0
excel_check  (SpreadsheetML round-trip, 33 checks)          FAILS= 0
apply_check  (apply/transform/filter fns, 9 checks)         9/9 PASS
```

---

## 🚀 Quick Start (TokenVector language)

```tokenvector
__tkv_import__ = ["tokenvector_data", "tokenvector_relational", "tokenvector_sql"]

def run() -> "i32":
    # 1. Create a DataFrame
    df = make_df([
        make_series_i64("user_id", [1, 2, 3, 4, 5]),
        make_series_str("dept", ["IT", "HR", "IT", "Sales", "HR"]),
        make_series_f64("salary", [75000.0, 52000.0, 88000.0, 61000.0, 58000.0]),
    ])

    # 2. GroupBy & aggregation
    report = groupby_agg(df, ["dept"], [
        make_agg("salary", AGG_COUNT, "headcount"),
        make_agg("salary", AGG_MEAN, "avg_salary"),
        make_agg("salary", AGG_MAX, "max_salary"),
    ])

    # 3. SQL on the same DataFrame
    top = sql_query(df, "SELECT dept, AVG(salary) AS m FROM df GROUP BY dept ORDER BY m DESC LIMIT 2")

    # 4. Excel round-trip (SpreadsheetML, opens directly in Excel)
    excel_write_file(df, "dept.xml", "Dept")
    back = excel_read_file("dept.xml")
    return 0
```

---

## 💻 Quick Start (.NET side via reflection)

```powershell
$asm  = [System.Reflection.Assembly]::LoadFrom("TokenVector.Data.dll")
$app  = $asm.GetType("TKVApp")

$csv = "id,name,score`n1,Alice,95.5`n2,Bob,88.0"
$df  = $app.GetMethod("csv_read_string").Invoke($null,
         @([string]$csv, [string]",", [int]1, [string]""))
$df.GetType().GetMethod("row_count").Invoke($df, @())   # -> 2
```

All engine functions are static methods of the `TKVApp` class (records such as `DataFrame`, `Series`, `Mask` are public classes usable as .NET types).

---

## 🔧 Build from source

Requirements: TokenVector compiler (`tkvc.exe`) and .NET Framework `ilasm.exe` (+ `csc.exe` for the smoke test).

```bash
# 1. Refresh the merged library sources after editing tvsrc/*.tkv
python tvsrc/_patch_merged.py        # refresh tokenvector_io section
python tvsrc/_patch_merged3.py       # splice new modules (idempotent)

# 2. Compile the merged source to IL (.exe form keeps the IL next to it)
tkvc.exe build --entry run tvsrc/tokenvector_data_all.tkv

# 3. Convert IL to library form and assemble the DLL
python tvsrc/_mk_dll.py              # -> TokenVector.Data.il -> TokenVector.Data.dll (ilasm)

# 4. Verify: C# reflection smoke test (54 symbols) + functional calls
csc.exe /nologo /out:smoke.exe tvsrc/smoke.cs && smoke.exe   # -> SMOKE OK

# 5. Package (no nuget.exe needed — layout-compatible zip)
python tvsrc/_pack108.py             # -> packages/TokenVector.Data.1.0.8-dev.nupkg
```

Prebuilt artifacts: `tvsrc/TokenVector.Data.dll` (~300 KB) and `packages/TokenVector.Data.1.0.8-dev.nupkg`.

See `FUNCTION_PARITY.md` (pandas/Polars feature comparison), `BENCHMARKS.md` (same-machine measurements) and `SESSION_HANDOFF.md` (build/verification log) for details.

---

## 📄 License

MIT — see [LICENSE](LICENSE).
