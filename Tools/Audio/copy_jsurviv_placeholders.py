"""Copy the JSurviv clips used as AUD-01 placeholders into Art/Audio under ASCII names.

Copies the audio file only, never its .meta, so Unity gives the copy a new GUID and the original
under Assets/Imported/JSurviv/ stays untouched. Waiting Room Music.wav (25.7 MB, uncompressed) is
deliberately skipped until ART-05 compresses it (AUD-01 T-5).

Usage: python -I Tools/Audio/copy_jsurviv_placeholders.py [--check]
"""
from pathlib import Path
import argparse
import filecmp
import shutil
import sys

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Assets/Imported/JSurviv/Sound'
TARGET = ROOT / 'Assets/_JungleMaze/Art/Audio/Placeholder/JSurviv'

# (source relative to SOURCE, target relative to TARGET, SfxId or music track using it)
CLIPS = [
    ('melee chém.mp3', 'melee_slash.mp3', 'MeleeSwing'),
    ('Đập.wav', 'hit_thud.wav', 'MeleeHit'),
    ('súng bắn.mp3', 'gun_shot.mp3', 'ShootRanged'),
    ('countdown.mp3', 'bomb_fuse_countdown.mp3', 'BombFuse'),
    ('bomb nổ.mp3', 'bomb_explosion.mp3', 'Explosion'),
    ('bleed.mp3', 'hurt_bleed.mp3', 'HurtLocal'),
    ('nhặt súng.mp3', 'pickup_weapon.mp3', 'PickupWeapon'),
    ('Nhặt đồ/Gun Put On.mp3', 'pickup_item_gun_put_on.mp3', 'PickupItem'),
    ('Nhặt đồ/Helmet Put On.mp3', 'wear_armor_helmet_put_on.mp3', 'WearArmor'),
    ('gauze.mp3', 'use_heal_gauze.mp3', 'UseHeal'),
    ('cola.mp3', 'use_drink_cola.mp3', 'UseDrink'),
    ('TimeOver.wav', 'match_end_time_over.wav', 'MatchEnd'),
    ('Loading Music.mp3', 'Music/loading_music.mp3', 'Music: Loading, WaitingRoom (T-5)'),
    ('ParkingMapMusic.mp3', 'Music/parking_map_music.mp3', 'Music: Match'),
]


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('--check', action='store_true', help='only verify the copies match their sources')
    args = parser.parse_args()
    failed = False
    for src_rel, dst_rel, used_by in CLIPS:
        src, dst = SOURCE / src_rel, TARGET / dst_rel
        if not src.is_file():
            print(f'MISSING  {src}')
            failed = True
            continue
        if args.check:
            same = dst.is_file() and filecmp.cmp(src, dst, shallow=False)
            print(f'{"OK      " if same else "DIFFERS "} {dst_rel}')
            failed |= not same
            continue
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dst)
        print(f'{used_by:34} {dst.relative_to(ROOT)}')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
