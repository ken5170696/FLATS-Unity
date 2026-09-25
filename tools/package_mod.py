"""Package a crosshair or data manifest for desktop import or catalogue publication.

Schema 1 packages a single crosshair preset. Schema 2 packages a data module for a
game adapter (for example crosshair@2 or enemy.tuning@1), including its JSON payload
from the manifest's folder. This authoring tool checks basic data fields. The game's
installer remains the authority for compatibility, dependency, conflict and package
validation.
"""
import argparse
import hashlib
import json
import math
import re
import zipfile
from pathlib import Path

VERSION = r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
ADAPTER = r'[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*@[1-9][0-9]{0,3}'
SCOPES = ('ClientOnly', 'RequiredForSession')
# Scopes each game adapter accepts; an unknown adapter is left to the game to reject.
ADAPTER_SCOPES = {'crosshair@2': ('ClientOnly',), 'enemy.tuning@1': ('RequiredForSession',)}


def check_range(manifest, prefix, required):
    lower, upper = manifest.get(prefix + 'Minimum'), manifest.get(prefix + 'Maximum')
    if not required and lower in (None, '') and upper in (None, ''):
        return
    for key, value in ((prefix + 'Minimum', lower), (prefix + 'Maximum', upper)):
        if not isinstance(value, str) or not re.fullmatch(VERSION, value):
            raise ValueError(f'{key} must have three numeric components')
    if tuple(map(int, lower.split('.'))) >= tuple(map(int, upper.split('.'))):
        raise ValueError(f'{prefix} range is empty or reversed')


def check_payload(manifest, folder):
    name = manifest.get('payload')
    if name in (None, ''):
        if manifest['adapter'] == 'enemy.tuning@1':
            raise ValueError('enemy.tuning@1 packages need a payload')
        return None
    relative = Path(name)
    if (not isinstance(name, str) or relative.is_absolute() or '..' in relative.parts or
            '\\' in name or relative.suffix.lower() != '.json'):
        raise ValueError('payload must be a relative .json path inside the package')
    path = folder / relative
    data = path.read_bytes()
    if len(data) > 16 * 1024:
        raise ValueError('payload exceeds 16 KiB')
    value = json.loads(data)
    if manifest['adapter'] == 'enemy.tuning@1':
        if value.get('schema') != 1:
            raise ValueError('enemy.tuning@1 payload needs schema 1')
        for key in ('health', 'damage', 'speed'):
            number = value.get(key, 1)
            if type(number) not in (int, float) or not math.isfinite(number) or not 0.25 <= number <= 4:
                raise ValueError(f'{key} must be a multiplier between 0.25 and 4')
    return relative.as_posix(), data


def package(manifest_path, output):
    raw = manifest_path.read_bytes()
    if len(raw) > 96 * 1024:
        raise ValueError('Manifest exceeds 96 KiB')
    manifest = json.loads(raw)
    schema, kind = manifest.get('schema'), manifest.get('kind')
    if (schema, kind) not in ((1, 'crosshair'), (2, 'crosshair'), (2, 'data')):
        raise ValueError('This tool packages crosshair data (schema 1 or 2) and schema 2 data modules')
    identity = manifest.get('id', '')
    if not re.fullmatch(r'[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*', identity) or len(identity) > 80:
        raise ValueError('Use a stable lowercase ASCII module ID')
    if identity.startswith('flats.') or identity in ('staging', 'downloads', 'install-batch.json'):
        raise ValueError('Reserved module ID')
    if not isinstance(manifest.get('version'), str) or not re.fullmatch(VERSION, manifest['version']):
        raise ValueError('version must have three numeric components')
    # Data modules depend on their adapter version, so a game range is optional for them.
    check_range(manifest, 'game', kind != 'data')
    check_range(manifest, 'api', True)
    if schema == 2 and tuple(map(int, manifest['apiMinimum'].split('.'))) < (1, 1, 0):
        raise ValueError('Manifest schema 2 needs apiMinimum 1.1.0 or later')
    if manifest.get('scope') not in SCOPES:
        raise ValueError('scope must be ClientOnly or RequiredForSession')
    extra = None
    if kind == 'crosshair':
        size = manifest.get('crosshairSize')
        if (manifest.get('scope') != 'ClientOnly' or
                type(manifest.get('crosshairStyle')) is not int or
                manifest['crosshairStyle'] not in (0, 1, 2) or
                type(size) not in (int, float) or not math.isfinite(size) or not 6 <= size <= 64):
            raise ValueError('Crosshair requires ClientOnly scope, style 0–2, size 6–64')
    else:
        adapter = manifest.get('adapter')
        if not isinstance(adapter, str) or not re.fullmatch(ADAPTER, adapter):
            raise ValueError('Data packages need an adapter such as crosshair@2')
        allowed = ADAPTER_SCOPES.get(adapter)
        if allowed and manifest['scope'] not in allowed:
            raise ValueError(f'{adapter} requires scope {" or ".join(allowed)}')
        if manifest.get('assembly') or manifest.get('entryType'):
            raise ValueError('Data packages cannot contain code')
        extra = check_payload(manifest, manifest_path.parent)
    for key, limit in (('name', 120), ('author', 120), ('category', 40)):
        value = manifest.get(key)
        if not isinstance(value, str) or not value.strip() or len(value) > limit:
            raise ValueError(f'{key} is required and must fit within {limit} characters')
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f'{identity}-{manifest["version"]}.zip'
    # Exclusive creation prevents accidentally replacing an immutable version.
    with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED) as target:
        entries = [('manifest.json', json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8'))]
        if extra:
            entries.append(extra)
        for name, data in entries:
            info = zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            target.writestr(info, data)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    receipt = {'file': archive.name, 'bytes': archive.stat().st_size, 'sha256': digest,
               'id': identity, 'version': manifest['version']}
    archive.with_suffix('.receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(receipt, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('manifest', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    arguments = parser.parse_args()
    try:
        package(arguments.manifest, arguments.output)
    except (ValueError, OSError, zipfile.BadZipFile) as error:
        parser.exit(1, f'Could not package module: {error}\n')
