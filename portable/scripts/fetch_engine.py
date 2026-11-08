"""Fetch the exact official source tree and apply only the documented GUI changes."""
import json
import pathlib
import re
import subprocess
import sys

portable = pathlib.Path(__file__).resolve().parents[1]
identity = json.loads((portable / 'engine-source.json').read_text(encoding='utf-8'))
source = pathlib.Path(sys.argv[1]).resolve()
assert re.fullmatch(r'[0-9a-f]{40}', identity['source_commit'])
assert re.fullmatch(r'[0-9a-f]{40}', identity['source_tree'])

def git(*args):
    return subprocess.run(['git', '-C', str(source), *args], check=True, capture_output=True, text=True).stdout.strip()

source.mkdir(parents=True, exist_ok=False)
git('init')
git('config', 'core.autocrlf', 'false')
git('remote', 'add', 'origin', 'https://github.com/ReagentX/imessage-exporter.git')
git('fetch', '--depth', '1', 'origin', identity['source_commit'])
git('checkout', '--detach', 'FETCH_HEAD')
assert git('rev-parse', 'HEAD') == identity['source_commit']
assert git('rev-parse', 'HEAD^{tree}') == identity['source_tree']
git('apply', str(portable / 'patches/suoh-password.patch'))
subprocess.run([sys.executable, str(portable / 'scripts/prepare_engine.py'), str(source)], check=True)
git('diff', '--check')
print('Verified source:', identity['source_commit'], identity['source_tree'])
