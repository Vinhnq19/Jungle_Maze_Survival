"""Transfer verified application images without retransmitting unchanged runtime layers.

Image config bytes and uncompressed layer SHA256 remain identical to the full release.
Restore exports ONLY the pinned existing base images; missing/mismatched bases fail closed.
The resulting standard bundle still goes through deploy.sh and its complete checksums.
"""
import argparse, gzip, hashlib, io, json, subprocess, tarfile, tempfile
from pathlib import Path, PurePosixPath

def sha(data): return hashlib.sha256(data).hexdigest()
def raw_layer(data): return gzip.decompress(data) if data[:2]==b'\x1f\x8b' else data
def docker_json(*args):return json.loads(subprocess.check_output(['docker',*args],text=True))
def put(tar,name,data):
    info=tarfile.TarInfo(name);info.size=len(data);info.mode=0o644;tar.addfile(info,io.BytesIO(data))
def safe(name):
    p=PurePosixPath(name)
    if p.is_absolute() or '..' in p.parts:raise ValueError('Unsafe archive path')
    return str(p)

def verify(args):
    with tarfile.open(args.archive,'r:gz') as tar:
        files={safe(m.name):tar.extractfile(m).read() for m in tar if m.isfile()}
    assert sha(Path(args.archive).read_bytes())==Path(str(args.archive)+'.sha256').read_text().split()[0]
    idx=json.loads(files['delta/index.json'])
    assert idx['originalReleaseSha256']==sha(Path(args.release).read_bytes())
    assert files['delta/restore.py']==Path(__file__).read_bytes(),'Rebuild delta after changing its restore tool'
    for name,h in idx['files'].items():assert sha(files[name])==h
    for name,h in idx['payload'].items():assert sha(files['delta/'+name])==h
    with tarfile.open(args.release,'r:gz') as source:
        manifest=json.load(source.extractfile('./manifest.json'))
    assert {(i['sourceId'],tuple(i['tags'])) for i in idx['images']}=={(i['id'],(i['name'],)) for i in manifest['images']}
    print('PASS delta checksum, full-release binding, payload files, pinned source images and restore source.')

def build(args):
    source=Path(args.release)
    assert sha(source.read_bytes())==Path(str(source)+'.sha256').read_text().split()[0]
    bases=docker_json('image','inspect',*args.base)
    known={layer for image in bases for layer in image['RootFS']['Layers']}
    with tarfile.open(source,'r:gz') as release:
        files={safe(m.name):release.extractfile(m).read() for m in release if m.isfile()}
    source_images={i['name']:i['id'] for i in json.loads(files['manifest.json'])['images']}
    original_sums=dict((name,h) for h,name in (line.split('  ',1) for line in files['SHA256SUMS'].decode().splitlines()))
    assert all(sha(files[n])==h for n,h in original_sums.items())
    payload={};images=[];required=set()
    with tarfile.open(fileobj=io.BytesIO(files.pop('images.tar.gz')),mode='r:gz') as archive:
        manifest=json.load(archive.extractfile('manifest.json'))
        for item in manifest:
            config=archive.extractfile(item['Config']).read(); cfg=json.loads(config)
            config_name=sha(config)+'.json';payload['configs/'+config_name]=config
            layers=cfg['rootfs']['diff_ids'];assert len(layers)==len(item['Layers'])
            for diff_id,path in zip(layers,item['Layers']):
                if diff_id in known:required.add(diff_id);continue
                data=raw_layer(archive.extractfile(path).read());assert 'sha256:'+sha(data)==diff_id
                payload['layers/'+diff_id[7:]+'.tar']=data
            images.append(dict(config=config_name,id='sha256:'+sha(config),sourceId=source_images[item['RepoTags'][0]],tags=item['RepoTags'],layers=layers))
    index=dict(format=1,originalReleaseSha256=sha(source.read_bytes()),
        bases=[dict(id=i['Id'],layers=i['RootFS']['Layers']) for i in bases],
        required=sorted(required),images=images,
        files={n:sha(d) for n,d in files.items()},payload={n:sha(d) for n,d in payload.items()})
    files.update({'delta/'+n:d for n,d in payload.items()})
    files['delta/index.json']=json.dumps(index,indent=2).encode()
    files['delta/restore.py']=Path(__file__).read_bytes()
    target=Path(args.output)
    with tarfile.open(target,'w:gz') as tar:
        for n,d in sorted(files.items()):put(tar,n,d)
    Path(str(target)+'.sha256').write_text(sha(target.read_bytes())+'  '+target.name+'\n')
    print(json.dumps(dict(bytes=target.stat().st_size,baseLayersReused=len(required),newLayers=len(payload)-len(images),sha256=sha(target.read_bytes()))))

def restore(args):
    root=Path(args.directory).resolve();idx=json.loads((root/'delta/index.json').read_text())
    assert idx['format']==1
    for name,expected in idx['files'].items():assert sha((root/safe(name)).read_bytes())==expected,name
    for name,expected in idx['payload'].items():assert sha((root/'delta'/safe(name)).read_bytes())==expected,name
    # Every required layer must come from these exact existing images, never an arbitrary tag.
    bases=docker_json('image','inspect',*[b['id'] for b in idx['bases']])
    assert {b['Id'] for b in bases}=={b['id'] for b in idx['bases']}
    required=set(idx['required']);found={}
    with tempfile.TemporaryDirectory(prefix='jungle-image-delta-') as scratch:
        scratch=Path(scratch)
        proc=subprocess.Popen(['docker','save',*[b['id'] for b in idx['bases']]],stdout=subprocess.PIPE)
        try:
            with tarfile.open(fileobj=proc.stdout,mode='r|') as saved:
                for member in saved:
                    if not member.isfile():continue
                    # Docker save can use OCI compressed blobs or legacy layer.tar.
                    data=saved.extractfile(member).read()
                    if not (member.name.endswith('/layer.tar') or '/blobs/' in '/'+member.name):continue
                    raw=raw_layer(data);key='sha256:'+sha(raw)
                    if key in required and key not in found:
                        p=scratch/(key[7:]+'.tar');p.write_bytes(raw);found[key]=p
            if proc.wait()!=0:raise RuntimeError('Export of base images failed')
        finally:
            if proc.poll() is None:proc.kill();proc.wait()
        assert required<=found.keys(),'Existing runtime layer missing; use full release instead'
        target=root/'images.tar.gz'
        with target.open('wb') as output, gzip.GzipFile(filename='',fileobj=output,mode='wb',compresslevel=1,mtime=0) as compressed, tarfile.open(fileobj=compressed,mode='w|') as tar:
            manifest=[];written=set()
            for image in idx['images']:
                config=(root/'delta/configs'/image['config']).read_bytes()
                assert 'sha256:'+sha(config)==image['id']
                put(tar,image['config'],config)
                paths=[]
                for layer in image['layers']:
                    name=layer[7:]+'/layer.tar';paths.append(name)
                    if layer in written:continue
                    path=found.get(layer,root/'delta/layers'/(layer[7:]+'.tar'))
                    data=path.read_bytes();assert 'sha256:'+sha(data)==layer
                    put(tar,name,data);written.add(layer)
                manifest.append(dict(Config=image['config'],RepoTags=image['tags'],Layers=paths))
            put(tar,'manifest.json',json.dumps(manifest).encode())
        subprocess.run(['docker','load','-i',str(target)],check=True)
        manifest_path=root/'manifest.json';release=json.loads(manifest_path.read_text())
        restored=[]
        for image in idx['images']:
            expected=json.loads((root/'delta/configs'/image['config']).read_text())['config']
            for tag in image['tags']:
                actual=docker_json('image','inspect',tag)[0]
                assert actual['RootFS']['Layers']==image['layers'],'Root filesystem differs'
                assert all(actual['Config'].get(k)==v or k=='ArgsEscaped' and not v and k not in actual['Config'] for k,v in expected.items()),'Runtime configuration differs'
                for record in release['images']:
                    if record['name']==tag:record['id']=actual['Id']
                restored.append(dict(tag=tag,sourceImageId=image['sourceId'],restoredImageId=actual['Id'],configSha256=image['id'],identicalRootFsAndRuntimeConfig=True))
        # OCI -> legacy repackaging can change manifest IDs without changing config/RootFS.
        release['deltaProvenance']=dict(originalReleaseSha256=idx['originalReleaseSha256'],images=restored)
        manifest_path.write_text(json.dumps(release,indent=2)+'\n')
        sums=(root/'SHA256SUMS').read_text().splitlines()
        changed={'images.tar.gz':sha(target.read_bytes()),'manifest.json':sha(manifest_path.read_bytes())}
        sums=[changed.get(line.split('  ',1)[1],line.split('  ',1)[0])+'  '+line.split('  ',1)[1] for line in sums]
        (root/'SHA256SUMS').write_text('\n'.join(sums)+'\n')
    print(json.dumps(dict(restored=restored,verification='Identical application/runtime layers and runtime configuration; manifest IDs may differ after repackaging.')))

if __name__=='__main__':
    p=argparse.ArgumentParser();sub=p.add_subparsers(dest='action',required=True)
    b=sub.add_parser('build');b.add_argument('--release',required=True);b.add_argument('--base',nargs='+',required=True);b.add_argument('--output',required=True)
    r=sub.add_parser('restore');r.add_argument('directory')
    v=sub.add_parser('verify');v.add_argument('--release',required=True);v.add_argument('--archive',required=True)
    args=p.parse_args();{'build':build,'restore':restore,'verify':verify}[args.action](args)
