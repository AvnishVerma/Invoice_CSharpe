# Font Awesome application icons

LedgerNest uses the bundled Font Awesome Free 7.2.0 Solid font for application icons. `Assets/FontAwesome-Icons.json` maps semantic icon names to glyphs; `IconFontFamily`, `FontAwesomeGlyphConverter`, and `FontAwesomeGlyph` supply the shared AXAML rendering. `FontAwesomeIconView` is the AXAML visual consumed by legacy composition callers.

Add new icons to the catalog using a glyph available in the bundled Free Solid font, then use the shared converter or markup extension. Keep icon structure in AXAML and keep captions separate from glyphs so localization does not translate icon identifiers. Do not introduce emoji, Material font references, or per-screen icon fonts.

The font works offline. Its upstream license and attribution are distributed in `Assets/FontAwesome-LICENSE.txt`. Application branding bitmap assets, generated document artwork/QR codes, and Avalonia's native control decorations remain unchanged. Password masking characters, mathematical operators, and punctuation are text, not application icons.

Source: https://github.com/FortAwesome/Font-Awesome/tree/7.2.0
