# -*- coding: utf-8 -*-
"""Splice idempotent: them tokenvector_pandas + tokenvector_read_json vao 2 file
merged (tokenvector_data_all.tkv va _libbuild.tkv).

- tokenvector_data_all.tkv: cac module don le duoc noi bang header
  '# -*- coding: utf-8 -*-' (line dac trung cua moi module) -> chen truoc header
  io (marker 'tokenvector_io.tkv'), moi module giu nguyen file goc.
- _libbuild.tkv: cac module noi bang marker '# ==================== NAME ===='
  -> them 2 marker + noi dung truoc 'def run' cuoi file.

Idempotent: neu marker cua module da ton tai thi khong lam gi (bo qua).
"""
import io
import os

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))


def read(p):
    return io.open(p, encoding='utf-8', newline='').readlines()


def write(p, lines):
    io.open(p, 'w', encoding='utf-8', newline='').writelines(lines)


def strip_blanks(lines):
    out = list(lines)
    while out and out[-1].strip() == '':
        out = out[:-1]
    return out


def block_of(path, banner):
    """Doc file module + banner, dam bao ket thuc bang dung 1 dong trong."""
    body = strip_blanks(read(path))
    return [banner] + body + ['\n', '\n']


def find_marker(lines, prefix):
    for idx, l in enumerate(lines):
        if l.startswith(prefix):
            return idx
    return None


def contains(lines, needle):
    for l in lines:
        if needle in l:
            return True
    return False


MODS = [
    ('tvsrc/tokenvector_pandas.tkv',
     'tokenvector_pandas'),
    ('tvsrc/tokenvector_read_json.tkv',
     'tokenvector_read_json'),
    ('tvsrc/tokenvector_dtype.tkv',
     'tokenvector_dtype'),
]

# ---------------------------------------------------------------- all
p_all = 'tvsrc/tokenvector_data_all.tkv'
all_lines = read(p_all)
changed_all = False
for path, name in MODS:
    if contains(all_lines, '# Module Pandas parity closure') or contains(all_lines, '# Module JSON native'):
        pass
    else:
        i = find_marker(all_lines, '# tokenvector_io.tkv')
        assert i is not None, 'io header not found in all'
        src = read(path)
        body = strip_blanks(src)
        body = [l for l in body if not l.startswith('__tkv_import__')]
        block = ['# -*- coding: utf-8 -*-', '\n'] + body + ['\n', '\n']
        pre = []
        k = i - 1
        while k >= 0 and all_lines[k].strip() == '':
            pre.append('\n')
            k -= 1
        all_lines = all_lines[:k + 1] + block + pre + all_lines[i:]
        changed_all = True
        print('spliced vao all:', name)
for path, name in MODS:
    banner = ('# Module Pandas parity closure' if name == 'tokenvector_pandas'
              else ('# Module JSON native' if name == 'tokenvector_read_json'
                    else '# TokenVector.Data - Module dtype hep'))
    if contains(all_lines, banner):
        print('skip (da co trong all):', name)
    else:
        print('CANH BAO: chua splice vao all:', name)
if changed_all:
    write(p_all, all_lines)

# ---------------------------------------------------------------- lib
p_lib = 'tvsrc/_libbuild.tkv'
lib_lines = read(p_lib)
changed_lib = False
for path, name in MODS:
    marker_full = '# ==================== %s ====================\n' % name
    if find_marker(lib_lines, marker_full) is not None:
        print('skip (da co trong lib):', name)
        continue
    body = strip_blanks(read(path))
    # bo __tkv_import__ trong noi dung splice (lib khong import)
    body = [l for l in body if not l.startswith('__tkv_import__')]
    block = ['', marker_full, '\n'] + body + ['\n', '\n']
    # chen truoc 'def run' CUOI cung
    k = None
    for idx, l in enumerate(lib_lines):
        if l.startswith('def run'):
            k = idx
    assert k is not None, 'def run not found in lib'
    lib_lines = lib_lines[:k] + block + lib_lines[k:]
    changed_lib = True
    print('spliced vao lib:', name)
if changed_lib:
    write(p_lib, lib_lines)

print('merged2 splice ok (idempotent)')
