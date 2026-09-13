# TokenVector.Data - Thông Tin Phát Hành (Release Notes)

[ 🇬🇧 English ](RELEASE_NOTES.md) | [ 🇻🇳 Tiếng Việt ](RELEASE_NOTES_VI.md)

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
