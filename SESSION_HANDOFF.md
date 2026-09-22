# SESSION HANDOFF — 2026-09-22 (đọc file này trước khi làm tiếp)

## Trạng thái: ĐÃ COMMIT (user yêu cầu) — Data repo: 7476d63 feat io v1.6.3,
b205bb9 build DLL/nupkg 1.0.3, 35f7aab docs, 40f1be9 smoke harness. Compiler
repo: c5bd63e feat encoding on with-open + sync tkvc.exe, 18deb7d dọn 102 .pyc
tracked. Chưa commit ở compiler (không phải của phiên io): media player
untracked + images/preview.png + submodule tokenvector-grammar dirty. Chưa
push (đợi user).

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
