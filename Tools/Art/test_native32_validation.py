"""Adversarial contract checks: each malformed export must be rejected.

Run: python -m unittest discover -s Tools/Art -p test_native32_validation.py
No fixture or test writes to runtime assets.
"""
import unittest
from PIL import Image, ImageDraw
from native32_pipeline import palette, validate


class Native32ValidationTests(unittest.TestCase):
    def setUp(self):
        _,self.rgb = palette()
        im = Image.new('RGBA',(32,32))
        d = ImageDraw.Draw(im)
        d.rectangle((2,2,29,29),fill=(*self.rgb[0],255))
        d.rectangle((3,3,28,28),fill=(*self.rgb[6],255))
        self.im = im.resize((64,64),Image.Resampling.NEAREST)

    def checks(self,im):
        return validate(im,self.rgb)['checks']

    def test_valid_export(self):
        self.assertTrue(all(self.checks(self.im).values()))

    def test_a_rejects_wrong_size_and_rgb(self):
        self.assertFalse(self.checks(self.im.resize((32,32)))['a'])
        self.assertFalse(self.checks(self.im.convert('RGB'))['a'])

    def test_b_rejects_single_displaced_pixel(self):
        self.im.putpixel((16,16),(*self.rgb[8],255))
        self.assertFalse(self.checks(self.im)['b'])

    def test_c_rejects_partial_alpha(self):
        ImageDraw.Draw(self.im).rectangle((16,16,17,17),fill=(*self.rgb[6],128))
        self.assertFalse(self.checks(self.im)['c'])

    def test_d_rejects_foreign_color(self):
        ImageDraw.Draw(self.im).rectangle((16,16,17,17),fill=(255,0,255,255))
        self.assertFalse(self.checks(self.im)['d'])

    def test_d_rejects_nine_shared_colors(self):
        d = ImageDraw.Draw(self.im)
        for i,c in enumerate(self.rgb[:9]):
            d.rectangle((12+i*2,12,13+i*2,13),fill=(*c,255))
        self.assertFalse(self.checks(self.im)['d'])

    def test_e_rejects_tiny_content_and_off_center(self):
        tiny = Image.new('RGBA',(64,64))
        ImageDraw.Draw(tiny).rectangle((20,20,39,39),fill=(*self.rgb[0],255))
        self.assertFalse(self.checks(tiny)['e'])
        offset = Image.new('RGBA',(64,64))
        offset.paste(self.im,(4,0))
        self.assertFalse(self.checks(offset)['e'])

    def test_f_rejects_missing_boundary_and_colored_hole_rim(self):
        bad = self.im.copy()
        ImageDraw.Draw(bad).rectangle((4,10,5,11),fill=(*self.rgb[6],255))
        self.assertFalse(self.checks(bad)['f'])
        hole = self.im.copy()
        ImageDraw.Draw(hole).rectangle((20,20,23,23),fill=(0,0,0,0))
        self.assertFalse(self.checks(hole)['f'])

    def test_empty_art_cannot_pass(self):
        self.assertFalse(validate(Image.new('RGBA',(64,64)),self.rgb)['passed'])


if __name__ == '__main__':
    unittest.main()
