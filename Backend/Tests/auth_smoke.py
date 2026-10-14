"""Test API/MongoDB và client -> API -> UDP thật; không dùng .env hay DB của team."""
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import hmac
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import time
import urllib.error
import urllib.request

BACKEND = Path(__file__).resolve().parents[1]
DLL = BACKEND / 'Api/bin/Debug/net8.0/Api.dll'
GAME_DLL = BACKEND / 'GameServer/bin/Debug/net8.0/GameServer.dll'
CLIENT_DLL = BACKEND / 'NetSmokeClient/bin/Debug/net8.0/NetSmokeClient.dll'
RUN_ID = secrets.token_hex(6)
CONTAINER = 'jungle-auth-test-' + RUN_ID
DATABASE = 'auth_test_' + RUN_ID
LOG_DIR = BACKEND.parent / '.utmp/auth-tests' / RUN_ID
passed = 0


def command(*args):
    return subprocess.check_output(args, text=True, encoding='utf-8').strip()


def check(name, condition):
    global passed
    if not condition:
        raise AssertionError(name)
    passed += 1
    print('PASS ' + name, flush=True)


def encode(value):
    raw = json.dumps(value, separators=(',', ':')).encode()
    return base64.urlsafe_b64encode(raw).decode().rstrip('=')


def decode(value):
    return json.loads(base64.urlsafe_b64decode(value + '=' * (-len(value) % 4)))


def signed_token(payload, key, algorithm='HS256'):
    header = encode({'alg': algorithm, 'typ': 'JWT'})
    body = header + '.' + encode(payload)
    digest = hashlib.sha256 if algorithm == 'HS256' else hashlib.sha512
    signature = base64.urlsafe_b64encode(hmac.new(key.encode(), body.encode(), digest).digest()).decode().rstrip('=')
    return body + '.' + signature


def main():
    if not all(path.exists() for path in (DLL, GAME_DLL, CLIENT_DLL)):
        raise RuntimeError('Build JungleBackend.sln before running this test.')
    # Chỉ dùng image đã có; không vô tình pull/start môi trường Compose của người dùng.
    command('docker', 'image', 'inspect', 'mongo:8.0', '--format', '{{.Id}}')
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    with socket.socket() as socket_probe:
        socket_probe.bind(('127.0.0.1', 0))
        api_port = socket_probe.getsockname()[1]
    base_url = f'http://127.0.0.1:{api_port}'
    key = base64.b64encode(secrets.token_bytes(64)).decode()
    process = None
    log = None
    container_created = False
    game_process = game_log = held_client = held_log = None
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def request(method, path, data=None, token=None):
        headers = {'Content-Type': 'application/json'}
        if token is not None:
            headers['Authorization'] = 'Bearer ' + token
        payload = json.dumps(data).encode() if data is not None else None
        req = urllib.request.Request(base_url + path, data=payload, headers=headers, method=method)
        try:
            response = opener.open(req, timeout=10)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            body = response.read().decode()
            is_json = 'json' in response.headers.get('Content-Type', '')
            return response.status, (json.loads(body) if is_json else body) if body else None

    def stop_api():
        nonlocal process, log
        if process is not None:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            process = None
        if log is not None:
            log.close()
            log = None

    def start_api(env, label):
        nonlocal process, log
        log = (LOG_DIR / (label + '.log')).open('w', encoding='utf-8')
        process = subprocess.Popen(['dotnet', str(DLL), '--urls', base_url], cwd=BACKEND,
                                   env=env, stdout=log, stderr=subprocess.STDOUT)
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError('API exited during startup; see the local test log.')
            try:
                if request('GET', '/health')[0] == 200:
                    return
            except (OSError, urllib.error.URLError):
                pass
            time.sleep(0.2)
        raise RuntimeError('API did not become healthy within 20 seconds.')

    def stop_game():
        nonlocal game_process, game_log
        if game_process is not None:
            if game_process.poll() is None:
                game_process.terminate()
                try:
                    game_process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    game_process.kill()
                    game_process.wait()
            game_process = None
        if game_log is not None:
            game_log.close()
            game_log = None

    def start_game(settings, label):
        nonlocal game_process, game_log
        log_path = LOG_DIR / (label + '.log')
        game_log = log_path.open('w', encoding='utf-8')
        game_process = subprocess.Popen(['dotnet', str(GAME_DLL)], cwd=BACKEND, env=settings,
                                        stdout=game_log, stderr=subprocess.STDOUT)
        for _ in range(100):
            if game_process.poll() is not None:
                raise RuntimeError('Test GameServer exited; see local log (do not share secrets).')
            if '[ready]' in log_path.read_text(encoding='utf-8'):
                return
            time.sleep(0.1)
        raise RuntimeError('Test GameServer did not start.')

    def client_start(config, client_env, clients=1, duration=2):
        config_path = LOG_DIR / 'client.json'
        config_path.write_text(json.dumps(config), encoding='utf-8')
        arguments = ['dotnet', str(CLIENT_DLL), '--config', str(config_path), '--name', 'IntegrationTest',
                     '--clients', str(clients), '--duration', str(duration)]
        settings = os.environ.copy()
        for name in ('NET_ACCESS_TOKEN', 'NET_USERNAME', 'NET_PASSWORD', 'NET_ROOM_CODE', 'NET_DISPLAY_NAME'):
            settings.pop(name, None)
        settings.update(client_env)
        return arguments, settings

    def run_client(config, client_env, clients=1, duration=2):
        arguments, settings = client_start(config, client_env, clients, duration)
        return subprocess.run(arguments, cwd=BACKEND, env=settings, capture_output=True,
                              text=True, encoding='utf-8', timeout=20)

    try:
        command('docker', 'run', '--detach', '--name', CONTAINER,
                '--publish', '127.0.0.1::27017', 'mongo:8.0')
        container_created = True
        for _ in range(60):
            probe = subprocess.run(['docker', 'exec', CONTAINER, 'mongosh', '--quiet', '--eval',
                                    'quit(db.adminCommand({ping:1}).ok ? 0 : 1)'],
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if probe.returncode == 0:
                break
            time.sleep(0.25)
        else:
            raise RuntimeError('Test MongoDB did not become ready.')
        mongo_port = command('docker', 'port', CONTAINER, '27017/tcp').rsplit(':', 1)[1]
        env = os.environ.copy()
        env.update({'Mongo__ConnectionString': f'mongodb://127.0.0.1:{mongo_port}',
                    'Mongo__Database': DATABASE, 'Jwt__Issuer': 'JungleMaze.Local',
                    'Jwt__Audience': 'JungleMaze.Game', 'Jwt__SigningKey': key,
                    'Jwt__ExpiryMinutes': '60', 'ASPNETCORE_ENVIRONMENT': 'Production',
                    'DOTNET_ENVIRONMENT': 'Production', 'Swagger__Enabled': 'true'})
        start_api(env, 'api')
        check('health pings real MongoDB', request('GET', '/health')[0] == 200)
        ui_status, ui = request('GET', '/swagger/index.html')
        check('Swagger UI serves interactive page', ui_status == 200 and 'swagger-ui-bundle.js' in ui)
        bundle_status, bundle = request('GET', '/swagger/swagger-ui-bundle.js')
        check('Swagger UI JavaScript asset loads', bundle_status == 200 and 'SwaggerUIBundle' in bundle)
        spec_status, spec = request('GET', '/swagger/v1/swagger.json')
        check('OpenAPI document includes all four endpoints', spec_status == 200 and
              {'/api/auth/register', '/api/auth/login', '/api/me', '/health'} <= spec['paths'].keys())
        bearer = spec['components']['securitySchemes']['Bearer']
        check('Swagger Authorize uses HTTP Bearer JWT',
              bearer['type'] == 'http' and bearer['scheme'] == 'bearer' and bearer['bearerFormat'] == 'JWT')
        check('only me requires Swagger authorization',
              spec['paths']['/api/me']['get']['security'] == [{'Bearer': []}] and
              not spec['paths']['/api/auth/register']['post'].get('security') and
              not spec['paths']['/api/auth/login']['post'].get('security') and
              not spec['paths']['/health']['get'].get('security'))
        check('Swagger shows register and login examples',
              spec['components']['schemas']['RegisterRequest']['properties']['username']['example'] == 'player01' and
              spec['components']['schemas']['LoginRequest']['properties']['password']['example'] == 'ClassDemo_123!')
        check('Swagger documents real status codes',
              {'201', '400', '409'} <= spec['paths']['/api/auth/register']['post']['responses'].keys() and
              {'200', '400', '401'} <= spec['paths']['/api/auth/login']['post']['responses'].keys())
        credentials = {'username': '  PlayerOne  ', 'password': 'DemoPassword_123!'}
        status, user = request('POST', '/api/auth/register', credentials)
        check('register returns 201 with trimmed name', status == 201 and user['username'] == 'PlayerOne')
        check('response excludes password/hash/normalized name',
              set(user) == {'id', 'username', 'createdAtUtc'})
        check('case-insensitive duplicate returns 409', request('POST', '/api/auth/register',
              {'username': 'playerone', 'password': credentials['password']})[0] == 409)
        for label, data in [('missing fields', {}), ('trimmed short name', {'username': ' a ', 'password': 'ValidPass123'}),
                            ('short password', {'username': 'OtherName', 'password': '123'}),
                            ('null password', {'username': 'OtherName', 'password': None})]:
            check('register validation: ' + label, request('POST', '/api/auth/register', data)[0] == 400)
        check('empty login returns 400', request('POST', '/api/auth/login', {})[0] == 400)
        status, login = request('POST', '/api/auth/login',
                                {'username': ' playerONE ', 'password': credentials['password']})
        check('login normalized username returns correct identity', status == 200 and login['userId'] == user['id'])
        check('login response contract', set(login) == {'accessToken', 'userId', 'expiresAtUtc'})
        token = login['accessToken']
        claims = decode(token.split('.')[1])
        check('JWT contains correct claims and no password',
              claims['sub'] == user['id'] and claims['iss'] == 'JungleMaze.Local'
              and claims['aud'] == 'JungleMaze.Game' and {'iat', 'jti', 'exp', 'nbf'} <= claims.keys()
              and not ({'password', 'passwordHash', 'PasswordHash'} & claims.keys()))
        for name in ['PlayerOne', 'UnknownPlayer']:
            check('invalid credentials rejected: ' + name,
                  request('POST', '/api/auth/login', {'username': name, 'password': 'WrongPassword'})[0] == 401)
        status, me = request('GET', '/api/me', token=token)
        check('authorized me reads authenticated user', status == 200 and me == user)
        check('room API without control secret reports unavailable', request('GET', '/api/rooms', token=token)[0] == 503)
        check('room API still requires JWT when not configured', request('GET', '/api/rooms')[0] == 401)
        check('missing bearer token rejected', request('GET', '/api/me')[0] == 401)
        check('malformed token rejected', request('GET', '/api/me', token='not-a-jwt')[0] == 401)
        tampered = claims.copy()
        tampered['sub'] = 'forged-player'
        check('modified JWT payload rejected', request('GET', '/api/me',
              token=token.split('.')[0] + '.' + encode(tampered) + '.' + token.split('.')[2])[0] == 401)
        for name, replacement in [('wrong issuer', {'iss': 'OtherIssuer'}), ('wrong audience', {'aud': 'OtherGame'}),
                                  ('expired', {'exp': int(time.time()) - 120, 'nbf': int(time.time()) - 300}),
                                  ('future validity', {'nbf': int(time.time()) + 120}), ('missing expiry', {'exp': None})]:
            changed = claims.copy()
            for field, value in replacement.items():
                if value is None:
                    changed.pop(field, None)
                else:
                    changed[field] = value
            check('JWT validation: ' + name, request('GET', '/api/me', token=signed_token(changed, key))[0] == 401)
        check('non-HS256 algorithm rejected', request('GET', '/api/me',
              token=signed_token(claims, key, 'HS512'))[0] == 401)
        with ThreadPoolExecutor(max_workers=4) as pool:
            statuses = list(pool.map(lambda _: request('POST', '/api/auth/register',
                {'username': 'ConcurrentUser', 'password': 'ConcurrentPass123'})[0], range(4)))
        check('concurrent duplicate: one 201 and three 409', sorted(statuses) == [201, 409, 409, 409])
        check('password with spaces registers', request('POST', '/api/auth/register',
              {'username': 'SpaceUser', 'password': ' SpacePass123! '})[0] == 201)
        check('password spaces preserved', request('POST', '/api/auth/login',
              {'username': 'SpaceUser', 'password': ' SpacePass123! '})[0] == 200
              and request('POST', '/api/auth/login', {'username': 'SpaceUser', 'password': 'SpacePass123!'})[0] == 401)
        script = "const u=db.users.findOne({NormalizedUsername:'PLAYERONE'}); if(!u || u.PasswordHash==='DemoPassword_123!' || u.PasswordHash.length<40 || u.Password!==undefined) quit(1); if(!db.users.getIndexes().some(i=>i.name==='ux_users_normalized_username' && i.unique)) quit(2); if(db.users.countDocuments({NormalizedUsername:'CONCURRENTUSER'})!==1) quit(3); print('verified');"
        check('MongoDB stores hash and enforces unique index', command('docker', 'exec', CONTAINER,
              'mongosh', DATABASE, '--quiet', '--eval', script) == 'verified')
        stop_api()
        start_api(env, 'api-restarted')
        check('login survives API restart', request('POST', '/api/auth/login',
              {'username': 'PlayerOne', 'password': credentials['password']})[0] == 200)
        stop_api()
        disabled_env = env.copy()
        disabled_env['Swagger__Enabled'] = 'false'
        start_api(disabled_env, 'api-swagger-disabled')
        check('Swagger flag disables UI and OpenAPI without disabling API',
              request('GET', '/swagger/index.html')[0] == 404 and
              request('GET', '/swagger/v1/swagger.json')[0] == 404 and
              request('POST', '/api/auth/login', {'username': 'PlayerOne', 'password': credentials['password']})[0] == 200)
        stop_api()
        invalid_env = env.copy()
        invalid_env['Jwt__SigningKey'] = 'short-key'
        invalid = subprocess.run(['dotnet', str(DLL), '--urls', base_url], cwd=BACKEND,
                                 env=invalid_env, capture_output=True, timeout=10)
        check('weak signing key prevents startup', invalid.returncode != 0
              and b'Jwt__SigningKey' in invalid.stderr + invalid.stdout)

        # Xác minh token do chính API cấp dùng được trong UDP server, không chỉ token tự tạo trong unit test.
        start_api(env, 'api-network')
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp_probe:
            udp_probe.bind(('127.0.0.1', 0))
            game_port = udp_probe.getsockname()[1]
        room_code = 'TestRoom_' + secrets.token_hex(12)
        game_env = env.copy()
        game_env.update({'GAME_BIND_ADDRESS': '127.0.0.1', 'GAME_PORT': str(game_port),
                         'MAX_PLAYERS': '10', 'TICK_RATE': '30', 'AUTH_MODE': 'Jwt',
                         'ROOM_CODE': room_code, 'AUTH_TIMEOUT_MS': '5000', 'METRICS_INTERVAL_SECONDS': '1'})
        start_game(game_env, 'game-jwt')
        client_config = {'mode': 'Local', 'apiBaseUrl': base_url, 'gameServerHost': '127.0.0.1',
                         'gameServerPort': game_port, 'authMode': 'Jwt', 'protocolVersion': 1, 'connectTimeoutMs': 5000}
        client_env = {'NET_ROOM_CODE': room_code, 'NET_USERNAME': 'PlayerOne', 'NET_PASSWORD': credentials['password']}
        cross = run_client(client_config, client_env)
        check('client login API -> signed JWT -> UDP JoinAccepted -> Pong', cross.returncode == 0
              and '[login]' in cross.stdout and ('playerId=' + user['id']) in cross.stdout and 'Pong sequence=' in cross.stdout)
        wrong_password = run_client(client_config, dict(client_env, NET_PASSWORD='WrongPassword'))
        check('client login failure stops before UDP, no RoomCode fallback', wrong_password.returncode == 1
              and 'HTTP 401' in wrong_password.stderr and 'transportConnected' not in wrong_password.stdout)
        wrong_room = run_client(client_config, dict(client_env, NET_ROOM_CODE='Wrong_Room'))
        check('JWT alone cannot bypass room code', wrong_room.returncode == 1 and 'InvalidRoomCode' in wrong_room.stderr)

        held_log_path = LOG_DIR / 'held-client.log'
        held_log = held_log_path.open('w', encoding='utf-8')
        held_args, held_env = client_start(client_config, client_env, duration=8)
        held_client = subprocess.Popen(held_args, cwd=BACKEND, env=held_env,
                                       stdout=held_log, stderr=subprocess.STDOUT)
        for _ in range(100):
            if 'JoinAccepted' in held_log_path.read_text(encoding='utf-8'):
                break
            if held_client.poll() is not None:
                raise RuntimeError('Held client did not join.')
            time.sleep(0.1)
        else:
            raise RuntimeError('Held client timed out.')
        duplicated = run_client(client_config, client_env)
        check('real API account cannot join twice', duplicated.returncode == 1 and 'DuplicateSession' in duplicated.stderr)
        held_client.wait(timeout=12)
        held_client = None
        held_log.close()
        held_log = None
        retry = run_client(client_config, client_env)
        check('real client disconnect releases user mapping for next join', retry.returncode == 0 and 'JoinAccepted' in retry.stdout)
        expired_claims = dict(claims, exp=int(time.time()) - 120, nbf=int(time.time()) - 300)
        expired_client = run_client(client_config, dict(client_env, NET_ACCESS_TOKEN=signed_token(expired_claims, key)))
        check('UDP rejects expired token with no fallback', expired_client.returncode == 1 and 'InvalidToken' in expired_client.stderr)
        stop_game()
        stop_api()

        # RoomCode vẫn chạy khi không có API và không có signing key.
        game_env.update({'AUTH_MODE': 'RoomCode', 'Jwt__SigningKey': ''})
        start_game(game_env, 'game-room')
        room_config = dict(client_config, authMode='RoomCode', apiBaseUrl=None)
        room_clients = run_client(room_config, {'NET_ROOM_CODE': room_code}, clients=10, duration=3)
        check('10 RoomCode clients join/pong with API stopped and no JWT key', room_clients.returncode == 0
              and 'joined=10 failed=0' in room_clients.stdout)
        room_capacity = run_client(room_config, {'NET_ROOM_CODE': room_code}, clients=11, duration=3)
        check('client tool shows 10 accepted and 11th RoomFull', room_capacity.returncode == 1
              and 'joined=10 failed=1' in room_capacity.stdout and 'RoomFull' in room_capacity.stderr)
        captured = cross.stdout + cross.stderr + (LOG_DIR / 'game-jwt.log').read_text(encoding='utf-8')
        check('network logs do not expose room/password/signing key/token',
              all(secret not in captured for secret in (room_code, credentials['password'], key, token)))
        stop_game()
        missing_jwt_key = dict(game_env, AUTH_MODE='Jwt', Jwt__SigningKey='')
        missing_key_process = subprocess.run(['dotnet', str(GAME_DLL)], cwd=BACKEND,
                                             env=missing_jwt_key, capture_output=True, timeout=10)
        check('JWT GameServer refuses startup without signing key', missing_key_process.returncode == 1
              and b'Jwt' in missing_key_process.stderr)
        print(f'{passed} checks passed; isolated API, GameServer, clients and MongoDB will be cleaned up.', flush=True)
    finally:
        if held_client is not None and held_client.poll() is None:
            held_client.terminate()
            held_client.wait(timeout=10)
        if held_log is not None:
            held_log.close()
        stop_game()
        stop_api()
        if container_created:
            subprocess.run(['docker', 'rm', '--force', '--volumes', CONTAINER], check=True,
                           stdout=subprocess.DEVNULL)


if __name__ == '__main__':
    main()
