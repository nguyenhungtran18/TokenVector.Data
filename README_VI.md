# TokenVector.Data

[ 🇬🇧 English ](README.md) | [ 🇻🇳 Tiếng Việt ](README_VI.md)

[![.NET 8](https://img.shields.io/badge/.NET-8.0%20LTS-purple.svg)](https://dotnet.microsoft.com/)
[![C# 12](https://img.shields.io/badge/C%23-12.0-blue.svg)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-51%2F51%20Passed-brightgreen.svg)]()

**`TokenVector.Data.dll`** là thư viện xử lý dữ liệu dạng bảng và DataFrame dạng cột (Columnar Storage) hiệu năng siêu cao, được viết bằng **C# 12 / .NET 8 LTS** tối ưu riêng cho hệ sinh thái ngôn ngữ lập trình **TokenVector** và trình biên dịch .NET CIL AOT.

Thư viện áp dụng kiến trúc bố cục bộ nhớ contiguous theo chuẩn Apache Arrow, đa luồng không GIL (True No-GIL Multithreading), mặt nạ null bitboard 64-bit (`BitmapMask`), tăng tốc phần cứng SIMD, phép nối chuỗi thời gian tài chính tần suất cao `AsOfJoin`, phân tích CSV/JSON đa luồng, tuần tự hóa Apache Arrow IPC Feather, và cầu nối Zero-Copy trực tiếp sang `TokenVector.Numerics.NDArray<T>` cùng `TokenVector.Numerics.Autograd.Tensor<T>`.

---

## 📋 Đặc Điểm Kiến Trúc Nổi Bật

| Tính năng | TokenVector.Data (.NET 8 / C# 12) |
| :--- | :--- |
| **Lưu trữ cột (Columnar Memory)** | Bộ nhớ unmanaged liên tục (`Column<T>`), chuỗi UTF-8 định dạng Arrow với buffer byte + vector offset (`StringColumn`) |
| **Biểu diễn giá trị Null** | Mặt nạ bitboard 64-bit `BitmapMask` khai thác chỉ thị phần cứng POPCNT và SIMD bitwise |
| **Đa luồng không GIL** | Tận dụng 100% các lõi CPU qua `Parallel.For` và TPL trong GroupBy, Hash Join, Parse CSV, và Filter |
| **Tăng tốc phần cứng** | Vector hóa SIMD (AVX2, SSE4, FMA) cho các phép toán số học, hàm cửa sổ và thống kê |
| **Phép nối quan hệ** | Parallel Radix Hash Join đa khóa (`Inner`, `Left`, `Right`, `FullOuter`, `Cross`) và **`AsOfJoin`** cho chuỗi thời gian tài chính |
| **Biến đổi cấu trúc bảng** | `Pivot` (dài sang rộng), `Melt` (rộng sang dài), `ConcatVertical`, `ConcatHorizontal` |
| **I/O Luồng dữ liệu** | Phân tích CSV đa luồng tự động suy luận Schema, đọc streaming NDJSON, và luồng nhị phân Apache Arrow IPC Feather |
| **Xử lý Out-Of-Core** | DataFrame ánh xạ bộ nhớ (`OutOfCoreDataFrame`) hỗ trợ truy vấn các tập dữ liệu vượt quá dung lượng RAM |
| **Cầu nối Zero-Copy Tensor** | Chuyển đổi tức thì, không sao chép bộ nhớ giữa `DataFrame`/`Series` và `NDArray<T>` / `Tensor<T>` của `TokenVector.Numerics` |

---

## 📊 Ma Trận So Sánh Kiến Trúc Đối Thủ

| Tiêu chí | TokenVector.Data | Polars | DuckDB | Pandas 2.x | MS.Data.Analysis |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Ngôn ngữ** | C# 12 / .NET 8 AOT | Rust Native | C++11 Native | Python / C | C# (.NET Core) |
| **Bộ nhớ** | Arrow Columnar | Arrow Columnar | Vector Chunked | Row/Col Block | Object / Array |
| **Đa luồng** | **No-GIL (100% Cores)** | Rayon Stealing | Vector Pipes | **Bị nghẽn bởi GIL**| Giới hạn |
| **Quản lý Null**| **64-bit Bitboard** | Arrow Validity | Validity Mask | `NaN` / Byte Mask | BitArray |
| **SIMD** | AVX2 / SSE4 / FMA | AVX2 / AVX-512 | Vector Chunks | NumPy / C | Một phần |
| **AsOf Join** | **Tích hợp sẵn** | `join_asof` | Qua SQL | `merge_asof` | ❌ Không |
| **AI / Tensor** | **Zero-Copy Bridge** | FFI Copy | Arrow Export | Copy / View | ❌ Không |
| **Out-Of-Core** | **MemoryMappedFiles** | Streaming | Disk Spilling | ❌ Không | ❌ Không |
| **Binary I/O** | Arrow IPC Feather | Arrow / Parquet | Parquet / DuckDB | Feather / Parquet | Arrow |

---

## ⚡ Kết Quả Đo Lường Hiệu Năng Thực Tế (5.000.000 Dòng)

Thực thi trên hệ thống CPU đa lõi, chế độ .NET 8.0 LTS Release Mode (xem chi tiết tại [BENCHMARKS_VI.md](BENCHMARKS_VI.md)):

| Khối lượng công việc (Workload) | Quy mô dữ liệu | Thời gian (Latency) | Thông lượng (Throughput) | Mức tiêu thụ bộ nhớ |
| :--- | :--- | :--- | :--- | :--- |
| **Số học vector hóa SIMD** (`colA * 2.5 + colB`) | 5.000.000 dòng | **11.2 ms** | **~446.4 triệu dòng/s** | Tối thiểu (Zero GC) |
| **Lọc điều kiện song song** (`val_a > 500.0`) | 5.000.000 dòng | **18.5 ms** | **~270.2 triệu dòng/s** | Mặt nạ Bitboard (625 KB) |
| **Gom nhóm đa cột Radix GroupBy** (8 nhóm x 4 aggs) | 5.000.000 dòng | **42.1 ms** | **~118.7 triệu dòng/s** | Zero Heap Allocation |
| **Trung bình trượt Rolling Mean** (Window=50, O(N)) | 1.000.000 dòng | **8.4 ms** | **~119.0 triệu dòng/s** | Bộ đệm cột kết quả |
| **Phép nối Parallel Hash Join** (1M dòng x 50K keys) | 1.000.000 dòng | **28.6 ms** | **~34.9 triệu dòng/s** | Bảng băm tra cứu |
| **Financial AsOf Join chuỗi thời gian** | 500.000 dòng | **14.2 ms** | **~35.2 triệu dòng/s** | Tối ưu |
| **Phân tích CSV đa luồng** (3 cột, có dấu nháy) | 500.000 dòng | **31.5 ms** | **~15.8 triệu dòng/s** | Streaming buffer |
| **Tuần tự hóa Apache Arrow IPC Feather** | 1.000.000 dòng | **9.1 ms** | **~109.8 triệu dòng/s** | Payload liên tục |

---

## 🧪 Kiểm Thử & Đảm Bảo Chất Lượng

Tất cả **51/51 automated unit tests** đã vượt qua thành công với tỷ lệ 100% ở chế độ Release:
```powershell
dotnet test TokenVector.Data.sln -c Release
```
```text
Passed!  - Failed: 0, Passed: 51, Skipped: 0, Total: 51, Duration: 47 ms - TokenVector.Data.Tests.dll (net8.0)
```

---

## 🚀 Hướng Dẫn Nhanh (C#)

```csharp
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Data.Compute;
using TokenVector.Data.Relational;
using TokenVector.Data.Interop;

// 1. Tạo DataFrame
var df = new DataFrame(
    Series.FromValues("user_id", new[] { 1, 2, 3, 4, 5 }),
    Series.FromStrings("dept", new[] { "IT", "HR", "IT", "Sales", "HR" }),
    Series.FromValues("salary", new[] { 75000.0, 52000.0, 88000.0, 61000.0, 58000.0 })
);

// 2. Gom nhóm GroupBy và tổng hợp song song
var summary = df.GroupBy("dept").Agg(
    Agg.Count("salary", "headcount"),
    Agg.Mean("salary", "avg_salary"),
    Agg.Max("salary", "max_salary")
);

// 3. Phép nối AsOf Join tần suất cao
var matched = trades.AsOfJoin(quotes, leftOn: "timestamp", rightOn: "timestamp", direction: AsOfDirection.Backward);

// 4. Cầu nối Zero-Copy sang TokenVector.Numerics NDArray
var numericMatrix = df.ToNDArray<double>(new[] { "user_id", "salary" });
```

---

## 💻 Hướng Dẫn Nhanh (Ngôn ngữ thuần TokenVector)

```tokenvector
import tv.data as td
from tv.data import Agg

# Đọc CSV và thực hiện phân tích gom nhóm
df = td.read_csv("employees.csv")
report = df.groupby("department").agg(
    Agg.mean("salary", out_name="avg_salary"),
    Agg.count("id", out_name="headcount")
)
print(report)
```

Xem tài liệu đầy đủ tại [Sổ tay nhà phát triển (Tiếng Anh)](USER_GUIDE.md), [Báo cáo Benchmark (Tiếng Việt)](BENCHMARKS_VI.md) hoặc [Hướng dẫn sử dụng (Tiếng Việt)](USER_GUIDE_VI.md).
