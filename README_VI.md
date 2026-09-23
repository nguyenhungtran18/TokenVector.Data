# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![Language](https://img.shields.io/badge/Language-TokenVector%20(tkv)-purple.svg)]()
[![Target](https://img.shields.io/badge/Target-.NET%20CIL%20DLL-blue.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Checks-21%2F21%20Suites%20Passed-brightgreen.svg)]()

**TokenVector.Data** là thư viện xử lý dữ liệu dạng bảng và DataFrame dạng cột (columnar) hiệu năng cao, được hiện thực **hoàn toàn bằng ngôn ngữ lập trình TokenVector (tkv)** và biên dịch ra assembly .NET CIL (`TokenVector.Data.dll`) qua `tkvc` + `ilasm`.

Phiên bản **1.0.8-dev** (thư viện v2.1–v2.2): SQL SELECT engine trên DataFrame, đọc/ghi Excel SpreadsheetML, stub trung thực cho Parquet, parse CSV đa luồng — trên nền lõi đã ngang pandas (group-by, join kể cả AsOf, window/ewm, chuỗi, datetime, dtype hẹp). Lịch sử gọn: **1.0.1** port 22 file C# sang tkv thuần; **1.0.2** thêm sort O(n log n), AsOf join O(n log m) và null propagation.

---

## 📦 Các module

| Module (tkv) | Nội dung |
| :--- | :--- |
| `tokenvector_data.tkv` | `DataTypes`, `Schema`, `Mask` (bitmap null), `Col`, `StrCol`, `Series`, `DataFrame`, các helper `ChunkedArray` |
| `tokenvector_compute.tkv` | `VectorMath` (số học/so sánh/scalar, làm tròn, isin, interpolate/where/mask), `FilterEngine`, `Aggregations` (sum/mean/min/max/std/var/quantile/mode/sem), `WindowFunctions` (rolling/expanding/ewm/pct_change/cumsum/rank, `win_apply`) |
| `tokenvector_relational.tkv` | `GroupByEngine` (đa khóa, đa tổng hợp, size/transform/filter/apply/head/tail/nth, rolling/resample), `JoinEngine` (Inner/Left/Right/Full/Cross + `AsOfJoin` tài chính + `merge_on_index`), `ReshapeEngine` (Pivot/pivot_table, Melt, Concat, stack/unstack, explode, crosstab, `merge_ordered`) |
| `tokenvector_sort.tkv` | Sort ổn định đa khóa (`sort_by_multi`), `nlargest`/`nsmallest` |
| `tokenvector_stats.tkv` | `corr`/`cov` (Welford 1-pass), `skew`/`kurt` (Fisher-Pearson hiệu chỉnh), `nunique`, ma trận tương quan |
| `tokenvector_datetime.tkv` | `dt_parse`/`dt_format` ISO-8601, 11 accessor, `dt_range`, `resample`, nền epoch-ms UTC |
| `tokenvector_strings.tkv` | ~34 phép `Series.str` + regex .NET đầy đủ (`extract`/`extractall`, `fullmatch`, `slice_replace`, `cat`, `pad`…) |
| `tokenvector_apply.tkv` | `series_apply`, `df_apply_col`/`transform`/`filter_rows` (hàm top-level và lambda) |
| `tokenvector_dtype.tkv` | Dtype hẹp (`series_astype`: i8…u64, f32, tag datetime64), `sort_index`, duyệt group |
| `tokenvector_pandas.tkv` | Helper đóng parity pandas (`factorize`, first/last/nth/mad, `df_eval`/`df_query`, stack/unstack, đổi múi giờ) |
| `tokenvector_read_json.tkv` | Đọc/ghi JSON (orient records, JSONL, object đơn) |
| `tokenvector_p100.tkv` | Missing-data dạng frame, `groupby_rolling`/`resample`, thao tác index positional, phụ trợ CSV/JSON |
| `tokenvector_sql.tkv` | SQL SELECT engine (`sql_query`/`sql_query2`/`sql_count`): WHERE/GROUP BY/HAVING/ORDER BY/LIMIT/OFFSET/DISTINCT, JOIN INNER/LEFT/RIGHT, IN/LIKE, 10 hàm tổng hợp |
| `tokenvector_excel.tkv` | Đọc/ghi SpreadsheetML 2003 (`excel_write/read_string/file`) — Excel mở trực tiếp |
| `tokenvector_parquet.tkv` | Ledger trung thực: chốt API, fail-fast cho tới khi compiler có binary write |
| `tokenvector_io.tkv` | Engine CSV (quote, suy dtype, đọc chunk streaming, encoding, `parse_dates`, `csv_read_par` song song), JSON/NDJSON, writer CSV/JSON |
| `tokenvector_numerics.tkv` | `NumericsInterop` — cầu nối `Series`/`DataFrame` ⇄ `Mat` (mô hình NDArray/Tensor) với view f64 **zero-copy** và cờ `requires_grad` |
| `tokenvector_arrow.tkv` | `ArrowIpcEngine` (round-trip dòng ARROW1 giữ nguyên mask null) + `OutOfCoreDataFrame` (persist / open / read batch) |

---

## ✨ Điểm nổi bật

* **Hiện thực thuần TokenVector** — không còn mã C# nào; toàn bộ engine nằm trong 18 module `.tkv` (~440 KB mã nguồn).
* **API DataFrame thân thuộc** — Series/DataFrame với schema, mask null theo cột, group-by đầy đủ, join 4 kiểu + AsOf + merge-on-index, pivot/melt/stack, window/ewm khớp pandas từng chữ số.
* **SQL trên DataFrame** — `sql_query(df, "SELECT cat, SUM(v) AS s FROM df GROUP BY cat HAVING s > 0 ORDER BY s DESC LIMIT 5")` đã đối chiếu từng con số với pandas thật.
* **I/O chắc chắn** — CSV có quote & tự suy luận kiểu (serial, chunk-streaming và song song 8 luồng), đọc NDJSON/JSON, round-trip Excel SpreadsheetML, round-trip writer, hỗ trợ cả file lẫn chuỗi.
* **Trao đổi kiểu Arrow** — định dạng dòng khung `ARROW1` giữ được mask null (validity bitmap) qua tuần tự hóa.
* **Cầu nối Numerics** — chuyển cột số sang `Mat` 1D/2D (buffer f64 phẳng + shape + `requires_grad`); cột f64 không null được bọc **zero-copy** (thay đổi qua Mat nhìn thấy ngay trong Series).
* **Đa luồng thật** — ngôn ngữ TKV spawn worker 8 luồng thật (parse CSV 8T, sweep output 8T thắng NumPy ~2.3×); engine giữ đơn luồng ở chỗ cần đơn định.
* **Tương tác .NET** — biên dịch ra DLL CIL thuần (~300 KB); gọi được từ mọi ngôn ngữ .NET qua reflection hoặc tham chiếu trực tiếp class `TKVApp`.

---

## 🧪 Kiểm chứng & đảm bảo chất lượng

21 suite check bản ngữ, toàn bộ xanh trên `tkvc` chuẩn:

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
strings_check (Series.str + regex, 10 nhóm)                 10/10 PASS
stat_check   (aggregates)                                   FAILS= 0
stats_check  (corr/cov/skew/kurt, 7 nhóm)                   7/7 PASS
dt_check     (datetime, 11 checks)                          11/11 PASS
csv2_check   (CSV flags/chunks/parallel, gồm t9)            ALL PASS
pd_check     (parse_dates/encoding, 15 checks)              FAILS= 0
p2_check     (acceptance parity pandas, 154 checks)         154/154 PASS
p100_check   (missing-data/index/merge, 74 checks)          74/74 PASS
v15_check    (rounding/groupby-complete/pivot/JSON)         ALL OK
sql_check    (SQL engine, 43 checks)                        FAILS= 0
excel_check  (SpreadsheetML round-trip, 33 checks)          FAILS= 0
apply_check  (apply/transform/filter fns, 9 checks)         9/9 PASS
```

---

## 🚀 Bắt đầu nhanh (ngôn ngữ TokenVector)

```tokenvector
__tkv_import__ = ["tokenvector_data", "tokenvector_relational", "tokenvector_sql"]

def run() -> "i32":
    # 1. Tao DataFrame
    df = make_df([
        make_series_i64("user_id", [1, 2, 3, 4, 5]),
        make_series_str("dept", ["IT", "HR", "IT", "Sales", "HR"]),
        make_series_f64("salary", [75000.0, 52000.0, 88000.0, 61000.0, 58000.0]),
    ])

    # 2. GroupBy & tong hop
    report = groupby_agg(df, ["dept"], [
        make_agg("salary", AGG_COUNT, "headcount"),
        make_agg("salary", AGG_MEAN, "avg_salary"),
        make_agg("salary", AGG_MAX, "max_salary"),
    ])

    # 3. SQL tren cung DataFrame
    top = sql_query(df, "SELECT dept, AVG(salary) AS m FROM df GROUP BY dept ORDER BY m DESC LIMIT 2")

    # 4. Excel round-trip (SpreadsheetML, Excel mo truc tiep)
    excel_write_file(df, "dept.xml", "Dept")
    back = excel_read_file("dept.xml")
    return 0
```

---

## 💻 Bắt đầu nhanh (phía .NET qua reflection)

```powershell
$asm  = [System.Reflection.Assembly]::LoadFrom("TokenVector.Data.dll")
$app  = $asm.GetType("TKVApp")

$csv = "id,name,score`n1,Alice,95.5`n2,Bob,88.0"
$df  = $app.GetMethod("csv_read_string").Invoke($null,
         @([string]$csv, [string]",", [int]1, [string]""))
$df.GetType().GetMethod("row_count").Invoke($df, @())   # -> 2
```

Mọi hàm của engine là static method của class `TKVApp` (các record như `DataFrame`, `Series`, `Mask` là class public dùng được như kiểu .NET).

---

## 🔧 Build từ mã nguồn

Yêu cầu: trình biên dịch TokenVector (`tkvc.exe`) và `ilasm.exe` của .NET Framework (+ `csc.exe` cho smoke test).

```bash
# 1. Refresh file merged sau khi sua tvsrc/*.tkv
python tvsrc/_patch_merged.py        # refresh section tokenvector_io
python tvsrc/_patch_merged3.py       # splice module moi (idempotent)

# 2. Bien dich file merged ra IL (dang .exe giu file .il canh ben)
tkvc.exe build --entry run tvsrc/tokenvector_data_all.tkv

# 3. Chuyen IL sang dang library roi hop bien thanh DLL
python tvsrc/_mk_dll.py              # -> TokenVector.Data.il -> TokenVector.Data.dll (ilasm)

# 4. Kiem chung: smoke test reflection C# (54 symbol) + goi ham that
csc.exe /nologo /out:smoke.exe tvsrc/smoke.cs && smoke.exe   # -> SMOKE OK

# 5. Dong goi (khong can nuget.exe — zip dung layout)
python tvsrc/_pack108.py             # -> packages/TokenVector.Data.1.0.8-dev.nupkg
```

Artifact dựng sẵn: `tvsrc/TokenVector.Data.dll` (~300 KB) và `packages/TokenVector.Data.1.0.8-dev.nupkg`.

Chi tiết xem `FUNCTION_PARITY.md` (so sánh tính năng pandas/Polars), `BENCHMARKS.md` (số đo cùng máy) và `SESSION_HANDOFF.md` (nhật ký build/verify).

---

## 📄 Giấy phép

MIT — xem [LICENSE](LICENSE).
