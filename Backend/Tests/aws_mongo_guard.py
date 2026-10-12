"""Chạy guard Bash thật bằng Docker CLI Linux; chỉ dùng volume test ngẫu nhiên.

Cần images mongo:7.0.43 và docker:29-cli. Không đọc DB/config của team.
"""
import json
from pathlib import Path
import secrets
import subprocess

BACKEND = Path(__file__).resolve().parents[1]


def run(*args, okay=True):
    result = subprocess.run(args, capture_output=True)
    if okay and result.returncode:
        raise RuntimeError(result.stderr.decode(errors='replace'))
    return result


def main():
    project = 'jungle-volume-guard-' + secrets.token_hex(6)
    folder = BACKEND.parent / '.utmp/aws-tests' / project
    folder.mkdir(parents=True)
    cli = project + '-cli'
    volume = project + '-data'
    image = 'mongo:7.0.43'
    source = BACKEND / 'Deploy/Aws/scripts/check-mongo-volume.sh'
    (folder / source.name).write_text(source.read_text(), encoding='utf-8', newline='\n')
    checks = 0

    def check(name, condition):
        nonlocal checks
        assert condition, name
        checks += 1
        print('PASS ' + name, flush=True)

    def guard(previous='8'):
        return run('docker', 'run', '--rm', '--network', 'none', '--entrypoint', 'bash',
                   '-v', '/var/run/docker.sock:/var/run/docker.sock', '-v', str(folder) + ':/checks:ro',
                   image, '-c', '/checks/docker version >/dev/null && PATH=/checks:$PATH bash /checks/check-mongo-volume.sh "$@"',
                   '--', volume, image, previous, okay=False)

    def read_marker():
        return run('docker', 'run', '--rm', '--network', 'none', '--entrypoint', 'bash',
                   '--mount', f'type=volume,source={volume},target=/check,readonly', image,
                   '-c', 'cat /check/marker').stdout

    try:
        run('docker', 'create', '--name', cli, 'docker:29-cli')
        run('docker', 'cp', cli + ':/usr/local/bin/docker', str(folder / 'docker'))
        run('docker', 'rm', '-v', cli)
        check('Missing volume allowed for first deploy', guard().returncode == 0)
        check('Guard does not create missing volume', run('docker', 'volume', 'inspect', volume, okay=False).returncode != 0)
        run('docker', 'volume', 'create', volume)
        check('Existing empty volume permits 8 -> 7', guard().returncode == 0)
        run('docker', 'run', '--rm', '--network', 'none', '--entrypoint', 'bash',
            '--mount', f'type=volume,source={volume},target=/check', image,
            '-c', 'printf test-data > /check/marker')
        blocked = guard()
        check('Existing nonempty volume blocks 8 -> 7', blocked.returncode == 1 and b'Refusing' in blocked.stderr)
        check('Refused deploy leaves data untouched', read_marker() == b'test-data')
        check('Existing MongoDB 7 data allows patch deploy', guard('7').returncode == 0)
        check('Patch guard leaves data untouched', read_marker() == b'test-data')
        check('Invalid previous major rejected', guard('unknown').returncode == 1)
        print(f'PASS MongoDB guard: {checks} checks.', flush=True)
    finally:
        # Chỉ dọn container/volume do bài này đặt tên bằng UUID; không chứa tài nguyên team.
        run('docker', 'rm', '-fv', cli, okay=False)
        run('docker', 'volume', 'rm', volume, okay=False)
        (folder / 'docker').unlink(missing_ok=True)


if __name__ == '__main__':
    main()
