"""Test image/cloud config trên Docker local, dùng project/volume riêng, không gọi AWS/DuckDNS.

Chạy sau build solution và build/tag release images; không đọc Backend/.env.
TLS internal chỉ dùng trong bài test. Release vẫn dùng chứng chỉ ACME public của Caddy.
"""
import argparse
import base64
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import secrets
import socket
import ssl
import subprocess
import time
import urllib.error
import urllib.request

BACKEND = Path(__file__).resolve().parents[1]
passed = 0


def run(*args, input=None, env=None, okay=True):
    result = subprocess.run(args, input=input, capture_output=True, env=env)
    if okay and result.returncode:
        # Không in argv/env/config; chúng có thể chứa secret kiểm thử.
        raise RuntimeError('Command failed: ' + args[0] + '\n' + result.stderr.decode(errors='replace')[-2000:])
    return result


def check(name, condition):
    global passed
    if not condition:
        raise AssertionError(name)
    passed += 1
    print('PASS ' + name, flush=True)


def port(kind=socket.SOCK_STREAM):
    with socket.socket(type=kind) as probe:
        probe.bind(('127.0.0.1', 0))
        return probe.getsockname()[1]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--release-id', required=True)
    release_id = parser.parse_args().release_id
    project = 'jungle-aws-check-' + secrets.token_hex(6)
    folder = BACKEND.parent / '.utmp/aws-tests' / project
    folder.mkdir(parents=True)
    images = {name: f'junglemaze-{name}:{release_id}' for name in ('api', 'game', 'mongo', 'caddy')}
    for image in images.values():
        data = json.loads(run('docker', 'image', 'inspect', image).stdout)[0]
        check(image + ' Linux/amd64', data['Os'] == 'linux' and data['Architecture'] == 'amd64')
    # Đọc file nguồn CRLF bình thường; mount bản LF vào Linux để kiểm tra Bash parser.
    script_folder = folder / 'scripts'
    script_folder.mkdir()
    for source in (BACKEND / 'Deploy/Aws/scripts').iterdir():
        (script_folder / source.name).write_text(source.read_text(encoding='utf-8-sig'), encoding='utf-8', newline='\n')
    run('docker', 'run', '--rm', '--entrypoint', 'bash', '-v', str(script_folder) + ':/checks:ro',
        images['mongo'], '-c', 'for file in /checks/*.sh; do bash -n "$file" || exit; done')
    check('All Bash scripts parse on Linux', True)
    compile((script_folder / 'duckdns.py').read_text(), 'duckdns.py', 'exec')
    check('DuckDNS Python parses', True)
    password = secrets.token_urlsafe(18)
    hashed = run('docker', 'run', '--rm', '-i', images['caddy'], 'caddy', 'hash-password',
                 '--algorithm', 'bcrypt', '--bcrypt-cost', '12', input=(password + '\n').encode()).stdout.decode().strip()
    check('Swagger bcrypt generated via stdin', hashed.startswith('$2'))
    room = secrets.token_urlsafe(24)
    values = dict(API_IMAGE=images['api'], GAME_IMAGE=images['game'], MONGO_IMAGE=images['mongo'],
                  CADDY_IMAGE=images['caddy'], JWT_SIGNING_KEY=secrets.token_urlsafe(64),
                  PUBLIC_DOMAIN='localhost', ACME_EMAIL='test@example.com', SWAGGER_USER='teacher',
                  SWAGGER_HASH=hashed, ROOM_CODE=room, AUTH_MODE='RoomCode')
    env_file = folder / 'test.env'
    env_file.write_text(''.join(f"{key}='{value}'\n" for key, value in values.items()), encoding='utf-8')
    http_port, https_port, udp_port = port(), port(), port(socket.SOCK_DGRAM)
    original = (BACKEND / 'compose.aws.yaml').read_text(encoding='utf-8')
    compose_file = folder / 'compose.yaml'
    compose_file.write_text(original.replace('0.0.0.0:7777:7777/udp', f'127.0.0.1:{udp_port}:7777/udp')
                           .replace('0.0.0.0:80:80/tcp', f'127.0.0.1:{http_port}:80/tcp')
                           .replace('0.0.0.0:443:443/tcp', f'127.0.0.1:{https_port}:443/tcp'), encoding='utf-8')
    caddy_source = (BACKEND / 'Deploy/Aws/Caddyfile').read_text(encoding='utf-8')
    (folder / 'Caddyfile').write_text(caddy_source.replace('{$PUBLIC_DOMAIN} {', '{$PUBLIC_DOMAIN} {\n    tls internal'), encoding='utf-8')
    prefix = ['docker', 'compose', '--project-name', project, '--env-file', str(env_file), '-f', str(compose_file)]

    def compose(*args, **kwargs):
        return run(*prefix, *args, **kwargs)

    config = json.loads(compose('config', '--format', 'json').stdout)
    services = config['services']
    check('Mongo/API have no host ports', not services['mongo'].get('ports') and not services['api'].get('ports'))
    # `compose config` escape $ thành $$ để có thể đọc lại output; container phải nhận $ thật.
    check('Compose renders bcrypt without truncation', services['caddy']['environment']['SWAGGER_HASH'].replace('$$', '$') == hashed)
    check('All services have bounded RAM', all(int(s['mem_limit']) > 0 for s in services.values()))
    check('Game cloud guard enabled', services['game-server']['environment']['PUBLIC_HOSTING'] == 'true')
    # Validate production Caddy syntax separately; no ACME request during validation.
    prod_caddy = folder / 'Caddyfile.production'
    prod_caddy.write_text(caddy_source, encoding='utf-8')
    caddy_env = {**os.environ, 'PUBLIC_DOMAIN': 'junglemaze.duckdns.org', 'ACME_EMAIL': 'test@example.com',
                 'SWAGGER_USER': 'teacher', 'SWAGGER_HASH': hashed}
    run('docker', 'run', '--rm', '-v', str(prod_caddy) + ':/etc/caddy/Caddyfile:ro',
        '-e', 'PUBLIC_DOMAIN', '-e', 'ACME_EMAIL', '-e', 'SWAGGER_USER', '-e', 'SWAGGER_HASH',
        images['caddy'], 'caddy', 'validate', '--config', '/etc/caddy/Caddyfile', env=caddy_env)
    check('Production Caddy configuration validates', True)
    rejected = run('docker', 'run', '--rm', '-e', 'PUBLIC_HOSTING=true', '-e', 'AUTH_MODE=Jwt',
                   images['game'], okay=False)
    check('Public JWT game fails before binding socket', rejected.returncode == 1 and b'RoomCode' in rejected.stderr)
    try:
        compose('up', '-d', '--no-build', '--pull', 'never', '--wait', '--wait-timeout', '180')
        check('Four bounded services start and API/Mongo become healthy', True)
        run_health = compose('exec', '-T', 'api', 'dotnet', 'Api.dll', '--healthcheck')
        check('API image healthcheck command works', run_health.returncode == 0)
        cid = compose('ps', '-q', 'caddy').stdout.decode().strip()
        runtime_env = json.loads(run('docker', 'inspect', cid).stdout)[0]['Config']['Env']
        check('Caddy runtime receives exact bcrypt hash', 'SWAGGER_HASH=' + hashed in runtime_env)
        cert = folder / 'root.crt'
        for _ in range(30):
            copied = run('docker', 'cp', cid + ':/data/caddy/pki/authorities/local/root.crt', str(cert), okay=False)
            if copied.returncode == 0:
                break
            time.sleep(0.5)
        # Xác minh certificate bằng CA của test; không dùng CERT_NONE/--insecure.
        context = ssl.create_default_context(cafile=str(cert))
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPSHandler(context=context))
        base = f'https://localhost:{https_port}'

        def request(method, path, data=None, authorization=None):
            headers = {'Content-Type': 'application/json'}
            if authorization:
                headers['Authorization'] = authorization
            payload = json.dumps(data).encode() if data is not None else None
            req = urllib.request.Request(base + path, data=payload, headers=headers, method=method)
            try:
                response = opener.open(req, timeout=12)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                return response.status, response.read()

        for _ in range(30):
            try:
                if request('GET', '/health')[0] == 200:
                    break
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(0.5)
        check('HTTPS health through Caddy with verified TLS', request('GET', '/health')[0] == 200)
        check('Swagger UI requires Basic auth', request('GET', '/swagger/index.html')[0] == 401)
        check('Swagger JSON requires Basic auth', request('GET', '/swagger/v1/swagger.json')[0] == 401)
        basic = 'Basic ' + base64.b64encode(('teacher:' + password).encode()).decode()
        check('Swagger UI opens with correct Basic auth', request('GET', '/swagger/index.html', authorization=basic)[0] == 200)
        check('Swagger JSON opens with correct Basic auth', request('GET', '/swagger/v1/swagger.json', authorization=basic)[0] == 200)
        account = dict(username='aws_check_user', password=secrets.token_urlsafe(16))
        status, _ = request('POST', '/api/auth/register', account)
        check('Register over HTTPS works without proxy Basic auth', status == 201)
        status, body = request('POST', '/api/auth/login', account)
        check('Login over HTTPS issues JWT', status == 200 and 'accessToken' in json.loads(body))
        bearer = 'Bearer ' + json.loads(body)['accessToken']
        check('API Bearer passes through Caddy', request('GET', '/api/me', authorization=bearer)[0] == 200)
        check('API still requires JWT for /api/me', request('GET', '/api/me')[0] == 401)
        check('Public request body >16KB rejected', request('POST', '/api/auth/register', {'username': 'x' * 18000})[0] == 413)
        # Recreate chỉ API để reset fixed window trước kiểm thử chính xác 60 + 1.
        compose('up', '-d', '--force-recreate', '--wait', '--wait-timeout', '90', 'api')
        # Gửi cùng cửa sổ; handshake TLS tuần tự trên PC chậm có thể vượt 60 giây.
        started = time.monotonic()
        with ThreadPoolExecutor(max_workers=20) as workers:
            codes = list(workers.map(lambda _: request('POST', '/api/auth/login', {})[0], range(60)))
        check('Auth burst fits one rate-limit window', time.monotonic() - started < 55)
        check('First 60 auth requests reach validation', all(code == 400 for code in codes))
        check('61st auth request returns 429', request('POST', '/api/auth/register', {})[0] == 429)
        check('Rate limit does not block health', request('GET', '/health')[0] == 200)
        check('Rate limit does not block authenticated /api/me', request('GET', '/api/me', authorization=bearer)[0] == 200)
        # Backup → delete test collection → restore archive; database is project-isolated.
        backup = compose('exec', '-T', 'mongo', 'mongodump', '--db', 'jungle_maze', '--archive', '--gzip').stdout
        check('Mongo binary archive backup created', len(backup) > 100)
        compose('exec', '-T', 'mongo', 'mongosh', '--quiet', '--eval', 'db.getSiblingDB("jungle_maze").users.drop()')
        compose('exec', '-T', 'mongo', 'mongorestore', '--drop', '--nsInclude', 'jungle_maze.*', '--archive', '--gzip', input=backup)
        count = compose('exec', '-T', 'mongo', 'mongosh', '--quiet', '--eval', 'db.getSiblingDB("jungle_maze").users.countDocuments({})').stdout.decode().strip()
        check('Restore preserves test account', count == '1')
        compose('up', '-d', '--force-recreate', '--wait', '--wait-timeout', '90', 'mongo', 'api')
        check('Account persists after Mongo/API recreation', request('GET', '/api/me', authorization=bearer)[0] == 200)
        client_config = folder / 'client.json'
        client_config.write_text(json.dumps(dict(mode='Local', gameServerHost='127.0.0.1', gameServerPort=udp_port,
                                                authMode='RoomCode', protocolVersion=1, connectTimeoutMs=5000)), encoding='utf-8')
        result = run('dotnet', str(BACKEND / 'NetSmokeClient/bin/Debug/net8.0/NetSmokeClient.dll'),
                     '--config', str(client_config), '--name', 'AwsDocker', '--clients', '10', '--duration', '6',
                     env={**os.environ, 'NET_ROOM_CODE': room})
        check('10 clients join/pong public-mode container UDP', b'joined=10 failed=0' in result.stdout)
        print(f'PASS AWS suite: {passed} checks (local Docker, not deployed EC2).', flush=True)
    finally:
        # Chỉ xóa tài nguyên có project name ngẫu nhiên của chính bài test.
        compose('down', '--volumes', '--remove-orphans', okay=False)
        env_file.unlink(missing_ok=True)


if __name__ == '__main__':
    main()
