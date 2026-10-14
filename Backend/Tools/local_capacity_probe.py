"""Local-only capacity experiment. Product policy and running dev stack are untouched.

Creates an allowlisted GameServer source snapshot under ignored out/, widens only
its copied RoomLimits, and starts a disposable 256MiB container. No API/MongoDB,
JWT, AWS, .env from the project or remote Docker contexts are used. Provisioning
uses the existing authenticated control endpoint on a random loopback port.
"""
import argparse
import csv
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import time

from room_benchmark import BACKEND, CLIENT, command, port, quantile, request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--single-levels', default='10,30,50,100,150,200,256')
    parser.add_argument('--room-levels', default='3,5,10,15,20,25')
    parser.add_argument('--skip-single', action='store_true')
    parser.add_argument('--skip-multi', action='store_true')
    parser.add_argument('--confirm-only', action='store_true', help='Run only long confirmation at the supplied levels; no search.')
    parser.add_argument('--warmup', type=int, default=15)
    parser.add_argument('--seconds', type=int, default=60)
    parser.add_argument('--confirm-seconds', type=int, default=180)
    args = parser.parse_args()
    single = [int(x) for x in args.single_levels.split(',')]
    multi = [int(x) for x in args.room_levels.split(',')]
    if args.skip_single and args.skip_multi:
        parser.error('Select at least one measurement family.')
    if args.confirm_only and ((not args.skip_single and len(single) != 1) or (not args.skip_multi and len(multi) != 1)):
        parser.error('--confirm-only requires one explicit level for each selected family.')
    for levels, maximum in ((single, 256), (multi, 25)):
        if not levels or levels != sorted(set(levels)) or min(levels) < 1 or max(levels) > maximum:
            parser.error('Increasing positive levels required; single<=256, room count<=25.')
    if not 0 <= args.warmup <= 300 or not 1 <= args.seconds <= 3600 or not 1 <= args.confirm_seconds <= 3600:
        parser.error('Invalid durations.')
    run_id = 'capacity-probe-' + dt.datetime.now(dt.timezone.utc).strftime('%Y%m%d-%H%M%S') + '-' + secrets.token_hex(3)
    out = BACKEND / 'out/capacity-probe' / run_id
    out.mkdir(parents=True)
    docker = ['docker', '--context', 'desktop-linux']
    env = {k: v for k, v in os.environ.items() if k not in ('DOCKER_HOST', 'DOCKER_CONTEXT')}
    def dc(*parts):
        return command(docker + list(parts), env=env, stderr=subprocess.STDOUT)
    def compose(*parts):
        return dc('compose', '-p', run_id, '-f', str(out / 'compose.json'), *parts)
    def utc(raw):
        return dt.datetime.fromisoformat(raw.replace('Z', '+00:00'))
    def dev_state():
        present = set(dc('ps', '-a', '--format', '{{.Names}}').splitlines())
        watched = [n for n in ('backend-api-1', 'backend-game-server-1', 'backend-mongo-1') if n in present]
        if not watched:
            return []
        containers = json.loads(dc('inspect', *watched))
        return [{k: c[k] for k in ('Id', 'Name', 'Image')} for c in containers]
    endpoint = json.loads(dc('context', 'inspect', 'desktop-linux'))[0]['Endpoints']['docker']['Host']
    if not endpoint.startswith(('npipe://', 'unix://')):
        raise RuntimeError('This experiment only supports a local Docker socket, never SSH/TCP contexts.')
    result = dict(runId=run_id, scope='UDP ping/pong only, empty gameplay tick; local source-snapshot experiment',
                  parameters=vars(args), stages=[], families={}, cleanupErrors=[])
    result['dockerEndpoint'] = endpoint
    result['gitCommit'] = command(['git', 'rev-parse', 'HEAD'], cwd=BACKEND)
    result['devBefore'] = dev_state()
    info = json.loads(dc('info', '--format', '{{json .}}'))
    result['docker'] = {k: info.get(k) for k in ('NCPU', 'MemTotal', 'KernelVersion', 'OperatingSystem', 'CgroupVersion', 'ServerVersion')}
    result['host'] = json.loads(command(['powershell', '-NoProfile', '-Command',
        '$c=Get-CimInstance Win32_Processor; $m=Get-CimInstance Win32_ComputerSystem; '
        '@{cpu=($c.Name -join "; "); ramBytes=$m.TotalPhysicalMemory; logicalCpu=$m.NumberOfLogicalProcessors} | ConvertTo-Json -Compress']))
    production_policy = BACKEND / 'Shared/RoomContracts.cs'
    production_bytes = production_policy.read_bytes()
    result['productionPolicySha256'] = hashlib.sha256(production_bytes).hexdigest()
    snapshot = out / 'source'
    snapshot.mkdir()
    for folder in ('GameServer', 'Shared'):
        shutil.copytree(BACKEND / folder, snapshot / folder, ignore=shutil.ignore_patterns('bin', 'obj'))
    for name in ('global.json', 'Directory.Build.props', '.dockerignore'):
        shutil.copyfile(BACKEND / name, snapshot / name)
    copied_policy = snapshot / 'Shared/RoomContracts.cs'
    copied = copied_policy.read_text(encoding='utf-8')
    overrides = dict(PlayersPerRoom=256, TotalPlayers=256, Rooms=32, RoomsPerUser=31)
    for name, value in overrides.items():
        copied, count = re.subn(r'(public const int ' + name + r' = )\d+;', lambda m: m[1] + str(value) + ';', copied)
        if count != 1:
            raise RuntimeError('Policy snapshot did not match expected source; no product change made.')
    copied_policy.write_text(copied, encoding='utf-8')
    result['snapshotOverrides'] = overrides
    hashes = {p.relative_to(snapshot).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
              for p in sorted(snapshot.rglob('*')) if p.is_file()}
    result['snapshotFiles'] = hashes
    result['snapshotSha256'] = hashlib.sha256(json.dumps(hashes, sort_keys=True).encode()).hexdigest()
    default_code, control_key = secrets.token_hex(16), secrets.token_hex(32)
    private = out / 'private.env'
    private.write_text(f'ROOM_CODE={default_code}\nROOM_CONTROL_TOKEN={control_key}\n', encoding='utf-8')
    tcp, udp = port(), port(__import__('socket').SOCK_DGRAM)
    base = f'http://127.0.0.1:{tcp}'
    image_tag = run_id + ':local-only'
    spec = {'services': {'game': {'image': image_tag,
            'build': {'context': str(snapshot), 'dockerfile': 'GameServer/Dockerfile'},
            'env_file': [str(private)], 'mem_limit': '256m', 'memswap_limit': '256m',
            'ports': [f'127.0.0.1:{udp}:7777/udp', f'127.0.0.1:{tcp}:7780/tcp'],
            'environment': {'GAME_BIND_ADDRESS': '0.0.0.0', 'ROOM_CONTROL_BIND_ADDRESS': '0.0.0.0',
                            'GAME_PORT': '7777', 'AUTH_MODE': 'RoomCode', 'MAX_PLAYERS': '10',
                            'TICK_RATE': '30', 'METRICS_INTERVAL_SECONDS': '1', 'ROOM_EMPTY_TTL_SECONDS': '3600'}}}}
    (out / 'compose.json').write_text(json.dumps(spec, indent=2), encoding='utf-8')
    config = out / 'client.json'
    config.write_text(json.dumps(dict(mode='Local', gameServerHost='127.0.0.1', gameServerPort=udp,
                                     authMode='RoomCode', protocolVersion=1, connectTimeoutMs=10000)), encoding='utf-8')
    def rpc(method, suffix='', body=None):
        return request(base, method, '/internal/rooms' + suffix, body=body, key=control_key)
    def listed():
        status, rooms = rpc('GET', '?ownerId=probe')
        if status != 200:
            raise RuntimeError('Control unavailable: ' + str(status))
        return rooms
    def empty_and_delete():
        for _ in range(60):
            rooms = listed()
            if all(r['activePlayers'] + r['pendingPlayers'] == 0 for r in rooms):
                break
            time.sleep(.2)
        else:
            raise RuntimeError('Clients did not release slots.')
        for r in rooms:
            if not r['isDefault'] and rpc('DELETE', '/' + r['id'] + '?ownerId=probe')[0] != 204:
                raise RuntimeError('Could not delete probe room.')
    container = None
    started = False
    try:
        subprocess.run(['dotnet', 'build', 'NetSmokeClient/NetSmokeClient.csproj', '-c', 'Release'], cwd=BACKEND, check=True)
        result['clientDllSha256'] = hashlib.sha256(CLIENT.read_bytes()).hexdigest()
        print('Building disposable local-only GameServer snapshot...', flush=True)
        started = True
        with (out / 'build.log').open('w', encoding='utf-8') as log:
            subprocess.run(docker + ['compose', '-p', run_id, '-f', str(out / 'compose.json'), 'up', '-d', '--build'],
                           env=env, stdout=log, stderr=subprocess.STDOUT, check=True)
        container = compose('ps', '-q', 'game')
        inspect = json.loads(dc('inspect', container))[0]
        assert inspect['HostConfig']['Memory'] == 268435456
        result['server'] = dict(memoryLimitBytes=268435456, memorySwapLimitBytes=inspect['HostConfig']['MemorySwap'],
                                imageId=inspect['Image'], cpuQuota=inspect['HostConfig']['NanoCpus'])
        for _ in range(80):
            try:
                if listed():
                    break
            except Exception:
                pass
            time.sleep(.2)
        else:
            raise RuntimeError('Probe control startup timeout.')
        def stage(family, level, seconds, suffix):
            empty_and_delete()
            plan = []
            default = listed()[0]
            targets = [level] if family == 'single' else [10] * level
            for index, count in enumerate(targets):
                if family == 'multi' and index == 0:
                    plan.append(dict(roomId=default['id'], roomCode=default_code, clients=count))
                else:
                    status, room = rpc('POST', body=dict(ownerId='probe', name='Load room', maxPlayers=256 if family == 'single' else 10))
                    if status != 201:
                        raise RuntimeError('Probe room creation failed: ' + str(status))
                    plan.append(dict(roomId=room['id'], roomCode=room['roomCode'], clients=count))
            label = f'{family}-{level}-{suffix}'
            plan_file, report_file = out / (label + '.private.json'), out / (label + '.json')
            plan_file.write_text(json.dumps(plan), encoding='utf-8')
            argv = ['dotnet', str(CLIENT), '--config', str(config), '--room-plan', str(plan_file), '--name', 'Capacity',
                    '--duration', str(seconds), '--warmup', str(args.warmup), '--ping-rate', '30', '--quiet', '--report', str(report_file)]
            samples = []
            try:
                with (out / (label + '.log')).open('w', encoding='utf-8') as log:
                    proc = subprocess.Popen(argv, cwd=BACKEND, env=env, stdout=log, stderr=subprocess.STDOUT)
                    deadline = time.monotonic() + args.warmup + seconds + 45
                    try:
                        while proc.poll() is None:
                            if time.monotonic() > deadline:
                                raise TimeoutError('Generator exceeded stage deadline.')
                            mem = int(dc('exec', container, 'sh', '-c',
                                'if [ -f /sys/fs/cgroup/memory.current ]; then cat /sys/fs/cgroup/memory.current; '
                                'else cat /sys/fs/cgroup/memory/memory.usage_in_bytes; fi'))
                            samples.append(dict(utc=dt.datetime.now(dt.timezone.utc).isoformat(), bytes=mem))
                            time.sleep(1)
                    finally:
                        if proc.poll() is None:
                            proc.kill(); proc.wait()
            finally:
                plan_file.unlink(missing_ok=True)
            data = json.loads(report_file.read_text(encoding='utf-8'))
            # A rejected/disconnected batch can finish before warm-up. Keep its
            # failure evidence instead of crashing while parsing a null window.
            measured = data['measurementUtc'] is not None and data['measurementEndUtc'] is not None
            lo = utc(data['measurementUtc']) if measured else dt.datetime.max.replace(tzinfo=dt.timezone.utc)
            hi = utc(data['measurementEndUtc']) if measured else lo
            metrics = []
            for line in dc('logs', container).splitlines():
                if line.startswith('[metrics]'):
                    m = dict(x.split('=', 1) for x in line.split()[1:] if '=' in x)
                    if measured and lo + dt.timedelta(seconds=1.2) <= utc(m['utc']) <= hi:
                        metrics.append(m)
            memory = [s['bytes'] for s in samples if lo <= utc(s['utc']) <= hi]
            sent = sum(c['sent'] for c in data['clients'])
            lost = sum(c['timedOut'] + c['outstanding'] for c in data['clients'])
            total = sum(targets)
            hz = sent / max(data['measuredSeconds'], .001) / total
            rtts = [x for c in data['clients'] for x in c['rttMs']]
            p95, p99 = quantile(rtts, .95), quantile(rtts, .99)
            per_room = []
            for room in plan:
                clients = [c for c in data['clients'] if c['room'] == room['roomId']]
                rr = [x for c in clients for x in c['rttMs']]
                per_room.append(dict(roomId=room['roomId'], requested=room['clients'], joined=sum(c['joined'] for c in clients),
                                     failed=sum(c['failed'] for c in clients), sent=sum(c['sent'] for c in clients),
                                     pongs=sum(c['pongs'] for c in clients), rttP95Ms=quantile(rr, .95), rttP99Ms=quantile(rr, .99)))
            cpu = max((float(m['cpuOneCorePercent']) for m in metrics), default=999)
            ticks = sum(float(m['ticksPerSecond']) for m in metrics) / len(metrics) if metrics else 0
            dropped = sum(int(m['droppedTicks']) for m in metrics)
            ram = max(memory, default=268435456) / 268435456 * 100
            criteria = dict(allJoined=data['joined'] == total, noUnexpectedDisconnect=data['failed'] == 0 and not data['cancelled'],
                timeoutUnder1Percent=sent > 0 and lost / sent < .01,
                allRoomP95Under50Ms=all(r['rttP95Ms'] is not None and r['rttP95Ms'] <= 50 for r in per_room),
                tick29to31Hz=29 <= ticks <= 31, noDroppedTick=dropped == 0, cpuUnder85Percent=cpu <= 85,
                ramUnder80Percent=ram <= 80, telemetryAvailable=bool(metrics) and bool(memory))
            valid = hz >= 28.5 and data['measuredSeconds'] >= seconds * .99
            # Rejection/disconnect is a functional failure, not proof that the
            # generator is too slow. Only label intact joined batches invalid.
            intact = criteria['allJoined'] and criteria['noUnexpectedDisconnect']
            verdict = 'INVALID_GENERATOR' if intact and not valid else 'PASS' if valid and all(criteria.values()) else 'FAIL'
            row = dict(label=label, family=family, level=level, activeRooms=len(plan), sessions=total, seconds=seconds,
                status=verdict, actualHz=hz, sent=sent, lost=lost, timeoutPercent=100*lost/max(sent,1), joined=data['joined'],
                failed=data['failed'], rttP95Ms=p95, rttP99Ms=p99, worstRoomP95Ms=max((r['rttP95Ms'] or 0 for r in per_room),default=0),
                tickHz=ticks, droppedTicks=dropped, cpuPeakPercent=cpu, ramPeakPercent=ram,
                generatorCpuPercent=data['generatorCpuOneCorePercent'], perRoom=per_room, criteria=criteria, metrics=metrics, memorySamples=samples)
            result['stages'].append(row)
            (out / 'progress.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
            print(f'{label}: {verdict} rooms={len(plan)} sessions={total} joined={data["joined"]} rate={hz:.2f}Hz '
                  f'p95={p95} worstRoom={row["worstRoomP95Ms"]:.2f} CPU={cpu:.1f}% RAM={ram:.1f}%', flush=True)
            return row
        def family_run(family, levels):
            if args.confirm_only:
                rows = [stage(family, level, args.confirm_seconds, 'confirm-only') for level in levels]
                passed = [r['level'] for r in rows if r['status'] == 'PASS']
                best = max(passed, default=0)
                result['families'][family] = dict(bestCandidate=best, upperTested=max(levels),
                    generatorLimited=any(r['status'] == 'INVALID_GENERATOR' for r in rows),
                    confirmed=best > 0, sessions=best if family == 'single' else best*10,
                    reachedTestBound=False, confirmationOnly=True)
                return
            best, upper, invalid = 0, None, False
            for level in levels:
                row = stage(family, level, args.seconds, 'measure')
                if row['status'] == 'FAIL':
                    row = stage(family, level, args.seconds, 'repeat')
                if row['status'] != 'PASS':
                    upper = level; invalid = row['status'] == 'INVALID_GENERATOR'; break
                best = level
            # Refine to five sessions (single) or one room (multi); avoid pretending RTT noise yields an exact ceiling.
            step = 5 if family == 'single' else 1
            if upper and not invalid:
                low, high = best, upper
                while high - low > step:
                    mid = ((low + high) // (2 * step)) * step
                    if mid <= low: mid = low + step
                    row = stage(family, mid, args.seconds, 'refine')
                    if row['status'] == 'INVALID_GENERATOR':
                        invalid = True; break
                    if row['status'] == 'PASS': best = low = mid
                    else: high = mid
            confirmed = False
            if best:
                confirmed = stage(family, best, args.confirm_seconds, 'confirm')['status'] == 'PASS'
            result['families'][family] = dict(bestCandidate=best, upperTested=upper, generatorLimited=invalid,
                confirmed=confirmed, sessions=best if family == 'single' else best*10,
                reachedTestBound=upper is None and best == levels[-1])
        if not args.skip_single:
            family_run('single', single)
        if not args.skip_multi:
            family_run('multi', multi)
    except BaseException as e:
        result['error'] = type(e).__name__ + ': ' + str(e)
        raise
    finally:
        if started:
            if container:
                try:
                    empty_and_delete()
                    logs = dc('logs', container)
                    if default_code in logs or control_key in logs:
                        result['cleanupErrors'].append('Sensitive log withheld')
                    else:
                        (out / 'server.log').write_text(logs, encoding='utf-8')
                except Exception as e:
                    result['cleanupErrors'].append('Collect/room cleanup: ' + type(e).__name__)
            try: compose('down', '--remove-orphans')
            except Exception as e: result['cleanupErrors'].append('Container cleanup: ' + type(e).__name__)
            try: dc('image', 'rm', image_tag)
            except Exception as e: result['cleanupErrors'].append('Image cleanup: ' + type(e).__name__)
        private.unlink(missing_ok=True)
        for p in out.glob('*.private.json'): p.unlink(missing_ok=True)
        result['productionPolicyUnchanged'] = production_policy.read_bytes() == production_bytes
        result['devAfter'] = dev_state()
        result['devContainersUnchanged'] = result['devBefore'] == result['devAfter']
        (out / 'report.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        columns = ['label','family','activeRooms','sessions','seconds','status','actualHz','joined','failed','sent','lost',
                   'timeoutPercent','rttP95Ms','rttP99Ms','worstRoomP95Ms','tickHz','droppedTicks','cpuPeakPercent','ramPeakPercent','generatorCpuPercent']
        with (out / 'report.csv').open('w', newline='', encoding='utf-8') as f:
            writer=csv.DictWriter(f,fieldnames=columns,extrasaction='ignore');writer.writeheader();writer.writerows(result['stages'])
        lines=['# Local source-snapshot capacity probe','', 'Network ping/pong only; no gameplay. Product policy untouched.','',
               '| Case | Rooms | Sessions | Seconds | Result | Send Hz | RTT p95/p99 | Worst room p95 | CPU core % | RAM % |',
               '| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |']
        for s in result['stages']:
            lines.append(f'| {s["label"]} | {s["activeRooms"]} | {s["sessions"]} | {s["seconds"]} | {s["status"]} | {s["actualHz"]:.2f} | '
                         f'{s["rttP95Ms"]}/{s["rttP99Ms"]} | {s["worstRoomP95Ms"]:.2f} | {s["cpuPeakPercent"]:.2f} | {s["ramPeakPercent"]:.2f} |')
        lines += ['', 'Family outcomes: `' + json.dumps(result['families']) + '`', '',
                  'Details, raw client/room data, criteria, metrics and environment are in JSON. Summary is CSV.',
                  'Provisioning bypassed public API/account: tests GameServer/control/UDP only.',
                  'Cleanup errors: ' + json.dumps(result['cleanupErrors'])]
        (out / 'report.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
        print('Report: '+str(out/'report.md'),flush=True)


if __name__ == '__main__':
    main()
