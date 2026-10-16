#!/usr/bin/env bash
# Administrative benchmark helper. Never publish this functionality as an API.
set -Eeuo pipefail
source "$(dirname "$0")/common.sh"
require_root
load_release
load_config
override=/run/junglemaze-test-jwt.yaml
case "${1:-}" in
  apply-policy)
    # Only the approved numeric limits change; account/DNS/DB secrets remain intact.
    python3 - "$CONFIG_FILE" <<'PY_POLICY'
import os, re, sys, tempfile
path=sys.argv[1]
text=open(path).read()
for key,value in dict(MAX_ROOM_PLAYERS=10,MAX_TOTAL_PLAYERS=50,MAX_ROOMS=5,MAX_ROOMS_PER_USER=2).items():
    pattern=r'^'+key+r'=.*$'
    if len(re.findall(pattern,text,re.M))>1: raise SystemExit('Duplicate room policy key; value withheld.')
    if re.search(pattern,text,re.M): text=re.sub(pattern,key+'='+str(value),text,flags=re.M)
    else: text=text.rstrip()+'\n'+key+'='+str(value)+'\n'
temporary=None
try:
    with tempfile.NamedTemporaryFile(mode='w',dir=os.path.dirname(path),delete=False) as f:
        temporary=f.name; f.write(text); f.flush(); os.fsync(f.fileno())
    os.chmod(temporary,0o600);os.replace(temporary,path)
finally:
    if temporary and os.path.exists(temporary):os.unlink(temporary)
print('Approved room policy applied: 10 players/room, 50 total, 5 rooms, 2/user.')
PY_POLICY
    ;;
  short-jwt)
    umask 077
    printf 'services:\n  api:\n    environment:\n      Jwt__ExpiryMinutes: "1"\n' > "$override"
    # Independent restoration if the controlling PC loses its connection.
    systemd-run --quiet --collect --unit=junglemaze-jwt-restore --on-active=2m \
      /bin/bash /opt/junglemaze/current/scripts/room-test.sh restore-jwt
    compose -f "$override" up -d --no-build --pull never --no-deps --wait --wait-timeout 90 api >/dev/null 2>&1
    ;;
  restore-jwt)
    rm -f -- "$override"
    compose up -d --no-build --pull never --no-deps --wait --wait-timeout 90 api >/dev/null 2>&1
    systemctl stop junglemaze-jwt-restore.timer 2>/dev/null || true
    ;;
  default-code)
    # Read only over authenticated SSH into the test process; never persist in evidence.
    printf '%s' "$ROOM_CODE"
    ;;
  prepare)
    mkdir -p /run/junglemaze-room-test
    chmod 700 /run/junglemaze-room-test
    : > /run/junglemaze-room-test/memory.csv
    # Cgroup includes container overhead/cache. CPU/tick windows come from GameServer metrics.
    cat > /run/junglemaze-room-test/collect.sh <<'COLLECT'
#!/usr/bin/env bash
while true; do
  value=$(docker exec junglemaze-aws-game-server-1 sh -c 'if [ -f /sys/fs/cgroup/memory.current ]; then cat /sys/fs/cgroup/memory.current; else cat /sys/fs/cgroup/memory/memory.usage_in_bytes; fi') || break
  printf '%s,%s\n' "$(date -u +%Y-%m-%dT%H:%M:%S.%NZ)" "$value" >> /run/junglemaze-room-test/memory.csv
  sleep 1
done
COLLECT
    systemd-run --quiet --collect --unit=junglemaze-room-collector /bin/bash /run/junglemaze-room-test/collect.sh
    ;;
  snapshot)
    python3 - <<'PY'
import json, subprocess, pathlib, platform, os
def call(*a): return subprocess.check_output(a, text=True)
i=json.loads(call('docker','inspect','junglemaze-aws-game-server-1'))[0]
safe=['MAX_PLAYERS','MAX_ROOM_PLAYERS','MAX_TOTAL_PLAYERS','MAX_ROOMS','MAX_ROOMS_PER_USER','AUTH_MODE','TICK_RATE','METRICS_INTERVAL_SECONDS']
env=dict(e.split('=',1) for e in i['Config']['Env'] if '=' in e)
metrics=[l for l in call('docker','logs','--tail','10000','junglemaze-aws-game-server-1').splitlines() if l.startswith('[metrics]')]
mem=pathlib.Path('/run/junglemaze-room-test/memory.csv')
print(json.dumps(dict(host=dict(kernel=platform.release(),architecture=platform.machine(),logicalCpu=os.cpu_count(),
 totalMemoryKiB=int(pathlib.Path('/proc/meminfo').read_text().split('MemTotal:')[1].split()[0])),
 imageId=i['Image'], memoryLimitBytes=i['HostConfig']['Memory'], restartCount=i['RestartCount'],
 oomKilled=i['State']['OOMKilled'], running=i['State']['Running'], controlPortPublished=bool(i['HostConfig']['PortBindings'].get('7780/tcp')),
 configuration={k:env.get(k) for k in safe}, metrics=metrics, memory=mem.read_text().splitlines() if mem.exists() else [])))
PY
    ;;
  cleanup)
    systemctl stop junglemaze-room-collector.service 2>/dev/null || true
    ;;
  *) echo 'Actions: apply-policy short-jwt restore-jwt default-code prepare snapshot cleanup' >&2; exit 2 ;;
esac
