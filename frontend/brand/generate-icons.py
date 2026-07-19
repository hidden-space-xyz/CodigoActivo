"""Derive every icon in ../public from codigo-activo-logo.png.

One-off tool, not part of `npm run build`. Needs Pillow (`pip install pillow`).
Run from anywhere:  python frontend/brand/generate-icons.py

Every output keeps the master's transparency -- the logo is never composited onto
a plate. og-image.png is NOT produced here: it needs the real webfonts, so it is
screenshotted from og.html in a browser at 1200x630 @2x. See CLAUDE.md.
"""

from pathlib import Path

from PIL import Image

BRAND = Path(__file__).resolve().parent
SRC = BRAND / "codigo-activo-logo.png"
PUBLIC = BRAND.parent / "public"
SHARED_ASSETS = BRAND.parent / "src" / "shared" / "assets"

SS = 8


def master() -> Image.Image:
    im = Image.open(SRC).convert("RGBA")
    im = im.crop(im.getchannel("A").getbbox())
    side = max(im.size)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(im, ((side - im.width) // 2, (side - im.height) // 2), im)
    return canvas


def icon(mark: Image.Image, size: int, *, inset: float) -> Image.Image:
    """Transparent square canvas with the mark centred inside."""
    big = size * SS
    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    inner = int(big * (1 - 2 * inset))
    img.alpha_composite(mark.resize((inner, inner), Image.LANCZOS), ((big - inner) // 2,) * 2)
    return img.resize((size, size), Image.LANCZOS)


def main() -> None:
    mark = master()

    # Browser tabs: no plate, so let the mark fill the canvas for legibility at 16px.
    ico = [16, 32, 48]
    frames = [icon(mark, s, inset=0.02) for s in ico]
    frames[-1].save(
        PUBLIC / "favicon.ico",
        format="ICO",
        sizes=[(s, s) for s in ico],
        append_images=frames[:-1],
    )
    icon(mark, 96, inset=0.02).save(PUBLIC / "favicon-96x96.png", optimize=True)

    # iOS applies its own rounded mask, so keep a safe margin. NOTE: iOS composites
    # any transparency onto black on the home screen -- this icon is transparent by
    # explicit request, and will show the mark on black there rather than on cream.
    icon(mark, 180, inset=0.11).save(PUBLIC / "apple-touch-icon.png", optimize=True)

    # Bundled copies are palette-quantized: the gradients survive 256 colours (mean composite
    # error 1.3/255 over both the light and dark theme backgrounds) at a fraction of the bytes.
    def quantized(size: int) -> Image.Image:
        return mark.resize((size, size), Image.LANCZOS).quantize(
            colors=256, method=Image.FASTOCTREE, dither=Image.NONE
        )

    quantized(192).save(SHARED_ASSETS / "logo-mark.png", optimize=True)
    # 640px covers the ~330 CSS px hero display at 2x DPR.
    quantized(640).save(SHARED_ASSETS / "logo-mark-large.png", optimize=True)
    mark.resize((512, 512), Image.LANCZOS).save(BRAND / "logo-mark-512.png", optimize=True)

    for name in ("favicon.ico", "favicon-96x96.png", "apple-touch-icon.png"):
        print(f"{name:38} {(PUBLIC / name).stat().st_size:>7} B")
    for label, path in (
        ("src/shared/assets/logo-mark.png", SHARED_ASSETS / "logo-mark.png"),
        ("src/shared/assets/logo-mark-large.png", SHARED_ASSETS / "logo-mark-large.png"),
        ("brand/logo-mark-512.png", BRAND / "logo-mark-512.png"),
    ):
        print(f"{label:38} {path.stat().st_size:>7} B")


if __name__ == "__main__":
    main()
