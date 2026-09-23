# -*- coding: utf-8 -*-
"""Splice idempotent: them tokenvector_p100 vao 2 file merged
(tokenvector_data_all.tkv va _libbuild.tkv). Cac module v1.7/v1.8 (pandas,
read_json, dtype) DA CO trong merged tu commit 5a59f96 - khong dong vao.

- all: module don le noi bang header '# -*- coding: utf-8 -*-' -> chen truoc
  header io (marker '# tokenvector_io.tkv'); banner rieng de idempotent.
- lib: marker '# ==================== tokenvector_p100 ===================='
  chen truoc 'def run' cuoi cung.
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


BANNER = '# Module pandas 100% closure (v1.9)'
SRC = 'tvsrc/tokenvector_p100.tkv'

# ---------------------------------------------------------------- all
p_all = 'tvsrc/tokenvector_data_all.tkv'
all_lines = read(p_all)
if contains(all_lines, BANNER):
    print('skip (da co trong all)')
else:
    i = find_marker(all_lines, '# tokenvector_io.tkv')
    assert i is not None, 'io header not found in all'
    body = strip_blanks(read(SRC))
    body = [l for l in body if not l.startswith('__tkv_import__')]
    block = [BANNER, '# -*- coding: utf-8 -*-', '\n'] + body + ['\n', '\n']
    pre = []
    k = i - 1
    while k >= 0 and all_lines[k].strip() == '':
        pre.append('\n')
        k -= 1
    all_lines = all_lines[:k + 1] + block + pre + all_lines[i:]
    write(p_all, all_lines)
    print('spliced vao all')

# ---------------------------------------------------------------- lib
p_lib = 'tvsrc/_libbuild.tkv'
lib_lines = read(p_lib)
marker_full = '# ==================== tokenvector_p100 ====================\n'
if find_marker(lib_lines, marker_full) is not None:
    print('skip (da co trong lib)')
else:
    body = strip_blanks(read(SRC))
    body = [l for l in body if not l.startswith('__tkv_import__')]
    block = ['', marker_full, '\n'] + body + ['\n', '\n']
    k = None
    for idx, l in enumerate(lib_lines):
        if l.startswith('def run'):
            k = idx
    assert k is not None, 'def run not found in lib'
    lib_lines = lib_lines[:k] + block + lib_lines[k:]
    write(p_lib, lib_lines)
    print('spliced vao lib')

print('p100 splice ok (idempotent)')
