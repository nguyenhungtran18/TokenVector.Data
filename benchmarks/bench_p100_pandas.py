# -*- coding: utf-8 -*-
# Benchmark pandas 2.3.3 - tai lap DUNG du lieu cua benchmarks/bench_p100.tkv (C1-C6).
# Moi bai: chay 3 lan, lay gia tri nho nhat (best-of-3, giong phia TokenVector).
import time

import numpy as np
import pandas as pd

N = 500_000

ids = np.arange(N, dtype=np.int64)
vals = (np.arange(N) % 10_000) * 0.001
grps = np.where(np.arange(N) % 3 == 0, "A", np.where(np.arange(N) % 3 == 1, "B", "C"))
ts = 1_735_689_600_000 + np.arange(N, dtype=np.int64) * 60_000
sids = pd.array([str(i).zfill(7) for i in range(N)], dtype=object)

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
    print(f"{name:34s} best_ms={best:10.3f} info={last}")


df = pd.DataFrame({"id": ids, "v": vals, "g": grps, "ts": ts, "sid": sids})

# C1: Series.shift(1)
s = df["v"]
bench("C1 shift+1 n=500000", lambda: f"chk={s.shift(1).iloc[10]:.17g}")

# C2: Series.fillna (10% null)
ns = s.copy()
ns.iloc[3::10] = np.nan
bench("C2 fillna n=500000", lambda: f"rows={ns.fillna(-1.0).shape[0]}")

# C3: groupby rolling window 7 (sum), du lieu ts tang dan trong nhom
bench(
    "C3 groupby_rolling w7 n=500000",
    lambda: f"rows={df.groupby('g', sort=True)['v'].rolling(7).sum().shape[0]}",
)

# C4: groupby resample 5 min x group (mean)
dt_idx = pd.to_datetime(df["ts"], unit="ms")
bench(
    "C4 groupby_resample 5min n=500000",
    lambda: f"buckets={df.set_index(dt_idx).groupby('g', sort=True)['v'].resample('5min').mean().shape[0]}",
)

# C5: reindex tren index chuoi sid (key buoc 2 + 20% key moi)
keys = pd.Index([str(i * 2).zfill(7) for i in range(N // 2)] + [str(2 * N + i).zfill(7) for i in range(N // 5)])
sid_df = df.set_index("sid")
bench(
    "C5 reindex (str index)",
    lambda: f"rows={sid_df.reindex(keys).shape[0]}",
)

# C6: join inner tren index chuoi sid (N x N/2)
rdf = pd.DataFrame(
    {"sid": [str(i * 2).zfill(7) for i in range(N // 2)], "rv": np.arange(N // 2) * 0.5}
).set_index("sid")
bench(
    "C6 merge_on_index inner",
    lambda: f"rows={sid_df.join(rdf, how='inner').shape[0]}",
)
