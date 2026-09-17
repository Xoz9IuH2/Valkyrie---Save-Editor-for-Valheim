"""Generate an original multi-size Windows icon from the Valkyrie emblem."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2]
output = root / 'Editor' / 'Assets'
output.mkdir(parents=True, exist_ok=True)

GOLD = '#E7C583'
GOLD_DARK = '#A28C62'
WHITE = '#EDF1EA'
NAVY = '#111B21'
EDGE = '#385058'


def draw_emblem(draw: ImageDraw.ImageDraw, size: int) -> None:
    s = size / 512
    cx, cy = size / 2, size * 0.52
    for side in (-1, 1):
        for i in range(4):
            points = [
                (cx + side * (18 + i * 28) * s, cy + (48 + i * 26) * s),
                (cx + side * (210 - i * 18) * s, cy + (-148 + i * 26) * s),
                (cx + side * (166 - i * 19) * s, cy + (12 + i * 22) * s),
            ]
            draw.polygon(points, fill=GOLD if i % 2 == 0 else GOLD_DARK)
    draw.polygon(
        [
            (cx, cy - 92 * s),
            (cx + 20 * s, cy + 78 * s),
            (cx, cy + 128 * s),
            (cx - 20 * s, cy + 78 * s),
        ],
        fill=WHITE,
    )
    r = max(2, int(8 * s))
    draw.ellipse((cx - r, cy - 132 * s - r, cx + r, cy - 132 * s + r), fill=GOLD)


def make_frame(size: int) -> Image.Image:
    image = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    inset = max(1, size // 32)
    radius = max(2, size // 8)
    draw.rounded_rectangle((inset, inset, size - inset - 1, size - inset - 1), radius=radius, fill=NAVY, outline=EDGE, width=max(1, size // 64))
    if size >= 24:
        draw_emblem(draw, size)
    else:
        cx, cy = size / 2, size / 2 + 0.5
        draw.polygon([(cx, 3), (size - 3, cy), (cx, size - 3), (3, cy)], fill=GOLD)
        draw.polygon([(cx, 6), (cx + 2, cy + 1), (cx, size - 5), (cx - 2, cy + 1)], fill=WHITE)
    return image


sizes = [16, 24, 32, 48, 64, 128, 256]
frames = [make_frame(size) for size in sizes]
icon_path = output / 'Valkyrie.ico'
frames[-1].save(icon_path, format='ICO', append_images=frames[:-1], sizes=[(size, size) for size in sizes])
preview_path = output / 'Valkyrie-icon-256.png'
frames[-1].save(preview_path)
print(icon_path)
print(preview_path)
