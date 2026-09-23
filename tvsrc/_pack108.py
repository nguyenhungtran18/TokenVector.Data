# -*- coding: utf-8 -*-
"""Pack TokenVector.Data.1.0.8-dev.nupkg (khong co nuget.exe -> zip thu cong
theo layout ban 1.0.7-dev)."""
import io, os, shutil, zipfile

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
STAGE = r'C:\Users\Admin\AppData\Local\Temp\opencode\nupkg108'
shutil.rmtree(STAGE, ignore_errors=True)
os.makedirs(os.path.join(STAGE, '_rels'))
os.makedirs(os.path.join(STAGE, 'lib', 'net8.0'))
os.makedirs(os.path.join(STAGE, 'package', 'services', 'metadata', 'core-properties'))

zold = zipfile.ZipFile('packages/TokenVector.Data.1.0.7-dev.nupkg')
rels = zold.read('_rels/.rels')
ct = zold.read('[Content_Types].xml')
readme = zold.read('README.md')
zold.close()

desc = ('High-performance columnar DataFrame and tabular data processing '
        'library, natively implemented in the TokenVector language (tkv) and '
        'compiled to a .NET CIL DLL: schema, columns, series, vector math, '
        'filter, group-by, joins (incl. AsOf), pivot/melt, CSV/JSON IO, Arrow '
        'IPC stream and out-of-core batches, SQL SELECT engine, '
        'SpreadsheetML Excel IO, zero-copy Mat bridge for numerics.')
psmdcp = ('<?xml version="1.0" encoding="utf-8"?>\r\n'
          '<coreProperties xmlns:dc="http://purl.org/dc/elements/1.1/" '
          'xmlns:dcterms="http://purl.org/dc/terms/" '
          'xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" '
          'xmlns="http://schemas.openxmlformats.org/package/2006/metadata/core-properties">\r\n'
          '  <dc:creator>TokenVector Project Team</dc:creator>\r\n'
          '  <dc:description>' + desc + '</dc:description>\r\n'
          '  <dc:identifier>TokenVector.Data</dc:identifier>\r\n'
          '  <version>1.0.8-dev</version>\r\n'
          '  <keywords>dataframe columnar arrow feather csv json timeseries asofjoin tokenvector data</keywords>\r\n'
          '  <lastModifiedBy>NuGet, Version=7.9.0.83, Culture=neutral, PublicKeyToken=31bf3856ad364e35;'
          'Microsoft Windows NT 10.0.19045.0;.NET Framework 4.7.2</lastModifiedBy>\r\n'
          '</coreProperties>')

def w(rel, data):
    p = os.path.join(STAGE, *rel.split('/'))
    if isinstance(data, str):
        io.open(p, 'w', encoding='utf-8', newline='').write(data)
    else:
        io.open(p, 'wb').write(data)

w('_rels/.rels', rels)
w('[Content_Types].xml', ct)
w('README.md', readme)
w('package/services/metadata/core-properties/nuget.psmdcp', psmdcp)
nuspec = io.open('tvsrc/package/TokenVector.Data.nuspec', encoding='utf-8').read()
w('TokenVector.Data.nuspec', nuspec)
shutil.copyfile('tvsrc/TokenVector.Data.dll',
                os.path.join(STAGE, 'lib', 'net8.0', 'TokenVector.Data.dll'))

out = 'packages/TokenVector.Data.1.0.8-dev.nupkg'
if os.path.exists(out):
    os.remove(out)
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(STAGE):
        for f in files:
            full = os.path.join(root, f)
            arc = os.path.relpath(full, STAGE).replace(os.sep, '/')
            z.write(full, arc)
print('packed:', out, os.path.getsize(out))
with zipfile.ZipFile(out) as z:
    print(z.namelist())
