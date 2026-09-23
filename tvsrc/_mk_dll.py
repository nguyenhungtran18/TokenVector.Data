# -*- coding: utf-8 -*-
"""Chuyen IL exe cua tokenvector_data_all thanh library IL cho
TokenVector.Data.dll (dung quy trinh commit b205bb9):
  - .assembly TKVApp {}       -> .assembly TokenVector.Data {}
  - .module TKVApp.exe        -> .module TokenVector.Data.dll
  - xoa dong .entrypoint (Main giu nguyen, khong con la entrypoint)
Chay tren IL vua build moi (exe-form tokenvector_data_all.il)
-> ghi ra TokenVector.Data.il (library-form), xong goi ilasm.
"""
import io
import os
import subprocess
import sys

os.chdir(os.path.dirname(os.path.abspath(__file__)))

SRC = 'tokenvector_data_all.il'
DST = 'TokenVector.Data.il'

lines = io.open(SRC, encoding='utf-8', newline='').readlines()
out = []
removed = {'asm': 0, 'mod': 0, 'ep': 0}
for l in lines:
    s = l.strip()
    if s == '.assembly TKVApp {}':
        out.append('.assembly TokenVector.Data {}\r\n')
        removed['asm'] += 1
        continue
    if s == '.module TKVApp.exe':
        out.append('.module TokenVector.Data.dll\r\n')
        removed['mod'] += 1
        continue
    if s == '.entrypoint':
        removed['ep'] += 1
        continue
    out.append(l)

io.open(DST, 'w', encoding='utf-8', newline='').writelines(out)
print('converted:', removed)

ilasm = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\ilasm.exe'
r = subprocess.run([ilasm, '/quiet', '/dll', '/output:TokenVector.Data.dll', DST],
                   capture_output=True, text=True)
print('ilasm rc=', r.returncode)
if r.returncode != 0:
    print(r.stdout[-3000:])
    print(r.stderr[-2000:])
    sys.exit(1)
print('ilasm ok')
