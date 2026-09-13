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

Xem tài liệu đầy đủ tại [Sổ tay nhà phát triển (Tiếng Anh)](USER_GUIDE.md) hoặc [Hướng dẫn sử dụng (Tiếng Việt)](USER_GUIDE_VI.md).
