"""Package a crosshair manifest for desktop import or catalogue publication.

This authoring tool checks basic data fields. The game's installer remains the
authority for compatibility, dependency, conflict and package validation.
"""
import argparse
import hashlib
import json
import math
import re
import zipfile
from pathlib import Path


def package(manifest_path, output):
    raw = manifest_path.read_bytes()
    if len(raw) > 96 * 1024:
        raise ValueError('Manifest exceeds 96 KiB')
    manifest = json.loads(raw)
    if manifest.get('schema') != 1 or manifest.get('kind') != 'crosshair':
        raise ValueError('This tool packages schema 1 crosshair data only')
    identity = manifest.get('id', '')
    if not re.fullmatch(r'[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*', identity) or len(identity) > 80:
        raise ValueError('Use a stable lowercase ASCII module ID')
    if identity.startswith('flats.') or identity in ('staging', 'downloads', 'install-batch.json'):
        raise ValueError('Reserved module ID')
    for key in ('version', 'gameMinimum', 'gameMaximum', 'apiMinimum', 'apiMaximum'):
        if not re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', manifest.get(key, '')):
            raise ValueError(f'{key} must have three numeric components')
    for prefix in ('game', 'api'):
        lower = tuple(map(int, manifest[prefix + 'Minimum'].split('.')))
        upper = tuple(map(int, manifest[prefix + 'Maximum'].split('.')))
        if lower >= upper:
            raise ValueError(f'{prefix} range is empty or reversed')
    size = manifest.get('crosshairSize')
    if (manifest.get('scope') != 'ClientOnly' or
            type(manifest.get('crosshairStyle')) is not int or
            manifest['crosshairStyle'] not in (0, 1, 2) or
            type(size) not in (int, float) or not math.isfinite(size) or not 6 <= size <= 64):
        raise ValueError('Crosshair requires ClientOnly scope, style 0–2, size 6–64')
    for key, limit in (('name', 120), ('author', 120), ('category', 40)):
        value = manifest.get(key)
        if not isinstance(value, str) or not value.strip() or len(value) > limit:
            raise ValueError(f'{key} is required and must fit within {limit} characters')
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f'{identity}-{manifest["version"]}.zip'
    # Exclusive creation prevents accidentally replacing an immutable version.
    with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED) as target:
        info = zipfile.ZipInfo('manifest.json', (2026, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        target.writestr(info, json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8'))
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
