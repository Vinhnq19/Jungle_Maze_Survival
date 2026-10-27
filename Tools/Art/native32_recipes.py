"""Hand-authored integer pixel geometry. No generated image is sampled here.

Recipes replace undersized legacy sprites instead of enlarging their pixels.
All drawing happens directly on a 32px canvas; the normalizer adds a 1px rim.
Filled material planes are deliberately broad, with light on the upper left.
"""
from PIL import Image, ImageDraw

RECIPE_IDS = {
    'HAND', 'ROPE', 'SCEPTER', 'CROSSBOW', 'SEED', 'WOODEN_HELMET',
    'SPIKE_HELMET', 'MAGIC_HELMET', 'WOODEN_ARMOR', 'ARMOR', 'MAGIC_ARMOR',
    'GOD_LEAF', 'SPIRIT_TEAR', 'MERMAID_TAIL', 'PHOENIX_FEATHERS',
    'UNICORN_BLOOD', 'ADRENALINE', 'ELIXIR', 'MAGIC', 'ELIXIR_OF_LIFE', 'COMPASS',
}


def draw_item(item_id, colors):
    im = Image.new('RGBA', (32, 32))
    d = ImageDraw.Draw(im)
    p = colors

    def box(xy, color): d.rectangle(xy, fill=p[color])
    def poly(xy, color): d.polygon(xy, fill=p[color])
    def ellipse(xy, color): d.ellipse(xy, fill=p[color])
    def line(xy, color, width=1): d.line(xy, fill=p[color], width=width)

    if item_id == 'HAND':
        # A single closed fist: four square knuckles, folded thumb, short wrist.
        poly([(5,11),(7,7),(10,7),(10,4),(14,3),(17,5),(20,5),
              (23,8),(25,8),(28,12),(28,20),(24,24),(22,28),
              (11,28),(10,24),(6,21),(3,17),(3,13)], 'skin_dark')
        poly([(5,12),(8,9),(11,9),(11,6),(14,5),(17,7),(20,7),
              (23,10),(25,10),(26,13),(25,19),(21,22),(11,22),
              (7,19),(5,16)], 'skin')
        box((12,24,21,27), 'skin')
        poly([(7,11),(10,10),(12,14),(10,17),(7,15)], 'skin_light')
        box((12,7,14,11), 'skin_light')
        box((17,8,19,12), 'skin_light')
        box((22,11,24,14), 'skin_light')
        # Fold separation is a single pixel wide; no nails or wrinkle texture.
        line([(10,18),(14,15),(20,16),(22,19)], 'skin_dark')
        poly([(11,18),(14,17),(19,18),(20,20),(15,21)], 'skin_light')
    elif item_id == 'ROPE':
        # Three oval turns, a visible hollow center, and one short loose end.
        ellipse((3,4,26,26), 'wood_dark')
        ellipse((4,4,25,23), 'wood_light')
        ellipse((6,6,23,21), 'wood')
        ellipse((7,7,22,19), 'wood_dark')
        ellipse((8,7,21,18), 'wood_light')
        ellipse((10,9,19,16), 'wood')
        ellipse((11,10,18,15), 'ink')
        ellipse((13,11,16,14), 'transparent')
        poly([(22,22),(25,23),(25,26),(28,26),(28,28),(23,28),(21,25)], 'wood')
        line([(5,14),(5,18),(8,22),(14,24),(20,23)], 'wood_dark')
        line([(8,6),(13,5),(19,6)], 'wood_light')
    elif item_id in {'SCEPTER', 'MAGIC'}:
        poly([(13,12),(17,12),(17,28),(13,28)], 'wood_dark')
        box((14,14,15,27), 'wood_light')
        if item_id == 'SCEPTER':
            poly([(10,3),(20,3),(24,8),(21,14),(10,14),(7,8)], 'gold')
            poly([(13,3),(18,3),(21,8),(18,12),(12,12),(10,8)], 'blue_dark')
            poly([(13,4),(17,4),(19,7),(16,10),(12,9)], 'blue')
            box((12,5,14,7), 'metal_light')
        else:
            poly([(15,3),(18,9),(24,10),(19,15),(20,21),(15,18),
                  (9,21),(10,15),(5,10),(12,9)], 'purple_dark')
            poly([(15,5),(17,11),(21,11),(17,14),(18,17),(15,15),
                  (12,17),(13,13),(9,11),(14,11)], 'purple')
            box((14,9,16,12), 'metal_light')
    elif item_id == 'CROSSBOW':
        poly([(3,11),(8,6),(15,3),(23,6),(28,11),(26,14),
              (21,10),(16,8),(10,10),(5,14)], 'wood_dark')
        line([(4,11),(9,8),(15,5),(22,8),(27,11)], 'wood_light', 2)
        line([(4,13),(15,22),(27,13)], 'metal_dark')
        poly([(13,9),(18,9),(18,25),(16,28),(12,26),(14,20)], 'wood')
        box((14,10,15,23), 'wood_light')
        poly([(16,6),(18,9),(16,9)], 'metal_light')
        line([(16,9),(16,22)], 'metal_light')
    elif item_id == 'SEED':
        poly([(14,5),(20,7),(25,13),(25,21),(20,27),(14,28),
              (8,25),(6,18),(8,11)], 'wood_dark')
        poly([(14,7),(19,9),(23,14),(22,21),(18,25),(13,26),
              (9,23),(8,17),(10,12)], 'wood')
        poly([(13,8),(16,9),(12,14),(11,22),(9,19),(10,13)], 'wood_light')
        line([(18,10),(15,16),(15,23)], 'gold')
        box((13,3,16,6), 'leaf')
    elif item_id in {'WOODEN_HELMET', 'SPIKE_HELMET'}:
        if item_id == 'WOODEN_HELMET':
            poly([(5,14),(8,8),(14,5),(20,5),(25,10),(27,18),
                  (27,24),(20,24),(20,20),(11,20),(11,24),(4,24),(4,18)], 'wood_dark')
            poly([(7,15),(10,10),(15,7),(19,7),(23,11),(25,17),(6,17)], 'wood')
            poly([(9,13),(12,10),(15,9),(15,16),(8,16)], 'wood_light')
            box((3,18,28,20), 'wood_light')
            box((5,22,10,25), 'wood')
            box((21,22,26,25), 'wood')
        else:
            poly([(6,15),(9,10),(14,8),(20,9),(24,13),(25,24),
                  (22,26),(8,26),(6,23)], 'metal_dark')
            poly([(8,15),(12,11),(17,10),(21,12),(23,19),(8,19)], 'metal')
            poly([(13,10),(15,3),(18,10)], 'metal_light')
            poly([(7,15),(3,9),(9,12)], 'metal_light')
            poly([(23,15),(28,9),(22,12)], 'metal_light')
            box((14,11,16,21), 'metal_light')
            box((6,21,25,23), 'gold')
    elif item_id == 'MAGIC_HELMET':
        poly([(7,23),(12,12),(17,4),(23,3),(21,7),(19,8),
              (20,16),(24,23),(28,24),(27,28),(3,28),(3,25)], 'purple_dark')
        poly([(10,23),(14,12),(18,6),(21,5),(18,9),(18,16),(21,23)], 'purple')
        poly([(14,12),(17,8),(16,16),(13,21),(11,22)], 'metal_light')
        box((8,22,23,24), 'gold')
        box((14,22,18,25), 'jade_dark')
        box((15,22,17,23), 'jade')
    elif item_id in {'WOODEN_ARMOR', 'ARMOR', 'MAGIC_ARMOR'}:
        dark, base, light = {
            'WOODEN_ARMOR': ('wood_dark','wood','wood_light'),
            'ARMOR': ('metal_dark','metal','metal_light'),
            'MAGIC_ARMOR': ('purple_dark','purple','metal_light'),
        }[item_id]
        poly([(8,3),(12,3),(12,8),(19,8),(19,3),(23,3),(28,9),
              (26,14),(23,13),(24,28),(7,28),(8,13),(5,14),(3,9)], dark)
        poly([(8,5),(10,5),(10,10),(21,10),(21,5),(23,6),
              (26,10),(24,11),(21,10),(22,25),(9,25),(10,10),(7,11),(5,10)], base)
        if item_id == 'WOODEN_ARMOR':
            for y in (12,17,22): box((10,y,21,y+2), light)
            line([(13,10),(13,25)], 'gold')
            line([(19,10),(19,25)], 'gold')
        else:
            poly([(10,11),(14,11),(14,21),(10,19)], light)
            line([(16,11),(16,24)], dark)
            if item_id == 'ARMOR':
                poly([(5,11),(3,5),(9,9)], light)
                poly([(25,11),(28,5),(22,9)], light)
            else:
                poly([(16,13),(19,17),(16,21),(14,17)], 'jade')
        box((8,25,23,27), dark)
        box((14,25,18,27), 'gold')
    elif item_id == 'GOD_LEAF':
        poly([(4,23),(5,14),(11,7),(28,3),(26,16),(21,24),
              (13,27),(6,26)], 'leaf_dark')
        poly([(6,21),(8,14),(13,9),(25,6),(23,16),(18,23),(10,25)], 'leaf')
        line([(4,28),(11,21),(18,14),(24,8)], 'gold_light')
        line([(11,21),(10,15)], 'leaf_dark')
        line([(16,16),(22,16)], 'leaf_dark')
    elif item_id == 'SPIRIT_TEAR':
        poly([(16,3),(19,10),(24,17),(26,22),(23,27),(18,28),
              (11,27),(7,23),(7,18),(12,10)], 'blue_dark')
        poly([(16,7),(18,12),(22,18),(23,22),(20,25),(13,25),
              (10,22),(10,18)], 'blue')
        poly([(14,12),(16,12),(13,19),(13,22),(11,21),(11,18)], 'metal_light')
        box((17,23,19,24), 'jade')
    elif item_id == 'MERMAID_TAIL':
        poly([(10,3),(22,3),(28,10),(28,21),(21,28),(10,28),
              (3,21),(3,10)], 'skin_dark')
        poly([(11,5),(21,5),(26,11),(26,20),(20,26),(11,26),
              (5,20),(5,11)], 'skin')
        poly([(9,10),(14,6),(18,6),(9,16),(6,18),(6,13)], 'skin_light')
        for x,y in [(13,13),(20,12),(12,20),(20,20)]:
            line([(x-2,y),(x,y+2),(x+2,y)], 'skin_light')
    elif item_id == 'PHOENIX_FEATHERS':
        poly([(5,28),(7,18),(10,11),(17,5),(26,3),(24,13),
              (20,21),(13,25)], 'red_dark')
        poly([(8,24),(10,17),(14,11),(22,6),(23,6),(21,14),
              (17,20)], 'red')
        poly([(9,22),(13,14),(20,8),(18,15),(13,21)], 'gold')
        line([(5,28),(12,20),(21,9)], 'gold_light')
    elif item_id == 'UNICORN_BLOOD':
        poly([(11,9),(11,6),(20,6),(20,11),(25,16),(26,23),
              (22,28),(9,28),(5,23),(6,16)], 'red_dark')
        ellipse((7,14,24,26), 'red')
        box((12,9,19,13), 'metal_dark')
        poly([(16,9),(18,3),(20,9)], 'gold_light')
        poly([(10,8),(3,7),(6,12),(11,14)], 'purple_dark')
        poly([(21,9),(28,7),(25,12),(21,14)], 'purple_dark')
        box((9,17,11,21), 'skin_light')
    elif item_id == 'ADRENALINE':
        box((10,3,21,28), 'wood_dark')
        box((8,7,23,24), 'gold')
        box((10,5,21,26), 'gold')
        box((10,7,12,23), 'gold_light')
        box((10,3,21,4), 'metal_light')
        box((10,27,21,28), 'metal_dark')
        poly([(17,9),(13,16),(16,16),(14,23),(21,14),(17,14)], 'jade_dark')
    elif item_id in {'ELIXIR','ELIXIR_OF_LIFE'}:
        dark,base,light = ('blue_dark','blue','metal_light') if item_id == 'ELIXIR' else ('wood_light','gold','gold_light')
        box((11,3,20,6), 'wood_dark')
        box((12,7,19,11), 'metal_dark')
        poly([(10,10),(21,10),(21,13),(26,18),(26,24),(22,28),
              (9,28),(5,24),(5,18),(10,13)], dark)
        poly([(10,15),(21,15),(24,19),(24,23),(21,26),(10,26),
              (7,23),(7,19)], base)
        box((9,18,11,22), light)
        box((13,12,15,15), 'metal_light')
        if item_id == 'ELIXIR_OF_LIFE':
            poly([(24,7),(24,3),(25,5),(28,5),(26,7),(26,10)], light)
    elif item_id == 'COMPASS':
        ellipse((3,3,28,28), 'wood_light')
        ellipse((4,4,27,27), 'gold')
        ellipse((6,6,25,25), 'metal_dark')
        ellipse((7,7,24,24), 'metal_light')
        line([(15,8),(15,10)], 'ink')
        line([(8,15),(10,15)], 'ink')
        line([(21,15),(23,15)], 'ink')
        line([(15,21),(15,23)], 'ink')
        poly([(21,10),(17,18),(11,22),(14,14)], 'blue_dark')
        poly([(21,10),(17,16),(14,14)], 'red')
    else:
        raise ValueError(f'No native recipe for {item_id}')
    return im
