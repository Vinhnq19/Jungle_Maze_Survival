"""Static checks against the untouched source and original project settings."""
from pathlib import Path
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'JSurvivUnity/Assets'
DEST = ROOT / 'Assets/Imported/JSurviv'


def main():
    def files(relative):
        output = subprocess.check_output(['rg', '--files', '--hidden', '--no-ignore', relative], cwd=ROOT)
        prefix = relative.replace('\\', '/') + '/'
        return {line.replace('\\', '/')[len(prefix):] for line in output.decode('utf-8').splitlines()}

    def guids(relative):
        output = subprocess.check_output(['rg', '--with-filename', '--no-heading', '--no-ignore', '^guid: ', relative, '-g', '*.meta'], cwd=ROOT)
        prefix = relative.replace('\\', '/') + '/'
        result = {}
        for line in output.decode('utf-8').splitlines():
            path, value = line.rsplit(':guid: ', 1)
            result[path.replace('\\', '/')[len(prefix):]] = value.strip()
        return result

    source_files = files('JSurvivUnity/Assets')
    dest_files = files('Assets/Imported/JSurviv')
    missing = sorted(source_files - dest_files)
    source_guids, dest_guids = guids('JSurvivUnity/Assets'), guids('Assets/Imported/JSurviv')
    changed_guids = [path for path, value in source_guids.items() if dest_guids.get(path) != value]
    all_asset_guids = guids('Assets')
    original_guids = {value: path for path, value in all_asset_guids.items()
                      if not path.startswith('Imported/JSurviv/')}
    collisions = [{'imported': path, 'original': original_guids[value], 'guid': value}
                  for path, value in dest_guids.items() if value in original_guids]
    changed_scenes_prefabs = [path for path in sorted(source_files & dest_files)
                             if path.endswith(('.unity', '.prefab')) and
                             (SOURCE / path).read_bytes() != (DEST / path).read_bytes()]
    protected = ['ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset']
    preserved = {}
    for relative in protected:
        before = ROOT / 'Library/JSurvivIntegration/Backup' / relative
        preserved[relative] = before.exists() and before.read_bytes() == (ROOT / relative).read_bytes()
    manifest = json.loads((ROOT / 'Packages/manifest.json').read_text())['dependencies']
    profiles = list((DEST / 'Settings/Build Profiles').glob('*.asset'))
    stale_profiles = [str(path.relative_to(ROOT)) for path in profiles if re.search(
        r'm_path: Assets/(?!Imported/JSurviv/)', path.read_text(encoding='utf-8-sig'))]
    result = {'sourceFileCount': len(source_files), 'sourceMetaCount': sum(path.endswith('.meta') for path in source_files),
              'missingCopiedFiles': missing, 'changedSourceGuids': changed_guids,
              'originalAssetGuidCollisions': collisions, 'protectedSettingsPreserved': preserved,
              'changedSourceScenesOrPrefabs': changed_scenes_prefabs,
              'staleBuildProfilePaths': stale_profiles, 'cinemachine': manifest['com.unity.cinemachine'],
              'socketIO': manifest['com.itisnajim.socketiounity'], 'newtonsoft': manifest['com.unity.nuget.newtonsoft-json']}
    output = ROOT / 'Library/JSurvivIntegration/static-validation.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result))
    if missing or changed_guids or collisions or changed_scenes_prefabs or stale_profiles or not all(preserved.values()):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
