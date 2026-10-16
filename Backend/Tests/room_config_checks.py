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

# Run the actual numeric migration against disposable files, including existing larger limits.
helper=(backend/'Deploy/Aws/scripts/room-test.sh').read_text(encoding='utf-8')
policy=helper.split("<<'PY_POLICY'\n",1)[1].split('\nPY_POLICY',1)[0]
with tempfile.TemporaryDirectory(prefix='jungle-policy-') as folder:
    path=Path(folder)/'config.env'
    original="ROOM_CODE='KeepMe'\nJWT_SIGNING_KEY='KeepKey'\nMAX_ROOMS=8\nMAX_TOTAL_PLAYERS=128\n"
    path.write_text(original)
    subprocess.run([sys.executable,'-c',policy,str(path)],check=True,capture_output=True)
    first=path.read_text()
    assert "ROOM_CODE='KeepMe'\nJWT_SIGNING_KEY='KeepKey'\n" in first
    assert 'MAX_ROOMS=5\n' in first and 'MAX_TOTAL_PLAYERS=50\n' in first
    subprocess.run([sys.executable,'-c',policy,str(path)],check=True,capture_output=True)
    assert path.read_text()==first
    path.write_text(first+'MAX_ROOMS=9\n')
    duplicate=path.read_text()
    assert subprocess.run([sys.executable,'-c',policy,str(path)],capture_output=True).returncode!=0
    assert path.read_text()==duplicate
print('PASS numeric policy migration: preserves secrets, applies selected limits, idempotent, rejects duplicate keys.')

# A busy room can fail while the aggregate RTT passes; every client's send rate must be valid.
sys.path.insert(0,str(backend/'Tools'))
from room_benchmark import evaluate_aws_stage
data=dict(measurementUtc='2026-10-16T00:00:00Z',measurementEndUtc='2026-10-16T00:01:00Z',
 measuredSeconds=60,joined=2,failed=0,cancelled=False,generatorCpuOneCorePercent=10,
 clients=[dict(room='a',sent=1800,timedOut=0,outstanding=0,rttMs=[100]*99),
          dict(room='b',sent=1800,timedOut=0,outstanding=0,rttMs=[151])])
snap=dict(metrics=['[metrics] utc=2026-10-16T00:00:10Z ticksPerSecond=30 cpuOneCorePercent=10 droppedTicks=0'],
 memory=['2026-10-16T00:00:10Z,50000000'],memoryLimitBytes=268435456,restartCount=0,oomKilled=False,running=True)
assert evaluate_aws_stage(data,snap,100,2,60)['status']=='FAIL'
data['clients'][1]['rttMs']=[140]
assert evaluate_aws_stage(data,snap,100,2,60)['status']=='PASS'
data['clients'][1]['sent']=1700
assert evaluate_aws_stage(data,snap,100,2,60)['status']=='INVALID_GENERATOR'
data['clients'][1]['sent']=1800;snap['metrics']=[]
assert evaluate_aws_stage(data,snap,100,2,60)['status']=='FAIL'
print('PASS AWS evaluator: per-room RTT, baseline allowance, per-client generator rate, missing telemetry.')

# Real transport helper must send LF bytes from Windows, not text-mode CRLF.
from unittest.mock import patch
from types import SimpleNamespace
from room_benchmark import ssh_script
with patch('room_benchmark.subprocess.run',return_value=SimpleNamespace(returncode=0,stdout=b'OK\n')) as run:
    assert ssh_script('no-network', 'echo OK\r\n')=='OK'
    assert run.call_args.kwargs['input']==b'echo OK\n'
    assert 'text' not in run.call_args.kwargs
with patch('room_benchmark.subprocess.run',return_value=SimpleNamespace(returncode=2,stdout=b'PRIVATE')):
    try:ssh_script('no-network','false\n')
    except RuntimeError as e:assert 'PRIVATE' not in str(e)
    else:raise AssertionError('Remote failure must be reported')
print('PASS SSH Bash transport: LF bytes on Windows and no private stdout on failure.')

# Resolve actual Compose interpolation with public examples only; never read team .env.
import os, json
for overrides, game_host, api_host in [({},'127.0.0.1','127.0.0.1'),
        ({'PUBLISH_HOST':'192.168.1.20','API_PUBLISH_HOST':''},'192.168.1.20','192.168.1.20'),
        ({'GAME_PUBLISH_HOST':'100.80.1.10','API_PUBLISH_HOST':'127.0.0.1'},'100.80.1.10','127.0.0.1')]:
    env={k:v for k,v in os.environ.items() if k not in ('GAME_PUBLISH_HOST','API_PUBLISH_HOST','PUBLISH_HOST','GAME_PORT','API_PORT','MONGO_PORT')}
    env.update(overrides)
    spec=json.loads(subprocess.check_output(['docker','compose','--env-file',str(backend/'.env.example'),
        '-f',str(backend/'compose.yaml'),'--profile','account','config','--format','json'],env=env,text=True,encoding='utf-8'))
    assert spec['services']['game-server']['ports'][0]['host_ip']==game_host
    assert spec['services']['api']['ports'][0]['host_ip']==api_host
    assert spec['services']['mongo']['ports'][0]['host_ip']=='127.0.0.1'
    assert spec['services']['game-server']['environment']['GAME_BIND_ADDRESS']=='0.0.0.0'
print('PASS Compose endpoint policy: Local loopback, legacy fallback, per-service VPN game bind and private DB.')

# Exercise real session initialization/error reporting without any network/SSH/AWS access.
from types import SimpleNamespace
from unittest.mock import patch
import datetime as dt, os, json
import room_benchmark
with tempfile.TemporaryDirectory(prefix='jungle-session-check-') as folder:
    output=Path(folder)/'result'
    args=SimpleNamespace(connect_ip='127.0.0.1',api_url='https://junglemaze.duckdns.org',output=str(output),
                         ssh_alias='never-connect',generator_on_server=False)
    with patch.dict(os.environ,{'AWS_ROOM_DEADLINE_UTC':(dt.datetime.now(dt.timezone.utc)+dt.timedelta(minutes=28)).isoformat()}), \
         patch.object(room_benchmark,'command',return_value='test-commit'), \
         patch.object(room_benchmark.socket,'create_connection',side_effect=OSError('simulated transport failure')):
        try: room_benchmark.aws_session(args)
        except OSError: pass
        else: raise AssertionError('Expected transport failure')
    report=json.loads((output/'report.json').read_text())
    assert report['error']=='OSError: simulated transport failure' and not report['confirmed']
print('PASS actual AWS session initialization and report/cleanup on failed HTTP transport.')

# Actual nested cleanup helper: an owner can delete with a refreshed JWT.
import ast
tree=ast.parse((backend/'Tools/room_benchmark.py').read_text(encoding='utf-8'))
# Execute the actual observer against synthetic Ethernet and bare IPv4 frames.
# No raw socket/admin privilege/network access is used by this regression.
phase1=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='phase1_connectivity')
capture=next(n for n in phase1.body if isinstance(n,ast.FunctionDef) and n.name=='capture_probe')
assignment=next(n for n in capture.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='capture' for t in n.targets))
observer=eval(compile(ast.Expression(assignment.value),'<actual observer>','eval'),{'interface':'test0'})
import socket,struct,types,contextlib,io
def frame(source,destination,bare=False):
    ip=bytearray(28);ip[0]=0x45;ip[9]=17;ip[12:16]=socket.inet_aton('192.0.2.1');ip[16:20]=socket.inet_aton('192.0.2.2')
    ip[20:28]=struct.pack('!HHHH',source,destination,8,0)
    return bytes(ip) if bare else b'\0'*12+b'\x08\x00'+bytes(ip)
class FakeRaw:
    def __init__(self):self.frames=[frame(45678,7777),frame(7777,45678),frame(45678,7777,True),frame(22,443)]
    def bind(self,address):assert address==('test0',0)
    def settimeout(self,value):pass
    def recvfrom(self,size):
        if not self.frames:raise socket.timeout()
        return self.frames.pop(0),None
counter=iter(range(30))
fake_socket=types.SimpleNamespace(AF_PACKET=0,SOCK_RAW=0,htons=socket.htons,socket=lambda *a:FakeRaw(),timeout=socket.timeout,inet_ntoa=socket.inet_ntoa)
with patch.dict(sys.modules,{'socket':fake_socket,'time':types.SimpleNamespace(monotonic=lambda:next(counter))}),contextlib.redirect_stdout(io.StringIO()) as printed:
    exec(compile(observer,'<actual header observer>','exec'),{})
observed=json.loads(printed.getvalue().splitlines()[-1])
assert observed['incoming']==2 and observed['outgoing']==1 and len(observed['headers'])==3
assert all(set(h)=={'utc','source','destination','sourcePort','destinationPort','udpLength'} for h in observed['headers'])
print('PASS actual header observer: Ethernet/TUN IPv4, unrelated port filtering, no payload persistence.')
aws_node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='aws_session')
delete_node=next(n for n in aws_node.body if isinstance(n,ast.FunctionDef) and n.name=='delete')
ns=dict(owned=[('expired-short-token','already-deleted'),('other-owner','other-room')],
        http=lambda *a:(204,None))
exec(compile(ast.Module(body=[delete_node],type_ignores=[]),'<actual room cleanup>','exec'),ns)
assert ns['delete']('refreshed-token','already-deleted')==204
assert ns['owned']==[('other-owner','other-room')]
ns['http']=lambda *a:(403,None)
ns['delete']('wrong-owner','other-room')
assert ns['owned']==[('other-owner','other-room')]
print('PASS cleanup regression: refreshed JWT removes stale room record; forbidden deletion preserves it.')

# Execute the actual finally block with mocked AWS boundaries. No real AWS command can run.
if sys.platform=='win32':
    lifecycle=backend/'Deploy/test-aws-rooms.ps1'
    code=r"""
$ErrorActionPreference='Stop'
$ast=[System.Management.Automation.Language.Parser]::ParseFile($env:JUNGLE_LIFECYCLE_SOURCE,[ref]$null,[ref]$null)
$bounded=$ast.Find({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Bounded'},$true)
Invoke-Expression $bounded.Extent.Text
Bounded powershell '-NoProfile -Command exit 0' 5
$caught=$false
try {Bounded powershell '-NoProfile -Command exit 7' 5} catch {$caught=$_.Exception.Message -match '\(7\)'}
if(-not $caught) {throw 'Native exit code not preserved'}
$try=$ast.Find({param($n) $n -is [System.Management.Automation.Language.TryStatementAst] -and $n.Finally -and $n.Finally.Extent.Text.Contains('startedByThisRun')},$false)
$body=$try.Finally.Extent.Text
$script=[scriptblock]::Create($body.Substring(1,$body.Length-2))
function Event($m) {}
function Save-Lifecycle {}
function Start-Sleep {}
function Stop-Process {}
function State { @{ State=@{Name='stopped'} } }
foreach($failStop in @($false,$true)) {
  $script:called=0
  function Aws { $script:called++; if($failStop) {throw 'simulated network failure'} }
  $taskLifecycle=@{startedByThisRun=$true};$taskWatchdog=$null
  & $script
  if($script:called -ne $(if($failStop){3}else{1})) {throw 'Stop attempts missing'}
  if(-not $taskLifecycle.stoppedConfirmed) {throw 'Did not verify Stopped'}
}
$taskLifecycle=@{startedByThisRun=$false};$script:called=0
& $script
if($script:called) {throw 'Stopped an instance not owned by this run'}
$failStop=$false
$taskLifecycle=@{startedByThisRun=$false;adoptedRunningInstance=$true};$script:called=0
& $script
if($script:called -ne 1 -or -not $taskLifecycle.stoppedConfirmed) {throw 'Explicitly adopted Running instance was not stopped'}
Write-Output 'PASS lifecycle finally: Stop after error, retries, Stopped verification, explicit Running adoption, no Stop before ownership.'
"""
    import os
    env=dict(os.environ,JUNGLE_LIFECYCLE_SOURCE=str(lifecycle))
    subprocess.run(['powershell','-NoProfile','-Command',code],env=env,check=True)
