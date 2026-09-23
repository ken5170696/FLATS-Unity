"""Fast source integrity gate. Unity import and Player tests are separate checks."""
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
errors = []
tracked = set(subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().strip('\0').split('\0'))

version = (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text(encoding='utf-8')
if 'm_EditorVersion: 6000.3.24f1' not in version:
    errors.append('Unity editor version differs from documented 6000.3.24f1')

player_settings = (ROOT / 'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf-8')
package_rules = (ROOT / 'Assets/_Flats/Runtime/Core/Modules/PackageModels.cs').read_text(encoding='utf-8')
player_version = re.search(r'(?m)^  bundleVersion: (\S+)$', player_settings)
module_version = re.search(r'GameVersion = "([^"]+)"', package_rules)
if not player_version or not module_version or player_version[1] != module_version[1]:
    errors.append('Player bundleVersion and module compatibility GameVersion differ')

manifest = json.loads((ROOT / 'Packages/manifest.json').read_text(encoding='utf-8'))
lock = json.loads((ROOT / 'Packages/packages-lock.json').read_text(encoding='utf-8'))
for package, requested in manifest['dependencies'].items():
    pinned = lock['dependencies'].get(package)
    if not pinned or pinned.get('version') != requested:
        errors.append(f'Package lock mismatch: {package}')

guids = {}
assets = [name for name in tracked if name.startswith('Assets/')]
for name in assets:
    path = ROOT / name
    if not path.is_file():
        errors.append(f'Missing tracked asset: {name}')
        continue
    if not name.endswith('.meta') and name + '.meta' not in tracked:
        errors.append(f'Missing tracked .meta: {name}')
    if name.endswith('.meta'):
        match = re.search(r'(?m)^guid: ([0-9a-f]{32})$', path.read_text(encoding='utf-8-sig', errors='replace'))
        if not match:
            errors.append(f'Missing or invalid GUID: {name}')
        elif match[1] in guids:
            errors.append(f'Duplicate GUID: {name} and {guids[match[1]]}')
        else:
            guids[match[1]] = name

if errors:
    print('\n'.join(errors), file=sys.stderr)
    sys.exit(1)
print(f'PASS: Unity version, {len(manifest["dependencies"])} direct packages, {len(assets)} assets, {len(guids)} GUIDs')
