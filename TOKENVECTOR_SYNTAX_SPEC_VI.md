# ĐẶC TẢ CÚ PHÁP XỬ LÝ DỮ LIỆU BẢNG & DATAFRAME DẠNG CỘT CHO NGÔN NGỮ TOKENVECTOR

[ 🇬🇧 English ](TOKENVECTOR_SYNTAX_SPEC.md) | [ 🇻🇳 Tiếng Việt ](TOKENVECTOR_SYNTAX_SPEC_VI.md)

**Mã tài liệu:** TKV-SPEC-DATA-2026-V1  
**Mục tiêu:** Đặc tả chuẩn hóa cú pháp xử lý dữ liệu dạng bảng, Columnar DataFrame, và chuỗi thời gian cho ngôn ngữ lập trình **TokenVector** (`tv.data` / `tokenvector.data`).

> **Ghi chú 2026-09-24:** đây là phác thảo thiết kế ngôn ngữ (aspirational) —
> chain-method (`df["x"]/100`) và import `tv.data` chưa tồn tại. Thư viện hiện
> tại dùng hàm tự do (`series_*`, `groupby_*`, …); xem `README_VI.md` để biết
> API thật.

---

## 1. Không Gian Tên & Nhập Module

Trong TokenVector, module DataFrame chuẩn được import qua **`tv.data`** (bí danh chuẩn là **`td`**) hoặc trực tiếp từ `tv`:

```tokenvector
# 1. Nhập module chuẩn
import tv.data as td

# 2. Nhập các lớp và hàm cụ thể
from tv.data import DataFrame, Series, read_csv, read_json, read_feather, concat, Agg
```

---

## 2. Khởi Tạo & Khảo Sát DataFrame

```tokenvector
# 1. Tạo DataFrame từ từ điển (dictionary) các cột
df = td.DataFrame({
    "id": [1, 2, 3, 4, 5],
    "name": ["Alice", "Bob", "Charlie", "David", "Eve"],
    "score": [88.5, 92.0, 79.5, 95.0, 84.0]
})

# 2. Khảo sát kích thước, schema và xem trước dữ liệu
print(df.shape)        # (5, 3)
print(df.column_names) # ["id", "name", "score"]
print(df.head(3))
print(df.describe())
```

---

## 3. Thao Tác Đọc/Ghi I/O Tốc Độ Cao

```tokenvector
# 1. Đọc CSV đa luồng với bộ quét phân tách SIMD
df_csv = td.read_csv("market_data.csv", delimiter=",", has_header=True)

# 2. Đọc NDJSON / JSON Lines dạng luồng (Streaming)
df_json = td.read_json("events.ndjson")

# 3. Đọc định dạng nhị phân Apache Arrow IPC Feather (Zero-Copy)
df_feather = td.read_feather("features.arrow")

# 4. Xuất dữ liệu
df.to_csv("output.csv")
df.to_feather("output.arrow")
```

---

## 4. Truy Vấn, Cắt Lát & Phân Tích Cửa Sổ (Window Analytics)

```tokenvector
# 1. Lọc điều kiện siêu tốc qua 64-bit Bitboard BitmapMask
high_scores = df.filter(lambda row: df["score"][row] > 90.0)

# 2. Chọn và biến đổi cột
df_sub = df.select("name", "score")
df_new = df.with_column("score_pct", df["score"] / 100.0)

# 3. Hàm cửa sổ trượt và hàm phân tích thống kê
prices = df["score"]
rolling_avg = prices.rolling_mean(window=3)
rolling_vol = prices.rolling_std(window=5)
lag1 = prices.shift(1)
diff1 = prices.diff(1)
cum_sum = prices.cumsum()
```

---

## 5. Gom Nhóm Đa Cột & Tổng Hợp (GroupBy)

```tokenvector
# GroupBy song song phân vùng băm (Hash-Partitioned)
summary = df.groupby("department", "role").agg(
    Agg.sum("salary", out_name="total_salary"),
    Agg.mean("age", out_name="avg_age"),
    Agg.count("id", out_name="headcount")
)
```

---

## 6. Phép Nối Quan Hệ & AsOf Join Chuỗi Thời Gian

```tokenvector
# 1. Phép nối băm quan hệ tiêu chuẩn (Hash Join)
joined = left_df.join(right_df, left_on="user_id", right_on="user_id", how="inner")

# 2. AsOf Join chuỗi thời gian tài chính tần suất cao
# Khớp lệnh giao dịch với báo giá gần nhất (quote.time <= trade.time) trong ngưỡng sai số
trades_with_quotes = trades.asof_join(
    quotes,
    left_on="timestamp",
    right_on="timestamp",
    by="symbol",
    tolerance=1000.0 # Ngưỡng 1000ns
)
```

---

## 7. Xoay Chuyển Cấu Trúc Bảng: Pivot & Melt

```tokenvector
# 1. Pivot từ dạng dài (long) sang dạng rộng (wide)
wide_df = long_df.pivot(index="date", columns="sensor", values="reading")

# 2. Melt từ dạng rộng (wide) sang dạng dài (long)
long_df = wide_df.melt(id_vars=["date"], value_vars=["temp", "humidity"])
```

---

## 8. Tương Thích Zero-Copy Với `TokenVector.Numerics`

```tokenvector
import tv.linalg as la
from tv import tensor

# 1. Trích xuất Zero-Copy từ Series sang 1D NDArray
vec = df["score"].to_ndarray()

# 2. Trích xuất từ DataFrame sang 2D NDArray
matrix = df.to_ndarray(["feature1", "feature2", "feature3"])

# 3. Chuyển đổi trực tiếp sang Tensor vi phân (Autograd)
X = df.to_tensor(["feature1", "feature2"], requires_grad=True)
y_pred = X @ weights
loss = (y_pred - target_tensor).pow(2).sum()
loss.backward()
```
