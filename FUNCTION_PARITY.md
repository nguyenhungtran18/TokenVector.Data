# FUNCTION PARITY — TokenVector.Data (TKV) vs pandas / Polars

> So sánh **chức năng** (API surface), không phải hiệu năng. Hiệu năng đã có ở `BENCHMARKS.md`.
> Trạng thái: ✅ có · 🟡 có nhưng hẹp/một phần · ❌ thiếu.
> Kiểm chứng bằng rà soát trực tiếp 6 module `tvsrc/*.tkv` (2026-09-21).

---

## 1. Tổng quan nhanh

| Nhóm chức năng | pandas | TKV hiện tại | Chênh lệch chính |
| :--- | :---: | :--- | :--- |
| Cấu trúc cột + null mask | ✅ | ✅ | Sắp ngang — TKV thiếu `Nullable<T>`/object, nhưng có Arrow-style bitmask |
| I/O (CSV/JSON) | ✅ | 🟡 | Thiếu: TSV/tùy chọn delimiter tùy cột, đọc chunk, JSON ghi, Excel/Parquet thực |
| Vec math + so sánh | ✅ | 🟡 | Có 23 op nhưng **không chaining/method API**, thiếu đẳng thức kiểu ffill trên cột mới |
| Filter / boolean | ✅ | ✅ | Đủ (and/or/evaluate/take) |
| GroupBy / agg | ✅ | 🟡 | Đủ 8 agg phổ biến; thiếu `nunique`, `first/last`, agg tự định nghĩa, transform, iterating |
| Join / AsOf / pivot / melt | ✅ | 🟡 | Đủ 5+1 loại join; thiếu `merge_on_index`, `merge_multi` nhiều cột phức tạp |
| Sort | ✅ | 🟡 | `sort_by` 1 cột, `sort_rows`; **thiếu multi-key sort**, `rank` (đã có `win_rank`) |
| Window / rolling | ✅ | 🟡 | Có rolling mean/sum/std, shift, diff, cumsum, rank; thiếu ewm, expanding, pct_change |
| Thống kê | ✅ | 🟡 | sum/mean/std/var/min/max/median/quantile + describe; thiếu corr/cov, skew/kurt |
| Chuỗi (string) | ✅ | 🟡 | 7 str ops; pandas có ~30; thiếu `extract/regex`, `split`, `pad`, `find`, `len` |
| Datetime | ✅ | ❌ | **Zero API datetime** — i64 epoch ns là quy ước; thiếu parse/format/properties |
| Categorical / dtype phong phú | ✅ | ❌ | Chỉ i64/f64/bool/str; thiếu i32, u8, f32, datetime64, category |
| MultiIndex / hierarchical | ✅ | ❌ | — |
| I/O Arrow thực (Parquet/Feather) | ✅ | 🟡 | `ARROW1` stream format **tự chế**, không tương thích Arrow thật; OOC có |
| Thống kê đa luồng | ✅ (bản 3.x) | ❌ (engine) | Engine 1T; nhưng ngôn ngữ TKV đã có thread 8T output thật |
| Index alignment (set_index, reindex) | ✅ | ❌ | Không có khái niệm index — chỉ vị trí |
| Missing data nâng cao | 🟡 | 🟡 | Có fill/drop/ffill/bfill; thiếu interpolate, missing-joins, isna trên DF |
| Interop (NumPy/tensor) | ✅ | ✅ | Mat zero-copy bridge — điểm mạnh riêng, pandas không có sẵn |

---

## 2. Chi tiết từng nhóm

### 2.1 Cấu trúc dữ liệu (tokenvector_data.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `pd.DataFrame` | `DataFrame` record | ✅ |
| `pd.Series` | `Series` record | ✅ |
| `pd.Index` | ❌ (chỉ `make_idx` nội bộ) | Không có labeled index; mọi truy cập theo vị trí |
| dtypes: int8..64, uint, float16/32, bool, object, datetime64, category | i64, f64, bool, str | 4 dtype vs ~12; đây là gap nền tảng |
| MultiIndex | ❌ | — |
| `df[col]`, `df.loc`, `df.iloc` | `col_by_name`, `get_index`, `slice_df` | 🟡 đủ cơ bản |
| `df.head/tail` | ✅ | — |
| `df.copy` | `clone_df` | ✅ |
| `df.rename(columns=...)` | `with_column_renamed` | ✅ |
| `df.assign` | `with_column` | ✅ |
| `df.drop` | `drop` (cột) | ✅ |
| `df.insert` | `with_column` | ✅ |
| `df.shape`, `df.columns`, `df.dtypes` | `row_count/column_count/names/schema_dtypes` | ✅ |
| `df.empty` | `row_count == 0` | 🟡 (thủ công) |
| `df.describe` | `describe` | ✅ |

### 2.2 I/O (tokenvector_io.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `read_csv` (đầy đủ: sep, header, dtype, na_values, chunksize, quoting, encoding, parse_dates) | `csv_read_string` / `csv_read_file` (sep, has_header, null_token) | 🟡 ~15% flag của pandas; chunksize chưa có |
| `to_csv` | `csv_write_string` / `csv_write_file` | ✅ cơ bản |
| `read_json` (orient, lines, dtype…) | `json_array_read_string`, `ndjson_read_string/file` | 🟡 đọc được, **không có write JSON** |
| `read_excel` | ❌ | — |
| `read_parquet` / `to_parquet` | ❌ (ARROW1 tự chế, không phải Parquet) | Không trao đổi được với hệ sinh thái |
| `read_feather` | ❌ (cùng ARROW1) | — |
| `read_sql` / `to_sql` | ❌ | — |
| `read_hdf` | ❌ | — |
| Out-of-core | — | TKV có `ooc_persist/open/read_batch` — pandas **không có sẵn** (điểm cộng) |

### 2.3 Vec math & so sánh (tokenvector_compute.tkv)

| pandas (NumPy ufunc) | TKV | Ghi chú |
| :--- | :--- | :--- |
| `+ - * / ** abs sqrt exp log` | `vec_add/sub/mul/div/pow/abs/sqrt/exp/log` (+ scalar variants) | ✅ 10 op |
| `== != < <= > >=` (Series & scalar) | `vec_eq/ne/lt/le/gt/ge` + scalar variants | ✅ 12 op |
| Trilinear/rounding: `floor, ceil, round, clip, sign, trunc` | ❌ | Thiếu |
| `cumprod`, `cummin/max` | ❌ (chỉ cumsum) | Thiếu |
| Method-call chaining `df.a + df.b` | Phải gán biến trung gian | API hẹp — không phải hạn chế năng lực |
| Operator broadcasting Series↔Series và Series↔scalar | ✅ cả hai dạng | — |

### 2.4 Filter / boolean (tokenvector_compute.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `df[mask]` | `filter(mask)` / `filter_col` | ✅ |
| `& \| ~` | `filter_and/or`, `mask_not/xor` | ✅ |
| `df.query("expr")` | ❌ (phải compose mask thủ công) | Thiếu expression string — tiện, không phải năng lực |
| `mask.any() / .all()` | `any / all` | ✅ |
| `np.where` / `mask.fillna` | ❌ | Thiếu |

### 2.5 Aggregation (tokenvector_compute.tkv + data)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `sum/mean/min/max/std/var` | ✅ cả 6 | — |
| `median` | ✅ `agg_median` / `series_quantile` | — |
| `quantile(q)` | ✅ | — |
| `count / nunique` | `count` ✅, `nunique` ❌ | nunique thiếu |
| `first / last / nth` | ❌ | Thiếu |
| `skew / kurt / corr / cov` | ❌ | Thiếu — pandas nâng cao thống kê |
| `prod / cumprod` | ❌ | Thiếu |
| `mode / sem / mad` | ❌ | Thiếu |
| Custom lambda agg | ❌ (chỉ enum AGG_*) | Thiếu — cần API nhận hàm |
| `Series.describe` | `describe` | ✅ |
| `value_counts` | ✅ | — |

### 2.6 GroupBy (tokenvector_relational.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `df.groupby([k1,k2]).agg({...})` | `groupby_agg(df, [keys], [make_agg…])` | ✅ đa key, đa agg |
| `groupby().transform` | ❌ | Thiếu |
| `groupby().apply(func)` | ❌ | Thiếu |
| `groupby().filter` | ❌ | Thiếu |
| `groupby().size / nunique` | ❌ (count có qua agg) | nunique thiếu |
| `groupby().head/tail` | ❌ | Thiếu |
| `groupby().resample` (thời gian) | ❌ | Gắn với gap datetime |
| Iterating groups | ❌ | Thiếu |

### 2.7 Join / Reshape (tokenvector_relational.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `merge how=inner/left/right/full` | `join_frames` 4 loại + `cross_join` + `join_multi` (đa cột) | ✅ |
| `merge_asof` (by, direction, tolerance) | `asof_join` | ✅ — điểm mạnh hiếm thấy ở lib mới |
| `concat(axis=0/1)` | `concat_vertical/horizontal` | ✅ |
| `pivot / pivot_table` | `pivot` | 🟡 (không có agg trong pivot_table) |
| `melt / wide_to_long` | `melt` | ✅ |
| `stack / unstack` | ❌ | Thiếu |
| `explode` | ❌ | Thiếu |
| `crosstab` | ❌ | Thiếu |
| `merge_ordered` | ❌ (asof bù một phần) | — |
| `df.join(on index)` | ❌ (join theo cột) | — |

### 2.8 Window (tokenvector_compute.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `rolling().mean/sum/std` | ✅ 3 | — |
| `rolling().min/max/count/var/quantile/apply` | ❌ | Thiếu |
| `expanding()` | ❌ | Thiếu |
| `ewm()` | ❌ | Thiếu — quan trọng với finance |
| `shift(freq)` | `win_shift` | ✅ (không freq) |
| `pct_change / diff(periods=k)` | `win_diff` (k=1), ❌ pct_change | 🟡 |
| `cumsum / cummax / cummin / cumprod` | `win_cumsum` có, còn lại ❌ | 🟡 |
| `rank(method=…)` | `win_rank` | 🟡 (1 method) |
| `groupby().rolling` | ❌ | Thiếu |

### 2.9 String (Series.str, tokenvector_data.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `contains/startswith/endswith` | ✅ (+ map variants) | — |
| `upper/lower/replace` | ✅ | — |
| `len / strip / lstrip / rstrip` | ❌ | Thiếu |
| `split / rsplit / join / get` | ❌ | Thiếu |
| `slice / slice_replace` | ❌ | Thiếu |
| `find / match / fullmatch` | ❌ | Thiếu |
| `extract / extractall` (regex) | ❌ | Thiếu — pandas mạnh vì có re2/regex C |
| `cat / repeat / pad / zfill` | ❌ | Thiếu |
| `isalnum/isdigit/…` | ❌ | Thiếu |
| `astype(str) / to_numeric` | `to_string_col` có | 🟡 |

### 2.10 Datetime

| pandas | TKV |
| :--- | :--- |
| `to_datetime / date_range / resample / dt.year / dt.month / tz` | ❌ Toàn bộ — engine coi i64 epoch ns là quy ước người dùng tự quản |

Đây là **gap lớn nhất về chức năng** so với pandas — vì toàn bộ time-series (resample, asof theo thời gian, rolling theo giờ) đều xoay quanh nó, dù `asof_join` đã có sẵn hợp lý cho time-series.

### 2.11 Nulls / missing data

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `isna / notna` | `is_null / non_null_count / null_count` | ✅ |
| `fillna(scalar)` | `fill_null_f64 / fill_null_str` | ✅ (per-type) |
| `fillna(method=)` | `ffill / bfill` | ✅ |
| `dropna(axis=0/1)` | `drop_null / drop_null_rows` | ✅ |
| `interpolate` | ❌ | Thiếu |
| `mask.where(cond, other)` | ❌ | Thiếu |
| NaT/None semantic đầy đủ | bitmask per-column (Arrow-style) | Kiểu null khác pandas (NaN-là-null) — gần Arrow hơn, **không có NaT** |

### 2.12 Sort / Rank

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `sort_values(by=[…], ascending=[…])` | `sort_by` (1 cột, 1 chiều), `sort_rows` | 🟡 thiếu multi-key |
| `sort_index` | ❌ | — |
| `nlargest / nsmallest` | ❌ | Thiếu |
| `rank(method, na_option)` | `win_rank` | 🟡 |
| `Series.argsort` | `to_indices` | 🟡 |

### 2.13 Interop & năng lực riêng (không phải pandas)

| Năng lực | TKV | pandas |
| :--- | :---: | :--- |
| Zero-copy bridge sang tensor/NDArray (`Mat`, `requires_grad`) | ✅ | ❌ (cần pytorch/arrow ngoài) |
| Biên dịch thành 1 DLL CIL — gọi từ C#/F# không cần Python runtime | ✅ | ❌ |
| Out-of-core read batch | ✅ | ❌ |
| Ngôn ngữ native đa luồng output thật (8T) | ✅ | ❌ (GIL / multiprocessing IPC) |
| Thư viện plugin hệ sinh thái (sklearn, matplotlib…) | ❌ | ✅ |

---

## 3. Kết luận & thứ tự ưu tiên vá gap

**Tổng: TKV phủ khoảng 45–55% chức năng pandas ở mức "có dùng được" cho khối
workload OLAP/tabular thuần số.** Nhóm đã ngang: cấu trúc cột, filter, groupby
cơ bản, 6 loại join, reshape, window cơ bản, null-mask Arrow-style, interop
Mat. Nhóm hẳn sẽ có sau vài ngày làm: missing ops liệt kê ở mục 2.3/2.5/2.8.

**3 gap lớn cấu trúc (làm trước):**

1. **Datetime + resample** — chặn toàn bộ time-series workflow. Chỉ cần
   `parse_datetime(str) -> i64ns`, `format_datetime(i64ns) -> str`,
   `dt_year/month/day/hour/…`, `resample(ts, freq, agg)` — re-use i64.
2. **Multi-key sort + `nunique`/`first/last`** — giúp groupby + report thực tế
   (sort 2 cột trong B7 cũng là con đường về đích hiệu năng vì merge sort hiện
   tại thua pandas 12×).
3. **`apply`/`transform` nhận hàm TKV** — mở khóa custom agg, custom window,
   groupby-apply; gần như không thêm C code vì compiler đã có delegate.

**Không nên làm (đánh đổi không đáng):** MultiIndex (chỉ pandas dùng tốt),
Parquet thật (cần spec lớn — giữ ARROW1 nội bộ, thêm Parquet sau), regex trên
string (tốn công lớn, pandas chỉ thắng nhờ libc regex).

---

## 4. Cách kiểm chứng lại

```bash
cd tvsrc
grep -oP '^def \K[a-z_0-9]+' tokenvector_*.tkv   # top-level API
grep -oP '^\s+def \K[a-z_0-9]+' tokenvector_data.tkv | sort -u  # method trên Series/DataFrame
```
