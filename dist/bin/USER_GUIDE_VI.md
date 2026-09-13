# TokenVector.Data - Sổ Tay Hướng Dẫn Phát Triển Toàn Diện

[ 🇬🇧 English ](USER_GUIDE.md) | [ 🇻🇳 Tiếng Việt ](USER_GUIDE_VI.md)

Tài liệu này cung cấp chi tiết kiến trúc chuyên sâu, tài liệu tham khảo API và các ví dụ mã nguồn thực tế khi phát triển với thư viện **`TokenVector.Data`**.

---

## 1. Kiến Trúc Bộ Nhớ Dạng Cột (Columnar Memory)

`TokenVector.Data` tổ chức bảng 2D thành tập hợp các vector cột 1D độc lập, liên tục trong bộ nhớ (`Column<T>` và `StringColumn`), tuân thủ chặt chẽ tiêu chuẩn Apache Arrow.

### 1.1 Cột Định Kiểu Liên Tục (`Column<T>`)
Các vector số nguyên, số thực và boolean được cấp phát dưới dạng bộ đệm unmanaged liên tục. Các giá trị null được theo dõi ngoài băng (out-of-band) qua mặt nạ bitboard 64-bit word (`BitmapMask`).

```csharp
var col = new Column<double>(length: 1000, nullable: true);
col.SetValue(0, 42.5);
col.SetNull(1);
```

### 1.2 Cột Chuỗi UTF-8 Định Dạng Arrow (`StringColumn`)
Chuỗi được lưu liên tục trong một mảng byte UTF-8 `byte[]` duy nhất kèm theo mảng vector offset `int[]` tăng đơn điệu:

```text
Mảng Offset: [ 0,  5,  5,  11, 17 ]
Mảng Byte:   [ 'h','e','l','l','o', 'w','o','r','l','d','g','o','o','d','b','y','e' ]
Giá trị:     [ "hello", null, "world", "goodbye" ]
```

---

## 2. Khởi Tạo & Thao Tác Với DataFrame

### 2.1 Tạo DataFrame từ các Series
```csharp
var df = new DataFrame(
    Series.FromValues("ticker", new[] { "AAPL", "MSFT", "GOOGL", "AMZN" }),
    Series.FromValues("price", new[] { 185.50, 410.25, 142.10, 178.90 }),
    Series.FromValues("volume", new[] { 50000000L, 25000000L, 30000000L, 40000000L })
);
```

### 2.2 Cắt Lát, Đánh Chỉ Mục & Chiếu Cột
```csharp
// 1. Lấy Series theo tên cột
Series priceSeries = df["price"];

// 2. Cắt lát dòng
DataFrame sub = df[0..2];

// 3. Lọc điều kiện nhanh qua mặt nạ boolean
BitmapMask mask = df["price"] > 150.0;
DataFrame highPriced = df[mask];
```

---

## 3. Engine Đọc/Ghi I/O Tốc Độ Cao

### 3.1 Đọc File CSV Đa Luồng
```csharp
var df = FastCsvReader.ReadFile("large_market_data.csv", new CsvReadOptions
{
    Delimiter = ',',
    HasHeader = true,
    PreviewRowsForInference = 500
});
```

### 3.2 Tuần Tự Hóa Apache Arrow IPC Feather
```csharp
// Lưu sang định dạng Arrow Feather (Zero-Copy)
ArrowIpcEngine.WriteFile(df, "data.feather");

// Đọc lại từ file Feather
DataFrame restored = ArrowIpcEngine.ReadFile("data.feather");
```

### 3.3 Xử Lý Out-Of-Core
```csharp
// Lưu trữ bảng vào tệp ánh xạ bộ nhớ
OutOfCoreDataFrame.Persist(df, "massive_dataset.dat");

// Mở file mà không cần load toàn bộ vào RAM
using var ooc = OutOfCoreDataFrame.Open("massive_dataset.dat");
DataFrame batch = ooc.ReadBatch(offset: 100000, length: 50000);
```

---

## 4. Gom Nhóm GroupBy & Tổng Hợp Đa Luồng

```csharp
var summary = df.GroupBy("sector", "country").Agg(
    Agg.Count("ticker", "total_assets"),
    Agg.Mean("price", "average_price"),
    Agg.Sum("volume", "total_volume"),
    Agg.Std("price", "volatility")
);
```

---

## 5. Parallel Hash Join & Financial AsOf Join

### 5.1 Phép Nối Quan Hệ Hash Join
```csharp
var joined = orders.Join(customers, leftOn: "customer_id", rightOn: "id", joinType: JoinType.Inner);
```

### 5.2 AsOf Join Dành Cho Chuỗi Thời Gian
Khớp lệnh dữ liệu tick bất đồng bộ tần suất cao:
```csharp
var tradesWithQuotes = trades.AsOfJoin(
    quotes,
    leftOn: "timestamp",
    rightOn: "timestamp",
    by: "symbol",
    direction: AsOfDirection.Backward,
    tolerance: 5000.0 // độ lệch tối đa 5000 nanoseconds
);
```

---

## 6. Cầu Nối Zero-Copy Sang `TokenVector.Numerics`

```csharp
using TokenVector.Data.Interop;
using TokenVector.Numerics.Core;
using TokenVector.Numerics.Autograd;

// 1. Trích xuất Zero-Copy từ Series sang 1D NDArray
NDArray<double> vec = df["price"].ToNDArray<double>();

// 2. Trích xuất ma trận 2D
NDArray<double> matrix = df.ToNDArray<double>(new[] { "price", "volume" });

// 3. Chuyển đổi sang Tensor có thể tính vi phân tự động
Tensor<double> X = df.ToTensor<double>(new[] { "price" }, requiresGrad: true);
var loss = (X * 2.0).Data.Buffer[0];
```
