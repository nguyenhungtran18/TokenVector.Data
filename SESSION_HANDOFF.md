# SESSION HANDOFF — 2026-09-23 (đọc file này trước khi làm tiếp)

## Trạng thái: **PHIÊN MỚI (0b) ĐANG DỞ — parity ~100%, 2 module mới chưa build
pass. Compiler tree (D:\TokenVector) ĐANG DIRTY bởi phiên khác + tkvc.exe dist
HỎNG — dùng tkvc worktree `/d/TokenVector._head_wt/3.code/dist/tkvc.exe` (đã
verify tốt).** Phiên trước (io v1.6.3) đã commit hết: Data 7476d63/b205bb9/
35f7aab/40f1be9/741a937; compiler c5bd63e/18deb7d. Chưa push (đợi user).

---

## 0e. PHIÊN 2026-09-23 (tiếp nữa) — v1.10 kernel nhanh + regression compiler tkvc mới (XONG ✅)

- **Kernel v1.10** (trên toolchain worktree sạch): groupby_rolling mảng phẳng
  O(n log n) đọc trực tiếp cột (2.1s→1.0s @500k), groupby_resample sort-based
  (semantics mới: output sort theo (nhóm,bucket) khớp pandas), merge_on_index
  hash-fallback (bỏ ràng buộc sort, thêm how="right"). p100_check 74/74,
  regression 18/18, DLL/nupkg **1.0.7-dev**, smoke 44/44.
- **Báo cáo regression tkvc dist 17:59** → `docs/COMPILER_REGRESSIONS_20260923.md`
  (R1 KeyError constant, R2 constant import, R3 use-before-def oan, R4 InvalidIL
  pattern hoán đổi list). Thư viện đã né R4 bằng buffer param — khi compiler
  sửa xong R1–R3, build lại p100 bằng tkvc mới để xác nhận.
- **Bài học mới cho handoff**: (1) alias list `a = b` rồi `a[i]=x` làm first-pass
  suy sai scalar — truyền buffer qua param thay vì alias; (2) hoán đổi 2 list
  qua biến tmp → InvalidProgramException trên tkvc cũ — copy từng phần tử.

---

## 0d. PHIÊN 2026-09-23 (tiếp nữa) — v1.9 pandas-100 closure (XONG ✅)

Yêu cầu "phải đạt 100%". Đóng nốt nhóm "tiện ích nhỏ còn sót" — module
`tokenvector_p100.tkv` (21 hàm): missing data DF/Series, groupby_rolling/
groupby_resample, df_set_index/df_reindex/merge_on_index, csv_write_ex
(date_format/quote_all), json_write_values/split/index.

- Suite mới `p100_check` **73/73**; regression **18/18**; merged (all+lib)
  build xanh; DLL rebuild + smoke **44/44**; nupkg **1.0.6-dev**.
- Docs: FUNCTION_PARITY §3i + các row overview cập nhật; RELEASE_NOTES EN/VI.

**Bài học compiler mới (quan trọng):**
1. **`__strtmp` không khai báo** — string-concat dài sinh temp không khai báo,
   có lúc emit im lặng → exe hỏng → BadImageFormatException lúc JIT. Workaround
   tầng thư viện: tách concat dài sang helper riêng, hàm chính ngắn lại.
2. **Byte điều khiển thô (0x01/0x02) trong string literal** → IL vỡ im lặng.
   Phải dùng escape (`""` 4 ký tự, như relational đã làm).
3. **`Series.take` với index −1** crash (physical index) — dựng tay theo dtype
   (pattern `series_shift`) thay vì take.
4. **Typeflow merge biến cùng tên 2 nhánh khác kiểu** (đã biết v1.8) — lặp lại
   ở `series_astype`; luôn đặt tên biến theo nhánh (`vals_b/i/f/s`).

**Còn lại thật sự (ledger trung thực):** Parquet/Feather/read_sql/Excel =
blocked-by-compiler (bitwise R5 + binary IO); MultiIndex = loại trừ có chủ đích
(mô hình position-based). Phần "đóng được ở tầng thư viện" = **0**.

**Bench so pandas (same-session 2026-09-23, v1.9):**
- Script mới `benchmarks/bench_p100.tkv` + `bench_p100_pandas.py` (C1-C6);
  B1-B7 re-run best-of-3. Kết quả đầy đủ ở BENCHMARKS.md §0b.
- **Cảm bấy perf đã vá:** `groupby_rolling` selection sort O(n²) → `sort_by`
  O(n log n) (500k × 3 nhóm: treo >10 phút → 2.117 ms). p100_check 73/73.
- **TKV thắng pandas ở C6** merge-on-index inner (61 vs 86 ms) nhờ two-pointer
  trên keys đã sort — lưu ý: API cần keys sort (zero-pad id số).
- Gap còn lại C3/C4: groupby_group materialize + per-window append (sàn
  append compiler). Kernel pre-allocated đã ghi ledger, hoán đợi compiler.

**Toolchain:** tkvc dist compiler tree vẫn hỏng — dùng
`/d/TokenVector._head_wt/3.code/dist/tkvc.exe`. Worktree giữ nguyên.

---

## 0c. PHIÊN 2026-09-23 (tiếp) — v1.8 dtype hẹp + sort_index + groupby iteration (XONG ✅)

Yêu cầu "làm tiếp nhóm chủ đích hoãn". Kết quả:
- **Dtype hẹp** (i8/i16/i32/u8/u16/u32/u64/f32/datetime64): tag trên storage chuẩn;
  module `tokenvector_dtype.tkv` (17 hàm) + vá core Series/Col (`_is_narrow_i64`,
  get_* tự widen, slice/take/clone giữ tag, make_series giữ tag).
- **CSV dtypes_spec mới**: "i8"…"u64", "f32", "datetime64" (`_csv_narrow_spec`).
- **sort_index**: `df_sort_index_cols` (axis=1); axis=0 identity.
- **Iterating groups**: `groupby_group_keys` + `groupby_group`.
- **Parquet/Feather**: CHỐT blocked-by-compiler (bitwise R5 + binary file IO).
  Khi R5 xong làm được trong thư viện.
- **Bug compiler mới ghi nhận**: typeflow merge biến cùng tên giữa các nhánh khác
  kiểu trong 1 hàm (v=get_f64 ở nhánh bool → v=get_i64 sau đó bị ép f64 → append
  list[i64] ra 0). Fix: tên biến riêng từng nhánh (vb/vi).
- p2_check **154/154**; 17/17 suite cũ xanh; DLL+smoke **28/28**; nupkg
  `TokenVector.Data.1.0.5-dev.nupkg`; FUNCTION_PARITY ~97% (§3h), RELEASE_NOTES 1.0.5-dev.
- Lưu ý quy trình: khi splice merged — reset `tokenvector_data_all.tkv` về HEAD
  TRƯỚC khi chạy `_patch_merged2.py` (tránh nhân đôi như 0b); kiểm tra
  `grep -c "class _QTok"` phải = 1 sau splice.

## 0b. PHIÊN 2026-09-23 (đêm) — pandas parity ~100% (XONG ✅ — kết quả cuối)

**KẾT QUẢ CUỐI (2026-09-23 sáng):** p2_check **110/110 PASS**; 17/17 suite cũ xanh;
merged splice idempotent xong (`tvsrc/_patch_merged2.py`, idempotent theo banner);
DLL rebuild qua tkvc→IL→`_mk_dll.py`→ilasm, smoke reflection **20/20 symbol v1.7**;
nupkg `TokenVector.Data.1.0.4-dev.nupkg` trong `packages/`;
FUNCTION_PARITY coverage ~80%→~95% (§3g v1.7); RELEASE_NOTES EN/VI 1.0.4-dev.
Lịch sử phiên (2 lỗi handoff + bug treo nested-append) giữ nguyên bên dưới để tham khảo.

Mục tiêu user: "làm tiếp đạt phủ 100%" — đóng các gap pandas còn lại trong
FUNCTION_PARITY.md §2.x (trừ nhóm loại trừ có chủ đích: Parquet/Excel thật,
dtype system, MultiIndex, index alignment).

**Ghi chú toolchain:** worktree `D:\TokenVector._head_wt` (tkvc TỐT, HEAD=18deb7d)
ĐƯỢC GIỮ LẠI — dist tkvc của compiler repo vẫn hỏng (tree dirty của phiên R4/R6
dở), đây là toolchain sạch duy nhất. Xóa worktree SAU KHI compiler repo được
sạch + rebuild tkvc chuẩn.

### File mới (chưa commit, chưa build pass hết):
- `tvsrc/tokenvector_pandas.tkv` — 34 defs: slice_replace/fullmatch/extractall/cat,
  factorize, first/last/nth/mad, groupby_first/last/nth/head/tail, stack/unstack,
  merge_ordered(ffill), dt_utc_offset/dt_tz_convert/dt_tz_to_utc (DST US
  post-2007), df_eval/df_query (shunting-yard: + - * / % // ** so sánh and/or/not;
  CHƯA hỗ trợ `in [...]` — tokenizer nhận nhưng chưa apply).
- `tvsrc/tokenvector_read_json.tkv` — 14 defs: json_read_string/file (chuan hóa
  dtype per-cột bool>i64>f64>str, sai loại→null), json_read_records(num_cols,
  time_cols→epoch ms), json_write_string/file (orient=records), jsonl_read_*,
  json_read_object. Parser tự viết (\uXXXX bỏ qua như io._json_parse_string).
- `tvsrc/p2_check.tkv` — suite acceptance 9 nhóm (~60 checks), entry `run`.
- Probe tạm `_b1/_b2/_b3.tkv` — GIỮ để debug nốt (xóa sau khi xong).

### ⚠️ tkvc.exe trong D:\TokenVector\3.code\dist đang HỎNG (đừng dùng!)
Build 22:14 từ tree dirty của phiên compiler R4/R6 dở (24 file modified:
il_codegen, tkv_compile, operators, list_type, control_flow…) — kể cả suite
cũ `comp_check.tkv` cũng fail `DT_I64 chua duoc khai bao`. **KHÔNG đụng tree
compiler đó.** Bản TỐT đã build từ HEAD worktree:
`/d/TokenVector._head_wt/3.code/dist/tkvc.exe` (HEAD=18deb7d, đã verify
`comp_check --entry run` SUCCESS). Xóa worktree khi xong phiên.

### Bắt được 2 lỗi cuối (sửa ĐẦU TIÊN sáng mai):
1. `_b2` (read_json): `literal '0' khong dung voi dtype 'str'` tại
   list_type.codegen_list_append — biến `vals` dùng chung 4 nhánh dtype với
   literal khác loại (0 / 0.0 / "") làm typeflow xung đột → **đổi tên riêng
   mỗi nhánh** (vals_b/vals_i/vals_f/vals_s).
2. `_b3` (pandas): `Ky vong dtype ..., gap 'DataFrame'` tại
   typed_dsl_parser.parse_param — nested def với param record
   (`_nth_fn(sub: "DataFrame")`) parser không nhận (mặc dù nonlocal đã đúng;
   v1.3 chỉ mở `func(DataFrame)->DataFrame` ở param hàm TOP-LEVEL) → **hoist
   nested func lên top-level + cấu hình qua biến global có kiểu**
   (`g_nth: "i32" = 0` style, compiler CÓ hỗ trợ global có kiểu), rồi
   `groupby_apply(df, keys, _pd_nth_fn, 1)`.

### Sự thật compiler mới bắt được phiên này (ghi nhớ cho code TKV):
- CẤM `while True:` → dùng `more = 1 / while more == 1`.
- CẤM `None` với record → sentinel object (kind 0) từ factory `_jt_new(0)`.
- Không có `ord()`/`chr()` builtin (gap R9) → hex digit = bảng if-chain.
- Hằng module-level phải literal (không gọi hàm lúc khai báo).
- CẤM chain 2 cấp `s.valid.bits` → gán biến trung gian.
- Mask không có is_valid/take → đọc `.bits[i]` qua biến trung gian.
- Nested func BẮT BUỘC dòng đầu `nonlocal <bien bat>, ...` (đã probe từ trước,
  phiên này xác nhận lại); param record của nested func CHƯA qua được parser.
- Linter: cấm `1e-9` (special number) → viết `0.000000001`.
- build entry suite là `--entry run` (không phải main).

### Checklist sáng mai (theo thứ tự):
1. Sửa 2 lỗi trên → build `_b2`, `_b3` xanh với tkvc của worktree.
2. Build + chạy `p2_check` (`--entry run`) → sửa tới khi P2OK (FAILS= 0).
3. Extend `tvsrc/_patch_merged.py` (idempotent) splice thêm
   tokenvector_pandas + tokenvector_read_json vào 2 file merged.
4. Regression toàn bộ 16 suite cũ + pd_check + p2_check (đều phải xanh).
5. Rebuild DLL/nupkg 1.0.4-dev (quy trình+MSYS_NO_PATHCONV=1 như mục 0,
   smoke.cs reflection — nhớ chọn tkvc TỐT: rebuild compiler khi tree
   compiler đã sạch, hoặc dùng tkvc worktree).
6. Docs: FUNCTION_PARITY (§1 các row + section 3g v1.7 + coverage mới —
   sau đợt này ước ~90–95%, còn lại Parquet/Excel/dtype/MultiIndex/index),
   RELEASE_NOTES EN/VI, handoff mục này thành "XONG".
7. Commit Data repo (3 commit: feat modules+suite / build merged+DLL / docs).
   Xóa `_b*`, worktree `D:\TokenVector._head_wt`.

---

## 0. PHIÊN 2026-09-22 — io v1.6.3: parse_dates + encoding (XONG, all green)

### Đã làm (chi tiết probe trong tvsrc/_RELNOTES_io_v163.md):
- **parse_dates**: `csv_read_ex`/`csv_read_chunks`/`csv_read_chunks_file` thêm
  tham số cuối `parse_dates: "list[str]"` — cột str → i64 epoch ms qua
  `series_dt_parse`, GIỮ tên cột (biến trung gian + `.rename`; compiler không
  cho method-call trên kết quả hàm). Cột lạ bỏ qua an toàn. ⚠️ ĐỔI CHỮ KÝ —
  caller cũ phải thêm `[]`.
- **csv_read_chunks_file_enc(..., encoding, parse_dates)**: latin-1 đọc cả
  file qua `_read_all_enc` (byte-per-char) rồi delegate `csv_read_chunks`;
  utf-8/"" stream thật (delegate csv_read_chunks_file).
- **BOM EF BB BF**: runtime TỰ STRIP khi mở file (probe cả 3 open mode) —
  KHÔNG cần strip tay. Chuỗi TKV byte-per-char: literal U+FEFF raw = 3 ký tự,
  `write_file("\ufeff...")` double-encode → không tạo fixture BOM thật được.
- **f.readline() KHÔNG trả \n cuối** (khác Python!) → `_read_all_enc` ghép
  `\n` giữa dòng (bug thật bắt được qua suite).
- **Compiler không cho truyền file-handle vào hàm** (param không kiểu —
  `_chunks_read_handle` bị từ chối) → mỗi read tự inline loop hoặc đọc-all.
- **Merged splice idempotent**: `tvsrc/_patch_merged.py` (marker-based, chống
  nhân đôi — dùng tool này cho mọi lần refresh merged sau này).
- Suite mới `tvsrc/pd_check.tkv` (15 checks, FAILS= 0); cập nhật csv2_check,
  io_check, tokenvector_arrow, bench, v15_check (+tokenvector_datetime import);
  FUNCTION_PARITY + RELEASE_NOTES (EN/VI) mục 1.0.3-dev; probe tạm đã dọn.
- Regression 16 suite ĐỀU XANH (base..csv2).

---

## 1. Compiler tkvc (D:\TokenVector) — đã rebuild, Testkit 14/14 xanh

### Đã vá (chi tiết đầy đủ trong COMPILER_BUGS.md):
- **BUG-1** `int_var * float_literal` crash/rác → fix: promote `operand_dtype='f64'`
  trong `operators.compile_binop` khi int-family + có float literal (helper
  `_float_literal_in` local trong operators.tkv). Test: `bug_int_float_mul_test.tkv` 4/4.
- **BUG-2** `float(counter)` trả 0.0 im lặng → fix: `float_builtin.push_float_builtin`
  thêm nhánh `'int'` gọi `TkvInt::ToF64`. Test: `bug_float_counter_test.tkv` 2/2.
  (Lưu ý: test ban đầu ghi sai kỳ vọng 2.5 — đúng là tổng 4.0, đã sửa test.)
- **BUG-3** gán lại local lệch kiểu (retype) → guard HƯỚNG NGUY HIỂM:
  - scalar int-family ← float expr: SyntaxError rõ (`_stmt_assign_scalar`).
  - list int-family ← float elems: SyntaxError rõ (guard `check_list_retype`
    expose qua ctx, plugin `list_type.codegen_assign_list_literal` gọi lại —
    đường `assign_list_literal` KHÔNG đi `_stmt_assign_scalar`).
  - Hướng widen an toàn (f64-list ← int elems) GIỮ NGUYÊN (chạy đúng 4.0).
  - Bẫy đã biết: dtype literal nguyên suy qua `_infer_literal_dtype` là `'int'`
    (struct TkvInt), KHÔNG phải `'i32'` — guard phải tính cả `'int'`.
  - Test: `bug_local_retype_test.ps1` 3/3 (list, scalar, widen).
- **KERNEL_LAB 6.2 Gap A** worker đa luồng trả `list[T]`:
  - `il_codegen.tkv` first-pass: `thread_ret_types` giữ nguyên TypeAnn (không
    `.dtype` trần); nhánh `thread_join` dùng lại TypeAnn khi không phải str.
  - `threading_feature.tkv`: spawn dùng `il_type_str` cho shape='list' →
    `Task<List<T>>` thật; join castclass/get_Result theo đúng IL type.
- **KERNEL_LAB 6.2 Gap B** worker là hàm lồng (closure):
  - `_build_closure_delegate` nhận thêm ctx, list-aware cho ret_il (Func`1<List<T>>).
  - Expose `'build_closure_delegate'` qua CẢ 2 ctx (walk ctx gần `'int_dtypes'`
    và codegen ctx gần `'compile_funcref_arg'` — mỗi cái xuất hiện 2 lần, cả 4
    chỗ đều phải có, đã thêm đủ).
  - `threading_feature.tkv`: nhánh nested closure TRƯỚC khi tra func_table;
    emit `[get_Factory] + [delegate instance] + StartNew` với FORM IL đã chứng
    minh chạy được: `Task`1<!!0>` (return) + `StartNew<{il_ret}>` + `Func`1<!!0>`
    ([mscorlib]) — các form khác gây MissingMethodException/BadImageFormat.
  - Hạn chế HIỂN THỊ: closure qua Task hiện trả 0 (probe in `closure_sum= 0`)
    dù direct call đúng 4.0 — cần điều tra tiếp (BUG-5 tiềm ẩn, chưa ghi).

### Đang phát hiện, CHƯA vá (ghi trong COMPILER_BUGS.md):
- **BUG-4** closure bắt list local rồi INDEX nó trong thân
  (`inp[i]`) → `SyntaxError: bien 'inp' chua duoc khai bao/tham so`.
  Direct call OK, lỗi ở first-pass suy shape cho node 'index' trên biến bị bắt.
  Hướng vá: khi xử lý `nonlocal` trong hàm lồng, copy TypeAnn của biến ngoài
  vào infer_scope cục bộ (xem `_probe_cap` pattern — file probe đã xóa, repro
  trong COMPILER_BUGS.md).

### Bài học build (QUAN TRỌNG):
- Sửa `compiler/*.tkv` (CORE) PHẢI rebuild tkvc.exe: `powershell -File
  D:\TokenVector\3.code\build_tkvc.ps1` (~15 giây) — CORE bị nhúng trong exe.
- Riêng LIBRARY `dist/il_features/*.tkv` nạp động lúc chạy → chỉ cần copy.
- str_replace hay trượt vì CRLF trong .tkv — nếu trượt 2 lần liên tiếp thì
  dùng python script patch theo số dòng (newline='' và writelines CRLF).

---

## 2. TokenVector.Data (D:\TokenVector.Data) — TKV 8T ĐÃ THẮNG numpy

### Benchmark chính thức (bench.tkv 500k, tkvc MỚI, warm best):
| Bài | TKV | pandas 2.3.3 | Kết |
|---|---:|---:|---|
| B1 vec arith | 10.5 ms | 3.4 | pandas ~3× |
| B2 filter | 5.0 | 4.9 | HÒA |
| B3 groupby warm | ~36 | 34.6 | HÒA |
| B4 join 10M | 1600 | 588 | pandas ~2.7× |
| B7 sort | 102 | 8.6 | pandas ~12× |

### Đa luồng output thật @5M (MỚI — bench_thread_out / bench_thread_real8):
| Cấu hình | ms |
|---|---:|
| TKV serial 1T | 41–56 |
| TKV 4T | 28.2 |
| TKV 8T | **13.2** |
| numpy 1T preload | 30.4 |
| numpy inline-pipeline | 113.7 |

→ **TKV 8T thắng numpy 1T ~2.3× và thắng numpy inline ~8.6×** — lần đầu
ngôn ngữ TKV có đa luồng sinh output thật (không chỉ reduction scalar).

### Files đã thêm/sửa ở TokenVector.Data:
- `BENCHMARKS.md` — mục 1a ghi kết quả trên (CHƯA mirror sang BENCHMARKS_VI.md).
- `tvsrc/bench_thread_out.tkv` (4T) + `tvsrc/bench_thread_real8.tkv` (8T) —
  benchmark đa luồng output; build bằng `tkvc.exe build --entry run <file.tkv>`.
- File probe tạm đã dọn sạch.

### Phân tích 1T vs pandas (trả lời câu hỏi cuối phiên):
- IL loop thuần của TKV KHÔNG thua C kernel: K2 fused=13.3ms vs numpy 30.4;
  K5 filter-count=9.15 vs 9.2 (hòa). B2/B3 hòa là bằng chứng.
- Khoảng cách B1 còn lại do: (1) không SIMD trong codegen, (2) append+mask
  ~10ns/elt khi sinh output, (3) alloc cột+mask mới mỗi op.
- 3 hướng gỡ ở 1T: SIMD codegen (bắt buộc sửa compiler), in-place/prealloc,
  hoist branch — chi tiết trong KERNEL_LAB.md.

---

## 3. Việc còn treo (nếu quay lại):
1. Vá BUG-4 (closure capture index) → mở đường worker đọc cột thật qua closure.
2. Điều tra BUG-5 (closure qua Task trả 0 dù direct đúng) — chưa ghi docs.
3. Đưa đa luồng vào engine thật: `groupby_agg`/`join_frames`/`vec_*` tự chia
   chunk theo số worker khi N lớn (hiện script phải tự viết worker).
4. Benchmark B2/B3 đa luồng 8T so pandas cùng phiên, cập nhật bảng.
5. Mirror mục 1a sang BENCHMARKS_VI.md + RELEASE_NOTES (chuẩn bị v1.0.3).
6. Commit 2 repo (CHỈ khi user yêu cầu) — thay đổi nhiều phiên trộn lẫn,
   cần chia commit có chủ đích.
