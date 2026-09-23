# Báo cáo regression compiler tkvc — dist 2026-09-23 17:59

> Người nhận: người sửa compiler tkvc (repo `D:\TokenVector`,
> nguồn build: `3.code/build/pyinstaller_src/`).
> Bản dist mới (17:59, 10.233.472 bytes, sau commit `2fd741f`) **phá khả năng
> build toàn bộ thư viện TokenVector.Data**: 10/12 module tvsrc fail. Bản cũ
> `D:\TokenVector._head_wt\3.code\dist\tkvc.exe` (01:00, 10.193.255 bytes)
> build xanh toàn bộ (18/18 suite PASS sáng cùng ngày) — dùng làm chuẩn đối chứng.
>
> Quy tắc nghiệm thu chung: sau mỗi fix, chạy lại **toàn bộ** suite của
> TokenVector.Data (lệnh ở cuối file) phải xanh như bản cũ, cộng acceptance
> test của từng mục bên dưới PASS.

---

## Mức độ nghiêm trọng

| # | Vấn đề | Mức | Phạm vi phá hỏng |
|---|---|---|---|
| R1 | Gán từ module-constant → KeyError crash il_codegen | **Blocker** | Mọi phép gán `x = DT_I64` (12 module đều dùng DT_* của `tokenvector_data`) |
| R2 | Module-constant qua `__tkv_import__` bị coi "chưa khai báo" | **Blocker** | Các hàm dùng trực tiếp `DT_I64`/`DT_STR`… của module khác (compute/io/stats/arrow/datetime) |
| R3 | "dùng trước khi gán" báo oan: biến gán trong vòng lặp trước vòng dùng | **Blocker** | `groupby_filter` (relational) + mọi pattern tương tự |
| R4 | Case loop phức tạp hơn trong `tokenvector_p100` → crash `.get_string` (stack thấy `try_parse_for`/`_parse_block` đệ quy) | High | build p100 với bản cũ cũng dính (tôi đang gắn kernel mới) — kiểm tra xem bản mới còn dính không sau R1–R3 |

Chi tiết từng mục kèm repro và tiêu chí nghiệm thu như sau.

---

## R1 — Gán từ module-constant crash il_codegen (KeyError)

**Repro tối giản** (file `_dt.tkv`, cùng thư mục chứa file có constant):

```tkv
# -*- coding: utf-8 -*-
DT_I64 = 4

def main() -> "i32":
    x = DT_I64
    print("const=" + str(x))
    return 0
```

Bản cũ: build OK, in `const=4`. Bản mới: build xong **crash lúc codegen** với:

```
File "il_codegen.py", line 3894, in <lambda>
File "il_codegen.py", line 3972, in declare_scalar
File "il_codegen.py", line 600, in _infer_dtype
File "il_codegen.py", line 3695, in __getitem__
KeyError: 'DT_I64'
```

**Vị trí nghi ngờ:** `_DtypeOnlyScope.__getitem__` (`il_codegen.py` ~dòng 3695)
được dùng bởi `_infer_dtype` (~dòng 600) khi first-pass suy dtype cho biến mới.
Khi RHS là `ast.Name` trỏ tới **module-constant** (không phải biến local), scope
micro không có tên đó → KeyError thay vì trả về None (hành vi khuyến nghị: với
constant, `_infer_dtype` nên trả `None` — ldc hoạt động với mọi dtype mà caller
yêu cầu, giống nhánh "không xác định được" trong docstring của hàm).

**Nghiệm thu:** repro trên build + chạy đúng; `tokenvector_data.tkv` build
OK; các module consumer (compute/io/stats/arrow/datetime/relational) không còn
KeyError/KeyError-tự-do ở tầng codegen cho pattern `x = CONST`.

## R2 — Module-constant qua import bị coi "chưa khai báo"

**Repro:** 2 file cùng thư mục.

`_tconst.tkv`:

```tkv
# -*- coding: utf-8 -*-
DT_I64 = 4
```

`_tuse.tkv`:

```tkv
# -*- coding: utf-8 -*-
__tkv_import__ = ["_tconst"]

def main() -> "i32":
    x = DT_I64
    print("const=" + str(x))
    return 0
```

Bản cũ: build OK, in `const=4`. Bản mới:

```
[tkv] Loi: ham 'main': bien 'DT_I64' chua duoc khai bao (dong 5) - khong phai
tham so, bien local, hang so module, ham top-level hay builtin.
```

**Vị trí nghi ngờ:** `_check_use_before_def` trong `tkv_compile.py` (~dòng
3130–3215) chỉ "thừa nhận" constant **cùng module** (module-level Store của
chính file) chứ không nhận diện tên được **import** qua `__tkv_import__`. Tập
`stored` đã có nhánh `ast.Import`/`ast.ImportFrom` nhưng cơ chế import của TKV
là splice nguồn (không phải Python import), nên constant của module bị import
không xuất hiện trong `stored` của file dùng. Cần thêm: tập tên constant
module-level của **mọi module trong `__tkv_import__`** (theo thứ tự splice)
vào tập "được phép Load" của R11-check (và đồng bộ với `_infer_dtype`/scope
codegen để constant import có dtype số đúng).

**Nghiệm thu:** repro trên build + chạy đúng với **cả 2 bản** (tức fix không
được phá hành vi cũ); `p100_check`/`pd_check`/`p2_check` build OK.

## R3 — "dùng trước khi gán" báo oan với biến gán trong vòng lặp

**Repro rút gọn từ code thật** (`tokenvector_relational.tkv`, hàm
`groupby_filter` ~dòng 1374):

```tkv
# -*- coding: utf-8 -*-
def make_df2() -> "i32":
    return 0

def main() -> "i32":
    xs = [1, 2, 3]
    keep = []
    nc = 0
    for i in range(3):
        nc = 2
        for j in range(nc):
            keep.append(xs[j])
    print("n=" + str(len(keep)))
    return 0
```

Lỗi thật của thư viện (bản mới):

```
[tkv] Loi: ham 'groupby_filter': bien 'nc' duoc dung truoc khi gan (dong 1378)
- compiler cu tu gan gia tri mac dinh 0/null am tham.
```

Trong code thật, `nc` được gán **bên trong vòng `for gx`** trước vòng `for c`
dùng nó — definite-assignment hiện tại không nhận "gán trong body vòng lặp
cha chạy trước vòng lặp con" (và cũng không nhận khởi tạo từ lời gọi hàm /
index-assign vào list ở nhánh sớm). Đây là false-positive của R11-check.

**Vị trí nghi ngờ:** `_check_use_before_def` (file trên), các nhánh
`ast.While`/`ast.For` (~dòng 3315+): vòng lặp hiện **không merge** assigned
sau body (đúng lý thuyết "có thể không chạy"), nhưng biến được gán trong body
vòng lặp cha **trước** vòng lặp con lồng nhau vẫn phải tính là assigned khi
kiểm tra vòng con — cần duyệt tuần tự statement-level (state dần dần) thay vì
đặt lại state về `saved` trước mỗi block con.

**Nghiệm thu:** repro trên build + chạy đúng (`n=6`); `tokenvector_relational.tkv`
build OK; toàn bộ suite Data xanh.

## R4 — Crash parse khi thân hàm có vòng lặp phức tạp (p100 kernel mới)

Bản cũ crash với stack `try_parse_for` (control_flow.py:562) → `_parse_block`
(il_codegen.py:3408/3412) khi gặp dòng:

```tkv
out_key_cols[k].append(kcols_in[k].get_string(r))
```

trong thân `groupby_rolling` của `tokenvector_p100.tkv` (bản đang dở của tôi,
kèm 2 vòng lặp + list-of-list literal `[]` per key). Cần xác minh trên bản mới
sau khi R1–R3 hết: build `tokenvector_p100.tkv` phải OK (hiện bản mới dừng ở
R3 nên chưa chạm tới R4).

---

## Cách tái lập toàn bộ (chạy trên Windows, MSYS/Git Bash)

```bash
cd /d/TokenVector.Data/tvsrc
TKV=/d/TokenVector/3.code/dist/tkvc.exe     # bản mới
# TKV=/d/TokenVector._head_wt/3.code/dist/tkvc.exe   # bản cũ (chuẩn đối chứng)

# 1) build từng module (không có entry → dùng --entry dt_name)
for m in tokenvector_data tokenvector_compute tokenvector_datetime \
         tokenvector_io tokenvector_strings tokenvector_stats \
         tokenvector_arrow tokenvector_relational tokenvector_pandas \
         tokenvector_dtype tokenvector_p100 tokenvector_read_json; do
  echo "== $m"; $TKV build $m.tkv --entry dt_name --out _m.exe 2>&1 | tail -1
done

# 2) suite đầy đủ (bản cũ xanh 18/18 vào sáng 2026-09-23)
for f in apply_check arr_check base_check comp_check core_check csv2_check \
         dt_check feat_check io_check num_check rel_check stats_check \
         strings_check v15_check vec_check; do
  $TKV build $f.tkv --entry run --out _r.exe >/dev/null 2>&1 && ./_r.exe >/dev/null 2>&1 \
    && echo "$f OK" || echo "$f FAIL"
done
$TKV build csv2_check.tkv --out _r.exe >/dev/null 2>&1 && ./_r.exe >/dev/null 2>&1 && echo "csv2 OK"
$TKV build pd_check.tkv   --out _r.exe >/dev/null 2>&1 && ./_r.exe >/dev/null 2>&1 && echo "pd OK"
$TKV build p2_check.tkv   --out _r.exe >/dev/null 2>&1 && ./_r.exe >/dev/null 2>&1 && echo "p2 OK"
$TKV build p100_check.tkv --out _r.exe >/dev/null 2>&1 && ./_r.exe >/dev/null 2>&1 && echo "p100 OK"
```

**Trạng thái theo từng bản (đo 2026-09-23):**

| Bản | Modules | Suite |
|---|---|---|
| cũ (01:00) | 12/12 OK | 18/18 PASS |
| mới (17:59) | 2/12 (data, strings) | 0 (dừng sớm ở R1/R2/R3) |

## Đề xuất thứ tự sửa

1. **R1** (KeyError `_infer_dtype` với constant) — nhỏ, mở khoá build data.
2. **R2** (constant qua import) — đưa `stored` của R11-check biết tên constant
   các module được splice; đồng bộ scope codegen.
3. **R3** (false-positive use-before-def với vòng lặp) — rà lại logic merge
   assigned của for/while lồng nhau; lưu ý giữ nguyên mục tiêu R11 (bắt
   `if x == 1: ...; x = 1` không khởi tạo).
4. **R4** — chỉ xác minh lại sau 1–3; nếu còn, gửi thêm case rút gọn.

Sau khi 1–3 xong: toàn bộ 12 module + 18/18 suite phải xanh như bản cũ
(bằng lệnh tái lập trên), khi đó tôi tiếp tục đóng phần còn lại của thư viện
với toolchain mới.
