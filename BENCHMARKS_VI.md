# Báo Cáo Benchmark v1.0.2 — TokenVector (TKV) vs Pandas (cùng máy)

[ 🇬🇧 English ](BENCHMARKS.md) | [ 🇻🇳 Tiếng Việt ](BENCHMARKS_VI.md)

> **Phạm vi báo cáo này:** đo thực nghiệm trên máy hiện tại giữa runtime **TokenVector (TKV)** (bản v1.0.2, chạy qua `tkvc` → IL → JIT) và **pandas 2.3.3** (Python 3.14.7), với **đúng cùng dữ liệu** cho từng bài đo.
>
> **Lưu ý quan trọng:** các số liệu "C# 12 / .NET 8 AOT" trong `BENCHMARKS.md` (5M rows, 16 cores) là của **bản C# cũ đã bị xóa khỏi repo**, đo trên phần cứng khác — chỉ mang tính tham khảo, **không so sánh trực tiếp** được với bảng dưới đây.

---

## 0b. Chạy lại cùng phiên 2026-09-23 (tvsrc v1.9 / DLL 1.0.6-dev) + bench tính năng mới

> So lại cả 2 engine cùng phiên sau khi build v1.9 (pandas-100 closure).
> Best-of-3 cả hai phía, cùng máy. Script: `tvsrc/bench.tkv` +
> `benchmarks/bench_pandas.py` (B1-B7), `benchmarks/bench_p100.tkv` +
> `benchmarks/bench_p100_pandas.py` (C1-C6, mới).

**B1-B7:** B1 4.6 vs 2.8 ms (pandas ~1.6×) · B2 20.2 vs 4.1 (~4.9×) ·
B3 60.9 vs 29.7 (~2.1×) · B4 1.507 vs 514 ms (~2.9×) · B5 259 vs 38 ms (~6.8×) ·
B6 299 ms · B7 120 vs 8.7 ms (~13.8×).

**C1-C6 (tính năng v1.9 — mới):** C1 shift 4.4 vs 1.2 ms · C2 fillna 16.0 vs 2.6 ·
C3 groupby_rolling(7) 2.117 vs 177 ms · C4 resample(5min) 675 vs 91 ·
C5 reindex 122 vs 67 · **C6 merge-on-index inner 61 vs 86 ms — TKV thắng ~1.4×**
(two-pointer trên keys đã sort, không hash).

> **Sửa perf kèm bench này:** `groupby_rolling` trước đây sort mỗi nhóm bằng
> **selection sort O(n²)** — ở 500k dòng × 3 nhóm thì C3 không chạy xong trong
> 10 phút. Đã thay bằng merge sort của engine (`sort_by`, O(n log n), stable):
> giờ là **2.117 ms**. `p100_check` 73/73 vẫn xanh.
>
> **Phần gap còn lại ở C3/C4:** materialize sub-DataFrame theo dòng
> (`groupby_group`) + append list từng window — sàn per-element append
> (~10 ns/elt) của compiler là chi phí trội. Kernel rolling pre-allocated
> sẽ đóng phần lớn; hoãn đến khi compiler có pre-allocation.


## 1. Điều kiện đo

| Thành phần | Giá trị |
| :--- | :--- |
| Runtime TKV | v1.0.2, build `tvsrc/bench.tkv` (tkvc → IL → .NET JIT). Các phép toán engine dưới đây chạy **single-thread**; bản thân ngôn ngữ có `thread_spawn`/`thread_join` (đã probe: ~4.4× speedup trên quét số học 5M rows với 2 worker) nhưng engine DataFrame chưa dùng |
| Đối thủ | pandas 2.3.3 trên Python 3.14.7 (NumPy backend) |
| Phương pháp | Mỗi bài chạy 3 lần, lấy **best (ms)**; pandas best-of-3 (riêng B4 chạy 1 lần do output 10M rows) |
| Kích thước | B1–B3: 500k rows · B4: join 100k×100k (output 10M) · B5: CSV 100k · B7: sort 100k |
| Script tái lập | `tvsrc/bench.tkv` và `benchmarks/bench_pandas.py` |

## 2. Kết quả (ms — số càng nhỏ càng tốt)

| Bài đo | N | TokenVector (TKV) | pandas 2.3.3 | pandas nhanh hơn |
| :--- | ---: | ---: | ---: | ---: |
| B1 Vector arithmetic (`a*2.5 + b`) | 500.000 | **16,2** | **2,9** | ~5,6× |
| B2 Filter (`val > 5.0`) | 500.000 | **15,2** | **4,4** | ~3,5× |
| B3 GroupBy 3 nhóm × 3 agg | 500.000 | **87,8** (~36 khi ấm) | **34,7** | ~2,5× (1,0× khi ấm) |
| B4 Hash join inner (100k×100k → 10M) | 200.000 input | **1.645** | **545** | ~3,0× |
| B5 CSV parse 100k×3 cột | 100.000 | **237*** | **38,8** | ~6,1× |
| B6 Arrow round-trip | — | **316** (write+read) | n/a (pyarrow chưa cài) | — |
| B7 Sort (`sort_by` merge sort) | 100.000 | **100,2** | **8,1** | ~12,4× |

\* B5 phía TKV chỉ tính `csv_read_string`; thời gian **dựng chuỗi CSV 100k** ở lượt đo này là ~305–340 ms vì benchmark dựng text bằng phép nối chuỗi O(n²) của chính script đo — phần này không thuộc engine parse.

> **Đo lại cùng phiên 20/09/2026 (sau khi viết lại thống kê Welford):** best-of-3 cho cả 2 engine, cùng máy cùng phiên. Các tối ưu thống kê (`series_variance`, `agg_variance`, `_compute_agg`) không nằm trên đường nóng B1–B7 (B3 chỉ dùng COUNT/MEAN/SUM), nên B1–B7 dao động trong nhiễu đo; mức dịch trong bảng đến từ việc đo lại cả 2 engine cùng phiên (lần này pandas đo được B4 545 ms, B5 38,8 ms, B7 8,1 ms). B3 khi ấm vẫn ~36 ms ≈ pandas chạy lạnh.

> **Cập nhật engine 20/09/2026 (CSV v2 + sửa đúng sai):**
> - **Viết lại CSV reader (single-pass v2):** bỏ vòng round-trip join/split ký tự US, bỏ validate kép, infer dtype có short-circuit, parse int bằng builtin `int()` thay vòng per-character. Probe riêng: 100k rows × 3 cột **409 → 208 ms (−49%)**; trong bench B5 parse **363 → 292 ms**.
> - **Sửa đúng sai (`make_series`):** wrapper Col→Series trước đây **làm mất null-mask** nên mọi cột đọc từ CSV/JSON đều im lặng thành all-valid. Null trong frame đọc được giữ nguyên từ nay (đã có suite `csv2_check` phủ).
> - **`sort_by`:** merge giờ so sánh inline qua field thay vì gọi `_rk_before` per-compare (~1,7 triệu call ở n=100k) — đo được **trung tính** (123 → 122 ms): hằng số copy record của merge là chủ đạo, muốn thắng tiếp cần key dạng value-kind hoặc hỗ trợ compiler.
> - Fast-path vec của 19/09 giữ nguyên: 5M rows chuỗi `vec_add_scalar`+`vec_mul` = **182 ms (−36% so với 286)**. Khoảng cách còn lại với pandas là append output từng phần tử (~10 ns/elt) — cần compiler pre-allocation/SIMD (xem `KERNEL_LAB.md`).
> - **Fast-path materialize join (20/09/2026):** `_materialize_joined` giờ phát hiện join không có row unmatched (quét `-1` một lượt O(total)) kết hợp cột nguồn all-valid (cache `Mask.all1()`) để chạy **copy branchless từng cell** với mask all-valid O(1) thay vì 2 nhánh + `set()` từng phần tử. B4 (output 10M rows): **1.789 → ~1.660 ms (−7%)**; đúng sai phủ bởi `rel_check` (left/right/full/asof vẫn đi slow path giữ ngữ nghĩa null).
> - **Arrow bỏ mask khi all-valid (20/09/2026):** `arrow_write_string` ghi 1 dòng sentinel `-1` thay vì 1 dòng mỗi bit validity khi cột số/bool all-valid, và `arrow_read_string` nhận diện sentinel (file cũ có bit tường minh vẫn đọc được). B6 write+read: **403 → ~330 ms (−18%)**.
> - **Đo B3 khi ấm (20/09/2026):** `groupby_agg` 3 expr trên 500k chạy lại chỉ **~36 ms** khi ấm (so với 81–92 ms lạnh trong bench) — **ngang pandas 33,4 ms** về mặt thống kê. `_compute_agg` vốn đã có fast-path all-valid (probe: vòng agg chỉ = 1 ms trong 36 ms); phần còn lại là phase hash nhóm (~40 ms) — sàn runtime của dict-backed grouping trong ngôn ngữ.
> - **Refactor bỏ pairs-list trong join — chủ động không làm:** đã đo phần dựng 2×10M pairs ~126 ms và sàn append vẫn còn sau refactor, trong khi phải đụng 6 call site với ngữ nghĩa suffix/skip/null riêng — không đáng đánh đổi rủi ro cho ~7% còn lại. Xem lại sau khi compiler có pre-allocation.

> **Cập nhật engine 19/09/2026 (fast-path all-valid):** các op vec/so sánh quét validity mask 1 lần và kéo nhánh dtype/null ra ngoài vòng lặp. Ở 500k, B1 cải thiện 20.4 → 17,7 ms và B2 20.3 → 18,1 ms (các lượt sau xuống 15,6 ms khi máy rảnh).

## 2a. Thống kê một lượt (Welford) — 5M rows, 20/09/2026

| Phép đo | Trước | Sau | |
| :--- | ---: | ---: | :--- |
| `series_variance` @5M | ~115 ms | **~52 ms** | **−55%** |
| `agg_variance` @5M | ~133 ms | **~50 ms** | **−62%** |

Tái lập: `cd tvsrc && tkvc.exe build stat_bench.tkv && ./stat_bench.exe`. Chi tiết thuật toán xem `RELEASE_NOTES_VI.md` mục 1.0.2.

## 2b. TokenVector đa luồng (thread cấp ngôn ngữ) — 5M rows × 20 lần

Ngôn ngữ TKV có thread thật (`thread_spawn`/`thread_join` trên `Task.Factory.StartNew`/ThreadPool). Benchmark `benchmarks/bench_tkv_parallel.tkv` chia mỗi bài cho 2 và 4 worker, mỗi worker tự đo thời gian bên trong (lặp 20 lần mỗi phép đo để vượt granularity ~15.6 ms của `DateTime.UtcNow` trên .NET Framework). Wall-clock best-of-3, khối lượng mỗi phép đo:

| Bài đo @ 5M rows × 20 | Tuần tự | 2 worker | 4 worker | Speedup tốt nhất | pandas 2.3.3 (tuần tự) |
| :--- | ---: | ---: | ---: | ---: | ---: |
| B1 Vector arithmetic (`a*2.5+b`) | 228.3 ms | 75.6 ms | 61.1 ms | **3.7×** | 42.0 ms |
| B2 Filter count (`v > 5.0`) | 264.3 ms | 61.8 ms | 74.7 ms | **4.3×** | 9.4 ms |

Đọc kết quả: chỉ với 2–4 worker cấp ngôn ngữ, không sửa gì engine, TKV đã thu hẹp phần lớn khoảng cách với pandas ở bài số học (5.4× → **1.5×**) và đáng kể ở filter (28× → **8.5×**). Speedup chưa tuyến tính vì mỗi worker phải tự sinh lại chunk của mình từ công thức — đa luồng thật của engine sẽ thao tác trên column buffer dùng chung nên không tốn phần này. `cpu_sum_ms` vượt `wall_ms` ở vài lần chạy 4 worker cho thấy ThreadPool điều phối chưa đều; scheduler chunk-per-core chuyên dụng sẽ tốt hơn.

### So sánh cùng số thread (công bằng 8v8, 5M × 20 lần)

| Bài đo | TKV tuần tự (1T) | TKV 8 worker | numpy 8 thread | Chênh TKV 8T vs numpy 8T |
| :--- | ---: | ---: | ---: | ---: |
| B1 Vector arithmetic | 228.3 ms | **25.2 ms** | 17.4 ms | 1.45× |
| B2 Filter count | 264.3 ms | **41.0 ms** | 3.9 ms | 10.5× |

### So sánh cùng số thread (8v8) — kèm lưu ý quan trọng

Khi chuẩn hóa khối lượng (×20 reps cả hai phía), thấy rõ các script song song TKV **không phải so sánh engine cùng loại**: worker TKV *sinh* giá trị chunk ngay lúc chạy (thuần compute, không đi qua mảng), trong khi kernel numpy *đọc/ghi mảng thật 40 MB* (memory-bound). Tổng thời gian đo được ở 5M × 20 reps:

| Cấu hình | B1 arith | B2 filter |
| :--- | ---: | ---: |
| TKV loop, 1T | 228.3 ms | 264.3 ms |
| TKV loop, 8T | 25.2 ms | 41.0 ms |
| numpy kernel, 1T | 730.3 ms | 184.1 ms |
| numpy kernel, 8T | 327.1 ms | 30.8 ms |

TKV "nhanh hơn" numpy ở B1 trong bảng này là do **hai bên làm việc khác nhau** (compute-bound loop vs memory-bound kernel), không phải engine nhanh hơn. So sánh engine-đối-engine hợp lệ vẫn là mục 2 (cột có sẵn trước khi đo ở cả hai phía): engine TKV chậm hơn pandas ~5–6× ở B1/B2. Điều các script song song chứng minh hợp lệ là **khả năng scale thread cấp ngôn ngữ**: ~9× wall-clock từ 1T→8T trên vòng lặp compute (25.2 ms so với 228.3 ms). Máy 28 lõi logic; numpy 4T→8T gần như không tăng vì chunk nhỏ dần so với overhead pool, còn TKV 8T vẫn scale tốt.

> Script: 2/4-worker `benchmarks/bench_tkv_parallel.tkv`, 8-worker `benchmarks/bench_tkv_parallel8.tkv` (biến động ThreadPool: một số run 36–46 ms; bảng ghi best-of-3).

### Vậy tại sao thua pandas? (Không phải vì ngôn ngữ Python)

Phân tích các thất bại cùng-loại (mục 2): pandas/NumPy gần như toàn bộ thời gian nằm trong **kernel C** — so sánh vector hóa SIMD, vòng sum chặt, reduction không cấp phát trên mảng liền khối vừa bộ nhớ cache CPU. Bản thân Python chỉ lo điều phối. Engine TKV thua vì các phép toán là **IL scalar với bounds-check từng phần tử và cấp phát mảng/mask trung gian** (`vec_add` tạo cột mới mỗi lần), không phải vì "bị thông dịch" — vòng lặp IL đã compile vẫn đạt ~2.3 ns/phần tử trên quét thuần compute. Thứ tự ưu tiên suy ra trực tiếp: (1) bỏ cấp phát trung gian (in-place ops), (2) SIMD cho kernel so sánh/số học, (3) đa luồng engine trên buffer dùng chung.

## 3. Đọc kết quả một cách trung thực

- **pandas thắng toàn bộ các bài đơn lược.** Đây là điều dễ hiểu: pandas/NumPy là thư viện C đã tối ưu hơn 15 năm, còn engine DataFrame của TKV hiện chạy từng phép toán **single-thread**, chưa bật SIMD/AVX — khác với kiến trúc quảng bá trong `BENCHMARKS.md` (thuộc bản C# cũ). Tuy nhiên **ngôn ngữ có đa luồng thật** (`thread_spawn`/`thread_join` trên `Task.Factory.StartNew`, ThreadPool): quét số học 5M rows chia cho 2 worker đo được **~4.4× speedup so với tuần tự**, nên đa luồng hóa engine là con đường cải thiện lớn nhất hiện có.
- **Khoảng cách hợp lý:** nhân đơn luồng thuần IL của TKV vẫn đạt cỡ 2.4×–7.7× chậm hơn pandas — không phải độ trễ "cấp bội"; nghẽn lớn nhất là B7 (sort 13×) và B5 (parse CSV ~7.7×).
- **Điểm nghẽn nêu bật:** B4 (join 10M output = 1.75 s) cho thấy bộ sinh kết quả ghép từng dòng của `_build_joined_df` cần vector hóa; B7 cho thấy merge sort IL có hằng số cao (so sánh/quy đổi qua helper).
- **Con đường thu hẹp khoảng cách (theo thứ tự lợi nhuận):**
  1. **Đa luồng hóa engine** bằng chính nguyên thủy `thread_spawn`/`thread_join` sẵn có — đã đo được **3.7×–4.3× speedup wall-clock** chỉ với 2–4 worker (mục 2b), trước cả khi động vào engine. Lưu ý: worker không nhận tham số (chia sẻ qua global) và mỗi worker chỉ trả về 1 scalar; materialize song song cần pattern output dùng chung.
  2. Sinh song song + buffer pre-alloc cho `_build_joined_df` và `_materialize_joined`.
  3. SIMD/AVX cho vector math nếu runtime hỗ trợ intrinsic.
- **Số C# cũ trong `BENCHMARKS.md`** (nhanh hơn pandas 8–25×) được đo trên .NET 8 AOT, 16 lõi, 5M rows — hợp lệ cho bản C# nhưng **không đại diện** cho runtime TKV hiện tại; hai bảng không thể đặt cạnh nhau như cùng một sản phẩm.

## 4. Cách tái lập

```bash
# TokenVector (TKV)
cd tvsrc
/d/SkillSpector-TKV/tkvc.exe build --entry run bench.tkv
./bench.exe

# pandas (500k, khớp bench.tkv)
python benchmarks/bench_pandas.py

# TokenVector đa luồng (5M rows, tuần tự vs 2/4 worker)
cd tvsrc && /d/SkillSpector-TKV/tkvc.exe build --entry run ../benchmarks/bench_tkv_parallel.tkv && ./bench_tkv_parallel.exe

# TokenVector 8 worker (so sánh cùng số thread với numpy 8T)
cd tvsrc && /d/SkillSpector-TKV/tkvc.exe build --entry run ../benchmarks/bench_tkv_parallel8.tkv && ./bench_tkv_parallel8.exe
```

Để bổ sung cột **Polars/DuckDB/pyarrow** trên chính máy này: `pip install polars duckdb pyarrow` rồi mở rộng `benchmarks/bench_pandas.py` — cấu trúc script cho phép thêm engine dễ dàng.
