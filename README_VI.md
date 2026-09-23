# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![Language](https://img.shields.io/badge/Language-TokenVector%20(tkv)-purple.svg)]()
[![Target](https://img.shields.io/badge/Target-.NET%20CIL%20DLL-blue.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Checks-21%2F21%20Suites%20Passed-brightgreen.svg)]()

## Đây là gì?

**TokenVector.Data là thư viện DataFrame** — hãy nghĩ tới pandas, nhưng viết
bằng ngôn ngữ TokenVector và đóng gói thành **một file DLL .NET duy nhất**,
không cần Python.

Bạn đưa cho nó bảng gồm dòng và cột. Nó trả lời: lọc, gom nhóm, nối bảng,
hỏi bằng SQL, đọc/ghi CSV/JSON/Excel. Nếu bạn dùng .NET (C#, F#…), bạn gọi
nó trực tiếp như mọi thư viện khác.

```
CSV / JSON / Excel  →   DataFrame  →  lọc · gom nhóm · nối · SQL  →  kết quả
```

Bản hiện tại: **1.0.8-dev**. Trạng thái: 21/21 suite test xanh.

---

## Repo này giúp gì khi bạn lập trình bằng TokenVector

TokenVector.Data là **thư viện chuẩn dạng bảng** của ngôn ngữ TokenVector:
import nó vào, chương trình của bạn làm việc được với file dữ liệu thật thay
vì vòng lặp viết tay.

**Việc nó gánh hộ bạn:**
- **Đọc file lộn xộn** — `csv_read_file("sales.csv", ",", 1, "")` lo quote,
  ô trống và đoán kiểu hộ bạn; JSON và Excel cũng cùng một kiểu
  (`excel_read_file`, `json_…`).
- **Trả lời câu hỏi về dữ liệu** — `sql_query(df, "SELECT … GROUP BY …")`,
  `groupby_agg`, join 4 kiểu + AsOf, hàm cửa sổ. Logic nghiệp vụ đọc như câu
  hỏi, không phải vòng lặp lồng nhau.
- **Nói chuyện với người không code** — `excel_write_file` ra file Excel mở
  trực tiếp; `csv_write_string`/`csv_write_file` đưa tiếp cho tool khác.
- **Ship kết quả** — cùng mã nguồn biên dịch ra `TokenVector.Data.dll`, nên
  bảng dựng bằng TokenVector mở trực tiếp từ C#/F# mà không cần cài Python.

**Cách dùng — toàn bộ quy trình 5 bước:**

1. Import cái cần — mỗi tên là một module, bạn chỉ trả giá cho cái mình
   liệt kê:
   `__tkv_import__ = ["tokenvector_data", "tokenvector_io", "tokenvector_sql"]`
   - `tokenvector_data` — nền móng: kiểu `DataFrame`/`Series`, hàm dựng
     `make_df` / `make_series_i64|str|f64`, dtype và mask null. Luôn import
     cái này; mọi module khác đều đứng trên nó.
   - `tokenvector_io` — đọc/ghi file và chuỗi: `csv_read_file`,
     `csv_write_string`, JSON/NDJSON. Import khi dữ liệu đi từ (hoặc ra)
     đĩa hay chuỗi.
   - `tokenvector_sql` — hỏi đáp: `sql_query` / `sql_query2`. Import khi
     bạn muốn `SELECT … WHERE … GROUP BY …` thay vì vòng lặp viết tay.
   Cần Excel? thêm `"tokenvector_excel"`. Ngày tháng? `"tokenvector_datetime"`.
   Xem bảng module đầy đủ bên dưới.
2. Lấy `DataFrame` — tự dựng (`make_df` + `make_series_i64/str/f64`) hoặc
   đọc (`csv_read_file`, `excel_read_file`, `json_read_string`…).
3. Biến đổi bằng hàm thường — `filter`, `groupby_agg`, `join_frames`,
   `sql_query`, `win_rolling_mean`, `dt_parse`, `series_str_*`. Không nối
   đuôi, không trạng thái ẩn.
4. Ghi ra (`csv_write_file`, `excel_write_file`, `json_…`) hoặc trả
   `DataFrame` về cho .NET.
5. Build và chạy: `tkvc.exe build --entry run myapp.tkv`, rồi `myapp.exe`.

Null đi theo mask riêng từng cột, nên lọc, nối và tổng hợp đối xử với dữ
liệu thiếu y hệt nhau ở mọi nơi — không có bất ngờ kiểu `NaN`.

---

## Xem trong 30 giây

```tokenvector
__tkv_import__ = ["tokenvector_data", "tokenvector_sql", "tokenvector_excel"]

def run() -> "i32":
    # 1. Dựng một cái bảng
    df = make_df([
        make_series_str("dept", ["IT", "HR", "IT", "Sales", "HR"]),
        make_series_f64("salary", [75000.0, 52000.0, 88000.0, 61000.0, 58000.0]),
    ])

    # 2. Hỏi bằng SQL — đã đối chiếu từng chữ số với pandas thật
    top = sql_query(df, "SELECT dept, AVG(salary) AS m FROM df GROUP BY dept ORDER BY m DESC LIMIT 2")

    # 3. Lưu ra file Excel mở trực tiếp, đọc lại kiểm tra
    excel_write_file(df, "dept.xml", "Dept")
    back = excel_read_file("dept.xml")   # vẫn 5 dòng, vẫn giá trị cũ
    return 0
```

Ba điều đáng chú ý:

1. **Bảng được lưu theo cột, không theo dòng.** Mỗi cột (`dept`, `salary`)
   nằm riêng với kiểu riêng và mask null riêng. Nhờ vậy gom nhóm và tính
   toán nhanh, và giá trị thiếu không bao giờ làm hỏng cả dòng.
2. **Mọi thứ là hàm thường.** Không có kiểu `df.something()` nối đuôi —
   bạn gọi `sql_query(df, …)`, `groupby_agg(df, …)`, `excel_write_file(…)`.
   Ít ma thuật, nhìn là biết cái gì chạy.
3. **Giá trị thiếu được ghi rõ.** Mỗi cột biết chính xác dòng nào null
   (bitmask), thay vì giấu thành `NaN`. Lọc và nối bảng đều tôn trọng mask đó.

---

## Khi nào nên dùng — và khi nào không?

**Hợp khi:**
- Bạn ship app .NET và cần bảng + SQL mà không muốn kèm Python.
- Bạn cần xuất Excel nhanh (cầu XML thuần nhanh hơn openpyxl ~24× theo số
  đo của repo; đánh đổi là file to hơn vì không nén).
- Bạn cần đáp số đã đối chiếu với pandas (group-by, join, window, SQL),
  với cách xử lý null rõ ràng.

**Không hợp (ghi thẳng):**
- Tốc độ quét thô trên dữ liệu lớn — Polars/DuckDB nhanh hơn 10–100× (xem
  số đo cùng máy trong `BENCHMARKS.md`).
- Parquet hay `.xlsx` nhị phân thật — chờ compiler có binary I/O (ghi rõ
  trong `tokenvector_parquet.tkv`).
- MultiIndex, lazy query plan, hệ sinh thái pandas (sklearn, vẽ đồ thị…).

---

## Nó làm được gì? (danh sách đầy đủ, bấm để mở)

<details>
<summary>18 module — bấm để mở rộng</summary>

| Module | Chứa gì |
| :--- | :--- |
| `tokenvector_data.tkv` | Bản thân cái bảng: kiểu, mask null, `Series`, `DataFrame` |
| `tokenvector_compute.tkv` | Tính toán trên cột, lọc, tổng/hiệu, cửa sổ trượt, ewm |
| `tokenvector_relational.tkv` | Gom nhóm, 4 kiểu nối + nối AsOf + nối theo index, pivot/melt/stack |
| `tokenvector_sort.tkv` | Sắp xếp đa khóa, lớn nhất/nhỏ nhất |
| `tokenvector_stats.tkv` | Tương quan, skew/kurtosis |
| `tokenvector_datetime.tkv` | Đọc/ghi ngày giờ, dải ngày, resample (UTC) |
| `tokenvector_strings.tkv` | ~34 phép xử lý chuỗi + regex đầy đủ |
| `tokenvector_apply.tkv` | Chạy hàm của bạn trên cột hoặc từng dòng |
| `tokenvector_dtype.tkv` | Kiểu số nhỏ/datetime, sắp theo index |
| `tokenvector_pandas.tkv` | Phần bù cho ngang pandas (`df.query`, factorize, …) |
| `tokenvector_read_json.tkv` | Đọc/ghi JSON |
| `tokenvector_p100.tkv` | Xử lý thiếu dữ liệu, rolling theo nhóm, tiện ích index |
| `tokenvector_sql.tkv` | SQL: `SELECT … WHERE … GROUP BY … HAVING … ORDER BY … LIMIT`, join, `IN`/`LIKE` |
| `tokenvector_excel.tkv` | Đọc/ghi SpreadsheetML (Excel mở trực tiếp) |
| `tokenvector_parquet.tkv` | Chốt API, từ chối trung thực cho tới khi compiler kịp |
| `tokenvector_io.tkv` | Engine CSV (quote, suy kiểu, streaming, song song 8 luồng), JSON |
| `tokenvector_numerics.tkv` | Cầu sang toán tensor (`Mat`, zero-copy, cờ gradient) |
| `tokenvector_arrow.tkv` | Định dạng nội bộ `ARROW1` + file to hơn RAM (đọc theo batch) |

</details>

---

## Sao tin được?

21 suite test nằm cạnh code (`tvsrc/*_check.tkv`), xanh toàn bộ trên compiler
chuẩn — gồm 154 check acceptance pandas, suite SQL 43 check đã đối chiếu
pandas thật, suite Excel 33 check round-trip. DLL phát hành còn được chọc từ
C# (54 symbol + gọi hàm thật). Chi tiết: `FUNCTION_PARITY.md`,
`BENCHMARKS.md`, `SESSION_HANDOFF.md`.

<details>
<summary>Danh sách suite đầy đủ — bấm để mở rộng</summary>

```text
base_check    kiểu lõi và mask                            SUCCESS
comp_check    toán / lọc / tổng hợp / cửa sổ              SUCCESS
core_check    Series / DataFrame cơ bản                   SUCCESS
rel_check     gom nhóm / nối / pivot                      SUCCESS
io_check      CSV / JSON / file I/O                       SUCCESS
num_check    cầu numerics                                 ALL PASS
arr_check    Arrow stream / out-of-core                   ALL PASS
feat_check   fill, text maps, dedup                      SUCCESS
vec_check    biên toán học                                FAILS= 0
strings_check chuỗi + regex (10 nhóm)                     10/10 PASS
stat_check   tổng hợp                                     FAILS= 0
stats_check  corr / skew / kurtosis (7 nhóm)              7/7 PASS
dt_check     datetime (11 checks)                         11/11 PASS
csv2_check   CSV flags / chunks / parallel                ALL PASS
pd_check     ngày tháng / encoding (15 checks)            FAILS= 0
p2_check     acceptance pandas (154 checks)               154/154 PASS
p100_check   thiếu dữ liệu / index (74 checks)            74/74 PASS
v15_check    rounding / pivot / JSON phụ                  ALL OK
sql_check    SQL engine (43 checks)                       FAILS= 0
excel_check  SpreadsheetML round-trip (33 checks)         FAILS= 0
apply_check  hàm tự viết (9 checks)                       9/9 PASS
```

</details>

---

## Gọi từ .NET

```powershell
$asm  = [System.Reflection.Assembly]::LoadFrom("TokenVector.Data.dll")
$app  = $asm.GetType("TKVApp")

$csv = "id,name,score`n1,Alice,95.5`n2,Bob,88.0"
$df  = $app.GetMethod("csv_read_string").Invoke($null,
         @([string]$csv, [string]",", [int]1, [string]""))
$df.GetType().GetMethod("row_count").Invoke($df, @())   # -> 2
```

Mọi hàm engine là static method của `TKVApp`; `DataFrame`, `Series`, `Mask`
là class public bình thường.

---

## Tự build

Cần compiler TokenVector (`tkvc.exe`) và `ilasm.exe` của .NET.

```bash
python tvsrc/_patch_merged.py        # refresh file merged sau khi sửa code
python tvsrc/_patch_merged3.py       # splice module mới (idempotent)
tkvc.exe build --entry run tvsrc/tokenvector_data_all.tkv   # -> .il
python tvsrc/_mk_dll.py              # -> TokenVector.Data.dll
csc.exe tvsrc/smoke.cs && smoke.exe  # -> SMOKE OK (54 symbol)
python tvsrc/_pack108.py             # -> packages/TokenVector.Data.1.0.8-dev.nupkg
```

Hoặc khỏi build: dùng `tvsrc/TokenVector.Data.dll` (~300 KB) và nupkg trong
`packages/` của repo.

---

## 📄 Giấy phép

MIT — xem [LICENSE](LICENSE).
