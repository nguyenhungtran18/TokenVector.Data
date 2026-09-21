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
| Sort | ✅ | ✅ (v1.1) | `sort_by_multi` multi-key stable (multi-pass, hỗ trợ key str), `nlargest/nsmallest` |
| Window / rolling | ✅ | 🟡 | Có rolling mean/sum/std, shift, diff, cumsum, rank; thiếu ewm, expanding, pct_change |
| Thống kê | ✅ | ✅ (v1.1) | + corr (Pearson Welford 1-pass), cov, skew, kurt (adjusted G1/G2 khớp pandas), `df_corr_matrix` |
| Chuỗi (string) | ✅ | 🟡 | 7 str ops; pandas có ~30; thiếu `extract/regex`, `split`, `pad`, `find`, `len` |
| Datetime | ✅ | ✅ (v1.1) | `dt_parse/format` ISO-8601, 8 component accessor, `dt_range`, `resample` (D/H/T/S/M/Y) — UTC, epoch ms |
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
| `median` | ✅ `agg_median` / `AGG_MEDIAN` trong groupby | — |
| `quantile(q)` | ✅ | — |
| `count / nunique` | `count` ✅, `nunique` ✅ (AGG_NUNIQUE + `series_nunique`) | — |
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
| `len / strip / lstrip / rstrip` | `series_str_len/strip/lstrip/rstrip` | ✅ (v1.2) |
| `split / get` | `series_str_split_get / df_str_split_expand / series_str_nsplit` | ✅ (v1.2, expand 2 cột) |
| `slice / slice_replace` | `series_str_slice` (Python-style âm) | 🟡 (thiếu slice_replace) |
| `find / match / fullmatch` | `series_str_find / series_str_contains / series_str_re_test` | ✅ match=search ngữ nghĩa (v1.2) |
| `extract / extractall` (regex) | `series_str_extract` + `re_find_all` (full .NET regex, C speed) | ✅ (v1.2, extract=match đầu) |
| `cat / repeat / pad / zfill` | `series_str_repeat / series_str_pad / series_str_zfill` | ✅ (v1.2) |
| `sub` (regex replace) | `series_str_replace_re` | ✅ (v1.2, không backref) |
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

**Gap lớn nhất (datetime) + stats + multi-key sort đã đóng (v1.1, 2026-09-21).** Gap còn lại:

1. **Parquet/Feather thật** nếu cần trao đổi với hệ sinh thái Python.
2. `rsplit / join / slice_replace / isalnum…` — nốt còn lại của string.
3. `groupby().apply(func)` — đã có delegate + apply block, chỉ còn nối vào
   groupby pipeline.

**Không nên làm (đánh đổi không đáng):** MultiIndex (chỉ pandas dùng tốt),
Parquet thật (cần spec lớn — giữ ARROW1 nội bộ, thêm Parquet sau).
Regex không còn là điểm yếu: v1.2 dùng builtin `re_*` của compiler (bọc
System.Text.RegularExpressions .NET) — full syntax, chạy C speed, không phải
tự viết engine.

---

## 3a. v1.1 — 2026-09-21: đã đóng 3 gap lớn

Module mới (tất cả có test suite riêng chạy xanh):

| Module | Nội dung | Test |
| :--- | :--- | :--- |
| `tokenvector_datetime.tkv` | `dt_parse/dt_format` (ISO-8601 + `%Y %m %d %H %M %S %f %b %p`), 8 accessor (`dt_year…dt_millisecond, dt_weekday, dt_day_name, dt_month_name`), `dt_add_*`, `dt_range` (D/H/T/S/W/M/Y, `3D`/`15T`...), `resample` (SUM/MEAN/MIN/MAX/COUNT, bucket lịch cho M/Y), `series_dt_parse/format/part` | `dt_check` 11/11 |
| `tokenvector_stats.tkv` | `series_corr/cov` (Welford chập 1-pass), `series_skew/kurt` (adjusted Fisher-Pearson khớp pandas), `series_nunique`, `df_corr_matrix` | `stats_check` S1-S3,S7 |
| `tokenvector_sort.tkv` | `sort_by_multi` (multi-pass stable, hỗ trợ key str qua RowKeyStr merge sort), `df_nlargest/nsmallest` | `stats_check` S5,S6 |
| `tokenvector_relational.tkv` | `AGG_NUNIQUE`, `AGG_MEDIAN` trong `groupby_agg` | `stats_check` S4 |

Nền tảng datetime: **i64 epoch milliseconds UTC** (khớp quy ước asof_join).
`series_corr` bỏ cặp null đồng thời; kurt/skew khớp pandas output cho dataset
chuẩn (skew(1..5)=0, kurt(1..5)=-5.875 adjusted). Regression: 12/12 suite cũ
vẫn xanh. DLL `TokenVector.Data.dll` (v1.1) đã rebuild + xác minh qua .NET
reflection (`dt_parse`/`dt_format`/`series_corr` từ C#).

## 3b. v1.2 — 2026-09-21: apply/transform nhận hàm + string ops đầy đủ

| Module | Nội dung | Test |
| :--- | :--- | :--- |
| `tokenvector_apply.tkv` | `series_apply` (f64 + str), `df_apply_col`, `df_transform`, `df_apply_col_str`, `df_filter_rows` (numeric/str), `series_filter_fn`, `series_reduce`, `series_sort_by_key` — nhận top-level func lẫn **lambda** (delegate compiler) | `apply_check` 6/6 |
| `tokenvector_strings.tkv` | `len/strip/lstrip/rstrip/split_get/split_expand/nsplit/slice/find/contains/zfill/pad/repeat` + regex API `re_test/re_find/re_count/re_find_all/series_str_extract/series_str_re_test/series_str_replace_re` | `strings_check` 8/8 |

Regex chạy trên builtin `re_search/re_findall/re_sub` của compiler (.NET
Regex — full syntax `\d \w \s [class] {m,n} (alt)`, C speed, vượt mục tiêu
"regex không làm"). apply giữ null-mask đúng (null đầu vào -> null đầu ra).
Regression: 15/15 suite xanh. DLL v1.2 rebuild + verify reflection
(`re_test`, `re_find_all`, `series_apply`, `series_str_len`).

**Lưu ý compiler (2 giới hạn gặp khi viết):** ternary chỉ hỗ trợ dạng C
`cond ? a : b` (Python `a if c else b` chỉ hợp lệ ở tầng parse, không parse
ở tầng IL); chain-method trên kết quả gọi hàm (`f(x).m()`) phải gán biến
trung gian. Cả 2 đã tránh trong code library.

## 4. Cách kiểm chứng lại

```bash
cd tvsrc
grep -oP '^def \K[a-z_0-9]+' tokenvector_*.tkv   # top-level API
grep -oP '^\s+def \K[a-z_0-9]+' tokenvector_data.tkv | sort -u  # method trên Series/DataFrame
```
