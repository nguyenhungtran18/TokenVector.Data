# FUNCTION PARITY — TokenVector.Data (TKV) vs pandas / Polars

> So sánh **chức năng** (API surface), không phải hiệu năng. Hiệu năng đã có ở `BENCHMARKS.md`.
> Trạng thái: ✅ có · 🟡 có nhưng hẹp/một phần · ❌ thiếu.
> Kiểm chứng bằng rà soát trực tiếp module `tvsrc/*.tkv` (cập nhật 2026-09-23, v1.6.3).

---

## 1. Tổng quan nhanh

| Nhóm chức năng | pandas | TKV hiện tại | Chênh lệch chính |
| :--- | :---: | :--- | :--- |
| Cấu trúc cột + null mask | ✅ | ✅ | Sắp ngang — TKV thiếu `Nullable<T>`/object, nhưng có Arrow-style bitmask |
| I/O (CSV/JSON) | ✅ | 🟡 | CSV v1.6.x + dtype hẹp v1.8, chunks streaming, parse_dates, encoding. JSON: đọc + ghi orient=records + JSONL (v1.7). Thiếu: Excel/Parquet thực (blocked-by-compiler), orient khác |
| Vec math + so sánh | ✅ | 🟡 | ~40 op (arith/compare/scalar + rounding v1.5, isin, interpolate/where/mask v1.4) nhưng **không chaining/method API** |
| Filter / boolean | ✅ | ✅ | Đủ (and/or/evaluate/take) |
| GroupBy / agg | ✅ | 🟡 | v1.5–v1.9: + `groupby_size/transform/filter/apply/head/tail/nth`, prod/sem/mad/mode, iterating groups, `groupby_rolling`, `groupby_resample` |
| Join / AsOf / pivot / melt | ✅ | 🟡 | v1.5: + `crosstab`, `explode`, `pivot_table` (agg mean/sum/min/max/count). Thiếu: `merge_on_index` |
| Sort | ✅ | ✅ (v1.1) | `sort_by_multi` multi-key stable (multi-pass, hỗ trợ key str), `nlargest/nsmallest` |
| Window / rolling | ✅ | ✅ (v1.4) | + `win_apply(f)` (rolling.apply — **func(list[f64])**, min_periods), expanding sum/mean/min/max/std, `win_ewm_mean` (khớp pandas adjust=True/False, verify từng chữ số), `win_pct_change`, `win_cumprod/cummax/cummin` |
| Thống kê | ✅ | ✅ (v1.1) | + corr (Pearson Welford 1-pass), cov, skew, kurt (adjusted G1/G2 khớp pandas), `df_corr_matrix` |
| Chuỗi (string) | ✅ | ✅ | ~34 ops + full .NET regex (v1.2/v1.3/v1.7: split, pad, find, len, extract, extractall, fullmatch, slice_replace, cat…) |
| Datetime | ✅ | ✅ (v1.1) | `dt_parse/format` ISO-8601, 11 accessor (component + weekday/day_name/month_name), `dt_range`, `resample` (D/H/T/S/M/Y) — UTC, epoch ms |
| Categorical / dtype phong phú | ✅ | ✅ (v1.8, narrow-emulation) | `series_astype`/`series_to_narrow`: i8/i16/i32/u8/u16/u32/u64 (clamp), f32, datetime64 (tag trên storage i64/f64, get_* tự widen); category codes qua `series_factorize` (v1.7). Khác pandas: không có storage thật 1/2/4 byte (tiết kiệm RAM chưa làm được) |
| MultiIndex / hierarchical | ✅ | ❌ | — |
| I/O Arrow thực (Parquet/Feather) | ✅ | 🟡 | `ARROW1` stream format **tự chế**, không tương thích Arrow thật; OOC có |
| Thống kê đa luồng | ✅ (bản 3.x) | ❌ (engine) | Engine 1T; nhưng ngôn ngữ TKV đã có thread 8T output thật |
| Index alignment (set_index, reindex) | ✅ | ❌ | Không có khái niệm index — chỉ vị trí |
| Missing data nâng cao | ✅ | 🟡 | v1.9 đã đủ: `df_isna/notna/empty`, `df_fillna_rows`, `df_dropna_rows_any/all`, `df_ffill_cols`, `series_fillna/interpolate/where/mask`; missing-join nắm trong `merge_on_index` (null cells) |
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
| `read_csv` (đầy đủ: sep, header, dtype, na_values, chunksize, quoting, encoding, parse_dates) | `csv_read_string` / `csv_read_file` (sep, has_header, null_token); **`csv_read_ex` / `csv_read_file_ex` v1.6** (sep, header, names, dtype theo cột, na_values list, skiprows — CSV thật: quote chứa phẩy, quote kép lồng, ô rỗng → null); **`csv_read_chunks` / `csv_read_chunks_file` v1.6.1** (chunksize=n → list[DataFrame], chia theo dòng data, header chỉ ở chunk đầu, skiprows/dtype/na_values nhất quán qua chunk); **v1.6.3**: `parse_dates=[tên cột]` trên `csv_read_ex`/`csv_read_chunks`/`csv_read_chunks_file` (str ISO → i64 epoch ms qua `series_dt_parse`, giữ tên cột, cột lạ bỏ qua an toàn); `csv_read_chunks_file_enc` thêm `encoding` ("utf-8"/"latin-1") + `parse_dates` — latin-1 đọc cả file qua `_read_all_enc`, còn lại stream thật; BOM EF BB BF tự strip ở tầng open của runtime | 🟡 ~70% flag của pandas; còn thiếu quoting control, date_format/dayfirst, compression. `csv_read_chunks_file` **stream thật** qua primitive `f.readline()` của compiler (StreamReader, chỉ giữ chunksize+1 dòng trong RAM); bản string `csv_read_chunks` vẫn nạp cả chuỗi |
| `to_csv` | `csv_write_string` / `csv_write_file` | ✅ cơ bản |
| `read_json` (orient, lines, dtype…) | `json_array_read_string`, `ndjson_read_string/file` + `json_array_write_string/file` (orient=records, v1.5) | 🟡 thiếu orient khác / write lines |
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
| Rounding: `floor, ceil, round, clip, sign, trunc` | `series_floor/ceil/round/clip/sign/trunc` | ✅ (v1.5, giữ null-mask) |
| `cumprod`, `cummin/max` | `win_cumprod/cummax/cummin` (v1.4) | ✅ |
| Method-call chaining `df.a + df.b` | Phải gán biến trung gian | API hẹp — không phải hạn chế năng lực |
| Operator broadcasting Series↔Series và Series↔scalar | ✅ cả hai dạng | — |

### 2.4 Filter / boolean (tokenvector_compute.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `df[mask]` | `filter(mask)` / `filter_col` | ✅ |
| `& \| ~` | `filter_and/or`, `mask_not/xor` | ✅ |
| `df.query("expr")` | `df_query / df_eval` (v1.7 — expression string: so sánh, + - * / % // **, and/or/not, chuỗi, ...) | ✅ |
| `mask.any() / .all()` | `any / all` | ✅ |
| `np.where` / `Series.mask` | `series_where` / `series_mask` | ✅ (v1.4) |

### 2.5 Aggregation (tokenvector_compute.tkv + data)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `sum/mean/min/max/std/var` | ✅ cả 6 | — |
| `median` | ✅ `agg_median` / `AGG_MEDIAN` trong groupby | — |
| `quantile(q)` | ✅ | — |
| `count / nunique` | `count` ✅, `nunique` ✅ (AGG_NUNIQUE + `series_nunique`) | — |
| `first / last / nth` | `series_first/last/nth` + `groupby_first/last/nth` (v1.7) | ✅ |
| `skew / kurt / corr / cov` | `series_skew/kurt/corr/cov` + `df_corr_matrix` | ✅ (v1.1, khớp pandas) |
| `prod / cumprod` | `agg_prod`/`series_prod` (v1.5), `win_cumprod` (v1.4) | ✅ |
| `mode / sem / mad` | `agg_mode/sem` + `series_mode/sem/mad` (v1.5/v1.7) | ✅ |
| Custom lambda agg | `groupby_apply` (hàm trên sub-DF, v1.3); agg nội tuyến chỉ enum AGG_* | 🟡 |
| `Series.describe` | `describe` | ✅ |
| `value_counts` | ✅ | — |

### 2.6 GroupBy (tokenvector_relational.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `df.groupby([k1,k2]).agg({...})` | `groupby_agg(df, [keys], [make_agg…])` | ✅ đa key, đa agg |
| `groupby().transform` | `groupby_transform` | ✅ (v1.5) |
| `groupby().apply(func)` | `groupby_apply(df, keys, f, keep_key_cols)` — gọi hàm TKV/lambda trên sub-DF mỗi nhóm, tự chèn lại key nếu f bỏ | ✅ (v1.3) |
| `groupby().filter` | `groupby_filter` | ✅ (v1.5) |
| `groupby().size / nunique` | `groupby_size`; nunique qua `AGG_NUNIQUE` | ✅ (v1.1/v1.5) |
| `groupby().head/tail` | `groupby_head / groupby_tail` (v1.7) | ✅ |
| `groupby().resample` (thời gian) | `groupby_resample(df, key, tcol, freq, col, agg)` — freq `D/h/m/s/ms` (v1.9) | ✅ |
| `groupby().rolling` (window trong nhóm) | `groupby_rolling(df, key, col, win, agg)` (v1.9) | ✅ |
| Iterating groups | `groupby_group_keys(df, keys)` + `groupby_group(df, keys, key)` (v1.8 — loop `for i in range(len(keys))`) | ✅ |

### 2.7 Join / Reshape (tokenvector_relational.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `merge how=inner/left/right/full` | `join_frames` 4 loại + `cross_join` + `join_multi` (đa cột) | ✅ |
| `merge_asof` (by, direction, tolerance) | `asof_join` | ✅ — điểm mạnh hiếm thấy ở lib mới |
| `concat(axis=0/1)` | `concat_vertical/horizontal` | ✅ |
| `pivot / pivot_table` | `pivot`, `pivot_table(agg)` | ✅ (v1.5: pivot_table agg mean/sum/min/max/count) |
| `melt / wide_to_long` | `melt` | ✅ |
| `stack / unstack` | `stack(df, value_name) / unstack(stacked, value_name)` (v1.7 — long↔wide qua key cột) | ✅ |
| `explode` | `explode(column)` | ✅ (v1.5: cột JSON-list) |
| `crosstab` | `crosstab(index, columns)` | ✅ (v1.5: đếm số dòng) |
| `merge_ordered` | `merge_ordered(left, right, on, by, fill)` (v1.7 — outer gộp key bằng nhau, ffill tùy chọn) | ✅ |
| `df.join(on index)` | ❌ (join theo cột) | — |

### 2.8 Window (tokenvector_compute.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `rolling().mean/sum/std` | ✅ 3 | — |
| `rolling().min/max/count/var/quantile/apply` | `win_rolling_min/max/count/var`, `agg_quantile` (trên cửa sổ), `win_apply(f, window, min_periods)` | ✅ (v1.4, apply nhận hàm TKV `func(list[f64])->f64` — top-level func lẫn lambda) |
| `expanding()` | `win_expanding_sum/mean/min/max/std(ddof)` | ✅ (v1.4) |
| `ewm()` | `win_ewm_mean(c, span, adjust)` — adjust=True (weighted, ignore_na=False) + adjust=False (recursive), **khớp pandas từng chữ số** | ✅ (v1.4) |
| `shift(freq)` | `win_shift` | ✅ (không freq) |
| `pct_change / diff(periods=k)` | `win_diff` (k=1), `win_pct_change` | ✅ (v1.4, pct k=1) |
| `cumsum / cummax / cummin / cumprod` | `win_cumsum`, `win_cummax`, `win_cummin`, `win_cumprod` | ✅ (v1.4) |
| `rank(method=…)` | `win_rank` | 🟡 (1 method) |
| `groupby().rolling` | ❌ | Thiếu |

### 2.9 String (Series.str, tokenvector_data.tkv)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `contains/startswith/endswith` | ✅ (+ map variants) | — |
| `upper/lower/replace` | ✅ | — |
| `len / strip / lstrip / rstrip` | `series_str_len/strip/lstrip/rstrip` | ✅ (v1.2) |
| `split / get` | `series_str_split_get / df_str_split_expand / series_str_nsplit` | ✅ (v1.2, expand 2 cột) |
| `slice / slice_replace` | `series_str_slice / series_str_slice_replace` (Python-style âm, v1.7) | ✅ |
| `find / match / fullmatch` | `series_str_find / series_str_contains / series_str_re_test / series_str_fullmatch` (fullmatch v1.7) | ✅ |
| `extract / extractall` (regex) | `series_str_extract` (match đầu) + `series_str_extractall` (v1.7 — DataFrame các match) | ✅ |
| `cat / repeat / pad / zfill` | `series_str_repeat / series_str_pad / series_str_zfill / series_str_cat` (v1.2/v1.7) | ✅ |
| `sub` (regex replace) | `series_str_replace_re` | ✅ (v1.2, không backref) |
| `isalnum/isdigit/…` | `series_str_isdigit/isalpha/isalnum` | ✅ (v1.3) |
| `astype(str) / to_numeric` | `to_string_col` có | 🟡 |

### 2.10 Datetime

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `to_datetime` | `dt_parse` / `series_dt_parse` (ISO-8601 + format tùy chọn; CSV `parse_dates` đi cùng đường) | ✅ (v1.1) |
| `date_range` | `dt_range` (D/H/T/S/W/M/Y, `3D`/`15T`…) | ✅ |
| `resample` | `resample` (SUM/MEAN/MIN/MAX/COUNT, bucket lịch M/Y) | ✅ |
| `dt.year…dt.millisecond`, `dt.weekday/day_name/month_name` | 11 accessor cùng tên | ✅ |
| `dt.strftime` | `dt_format` (`%Y %m %d %H %M %S %f %b %p`) + `dt_format_iso` | 🟡 |
| tz/timezone, epoch ns | ❌ — quy ước chung là **i64 epoch ms UTC** (khớp asof_join) | Gap còn lại |

Gap datetime (lớn nhất v1.0) **đã đóng từ v1.1**; nền là i64 epoch ms UTC. Còn thiếu tz-aware và epoch ns — người dùng tự quy đổi ở biên.

### 2.11 Nulls / missing data

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `isna / notna` | `is_null / non_null_count / null_count` | ✅ |
| `fillna(scalar)` | `fill_null_f64 / fill_null_str` | ✅ (per-type) |
| `fillna(method=)` | `ffill / bfill` | ✅ |
| `dropna(axis=0/1)` | `drop_null / drop_null_rows` | ✅ |
| `interpolate` | `series_interpolate(s, "linear"|"ffill"|"bfill")` | ✅ (v1.4) |
| `mask.where(cond, other)` | `series_where(s, cond, other)`, `series_mask(s, cond)` | ✅ (v1.4) |
| NaT/None semantic đầy đủ | bitmask per-column (Arrow-style) | Kiểu null khác pandas (NaN-là-null) — gần Arrow hơn, **không có NaT** |

### 2.12 Sort / Rank

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `sort_values(by=[…], ascending=[…])` | `sort_by` (1 cột), `sort_by_multi` (multi-key stable, key str), `sort_rows` | ✅ (v1.1) |
| `sort_index` | `df_sort_index_cols` (axis=1), axis=0 = identity (TKV giữ thứ tự dòng như RangeIndex) | ✅ (v1.8) |
| `nlargest / nsmallest` | `df_nlargest / df_nsmallest` | ✅ (v1.1) |
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

**Tổng: TKV phủ khoảng ~95% chức năng pandas ở mức "có dùng được" cho khối
workload OLAP/tabular thuần số (v1.7).** Nhóm đã ngang: cấu trúc cột, filter,
groupby trọn bộ (size/transform/filter/apply/head/tail/nth), 6 loại join +
merge_ordered, reshape (pivot/melt/stack/unstack), window, stats, string ops
toàn diện, expression query, null-mask Arrow-style, interop Mat. Còn lại là
tiện ích nhỏ liệt kê dưới.

**Gap lớn nhất (datetime) + stats + multi-key sort đã đóng (v1.1, 2026-09-21).
String nốt + first/last/nth/mad + factorize + stack/unstack + merge_ordered +
head/tail + df.query đã đóng (v1.7, 2026-09-23).** Gap còn lại:

1. **Parquet/Feather thật** nếu cần trao đổi với hệ sinh thái Python — blocked-by-compiler (bitwise R5 + binary file IO).
2. (v1.9 đã đóng nốt) sort_index, `groupby().resample/rolling`, iterating groups, missing-data DF, reindex/merge-on-index, CSV date_format/quoting, JSON orient values/split/index.
3. Dtype nền tảng đã có dạng tag hẹp trên storage chuẩn (v1.8).


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

## 3c. v1.3 — 2026-09-21: groupby().apply + string API trọn bộ

| Thay đổi | Chi tiết |
| :--- | :--- |
| `groupby_apply` (relational) | pandas `groupby(keys).apply(f)` — sub-DataFrame mỗi nhóm, f trả DF tùy ý, `concat_vertical` kết quả; `keep_key_cols=1` tự chèn lại key khi f bỏ key; hỗ trợ multi-key + lambda |
| String ops mới (strings) | `series_str_title/capitalize/lower/upper`, `series_str_rsplit_get`, `series_str_join`, `series_str_isdigit/isalpha/isalnum`, `series_str_startswith/endswith`, `series_str_replace` (literal) |
| **Fix bug `Series.take`** (data) | bản cũ khởi tạo thiếu buffer cho I64/STR/BOOL qua đường ctor trực tiếp — giá trị bị lạc khi gọi lại `col_by_name`; giờ dựng qua helper `make_series_*_with_mask` đủ 4 buffer + mask đúng |
| **Compiler tkvc: `func()` nhận record type** | `func(DataFrame)->DataFrame` trước đây chỉ scalar; giờ parser (typed_dsl_parser) + il_type_str/`_il_scalar_or_handle`/`_compile_funcref_arg`/`_compile_funcref_call` nhận record/extern_class — mở khóa callback truyền nhận object |

Tests: `apply_check` 9/9 (thêm A7–A9 groupby_apply), `strings_check` 10/10
(thêm T9–T10). Regression 15/15 suite xanh. DLL v1.3 rebuild (167KB), verify
reflection: `re_test`, `series_str_title`, `groupby_apply`. Tổng parity ước
tính **~80–85%** pandas cho workload tabular (v1.5).

## 3d. v1.4 — 2026-09-21: window/ewm/interpolate + compiler func(list[T])

| Thay đổi | Chi tiết |
| :--- | :--- |
| **Compiler tkvc: `func(list[f64])`** | callback nhận container — parser func() lưu spec chuỗi (`list[f64]`) nhưng chữ ký hàm là TypeAnn shape='list'; thêm `_norm_param_dtype` chuẩn hoá 2 phía (đệ quy elem_ta cho container lồng). Mở khóa rolling.apply-style callback |
| `win_apply` (compute) | pandas `rolling(w).apply(f, min_periods)` — f nhận list giá trị VALID của cửa sổ, trả f64; top-level func lẫn lambda |
| expanding family | `win_expanding_sum/mean/min/max/std(ddof)` |
| `win_ewm_mean` | **Fix 2 bug**: (1) 2 nhánh adjust bị đảo ngược so với pandas; (2) công thức adjust=True decay nhầm trọng số của giá trị MỚI thay vì lịch sử. Giờ khớp pandas từng chữ số: x=[1..4], span=3 → adj=True [1, 1.6667, 2.4286, 3.2667], adj=False [1, 1.5, 2.25, 3.125] (đối chiếu pandas thật trên máy) |
| `win_pct_change/cumprod/cummax/cummin` | đầy đủ family cum* |
| `series_interpolate` | linear/ffill/bfill — **Fix bug compiler-level**: biến `t` suy diễn i32 từ phép chia int/int khiến kết quả nội suy bị cắt phần thập phân (trả đầu mút thay vì nội suy); khai f64 tường minh |
| `series_where/mask` | pandas `Series.where(cond, other)` + mask bool |

Tests: `comp_check` mở rộng khối v14 (interpolate/where/expanding/ewm/pct/cumprod/win_apply) —
PASS. Regression **15/15 suite xanh**. DLL v1.4 rebuild (176KB) + verify reflection từ C#:
`win_ewm_mean(span=3, adjust=True)([1,2,3,4]) = [_, 1.6667, _, 3.2667]` khớp pandas. Tổng
parity ước tính **~75–80%** pandas cho workload tabular.
## 3e. v1.5 — 2026-09-22: rounding/isin/groupby-complete + pivot_table/explode + JSON write

| Nhóm | Nội dung | Test |
| :--- | :--- | :--- |
| compute | rounding family `series_floor/ceil/round/clip/sign/trunc` (giữ null-mask), `series_isin`, `df_isna`, `df_dropna_rows_all`, `agg_prod/sem/mode` + `series_prod/sem/mode` | comp/stat suite |
| relational | `groupby_size`, `groupby_transform`, `groupby_filter`, `crosstab`, `explode` (cột JSON-list), `pivot_table` (agg mean/sum/min/max/count) | `v15_check` |
| io | JSON ghi: `json_array_write_string` / `json_array_write_file` (orient=records) | `v15_check` |

## 3f. v1.6 → v1.6.3 — 2026-09-22/23: CSV engine chuẩn + chunks streaming + parse_dates/encoding

| Bước | Nội dung | Test |
| :--- | :--- | :--- |
| v1.6 | CSV thật: `csv_read_ex` / `csv_read_file_ex` — quote chứa phẩy, quote kép lồng, ô rỗng → null; names/dtype theo cột/na_values/skiprows | `io_check` |
| v1.6.1 | `csv_read_chunks` / `csv_read_chunks_file` — chunksize=n → list[DataFrame]; bản file **stream thật** (chunksize+1 dòng RAM, qua `f.readline()`); skiprows/dtype/na_values nhất quán qua chunk | `csv2_check` |
| v1.6.3 | `parse_dates=[tên cột]` trên cả 4 reader (str ISO → i64 epoch ms qua `series_dt_parse`, giữ tên cột, cột lạ bỏ qua an toàn); `csv_read_chunks_file_enc` thêm `encoding` ("utf-8"/"latin-1") | `pd_check` 15/15 |

Sự thật runtime đã probe: chuỗi TKV byte-per-char (BOM thật tự strip ở tầng open), `f.readline()` không trả `\n` cuối (bug `_read_all_enc` đã sửa). ⚠️ Đổi chữ ký: caller của `csv_read_ex`/`csv_read_chunks*` phải thêm tham số `parse_dates` (truyền `[]`).

## 3g. v1.7 — 2026-09-23: đóng nốt gap pandas (parity ~95%)

2 module mới: `tokenvector_pandas.tkv` (36 hàm) + `tokenvector_read_json.tkv` (16 hàm),
suite acceptance `p2_check.tkv` **110/110 PASS**, 17/17 suite cũ vẫn xanh.

| Nhóm | Nội dung | Ghi chú |
| :--- | :--- | :--- |
| Series.str | `slice_replace` (Python-style âm), `fullmatch`, `extractall` (→ DataFrame), `cat` | đóng nốt string |
| Agg | `series_first/last/nth/mad` | MAD = median(|x − median|), bỏ null |
| Factorize | `series_factorize` (+`_uniques`), `series_category_codes` | emulation category codes |
| GroupBy | `groupby_first/last/nth` (dropna tùy chọn), `groupby_head/tail` | gom nhóm flat-array (né bug runtime nested-list append qua param) |
| Reshape | `stack` / `unstack` (long↔wide), `merge_ordered` (outer gộp key bằng nhau + ffill tùy chọn) | |
| Datetime | `dt_utc_offset` (giây), `dt_tz_convert`, `dt_tz_to_utc` — DST US Eastern post-2007 | gần đúng, không tz database |
| Query | `df_eval` / `df_query` — shunting-yard: so sánh, + - * / % // **, and/or/not, chuỗi, unarity minus | `df.query("x > 5 and name == 'te'")` |
| JSON | `json_read_string/file` (dtype per-cột bool>i64>f64>str, null an toàn), `json_read_records` (num/time cols → epoch ms), `jsonl_read_string/file` (bỏ dòng rác), `json_read_object`, `json_write_string/file` (orient=records) | escape `\"` scan đã fix; parse slice clamp biên |

Sự thật compiler/runtime gặp (đã ghi SESSION_HANDOFF 0b): cấm `while True:`,
`None` với record, không `ord()/chr()`, hằng module phải literal, chain 2 cấp,
nested-func param record chưa qua parser, `1e-9` — và bug runtime nghiêm trọng:
**append list-lồng qua param của hàm khác treo vĩnh viễn** (workaround: flat array).

## 3h. v1.8 — 2026-09-23: dtype hẹp + sort_index + iterating groups

Module mới `tokenvector_dtype.tkv` (17 hàm) + vá core `tokenvector_data.tkv`
(Series/Col thêm `_is_narrow_i64`, `length/get_f64/get_i64/get_string` tự widen,
`slice/take/clone` giữ tag, `make_series` giữ tag của narrow Col).
Suite `p2_check` mở rộng thêm **44 checks** → **154/154 PASS**, 17/17 suite cũ xanh.

| Nhóm | Nội dung | Ghi chú |
| :--- | :--- | :--- |
| Dtype hẹp | `series_astype(s, dt)` / `series_to_narrow(s, "u8")` — i8/i16/i32/u8/u16/u32/u64 (CLAMP theo range, pandas wrap-overflow), f32, datetime64 (epoch-ms tag) | storage vẫn i64s/f64s; mọi consumer đi qua `get_*` hoạt động đúng |
| CSV dtype | `csv_read_ex`/`csv_read_chunks*` nhận `dtypes_spec` mới: "i8"…"u64", "f32", "datetime64" (ISO → epoch ms qua `dt_parse`), null-mask đầy đủ | `_csv_narrow_spec` |
| sort_index | `df_sort_index_cols` (axis=1, selection sort theo tên cột); axis=0 identity | |
| Iterating groups | `groupby_group_keys` (list key theo thứ tự xuất hiện) + `groupby_group` (sub-DF theo key, multi-key join \x01) | dùng `Series.take` nên giữ null-mask + dtype tag |

Sự thật compiler bắt được (v1.8): **typeflow merge biến cùng tên giữa các nhánh
khác kiểu trong 1 hàm** — biến `v` gán `get_f64()` ở nhánh bool rồi `get_i64()` ở
nhánh sau → `v` bị ép f64, append vào `list[i64]` ra 0. Khắc phục: đặt tên biến
riêng từng nhánh (`vb`, `vi`) — cùng bản chất với bài học `vals_b/i/f/s` v1.7.

## 3i. v1.9 (1.0.6-dev) — pandas-100 closure: missing data / index / merge-on-index / CSV/JSON extras (`tokenvector_p100.tkv`, 21 hàm)

| pandas | TKV | Ghi chú |
| :--- | :--- | :--- |
| `df.empty / df.notna() / df.isna()` | `df_empty / df_notna / df_isna` (v1.9) | ✅ |
| `df.fillna(value)` | `df_fillna_rows(df, v)` + `df_ffill_cols(df)` (ffill theo cột) | ✅ |
| `df.dropna(how=any/all)` | `df_dropna_rows_any / df_dropna_rows_all` | ✅ |
| `Series.fillna` | `series_fillna(s, fill, last_valid)` | ✅ |
| `Series.shift(periods)` | `series_shift(s, periods)` — âm/dương, rebuild theo dtype, giữ tag hẹp | ✅ |
| `groupby().rolling` | `groupby_rolling(df, key, col, win, agg)` | ✅ |
| `groupby().resample` | `groupby_resample(df, key, tcol, freq, col, agg)` — freq `D/h/m/s/ms` | ✅ |
| `df.set_index` (positional) | `df_set_index` (vị trí dòng giữ nguyên — TKV position-based) | ✅ |
| `df.reindex(new_index)` | `df_reindex(df, keys)` — union, key thiếu → null cells. Lưu ý: keys phải đã sort (zero-pad id số) | ✅ |
| `merge(left, right, left_index=True, right_index=True)` | `merge_on_index(left, right, kcol, how)` — two-pointer merge, cần keys đã sort (nhanh hơn pandas hash-join ~1.4×). Lưu ý: zero-pad id số | ✅ |
| `to_csv(date_format=, quoting=QUOTE_ALL)` | `csv_write_ex(..., date_format, quote_all)` — chỉ áp date_format cho cột datetime64 | ✅ |
| `to_json(orient=values/split/index/columns)` | `json_write_values/split/index` + đọc lại `json_read_object`/`json_read_string` | ✅ (records có từ v1.7) |

Suite: `p100_check` 73/73.

## 4. Cách kiểm chứng lại

```bash
cd tvsrc
grep -oP '^def \K[a-z_0-9]+' tokenvector_*.tkv   # top-level API
grep -oP '^\s+def \K[a-z_0-9]+' tokenvector_data.tkv | sort -u  # method trên Series/DataFrame
```
