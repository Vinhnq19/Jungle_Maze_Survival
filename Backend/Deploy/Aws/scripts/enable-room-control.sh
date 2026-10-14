#!/usr/bin/env bash
# Add one private key; preserve all previous account, DNS and database settings.
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
[[ -f "$CONFIG_FILE" ]] || { echo 'Run configure.sh first.' >&2; exit 1; }
python3 - "$CONFIG_FILE" <<'PY'
import os, re, secrets, sys, tempfile
path = sys.argv[1]
with open(path) as f:
    text = f.read()
matches = re.findall(r'^ROOM_CONTROL_TOKEN=(.*)$', text, re.M)
if len(matches) > 1:
    raise SystemExit('Duplicate ROOM_CONTROL_TOKEN; fix config manually, value not printed.')
if matches and matches[0].strip().strip("'\""):
    print('Room control key already exists; kept unchanged.')
else:
    text = re.sub(r'^ROOM_CONTROL_TOKEN=.*\n?', '', text, flags=re.M)
    # Atomic replacement on the same filesystem: a failed write preserves the old config.
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', dir=os.path.dirname(path), delete=False) as f:
            temporary = f.name
            f.write(text.rstrip() + "\nROOM_CONTROL_TOKEN='" + secrets.token_hex(32) + "'\n")
            f.flush(); os.fsync(f.fileno())
        os.chmod(temporary, 0o600)
        os.replace(temporary, path)
    finally:
        if temporary and os.path.exists(temporary):
            os.unlink(temporary)
    print('Private room control key added. No service was restarted.')
PY
