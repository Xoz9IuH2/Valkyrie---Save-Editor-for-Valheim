"""Generate an original Nexus cover without game artwork."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math

root = Path(__file__).resolve().parents[2]
output = root / 'artifacts' / 'nexus'
output.mkdir(parents=True, exist_ok=True)
w, h = 1920, 1080
image = Image.new('RGB', (w, h))
pixels = image.load()
for y in range(h):
    for x in range(w):
        glow = max(0, 1 - math.hypot((x - 1450) / 1200, (y - 380) / 850))
        pixels[x, y] = (int(13 + glow * 12), int(22 + glow * 23), int(29 + glow * 28))
d = ImageDraw.Draw(image)
gold = '#E7C583'
white = '#EDF1EA'
muted = '#96AFB6'
fonts = Path('C:/Windows/Fonts')
def font(size, bold=False):
    return ImageFont.truetype(str(fonts / ('segoeuib.ttf' if bold else 'segoeui.ttf')), size)

for x in range(1150, 1900, 90):
    d.line((x, 120, x - 360, 970), fill='#24383E', width=1)
for y in range(180, 1000, 90):
    d.line((1030, y, 1850, y), fill='#24383E', width=1)

d.rectangle((64, 64, w - 64, h - 64), outline='#385058', width=2)
d.line((112, 172, 210, 172), fill=gold, width=5)
d.text((238, 147), 'CHARACTER SAVE EDITOR', font=font(30, True), fill=gold)
d.text((105, 264), 'VALKYRIE', font=font(155, True), fill=white)
d.text((116, 457), 'SAVE EDITOR FOR VALHEIM', font=font(44), fill=gold)
d.text((116, 567), 'Your character. Your adventure.', font=font(33), fill=muted)

# Original geometric wing emblem, not an in-game asset.
cx, cy = 1480, 440
for side in (-1, 1):
    for i in range(4):
        points = [(cx + side * (24 + i * 34), cy + 65 + i * 31),
                  (cx + side * (254 - i * 22), cy - 160 + i * 32),
                  (cx + side * (202 - i * 23), cy + 24 + i * 27)]
        d.polygon(points, fill=gold if i % 2 == 0 else '#A28C62')
d.polygon([(cx, cy - 104), (cx + 24, cy + 93), (cx, cy + 151), (cx - 24, cy + 93)], fill=white)
d.ellipse((cx - 9, cy - 149, cx + 9, cy - 131), fill=gold)

d.line((116, 736, 1804, 736), fill='#49616A', width=2)
features = [('01', 'SKILLS'), ('02', 'INVENTORY'), ('03', 'BACKUPS')]
for i, (number, label) in enumerate(features):
    x = 116 + i * 570
    d.text((x, 787), number, font=font(27), fill=gold)
    d.text((x + 66, 775), label, font=font(42, True), fill=white)
d.text((116, 928), 'WINDOWS x64   /   STANDALONE   /   EN + RU', font=font(26), fill=muted)
d.text((1370, 934), 'UNOFFICIAL FAN TOOL', font=font(22), fill=muted)
path = output / 'Valkyrie-Nexus-Preview.png'
image.save(path, optimize=True)
print(path)
