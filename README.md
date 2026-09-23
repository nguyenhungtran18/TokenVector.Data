# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![Language](https://img.shields.io/badge/Language-TokenVector%20(tkv)-purple.svg)]()
[![Target](https://img.shields.io/badge/Target-.NET%20CIL%20DLL-blue.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Checks-21%2F21%20Suites%20Passed-brightgreen.svg)]()

## What is this?

**TokenVector.Data is a DataFrame library** — think pandas, but written in the
TokenVector language and shipped as a single `.NET DLL` with no Python needed.

You give it rows and columns. It gives you back answers: filter them, group
them, join them, ask SQL questions, read/write CSV/JSON/Excel. If you use
.NET (C#, F#…), you call it directly like any other library.

```
CSV / JSON / Excel  →   DataFrame  →  filter · group · join · SQL  →  result
```

Current release: **1.0.8-dev**. Status: 21/21 test suites green.

---

## What this repo does for the TokenVector language

TokenVector.Data is not just *an app written in* TokenVector — it is the
language's **tabular standard library**, and its heaviest workout.

**If you write TokenVector programs, this repo gives you:**
- File and text I/O you can trust — CSV (quotes, encodings, chunks, 8-thread
  parallel), JSON/NDJSON, Excel. One import and you read real-world files:
  `__tkv_import__ = ["tokenvector_io", "tokenvector_excel"]`.
- A question layer over your data — SQL (`sql_query`), group-by, 4-way joins
  + AsOf, window functions — so business logic reads like questions, not loops.
- .NET for free — the same `.tkv` sources compile to `TokenVector.Data.dll`,
  so tables built in TokenVector work directly from C#/F# with no Python
  runtime. Your language just grew an ecosystem bridge.

**If you hack on the TokenVector compiler, this repo is your gym:** every
feature below is load-tested here, and its failures caught real compiler bugs
(logged in `SESSION_HANDOFF.md`):

| Language feature | Where this repo leans on it |
| :--- | :--- |
| `thread_spawn` / `thread_join` returning `list[T]` | 8-thread CSV parse, 8-thread output sweep |
| First-class `func` + lambdas as arguments | `series_apply`, `win_apply(f)`, `groupby_apply(f)` |
| Records, `list[T]`, `dict` | `DataFrame` / `Series` / `Mask`, group hash maps |
| Strings (`find` / `split` / `slice` / regex) | CSV, XML and JSON parsers from scratch; 34 string ops |
| File primitives (`read_file`, `write_file`, `f.readline`) | All I/O, true-streaming chunk reads |
| `datetime()` / `datetime_ticks` | Benchmarks, the datetime module, resampling |
| Typed globals | Configuring no-argument thread workers |

Known language ceilings this library has hit (all filed for the compiler):
no binary file write (hence no real Parquet/xlsx), no bitwise ops, thread
workers take no arguments, no `.dtype` on complex expressions. Practical
workarounds live in `AGENTS.md`.

---

## Show me in 30 seconds

```tokenvector
__tkv_import__ = ["tokenvector_data", "tokenvector_sql", "tokenvector_excel"]

def run() -> "i32":
    # 1. Make a table
    df = make_df([
        make_series_str("dept", ["IT", "HR", "IT", "Sales", "HR"]),
        make_series_f64("salary", [75000.0, 52000.0, 88000.0, 61000.0, 58000.0]),
    ])

    # 2. Ask a SQL question — verified digit-for-digit against real pandas
    top = sql_query(df, "SELECT dept, AVG(salary) AS m FROM df GROUP BY dept ORDER BY m DESC LIMIT 2")

    # 3. Save it so Excel opens it directly, read it back
    excel_write_file(df, "dept.xml", "Dept")
    back = excel_read_file("dept.xml")   # same 5 rows, same values
    return 0
```

Three things to notice:

1. **Tables are columns, not rows.** Each column (`dept`, `salary`) is stored
   on its own with its own type and its own null-mask. That is why grouping
   and math are fast, and why a missing value never corrupts a whole row.
2. **Everything is a plain function.** There is no `df.something()` chaining —
   you call `sql_query(df, …)`, `groupby_agg(df, …)`, `excel_write_file(…)`.
   Less magic, easier to see what runs.
3. **Missing values are explicit.** Every column knows exactly which rows are
   null (a bitmask), instead of hiding them as `NaN`. Filters and joins
   respect that mask.

---

## When should I use it — and when not?

**Good fit:**
- You ship a .NET app and want tables + SQL without bundling Python.
- You need Excel files fast (plain-XML bridge writes ~24× faster than
  openpyxl in our measurement; files are bigger — no ZIP).
- You want pandas-checked answers (group-by, joins, window functions,
  SQL) with the null handling spelled out.

**Not a fit (honest list):**
- Raw speed on big scans — Polars/DuckDB are 10–100× faster (see
  `BENCHMARKS.md` for same-machine numbers).
- Real Parquet or `.xlsx` binaries — blocked until the compiler grows binary
  I/O (tracked honestly in `tokenvector_parquet.tkv`).
- MultiIndex, lazy query plans, the pandas ecosystem (sklearn, plotting…).

---

## What can it do? (the full list, collapsed)

<details>
<summary>18 modules — click to expand</summary>

| Module | What lives there |
| :--- | :--- |
| `tokenvector_data.tkv` | The table itself: types, null-mask, `Series`, `DataFrame` |
| `tokenvector_compute.tkv` | Math on columns, filters, sums/means, rolling windows, ewm |
| `tokenvector_relational.tkv` | Group-by, 4 kinds of join + AsOf join, pivot/melt/stack |
| `tokenvector_sort.tkv` | Multi-key sorting, largest/smallest |
| `tokenvector_stats.tkv` | Correlations, skew/kurtosis |
| `tokenvector_datetime.tkv` | Parse/format dates, ranges, resampling (UTC) |
| `tokenvector_strings.tkv` | ~34 text operations + full regex |
| `tokenvector_apply.tkv` | Run your own function over a column or rows |
| `tokenvector_dtype.tkv` | Small integer/float/datetime flavors, sort-by-index |
| `tokenvector_pandas.tkv` | Extras that close pandas gaps (`df.query`, factorize, …) |
| `tokenvector_read_json.tkv` | JSON readers/writers |
| `tokenvector_p100.tkv` | Missing-data tools, rolling-per-group, index helpers |
| `tokenvector_sql.tkv` | SQL: `SELECT … WHERE … GROUP BY … HAVING … ORDER BY … LIMIT`, joins, `IN`/`LIKE` |
| `tokenvector_excel.tkv` | SpreadsheetML read/write (Excel opens it) |
| `tokenvector_parquet.tkv` | Reserved API, refuses honestly until the compiler catches up |
| `tokenvector_io.tkv` | CSV engine (quotes, encodings, streaming, 8-thread parallel), JSON |
| `tokenvector_numerics.tkv` | Bridge to tensor math (`Mat`, zero-copy, gradients flag) |
| `tokenvector_arrow.tkv` | Internal `ARROW1` format + files bigger than RAM (batches) |

</details>

---

## How do I know it works?

21 test suites live next to the code (`tvsrc/*_check.tkv`) and all pass on
the stock compiler — including 154 pandas-acceptance checks, a 43-check SQL
suite cross-checked against real pandas, and a 33-check Excel round-trip
suite. The shipped DLL is additionally probed from C# (54 symbols + real
calls). Details: `FUNCTION_PARITY.md`, `BENCHMARKS.md`, `SESSION_HANDOFF.md`.

<details>
<summary>Full suite list — click to expand</summary>

```text
base_check    core types and masks                        SUCCESS
comp_check    math / filter / aggregates / windows         SUCCESS
core_check    Series / DataFrame basics                   SUCCESS
rel_check     group-by / joins / pivot                    SUCCESS
io_check      CSV / JSON / file I/O                       SUCCESS
num_check     numerics bridge                             ALL PASS
arr_check     Arrow stream / out-of-core                  ALL PASS
feat_check    fill, text maps, dedup                      SUCCESS
vec_check     math edge cases                             FAILS= 0
strings_check text + regex (10 groups)                    10/10 PASS
stat_check    aggregates                                  FAILS= 0
stats_check   corr / skew / kurtosis (7 groups)           7/7 PASS
dt_check      datetime (11 checks)                        11/11 PASS
csv2_check    CSV flags / chunks / parallel               ALL PASS
pd_check      dates / encodings (15 checks)               FAILS= 0
p2_check      pandas acceptance (154 checks)              154/154 PASS
p100_check    missing data / index (74 checks)            74/74 PASS
v15_check     rounding / pivot / JSON extras              ALL OK
sql_check     SQL engine (43 checks)                      FAILS= 0
excel_check   SpreadsheetML round-trip (33 checks)        FAILS= 0
apply_check   custom functions (9 checks)                 9/9 PASS
```

</details>

---

## Calling it from .NET

```powershell
$asm  = [System.Reflection.Assembly]::LoadFrom("TokenVector.Data.dll")
$app  = $asm.GetType("TKVApp")

$csv = "id,name,score`n1,Alice,95.5`n2,Bob,88.0"
$df  = $app.GetMethod("csv_read_string").Invoke($null,
         @([string]$csv, [string]",", [int]1, [string]""))
$df.GetType().GetMethod("row_count").Invoke($df, @())   # -> 2
```

Every engine function is a static method of `TKVApp`; `DataFrame`, `Series`
and `Mask` are plain public classes.

---

## Building it yourself

You need the TokenVector compiler (`tkvc.exe`) and .NET `ilasm.exe`.

```bash
python tvsrc/_patch_merged.py        # refresh merged sources after edits
python tvsrc/_patch_merged3.py       # splice in new modules (idempotent)
tkvc.exe build --entry run tvsrc/tokenvector_data_all.tkv   # -> .il
python tvsrc/_mk_dll.py              # -> TokenVector.Data.dll
csc.exe tvsrc/smoke.cs && smoke.exe  # -> SMOKE OK (54 symbols)
python tvsrc/_pack108.py             # -> packages/TokenVector.Data.1.0.8-dev.nupkg
```

Or skip the build: use `tvsrc/TokenVector.Data.dll` (~300 KB) and the
`packages/` nupkg straight from this repo.

---

## 📄 License

MIT — see [LICENSE](LICENSE).
