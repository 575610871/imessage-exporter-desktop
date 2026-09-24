"""Stamp upstream's 0.0.0 workspace placeholders without changing locked dependencies.

The source archive retains the original files. Run this script once after applying
the GUI password bridge; it is also safe to run again on the same snapshot.
"""
import json
import pathlib
import re
import sys
import tomllib

source = pathlib.Path(sys.argv[1]).resolve()
identity = json.loads((pathlib.Path(__file__).resolve().parents[1] / 'engine-source.json').read_text(encoding='utf-8'))
version = identity['engine_version']
assert re.fullmatch(r'\d+\.\d+\.\d+\+suoh\.[0-9a-f]{7}', version), 'Unexpected engine version format'
assert version == identity['base_release'] + '+suoh.' + identity['source_commit'][:7]
packages = ('imessage-database', 'imessage-exporter')
changes = {}
for name in packages:
    path = source / name / 'Cargo.toml'
    text = path.read_text(encoding='utf-8')
    data = tomllib.loads(text)
    assert data['package']['name'] == name
    assert data['package']['version'] in ('0.0.0', version), 'Unexpected upstream package version'
    updated, count = re.subn(r'^version = "[^"]+"$', 'version = "' + version + '"', text, count=1, flags=re.MULTILINE)
    assert count == 1
    result = tomllib.loads(updated)
    data['package']['version'] = version
    assert result == data, 'Only the workspace package version may change'
    changes[path] = updated

lock_path = source / 'Cargo.lock'
lock = lock_path.read_text(encoding='utf-8')
original = tomllib.loads(lock)
for name in packages:
    entries = [p for p in original['package'] if p['name'] == name and 'source' not in p]
    assert len(entries) == 1 and entries[0]['version'] in ('0.0.0', version)
    entries[0]['version'] = version
    pattern = r'(\[\[package\]\]\nname = "' + re.escape(name) + r'"\nversion = ")[^"]+("\n)'
    lock, count = re.subn(pattern, lambda match: match[1] + version + match[2], lock)
    assert count == 1, 'Workspace package must occur exactly once in Cargo.lock'
assert tomllib.loads(lock) == original, 'Locked third-party dependencies must not change'
changes[lock_path] = lock
for path, contents in changes.items():
    path.write_text(contents, encoding='utf-8', newline='\n')
print('Engine version: ' + version + '; third-party dependency lock preserved.')
