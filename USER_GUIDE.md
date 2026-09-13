# TokenVector.Data - Comprehensive Developer Guide

[ 🇬🇧 English ](USER_GUIDE.md) | [ 🇻🇳 Tiếng Việt ](USER_GUIDE_VI.md)

This guide provides deep architectural details, API references, and practical code examples for developing with **`TokenVector.Data`**.

---

## 1. Columnar Memory Architecture

`TokenVector.Data` represents 2D tables as a collection of independent contiguous 1D column vectors (`Column<T>` and `StringColumn`), aligning directly with Apache Arrow physical layout standards.

### 1.1 Contiguous Typed Columns (`Column<T>`)
Numeric and boolean primitive vectors are allocated as flat unmanaged memory buffers. Null entries are tracked out-of-band via a 64-bit word Bitboard mask (`BitmapMask`).

```csharp
var col = new Column<double>(length: 1000, nullable: true);
col.SetValue(0, 42.5);
col.SetNull(1);
```

### 1.2 Arrow Variable-Length Binary UTF-8 Columns (`StringColumn`)
Strings are stored contiguously in a single flat `byte[]` UTF-8 buffer accompanied by an `int[]` monotonic offset array:

```text
Offsets Buffer: [ 0,  5,  5,  11, 17 ]
Data Buffer:    [ 'h','e','l','l','o', 'w','o','r','l','d','g','o','o','d','b','y','e' ]
Entries:        [ "hello", null, "world", "goodbye" ]
```

---

## 2. DataFrame Construction & Selection

### 2.1 Building from Series
```csharp
var df = new DataFrame(
    Series.FromValues("ticker", new[] { "AAPL", "MSFT", "GOOGL", "AMZN" }),
    Series.FromValues("price", new[] { 185.50, 410.25, 142.10, 178.90 }),
    Series.FromValues("volume", new[] { 50000000L, 25000000L, 30000000L, 40000000L })
);
```

### 2.2 Slicing, Indexing & Projections
```csharp
// 1. Column selection
Series priceSeries = df["price"];

// 2. Row slicing
DataFrame sub = df[0..2];

// 3. Fast boolean filtering
BitmapMask mask = df["price"] > 150.0;
DataFrame highPriced = df[mask];
```

---

## 3. High-Throughput I/O Engine

### 3.1 Parallel CSV Parsing
```csharp
var df = FastCsvReader.ReadFile("large_market_data.csv", new CsvReadOptions
{
    Delimiter = ',',
    HasHeader = true,
    PreviewRowsForInference = 500
});
```

### 3.2 Apache Arrow IPC Feather Streaming
```csharp
// Save to Arrow Feather format (Zero-Copy)
ArrowIpcEngine.WriteFile(df, "data.feather");

// Read from Arrow Feather file
DataFrame restored = ArrowIpcEngine.ReadFile("data.feather");
```

### 3.3 Out-Of-Core Processing
```csharp
// Persist dataset to memory-mapped binary storage
OutOfCoreDataFrame.Persist(df, "massive_dataset.dat");

// Open without loading entire dataset to RAM
using var ooc = OutOfCoreDataFrame.Open("massive_dataset.dat");
DataFrame batch = ooc.ReadBatch(offset: 100000, length: 50000);
```

---

## 4. Multi-Threaded GroupBy & Aggregations

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

### 5.1 Relational Hash Join
```csharp
var joined = orders.Join(customers, leftOn: "customer_id", rightOn: "id", joinType: JoinType.Inner);
```

### 5.2 Financial AsOf Join
Matches asynchronous, high-frequency tick data:
```csharp
var tradesWithQuotes = trades.AsOfJoin(
    quotes,
    leftOn: "timestamp",
    rightOn: "timestamp",
    by: "symbol",
    direction: AsOfDirection.Backward,
    tolerance: 5000.0 // max 5000 nanoseconds diff
);
```

---

## 6. Zero-Copy Bridge to `TokenVector.Numerics`

```csharp
using TokenVector.Data.Interop;
using TokenVector.Numerics.Core;
using TokenVector.Numerics.Autograd;

// 1. Zero-Copy Series to 1D NDArray
NDArray<double> vec = df["price"].ToNDArray<double>();

// 2. Extract 2D matrix
NDArray<double> matrix = df.ToNDArray<double>(new[] { "price", "volume" });

// 3. Convert to Differentiable Autograd Tensor
Tensor<double> X = df.ToTensor<double>(new[] { "price" }, requiresGrad: true);
var loss = (X * 2.0).Data.Buffer[0];
```
