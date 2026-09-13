# Báo Cáo Kiểm Thử Tự Động & Đảm Bảo Chất Lượng - TokenVector.Data

[ 🇬🇧 English ](TEST_REPORT.md) | [ 🇻🇳 Tiếng Việt ](TEST_REPORT_VI.md)

**Ngày thực thi:** 13/09/2026  
**Nền tảng:** .NET 8.0 LTS (`net8.0`)  
**Công cụ kiểm thử:** xUnit v2.5.6 (64-bit)  
**Cấu hình:** Release (`-c Release`)  
**Kết quả kiểm thử:** **51 / 51 Passed (100% Green, 0 Failed, 0 Skipped)**  
**Tổng thời gian chạy:** 47 ms

---

## 1. Tóm Tắt Theo Danh Mục Kiểm Thử

| Tệp kiểm thử | Danh mục kiểm thử | Số ca test | Trạng thái |
| :--- | :--- | :--- | :--- |
| `ColumnAndSeriesTests.cs` | Bitboards, Columns, String UTF-8, ChunkedArray, Series Math & Casting | 12 | ✅ 12/12 Passed |
| `DataFrameBasicTests.cs` | Khởi tạo DataFrame 2D, Cắt lát, Đánh chỉ mục Mask, Sắp xếp, Describe, Drop/Fill Null | 7 | ✅ 7/7 Passed |
| `FilterAndComputeTests.cs` | SIMD VectorMath, Lọc đa luồng FilterEngine, Hàm cửa sổ (Rolling/Shift/Diff/Rank) | 8 | ✅ 8/8 Passed |
| `GroupByTests.cs` | Gom nhóm đa cột, Tổng hợp (Sum, Mean, Count, Std, First, Last), Xử lý Null | 4 | ✅ 4/4 Passed |
| `JoinAndReshapeTests.cs` | Radix Hash Joins (Inner, Left, Right, FullOuter, Cross), AsOfJoin, Pivot, Melt, Concat | 9 | ✅ 9/9 Passed |
| `CsvAndJsonIOTests.cs` | Phân tích CSV đa ký tự phân tách, RFC 4180 quotes, NDJSON, JSON mảng, Suy luận kiểu | 7 | ✅ 7/7 Passed |
| `ArrowAndMemMapTests.cs` | Luồng nhị phân Apache Arrow IPC Feather, Tệp ánh xạ bộ nhớ OutOfCore | 2 | ✅ 2/2 Passed |
| `NumericsInteropTests.cs` | Chuyển đổi Zero-Copy giữa DataFrame/Series và NDArray/Tensor | 4 | ✅ 4/4 Passed |
| **Tổng cộng** | **Toàn bộ bộ kiểm thử thư viện** | **51** | **✅ 51/51 Passed (100%)** |
