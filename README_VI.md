# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![Language](https://img.shields.io/badge/Language-TokenVector%20(tkv)-purple.svg)]()
[![Target](https://img.shields.io/badge/Target-.NET%20CIL%20DLL-blue.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Checks-9%2F9%20Suites%20Passed-brightgreen.svg)]()

**TokenVector.Data** là thư viện xử lý dữ liệu dạng bảng và DataFrame dạng cột (columnar) hiệu năng cao, được hiện thực **hoàn toàn bằng ngôn ngữ lập trình TokenVector (tkv)** và biên dịch ra assembly .NET CIL (`TokenVector.Data.dll`) qua `tkvc` + `ilasm`.

Phiên bản **1.0.1** đánh dấu việc chuyển dịch hoàn toàn thư viện từ C# sang ngôn ngữ TokenVector: toàn bộ 22 file C# đã được port sang 6 module `.tkv` thuần chủng, và 51 unit test được ánh xạ lại thành các check tkv bản ngữ — **51/51 PASS**.

Phiên bản **1.0.2** là đợt nâng cấp hiệu năng & tính năng:

* **Hiệu năng** — `DataFrame.sort_by` dùng merge sort ổn định **O(n log n)** (trước là insertion sort O(n²)); `asof_join` sort frame phải 1 lần rồi **binary search** từng dòng trái — tổng thể **O(n log m)** (trước là quét tuyến tính O(n·m)), đồng thời hỗ trợ frame phải **chưa sort**; rolling `mean`/`std` đạt **O(n)** (std trước là O(n·window) với 2 lượt quét).
* **Null propagation** — `vec_add/sub/mul/div`, `vec_add_scalar/mul_scalar`, `vec_abs/sqrt/exp/log/pow` truyền null xuyên suốt (null vào → null ra).
* **API mới** — `Series.ffill/bfill`, `Series.str_map_contains/startswith/endswith/upper/lower/replace`, `Series.between`, `Series.is_in_f64/is_in_str`, `DataFrame.drop_duplicates/duplicated_mask/value_counts`.

---

## 📦 Các module

| Module (tkv) | Nội dung |
| :--- | :--- |
| `tokenvector_data.tkv` | `DataTypes`, `Schema`, `Mask` (bitmap null), `Col`, `StrCol`, `Series`, `DataFrame`, các helper `ChunkedArray` |
| `tokenvector_compute.tkv` | `VectorMath` (cộng/trừ/nhân/chia, exp, log, abs, phép với scalar), `FilterEngine`, `Aggregations` (sum/mean/min/max/std/var/quantile), `WindowFunctions` (rolling, shift, diff, cumsum, rank) |
| `tokenvector_relational.tkv` | `GroupByEngine` (đa khóa, đa tổng hợp), `JoinEngine` (Inner/Left/Right/Full/Cross + `AsOfJoin` tài chính), `ReshapeEngine` (Pivot, Melt, Concat dọc/ngang) |
| `tokenvector_io.tkv` | `FastCsvReader/Writer` (quoted field, tự suy luận kiểu), `FastJsonReader` (NDJSON + JSON array) |
| `tokenvector_numerics.tkv` | `NumericsInterop` — cầu nối `Series`/`DataFrame` ⇄ `Mat` (mô hình NDArray/Tensor) với **zero-copy** cho cột f64 không null, kèm cờ `requires_grad` |
| `tokenvector_arrow.tkv` | `ArrowIpcEngine` (round-trip dòng ARROW1, giữ nguyên mask null) + `OutOfCoreDataFrame` (persist / open / read batch) |

---

## ✨ Điểm nổi bật

* **Hiện thực thuần TokenVector** — không còn mã C# nào; toàn bộ engine nằm trong 6 module `.tkv` (~116 KB mã nguồn).
* **API DataFrame thân thuộc** — Series/DataFrame với schema, mask null theo cột, group-by đa tổng hợp, 5 kiểu join, AsOf time-series join, pivot/melt.
* **I/O chắc chắn** — CSV có quoted field & tự suy luận kiểu, đọc NDJSON và JSON array, round-trip writer, hỗ trợ cả file lẫn chuỗi.
* **Trao đổi kiểu Arrow** — định dạng dòng khung `ARROW1` giữ được mask null (validity bitmap) qua tuần tự hóa.
* **Cầu nối Numerics** — chuyển cột số sang `Mat` 1D/2D (buffer f64 phẳng + shape + `requires_grad`); cột f64 không null được bọc **zero-copy** (thay đổi qua Mat nhìn thấy ngay trong Series).
* **Tương tác .NET** — biên dịch ra DLL CIL thuần; gọi được từ mọi ngôn ngữ .NET qua reflection hoặc tham chiếu trực tiếp class `TKVApp`.

---

## 🧪 Kiểm chứng & đảm bảo chất lượng

Bộ test C# gốc (51 `[Fact]`) đã được ánh xạ đầy đủ thành các chương trình check tkv bản ngữ. Trạng thái regression — toàn bộ xanh:

```text
base_check   (DataTypes/Schema/Mask/Column/StringColumn)   SUCCESS
comp_check   (VectorMath/Filter/Agg/Window)                SUCCESS
core_check   (Series/DataFrame/ChunkedArray)               SUCCESS
rel_check    (GroupBy/Join/AsOf/Pivot/Melt/Concat)         SUCCESS
io_check     (CSV/NDJSON/JSON array/TSV/file IO)           SUCCESS
num_check    (NumericsInterop: 4 check)                    ALL PASS
arr_check    (ArrowIpc/OutOfCore: 2 check)                 ALL PASS
feat_check   (v1.0.2: ffill/bfill, string maps, dedup,
              value_counts, between, is_in, null prop)     SUCCESS
```

---

## 🚀 Bắt đầu nhanh (ngôn ngữ TokenVector)

```tokenvector
__tkv_import__ = ["tokenvector_data", "tokenvector_relational"]

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

    # 3. Cac kieu join
    joined = join_frames(df, bonuses, "user_id", "user_id", JOIN_INNER)

    # 4. CSV I/O
    df2 = csv_read_string(csv_text, ",", 1, "")
    out = csv_write_string(df2, ",", 1, "")
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

Yêu cầu: trình biên dịch TokenVector (`tkvc.exe`) và `ilasm.exe` của .NET Framework.

```bash
# 1. Gop 6 module thanh 1 file nguon thu vien
tkvc.exe build --entry run tokenvector_data_all.tkv   # tao .exe + tokenvector_data_all.il

# 2. Chuyen IL: doi ten module/assembly thanh TokenVector.Data, bo .entrypoint

# 3. Hop bien thanh DLL
ilasm.exe /nologo /quiet /dll /output:TokenVector.Data.dll TokenVector.Data.il

# 4. Dong goi
nuget.exe pack TokenVector.Data.nuspec
```

Artifact dựng sẵn: `tvsrc/TokenVector.Data.dll` và `packages/TokenVector.Data.1.0.3.nupkg`.

---

## 📄 Giấy phép

MIT — xem [LICENSE](LICENSE).
