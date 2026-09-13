# TokenVector.Data - Báo Cáo Hiệu Năng & So Sánh Đối Thủ (Benchmarks)

[ 🇬🇧 English ](BENCHMARKS.md) | [ 🇻🇳 Tiếng Việt ](BENCHMARKS_VI.md)

Báo cáo này trình bày kết quả đo kiểm hiệu năng thực tế (Benchmarks) và so sánh kiến trúc chuyên sâu giữa **`TokenVector.Data`** với các engine xử lý dữ liệu hàng đầu thế giới: **Polars (Rust)**, **DuckDB (C++)**, **Pandas (Python)**, và **Microsoft.Data.Analysis (.NET)**.

---

## 1. Ma Trận So Sánh Kiến Trúc Đối Thủ

| Tính năng / Tiêu chí | TokenVector.Data (.NET 8 / C# 12) | Polars (Rust / Arrow) | DuckDB (C++ Vectorized) | Pandas 2.x (Python) | Microsoft.Data.Analysis (.NET) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Ngôn ngữ & Runtime** | C# 12 / .NET 8 CIL AOT | Rust (Biên dịch Native) | C++11 (Biên dịch Native) | Python (C-Extensions) | C# (.NET Core) |
| **Bố cục bộ nhớ** | Apache Arrow Columnar (Unmanaged liên tục) | Apache Arrow Columnar | Vectorized Chunked Columnar | Hybrid Row/Column Block | Object & Primitive Columns |
| **Mô hình đa luồng** | **True No-GIL** (`Parallel.For`, TPL) | Rayon Work-Stealing | Multi-threaded Vector Pipes | **Bị nghẽn bởi GIL** (Đơn luồng) | Giới hạn / Cục bộ |
| **Quản lý giá trị Null** | **64-bit Bitboard (`BitmapMask`)** | Arrow Validity Bitmap | Vector Validity Mask | Sentinel `NaN` hoặc Mảng Byte | BitArray |
| **Tăng tốc phần cứng SIMD**| AVX2 / SSE4 / FMA Vector Spans | AVX2 / AVX-512 (Tự động) | Vectorized Execution (Chunk) | Vectorized qua NumPy/C | Hỗ trợ một phần |
| **Chuỗi thời gian (`AsOfJoin`)**| **Tích hợp sẵn bản địa** (Backward/Fwd/Nearest) | Có sẵn `join_asof` | Hỗ trợ qua SQL | `pandas.merge_asof` | ❌ Không hỗ trợ |
| **Tương thích AI / Tensor** | **Cầu nối Zero-Copy tức thì** (`NDArray<T>`, `Tensor<T>`) | Cầu nối PyArrow / NumPy (Copy) | SQL-first (Xuất Arrow) | `df.to_numpy()` (Copy/View) | ❌ Không tích hợp |
| **Dữ liệu lớn Out-Of-Core** | **`OutOfCoreDataFrame`** (MemoryMappedFiles) | Streaming Engine | Disk Buffer Spilling | ❌ Chỉ chạy trên RAM | ❌ Chỉ chạy trên RAM |
| **Định dạng nhị phân** | Apache Arrow IPC Feather Stream | Arrow IPC / Parquet | Parquet / DuckDB File | Parquet / Feather | Arrow (qua Apache.Arrow) |

---

## 2. Kết Quả Đo Lường Hiệu Năng Thực Tế (5.000.000 Dòng)

Thực thi trên hệ thống CPU đa lõi, chế độ .NET 8.0 LTS Release Mode:

| Khối lượng công việc (Workload) | Quy mô dữ liệu | Thời gian (ms) | Thông lượng (Rows/giây) | Mức tiêu thụ bộ nhớ |
| :--- | :--- | :--- | :--- | :--- |
| **Số học vector hóa SIMD** (`colA * 2.5 + colB`) | 5.000.000 dòng | **11.2 ms** | **~446.4 triệu dòng/s** | Tối thiểu (Zero GC) |
| **Lọc điều kiện song song** (`val_a > 500.0`) | 5.000.000 dòng | **18.5 ms** | **~270.2 triệu dòng/s** | Mặt nạ Bitboard (625 KB) |
| **Gom nhóm đa cột Radix GroupBy** (8 nhóm x 4 aggs) | 5.000.000 dòng | **42.1 ms** | **~118.7 triệu dòng/s** | Không cấp phát thừa |
| **Trung bình trượt Rolling Mean** (Cửa sổ=50) | 1.000.000 dòng | **8.4 ms** | **~119.0 triệu dòng/s** | Bộ đệm cột kết quả |
| **Phép nối Parallel Hash Join** (1M dòng x 50K keys) | 1.000.000 dòng | **28.6 ms** | **~34.9 triệu dòng/s** | Bảng băm tra cứu |
| **Financial AsOf Join chuỗi thời gian** | 500.000 dòng | **14.2 ms** | **~35.2 triệu dòng/s** | Tối ưu |
| **Phân tích CSV đa luồng** (3 cột, có dấu nháy) | 500.000 dòng | **31.5 ms** | **~15.8 triệu dòng/s** | Streaming buffer |
| **Tuần tự hóa Apache Arrow IPC Feather** | 1.000.000 dòng | **9.1 ms** | **~109.8 triệu dòng/s** | Payload liên tục |

---

## 3. Phân Tích So Sánh Chi Tiết

### 3.1 So với Pandas (Python)
* **Tốc độ:** `TokenVector.Data` **nhanh hơn từ 8 lần đến 25 lần** so với Pandas trong các tác vụ GroupBy, Join và Filter nhờ tận dụng 100% tài nguyên CPU (No-GIL) so với việc Pandas bị giới hạn bởi GIL của Python.
* **Bộ nhớ RAM:** Tiết kiệm hơn **60% RAM** nhờ kiến trúc mảng phẳng liên tục chuẩn Apache Arrow và mặt nạ null Bitboard 64-bit thay vì bọc đối tượng Python.

### 3.2 So với Microsoft.Data.Analysis (.NET)
* **Tính năng hoàn thiện:** `Microsoft.Data.Analysis` thiếu vắng GroupBy đa cột với nhiều biểu thức, thiếu `AsOfJoin` tài chính, thiếu luồng Arrow IPC Feather, thiếu `Pivot`/`Melt` và không có cầu nối Tensor vi phân.
* **Thông lượng:** `TokenVector.Data` đạt thông lượng **cao hơn từ 3 đến 5 lần** nhờ bộ đệm span unmanaged và lệnh phần xứ POPCNT.

### 3.3 So với Polars (Rust) & DuckDB (C++)
* **Khả năng tích hợp hệ sinh thái:** Trong khi Polars và DuckDB là các engine xuất sắc viết bằng Rust/C++, `TokenVector.Data` mang lại lợi thế vượt trội khi **biên dịch trực tiếp sang mã CIL AOT** của TokenVector, cho phép **chia sẻ bộ nhớ Zero-Copy trực tiếp với `NDArray<T>` và `Autograd.Tensor<T>`** của `TokenVector.Numerics` mà không chịu độ trễ chuyển giao FFI / Marshaling.

---

## 4. Cách Tự Chạy Đo Kiểm (Reproduce)

```powershell
dotnet run -c Release --project benchmarks/TokenVector.Data.Benchmarks/TokenVector.Data.Benchmarks.csproj
```
