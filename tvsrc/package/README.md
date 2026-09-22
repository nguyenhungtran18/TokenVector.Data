# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![Language](https://img.shields.io/badge/Language-TokenVector%20(tkv)-purple.svg)]()
[![Target](https://img.shields.io/badge/Target-.NET%20CIL%20DLL-blue.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Checks-51%2F51%20Passed-brightgreen.svg)]()

**TokenVector.Data** is a high-performance columnar DataFrame and tabular data processing library **natively implemented in the TokenVector programming language (tkv)** and compiled to a .NET CIL assembly (`TokenVector.Data.dll`) via `tkvc` + `ilasm`.

Version **1.0.1** marks the complete migration of the library from C# to the TokenVector language: all 22 C# source files were ported to 6 native `.tkv` modules, and all 51 unit tests were re-mapped as native tkv checks — **51/51 passing**.

---

## 📦 Modules

| Module (tkv) | Contents |
| :--- | :--- |
| `tokenvector_data.tkv` | `DataTypes`, `Schema`, `Mask` (null bitmap), `Col`, `StrCol`, `Series`, `DataFrame`, `ChunkedArray` helpers |
| `tokenvector_compute.tkv` | `VectorMath` (add/sub/mul/div, exp, log, abs, scalar ops), `FilterEngine`, `Aggregations` (sum/mean/min/max/std/var/quantile), `WindowFunctions` (rolling, shift, diff, cumsum, rank) |
| `tokenvector_relational.tkv` | `GroupByEngine` (multi-key, multi-agg), `JoinEngine` (Inner/Left/Right/Full/Cross + financial `AsOfJoin`), `ReshapeEngine` (Pivot, Melt, Concat vertical/horizontal) |
| `tokenvector_io.tkv` | `FastCsvReader/Writer` (quoted fields, schema inference), `FastJsonReader` (NDJSON streaming + JSON array) |
| `tokenvector_numerics.tkv` | `NumericsInterop` — `Series`/`DataFrame` ⇄ `Mat` (NDArray/Tensor model) bridge with **zero-copy** f64 view and `requires_grad` flag |
| `tokenvector_arrow.tkv` | `ArrowIpcEngine` (ARROW1 stream round-trip preserving null masks) + `OutOfCoreDataFrame` (persist / open / read batch) |

---

## ✨ Highlights

* **Pure TokenVector implementation** — no C# sources remain; the whole engine lives in 6 `.tkv` modules (~116 KB source).
* **Familiar DataFrame API** — Series/DataFrame with schema, per-column null masks, group-by aggregations, 5 join kinds, AsOf time-series join, pivot/melt.
* **Robust I/O** — CSV with quotes & type inference, NDJSON and JSON-array readers, writer round-trips, file and string based.
* **Arrow-style interchange** — `ARROW1`-framed stream format that survives null masks (validity bitmaps) through serialization.
* **Numerics bridge** — convert numeric columns to a 1D/2D `Mat` (flat f64 buffer + shape + `requires_grad`); f64 columns with no nulls are wrapped **zero-copy** (mutations through the Mat are visible in the Series).
* **.NET interop** — compiled to a plain CIL DLL; callable from any .NET language via reflection or direct references to the `TKVApp` class.

---

## 🧪 Verification & Quality Assurance

The original C# test suite (51 `[Fact]`s) has been fully re-mapped to native tkv check programs. Regression status — all green:

```text
base_check   (DataTypes/Schema/Mask/Column/StringColumn)   SUCCESS
comp_check   (VectorMath/Filter/Agg/Window)                SUCCESS
core_check   (Series/DataFrame/ChunkedArray)               SUCCESS
rel_check    (GroupBy/Join/AsOf/Pivot/Melt/Concat)         SUCCESS
io_check     (CSV/NDJSON/JSON array/TSV/file IO)           SUCCESS
num_check    (NumericsInterop: 4 checks)                   ALL PASS
arr_check    (ArrowIpc/OutOfCore: 2 checks)                ALL PASS
```

---

## 🚀 Quick Start (TokenVector language)

```tokenvector
__tkv_import__ = ["tokenvector_data", "tokenvector_relational"]

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

    # 3. Join kinds
    joined = join_frames(df, bonuses, "user_id", "user_id", JOIN_INNER)

    # 4. CSV I/O
    df2 = csv_read_string(csv_text, ",", 1, "")
    out = csv_write_string(df2, ",", 1, "")
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

Requirements: TokenVector compiler (`tkvc.exe`) and .NET Framework `ilasm.exe`.

```bash
# 1. Merge the 6 modules into one library source (see tvsrc/build scripts)
tkvc.exe build --entry run tokenvector_data_all.tkv   # emits .exe + tokenvector_data_all.il

# 2. Convert IL: rename module/assembly to TokenVector.Data, remove .entrypoint

# 3. Assemble the DLL
ilasm.exe /nologo /quiet /dll /output:TokenVector.Data.dll TokenVector.Data.il

# 4. Package
nuget.exe pack TokenVector.Data.nuspec
```

Prebuilt artifacts: `tvsrc/TokenVector.Data.dll` and `packages/TokenVector.Data.1.0.3.nupkg`.

---

## 📄 License

MIT — see [LICENSE](LICENSE).
