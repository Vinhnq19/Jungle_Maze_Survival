"""Kiểm tra bundle gửi EC2 mà không giải nén hoặc đọc config riêng của workspace."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import tarfile


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('archive', type=Path)
    parser.add_argument('--apps-only',action='store_true',help='Require a minimal release for the controlled AWS room test.')
    args = parser.parse_args()
    archive = args.archive
    with archive.open('rb') as source:
        actual = hashlib.file_digest(source, 'sha256').hexdigest()
    expected = Path(str(archive) + '.sha256').read_text().split()[0]
    assert actual == expected, 'Archive checksum mismatch'
    with tarfile.open(archive, 'r:gz') as bundle:
        files = {}
        for member in bundle.getmembers():
            path = PurePosixPath(member.name)
            assert not path.is_absolute() and '..' not in path.parts, 'Unsafe archive path'
            assert member.isdir() or member.isfile(), 'Unexpected link/special file'
            if member.isfile():
                files[str(path)] = member
        checksums = bundle.extractfile(files['SHA256SUMS']).read().decode().splitlines()
        expected_files = {}
        for line in checksums:
            digest, filename = line.split('  ', 1)
            expected_files[filename] = digest
        assert set(files) == set(expected_files) | {'SHA256SUMS'}, 'Unlisted/missing bundle file'
        for filename, digest in expected_files.items():
            with bundle.extractfile(files[filename]) as source:
                assert hashlib.file_digest(source, 'sha256').hexdigest() == digest, filename
        assert {'compose.aws.yaml', 'Caddyfile', 'release.env', 'manifest.json', 'images.tar.gz',
                'client.aws.json', 'scripts/deploy.sh', 'scripts/bootstrap.sh'} <= set(files)
        for filename in files:
            assert PurePosixPath(filename).name not in ('.env', '.env.aws'), 'Secret config in bundle'
            assert not filename.endswith(('.pem', '.pfx', '.key', '.archive.gz')), 'Private/DB file in bundle'
            assert filename == 'images.tar.gz' or filename == 'SHA256SUMS' or filename.startswith(('docs/', 'scripts/')) \
                or filename in ('compose.aws.yaml', 'Caddyfile', 'release.env', 'manifest.json', 'client.aws.json','client.aws-auto.json','client.phase2-aws.json')
            if filename != 'images.tar.gz':
                raw = bundle.extractfile(files[filename]).read()
                assert b'\r' not in raw and not raw.startswith(b'\xef\xbb\xbf'), 'Text must use LF/UTF8 without BOM'
                raw.decode('utf-8')
        manifest = json.load(bundle.extractfile(files['manifest.json']))
        assert not args.apps_only or manifest.get('appsOnly'), 'Room test requires an apps-only bundle'
        assert len(manifest['images']) == (2 if manifest.get('appsOnly') else 4)
        assert all(item['os'] == 'linux' and item['architecture'] == 'amd64' for item in manifest['images'])
        assert manifest['publicGameAuth'] == 'RoomCode'
        assert manifest['mongoVersion'] == ('reuse-existing-7' if manifest.get('appsOnly') else '7.0.43')
        client = json.load(bundle.extractfile(files['client.aws.json']))
        assert client['gameServerHost'] == manifest['domain'] and manifest['domain'].endswith('.duckdns.org')
        assert client['apiBaseUrl'] == 'https://' + manifest['domain']
        if 'client.aws-auto.json' in files:
            auto=json.load(bundle.extractfile(files['client.aws-auto.json']))
            assert auto['transport']=='Auto' and auto['protocolVersion']==2 and auto['webSocketUrl']=='wss://'+manifest['domain']+'/game'
            proxy=bundle.extractfile(files['Caddyfile']).read().decode()
            assert 'handle /game' in proxy and 'reverse_proxy game-server:7781' in proxy
            compose=bundle.extractfile(files['compose.aws.yaml']).read().decode()
            assert 'WS_ENABLED:' in compose and ':7781:7781' not in compose
        if 'client.phase2-aws.json' in files:
            unity=json.load(bundle.extractfile(files['client.phase2-aws.json']))
            assert unity['protocolVersion']==3 and unity['transport']=='Auto'
            assert unity['apiBaseUrl']=='https://'+manifest['domain']
            assert unity['webSocketUrl']=='wss://'+manifest['domain']+'/game'
        # Đọc archive docker save bên trong, không extract filesystem hoặc giữ thêm bản 500MB.
        tags = set()
        configurations = {}
        image_manifest = None
        with bundle.extractfile(files['images.tar.gz']) as source, tarfile.open(fileobj=source, mode='r|gz') as images:
            for member in images:
                if not member.isfile() or member.size > 128 * 1024:
                    continue
                try:
                    value = json.load(images.extractfile(member))
                except (ValueError, UnicodeDecodeError):
                    continue
                if member.name == 'manifest.json':
                    image_manifest = value
                if isinstance(value, dict) and 'architecture' in value and 'os' in value:
                    configurations[member.name] = value
        assert image_manifest is not None, 'Missing docker save manifest'
        for image in image_manifest:
            tags.update(image.get('RepoTags') or [])
            config = configurations[image['Config']]
            assert config['os'] == 'linux' and config['architecture'] == 'amd64'
            assert not any(item.startswith(('ROOM_CODE=', 'JWT_SIGNING_KEY=', 'DUCKDNS_TOKEN=', 'Jwt__SigningKey='))
                           for item in config.get('config', {}).get('Env', [])), 'Private env baked into image'
        assert tags == {image['name'] for image in manifest['images']}, 'Wrong/missing saved Docker images'
    print(f'PASS release: {len(files)} files, all checksums, {len(manifest["images"])} Linux AMD64 images, no private config, LF scripts.')
    print('SHA256: ' + actual)


if __name__ == '__main__':
    main()
