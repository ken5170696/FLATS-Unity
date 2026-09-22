"""Check the public file boundary without importing Unity. Python 3.9+."""
from pathlib import Path
import re
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
files = set(subprocess.check_output(
    ['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=root
).decode().split('\0')) - {''}
roots = {'Assets', 'Packages', 'ProjectSettings', 'docs', 'tools', '.github'}
metadata = {'.gitignore', '.gitattributes', 'README.md', 'THIRD_PARTY_NOTICES.md'}
errors = []
guids = {}
checked_parents = set()
for name in sorted(files):
    path = root / name
    if name.split('/')[0] not in roots and name not in metadata:
        errors.append(f'Outside public allowlist: {name}')
    if path.is_symlink():
        errors.append(f'Symlink: {name}')
    for parent in path.parents:
        if parent == root or parent in checked_parents:
            break
        checked_parents.add(parent)
        if parent.is_symlink() or getattr(parent, 'is_junction', lambda: False)():
            errors.append(f'Linked parent directory: {parent.relative_to(root)}')
    if not path.is_file():
        errors.append(f'Missing tracked file: {name}')
        continue
    if path.stat().st_size >= 50 * 1024 * 1024:
        errors.append(f'Review large file before publishing: {name}')
    if path.name.lower() in {'agents.md', '.env', 'skills.md'}:
        errors.append(f'Private instruction/config file: {name}')
    if name.startswith('Assets/') and not name.endswith('.meta') and name + '.meta' not in files:
        errors.append(f'Missing meta: {name}')
    if path.suffix == '.meta':
        match = re.search(r'^guid: ([a-f0-9]{32})$', path.read_text(), re.M)
        if match:
            guid = match[1]
            if guid in guids:
                errors.append(f'Duplicate GUID: {name}, {guids[guid]}')
            guids[guid] = name
    if path.suffix in {'.cs', '.json', '.asset', '.prefab', '.unity', '.asmdef', '.md', '.txt'}:
        text = path.read_text(encoding='utf-8-sig', errors='replace')
        for pattern in [r'MCPForUnity', r'com\.coplaydev', r'active-takeover\.txt',
                        r'serialization_recovery', r'tail2511fc', r'C:\\Users\\',
                        r'(?m)^[ \t]*privateCode:[ \t]*\S+', r'gh[pousr]_[A-Za-z0-9]{30,}',
                        r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----']:
            if re.search(pattern, text):
                errors.append(f'Excluded dependency or credential pattern in {name}')
                break
print('\n'.join(errors) if errors else f'PASS: {len(files)} public files; {len(guids)} unique asset GUIDs')
sys.exit(bool(errors))
