"""Builds the app's icon font: only the Material Symbols glyphs the app uses, at a few kilobytes instead of 15 MB.

    python tools/IconFont/subset-icons.py            # rebuild Resources/Fonts/MaterialSymbolsRounded.ttf
    python tools/IconFont/subset-icons.py --check    # only report; exit 1 if the built font lacks an icon the code uses

Run it again whenever a page starts using another icon: it finds every `IconFont.<Name>` in the app's XAML and C#, looks the
codepoint up in Models/MaterialSymbolsRoundedIcons.cs, and subsets the full font (kept beside this script) down to those
glyphs. An icon that is not in the built font draws as an empty box, so use --check (or just rebuild) after adding one. Icons
that are chosen at run time from a string can't be found by the scan; list their names, one per line, in extra-icons.txt.

Needs fontTools:  pip install fonttools

Android loads only a font's default instance, so the variable axes (fill, grade, optical size, weight) are pinned at their
defaults, and the layout tables (ligatures, kerning) are dropped: the app addresses glyphs by codepoint, never by name.
"""
import re
import sys
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

HERE = Path(__file__).resolve().parent
APP = HERE.parent.parent / "OvertonesPlayground"
FULL_FONT = HERE / "MaterialSymbolsRounded-VariableFont.ttf"
BUILT_FONT = APP / "Resources" / "Fonts" / "MaterialSymbolsRounded.ttf"
ICON_CLASS = APP / "Models" / "MaterialSymbolsRoundedIcons.cs"
EXTRA = HERE / "extra-icons.txt"


def icon_codepoints():
    """IconFont constant name -> codepoint, from the generated class."""
    text = ICON_CLASS.read_text(encoding="utf-8-sig")
    return {name: int(digits, 16) for name, digits in re.findall(r'public const string (\w+) = "\\[uU]0*([0-9a-fA-F]+)";', text)}


def icons_in_use():
    """Every IconFont.<Name> the app's own code and XAML refer to."""
    used = set()
    for pattern in ("*.xaml", "*.cs"):
        for path in APP.rglob(pattern):
            if any(part in ("obj", "bin") for part in path.parts) or path == ICON_CLASS:
                continue
            used.update(re.findall(r"\bIconFont\.([A-Za-z0-9_]+)", path.read_text(encoding="utf-8-sig", errors="replace")))
    if EXTRA.exists():
        used.update(line.strip() for line in EXTRA.read_text(encoding="utf-8").splitlines() if line.strip() and not line.startswith("#"))
    return used


def built_codepoints():
    if not BUILT_FONT.exists():
        return set()
    return set(TTFont(BUILT_FONT).getBestCmap())


def main():
    check_only = "--check" in sys.argv
    known = icon_codepoints()
    used = icons_in_use()
    unknown = sorted(name for name in used if name not in known)
    if unknown:
        sys.exit(f"IconFont has no constant named: {', '.join(unknown)}")
    wanted = {known[name] for name in used}

    if check_only:
        missing = sorted(name for name in used if known[name] not in built_codepoints())
        print(f"{len(used)} icons in use; missing from the built font: {len(missing)}")
        for name in missing:
            print("  MISSING", name)
        sys.exit(1 if missing else 0)

    font = TTFont(FULL_FONT)
    if "fvar" in font:
        font = instancer.instantiateVariableFont(font, {axis.axisTag: axis.defaultValue for axis in font["fvar"].axes})

    options = subset.Options()
    options.layout_features = []
    options.hinting = False
    options.notdef_outline = True
    options.glyph_names = False
    options.name_IDs = [1, 2, 3, 4, 6]
    options.name_languages = [0x409]
    options.drop_tables += ["GSUB", "GPOS", "GDEF", "STAT", "MVAR", "HVAR", "avar", "fvar", "gvar", "cvar", "DSIG"]
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=sorted(wanted))
    subsetter.subset(font)

    BUILT_FONT.parent.mkdir(parents=True, exist_ok=True)
    font.save(BUILT_FONT)
    covered = set(TTFont(BUILT_FONT).getBestCmap())
    print(f"{len(used)} icons -> {BUILT_FONT.name}: {BUILT_FONT.stat().st_size:,} bytes "
          f"(full font {FULL_FONT.stat().st_size:,}); every icon present: {wanted <= covered}")


if __name__ == "__main__":
    main()
