"""Exercise the actual AWS migration's Python body against disposable files only."""
from pathlib import Path
import re
import subprocess
import sys
import tempfile

backend = Path(__file__).resolve().parents[1]
script = (backend / 'Deploy/Aws/scripts/enable-room-control.sh').read_text(encoding='utf-8')
body = script.split("<<'PY'\n", 1)[1].split('\nPY', 1)[0]
with tempfile.TemporaryDirectory(prefix='jungle-room-config-') as folder:
    path = Path(folder) / 'config.env'
    original = "# Keep this comment\nROOM_CODE='ExampleOnly'\nJWT_SIGNING_KEY='ExistingExample'\nPUBLIC_DOMAIN='example.duckdns.org'\n"
    for initial in (original, original + "ROOM_CONTROL_TOKEN=''\n"):
        path.write_text(initial, encoding='utf-8')
        subprocess.run([sys.executable, '-c', body, str(path)], check=True, capture_output=True)
        first = path.read_text(encoding='utf-8')
        assert first.startswith(original)
        assert len(re.findall(r'^ROOM_CONTROL_TOKEN=', first, re.M)) == 1
        assert re.search(r"^ROOM_CONTROL_TOKEN='[0-9a-f]{64}'$", first, re.M)
        subprocess.run([sys.executable, '-c', body, str(path)], check=True, capture_output=True)
        assert path.read_text(encoding='utf-8') == first
    duplicate = original + "ROOM_CONTROL_TOKEN='A'\nROOM_CONTROL_TOKEN='B'\n"
    path.write_text(duplicate, encoding='utf-8')
    rejected = subprocess.run([sys.executable, '-c', body, str(path)], capture_output=True)
    assert rejected.returncode != 0 and path.read_text(encoding='utf-8') == duplicate
print('PASS 9 private room config migration checks; existing settings preserved, duplicate keys rejected.')
