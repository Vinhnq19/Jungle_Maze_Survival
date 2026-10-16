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
import ssl
import http.client
from http.client import HTTPSConnection, HTTPException
import base64
import shlex
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


def ssh_script(alias, script, seconds=45):
    # Binary stdin preserves LF on Windows. text=True translates LF to CRLF,
    # which turns a Bash action like default-code into an unknown action ending CR.
    proc = subprocess.run(['ssh','-o','BatchMode=yes','-o','ConnectTimeout=30',alias,'sudo bash -s'],
                          input=script.replace('\r\n','\n').encode('utf-8'), capture_output=True, timeout=seconds)
    if proc.returncode:
        raise RuntimeError('Administrative SSH command failed, exit '+str(proc.returncode))
    return proc.stdout.decode('utf-8').strip()


def evaluate_aws_stage(data, snapshot, baseline_p95, expected, seconds):
    """Pure evaluation: every room/client matters; Internet RTT uses measured baseline."""
    instant = lambda s: dt.datetime.fromisoformat(s.replace('Z', '+00:00'))
    lo, hi = instant(data['measurementUtc']), instant(data['measurementEndUtc'])
    metrics = []
    for line in snapshot['metrics']:
        values = dict(v.split('=', 1) for v in line.split()[1:] if '=' in v)
        if lo + dt.timedelta(seconds=5.2) <= instant(values['utc']) <= hi:
            metrics.append(values)
    memory = [int(l.split(',')[1]) for l in snapshot['memory'] if lo <= instant(l.split(',')[0]) <= hi]
    rooms = {}
    for c in data['clients']:
        rooms.setdefault(c['room'], []).append(c)
    rtt = [r for c in data['clients'] for r in c['rttMs']]
    per_room = {rid: {'clients': len(cs), 'joined': sum(bool(c.get('joined')) for c in cs),
                     'sent': sum(c['sent'] for c in cs), 'pongs': sum(len(c['rttMs']) for c in cs),
                     'lost': sum(c['timedOut']+c['outstanding'] for c in cs),
                     'actualHz': sum(c['sent'] for c in cs)/max(data['measuredSeconds']*len(cs),1),
                     'p95Ms': quantile([r for c in cs for r in c['rttMs']], .95),
                     'p99Ms': quantile([r for c in cs for r in c['rttMs']], .99)} for rid, cs in rooms.items()}
    sent = sum(c['sent'] for c in data['clients'])
    lost = sum(c['timedOut'] + c['outstanding'] for c in data['clients'])
    hz = min((c['sent'] / data['measuredSeconds'] for c in data['clients']), default=0)
    tick = sum(float(m['ticksPerSecond']) for m in metrics) / len(metrics) if metrics else 0
    cpu = max((float(m['cpuOneCorePercent']) for m in metrics), default=999)
    limit = snapshot['memoryLimitBytes']
    ram = max(memory, default=limit) / limit * 100
    criteria = dict(allJoined=data['joined'] == expected, noUnexpectedDisconnect=data['failed'] == 0 and not data['cancelled'],
                    pongTimeoutUnder1Percent=sent > 0 and lost / sent < .01,
                    perRoomRttWithinBaseline=all(r['p95Ms'] is not None and r['p95Ms'] <= baseline_p95 + 50 for r in per_room.values()) and bool(per_room),
                    tick29to31Hz=29 <= tick <= 31,
                    noDroppedTick=bool(metrics) and all(int(m['droppedTicks']) == 0 for m in metrics),
                    cpuOneCoreUnder85Percent=cpu <= 85, ramUnder80Percent=ram <= 80,
                    telemetryAvailable=bool(metrics) and bool(memory), noOomOrRestart=not snapshot['oomKilled'] and snapshot['restartCount'] == 0 and snapshot['running'])
    valid = len(data['clients']) == expected and hz >= 28.5 and data['measuredSeconds'] >= seconds * .99
    return dict(clients=expected, seconds=seconds, status='INVALID_GENERATOR' if not valid else 'PASS' if all(criteria.values()) else 'FAIL',
                joined=data['joined'], failed=data['failed'], sent=sent, lost=lost, actualHz=sent/max(data['measuredSeconds']*expected, 1),
                minimumClientHz=hz, timeoutPercent=100*lost/max(sent, 1), rttP95Ms=quantile(rtt,.95), rttP99Ms=quantile(rtt,.99),
                tickHz=tick, droppedTicks=sum(int(m['droppedTicks']) for m in metrics),
                cpuPeakPercent=cpu, ramPeakPercent=ram, criteria=criteria, rooms=per_room, metrics=metrics,
                generatorCpuPercent=data['generatorCpuOneCorePercent'])


def aws_session(args):
    """AWS lifecycle belongs to test-aws-rooms.ps1; this process owns only fixture rooms/clients."""
    import ipaddress
    from urllib.parse import urlsplit
    ipaddress.IPv4Address(args.connect_ip)
    host = urlsplit(args.api_url).hostname
    if urlsplit(args.api_url).scheme != 'https' or host != 'junglemaze.duckdns.org':
        raise ValueError('AWS session requires the configured HTTPS hostname.')
    deadline = dt.datetime.fromisoformat(os.environ['AWS_ROOM_DEADLINE_UTC'].replace('Z', '+00:00'))
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=False)
    on_server=bool(args.generator_on_server)
    result = dict(scope=('AWS co-located UDP generator via SSM; NOT Internet RTT' if on_server else 'AWS Internet UDP ping/pong 30Hz')+'; no gameplay',
                  generatorOnServer=on_server, generatedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),
                  checks=[], stages=[], cleanupErrors=[], confirmed=False)
    result['gitCommit']=command(['git','rev-parse','HEAD'],cwd=BACKEND)
    result['generatorHost']=dict(platform=platform.platform(),processor=platform.processor(),logicalCpu=os.cpu_count())
    source_hash=hashlib.sha256()
    for p in sorted(BACKEND.rglob('*')):
        if p.is_file() and p.suffix in ('.cs','.csproj','.yaml','.py','.sh','.ps1') and not any(x in p.relative_to(BACKEND).parts for x in ('out','bin','obj','__pycache__')):
            source_hash.update(p.relative_to(BACKEND).as_posix().encode());source_hash.update(p.read_bytes())
    result['backendSourceSha256']=source_hash.hexdigest()
    checks, stages = result['checks'], result['stages']
    tokens, owned, procs = [], [], []
    short_enabled = False
    private_plan = out / 'private-room-plan.json'
    config = out / 'client.json'
    config.write_text(json.dumps(dict(mode='Online', gameServerHost=args.connect_ip, gameServerPort=7777,
                                      authMode='RoomCode', protocolVersion=1, connectTimeoutMs=5000)), encoding='utf-8')
    # HTTPS SNI/certificate verification stays on hostname; TCP uses current public IP.
    tunnel=None
    tls_port=port() if on_server else 443
    class PinnedHTTPS(HTTPSConnection):
        def connect(self):
            sock = socket.create_connection(('127.0.0.1' if on_server else args.connect_ip, tls_port), self.timeout)
            self.sock = self._context.wrap_socket(sock, server_hostname=self.host)
    def http(method, path, token=None, body=None):
        conn = PinnedHTTPS(host, timeout=10, context=ssl.create_default_context())
        headers = {'Content-Type':'application/json'}
        if token is not None: headers['Authorization'] = 'Bearer ' + token
        try:
            conn.request(method, path, body=json.dumps(body) if body is not None else None, headers=headers)
            res = conn.getresponse(); raw=res.read()
            return res.status, json.loads(raw) if raw and 'json' in res.getheader('Content-Type','') else None
        finally: conn.close()
    def remote(action):
        if action not in ('short-jwt','restore-jwt','default-code','prepare','snapshot','cleanup'): raise ValueError(action)
        return command(['ssh','-o','BatchMode=yes','-o','ConnectTimeout=15', args.ssh_alias,
                        'sudo bash /opt/junglemaze/current/scripts/room-test.sh ' + action], timeout=115, stderr=subprocess.DEVNULL)
    def budget(seconds):
        if dt.datetime.now(dt.timezone.utc) + dt.timedelta(seconds=seconds) >= deadline:
            raise TimeoutError('Stage would exceed session deadline; reserving cleanup/Stop time.')
    def check(name, ok, status=None):
        checks.append(dict(name=name, passed=bool(ok), httpStatus=status))
        print(('PASS ' if ok else 'FAIL ') + name, flush=True)
        if not ok: raise AssertionError(name)
    def post(token, name='AwsRoom', **extra):
        status, room = http('POST','/api/rooms',token,dict(name=name,maxPlayers=10,**extra))
        if status == 201: owned.append((token, room['id']))
        return status, room
    def delete(token, rid):
        status,_=http('DELETE','/api/rooms/'+rid,token)
        # The same owner may delete using a newly issued JWT. Track cleanup by room ID.
        if status in (204,404): owned[:]=[(t,r) for t,r in owned if r!=rid]
        return status
    def empty(token, rid):
        for _ in range(30):
            status, rooms=http('GET','/api/rooms',token)
            if status==200 and all(r['activePlayers']+r['pendingPlayers']==0 for r in rooms if r['id']==rid): return
            time.sleep(.2)
        raise RuntimeError('Room slots not released.')
    def clients(targets, seconds, warmup, label, during=None):
        budget(seconds+warmup+35)
        if on_server:
            # Private plan travels on SSH stdin, never in argv/logs. Only scrubbed report returns.
            job=dict(targets=targets,seconds=seconds,warmup=warmup)
            runner=r"""
import json,sys,subprocess,pathlib,tempfile,shutil
job=json.load(sys.stdin)
folder=pathlib.Path(tempfile.mkdtemp(prefix='junglemaze-bench-',dir='/run'))
try:
 (folder/'plan.json').write_text(json.dumps(job['targets']))
 (folder/'client.json').write_text(json.dumps(dict(mode='Local',gameServerHost='127.0.0.1',gameServerPort=7777,authMode='RoomCode',protocolVersion=1,connectTimeoutMs=5000)))
 env=dict(l.split('=',1) for l in pathlib.Path('/opt/junglemaze/current/release.env').read_text().splitlines() if '=' in l)
 cmd=['docker','run','--rm','--name','junglemaze-bench-client','--network','host','--memory','256m','--memory-swap','256m','--cpus','0.75',
      '--read-only','--tmpfs','/tmp:rw,nosuid,size=32m','-v','/opt/junglemaze/test-client:/client:ro','-v',str(folder)+':/job',
      '--entrypoint','dotnet',env['API_IMAGE'],'/client/NetSmokeClient.dll','--config','/job/client.json','--room-plan','/job/plan.json',
      '--name','AwsRoom','--duration',str(job['seconds']),'--warmup',str(job['warmup']),'--ping-rate','30','--quiet','--report','/job/report.json']
 subprocess.run(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=job['seconds']+job['warmup']+25)
 print((folder/'report.json').read_text())
finally:
 subprocess.run(['docker','rm','-f','junglemaze-bench-client'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 shutil.rmtree(folder)
"""
            def execute():
                p=subprocess.Popen(['ssh','-o','BatchMode=yes','-o','ConnectTimeout=15',args.ssh_alias,
                    'sudo python3 -c '+shlex.quote(runner)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL)
                procs.append(p)
                try:
                    raw,_=p.communicate(json.dumps(job).encode(),timeout=seconds+warmup+65)
                    if p.returncode: raise RuntimeError('Remote client failed; no secret output published.')
                    return json.loads(raw)
                finally:
                    if p.poll() is None:p.kill();p.wait()
            with ThreadPoolExecutor(max_workers=1) as executor:
                future=executor.submit(execute)
                if during:during()
                data=future.result()
            (out/(label+'.json')).write_text(json.dumps(data,indent=2),encoding='utf-8')
            return data
        private_plan.write_text(json.dumps(targets),encoding='utf-8')
        argv=['dotnet',str(CLIENT),'--config',str(config),'--room-plan',str(private_plan),'--name','AwsRoom',
              '--duration',str(seconds),'--warmup',str(warmup),'--ping-rate','30','--quiet','--report',str(out/(label+'.json'))]
        with (out/(label+'.log')).open('w',encoding='utf-8') as log:
            p=subprocess.Popen(argv,cwd=BACKEND,stdout=log,stderr=subprocess.STDOUT);procs.append(p)
            try:
                if during: during()
                p.wait(timeout=seconds+warmup+25)
            finally:
                if p.poll() is None: p.kill();p.wait()
                private_plan.unlink(missing_ok=True)
        return json.loads((out/(label+'.json')).read_text(encoding='utf-8'))
    try:
        if on_server:
            tunnel=subprocess.Popen(['ssh','-N','-o','BatchMode=yes','-o','ConnectTimeout=15','-o','ExitOnForwardFailure=yes',
                                     '-L',f'127.0.0.1:{tls_port}:127.0.0.1:443',args.ssh_alias],
                                    stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            for _ in range(150):
                if tunnel.poll() is not None: raise RuntimeError('SSM HTTPS tunnel exited before becoming ready.')
                try:
                    with socket.create_connection(('127.0.0.1',tls_port),.3):break
                except OSError:time.sleep(.2)
            else: raise RuntimeError('SSM HTTPS tunnel not ready.')
        check('HTTPS certificate and DB health',http('GET','/health')[0]==200)
        # Reuse private fixture credentials prepared before EC2 starts; never copy to evidence.
        fixtures=json.loads(Path(os.environ['AWS_ROOM_FIXTURES']).read_text(encoding='utf-8-sig'))
        for suffix,creds in zip('ABC', fixtures):
            registration=http('POST','/api/auth/register',body=creds)[0]
            check(('persisted fixture account ' if args.reconnect_only else 'register/reuse test user ')+suffix,
                  registration==409 if args.reconnect_only else registration in (201,409),registration)
            status, login=http('POST','/api/auth/login',body=creds)
            check('login test user '+suffix,status==200)
            tokens.append(login['accessToken'])
            if suffix=='A': credentials_a=creds
        a,b,c=tokens
        status, initial=http('GET','/api/rooms',a)
        check('exclusive clean test session (only default room)',status==200 and len(initial)==1 and initial[0]['isDefault'])
        default=initial[0];check('default has ten slots and hides code',default['maxPlayers']==10 and not default.get('roomCode'))
        if getattr(args,'dual_aws',False):
            if on_server:raise ValueError('Dual transport acceptance requires a physical PC.')
            result['scope']='Real PC -> EC2 HTTPS/WSS; UDP/Auto inspected separately; no gameplay'
            def dual_client(room,count,seconds,warmup,label,transport='Wss',during=None,local=False):
                budget(seconds+warmup+35)
                cfg=out/(label+'-config.json')
                cfg.write_text(json.dumps(dict(mode='Local' if local else 'Online',apiBaseUrl=args.api_url,gameServerHost='127.0.0.1' if local else args.connect_ip,
                    gameServerPort=local_udp if local else 7777,authMode='RoomCode',protocolVersion=2,connectTimeoutMs=5000,
                    transport=transport,webSocketUrl=f'ws://127.0.0.1:{local_ws}/game' if local else 'wss://'+host+'/game',
                    webSocketConnectIp=None if local else args.connect_ip)),encoding='utf-8')
                env={k:v for k,v in os.environ.items() if k not in ('NET_TRANSPORT','NET_WS_URL','NET_WS_CONNECT_IP','NET_GAME_SERVER_HOST','NET_GAME_SERVER_PORT','NET_MODE','NET_ACCESS_TOKEN')}
                env['NET_ROOM_CODE']=room['roomCode']
                argv=['dotnet',str(CLIENT),'--config',str(cfg),'--clients',str(count),'--name','AwsDual','--duration',str(seconds),
                    '--warmup',str(warmup),'--ping-rate','30','--quiet','--room-label',room['id'],'--report',str(out/(label+'.json'))]
                with (out/(label+'.log')).open('w',encoding='utf-8') as log:
                    p=subprocess.Popen(argv,env=env,cwd=BACKEND,stdout=log,stderr=subprocess.STDOUT);procs.append(p)
                    try:
                        if during:during()
                        p.wait(timeout=seconds+warmup+30)
                    finally:
                        if p.poll() is None:p.kill();p.wait()
                return json.loads((out/(label+'.json')).read_text(encoding='utf-8'))
            for name,jwt in [('missing',None),('malformed','bad-token'),('tampered',a.rsplit('.',1)[0]+'.'+'A'*43)]:
                check(name+' JWT cannot create room',http('POST','/api/rooms',jwt,dict(name='Unauthorized',maxPlayers=10))[0]==401)
            status,one=post(a,'DualPC');check('valid JWT creates ten-slot/six-character room',status==201 and bool(re.fullmatch(r'[A-HJ-NP-Z2-9]{6}',one['roomCode'])))
            check('other account cannot delete owner room',http('DELETE','/api/rooms/'+one['id'],b)[0]==403)
            remote('prepare')
            forced=dual_client(one,1,15,2,'forced-wss')
            check('physical PC WSS joins and receives real pongs',forced['joined']==1 and forced['failed']==0 and sum(c['pongs'] for c in forced['clients'])>0)
            empty(a,one['id'])
            if args.reconnect_only:
                result['confirmed']=True;result['awsStopStartRejoin']='PASS';result['conclusion']='Real-PC WSS rejoin after guarded Stop/Start; no gameplay resume.'
                return result
            automatic=dual_client(one,1,15,8,'auto',transport='Auto')
            check('Auto joins using UDP or WSS with real pongs',automatic['joined']==1 and automatic['failed']==0 and sum(c['pongs'] for c in automatic['clients'])>0)
            result['autoRoute']=dict(transport=automatic['clients'][0]['transport'],fallbackReason=automatic['clients'][0]['fallbackReason'])
            empty(a,one['id'])
            default_room=dict(id=default['id'],roomCode=remote('default-code'))
            over=dual_client(default_room,11,6,2,'default-11-wss')
            check('default ten WSS slots, eleventh RoomFull',over['joined']==10 and sum(c['reason']=='RoomFull' for c in over['clients'])==1)
            empty(a,default['id'])
            local_udp,local_ws=port(socket.SOCK_DGRAM),port()
            local_room=dict(id='local-default',roomCode=secrets.token_hex(24))
            local_container='jungle-dual-local-'+secrets.token_hex(4)
            local_env=dict(os.environ,ROOM_CODE=local_room['roomCode'])
            local_args=['docker','--context','desktop-linux','run','-d','--name',local_container,'--memory','256m',
                '-p',f'127.0.0.1:{local_udp}:7777/udp','-p',f'127.0.0.1:{local_ws}:7781/tcp',
                '-e','ROOM_CODE','-e','AUTH_MODE=RoomCode','-e','WS_ENABLED=true',args.local_image]
            try:
                command(local_args,env=local_env,timeout=30)
                time.sleep(2)
                with ThreadPoolExecutor(max_workers=2) as pool:
                    concurrent=pool.submit(dual_client,local_room,1,300,15,'concurrent-local','Wss',None,True)
                    data=dual_client(one,10,300,15,'wss-10-300s')
                    local_data=concurrent.result()
                result['localConcurrent']=local_data
                check('Local Docker and AWS run concurrently with separate endpoints',local_data['joined']==1 and local_data['failed']==0 and local_data['endpoint']!=data['endpoint'])
            finally:
                subprocess.run(['docker','--context','desktop-linux','rm','-f',local_container],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=30)
            snapshot=json.loads(remote('snapshot'));(out/'telemetry-wss.json').write_text(json.dumps(snapshot),encoding='utf-8')
            row=evaluate_aws_stage(data,snapshot,float('inf'),10,300)
            row['criteria'].pop('perRoomRttWithinBaseline')
            row['status']='INVALID_GENERATOR' if row['minimumClientHz']<28.5 or data['measuredSeconds']<297 else 'PASS' if all(row['criteria'].values()) else 'FAIL'
            row.update(label='wss-10-300s',transport='Wss',rttP50Ms=quantile([r for c in data['clients'] for r in c['rttMs']],.5))
            stages.append(row);result['confirmed']=row['status']=='PASS';result['confirmedSessions']=10 if result['confirmed'] else None
            result['conclusion']='Real PC WSS transport only; Unity/gameplay/two physical PCs are not accepted by this test.'
            result['twoPhysicalPcLan']='NOT_TESTED';result['twoPhysicalPcVpn']='NOT_TESTED'
            check('WSS 10 sessions/300s stability',result['confirmed'])
            return result
        prior=None
        if args.capacity_after:
            prior_path=Path(args.capacity_after)
            prior=json.loads(prior_path.read_text(encoding='utf-8-sig'))
            required={'missing JWT cannot create room','malformed JWT cannot create room','tampered JWT cannot create room',
                      'expired real JWT rejected after 30s skew','normal 60-minute lifetime restored',
                      'sixth room rejected by server room limit','sixth create request per minute returns 429',
                      'default accepts ten and rejects eleventh','one room accepts ten, rejects eleventh with RoomFull'}
            passed={c['name'] for c in prior['checks'] if c['passed']}
            check('continuation requires previously passed real AWS security/10-session evidence',
                  on_server and prior.get('generatorOnServer') and required<=passed and all(c['passed'] for c in prior['checks'])
                  and any(r['clients']==10 and r['status']=='PASS' for r in prior['stages']))
            result['priorEvidenceSha256']=hashlib.sha256(prior_path.read_bytes()).hexdigest()
            result['priorEvidenceFile']=prior_path.name
            extra=[]
            for owner in (a,a,b,b):
                status,r=post(owner,'CapacityContinue')
                check('JWT creates a continuation room',status==201,status)
                extra.append(r)
            one,two=extra[:2];extras=extra[2:]
        else:
            payload=dict(name='Unauthorized',maxPlayers=10)
            for name,t in [('missing',None),('malformed','bad-token'),('tampered',a.rsplit('.',1)[0]+'.'+'A'*43)]:
                status,_=http('POST','/api/rooms',t,payload)
                check(name+' JWT cannot create room',status==401,status)
            check('unauthorized requests created no room',len(http('GET','/api/rooms',a)[1])==1)
            budget(200)
            short_enabled=True;remote('short-jwt')
            status,login=http('POST','/api/auth/login',body=credentials_a)
            check('login with temporary one-minute token',status==200)
            short=login['accessToken']
            claims=json.loads(base64.urlsafe_b64decode(short.split('.')[1]+'=='))
            check('actual issued lifetime is one minute',claims['exp']-claims['iat']==60)
            remote('restore-jwt');short_enabled=False
            # API restart resets rate windows, preserves signing key and GameServer rooms.
            status,room=post(short,'ShortToken')
            check('real short JWT creates room before expiry',status==201,status)
            check('owner deletes empty short-token room',delete(a,room['id'])==204)
            remaining=claims['exp']+35-time.time()
            if remaining>0: time.sleep(remaining)
            status,_=post(short,'Expired')
            check('expired real JWT rejected after 30s skew',status==401,status)
            check('expired token created no room',len(http('GET','/api/rooms',a)[1])==1)
            status,normal=http('POST','/api/auth/login',body=credentials_a)
            nc=json.loads(base64.urlsafe_b64decode(normal['accessToken'].split('.')[1]+'=='))
            check('normal 60-minute lifetime restored',status==200 and nc['exp']-nc['iat']==3600)
            a=normal['accessToken'];tokens[0]=a
            status,one=post(a,'OwnerA',ownerId='forged-user')
            check('authenticated owner can create',status==201)
            check('six-character random room code',bool(re.fullmatch(r'[A-HJ-NP-Z2-9]{6}',one['roomCode'])))
            check('owner comes from verified sub',any(r['id']==one['id'] for r in http('GET','/api/rooms',a)[1]))
            check('other user cannot see room or code',len(http('GET','/api/rooms',b)[1])==1)
            check('cross-owner deletion denied',delete(b,one['id'])==403)
            check('default cannot be deleted',delete(a,default['id'])==409)
            check('capacity above ten rejected',http('POST','/api/rooms',a,dict(name='Oversize',maxPlayers=11))[0]==400)
            status,two=post(a,'OwnerA2');check('owner creates second room',status==201)
            check('owner third room rejected',post(a,'OwnerA3')[0]==409)
            # A now used four requests; no more A POSTs during this window.
            extras=[]
            for i in range(2):
                status,r=post(b,'OwnerB'+str(i));check('second owner room '+str(i),status==201);extras.append(r)
            status,error=post(c,'Sixth')
            check('sixth room rejected by server room limit',status==409 and error['error']=='RoomLimitReached')
            for _ in range(4): post(c,'RateCheck')
            check('sixth create request per minute returns 429',post(c,'RateCheck')[0]==429)
        default_code=remote('default-code')
        targets=[dict(roomId=default['id'],roomCode=default_code,clients=10)] + [
            dict(roomId=r['id'],roomCode=r['roomCode'],clients=10) for r in [one,two,*extras]]
        remote('prepare')
        initial_snapshot=json.loads(remote('snapshot')); result['server']=dict(initial_snapshot,metrics=[],memory=[])
        if on_server:
            result['controlHost']=result['generatorHost']
            result['generatorHost']=initial_snapshot['host']
            result['generatorContainer']=dict(memoryLimitBytes=256*1024*1024,cpuLimitOneCorePercent=75,sharesEc2WithGameServer=True)
        check('256MiB GameServer, internal control port, RoomCode mode',
              initial_snapshot['memoryLimitBytes']==256*1024*1024 and not initial_snapshot['controlPortPublished']
              and initial_snapshot['configuration']['AUTH_MODE']=='RoomCode')
        check('deployed operating policy 10/50/5/2',all(initial_snapshot['configuration'][k]==str(v) for k,v in
              dict(MAX_PLAYERS=10,MAX_ROOM_PLAYERS=10,MAX_TOTAL_PLAYERS=50,MAX_ROOMS=5,MAX_ROOMS_PER_USER=2).items()))
        default_full=clients([dict(targets[0],clients=11)],3,0,'default-intentional-11th')
        check('default accepts ten and rejects eleventh',default_full['joined']==10 and
              sum(x.get('reason')=='RoomFull' for x in default_full['clients'])==1)
        empty(a,default['id'])
        if not prior:
            # RoomFull is intentional here; do not count it as a performance disconnect.
            def occupied():
                for _ in range(50):
                    rs=http('GET','/api/rooms',a)[1]
                    if any(r['id']==one['id'] and r['activePlayers']>0 for r in rs):
                        check('occupied room cannot be deleted',delete(a,one['id'])==409);return
                    time.sleep(.1)
                check('room became occupied',False)
            oversized=clients([dict(roomId=one['id'],roomCode=one['roomCode'],clients=11)],6,0,'intentional-11th',occupied)
            check('one room accepts ten, rejects eleventh with RoomFull',oversized['joined']==10 and
                  sum(x.get('reason')=='RoomFull' for x in oversized['clients'])==1)
            empty(a,one['id'])
        baseline=clients([dict(targets[0],clients=1)],30,5,'baseline')
        rtts=[r for cl in baseline['clients'] for r in cl['rttMs']]
        baseline95=quantile(rtts,.95)
        check(('EC2 loopback baseline' if on_server else 'Internet baseline')+' has joined client and pongs',baseline['failed']==0 and baseline95 is not None)
        result['baselineP95Ms']=baseline95
        if prior: check('continuation uses identical GameServer image',prior['server']['imageId']==initial_snapshot['imageId'])
        best=10 if prior else None
        for n,secs in ([(30,45),(50,60)] if prior else [(10,45),(30,45),(50,60)]):
            subset=targets[:n//10]
            data=clients(subset,secs,10,'load-'+str(n))
            snap=json.loads(remote('snapshot'))
            (out/('telemetry-'+str(n)+'.json')).write_text(json.dumps(snap),encoding='utf-8')
            row=evaluate_aws_stage(data,snap,baseline95,n,secs);row['label']='load-'+str(n);stages.append(row)
            print(row['label']+': '+row['status'],flush=True)
            for owner,rid in [(a,default['id']),*owned]: empty(owner,rid)
            if row['status']!='PASS':break
            best=n
        if best:
            data=clients(targets[:best//10],180,10,'confirm-'+str(best))
            snap=json.loads(remote('snapshot'))
            (out/'telemetry-confirm.json').write_text(json.dumps(snap),encoding='utf-8')
            row=evaluate_aws_stage(data,snap,baseline95,best,180);row['label']='confirm-'+str(best);stages.append(row)
            result['confirmed']=row['status']=='PASS'
            result['confirmedSessions']=best if result['confirmed'] else None
        result['conclusion']='Network-only result. No Unity/gameplay capacity claim.'
    except BaseException as e:
        result['error']=type(e).__name__+': '+str(e)
        result['conclusion']='Incomplete AWS run. No additional capacity confirmed.'
        raise
    finally:
        for p in procs:
            if p.poll() is None: p.kill();p.wait()
        cleanup_end=time.monotonic()+45
        for owner,rid in list(owned):
            if time.monotonic()>cleanup_end:
                result['cleanupErrors'].append('Cleanup deadline; Stop EC2 takes priority');break
            try:
                empty(owner,rid)
                if delete(owner,rid) not in (204,404): result['cleanupErrors'].append('Room delete failed')
            except Exception as e: result['cleanupErrors'].append('Room cleanup: '+type(e).__name__)
        if short_enabled:
            try: remote('restore-jwt')
            except Exception as e: result['cleanupErrors'].append('JWT restore: '+type(e).__name__)
        try: remote('cleanup')
        except Exception as e: result['cleanupErrors'].append('Collector cleanup: '+type(e).__name__)
        private_plan.unlink(missing_ok=True)
        if tunnel and tunnel.poll() is None:tunnel.terminate();tunnel.wait(timeout=10)
        (out/'report.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
        columns=['label','clients','seconds','status','joined','failed','actualHz','minimumClientHz','rttP95Ms','rttP99Ms','tickHz','cpuPeakPercent','ramPeakPercent']
        with (out/'report.csv').open('w',newline='',encoding='utf-8') as f:
            writer=csv.DictWriter(f,fieldnames=columns,extrasaction='ignore');writer.writeheader();writer.writerows(stages)
        print('AWS evidence: '+str(out/'report.json'),flush=True)


def phase1_connectivity(args):
    """Real PC sockets only. The PowerShell owner guards EC2 Start/Stop separately.

    SSH/SSM carries administration and HTTPS, NEVER game UDP. No payload capture,
    no credentials in argv/evidence, no environment-driven silent route changes.
    """
    import ipaddress
    from concurrent.futures import TimeoutError as FutureTimeout
    ipaddress.IPv4Address(args.connect_ip)
    if args.generator_on_server:
        raise ValueError('Phase 1 requires a real PC generator, not GeneratorOnServer.')
    out = Path(args.output).resolve()
    out.mkdir(parents=True, exist_ok=False)
    result = dict(scope='Physical PC -> EC2; no gameplay', generatedUtc=dt.datetime.now(dt.timezone.utc).isoformat(),
                  generatorOnServer=False, generatorHost=platform.node(), checks=[], routes=[], stages=[], cleanupErrors=[],
                  twoPhysicalPcLan='NOT_TESTED', twoPhysicalPcVpn='NOT_TESTED', tenPhysicalPlayers='NOT_TESTED',
                  reconnectOnly=args.reconnect_only)
    owned, procs = [], []
    tunnel = None
    collector_started = False
    deadline = dt.datetime.fromisoformat(os.environ['AWS_ROOM_DEADLINE_UTC'].replace('Z','+00:00'))
    def budget(seconds=30):
        if dt.datetime.now(dt.timezone.utc)+dt.timedelta(seconds=seconds) >= deadline:
            raise TimeoutError('Phase 1 budget exhausted; cleanup/Stop takes priority.')
    def ssh(script, seconds=45):
        return ssh_script(args.ssh_alias, script, seconds)
    def remote(action):
        if action not in ('default-code','prepare','snapshot','cleanup'): raise ValueError(action)
        return ssh('bash /opt/junglemaze/current/scripts/room-test.sh '+action+'\n')
    def check(name, ok):
        result['checks'].append(dict(name=name,status='PASS' if ok else 'FAIL'))
        print(('PASS ' if ok else 'FAIL ')+name, flush=True)
        if not ok: raise AssertionError(name)
    def run_client(label, endpoint, room_code, count=1, seconds=15, rate=1, warmup=0, config=None):
        budget(seconds+warmup+25)
        client_config = out/(label+'-config.json')
        if config is None:
            client_config.write_text(json.dumps(dict(mode='Online',apiBaseUrl='https://junglemaze.duckdns.org',
                gameServerHost=endpoint,gameServerPort=7777,authMode='RoomCode',protocolVersion=1,connectTimeoutMs=5000)),encoding='utf-8')
        else: client_config = Path(config)
        env={k:v for k,v in os.environ.items() if k not in ('NET_MODE','NET_GAME_SERVER_HOST','NET_GAME_SERVER_PORT','NET_API_BASE_URL','NET_ACCESS_TOKEN')}
        env['NET_ROOM_CODE']=room_code
        with (out/(label+'.log')).open('w',encoding='utf-8') as log:
            p=subprocess.Popen(['dotnet',str(CLIENT),'--config',str(client_config),'--name','Phase1Pc','--clients',str(count),
                '--duration',str(seconds),'--warmup',str(warmup),'--ping-rate',str(rate),'--quiet','--report',str(out/(label+'.json'))],
                env=env,cwd=BACKEND,stdout=log,stderr=subprocess.STDOUT);procs.append(p)
            try: p.wait(timeout=seconds+warmup+20)
            finally:
                if p.poll() is None:p.kill();p.wait()
        return json.loads((out/(label+'.json')).read_text(encoding='utf-8'))
    def capture_probe(label, endpoint, room_code, interface=''):
        # Only Ethernet/IP/UDP header fields are retained. Physical interface avoids
        # Docker bridge duplicates; tailscale0 is used for decrypted overlay headers.
        capture = '''import socket,struct,time,json,subprocess,datetime
iface=INTERFACE or json.loads(subprocess.check_output(['ip','-j','route','get','1.1.1.1']))[0]['dev']
s=socket.socket(socket.AF_PACKET,socket.SOCK_RAW,socket.htons(3));s.bind((iface,0));s.settimeout(.25)
lo=datetime.datetime.now(datetime.timezone.utc).isoformat();end=time.monotonic()+12;rows=[];incoming=outgoing=0
print('READY',flush=True)
while time.monotonic()<end:
 try: data,addr=s.recvfrom(2048)
 except socket.timeout:continue
 # Ethernet on ens5, or a bare IPv4 frame on Linux tun/tailscale0.
 if len(data)>=42 and data[12:14]==b'\\x08\\x00':ip=data[14:]
 elif len(data)>=28 and data[0]>>4==4:ip=data
 else:continue
 ihl=(ip[0]&15)*4
 if ip[9]!=17 or len(ip)<ihl+8:continue
 sp,dp,length=struct.unpack('!HHH',ip[ihl:ihl+6])
 if sp!=7777 and dp!=7777:continue
 outgoing+=sp==7777;incoming+=dp==7777
 if len(rows)<20:rows.append(dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),source=socket.inet_ntoa(ip[12:16]),destination=socket.inet_ntoa(ip[16:20]),sourcePort=sp,destinationPort=dp,udpLength=length))
print(json.dumps(dict(interface=iface,startUtc=lo,endUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),incoming=incoming,outgoing=outgoing,headers=rows)),flush=True)
'''.replace('INTERFACE',repr(interface))
        p=subprocess.Popen(['ssh','-o','BatchMode=yes','-o','ConnectTimeout=30',args.ssh_alias,'sudo python3 -u -'],
                           stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8')
        procs.append(p);p.stdin.write(capture);p.stdin.close()
        try:
            with ThreadPoolExecutor(max_workers=1) as pool:
                ready=pool.submit(p.stdout.readline)
                try: line=ready.result(timeout=40).strip()
                except FutureTimeout: p.kill();raise TimeoutError('Header observer readiness timeout')
            if line!='READY': raise RuntimeError('EC2 header observer failed: '+p.stderr.read()[:160])
            data=run_client(label,endpoint,room_code,seconds=8)
            p.stdin=None
            stdout,_=p.communicate(timeout=20)
            if p.returncode:raise RuntimeError('Header observer exited unsuccessfully')
            observed=json.loads(stdout.strip())
            (out/(label+'-headers.json')).write_text(json.dumps(observed,indent=2),encoding='utf-8')
            passed=data['joined']==1 and data['failed']==0 and sum(c['pongs'] for c in data['clients'])>0
            route=dict(label=label,endpoint=endpoint,status='PASS' if passed else 'BLOCKED',
                joined=data['joined'],pongs=sum(c['pongs'] for c in data['clients']),
                pcTransportSent=sum(c.get('transportPacketsSent',0) for c in data['clients']),
                pcTransportReceived=sum(c.get('transportPacketsReceived',0) for c in data['clients']),
                ec2IncomingHeaders=observed['incoming'],ec2OutgoingHeaders=observed['outgoing'],
                startUtc=observed['startUtc'],endUtc=observed['endUtc'])
            result['routes'].append(route);print(label+': '+route['status'],flush=True)
            return passed
        finally:
            if p.poll() is None:p.kill();p.wait()
    local_port=port()
    class ManagementHTTPS(HTTPSConnection):
        def connect(self):
            sock=socket.create_connection(('127.0.0.1',local_port),self.timeout)
            self.sock=self._context.wrap_socket(sock,server_hostname=self.host)
    def http(method,path,token=None,body=None):
        conn=ManagementHTTPS('junglemaze.duckdns.org',timeout=10,context=ssl.create_default_context())
        headers={'Content-Type':'application/json'}
        if token:headers['Authorization']='Bearer '+token
        try:
            conn.request(method,path,body=json.dumps(body) if body is not None else None,headers=headers)
            res=conn.getresponse();raw=res.read()
            return res.status,json.loads(raw) if raw and 'json' in res.getheader('Content-Type','') else None
        finally:conn.close()
    def empty(token,rid):
        for _ in range(40):
            status,rooms=http('GET','/api/rooms',token)
            if status==200 and all(r['activePlayers']+r['pendingPlayers']==0 for r in rooms if r['id']==rid):return
            time.sleep(.25)
        raise RuntimeError('Slots not returned after disconnect')
    try:
        with (out/'management-ssh.log').open('w',encoding='utf-8') as tunnel_log:
            tunnel=subprocess.Popen(['ssh','-N','-o','BatchMode=yes','-o','ConnectTimeout=30','-o','ExitOnForwardFailure=yes',
                '-L',f'127.0.0.1:{local_port}:127.0.0.1:443',args.ssh_alias],stdout=subprocess.DEVNULL,stderr=tunnel_log)
        procs.append(tunnel)
        for _ in range(200):
            if tunnel.poll() is not None:raise RuntimeError('SSM HTTPS tunnel exited; see management-ssh.log')
            try:
                with socket.create_connection(('127.0.0.1',local_port),.3):break
            except OSError:time.sleep(.2)
        for _ in range(30):
            try:
                if http('GET','/health')[0]==200:break
            except (OSError, ssl.SSLError, HTTPException):pass
            time.sleep(2)
        else:raise RuntimeError('AWS HTTPS/Mongo readiness timeout')
        check('HTTPS certificate and Mongo health over management SSM',True)
        result['apiPath']='Verified HTTPS hostname via SSM; not evidence of public UDP'
        fixtures=json.loads(Path(os.environ['AWS_ROOM_FIXTURES']).read_text(encoding='utf-8-sig'))
        creds=fixtures[0]
        check('register/reuse account',http('POST','/api/auth/register',body=creds)[0] in (201,409))
        status,login=http('POST','/api/auth/login',body=creds)
        check('login yields JWT',status==200);token=login['accessToken']
        for name,jwt in [('missing',None),('malformed','bad-token'),('tampered',token.rsplit('.',1)[0]+'.'+'A'*43)]:
            check(name+' JWT cannot create room',http('POST','/api/rooms',jwt,dict(name='Phase1Unauthorized',maxPlayers=10))[0]==401)
        status,rooms=http('GET','/api/rooms',token)
        check('exclusive session starts with default only',status==200 and len(rooms)==1 and rooms[0]['isDefault'])
        default=rooms[0];default_code=remote('default-code')
        status,room=http('POST','/api/rooms',token,dict(name='Phase1-PC',maxPlayers=10))
        if status==201:owned.append((token,room['id']))
        check('valid JWT creates ten-slot room with six-character code',status==201 and bool(re.fullmatch(r'[A-HJ-NP-Z2-9]{6}',room['roomCode'])))
        result['diagnostics']=ssh("ss -lunp | grep ':7777 '; docker port junglemaze-aws-game-server-1; ip -j route; iptables -S DOCKER-USER 2>/dev/null || true\n")
        direct=capture_probe('public-ip',args.connect_ip,default_code)
        endpoint=args.connect_ip;route='public-ip'
        if not direct:
            ts=Path('C:/Program Files/Tailscale/tailscale.exe')
            if not ts.exists():
                result['routes'].append(dict(label='tailscale',status='BLOCKED',reason='PC client not installed; download/login blocked on current DNS/network'))
            elif args.vpn_ip:
                ip=ipaddress.IPv4Address(args.vpn_ip)
                if ip not in ipaddress.ip_network('100.64.0.0/10'):raise ValueError('VPN address must be EC2 Tailscale IPv4')
                state=json.loads(command([str(ts),'status','--json'],timeout=10))
                check('Tailscale PC is Running',state['BackendState']=='Running')
                peers=list((state.get('Peer') or {}).values())
                check('VPN target is an online enrolled peer',any(str(ip) in p.get('TailscaleIPs',[]) and p.get('Online') for p in peers))
                direct=capture_probe('tailscale',str(ip),room['roomCode'],'tailscale0')
                result['tailscalePath']=command([str(ts),'ping','--c','3',str(ip)],timeout=20)
                endpoint=str(ip);route='tailscale'
            else:result['routes'].append(dict(label='tailscale',status='NOT_TESTED',reason='Pass enrolled EC2 -VpnIp explicitly; no silent endpoint fallback'))
        if not direct:
            result['status']='BLOCKED'
            result['stability']='NOT_TESTED';result['simultaneousLocalAws']='NOT_TESTED'
            result['awsStopStartRejoin']='NOT_TESTED'
            return result
        check('dynamic room joins from real PC',run_client('dynamic-room',endpoint,room['roomCode'],seconds=15)['failed']==0)
        empty(token,room['id'])
        if args.reconnect_only:
            result['status']='PASS';result['awsStopStartRejoin']='PASS';return result
        over=run_client('intentional-11',endpoint,default_code,count=11,seconds=8)
        check('default receives ten and rejects eleventh with RoomFull',over['joined']==10 and sum(c['reason']=='RoomFull' for c in over['clients'])==1)
        empty(token,default['id'])
        check('disconnect returns default slots',run_client('returned-slot',endpoint,default_code,seconds=8)['failed']==0)
        empty(token,default['id'])
        check('wrong RoomCode rejected',run_client('invalid-code',endpoint,secrets.token_hex(16),seconds=8)['clients'][0]['reason']=='InvalidRoomCode')
        remote('prepare')
        collector_started = True
        budget(355)
        if args.local_config:
            # Code is read from this process only; a separate local endpoint is required.
            local_code=os.environ.get('PHASE1_LOCAL_ROOM_CODE')
            if not local_code:raise ValueError('Concurrent Local test needs PHASE1_LOCAL_ROOM_CODE in process environment')
            with ThreadPoolExecutor(max_workers=2) as pool:
                lf=pool.submit(run_client,'concurrent-local','127.0.0.1',local_code,1,300,30,5,args.local_config)
                data=run_client('pc-10-300s',endpoint,room['roomCode'],10,300,30,5)
                local_data=lf.result()
            check('Local and AWS client endpoints remain independent',local_data['endpoint']!=data['endpoint'] and local_data['failed']==0 and local_data['joined']==1)
            result['simultaneousLocalAws']='PASS'
        else:result['simultaneousLocalAws']='NOT_TESTED'
        if not args.local_config:data=run_client('pc-10-300s',endpoint,room['roomCode'],10,300,30,5)
        snap=json.loads(remote('snapshot'))
        (out/'telemetry.json').write_text(json.dumps(snap,indent=2),encoding='utf-8')
        # RTT is reported, no Local latency gate is applied to Sydney/relay.
        row=evaluate_aws_stage(data,snap,float('inf'),10,300)
        row['criteria'].pop('perRoomRttWithinBaseline')
        row['status']='INVALID_GENERATOR' if row['minimumClientHz']<28.5 or data['measuredSeconds']<297 else 'PASS' if all(row['criteria'].values()) else 'FAIL'
        row['rttP50Ms']=quantile([r for c in data['clients'] for r in c['rttMs']],.5)
        row['route']=route;result['stages'].append(row)
        result['stability']=row['status'];result['status']=row['status']
        result['awsStopStartRejoin']='NOT_TESTED'
        return result
    except BaseException as e:
        result['status']='FAIL';result['error']=type(e).__name__+': '+str(e)
        raise
    finally:
        for p in procs:
            if p is not tunnel and p.poll() is None:p.kill();p.wait()
        for owner,rid in owned:
            try:
                empty(owner,rid)
                if http('DELETE','/api/rooms/'+rid,owner)[0] not in (204,404):result['cleanupErrors'].append('Room delete failed')
            except Exception as e:result['cleanupErrors'].append('Room cleanup '+type(e).__name__)
        if collector_started:
            try:remote('cleanup')
            except Exception as e:result['cleanupErrors'].append('Collector cleanup '+type(e).__name__)
        if tunnel and tunnel.poll() is None:tunnel.terminate();tunnel.wait(timeout=10)
        (out/'report.json').write_text(json.dumps(result,indent=2,allow_nan=False),encoding='utf-8')
        print('Phase1 '+result.get('status','FAIL')+': '+str(out/'report.json'),flush=True)


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
    parser.add_argument('--aws-session', action='store_true', help='Use an already running, guarded AWS test session; never starts EC2.')
    parser.add_argument('--connect-ip', help='Connect HTTPS/UDP to current IP while verifying the original TLS hostname.')
    parser.add_argument('--ssh-alias', default='junglemaze')
    parser.add_argument('--capacity-after',help='Continue capacity stages only after a saved successful AWS JWT/10-session verification; record evidence hash.')
    parser.add_argument('--generator-on-server',action='store_true',help='SSM-only management + generator in EC2; no WAN RTT claim.')
    parser.add_argument('--phase1-local', action='store_true', help='Local restart/persistence and one 10-session/300s stability stage, not a capacity search.')
    parser.add_argument('--dual-local', action='store_true', help='Local account/restart checks and separate UDP v2 + WebSocket 10-session/300s stages.')
    parser.add_argument('--dual-aws', action='store_true', help='Use an already guarded/deployed AWS session for real-PC WSS/Auto, never co-located.')
    parser.add_argument('--local-image', help='Locally verified GameServer image for the concurrent isolated Local Docker smoke.')
    parser.add_argument('--phase1-connectivity', action='store_true', help='Real PC -> EC2 probe, VPN fallback and bounded 10-session stability. Never generates UDP inside EC2.')
    parser.add_argument('--vpn-ip', help='Already enrolled EC2 Tailscale IPv4; requires the PC Tailscale client to be Running.')
    parser.add_argument('--local-config', help='An independently running Local stack for concurrent Local/AWS smoke.')
    parser.add_argument('--reconnect-only', action='store_true', help='A 15s real-PC rejoin after guarded EC2 Stop/Start.')
    args = parser.parse_args()
    if args.phase1_connectivity:
        return phase1_connectivity(args)
    if args.aws_session or args.dual_aws:
        return aws_session(args)
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
    fixture_credentials = {}
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
        fixture_credentials[name] = credentials
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
            api_port, udp_port, ws_port = port(), port(socket.SOCK_DGRAM), port()
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
                                    mem_limit='256m', memswap_limit='256m', ports=[f'127.0.0.1:{udp_port}:7777/udp',f'127.0.0.1:{ws_port}:7781/tcp'],
                                    environment={'GAME_BIND_ADDRESS': '0.0.0.0', 'GAME_PORT': '7777', 'MAX_PLAYERS': '10',
                                                 'AUTH_MODE': 'RoomCode', 'TICK_RATE': '30', 'METRICS_INTERVAL_SECONDS': '1',
                                                 'WS_ENABLED':'true','WS_PORT':'7781','WS_BIND_ADDRESS':'0.0.0.0',
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
            if args.phase1_local or args.dual_local:
                # A fresh Compose startup, then a full stop/start using the SAME volume.
                compose('down')
                compose('up', '-d', '--no-build')
                container = compose('ps', '-q', 'game-server')
                for _ in range(120):
                    try:
                        if request(base, 'GET', '/health')[0] == 200: break
                    except (OSError, urllib.error.URLError): pass
                    time.sleep(.5)
                else: raise RuntimeError('Local restart readiness timeout')
                code_status, login_data = request(base, 'POST', '/api/auth/login', body=fixture_credentials['benchmark'])
                check('account persists after full Compose stop/start with retained volume', code_status == 200)
                token = login_data['accessToken']
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
                minimum_hz = min((c['sent']/max(data['measuredSeconds'],.001) for c in data['clients']), default=0)
                valid = minimum_hz >= 30 * .95 and data['measuredSeconds'] >= seconds * .99
                status = 'INVALID_GENERATOR' if not valid else 'PASS' if all(criteria.values()) else 'FAIL'
                row = dict(label=label, clients=n, seconds=seconds, status=status, actualHz=hz, joined=data['joined'],
                           failed=data['failed'], sent=sent, lost=lost, timeoutPercent=100 * lost / max(sent, 1),
                           minimumClientHz=minimum_hz, rttP50Ms=quantile(rtts,.5), rttP95Ms=p95, rttP99Ms=p99, tickHz=tps, droppedTicks=dropped, cpuPeakPercent=cpu,
                           ramPeakPercent=ram, criteria=criteria, generatorCpuPercent=data['generatorCpuOneCorePercent'],
                           metrics=metrics, memorySamples=samples)
                stages.append(row)
                print(f'{label}: {status} joined={data["joined"]}/{n} rate={hz:.2f}Hz p95={p95} RAM={ram:.1f}% CPU={cpu:.1f}%', flush=True)
                wait_empty(token, room_id)
                return status
            best, failed_at, invalid = 0, None, False
            if args.dual_local:
                for transport in ('Udp','Wss'):
                    config.write_text(json.dumps(dict(mode='Local',gameServerHost='127.0.0.1',gameServerPort=udp_port,
                        authMode='RoomCode',protocolVersion=2,connectTimeoutMs=5000,transport=transport,
                        webSocketUrl=f'ws://127.0.0.1:{ws_port}/game')),encoding='utf-8')
                    verdict=stage(10,300,'dual-'+transport.lower())
                    stages[-1]['transport']=transport
                result['confirmed']=all(s['status']=='PASS' for s in stages)
                result['conclusion']='Local UDP v2/WebSocket network-only stability; no gameplay capacity claim.'
                return result
            for n in ([10] if args.phase1_local else levels):
                verdict = stage(n, 300 if args.phase1_local else args.seconds, f'level-{n}')
                if verdict != 'PASS':
                    invalid = verdict == 'INVALID_GENERATOR'; failed_at = n; break
                best = n
            if failed_at and not invalid and not args.phase1_local:
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
            confirmed = bool(best and not invalid and (args.phase1_local or stage(best, args.confirm_seconds, f'confirm-{best}') == 'PASS'))
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
