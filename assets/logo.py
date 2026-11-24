# Logo üretir: logo.png (1024), logo96.png, logo.ico
import os
from PIL import Image, ImageDraw

here = os.path.dirname(os.path.abspath(__file__))
S = 1024
KOYU, MAVI, BEYAZ = (20, 24, 31, 255), (10, 92, 255, 255), (255, 255, 255, 255)

im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
mask = Image.new('L', (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], 220, fill=255)
bg = Image.new('RGBA', (S, S))
bd = ImageDraw.Draw(bg)
bd.rectangle([0, 0, S, S], fill=KOYU)
bd.polygon([(S, 0), (S, S), (0, S)], fill=MAVI)
im.paste(bg, (0, 0), mask)
d = ImageDraw.Draw(im)

# Disk
cx, cy, r = 440, 440, 300
d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BEYAZ)
d.ellipse([cx - 175, cy - 175, cx + 175, cy + 175], outline=(200, 210, 225, 255), width=14)
d.ellipse([cx - 70, cy - 70, cx + 70, cy + 70], fill=KOYU)

# İndirme rozeti
bx, by, br = 740, 740, 190
d.ellipse([bx - br - 24, by - br - 24, bx + br + 24, by + br + 24], fill=MAVI)
d.ellipse([bx - br, by - br, bx + br, by + br], fill=BEYAZ)
w = 46
d.rectangle([bx - w, by - 120, bx + w, by + 20], fill=MAVI)
d.polygon([(bx - 115, by + 5), (bx + 115, by + 5), (bx, by + 125)], fill=MAVI)

im.save(os.path.join(here, 'logo.png'))
im.save(os.path.join(here, 'logo.ico'), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
im.resize((96, 96), Image.LANCZOS).save(os.path.join(here, 'logo96.png'))
