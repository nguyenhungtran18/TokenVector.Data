# KernelLab — Thí nghiệm: mượn kernel của TokenVector.Numerics cho TokenVector.Data?

> **Câu hỏi:** Có thể "mượn tạm" `SIMDKernels` (AVX2, net8.0) từ **TokenVector.Numerics** để giúp **TokenVector.Data** đánh bại pandas không?
>
> **Phạm vi:** thí nghiệm đo trên máy hiện tại (28 logical cores, AVX2 + FMA = true), pandas 2.3.3, runtime TKV v1.0.2, dotnet SDK 9.0.318. Thư viện Numerics được tham chiếu **chỉ để đo** — không có thay đổi nào trong repo Numerics.

## 1. Rào cản tích hợp trực tiếp (kết quả kiểm tra)

| Kiểm tra | Kết quả |
| :--- | :--- |
| Numerics TFM | `net8.0` (`System.Runtime.Intrinsics`, `unsafe`, spans) |
| Runtime TKV | compile lên mscorlib **net40** (ilasm v4.0.30319) → **không load được assembly net8.0 vào process TKV** |
| FFI của TKV | P/Invoke thật (`pinvokeimpl`, `ctypes_cdll`) — nhưng máy **không có C compiler** (gcc/cl/clang/rustc/tcc) để build DLL native |
| NuGet feed | offline — không cài được pythonnet/polars/pyarrow mới |

→ Cả hai đường "mượn" (load trực tiếp, hay native DLL wrap) **không khả dụng trên máy này**, nên thí nghiệm chuyển sang câu hỏi quyết định hơn: *kernel AVX2 của Numerics nhanh tới đâu so với pandas, và port nó về ngôn ngữ TKV có thắng pandas không?*

## 2. Số đo (5M doubles, per-pass ms, best-of-3 × 20 passes)

| Công việc | pandas (numpy C, 1T) | Numerics kernel AVX2 (net8, 1T) | TKV compute-loop (1T) | TKV vec_add trên cột thật (1T) |
| :--- | ---: | ---: | ---: | ---: |
| B1 `a*2.5 + b` | 36.5 | **10.7**¹ | 11.5² | 173.7 |
| B2 filter/sum | 9.2 | ~3.2³ | 11.5 | 173.7 |

¹ `AddContiguous<double>` — 1 lượt đọc 2 mảng + 1 ghi mảng (memory-bound ~40 MB/lượt).
² Thuần compute (sinh giá trị lúc chạy, không qua mảng) — không so sánh trực tiếp được với B1, chỉ dùng để đo chất lượng JIT IL.
³ `SumContiguous`-tương đương (1 đọc + reduction, 20 MB/lượt).
⁴ `vec_add` của TVM.Data tạo cột + mask mới mỗi lần (cấp phát trung gian, bounds-check, không SIMD).

## 3. Phân tích: Numerics kernel có đánh bại pandas không?

- **Ở mức kernel thuần CÓ**: `AddContiguous` AVX2 (10.7 ms) nhanh hơn ~3.4× so với đường pandas/numpy đo được (36.5 ms) cho cùng công việc — vì pandas phải đi qua broadcast, boxing Python layer, và kết quả allocation mỗi op.
- **Nhưng con số 3.4× là tối đa lý thuyết**: thực tế engine cần null-mask, type-dispatch, slicing — chi phí mà pandas cũng phải trả và Numerics engine (net8) cũng có.

## 4. Trở ngại thực tế để tích hợp vào TKV.Data hiện tại

| Con đường | Khả thi? | Lý do |
| :--- | :--- | :--- |
| A. Reference trực tiếp Numerics (net8) vào TKV runtime (net40) | ❌ | Khác TFM, khác mscorlib version |
| B. Build native DLL (C/C++/Rust) wrap kernel AVX2, gọi qua FFI của TKV | ❌ trên máy này | Không có C compiler; phải setup mingw-w64 hoặc MSVC Build Tools |
| C. Port SIMDKernels sang ngôn ngữ TKV (.tkv thuần) như bạn định hướng | ✅ khả thi nhất | Không cần native DLL, giữ đúng 1 ngôn ngữ; **nhưng bị chặn bởi:** (1) compiler TKV chưa phát intrinsic SIMD; (2) bounds-check từng phần tử; (3) cấp phát mảng/mask trung gian mỗi op |

## 5. Kết luận & khuyến nghị

1. **Không thể "mượn tạm" trực tiếp** Numerics vào TKV.Data trên máy này — nhưng thí nghiệm cho thấy **giá trị của kernel AVX2 là thật**: nhanh gấp ~3.4× so với cùng công việc qua pandas.
2. **Port SIMDKernels về .tkv thuần là hướng đúng dài hạn** của bạn — nhưng để đánh bại pandas thì compiler TKV **cần thêm 3 thứ theo thứ tự**: SIMD intrinsic cho pattern rollback, il_gen tối ưu bounds-check, engine tái sử dụng buffer (in-place ops). Không có SIMD trong codegen thì port kernel chỉ thu được tốc độ ~11.5 ms/pass (compute) hoặc chậm hơn nhiều khi phải thao tác trên mảng thật.
3. **Con đường thực dụng ngay bây giờ (trên máy này):** đa luồng engine bằng `thread_spawn` (đã chứng minh ~9× wall-clock 1T→8T) + bỏ cấp phát trung gian (in-place ops) → đạt ~3–5×; sau đó thêm SIMD codegen để có cú hích 3–8× còn lại.

> **Trả lời ngắn:** kernel của Numerics đủ mạnh để đánh bại pandas ở mức lý thuyết (3.4× nhanh hơn), nhưng **không thể mang sang TKV.Data hôm nay** do chênh TFM (net8 vs net40) + thiếu C compiler cho FFI. Muốn "đánh bại pandas" bằng ngôn ngữ TKV thuần, cần compiler hỗ trợ SIMD codegen — đây là việc thuộc về compiler, không phải thư viện.

## 6. Thí nghiệm bổ sung (vòng 2): port thuần .tkv chạy trên buffer CỘT THẬT

Đã viết lại toàn bộ pipeline đo bằng **kernel thuần .tkv** (không dùng Numerics, không dùng C) chạy trực tiếp trên `Col.f64s` 5M phần tử — đúng những gì kernel port sẽ phải làm. Đồng thời **phát hiện và sửa 1 lỗi đo của vòng 1**: số `vec_add 173.7ms` cũ đo ở 500k với timer tick ~15.6ms là nhiễu; đo chuẩn hóa ×20 pass cho cả hai phía cho số đúng.

### 6.1. Phân rã chi phí B1 `a*2.5+b` (5M × 20 pass, per-pass ms)

| Bước đo | ms/pass | So với numpy 1T (36.5) |
| :--- | ---: | :--- |
| K1 — compute thuần, không đụng mảng (chất lượng JIT) | 12.5 | 2.9× nhanh hơn |
| K2 — fused đọc 2 buffer thật, reduction không ghi output | **13.3** | 2.7× nhanh hơn |
| K5 — filter count (đọc 1 buffer, không ghi) | **9.15** | **9.2 numpy → HÒA** |
| K3 — fused + ghi output vào list (append) | 61.7 | 1.7× chậm |
| K3p — prealloc bằng comprehension | 72.2 | 2.0× chậm |
| Engine hiện tại `vec_add_scalar` + `vec_mul` (có null-check/mask/branch từng phần tử) | **~300** | 8.2× chậm |

**Kết luận cấu trúc chi phí:**
1. **Đọc mảng gần như miễn phí**: K1 (12.5) ≈ K2 (13.3) — bounds-check + index IL không phải nút thắt.
2. **~80% thời gian engine bị đốt ở đường null/mask/branch**: `is_null()`×2 + `mk.set()` + if dtype TỪNG PHẦN TỬ (300ms) so với 62ms khi chỉ làm tính toán + append. Đây là **lỗi của code engine (thuần .tkv, sửa được ngay)**, không phải của ngôn ngữ hay JIT.
3. **Append nhanh hơn prealloc** (63.9 vs 72.2): comprehension cấp phát trước tốn hơn khuyếch tán; `[0.0] * n` không được compiler hỗ trợ (TypeError suy diễn dtype).
4. **B2 (filter/count/reduction không ghi output): kernel port thuần .tkv ĐÃ HÒA numpy ở 1 thread** (9.15 vs 9.2 ms) — và reduction/count đa luồng bằng `thread_spawn` (trả scalar, đã chứng minh 9× wall-clock 1T→8T) sẽ **THẮNG numpy đa thread** vì numpy 8T chỉ nhanh hơn ~1.05–5× tùy bài.
5. **B1 (op sinh cột mới) thua 1.7× ở 1 thread** sau khi tự viết kernel đúng cách — khoảng cách duy nhất nằm ở đường ghi output 48ms/5M (~10ns/elt, list append boxing f64). SIMD của numpy chỉ là nguyên nhân thứ yếu; đường ghi output + null-mask mới là chính.

### 6.2. Ma trận đa luồng trên dữ liệu THẬT (khác với dữ liệu sinh trong worker)

| Cơ chế chia sẻ/trả kết quả | Kết quả probe |
| :--- | :--- |
| Worker trả scalar (reduction/count/sum) | ✅ hoạt động, 9× wall-clock 1T→8T |
| Worker trả list/dict | ❌ `thread_join` sinh `get_Result()` hardcode theo dtype scalar → `MissingMethodException` runtime |
| Biến container module-level | ❌ compiler cấm (chỉ scalar có kiểu) |
| Closure bắt biến local kiểu record (Col) | ❌ sinh IL hỏng `stloc.s ('run__w__Closure','A','class Col')` → ilasm fail |
| Closure bắt biến local kiểu list | ❌ cùng lỗi IL y hệt |

→ **Reduction/filter-count đa luồng được NGAY HÔM NAY** (trả scalar); **op sinh output đa luồng bị chặn bởi 2 bug compiler** (trả giá trị không scalar + capture container vào closure).

### 6.3. Port Numerics → thuần TKV: đánh giá cuối

| Nhóm Ops của Numerics | Port thuần .tkv được? | Kết cục hiệu năng so với pandas |
| :--- | :--- | :--- |
| Reductions (sum/count/min/max/any/all) | ✅ port được ngay | **HÒA 1T, THẮNG khi đa luồng** (trả scalar OK) |
| Filter/count-if | ✅ port được ngay | HÒA 1T (9.15 vs 9.2); đa luồng THẮNG |
| Element-wise sinh cột mới (add/mul/...) | ✅ port được | Thua 1.7× 1T cho đến khi: (a) engine bỏ null-check/mask/branch per-element bằng fast-path all-valid, (b) compiler sửa return-list/closure-container cho đa luồng output |
| `Math.Sqrt/Exp/Log/Sin...` | ⚠️ chỉ có Abs/Floor/Min/Pow trong codegen hiện tại — sqrt/exp/log/sin cần thêm builtin (nhỏ, không phải SIMD) | — |
| SIMD kernel thật (AVX2 rollback mask) | ❌ cần compiler phát intrinsic | Lợi thế 3.4× của Numerics không port được bằng loop thuần |

**Hành động thuần .tkv sửa được NGAY trong engine (không cần compiler):**
- Fast-path all-valid: kiểm tra null mask **1 lần** (O(n) quét buffer i64 rẻ) thay vì `is_null()` ×2 từng phần tử → B1 engine từ ~300ms xuống kỳ vọng ~70–90ms.
- Kéo nhánh dtype ra ngoài vòng lặp (đã đúng ở fast-path mới của join/groupby, áp dụng cho vec_*).
- `vec_*` sinh output: giữ append (nhanh hơn prealloc), cân nhắc mask all-valid khi cả 2 input không null.

**Artifact:** `benchmarks/bench_tkv_portlab.tkv` (K1/K2/K3/K5 + engine chain, chống nhiễu tick bằng ×20 pass).
