"""Validate every encoded UI-03 export and challenge the validation contracts.

Run: approved Python -I -X utf8 Tools/Art/test_ui03_overlay_validation.py
Writes only Docs/Art/Production_A/MatchOverlay/Validation.json.
"""
import hashlib
import json
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from PIL import Image, ImageDraw
from native32_pipeline import palette, validate

ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'Docs/Art/Production_A/MatchOverlay'
_, RGB=palette()


def validate_image(im,spec):
    rgba=im.convert('RGBA'); data=list(rgba.get_flattened_data())
    bbox=rgba.getchannel('A').getbbox()
    checks=dict(size=im.size==tuple(spec['size']),rgba=im.mode=='RGBA',
                binary_alpha={p[3] for p in data}<={0,255},nonempty=bbox is not None)
    mask=spec['kind']=='white mask'
    colors={p[:3] for p in data if p[3]}
    checks['palette']=colors<={(255,255,255)} if mask else colors<=set(RGB)
    checks['color_count']=0<len(colors)<=(1 if mask else 8)
    checks['white_mask']=all(p[:3]==(255,255,255) for p in data if p[3]) if mask else True
    if bbox:
        x0,y0,x1,y1=bbox; w,h=rgba.size
        # Grid intentionally reaches two edges; all other masks have symmetric
        # authored padding. Colored icons require >=3px at export, <=1px offset.
        grid=Path(spec['path']).stem=='KillcamGrid'
        checks['padding']=True if grid else min(x0,y0,w-x1,h-y1)>=(1 if mask else 3)
        checks['centering']=True if grid else max(abs((x0+x1-w)/2),abs((y0+y1-h)/2))<=1
    else:
        checks['padding']=checks['centering']=False
    if not mask and im.size==(64,64):
        result=validate(im,RGB)['checks']
        checks['native32_x2']=result['b']
        checks['content_52_to_56px']=result['e']
        checks['ink_outline_1px']=result['f']
    elif not mask:
        checks['native32_x2']=checks['content_52_to_56px']=checks['ink_outline_1px']=False
    return checks


def validate_geometry(name,im):
    """Independent geometry fixtures for the specified masks, including seams."""
    alpha=im.getchannel('A')
    if name=='KillcamGrid':
        return all(alpha.getpixel((x,y))==(255 if x==0 or y==0 else 0)
                   for y in range(48) for x in range(48))
    if name=='ZoneRing':
        expected=Image.new('L',(256,256)); ImageDraw.Draw(expected).ellipse((4,4,251,251),outline=255,width=4)
        return alpha.tobytes()==expected.tobytes()
    if name=='ToggleBox':
        return alpha.crop((12,12,36,36)).getbbox() is None and alpha.getpixel((4,24))==255
    if name in ('PlayerDot','PlayerDotOutline'):
        expected=Image.new('L',(32,32)); d=ImageDraw.Draw(expected)
        if name=='PlayerDot': d.ellipse((5,5,26,26),fill=255)
        else:
            d.ellipse((4,4,27,27),fill=255); d.ellipse((5,5,26,26),fill=0)
        return expected.tobytes()==alpha.tobytes()
    return True


class ValidatorTests(unittest.TestCase):
    def setUp(self):
        self.spec=dict(size=[64,64],kind='colored icon',path='Fixture.png')
        native=Image.new('RGBA',(32,32)); d=ImageDraw.Draw(native)
        d.rectangle((2,2,29,29),fill=(*RGB[0],255)); d.rectangle((3,3,28,28),fill=(*RGB[6],255))
        self.im=native.resize((64,64),Image.Resampling.NEAREST)

    def test_valid_fixture(self): self.assertTrue(all(validate_image(self.im,self.spec).values()))

    def test_size_mode(self):
        self.assertFalse(validate_image(self.im.resize((32,32)),self.spec)['size'])
        self.assertFalse(validate_image(self.im.convert('RGB'),self.spec)['rgba'])

    def test_alpha(self):
        self.im.putpixel((12,12),(*RGB[6],128))
        self.assertFalse(validate_image(self.im,self.spec)['binary_alpha'])

    def test_palette_and_color_count(self):
        self.im.putpixel((12,12),(255,0,255,255))
        self.assertFalse(validate_image(self.im,self.spec)['palette'])
        for i,c in enumerate(RGB[:9]): self.im.putpixel((12+i,14),(*c,255))
        self.assertFalse(validate_image(self.im,self.spec)['color_count'])

    def test_grid_blocks_and_outline(self):
        self.im.putpixel((12,12),(*RGB[8],255))
        self.assertFalse(validate_image(self.im,self.spec)['native32_x2'])
        ImageDraw.Draw(self.im).rectangle((4,10,5,11),fill=(*RGB[6],255))
        self.assertFalse(validate_image(self.im,self.spec)['ink_outline_1px'])

    def test_padding_center_empty(self):
        shifted=Image.new('RGBA',(64,64)); shifted.paste(self.im,(4,0))
        checks=validate_image(shifted,self.spec)
        self.assertFalse(checks['padding']); self.assertFalse(checks['centering'])
        self.assertFalse(validate_image(Image.new('RGBA',(64,64)),self.spec)['nonempty'])

    def test_white_mask(self):
        spec=dict(size=[32,32],kind='white mask',path='Mask.png')
        im=Image.new('RGBA',(32,32)); ImageDraw.Draw(im).rectangle((4,4,27,27),fill='white')
        self.assertTrue(all(validate_image(im,spec).values()))
        im.putpixel((8,8),(254,255,255,255))
        self.assertFalse(validate_image(im,spec)['white_mask'])

    def test_grid_topology_and_empty_toggle(self):
        self.assertFalse(validate_geometry('KillcamGrid',Image.new('RGBA',(48,48),'white')))
        self.assertFalse(validate_geometry('ToggleBox',Image.new('RGBA',(48,48),'white')))


def main():
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(ValidatorTests)
    tests=unittest.TextTestRunner(verbosity=2).run(suite)
    mapping=json.loads((DOC/'asset_mapping.json').read_text(encoding='utf-8'))
    expected={'SliderHandle','ToggleBox','ToggleCheck','ZoneRing','KillcamGrid','PlayerDot',
              'PlayerDotOutline','KillerMark','CausePlayer','CauseZone','CauseLeft','CauseDisconnect',
              'CauseUnknown','IconVolume','IconMusic','IconMouse','IconShake','IconExit','IconSpectate'}
    assert {Path(s['path']).stem for s in mapping['assets']}==expected
    rows=[]
    for spec in mapping['assets']:
        path=ROOT/spec['path']; name=path.stem
        with Image.open(path) as im:
            checks=validate_image(im,spec); checks['mask_geometry']=validate_geometry(name,im)
            bbox=im.getchannel('A').getbbox()
            colors={p[:3] for p in im.get_flattened_data() if p[3]}
            if spec['kind']!='white mask':
                with Image.open(DOC/f'{name}_native32.png') as native:
                    checks['native_source_matches']=native.size==(32,32) and native.resize((64,64),Image.Resampling.NEAREST).tobytes()==im.tobytes()
            rows.append(dict(path=spec['path'],checks=checks,passed=all(checks.values()),
                             bbox=bbox,color_count=len(colors),sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        print(('PASS ' if rows[-1]['passed'] else 'FAIL ')+name)
    reuse_unchanged=all(hashlib.sha256((ROOT/r['path']).read_bytes()).hexdigest()==r['sha256'] for r in mapping['reuse'])
    result=dict(passed=tests.wasSuccessful() and all(r['passed'] for r in rows) and reuse_unchanged,
                files=len(rows),validator_tests=tests.testsRun,validator_tests_passed=tests.wasSuccessful(),
                reuse_unchanged=reuse_unchanged,assets=rows,
                exceptions={'KillcamGrid':'Top/left tile edges intentionally have zero padding and asymmetric coverage.'},
                unity='Not run; import settings are handoff metadata, not verified importer state.')
    (DOC/'Validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f"{'PASS' if result['passed'] else 'FAIL'}: {len(rows)}/19 exports; {tests.testsRun} validator tests; reuse unchanged={reuse_unchanged}")
    raise SystemExit(0 if result['passed'] else 1)


if __name__=='__main__': main()
