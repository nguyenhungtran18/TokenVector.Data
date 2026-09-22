# RELEASE NOTES (tvsrc/io) — v1.6.3 (2026-09-22)

## csv parse_dates (pandas read_csv(parse_dates=[...]))
- `csv_read_ex(..., parse_dates: "list[str]")` — cuối cùng: sau khi dựng cột,
  mỗi tên cột trong `parse_dates` được chuyển str → i64 epoch ms qua
  `series_dt_parse` (module datetime), GIỮ nguyên tên cột (qua biến trung gian
  + `.rename`; compiler không cho method-call trực tiếp trên kết quả hàm).
  Cột không tồn tại → bỏ qua an toàn (giống pandas không fail). Cột đã i64 →
  không đổi. Cột rỗng → str (không parse) — cùng hạn chế đã ghi ở v1.6.
- Cột str ISO `2024-03-15T10:30:00` → epoch ms đúng (`dt_check` chuẩn).
- `csv_read_chunks` và `csv_read_chunks_file` nhận `parse_dates` và truyền
  nhất quán vào mọi `csv_read_ex` của từng chunk (chunk 2+ parse độc lập,
  dtype i64 giống chunk đầu — chứng minh ở `pd_check` t2/t3).
- ⚠️ Đổi chữ ký: mọi caller cũ 8/9 tham số phải thêm `parse_dates` (dùng `[]`).
  Đã cập nhật: `csv2_check`, và suites import io (thêm `tokenvector_datetime`).

## csv encoding (pandas read_csv(encoding=...))
- API mới `csv_read_chunks_file_enc(file, sep, header, null, names, dtypes,
  na_values, skiprows, chunksize, encoding, parse_dates)`:
  - `"latin-1"`/`"latin1"`: đọc cả file qua `_read_all_enc` (byte-per-char,
    không bao giờ lỗi với file không utf-8) rồi delegate `csv_read_chunks`
    — khối file nhỏ, chấp nhận được (pandas latin-1 cũng hiếm file lớn).
  - `"utf-8"`/`"utf8"`/`""`/khác: delegate `csv_read_chunks_file` = STREAM
    THẬT (chỉ giữ chunksize+1 dòng trong RAM).
- BOM EF BB BF: runtime TKV TỰ STRIP khi mở file (probe chứng minh với cả 3
  chế độ open: default/utf-8/latin-1) → không cần/có check BOM tay nào cả.

## Sự thật kỹ thuật đã probe (không đoán)
1. String TKV là byte-per-char: literal chứa U+FEFF raw trong source .tkv đọc
   thành 3 ký tự; `"\ufeff"` trong source (EF BB BF) cũng 3 ký tự → KHÔNG thể
   so sánh 1 ký tự BOM; và `write_file("\ufeff...")` double-encode (giống bug
   `_t9bom.csv` cũ) nên không tạo được fixture BOM thật từ string API.
2. `f.readline()` của runtime: KHÔNG trả `\n` cuối (khác Python) →
   `_read_all_enc` phải tự ghép `\n` giữa các dòng (bug thật đã bắt khi t5
   FAIL: header dính `id,name1,an2,binh`).
3. Compiler: KHÔNG truyền file-handle vào hàm (param không có kiểu —
   `_chunks_read_handle` bị từ chối build) → mỗi `read` phải tự inline loop
   hoặc đọc-all; đây là lý do kiến trúc hiện tại.
4. Substring slice ngoài độ dài chuỗi → crash InvalidProgram (cắt `[0:10]`
   trên dòng 7 ký tự) — cẩn thận khi slice print debug.
5. `.dtype`/property chỉ đọc trên biến đơn; method-call trên kết quả hàm
   (`f(x).rename(...)`) không được hỗ trợ — luôn gán biến trung gian.

## Kiểm chứng
- Suite mới `pd_check.tkv` (15 checks): parse_dates ex/chunks/file/enc,
  tên cột giữ nguyên, epoch đúng, latin-1/default enc, cột lạ bỏ qua →
  FAILS= 0.
- Regression toàn bộ xanh: base, vec, comp, apply, strings, rel, core, num,
  arr, feat, stat, stats, v15, io, dt, csv2 (không FAIL nào ngoài `FAILS= 0`).
- Merged: `tokenvector_data_all.tkv` + `_libbuild.tkv` đã splice lại section io
  bằng script idempotent (marker-based, chống nhân đôi — AGENTS.md).
