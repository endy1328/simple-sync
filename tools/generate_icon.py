from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
ICO_PATH = ASSETS / "simple-sync.ico"
PNG_PATH = ASSETS / "simple-sync-256.png"


def gradient(size: int) -> Image.Image:
    image = Image.new("RGBA", (size, size))
    pixels = image.load()
    c1 = (19, 184, 166)
    c2 = (33, 118, 255)
    c3 = (56, 67, 208)

    for y in range(size):
        for x in range(size):
            t = (x + y) / (2 * (size - 1))
            if t < 0.58:
                local = t / 0.58
                color = tuple(round(c1[i] * (1 - local) + c2[i] * local) for i in range(3))
            else:
                local = (t - 0.58) / 0.42
                color = tuple(round(c2[i] * (1 - local) + c3[i] * local) for i in range(3))
            pixels[x, y] = (*color, 255)

    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    radius = round(size * 0.22)
    draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    image.putalpha(mask)
    return image


def rounded_rect(draw: ImageDraw.ImageDraw, xy: tuple[int, int, int, int], radius: int, fill: tuple[int, int, int, int]) -> None:
    draw.rounded_rectangle(xy, radius=radius, fill=fill)


def make_icon(size: int) -> Image.Image:
    scale = size / 256

    def s(value: int) -> int:
        return round(value * scale)

    image = gradient(size)
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)

    draw.ellipse((s(179), s(31), s(225), s(77)), fill=(255, 255, 255, 46))
    draw.ellipse((s(14), s(167), s(76), s(229)), fill=(8, 26, 58, 30))

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow_draw = ImageDraw.Draw(shadow)
    rounded_rect(shadow_draw, (s(48), s(86), s(202), s(197)), s(14), (9, 32, 71, 82))
    shadow = shadow.filter(ImageFilter.GaussianBlur(s(10)))
    image.alpha_composite(shadow, (0, s(7)))

    # Back document.
    rounded_rect(draw, (s(68), s(50), s(178), s(182)), s(12), (245, 250, 255, 255))
    draw.polygon([(s(146), s(50)), (s(178), s(82)), (s(158), s(90)), (s(146), s(78))], fill=(191, 215, 255, 255))
    draw.polygon([(s(146), s(50)), (s(178), s(82)), (s(158), s(82)), (s(146), s(70))], fill=(221, 235, 255, 255))

    # Folder.
    draw.rounded_rectangle((s(48), s(86), s(202), s(197)), radius=s(14), fill=(255, 176, 32, 255))
    draw.polygon(
        [(s(58), s(86)), (s(105), s(86)), (s(121), s(103)), (s(192), s(103)), (s(202), s(128)), (s(48), s(128))],
        fill=(255, 229, 140, 255),
    )
    draw.rounded_rectangle((s(48), s(118), s(202), s(197)), radius=s(13), fill=(255, 202, 58, 255))

    # One-way sync arrow.
    line_width = max(4, s(18))
    accent_width = max(2, s(7))
    draw.line((s(103), s(151), s(159), s(151)), fill=(255, 255, 255, 255), width=line_width)
    draw.line((s(143), s(127), s(169), s(151), s(143), s(175)), fill=(255, 255, 255, 255), width=line_width, joint="curve")
    draw.line((s(102), s(151), s(158), s(151)), fill=(17, 85, 204, 140), width=accent_width)
    draw.line((s(142), s(128), s(166), s(151), s(142), s(174)), fill=(17, 85, 204, 140), width=accent_width, joint="curve")

    image.alpha_composite(layer)
    return image


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    sizes = [16, 24, 32, 48, 64, 128, 256]
    images = [make_icon(size) for size in sizes]
    images[-1].save(PNG_PATH)
    images[-1].save(ICO_PATH, sizes=[(size, size) for size in sizes])
    print(ICO_PATH)
    print(PNG_PATH)


if __name__ == "__main__":
    main()
