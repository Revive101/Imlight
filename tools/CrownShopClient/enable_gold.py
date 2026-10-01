#!/usr/bin/env python3
"""Let the private r806919.Wizard_1_610 client use the server's Crown Shop gold price.

The native catalog reader overrides a positive gold price with FLAG_CrownsOnly
from the item template. Change its conditional jump to always use its existing
`goldCost < 1` fallback. This affects Crown Shop currency selection only; the
server still validates price, funds, inventory and supported delivery.

Usage: python3 enable_gold.py /path/to/private/WizardGraphicalClient.exe
Requires a full client restart. The original executable is backed up beside it.
"""
import argparse
import hashlib
import os
from pathlib import Path
import shutil
import tempfile

# WGCPCSInterface's catalog reader, VA 0x141306699 in this client revision.
# Includes the FLAG_CrownsOnly lookup and the existing gold-price fallback.
ORIGINAL = bytes.fromhex(
    '488b7424604885f6741b488d0d6e798f01e871540d008bd0488bcee8670c2300'
    '0fb6c8eb0a83bb4c010000010f9cc1488b45808888')
PATCHED = ORIGINAL[:8] + b'\xeb' + ORIGINAL[9:]


def transform(data):
    if data[:2] != b'MZ':
        raise ValueError('Not a Windows executable')
    if data.count(PATCHED) == 1 and ORIGINAL not in data:
        return data
    if data.count(ORIGINAL) != 1 or PATCHED in data:
        raise ValueError('Unrecognized client revision; no changes made')
    return data.replace(ORIGINAL, PATCHED, 1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client', type=Path)
    args = parser.parse_args()
    path = args.client.resolve()
    original = path.read_bytes()
    updated = transform(original)
    if updated == original:
        print('Gold-price patch is already installed.')
        return
    digest = hashlib.sha256(original).hexdigest()
    backup = path.with_name(path.name + '.before-crown-shop-gold-' + digest[:12])
    if backup.exists():
        if backup.read_bytes() != original:
            raise ValueError('Backup differs; refusing to overwrite it')
    else:
        shutil.copy2(path, backup)
    # Replace the file atomically; do not modify the running client's mapped file.
    fd, temporary = tempfile.mkstemp(prefix=path.name + '.', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as output:
            output.write(updated)
            output.flush()
            os.fsync(output.fileno())
        shutil.copystat(path, temporary)
        os.replace(temporary, path)
    finally:
        Path(temporary).unlink(missing_ok=True)
    assert path.read_bytes() == updated
    print('Installed Crown Shop gold-price patch. Restart Wizard101 to activate it.')
    print('Backup:', backup)
    print('Changed bytes:', sum(a != b for a, b in zip(original, updated)))


if __name__ == '__main__':
    main()
