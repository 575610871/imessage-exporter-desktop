"""Audit the pinned engine, GUI CLI contract and exports of upstream's public fixture."""
import json
import pathlib
import re
import sqlite3
import struct
import subprocess
import sys

engine, database, output = (pathlib.Path(arg).resolve() for arg in sys.argv[1:])
output.mkdir(parents=True, exist_ok=True)
identity = json.loads((pathlib.Path(__file__).resolve().parents[1] / 'engine-source.json').read_text(encoding='utf-8'))

def invoke(arguments, label):
    result = subprocess.run([str(engine), *arguments], capture_output=True, text=True,
                            encoding='utf-8', errors='replace', timeout=120)
    (output / f'{label}.log').write_text(result.stdout + result.stderr, encoding='utf-8')
    assert result.returncode == 0, f'{label} failed: {result.stdout}\n{result.stderr}'
    return result

version = invoke(['--version'], 'version').stdout.strip()
assert version == 'imessage-exporter ' + identity['engine_version'], f'Unexpected engine version: {version}'
help_text = invoke(['--help'], 'help').stdout
required = {
    'db-path', 'platform', 'format', 'export-path', 'copy-method', 'no-progress',
    'diagnostics', 'start-date', 'end-date', 'conversation-filter', 'custom-name',
    'use-caller-id', 'no-lazy', 'use-message-times',
}
available = set(re.findall(r'--([a-z][a-z-]+)\b', help_text))
assert required <= available, f'GUI options missing from engine: {sorted(required - available)}'
assert all(value in help_text for value in ('clone', 'basic', 'full', 'disabled', 'iOS', 'macOS')), 'CLI value contract changed'

pe = engine.read_bytes()
pe_offset = struct.unpack_from('<I', pe, 0x3C)[0]
assert pe[pe_offset:pe_offset + 4] == b'PE\x00\x00', 'Not a PE executable'
machine, sections = struct.unpack_from('<HH', pe, pe_offset + 4)
assert machine == 0x8664, 'Engine must be Windows x64'
optional_size = struct.unpack_from('<H', pe, pe_offset + 20)[0]
optional = pe_offset + 24
assert struct.unpack_from('<H', pe, optional)[0] == 0x20B, 'Expected PE32+'
table = optional + optional_size

def file_offset(rva):
    for i in range(sections):
        size, address, raw_size, raw_address = struct.unpack_from('<IIII', pe, table + i * 40 + 8)
        if address <= rva < address + max(size, raw_size):
            return raw_address + rva - address
    raise ValueError(f'Invalid RVA: {rva:x}')

imports_rva = struct.unpack_from('<I', pe, optional + 112 + 8)[0]
cursor = file_offset(imports_rva)
dlls = []
while any(pe[cursor:cursor + 20]):
    name = file_offset(struct.unpack_from('<I', pe, cursor + 12)[0])
    dlls.append(pe[name:pe.index(0, name)].decode('ascii'))
    cursor += 20
assert not any(name.lower().startswith(('vcruntime', 'msvcp', 'libgcc', 'libstdc++', 'libwinpthread')) for name in dlls), f'Unbundled runtime dependency: {dlls}'

with sqlite3.connect(f'{database.as_uri()}?mode=ro', uri=True) as connection:
    messages = connection.execute('SELECT COUNT(*) FROM message').fetchone()[0]

report = {'version': version, 'source_commit': identity['source_commit'], 'gui_flags': sorted(required),
          'imports': dlls, 'fixture_message_count': messages, 'exports': {}, 'gui_option_exports': {}}
for kind in ('html', 'txt'):
    destination = output / kind
    result = invoke(['--db-path', str(database), '--platform', 'macOS', '--format', kind,
                     '--copy-method', 'disabled', '--export-path', str(destination), '--no-progress'], kind)
    count = len(list(destination.rglob(f'*.{kind}')))
    assert messages == 0 or count > 0, f'No {kind} transcripts produced'
    report['exports'][kind] = {'exit_code': result.returncode, 'files': count}

    # Exercise timestamp/date/name switches exposed by the existing GUI.
    destination = output / (kind + '-options')
    switches = ['--custom-name', 'Suoh test user', '--no-lazy'] if kind == 'html' else ['--use-caller-id']
    result = invoke(['--db-path', str(database), '--platform', 'macOS', '--format', kind,
                     '--copy-method', 'clone', '--export-path', str(destination), '--no-progress',
                     '--start-date', '2001-01-01', '--end-date', '2100-01-01', '--use-message-times', *switches], kind + '-options')
    count = len(list(destination.rglob(f'*.{kind}')))
    assert messages == 0 or count > 0, f'No {kind} transcripts produced with GUI options'
    report['gui_option_exports'][kind] = {'exit_code': result.returncode, 'files': count}

diagnostics = invoke(['--db-path', str(database), '--platform', 'macOS', '--diagnostics'], 'diagnostics')
report['diagnostics'] = {'exit_code': diagnostics.returncode}
(output / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
