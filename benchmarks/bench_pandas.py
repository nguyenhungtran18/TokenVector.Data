# -*- coding: utf-8 -*-
# Benchmark pandas 2.3.3 - tai lap DUNG du lieu cua tvsrc/bench.tkv (B1-B7) de so sanh cung may.
# Moi bai: chay 3 lan, lay gia tri nho nhat (best-of-3, giong phia TokenVector).
import io
import time

import numpy as np
import pandas as pd

N = 500_000
NJ = 100_000

ids = np.arange(N, dtype=np.int64)
vals = (np.arange(N) % 10_000) * 0.001
groups = np.where(np.arange(N) % 3 == 0, "A", np.where(np.arange(N) % 3 == 1, "B", "C"))

results = []


def bench(name, fn, runs=3):
    best = float("inf")
    last = None
    for _ in range(runs):
        t0 = time.perf_counter()
        last = fn()
        dt = (time.perf_counter() - t0) * 1000.0
        best = min(best, dt)
    results.append((name, best, last))
    print(f"{name:28s} best_ms={best:10.3f} info={last}")


# B1: colA * 2.5 + colB
b1_df = pd.DataFrame({"a": vals, "b": vals})


def b1():
    s = b1_df["a"] * 2.5 + b1_df["b"]
    return f"chk={s.iloc[10]:.17g}"


bench("B1 vec_arith n=500000", b1)

# B2: filter val > 5.0
f_df = pd.DataFrame({"a": vals})


def b2():
    res = f_df[f_df["a"] > 5.0]
    return f"kept={len(res)}"


bench("B2 filter n=500000", b2)

# B3: groupby 3 nhom x 3 agg (count, mean, sum)
g_df = pd.DataFrame({"g": groups, "id": ids, "a": vals, "b": vals})


def b3():
    gr = g_df.groupby("g", sort=True).agg(cnt=("a", "count"), avg=("a", "mean"), total=("b", "sum"))
    return f"groups={len(gr)}"


bench("B3 groupby n=500000", b3)

# B4: hash join inner (100k x 100k -> 10M rows)
ldf = pd.DataFrame({"k": np.arange(NJ) % 1000, "lv": np.arange(NJ).astype(float)})
rdf = pd.DataFrame({"k": np.arange(NJ) % 997, "rv": np.arange(NJ) * 2.0})


def b4():
    jr = ldf.merge(rdf, on="k", how="inner")
    return f"out={len(jr)}"


bench("B4 join l=100000 r=100000", b4, runs=1)  # 10M rows output - chay 1 lan de tiet kiem RAM

# B5: CSV parse 100k (build text tuong duong phia TKV)
csv_lines = ["id,val,grp"]
for i in range(100_000):
    csv_lines.append(f"{i},{vals[i]},{groups[i]}")
csv_text = "\n".join(csv_lines)


def b5():
    cdf = pd.read_csv(io.StringIO(csv_text))
    return f"rows={len(cdf)}"


bench("B5 csv parse 100k", b5)

# B6: Arrow IPC round-trip - N/A (pyarrow khong cai trong moi truong nay)
results.append(("B6 arrow_write+read n/a", None, "pyarrow not installed"))
print("B6 arrow_write+read          skipped (pyarrow not installed)")

# B7: sort 100k float
sa = (np.arange(100_000) * 7919) % 100_000
s_df = pd.DataFrame({"a": sa.astype(float)})


def b7():
    ss = s_df.sort_values("a", ascending=True)
    return f"first={ss['a'].iloc[0]:.1f}"


bench("B7 sort_by n=100000", b7)

print("---")
for name, ms, info in results:
    print(f"{name:28s} {ms if ms is not None else 'n/a':>12} {info}")
