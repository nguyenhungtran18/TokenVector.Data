import io

def read(p):
    return io.open(p, encoding='utf-8', newline='').readlines()

new_io = read('tvsrc/tokenvector_io.tkv')
while new_io and new_io[-1].strip() == '':
    new_io = new_io[:-1]

CANON_TAIL = [
    '# ==============================================================================\n',
    '# TOKENVECTOR.DATA - NUMERICS INTEROP MODULE (port tu NumericsInterop.cs)\n',
    '# Bridge giua Series/DataFrame va NDArray/Tensor cua TokenVector.Numerics.\n',
    '# Trong tkv, NDArray/Tensor duoc mo hinh hoa bang record Mat:\n',
    '#   Mat = { rank, rows, cols, f64s: list[f64], requires_grad }\n',
    '#   - ToNDArray<T>/ToTensor<T> (Series)     -> series_to_mat / series_to_mat_grad\n',
    '#   - ToNDArray<T>/ToTensor<T> (DataFrame)  -> df_to_mat_2d / df_to_mat_2d_grad\n',
    '#   - FromNDArray/FromTensor                -> mat_to_df / mat_to_df_from_tensor\n',
    '# ==============================================================================\n',
    '\n',
    '# NDArray 1D hoac 2D f64 (mo hinh hoa NDArray<T>/Tensor<T>)\n',
    '\n',
    'def run() -> "str":\n',
    '    return "LIBOK"\n',
]

# 1) tokenvector_data_all.tkv
p_all = 'tvsrc/tokenvector_data_all.tkv'
all_lines = read(p_all)
i = None
for idx, l in enumerate(all_lines):
    if l.startswith('# tokenvector_io.tkv'):
        i = idx
        break
assert i is not None, 'io header not found in all'
# lui qua dong trong (splice banner nam giua) de thay tu dau khoi io
while i > 0 and all_lines[i - 1].strip() == '':
    i = i - 1
# neu co banner '====' ngay truoc khoi trong -> giu banner, thay tu header io
if i > 0 and all_lines[i - 1].startswith('# ===================='):
    i = i + 1
    while i < len(all_lines) and all_lines[i].strip() == '':
        i = i + 1
k = None
for idx, l in enumerate(all_lines):
    if l.startswith('def run()') and idx > i:
        k = idx
        break
assert k is not None, 'def run not found in all'
all_out = all_lines[:i] + new_io + CANON_TAIL
io.open(p_all, 'w', encoding='utf-8', newline='').writelines(all_out)

# 2) _libbuild.tkv
p_lib = 'tvsrc/_libbuild.tkv'
lib_lines = read(p_lib)
m = None
for idx, l in enumerate(lib_lines):
    if l.startswith('# ==================== tokenvector_io ===================='):
        m = idx
        break
assert m is not None, 'io marker not found in lib'
j = None
for idx, l in enumerate(lib_lines):
    if l.startswith('# ==================== tokenvector_relational ====================') and idx > m:
        j = idx
        break
assert j is not None, 'relational marker not found in lib'
marker = '# ==================== tokenvector_io ====================\n'
lib_out = lib_lines[:m] + [marker] + new_io + ['\n'] + lib_lines[j:]
io.open(p_lib, 'w', encoding='utf-8', newline='').writelines(lib_out)

print('merged splice ok (idempotent)')
