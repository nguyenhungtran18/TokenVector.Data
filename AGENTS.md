# AGENTS.md — TokenVector.Data

Ghi chú cho agent/session làm việc trên repo này (mục tiêu: đạt parity pandas).

## Kiến trúc & quy trình build (bắt buộc nhớ)

- **2 repo dính nhau**: mã nguồn `.tkv` nằm ở `tvsrc/*.tkv` (repo này), nhưng
  **compiler tkvc** ở `D:\TokenVector`. Sửa compiler phải build lại tkvc.exe rồi
  mới compile được file `.tkv` ở đây; sửa `.tkv` ở đây phải **rebuild 2 file
  merged** (`tokenvector_data_all.tkv` + lib dùng cho DLL) trước khi build DLL.
- Merged file bị script cũ **nhân đôi nội dung** nếu refresh nhiều lần (data/
  relational lặp 3 lần) — khi nghi ngờ, dựng lại merged từ đầu từ các module
  gốc, không refresh đè lên merged cũ.
- **DLL**: build bằng `ilasm` từ IL; `tkvc` không có cờ `--il` — IL được ghi cạnh
  exe bởi `assemble_il_to_exe` (`tokenvector_compile`). Sau build, verify DLL
  bằng C# reflection (console app gọi qua `Assembly.LoadFrom`).
- Regression chuẩn: chạy đủ **15 suite** `*_check.tkv` (base, vec, comp, apply,
  strings, relational, sort, datetime, io...). Chuỗi `FAILS= 0` nghĩa là 0 lỗi
  — **đừng đọc nhầm là FAIL** (grep `FAIL` false positive).
- TokenVector.Data hiện chạy độc lập với pandas thực tế: khi cần ground truth
  (ewm, corr, semantics `adjust=True/False`...) **gọi pandas trực tiếp bằng
  python3 trên máy** để đối chiếu từng con số, đừng đoán.

## Bug compiler đã vá & bug phải nhớ (đã commit: 0600820, 12664c3, + phiên v1.5)

- BUG-1 int_var*float_literal → promote f64; BUG-2 float(counter) trả 0; BUG-3
  retype local lệch kiểu. Chi tiết đầy đủ trong `D:\TokenVector\COMPILER_BUGS.md`
  và `SESSION_HANDOFF.md` (repo này).
- **Suy diễn dtype là bẫy số 1**: biến khởi tạo `0.0` vẫn bị suy `'int'` nếu qua
  phép chia int/int → khai báo tường minh `x: "f64" = ...` trong mọi code tính
  toán (đã gây 2 bug thật: interpolate, ewm).
- `Series.take` cũ: ctor trực tiếp thiếu 3/4 buffer → nhóm I64/STR/BOOL mất giá
  trị. Mọi constructor Series phải đi qua `make_series_*_with_mask` (đủ 4
  buffer + mask).

## Quy ước API (giữ nhất quán khi thêm hàm mới)

- Không có method namespace — mọi thao tác pandas `.str.*`, `.ewm()`... là
  **hàm tự do** `series_*` / `win_*` / `df_*` trong module phù hợp (strings,
  compute, relational, apply).
- Callbacks: top-level func HOẶC lambda truyền thẳng, signature annotation
  `func(f64)->f64`, `func(DataFrame)->DataFrame`, `func(list[f64])->f64` —
  compiler tkvc đã hỗ trợ cả 3 (nhớ rebuild tkvc.exe sau khi vá).
- Semantics khớp pandas: ewm adjust=True decay **lịch sử** (`num=(1-a)*num+x`),
  adjust=False đệ quy; `pct_change` là `(cur-prev)/prev`; `groupby_apply` trả
  DataFrame có `keep_key_cols` chèn lại key.

## Bẫy kỹ thuật .tkv / compiler (đã mất thời gian, đừng lặp lại)

- File `.tkv` đa số **CRLF** — `str_replace` hay trượt. Dùng python
  `open(..., 'rb')`, split `\n`, replace, write `\r\n` (hoặc `\n` tường minh).
- Heredoc bash dài (chèn >100 dòng) bị cắt ngầm → **write_file ra file block**
  rồi append bằng python, đừng heredoc.
- 1 local trong tkv chỉ giữ **1 kiểu duy nhất** — tái sử dụng biến với 2 kiểu
  (vd `al` là list[str] rồi f64) là lỗi ngầm; đổi tên biến.
- Chain-method trên kết quả (record): `_method_call_expr_receiver_ta` phải trả
  `TypeAnn(dtype,'record')`; `_infer_dtype` nhánh method_call_expr trả `'str'`
  mặc định → sai dtype khi so sánh (`Series.is_null` → i32 bị coi là str).
- Debug trong plugin compiler (`dist/il_features/*.tkv`): `sys.stderr.write` với
  `%r\n'` dễ tạo **SyntaxError unterminated string literal** khi patch tay —
  sau khi dọn debug, nhớ xóa cả dòng `' % (...)` mồ côi; sửa phải làm ở
  **cả 2 nơi**: `D:\TokenVector\3.code\compiler\il_features\` và
  `D:\TokenVector\3.code\dist\il_features\` (dist nạp lúc runtime).

## Build tkvc.exe (D:\TokenVector) — bẫy lớn nhất

- Script chuẩn: `powershell -File D:\TokenVector\3.code\build_tkvc.ps1`
  (chạy từ bất kỳ đâu, KHÔNG chạy spec trực tiếp — spec cũ sai pathex).
- Script tự staging: copy `.tkv` → `.py`, tách CORE (import trực tiếp trong
  il_codegen) vs LIBRARY (nạp runtime qua plugin_loader từ `dist/il_features/`).
- **Core/Library split**: plugin_loader nạp `.tkv` trong `dist/il_features/`;
  module bị import `from il_features.X import ...` trong il_codegen là CORE,
  đóng gói vào exe (list_type, record_feature, string_feature, duck_typing,
  stdlib_math...). Nếu một module LIBRARY import module CORE chưa có trong exe
  → `ModuleNotFoundError: No module named 'il_features.list_type'` lúc runtime
  với traceback vô dụng. Kiểm tra xref PyInstaller:
  `build/pyinstaller/tkvc/xref-tkvc.html`.
- Sau build phải **copy thủ công**: `build/pyinstaller_src/il_features_library/
  *.tkv` → `dist/il_features/` nếu script copy bị lệch (dist là nơi nạp runtime,
  không phải staging).
- Smoke test bắt buộc sau build tkvc: compile + chạy 1 file `.tkv` nhỏ trước
  khi tin build thành công (build "complete" không chắc nạp được plugin).

## Trạng thái & dở dang

- Parity ~75–80% pandas (FUNCTION_PARITY.md). Gap còn: Parquet thật,
  categorical/dtype, MultiIndex, index alignment, ewm return Series-quo.
- FUNCTION_PARITY.md là "source of truth" danh sách hàm — cập nhật mỗi lần thêm.
- Untracked `_p*.tkv`, `_pb*.tkv`, `_probe*.tkv`, `dbg_*.tkv` ở tvsrc là probe
  tạm — xóa trước khi commit.
