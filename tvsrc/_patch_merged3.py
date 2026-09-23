# -*- coding: utf-8 -*-
"""Splice idempotent v2.1: them tokenvector_sql + tokenvector_excel +
tokenvector_parquet vao 2 file merged (tokenvector_data_all.tkv, _libbuild.tkv).

Quy uoc nhu _patch_merged2.py:
- all: chen khoi (banner + body, bo __tkv_import__) truoc header io
  (marker '# tokenvector_io.tkv'). Idempotent theo needle `def <fn>(` dau tien.
- lib: chen khoi (marker '# ==== NAME ====' + body, bo __tkv_import__)
  truoc 'def run' CUOI file. Idempotent theo marker.
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


def contains(lines, needle):
    for l in lines:
        if needle in l:
            return True
    return False


MODS = [
    ('tvsrc/tokenvector_sql.tkv', 'tokenvector_sql', 'def sql_query('),
    ('tvsrc/tokenvector_excel.tkv', 'tokenvector_excel', 'def excel_write_string('),
    ('tvsrc/tokenvector_parquet.tkv', 'tokenvector_parquet', 'def parquet_blocked_reason('),
]

# ---------------------------------------------------------------- all
p_all = 'tvsrc/tokenvector_data_all.tkv'
all_lines = read(p_all)
changed_all = False
for path, name, needle in MODS:
    if contains(all_lines, needle):
        print('skip (da co trong all):', name)
        continue
    i = None
    for idx, l in enumerate(all_lines):
        if l.startswith('# tokenvector_io.tkv'):
            i = idx
            break
    assert i is not None, 'io header not found in all'
    body = strip_blanks(read(path))
    body = [l for l in body if not l.startswith('__tkv_import__')]
    banner = '# ==================== %s ====================\n' % name
    block = [banner, '\n'] + body + ['\n', '\n']
    pre = []
    k = i - 1
    while k >= 0 and all_lines[k].strip() == '':
        pre.append('\n')
        k -= 1
    all_lines = all_lines[:k + 1] + block + pre + all_lines[i:]
    changed_all = True
    print('spliced vao all:', name)
if changed_all:
    write(p_all, all_lines)

# ---------------------------------------------------------------- lib
p_lib = 'tvsrc/_libbuild.tkv'
lib_lines = read(p_lib)
changed_lib = False
for path, name, needle in MODS:
    marker_full = '# ==================== %s ====================\n' % name
    found = False
    for l in lib_lines:
        if l.startswith(marker_full.strip()):
            found = True
            break
    if found:
        print('skip (da co trong lib):', name)
        continue
    body = strip_blanks(read(path))
    body = [l for l in body if not l.startswith('__tkv_import__')]
    block = ['', marker_full, '\n'] + body + ['\n', '\n']
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

print('merged3 splice ok (idempotent)')
