"""Isolated local network benchmark. Never starts/deploys EC2 or reads Backend/.env.

--remote is an explicit, future opt-in: requires an existing Docker context, JWT
in BENCH_ACCESS_TOKEN, reachable API/client config and a named server container.
It only creates/deletes its own room; never restarts or removes remote services.
"""
import argparse
import csv
import datetime as dt
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import secrets
import socket
import subprocess
import time
import urllib.error
import urllib.request
import re
from concurrent.futures import ThreadPoolExecutor

BACKEND = Path(__file__).resolve().parents[1]
CLIENT = BACKEND / 'NetSmokeClient/bin/Release/net8.0/NetSmokeClient.dll'
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def command(args, **kwargs):
    return subprocess.check_output(args, text=True, encoding='utf-8', **kwargs).strip()


def port(kind=socket.SOCK_STREAM):
    with socket.socket(type=kind) as s:
        s.bind(('127.0.0.1', 0))
        return s.getsockname()[1]


def request(base, method, path, token=None, body=None, key=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if key:
        headers['X-Room-Control-Key'] = key
    req = urllib.request.Request(base + path, method=method, headers=headers,
                                 data=json.dumps(body).encode() if body is not None else None)
    try:
        response = OPENER.open(req, timeout=10)
    except urllib.error.HTTPError as e:
        response = e
    with response:
        raw = response.read().decode()
        return response.status, json.loads(raw) if raw and 'json' in response.headers.get('Content-Type', '') else raw


def quantile(samples, p):
    return sorted(samples)[math.ceil(len(samples) * p) - 1] if samples else None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--functional-only', action='store_true')
    parser.add_argument('--remote', action='store_true')
    parser.add_argument('--docker-context')
    parser.add_argument('--api-url')
    parser.add_argument('--client-config')
    parser.add_argument('--server-container')
    parser.add_argument('--levels', default='5,10')
    parser.add_argument('--warmup', type=int, default=15)
    parser.add_argument('--seconds', type=int, default=60)
    parser.add_argument('--confirm-seconds', type=int, default=180)
    parser.add_argument('--output')
    args = parser.parse_args()
    levels = [int(x) for x in args.levels.split(',')]
    if not levels or sorted(set(levels)) != levels or min(levels) < 1 or max(levels) > 10:
        parser.error('Current room policy: levels must increase within 1..10.')
    if args.warmup < 0 or not 1 <= args.seconds <= 3600 or not 1 <= args.confirm_seconds <= 3600:
        parser.error('Invalid measurement duration.')
    if args.remote and (not all((args.docker_context, args.api_url, args.client_config, args.server_container))
                        or not os.environ.get('BENCH_ACCESS_TOKEN')):
        parser.error('Remote needs --docker-context/--api-url/--client-config/--server-container and BENCH_ACCESS_TOKEN.')
    if not args.remote and args.docker_context:
        parser.error('Use --remote explicitly; local benchmark always uses Docker Desktop default context.')
    run_id = 'room-bench-' + dt.datetime.now(dt.timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + secrets.token_hex(3)
    out = Path(args.output).resolve() if args.output else BACKEND / 'out/room-bench' / run_id
    out.mkdir(parents=True, exist_ok=False)
    docker = ['docker', '--context', args.docker_context] if args.remote else ['docker', '--context', 'desktop-linux']
    # Explicit context prevents an ambient DOCKER_HOST from accidentally targeting EC2.
    clean_env = {k: v for k, v in os.environ.items() if k not in ('DOCKER_HOST', 'DOCKER_CONTEXT')}
    def dc(*parts):
        return command(docker + list(parts), env=clean_env)
    def compose(*parts):
        return dc('compose', '-p', run_id, '-f', str(out / 'compose.json'), *parts)
    stages, checks, room_ids = [], [], []
    result = {'runId': run_id, 'scope': 'network ping/pong only; no gameplay simulation', 'remote': args.remote,
              'host': {'platform': platform.platform(), 'processor': platform.processor(), 'logicalCpu': os.cpu_count()},
              'gitCommit': command(['git', 'rev-parse', 'HEAD'], cwd=BACKEND), 'stages': stages, 'checks': checks}
    result['parameters'] = {'levels': levels, 'warmupSeconds': args.warmup, 'measureSeconds': args.seconds,
                            'confirmationSeconds': args.confirm_seconds, 'pingRate': 30}
    if platform.system() == 'Windows':
        machine = command(['powershell', '-NoProfile', '-Command',
                           '$c=Get-CimInstance Win32_Processor; $m=Get-CimInstance Win32_ComputerSystem; '
                           '@{cpu=($c.Name -join "; "); ramBytes=$m.TotalPhysicalMemory} | ConvertTo-Json -Compress'])
        result['host'].update(json.loads(machine))
    # Fingerprint actual backend source/config, including uncommitted changes; omit secrets/bin/obj/out.
    files = sorted(p for p in BACKEND.rglob('*') if p.is_file() and p.suffix in ('.cs', '.csproj', '.yaml', '.py')
                   and not any(x in p.relative_to(BACKEND).parts for x in ('bin', 'obj', 'out')))
    digest = hashlib.sha256()
    for p in files:
        digest.update(p.relative_to(BACKEND).as_posix().encode()); digest.update(p.read_bytes())
    result['backendSourceSha256'] = digest.hexdigest()
    result['dockerInfo'] = json.loads(dc('info', '--format', '{{json .}}'))
    # Keep environment useful without publishing registry credentials or raw Docker configuration.
    result['dockerInfo'] = {k: result['dockerInfo'].get(k) for k in ('NCPU', 'MemTotal', 'KernelVersion', 'OperatingSystem', 'ServerVersion', 'CgroupVersion')}
    room_code = secrets.token_hex(16)
    control_key = secrets.token_hex(32)
    room_id = token = None
    stack_started = False
    container = args.server_container
    config = Path(args.client_config).resolve() if args.remote else out / 'client.json'
    base = args.api_url
    private_env = out / 'private.env'
    def check(name, ok):
        checks.append({'name': name, 'passed': bool(ok)})
        print(('PASS ' if ok else 'FAIL ') + name, flush=True)
        if not ok:
            raise AssertionError(name)
    def login(name):
        credentials = {'username': name, 'password': secrets.token_urlsafe(24)}
        check('register ' + name, request(base, 'POST', '/api/auth/register', body=credentials)[0] == 201)
        status, login_data = request(base, 'POST', '/api/auth/login', body=credentials)
        check('login ' + name, status == 200)
        return login_data['accessToken']
    def run_clients(count, code, seconds, warmup, label, target_room=None):
        report_file = out / (label + '.json')
        env = os.environ.copy()
        env['NET_ROOM_CODE'] = code
        argv = ['dotnet', str(CLIENT), '--config', str(config), '--clients', str(count), '--name', 'Benchmark',
                '--duration', str(seconds), '--warmup', str(warmup), '--ping-rate', '30', '--quiet',
                '--report', str(report_file), '--room-label', target_room or room_id or 'default']
        with (out / (label + '.log')).open('w', encoding='utf-8') as log:
            proc = subprocess.Popen(argv, cwd=BACKEND, env=env, stdout=log, stderr=subprocess.STDOUT)
            try:
                proc.wait(timeout=warmup + seconds + 30)
            finally:
                if proc.poll() is None:
                    proc.kill(); proc.wait()
        return json.loads(report_file.read_text(encoding='utf-8'))
    def wait_empty(owner_token, rid):
        for _ in range(40):
            status, rooms = request(base, 'GET', '/api/rooms', owner_token)
            if status == 200 and all(r['activePlayers'] + r['pendingPlayers'] == 0 for r in rooms if r['id'] == rid):
                return
            time.sleep(.25)
        raise RuntimeError('Room did not release slots after client exit.')
    try:
        subprocess.run(['dotnet', 'build', 'NetSmokeClient/NetSmokeClient.csproj', '-c', 'Release'], cwd=BACKEND, check=True)
        if not args.remote:
            api_port, udp_port = port(), port(socket.SOCK_DGRAM)
            base = f'http://127.0.0.1:{api_port}'
            # Compose receives secrets from a temporary, private file, never from argv or checked-in YAML.
            private_env.write_text(f'ROOM_CODE={room_code}\nROOM_CONTROL_TOKEN={control_key}\nJwt__SigningKey={secrets.token_hex(48)}\n', encoding='utf-8')
            common = {'env_file': [str(private_env)]}
            spec = {'services': {
                'mongo': {'image': 'mongo:8.0', 'volumes': ['data:/data/db'], 'mem_limit': '768m',
                          'command': ['mongod', '--wiredTigerCacheSizeGB', '0.25'],
                          'healthcheck': {'test': ['CMD', 'mongosh', '--quiet', '--eval', 'quit(db.adminCommand({ping:1}).ok ? 0 : 1)'],
                                          'interval': '2s', 'timeout': '5s', 'retries': 40}},
                'api': dict(common, build={'context': str(BACKEND), 'dockerfile': 'Api/Dockerfile'}, mem_limit='384m',
                            ports=[f'127.0.0.1:{api_port}:5000'], depends_on={'mongo': {'condition': 'service_healthy'}},
                            environment={'Mongo__ConnectionString': 'mongodb://mongo:27017', 'Mongo__Database': 'benchmark',
                                         'Jwt__Issuer': 'JungleMaze.Local', 'Jwt__Audience': 'JungleMaze.Game',
                                         'Swagger__Enabled': 'true', 'RoomControl__BaseUrl': 'http://game-server:7780'}),
                'game-server': dict(common, build={'context': str(BACKEND), 'dockerfile': 'GameServer/Dockerfile'},
                                    mem_limit='256m', memswap_limit='256m', ports=[f'127.0.0.1:{udp_port}:7777/udp'],
                                    environment={'GAME_BIND_ADDRESS': '0.0.0.0', 'GAME_PORT': '7777', 'MAX_PLAYERS': '10',
                                                 'AUTH_MODE': 'RoomCode', 'TICK_RATE': '30', 'METRICS_INTERVAL_SECONDS': '1',
                                                 'ROOM_CONTROL_BIND_ADDRESS': '0.0.0.0'})}, 'volumes': {'data': {}}}
            (out / 'compose.json').write_text(json.dumps(spec, indent=2), encoding='utf-8')
            config.write_text(json.dumps(dict(mode='Local', gameServerHost='127.0.0.1', gameServerPort=udp_port,
                                              authMode='RoomCode', protocolVersion=1, connectTimeoutMs=5000)), encoding='utf-8')
            stack_started = True
            print('Building isolated local Docker stack...', flush=True)
            with (out / 'build.log').open('w', encoding='utf-8') as build_log:
                subprocess.run(docker + ['compose', '-p', run_id, '-f', str(out / 'compose.json'), 'up', '-d', '--build'],
                               env=clean_env, stdout=build_log, stderr=subprocess.STDOUT, check=True)
            container = compose('ps', '-q', 'game-server')
            for _ in range(120):
                try:
                    if request(base, 'GET', '/health')[0] == 200:
                        break
                except (OSError, urllib.error.URLError):
                    pass
                time.sleep(.5)
            else:
                raise RuntimeError('Isolated API health timeout; see build/server logs.')
            status, spec = request(base, 'GET', '/swagger/v1/swagger.json')
            capacity_schema = spec['components']['schemas']['CreateRoomRequest']['properties']['maxPlayers']
            check('Swagger advertises maximum/default capacity 10', status == 200
                  and capacity_schema['maximum'] == 10 and capacity_schema['default'] == 10)
            token = login('benchmark')
            owner, other = login('roomowner'), login('otherowner')
            check('JWT required to list rooms', request(base, 'GET', '/api/rooms')[0] == 401)
            status, listed = request(base, 'GET', '/api/rooms', owner)
            default_id = listed[0]['id']
            check('default visible without code', status == 200 and len(listed) == 1 and listed[0]['roomCode'] is None)
            own = []
            for i in range(2):
                status, created = request(base, 'POST', '/api/rooms', owner, {'name': 'Test ' + str(i), 'maxPlayers': 2, 'ownerId': 'spoofed'})
                check('create owner room ' + str(i), status == 201 and bool(created['roomCode']))
                check('six-character readable join code', bool(re.fullmatch(r'[A-HJ-NP-Z2-9]{6}', created['roomCode'])))
                own.append(created); room_ids.append((owner, created['id']))
            check('owner cannot create third added room', request(base, 'POST', '/api/rooms', owner, {'name': 'Third', 'maxPlayers': 2})[0] == 409)
            check('capacity validation >10', request(base, 'POST', '/api/rooms', owner, {'name': 'Bad', 'maxPlayers': 11})[0] == 400)
            check('blank room name rejected', request(base, 'POST', '/api/rooms', owner, {'name': '', 'maxPlayers': 2})[0] == 400)
            check('sixth create request rate-limited', request(base, 'POST', '/api/rooms', owner, {'name': 'Sixth', 'maxPlayers': 2})[0] == 429)
            check('other user cannot see private rooms', len(request(base, 'GET', '/api/rooms', other)[1]) == 1)
            check('non-owner delete denied', request(base, 'DELETE', '/api/rooms/' + own[0]['id'], other)[0] == 403)
            check('default deletion blocked', request(base, 'DELETE', '/api/rooms/' + default_id, owner)[0] == 409)
            owner_list = request(base, 'GET', '/api/rooms', owner)[1]
            check('owner derived from JWT, ignores body ownerId', len(owner_list) == 3)
            for item in own:
                check('owner deletes empty room', request(base, 'DELETE', '/api/rooms/' + item['id'], owner)[0] == 204)
            room_ids.clear()
            smoke = run_clients(11, room_code, 3, 0, 'default-11')
            check('default remains 10 and rejects eleventh', smoke['joined'] == 10 and sum(c['reason'] == 'RoomFull' for c in smoke['clients']) == 1)
            wait_empty(token, default_id)
        else:
            token = os.environ['BENCH_ACCESS_TOKEN']
        status, created = request(base, 'POST', '/api/rooms', token, {'name': 'Benchmark ' + run_id[-6:], 'maxPlayers': 10})
        check('create separate 10-slot benchmark room', status == 201)
        room_id, code = created['id'], created['roomCode']
        room_ids.append((token, room_id))
        inspect = json.loads(dc('inspect', container))[0]
        limit = inspect['HostConfig']['Memory']
        check('GameServer limit is 256MiB', limit == 256 * 1024 * 1024)
        check('private HTTP control port is not published', '7780/tcp' not in inspect['HostConfig']['PortBindings'])
        result['server'] = {'memoryLimitBytes': limit, 'imageId': inspect['Image'], 'cpuQuota': inspect['HostConfig']['NanoCpus']}
        safe_environment = dict(item.split('=', 1) for item in inspect['Config']['Env'] if '=' in item)
        defaults = {'MAX_PLAYERS': '10', 'MAX_ROOM_PLAYERS': '10', 'MAX_TOTAL_PLAYERS': '50', 'MAX_ROOMS': '5',
                    'MAX_ROOMS_PER_USER': '2', 'ROOM_EMPTY_TTL_SECONDS': '600', 'AUTH_MODE': 'RoomCode',
                    'TICK_RATE': '30', 'METRICS_INTERVAL_SECONDS': '5'}
        result['server']['configuration'] = {k: safe_environment.get(k, v) for k, v in defaults.items()}
        if not args.remote:
            with ThreadPoolExecutor(max_workers=1) as executor:
                future = executor.submit(run_clients, 11, code, 3, 0, 'room-11')
                occupied = False
                for _ in range(60):
                    status, current = request(base, 'GET', '/api/rooms', token)
                    if status == 200 and any(r['id'] == room_id and r['activePlayers'] > 0 for r in current):
                        occupied = True; break
                    time.sleep(.05)
                check('cannot delete occupied room through API', occupied and request(base, 'DELETE', '/api/rooms/' + room_id, token)[0] == 409)
                oversized = future.result()
            check('dynamic room accepts 10 and rejects 11th', oversized['joined'] == 10 and sum(c['reason'] == 'RoomFull' for c in oversized['clients']) == 1)
            wait_empty(token, room_id)
            # Real process restart, not just a new in-memory registry instance.
            compose('restart', 'game-server')
            for _ in range(80):
                status, current = request(base, 'GET', '/api/rooms', token)
                if status == 200:
                    break
                time.sleep(.1)
            check('restart recreates only default room', status == 200 and len(current) == 1 and current[0]['isDefault'])
            room_ids.clear()
            default_id = current[0]['id']
            status, created = request(base, 'POST', '/api/rooms', token, {'name': 'Benchmark after restart', 'maxPlayers': 10})
            check('can create room again after restart', status == 201)
            room_id, code = created['id'], created['roomCode']
            room_ids.append((token, room_id))
            extra_rooms = []
            for index, owner_token in enumerate((other, other, token)):
                status, extra = request(base, 'POST', '/api/rooms', owner_token, {'name': 'Extra test ' + str(index), 'maxPlayers': 10})
                check('allow created room ' + str(index + 2) + ' of four', status == 201)
                extra_rooms.append((owner_token, extra))
                room_ids.append((owner_token, extra['id']))
            overflow_owner = login('roomoverflow')
            status, error = request(base, 'POST', '/api/rooms', overflow_owner, {'name': 'Sixth total', 'maxPlayers': 10})
            check('sixth total room rejected by global room limit', status == 409 and error['error'] == 'RoomLimitReached')
            # Brief functional check only, not a multi-room performance certification.
            targets = [(token, default_id, room_code), (token, room_id, code)] + [
                (owner_token, extra['id'], extra['roomCode']) for owner_token, extra in extra_rooms]
            with ThreadPoolExecutor(max_workers=5) as executor:
                futures = [executor.submit(run_clients, 10, join_code, 6, 0, 'five-' + str(index), rid)
                           for index, (_, rid, join_code) in enumerate(targets)]
                full = False
                for _ in range(80):
                    left = request(base, 'GET', '/api/rooms', token)[1]
                    right = request(base, 'GET', '/api/rooms', other)[1]
                    live_rooms = {r['id']: r for r in left + right}
                    if len(live_rooms) == 5 and all(r['activePlayers'] == 10 for r in live_rooms.values()):
                        full = True; break
                    time.sleep(.05)
                check('API reports five simultaneous rooms with 10 active each', full)
                joined = [f.result() for f in futures]
                check('50 simultaneous sessions join and pong without client failures',
                      sum(r['joined'] for r in joined) == 50 and all(r['failed'] == 0 for r in joined))
            for owner_token, rid, _ in targets:
                wait_empty(owner_token, rid)
            for owner_token, extra in extra_rooms:
                check('delete extra empty test room', request(base, 'DELETE', '/api/rooms/' + extra['id'], owner_token)[0] == 204)
                room_ids.remove((owner_token, extra['id']))
        if not args.functional_only:
            def stage(n, seconds, label):
                # Independent sampling includes cgroup memory (not just process WorkingSet).
                report_path = out / (label + '.json')
                env = dict(os.environ, NET_ROOM_CODE=code)
                argv = ['dotnet', str(CLIENT), '--config', str(config), '--clients', str(n), '--name', 'Load',
                        '--duration', str(seconds), '--warmup', str(args.warmup), '--ping-rate', '30', '--quiet',
                        '--report', str(report_path), '--room-label', room_id]
                samples = []
                with (out / (label + '.log')).open('w', encoding='utf-8') as log:
                    proc = subprocess.Popen(argv, cwd=BACKEND, env=env, stdout=log, stderr=subprocess.STDOUT)
                    deadline = time.monotonic() + args.warmup + seconds + 40
                    try:
                        while proc.poll() is None:
                            if time.monotonic() > deadline:
                                raise TimeoutError('Client process exceeded stage deadline.')
                            # Docker Desktop may use cgroup v1, EC2 may use v2.
                            mem = int(dc('exec', container, 'sh', '-c',
                                         'if [ -f /sys/fs/cgroup/memory.current ]; then cat /sys/fs/cgroup/memory.current; '
                                         'else cat /sys/fs/cgroup/memory/memory.usage_in_bytes; fi'))
                            samples.append({'utc': dt.datetime.now(dt.timezone.utc).isoformat(), 'bytes': mem})
                            time.sleep(1)
                    finally:
                        if proc.poll() is None:
                            proc.kill(); proc.wait()
                data = json.loads(report_path.read_text(encoding='utf-8'))
                start, end = data['measurementUtc'], data['measurementEndUtc']
                def instant(raw):
                    return dt.datetime.fromisoformat(raw.replace('Z', '+00:00'))
                lo, hi = instant(start), instant(end)
                metrics = []
                metric_window = int(result['server']['configuration']['METRICS_INTERVAL_SECONDS'])
                for line in dc('logs', container).splitlines():
                    if not line.startswith('[metrics]'):
                        continue
                    values = dict(x.split('=', 1) for x in line.split()[1:] if '=' in x)
                    # Only complete server metric windows inside the client's measurement.
                    if lo + dt.timedelta(seconds=metric_window + .2) <= instant(values['utc']) <= hi:
                        metrics.append(values)
                memory = [s['bytes'] for s in samples if lo <= instant(s['utc']) <= hi]
                sent = sum(c['sent'] for c in data['clients'])
                lost = sum(c['timedOut'] + c['outstanding'] for c in data['clients'])
                rtts = [r for c in data['clients'] for r in c['rttMs']]
                hz = sent / data['measuredSeconds'] / n
                p95, p99 = quantile(rtts, .95), quantile(rtts, .99)
                tps = sum(float(m['ticksPerSecond']) for m in metrics) / len(metrics) if metrics else 0
                cpu = max((float(m['cpuOneCorePercent']) for m in metrics), default=999)
                dropped = sum(int(m['droppedTicks']) for m in metrics)
                ram = max(memory, default=limit) / limit * 100
                criteria = {'allJoined': data['joined'] == n, 'noUnexpectedDisconnect': data['failed'] == 0 and not data['cancelled'],
                            'pongTimeoutUnder1Percent': sent > 0 and lost / sent < .01,
                            'rttP95Under50Ms': p95 is not None and p95 <= 50,
                            'tick29to31Hz': 29 <= tps <= 31, 'noDroppedTick': dropped == 0,
                            'cpuOneCoreUnder85Percent': cpu <= 85, 'ramUnder80Percent': ram <= 80,
                            'telemetryAvailable': bool(metrics) and bool(memory)}
                valid = hz >= 30 * .95 and data['measuredSeconds'] >= seconds * .99
                status = 'INVALID_GENERATOR' if not valid else 'PASS' if all(criteria.values()) else 'FAIL'
                row = dict(label=label, clients=n, seconds=seconds, status=status, actualHz=hz, joined=data['joined'],
                           failed=data['failed'], sent=sent, lost=lost, timeoutPercent=100 * lost / max(sent, 1),
                           rttP95Ms=p95, rttP99Ms=p99, tickHz=tps, droppedTicks=dropped, cpuPeakPercent=cpu,
                           ramPeakPercent=ram, criteria=criteria, generatorCpuPercent=data['generatorCpuOneCorePercent'],
                           metrics=metrics, memorySamples=samples)
                stages.append(row)
                print(f'{label}: {status} joined={data["joined"]}/{n} rate={hz:.2f}Hz p95={p95} RAM={ram:.1f}% CPU={cpu:.1f}%', flush=True)
                wait_empty(token, room_id)
                return status
            best, failed_at, invalid = 0, None, False
            for n in levels:
                verdict = stage(n, args.seconds, f'level-{n}')
                if verdict != 'PASS':
                    invalid = verdict == 'INVALID_GENERATOR'; failed_at = n; break
                best = n
            if failed_at and not invalid:
                low, high = best, failed_at
                while high - low > 1:
                    n = (low + high) // 2
                    verdict = stage(n, args.seconds, f'refine-{n}')
                    if verdict == 'INVALID_GENERATOR':
                        invalid = True; break
                    if verdict == 'PASS':
                        low = n; best = n
                    else:
                        high = n
            confirmed = bool(best and not invalid and stage(best, args.confirm_seconds, f'confirm-{best}') == 'PASS')
            result['bestCandidate'] = best
            result['confirmed'] = confirmed
            result['conclusion'] = (f'Confirmed at least {best} sessions under this network-only test; no gameplay capacity claim.'
                                    if confirmed else 'No stable capacity confirmed; inspect failed criteria or generator validity.')
        else:
            result['conclusion'] = 'Functional checks only; no capacity measurement.'
    except BaseException as e:
        result['error'] = type(e).__name__ + ': ' + str(e)
        result['conclusion'] = 'Incomplete run. No capacity claim.'
        raise
    finally:
        cleanup_errors = []
        for owner_token, rid in room_ids:
            try:
                wait_empty(owner_token, rid)
                status, _ = request(base, 'DELETE', '/api/rooms/' + rid, owner_token)
                if status not in (204, 404):
                    cleanup_errors.append('Room cleanup HTTP ' + str(status))
            except Exception as e:
                cleanup_errors.append(type(e).__name__)
        if stack_started:
            try:
                logs = compose('logs', '--no-color', 'game-server', 'api')
                # No code/password/token should be logged; stop report delivery if regression occurs.
                if room_code in logs or control_key in logs or (room_id and code in logs):
                    cleanup_errors.append('Unexpected sensitive value in logs; logs withheld')
                else:
                    (out / 'server.log').write_text(logs, encoding='utf-8')
            except Exception as e:
                cleanup_errors.append('Collect logs: ' + type(e).__name__)
            try:
                compose('down', '--volumes', '--remove-orphans')
            except Exception as e:
                cleanup_errors.append('Remove isolated stack: ' + type(e).__name__)
        private_env.unlink(missing_ok=True)
        result['cleanupErrors'] = cleanup_errors
        (out / 'report.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        columns = ['label', 'clients', 'seconds', 'status', 'actualHz', 'joined', 'failed', 'sent', 'lost', 'timeoutPercent',
                   'rttP95Ms', 'rttP99Ms', 'tickHz', 'droppedTicks', 'cpuPeakPercent', 'ramPeakPercent', 'generatorCpuPercent']
        with (out / 'report.csv').open('w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=columns, extrasaction='ignore'); writer.writeheader(); writer.writerows(stages)
        lines = ['# Room network benchmark', '', result.get('conclusion', 'Incomplete'), '',
                 f'Run: `{run_id}`. Commit: `{result["gitCommit"]}`; actual source SHA256: `{result["backendSourceSha256"]}`.', '',
                 'GameServer: 256MiB, 30Hz fixed tick with NO gameplay. Client: 30 ping/s/session.',
                 'Local Docker Desktop results do not establish EC2 or Unity gameplay capacity.', '',
                 '| Sessions | Seconds | Verdict | Send Hz/client | RTT p95/p99 ms | Tick Hz | CPU peak % of core | RAM peak % |',
                 '| --- | --- | --- | --- | --- | --- | --- | --- |']
        failed_notes = []
        for s in stages:
            lines.append(f'| {s["clients"]} | {s["seconds"]} | {s["status"]} | {s["actualHz"]:.2f} | {s["rttP95Ms"]}/{s["rttP99Ms"]} | {s["tickHz"]:.2f} | {s["cpuPeakPercent"]:.2f} | {s["ramPeakPercent"]:.2f} |')
            failed = [k for k, v in s['criteria'].items() if not v]
            if failed:
                failed_notes.append(s['label'] + ': ' + ', '.join(failed))
        lines += ['', *failed_notes]
        lines += ['', 'CPU criterion uses the highest server metric window; memory uses sampled cgroup v2 memory.current or v1 memory.usage_in_bytes (includes container overhead/cache).',
                  'RTT uses nearest-rank percentiles over measured pongs; warm-up excluded and final pongs drained for up to 5.2s.',
                  'Generator below 28.5Hz/client marks the run INVALID; missing telemetry fails it.', '',
                  'Environment and per-client raw samples are in JSON; stage summary is in CSV.',
                  'Cleanup errors: ' + (', '.join(cleanup_errors) or 'none')]
        (out / 'report.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
        print('Report: ' + str(out / 'report.md'), flush=True)


if __name__ == '__main__':
    main()
