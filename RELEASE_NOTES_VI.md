# TokenVector.Data - Thông Tin Phát Hành (Release Notes)

[ 🇬🇧 English ](RELEASE_NOTES.md) | [ 🇻🇳 Tiếng Việt ](RELEASE_NOTES_VI.md)

---

## ⚡ Phiên Bản 1.0.3-dev (22/09/2026) - CSV datetime & encoding (tvsrc v1.6.3)

**Phiên bản thư viện tvsrc v1.6.3** (compiler tkvc không đổi; DLL chưa rebuild).

### ✨ API mới
* **`parse_dates` trên mọi CSV reader** — `csv_read_ex`, `csv_read_chunks`, `csv_read_chunks_file` nhận thêm tham số cuối `parse_dates: "list[str]"`; cột str được liệt kê sẽ đổi sang i64 epoch-ms qua `series_dt_parse` (module datetime), giữ nguyên tên cột; tên lạ được bỏ qua an toàn (giống pandas). Đổi chữ ký: caller cũ phải thêm `[]` khi không có cột ngày.
* **`csv_read_chunks_file_enc(..., encoding, parse_dates)`** — đọc CSV theo chunk có encoding: `"latin-1"`/`"latin1"` đọc cả file byte-per-char qua `_read_all_enc` rồi delegate `csv_read_chunks`; `"utf-8"`/`""`/khác stream thật (chỉ giữ chunksize+1 dòng trong RAM). BOM UTF-8 (EF BB BF) tự strip ở tầng open của runtime (đã probe cả 3 chế độ mở file).

### 🔎 Sự thật runtime (đã probe, không đoán)
* Chuỗi TKV là byte-per-char: literal U+FEFF raw trong source đọc thành 3 ký tự, `write_file` double-encode — không tạo được fixture BOM thật từ string API; xử lý BOM nằm ở tầng mở file.
* `f.readline()` KHÔNG trả `
` cuối dòng (khác Python); `_read_all_enc` tự ghép `
` giữa các dòng (suite mới bắt được bug thật này).
* Compiler không cho truyền file-handle vào hàm (param không kiểu) và không cho method-call trên kết quả hàm trực tiếp — cả hai quyết định kiến trúc hiện tại.

### 🧪 Kiểm chứng
* Suite mới `pd_check.tkv` (15 checks: parse_dates qua ex/chunks/file/enc, giữ tên cột, epoch đúng, encoding latin-1/mặc định, bỏ qua cột lạ) — `FAILS= 0`.
* Regression toàn bộ xanh: base, vec, comp, apply, strings, rel, core, num, arr, feat, stat, stats, v15, io, dt, csv2.

---

## ⚡ Phiên Bản 1.0.2 (19/09/2026) - Nâng Cấp Hiệu Năng & Tính Năng

**`TokenVector.Data`** v1.0.2 tập trung vào nâng cấp thuật toán và các API làm sạch dữ liệu, tất cả được xác minh bằng **11/11 check suite xanh**.

### 🚀 Nâng cấp hiệu năng
* **`DataFrame.sort_by` O(n log n)** — merge sort ổn định (`_ms_rows`) thay thế insertion sort O(n²).
* **`asof_join` O(n log m)** — frame phải được sort 1 lần theo thời gian (merge sort ổn định `_ms_ts`), mỗi dòng trái được khớp bằng binary search; **hỗ trợ frame phải chưa sort**; khi bằng thời gian, chọn mốc sớm nhất thỏa điều kiện.
* **Rolling O(n)** — `win_rolling_std` viết lại theo công thức truy hồi sum/sum² một lượt quét (trước là O(n·window) 2 lượt); `win_rolling_mean` dựng mask validity ngay trong vòng chính.
* **Fast-path all-valid cho vector math (19/09/2026, vòng 2)** — `vec_add`, `vec_sub`, `vec_mul`, `vec_div`, `vec_add_scalar`, `vec_mul_scalar`, `vec_abs`, `vec_sqrt`, `vec_exp`, `vec_log`, `vec_pow` cùng 12 phép so sánh (`vec_gt/ge/lt/le/eq/ne[_scalar]`) quét validity mask đúng 1 lần (có cache qua `Mask.all1()`), kéo nhánh dtype/null ra ngoài vòng lặp; mask kết quả dựng bằng khởi tạo sẵn thay vì `set()` từng phần tử. Đo ở 5M rows: chuỗi `vec_add_scalar`+`vec_mul` **~286 ms → ~182 ms (−36%)**; B1 500k 20.4→17,7 ms. Phần chênh còn lại với numpy là chi phí append output (~10 ns/phần tử) — cần compiler hỗ trợ pre-allocation/SIMD để thu hẹp tiếp.
* **CSV reader single-pass v2 (20/09/2026)** — `csv_read_string` split mỗi dòng đúng 1 lần thẳng vào grid phẳng (bỏ vòng round-trip join → split qua ký tự US), bỏ `` theo từng dòng thay vì replace cả dòng, infer dtype có short-circuit (cell pure-digit bỏ qua check float/bool; fail int thì phân loại float/bool ngay trong bước đó), parse int bằng builtin `int()` thay vòng per-character thủ công. Probe riêng: 100k×3 **409 → 208 ms (−49%)**; trong bench B5 363 → 292 ms.
* **Sửa `make_series` mất null-mask (20/09/2026)** — wrapper Col→Series trước đây làm rơi validity mask nên mọi cột số/bool đọc từ CSV/JSON đều im lặng thành all-valid; giờ wrapper bảo toàn mask khi có null.
* **`sort_by` so sánh inline (20/09/2026)** — `_ms_rows` so sánh field của `RowKey` trực tiếp thay vì gọi `_rk_before` per-compare (~1,7 triệu call ở n=100k); đo được **trung tính** (123 → 122 ms) vì hằng số copy record của merge là chủ đạo — bước thắng tiếp cần key dạng value-kind hoặc hỗ trợ compiler.
* **Fast-path materialize join (20/09/2026)** — `_materialize_joined` đi đường copy branchless từng cell khi join không sinh row unmatched (quét `-1` một lượt O(total)) và cột nguồn all-valid (cache `Mask.all1()`); mask kết quả tạo all-valid O(1) thay vì `set()` từng phần tử. Áp dụng cho mọi loại join (inner/left/right/full/multi/asof) với fallback đúng: left/right/full/asof giữ slow path 2 nhánh đầy đủ ngữ nghĩa null. B4 (100k×100k inner → 10M rows × 4 cột): **1.789 → ~1.660 ms (−7%)**.
* **Arrow bỏ mask khi all-valid (20/09/2026)** — `arrow_write_string` ghi 1 dòng sentinel `-1` thay vì 1 dòng mỗi bit validity cho cột số/bool all-valid; `arrow_read_string` nhận diện sentinel và vẫn đọc được file cũ dạng bit tường minh. B6 write+read 100k: **403 → ~330 ms (−18%)**.
* **Đo B3 khi ấm (20/09/2026)** — `groupby_agg` (3 expr, 500k rows, 3 nhóm) chạy lại **~36 ms khi ấm** so với pandas 33,4 ms — thực chất ngang ngửa; `_compute_agg` vốn đã có fast-path all-valid (probe: vòng agg chỉ ~1 ms trong 36 ms), còn phase hash nhóm (~40 ms) là sàn runtime dict của ngôn ngữ.
* **Refactor bỏ pairs-list trong join — chủ động không làm (20/09/2026)** — phần dựng 2×10M pairs đo được ~126 ms và sàn append từng phần tử vẫn là chủ đạo sau đó; đụng vào 6 call site (mỗi cái có ngữ nghĩa suffix/skip/null riêng) không đáng rủi ro cho ~7% còn lại. Xem lại khi compiler có pre-allocation.
* **Thống kê Welford 1 lượt (20/09/2026)** — `series_variance` và `agg_variance` viết lại theo thuật toán Welford một lượt (trước đây 2–3 lượt: một lượt tính mean + một lượt bình phương lệch qua call `is_null`/`get_f64` từng phần tử); `series_sum`/`series_min`/`series_max` đọc thẳng buffer có kiểu, kéo nhánh dtype/null ra ngoài vòng, đếm non-null qua cache pop-count của mask; `_compute_agg` nhánh MIN/MAX bỏ pass SUM thừa, nhánh có-null của f64/i64 gộp SUM/MEAN/MIN/MAX/FIRST/LAST/STD vào một lượt duy nhất thay vì dựng list trung gian. Đo ở 5M rows: `series_variance` **~115 → ~52 ms/lần (−55%)**, `agg_variance` **~133 → ~50 ms (−62%)**. Suite mới `stat_check.tkv` (22 kiểm tra gồm parity Welford vs 2-lượt, ngữ nghĩa null, i64/bool, STD qua groupby).
* `Mask.all1()` mới — O(1) khi cache pop-count còn hợp lệ, ngược lại quét một lượt thoát sớm và tự cache (`set()`/`set_all()` làm mất cache).

### 🛡️ Null Propagation (VectorMath)
* `vec_add`, `vec_sub`, `vec_mul`, `vec_div`, `vec_add_scalar`, `vec_mul_scalar`, `vec_abs`, `vec_sqrt`, `vec_exp`, `vec_log`, `vec_pow` truyền null xuyên suốt: dòng nào có null đầu vào thì dòng kết quả cũng null (null vào → null ra).

### ✨ API mới
* **Series** — `ffill`, `bfill`, `str_map_contains`, `str_map_startswith`, `str_map_endswith`, `str_map_upper`, `str_map_lower`, `str_map_replace`, `between(lo, hi, inclusive)`, `is_in_f64(danh_sách)`, `is_in_str(danh_sách)`.
* **DataFrame** — `drop_duplicates(subset)`, `duplicated_mask(subset)`, `value_counts(tên_cột)` (sắp giảm dần theo count, ổn định).
* Helper — `str_startswith`, `str_endswith`.

### 🧪 Kiểm chứng
* Check suite mới `feat_check.tkv` bao phủ toàn bộ tính năng v1.0.2 kể cả null propagation.
* Kiểm chứng tích hợp fast-path vs slow-path (giữ null) cho các vec op viết lại, f64/i64, kèm luồng filter qua mask (37 check).
* Suite mới `csv2_check.tkv`: infer i64/f64/bool, field có quote với delimiter và quote nhân đôi bên trong, giữ null end-to-end qua `make_series`, xử lý CRLF, chế độ không header, kèm probe hiệu năng 100k dòng.
* `join_multi` và `groupby_agg` nhiều khóa dùng composite key chuỗi qua dict O(1) (dict lồng nhau không tương thích codegen ở chế độ build nhiều module; lưu ý: bản build DLL gộp mọi module thành một nên hạn chế này chỉ áp dụng khi compile module `.tkv` standalone).
* Toàn bộ suite xanh: `base_check`, `comp_check`, `core_check`, `rel_check`, `io_check`, `num_check`, `arr_check`, `feat_check`, `vec_check`, `csv2_check`, `stat_check` (+ chạy bench).

### 📦 Artifact
* `TokenVector.Data.dll` — build lại từ `tokenvector_data_all.tkv` (tkvc → IL → ilasm), xác minh bằng smoke test reflection.
* `TokenVector.Data.1.0.2.nupkg` — bump version, cập nhật release notes.

---

## 🚀 Phiên Bản 1.0.0 (13/09/2026) - Bản Phát Hành Chính Thức Đầu Tiên

**`TokenVector.Data`** v1.0.0 là phiên bản nền tảng đầu tiên của thư viện xử lý dữ liệu dạng bảng và Columnar DataFrame hiệu năng cao, được thiết kế chuyên biệt cho hệ sinh thái ngôn ngữ lập trình **TokenVector** và trình biên dịch .NET 8 LTS CIL AOT.

---

### 🌟 Điểm Nhấn Kiến Trúc & Tính Năng Nổi Bật

#### 1. Lưu Trữ Bộ Nhớ Theo Cột (Chuẩn Apache Arrow)
* **Cột định kiểu liên tục (`Column<T>`):** Vector bộ nhớ unmanaged phẳng cho các kiểu dữ liệu nguyên thủy (số và boolean), tối ưu hóa tuyệt đối bộ nhớ đệm L1/L2/L3 của CPU.
* **Bố cục nhị phân UTF-8 độ dài biến thiên (`StringColumn`):** Bộ đệm byte `byte[]` liên tục kèm mảng offset `int[]` tăng đơn điệu, triệt tiêu hoàn toàn hiện tượng phân mảnh bộ nhớ của Garbage Collector (GC).
* **Mảng phân mảnh đa khối (`ChunkedArray`):** Hỗ trợ ghép nối không cấp phát lại bộ nhớ và xử lý dữ liệu streaming dạng batch.

#### 2. Quản Lý Null Bằng Bitboard 64-bit (`BitmapMask`)
* Quản lý giá trị null bằng mặt nạ bit 64-bit word-aligned (bit `1` = hợp lệ, bit `0` = null).
* Đếm bit bằng chỉ thị phần cứng POPCNT (`BitOperations.PopCount`) và các toán tử bitwise SIMD (`&`, `|`, `^`, `~`).
* Trích xuất vị trí bit set độ phức tạp $O(N)$ (`ToIndices()`) với kỹ thuật bỏ qua bit bằng Trailing Zero Count.

#### 3. Đa Luồng Không GIL & Tăng Tốc Phần Cứng SIMD
* **Phép toán vector hóa SIMD (`VectorMath`):** Số học vector hóa (`+`, `-`, `*`, `/`, `%`, `Pow`), hàm đơn thức (`Abs`, `Sqrt`, `Exp`, `Log`), và các phép so sánh (`>`, `>=`, `<`, `<=`, `==`, `!=`).
* **Hàm cửa sổ phân tích (`WindowFunctions`):** Tính trung bình trượt (Rolling Mean), tổng trượt (Rolling Sum), độ lệch chuẩn trượt (Rolling Std), độ trễ/dẫn trước (`Shift`), sai phân rời rạc (`Diff`), tổng tích lũy (`CumSum`), và xếp hạng thứ tự (`Rank`).
* **Tổng hợp thống kê (`Aggregations`):** Tính toán vector hóa các đại lượng `Sum`, `Mean`, `Min`, `Max`, `Median`, `Std`, `Variance`, `Quantile` tự động bỏ qua giá trị null.

#### 4. Engine Xử Lý Quan Hệ & Biến Đổi Cấu Trúc Bảng
* **Parallel Radix Hash Join (`JoinEngine`):** Nối song song đa khóa hỗ trợ `Inner`, `Left`, `Right`, `FullOuter`, và `Cross` join.
* **AsOf Join Chuỗi Thời Gian Tài Chính:** Khớp dữ liệu thời gian bất đồng bộ tần suất cao theo các hướng `Backward`, `Forward`, và `Nearest` kèm ngưỡng sai số thời gian (tolerance) và phân vùng theo nhóm.
* **GroupBy Đa Cột Song Song (`GroupByEngine`):** Phân vùng băm song song với nhiều biểu thức tổng hợp cùng lúc (`Agg.Sum`, `Agg.Mean`, `Agg.Count`, `Agg.Min`, `Agg.Max`, `Agg.Std`, `Agg.First`, `Agg.Last`).
* **Biến Đổi Bảng (`ReshapeEngine`):** Đa luồng cho `Pivot` (từ dạng dài sang dạng rộng), `Melt` (từ dạng rộng sang dạng dài), `ConcatVertical` (ghép dọc), và `ConcatHorizontal` (ghép ngang).

#### 5. Đọc/Ghi I/O Tốc Độ Cao & Xử Lý Out-Of-Core
* **Bộ Đọc CSV Đa Luồng (`FastCsvReader`):** Phân luồng đọc song song theo khối, xử lý trích xuất dấu nháy kép RFC 4180 và tự động suy luận Schema.
* **Bộ Ghi CSV Bộ Đệm Cao (`FastCsvWriter`):** Ghi luồng trực tiếp với bộ đệm byte UTF-8 tối ưu hóa I/O.
* **Bộ Đọc NDJSON / JSON Lines (`FastJsonReader`):** Đọc streaming tiết kiệm RAM cho các bản ghi JSON dạng dòng.
* **Engine Apache Arrow IPC Feather (`ArrowIpcEngine`):** Tuần tự hóa và giải tuần tự hóa nhị phân Zero-Copy theo chuẩn Apache Arrow IPC Stream và Feather.
* **Lưu Trữ Out-Of-Core (`OutOfCoreDataFrame`):** Lưu trữ tệp ánh xạ bộ nhớ (Memory-Mapped Files) cho phép truy vấn các tập dữ liệu vượt quá dung lượng RAM vật lý.

#### 6. Cầu Nối Zero-Copy Sang `TokenVector.Numerics`
* Trích xuất tức thì không sao chép bộ nhớ từ `Series` 1D và `DataFrame` 2D sang `TokenVector.Numerics.Core.NDArray<T>`.
* Chuyển đổi trực tiếp sang đồ thị tính toán vi phân `TokenVector.Numerics.Autograd.Tensor<T>` phục vụ huấn luyện mô hình học sâu.
* Phương thức khởi tạo hai chiều `DataFrame.FromNDArray<T>` và `DataFrame.FromTensor<T>`.

#### 7. Module Ngôn Ngữ TokenVector Thuần & Đặc Tả Cú Pháp
* Module TokenVector thuần (`tv_data.tkv`) cung cấp cú pháp `import tv.data as td`, `td.DataFrame`, `td.Series`, `td.read_csv`, `td.read_feather`, `td.concat`.
* Bộ tài liệu đặc tả chuẩn hóa: `TOKENVECTOR_SYNTAX_SPEC.md` và `TOKENVECTOR_SYNTAX_SPEC_VI.md`.

---

### 🧪 Đảm Bảo Chất Lượng & Kết Quả Kiểm Thử

* **Tổng số ca kiểm thử tự động:** 51
* **Vượt qua (Passed):** 51 (100% Green)
* **Thất bại (Failed):** 0
* **Bỏ qua (Skipped):** 0
* **Thời gian thực thi:** 47 ms trên nền tảng .NET 8.0 LTS cấu hình Release.

---

### 📦 Các Gói & Tệp Phân Phối Đã Đóng Gói

* `TokenVector.Data.dll` (Assembly lõi tương thích .NET 8.0 LTS và AOT)
* `TokenVector.Data.1.0.0.nupkg` (Gói NuGet tiêu chuẩn)
* `TokenVector.Data.1.0.0.snupkg` (Gói NuGet Symbols)
* `TokenVector.Data-v1.0.0-Release.zip` (Gói lưu trữ phân phối đầy đủ)
