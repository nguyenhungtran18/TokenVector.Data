# TOKENVECTOR LANGUAGE - NATIVE COLUMNAR DATAFRAME & TABULAR DATA SPECIFICATION

[ 🇬🇧 English ](TOKENVECTOR_SYNTAX_SPEC.md) | [ 🇻🇳 Tiếng Việt ](TOKENVECTOR_SYNTAX_SPEC_VI.md)

**Document Code:** TKV-SPEC-DATA-2026-V1  
**Target:** Formal native tabular processing, columnar DataFrame, and time-series syntax specification for the **TokenVector** programming language (`tv.data` / `tokenvector.data`).

---

## 1. Namespaces & Module Imports

In TokenVector, the standard DataFrame module is imported via **`tv.data`** (aliased as **`td`**) or directly from `tv`:

```tokenvector
# 1. Standard module import
import tv.data as td

# 2. Specific class and function imports
from tv.data import DataFrame, Series, read_csv, read_json, read_feather, concat, Agg
```

---

## 2. DataFrame Creation & Inspection

```tokenvector
# 1. Create DataFrame from dictionary of columns
df = td.DataFrame({
    "id": [1, 2, 3, 4, 5],
    "name": ["Alice", "Bob", "Charlie", "David", "Eve"],
    "score": [88.5, 92.0, 79.5, 95.0, 84.0]
})

# 2. Inspect shape, schema, and preview
print(df.shape)        # (5, 3)
print(df.column_names) # ["id", "name", "score"]
print(df.head(3))
print(df.describe())
```

---

## 3. High-Throughput I/O Operations

```tokenvector
# 1. Multi-threaded CSV Parsing with SIMD delimiter scanner
df_csv = td.read_csv("market_data.csv", delimiter=",", has_header=True)

# 2. Streaming NDJSON / JSON Lines Parsing
df_json = td.read_json("events.ndjson")

# 3. Apache Arrow IPC Feather Binary Format (Zero-Copy)
df_feather = td.read_feather("features.arrow")

# 4. Exporting DataFrames
df.to_csv("output.csv")
df.to_feather("output.arrow")
```

---

## 4. Querying, Slicing & Window Analytics

```tokenvector
# 1. Fast Predicate Filtering via 64-bit Bitboard BitmapMask
high_scores = df.filter(lambda row: df["score"][row] > 90.0)

# 2. Column Selection and Mutation
df_sub = df.select("name", "score")
df_new = df.with_column("score_pct", df["score"] / 100.0)

# 3. Window & Analytical Functions
prices = df["score"]
rolling_avg = prices.rolling_mean(window=3)
rolling_vol = prices.rolling_std(window=5)
lag1 = prices.shift(1)
diff1 = prices.diff(1)
cum_sum = prices.cumsum()
```

---

## 5. Multi-Column GroupBy & Aggregations

```tokenvector
# Parallel Hash-Partitioned GroupBy
summary = df.groupby("department", "role").agg(
    Agg.sum("salary", out_name="total_salary"),
    Agg.mean("age", out_name="avg_age"),
    Agg.count("id", out_name="headcount")
)
```

---

## 6. Relational Joins & Financial AsOf Joins

```tokenvector
# 1. Standard Relational Hash Join
joined = left_df.join(right_df, left_on="user_id", right_on="user_id", how="inner")

# 2. High-Frequency Financial Time-Series AsOf Join
# Matches trades with the most recent quote (quote.time <= trade.time) within tolerance
trades_with_quotes = trades.asof_join(
    quotes,
    left_on="timestamp",
    right_on="timestamp",
    by="symbol",
    tolerance=1000.0 # 1000ns tolerance
)
```

---

## 7. Tabular Reshaping: Pivot & Melt

```tokenvector
# 1. Pivot from long format to wide format
wide_df = long_df.pivot(index="date", columns="sensor", values="reading")

# 2. Melt from wide format to long format
long_df = wide_df.melt(id_vars=["date"], value_vars=["temp", "humidity"])
```

---

## 8. Zero-Copy Interoperability with `TokenVector.Numerics`

```tokenvector
import tv.linalg as la
from tv import tensor

# 1. Zero-Copy extraction from Series to 1D NDArray
vec = df["score"].to_ndarray()

# 2. Extraction from DataFrame to 2D NDArray
matrix = df.to_ndarray(["feature1", "feature2", "feature3"])

# 3. Direct conversion to Autograd Differentiable Tensor
X = df.to_tensor(["feature1", "feature2"], requires_grad=True)
y_pred = X @ weights
loss = (y_pred - target_tensor).pow(2).sum()
loss.backward()
```
