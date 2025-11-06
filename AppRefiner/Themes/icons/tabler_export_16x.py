# tabler_export_qt.py
# Render Tabler SVGs to per-kind colored PNGs and raw 32-bit RGBA files,
# and build a sprite sheet PNG with a JSON map.

import argparse
import os
from pathlib import Path
from xml.etree import ElementTree as ET

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtCore import QRectF
from PySide6.QtGui import QImage, QPainter
from PySide6.QtSvg import QSvgRenderer
from PIL import Image
import json

# Per-kind slug and color
THEMES = {
    "default": {
        "ClassMethod":       {"slug": "hexagon-letter-m", "color": "#00b2e3"},
        "Parameter":         {"slug": "chevron-right",    "color": "#a855f7"},
        "SystemVariable":    {"slug": "settings",         "color": "#54565a"},
        "LocalVariable":     {"slug": "circle-letter-l",  "color": "#93d500"},
        "InstanceVariable":  {"slug": "cube",             "color": "#84d3e5"},
        "ComponentVariable": {"slug": "components",       "color": "#ff6b00"},
        "GlobalVariable":    {"slug": "world",            "color": "#231f20"},
        "Property":          {"slug": "key",              "color": "#d3549f"},
        "ExternalFunction":  {"slug": "external-link",    "color": "#ef3c45"},
        "ConstantValue":     {"slug": "lock",             "color": "#ff9e18"},
        "Field":             {"slug": "circle-letter-f",  "color": "#0dcaf0"},  # info blue/tea
        
    },
    "geometric": {
        "ClassMethod":       {"slug": "braces",       "color": "#5b9bd5"},  # blue
        "Parameter":         {"slug": "arrow-right",  "color": "#9b59b6"},  # purple
        "SystemVariable":    {"slug": "square",       "color": "#6c757d"},  # gray
        "LocalVariable":     {"slug": "circle-dot",   "color": "#70ad47"},  # green
        "InstanceVariable":  {"slug": "circle",       "color": "#44d4e8"},  # cyan
        "ComponentVariable": {"slug": "box",          "color": "#ff8c42"},  # orange
        "GlobalVariable":    {"slug": "world",        "color": "#2c3e50"},  # dark blue-gray
        "Property":          {"slug": "diamond",      "color": "#e74c9c"},  # pink
        "ExternalFunction":  {"slug": "brackets",     "color": "#e74c3c"},  # red
        "ConstantValue":     {"slug": "lock",         "color": "#f39c12"},  # amber
        "Field":             {"slug": "square-dot",   "color": "#17a2b8"},  # teal
    },
    "alphabet": {
        "ClassMethod":       {"slug": "square-letter-m", "color": "#4472c4"},  # royal blue
        "Parameter":         {"slug": "square-letter-r", "color": "#9c27b0"},  # purple
        "SystemVariable":    {"slug": "square-letter-s", "color": "#7f8c8d"},  # slate gray
        "LocalVariable":     {"slug": "circle-letter-l", "color": "#a2d729"},  # lime green
        "InstanceVariable":  {"slug": "circle-letter-i", "color": "#56c5d0"},  # turquoise
        "ComponentVariable": {"slug": "square-letter-c", "color": "#ff7733"},  # burnt orange
        "GlobalVariable":    {"slug": "circle-letter-g", "color": "#34495e"},  # charcoal
        "Property":          {"slug": "square-letter-p", "color": "#d946a8"},  # magenta
        "ExternalFunction":  {"slug": "square-letter-f", "color": "#dc3545"},  # crimson
        "ConstantValue":     {"slug": "square-letter-k", "color": "#ffc107"},  # gold
        "Field":             {"slug": "square-letter-f", "color": "#14b8a6"},  # teal
    },
    "devs": {
    "ClassMethod":       {"slug": "code",           "color": "#4a90e2"},  # blue
    "Parameter":         {"slug": "arrow-right",    "color": "#a855f7"},  # purple
    "SystemVariable":    {"slug": "settings",       "color": "#95a5a6"},  # silver
    "LocalVariable":     {"slug": "variable",       "color": "#7fba00"},  # green
    "InstanceVariable":  {"slug": "circle-dot",     "color": "#00d4ff"},  # electric cyan
    "ComponentVariable": {"slug": "puzzle",         "color": "#ff6b35"},  # coral
    "GlobalVariable":    {"slug": "world-code",     "color": "#2d3436"},  # near black
    "Property":          {"slug": "key",            "color": "#e056fd"},  # purple
    "ExternalFunction":  {"slug": "external-link",  "color": "#ff3838"},  # bright red
    "ConstantValue":     {"slug": "shield-lock",    "color": "#ffb142"},  # golden orange
    "Field":             {"slug": "file-text",      "color": "#0d9488"},  # dark teal
},
"Monogram": {
    "ClassMethod":       {"slug": "circle-letter-m", "color": "#4a6fa5"},  # blue
    "Parameter":         {"slug": "circle-letter-r", "color": "#9b59b6"},  # purple
    "SystemVariable":    {"slug": "circle-letter-s", "color": "#6b7280"},  # gray
    "LocalVariable":     {"slug": "circle-letter-l", "color": "#82c91e"},  # green
    "InstanceVariable":  {"slug": "circle-letter-i", "color": "#22d3ee"},  # cyan
    "ComponentVariable": {"slug": "circle-letter-c", "color": "#fd7e14"},  # orange
    "GlobalVariable":    {"slug": "circle-letter-g", "color": "#212529"},  # charcoal
    "Property":          {"slug": "circle-letter-p", "color": "#e64980"},  # magenta
    "ExternalFunction":  {"slug": "circle-letter-f", "color": "#fa5252"},  # red
    "ConstantValue":     {"slug": "circle-letter-k", "color": "#fab005"},  # amber
    "Field":             {"slug": "circle-letter-f", "color": "#20c997"},  # teal
},

"Semantic":{
    "ClassMethod":       {"slug": "braces",       "color": "#5b9bd5"},  # blue
    "Parameter":         {"slug": "arrow-right",  "color": "#9b59b6"},  # purple
    "SystemVariable":    {"slug": "settings",     "color": "#6c757d"},  # gray
    "LocalVariable":     {"slug": "variable",     "color": "#70ad47"},  # green
    "InstanceVariable":  {"slug": "circle-dot",   "color": "#44d4e8"},  # cyan
    "ComponentVariable": {"slug": "box",          "color": "#ff8c42"},  # orange
    "GlobalVariable":    {"slug": "world",        "color": "#2c3e50"},  # dark blue-gray
    "Property":          {"slug": "key",          "color": "#e74c9c"},  # pink
    "ExternalFunction":  {"slug": "world-code",   "color": "#e74c3c"},  # red
    "ConstantValue":     {"slug": "lock",         "color": "#f39c12"},  # amber
    "Field":             {"slug": "square-dot",   "color": "#17a2b8"},  # teal
},

"Hierarchy": {
    "ClassMethod":       {"slug": "braces",         "color": "#5470c6"},  # blue
    "Parameter":         {"slug": "arrow-right",    "color": "#9c6ade"},  # purple
    "SystemVariable":    {"slug": "square",         "color": "#73808c"},  # gray
    "LocalVariable":     {"slug": "circle-dot",     "color": "#91cc75"},  # green
    "InstanceVariable":  {"slug": "circle",         "color": "#5bc0de"},  # cyan
    "ComponentVariable": {"slug": "box",            "color": "#ff9f40"},  # orange
    "GlobalVariable":    {"slug": "globe",          "color": "#34495e"},  # slate
    "Property":          {"slug": "diamond",        "color": "#ee6fa8"},  # pink
    "ExternalFunction":  {"slug": "brackets-angle", "color": "#ee6666"},  # red
    "ConstantValue":     {"slug": "lock-square",    "color": "#fac858"},  # gold
    "Field":             {"slug": "square-dot",     "color": "#14b8a6"},  # teal
},
"Hybrid": {
    "ClassMethod":       {"slug": "braces",          "color": "#4169e1"},  # royal blue
    "Parameter":         {"slug": "circle-letter-r", "color": "#9370db"},  # medium purple
    "SystemVariable":    {"slug": "settings",        "color": "#708090"},  # slate gray
    "LocalVariable":     {"slug": "circle-letter-l", "color": "#32cd32"},  # lime green
    "InstanceVariable":  {"slug": "circle-letter-i", "color": "#00bfff"},  # deep sky blue
    "ComponentVariable": {"slug": "box",             "color": "#ff8c00"},  # dark orange
    "GlobalVariable":    {"slug": "world",           "color": "#2f4f4f"},  # dark slate
    "Property":          {"slug": "key",             "color": "#da70d6"},  # orchid
    "ExternalFunction":  {"slug": "brackets-angle",  "color": "#dc143c"},  # crimson
    "ConstantValue":     {"slug": "lock",            "color": "#ffa500"},  # orange
    "Field":             {"slug": "circle-letter-f", "color": "#0dcaf0"},  # info blue/teal
},

"Terminal": {
    "ClassMethod":       {"slug": "code",          "color": "#0ea5e9"},  # sky blue
    "Parameter":         {"slug": "chevron-right", "color": "#a855f7"},  # purple
    "SystemVariable":    {"slug": "settings",      "color": "#64748b"},  # slate
    "LocalVariable":     {"slug": "variable",      "color": "#84cc16"},  # lime
    "InstanceVariable":  {"slug": "circle-dot",    "color": "#06b6d4"},  # cyan
    "ComponentVariable": {"slug": "box",           "color": "#f97316"},  # orange
    "GlobalVariable":    {"slug": "world-code",    "color": "#1e293b"},  # dark slate
    "Property":          {"slug": "brackets",      "color": "#ec4899"},  # pink
    "ExternalFunction":  {"slug": "external-link", "color": "#ef4444"},  # red
    "ConstantValue":     {"slug": "diamond",       "color": "#eab308"},  # yellow
    "Field":             {"slug": "file-code",     "color": "#06b6d4"},  # cyan
}
}

def recolor_svg(svg_bytes: bytes, hex_color: str, target_px: int) -> bytes:
    root = ET.fromstring(svg_bytes)
    root.set("width", str(target_px))
    root.set("height", str(target_px))
    root.set("color", hex_color)

    def walk(elem):
        st = elem.get("stroke")
        if st is not None and st.strip().lower() == "currentcolor":
            elem.set("stroke", hex_color)
        fl = elem.get("fill")
        if fl is not None and fl.strip().lower() == "currentcolor":
            elem.set("fill", hex_color)
        for child in list(elem):
            walk(child)

    walk(root)
    return ET.tostring(root, encoding="utf-8")

def svg_path_for_slug(icons_dir: Path, slug: str) -> Path:
    return icons_dir / f"{slug}.svg"

def render_svg_to_qimage(svg_bytes: bytes, size_px: int) -> QImage:
    renderer = QSvgRenderer(svg_bytes)
    img = QImage(size_px, size_px, QImage.Format_RGBA8888)
    img.fill(0)
    painter = QPainter(img)
    try:
        painter.setRenderHint(QPainter.Antialiasing, True)
        target = QRectF(0, 0, size_px, size_px)
        renderer.render(painter, target)
    finally:
        painter.end()
    return img

def qimage_to_pillow(img: QImage) -> Image.Image:
    if img.format() != QImage.Format_RGBA8888:
        img = img.convertToFormat(QImage.Format_RGBA8888)
    width, height = img.width(), img.height()
    stride = img.bytesPerLine()
    mv = img.bits()
    buf = bytes(mv)
    # Remove any per-row padding so Pillow sees a tight RGBA buffer
    tight = bytearray(width * height * 4)
    row_len = width * 4
    for y in range(height):
        tight[y * row_len:(y + 1) * row_len] = buf[y * stride:y * stride + row_len]
    return Image.frombytes("RGBA", (width, height), bytes(tight))

def save_qimage_png(img: QImage, path: Path):
    pil = qimage_to_pillow(img)
    pil.save(path, "PNG")

def write_raw_rgba_from_qimage(img: QImage, rgba_path: Path):
    pil = qimage_to_pillow(img)
    rgba_path.write_bytes(pil.tobytes("raw", "RGBA"))

def build_sprite_sheet(pil_images, kinds, size_px, padding, out_png: Path, out_json: Path):
    """
    Simple horizontal strip sprite.
    padding pixels between icons, transparent background.
    """
    n = len(pil_images)
    cell = size_px
    width = n * cell + max(0, n - 1) * padding
    height = cell
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))

    mapping = {}
    x = 0
    for idx, (kind, img) in enumerate(zip(kinds, pil_images)):
        sheet.paste(img, (x, 0))
        mapping[kind] = {"x": x, "y": 0, "w": cell, "h": cell, "index": idx}
        x += cell + padding

    sheet.save(out_png, "PNG")
    out_json.write_text(json.dumps({"image": out_png.name, "size": cell, "padding": padding, "map": mapping}, indent=2))

def build_combined_visualization(sprites_dict, size_px, output_path: Path):
    """
    Creates a combined PNG showing all themes grouped by style.
    White background with section headers and theme labels.

    Layout:
    - Section 1: "OUTLINE STYLE" + 4 theme rows
    - Section 2: "FILLED STYLE" + 4 theme rows
    """
    from PIL import ImageDraw, ImageFont

    # Layout constants
    LABEL_WIDTH = 150
    SECTION_HEADER_HEIGHT = 40
    THEME_ROW_HEIGHT = size_px + 20  # sprite height + padding
    LEFT_MARGIN = 20
    TOP_MARGIN = 20
    SECTION_GAP = 30

    # Calculate sprite width (9 icons with 2px padding)
    sprite_width = sprites_dict["outline"]["default"].width

    # Total dimensions
    total_width = LEFT_MARGIN + LABEL_WIDTH + sprite_width + LEFT_MARGIN
    num_themes = len(sprites_dict["outline"])
    total_height = (TOP_MARGIN +
                    SECTION_HEADER_HEIGHT + num_themes * THEME_ROW_HEIGHT +  # Outline section
                    SECTION_GAP +
                    SECTION_HEADER_HEIGHT + num_themes * THEME_ROW_HEIGHT +  # Filled section
                    TOP_MARGIN)

    # Create white background
    combined = Image.new("RGB", (total_width, total_height), (255, 255, 255))
    draw = ImageDraw.Draw(combined)

    # Try to load a larger font, fall back to default
    try:
        header_font = ImageFont.truetype("arial.ttf", 20)
        label_font = ImageFont.truetype("arial.ttf", 14)
    except:
        header_font = ImageFont.load_default()
        label_font = ImageFont.load_default()

    y_pos = TOP_MARGIN

    # Process each style (outline, filled)
    for style_idx, style in enumerate(["outline", "filled"]):
        # Draw section header
        header_text = f"{style.upper()} STYLE"
        draw.text((LEFT_MARGIN, y_pos), header_text, fill=(0, 0, 0), font=header_font)
        y_pos += SECTION_HEADER_HEIGHT

        # Draw each theme row
        for theme in sorted(sprites_dict[style].keys()):
            # Theme label
            label_y = y_pos + (THEME_ROW_HEIGHT - size_px) // 2
            draw.text((LEFT_MARGIN, label_y), theme.capitalize(), fill=(0, 0, 0), font=label_font)

            # Paste sprite (need to convert RGBA to RGB for white background)
            sprite = sprites_dict[style][theme]
            sprite_x = LEFT_MARGIN + LABEL_WIDTH
            sprite_y = y_pos + (THEME_ROW_HEIGHT - size_px) // 2

            # Convert RGBA sprite to RGB by compositing on white
            sprite_rgb = Image.new("RGB", sprite.size, (255, 255, 255))
            sprite_rgb.paste(sprite, (0, 0), sprite)
            combined.paste(sprite_rgb, (sprite_x, sprite_y))

            y_pos += THEME_ROW_HEIGHT

        # Add gap between sections (but not after the last section)
        if style_idx < 1:
            y_pos += SECTION_GAP

    # Save combined visualization
    combined.save(output_path, "PNG")
    print(f"Created combined visualization: {output_path}")

def main():
    parser = argparse.ArgumentParser(description="Generate combined theme visualization from Tabler SVG icons")
    parser.add_argument("--icons-dir", required=True, help="Path to Tabler SVG icons folder")
    parser.add_argument("--size", type=int, default=16, help="Icon size in pixels")
    parser.add_argument("--fallback-color", default="#231f20", help="Used if a kind has no color")
    args = parser.parse_args()

    # Hardcoded sprite padding
    sprite_padding = 2

    icons_dir = Path(args.icons_dir)
    if not icons_dir.exists():
        raise SystemExit(f"Icons dir not found: {icons_dir}")




    # Collect all sprites organized by style and theme
    sprites = {}

    for style in ["outline","filled"]:
        sprites[style] = {}

        for theme in sorted(THEMES.keys()):
            rendered_pil = []
            kinds_in_order = []
            out_dir = Path(f"{theme}_{style}")
            out_dir.mkdir(parents=True, exist_ok=True)

            KIND_TO_TABLER = THEMES[theme]
            # Stable order by kind name
            for kind in sorted(KIND_TO_TABLER.keys()):
                spec = KIND_TO_TABLER[kind]
                slug = spec["slug"]
                color = spec.get("color", args.fallback_color)

                svg_path = svg_path_for_slug(icons_dir / style, slug)
                if not svg_path.exists():
                    svg_path = svg_path_for_slug(icons_dir / "outline", slug)

                raw_svg = svg_path.read_bytes()
                recolored = recolor_svg(raw_svg, color, args.size)
                img = render_svg_to_qimage(recolored, args.size)

                # Write individual RGBA file
                rgba_path = out_dir / f"{kind}.rgba"
                write_raw_rgba_from_qimage(img, rgba_path)
                expected = args.size * args.size * 4
                actual = rgba_path.stat().st_size
                if actual != expected:
                    print(f"Warning: {rgba_path.name} size {actual} vs expected {expected}")
                print(f"Wrote {rgba_path}")

                # Convert to PIL and collect for sprite
                pil_img = qimage_to_pillow(img)
                rendered_pil.append(pil_img)
                kinds_in_order.append(kind)

            # Build sprite for this theme
            sprite_width = len(rendered_pil) * args.size + max(0, len(rendered_pil) - 1) * sprite_padding
            sprite_height = args.size
            sprite = Image.new("RGBA", (sprite_width, sprite_height), (0, 0, 0, 0))

            x = 0
            for img in rendered_pil:
                sprite.paste(img, (x, 0))
                x += args.size + sprite_padding

            sprites[style][theme] = sprite
            print(f"Generated sprite for {theme} ({style})")

    # Build combined visualization
    output_path = Path("all_themes_combined.png")
    build_combined_visualization(sprites, args.size, output_path)

if __name__ == "__main__":
    main()
