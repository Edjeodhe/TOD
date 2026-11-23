"""16차: 속성 몬스터 파츠 프롬프트 만들기 (몬스터 4종 x 속성 8종)

python tools/make_element_enemy_prompts.py
→ tools/codex_prompts/enemy_<종류>_parts_<속성>.txt
참고 그림은 기존 파츠 시트(art_src/raw/enemy_<종류>_parts.png). 파츠 순서와 배치는 그대로 두고 속성 옷만 입힌다.
바람, 독은 그림에 초록이 들어가므로 자홍 키로 뽑는다(굽기에서 key 구분).
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
P = ROOT / "tools/codex_prompts"

TYPES = ["stalker", "charger", "shooter", "boss"]

ELEMENTS = {
    "fire": ("FIRE: charred, ember-cracked cloth and hide with thin glowing orange-red cracks, singed ragged edges, little flame tufts licking from the edges and the head, drifting ash flecks",
             "deep charcoal / burnt red / ember orange palette (#1A1012, #5A1E1A, #B8432A, #F08A3C)", "green"),
    "ice": ("ICE: frost-covered, pale frozen cloth with hanging icicles, sharp ice-crystal spikes growing from the shoulders and head, frosted rime edges, cold glowing eyes",
            "dark navy / frost blue / pale ice palette (#141B26, #33506E, #6F93B5, #CFE6F2)", "green"),
    "lightning": ("LIGHTNING: static-charged, frayed fur and threads standing on end, copper-wire stitches, small zig-zag spark marks crackling on the body, glowing yellow eyes",
                  "dark violet / storm purple / electric yellow palette (#1A1626, #3E3366, #6B5BA8, #F2D65C)", "green"),
    "light": ("LIGHT: ivory and pale-gold holy look, gilded stitches, stained-glass patches, a thin halo ring and small radiant spikes behind the head, glowing warm-white eyes",
              "dark umber / old gold / ivory palette (#221E18, #6E5E3E, #CBB98A, #FFF3C8)", "green"),
    "dark": ("DARK: inky black-violet shadow body, edges dissolving into curling smoke wisps, hollow eye sockets with glowing violet eyes, tattered void-black cloth",
             "near-black / deep violet / violet glow palette (#0B0910, #241A36, #3B2A55, #9B7BD8)", "green"),
    "blood": ("BLOOD: crimson blood-soaked rags and bandages, dripping red thread and blood drops hanging from the edges, raw stitched wounds, glowing red eyes",
              "black-maroon / dried blood / crimson palette (#160A0D, #4A1018, #7A1E2A, #D0485A)", "green"),
    "wind": ("WIND: billowing torn ribbons and scarves streaming backwards as if in a gale, swirl patterns stitched on the cloth, a few dry leaves caught in the fabric, pale grey-teal glowing eyes",
             "slate grey / grey-teal / pale sage palette (#1A2024, #3E5458, #5E8A86, #BFD9C8)", "magenta"),
    "poison": ("POISON: sickly toxic look, cloth stained and rotting, bubbling acid-green ooze dripping from the edges, small toadstool mushrooms and blisters growing on the body, glowing toxic-green eyes",
               "black-olive / moss / acid green palette (#121710, #2E3D1E, #4F6B2E, #A8D04A)", "magenta"),
}

BG = {
    "green": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key green (#00FF00) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no numbers. Do not use any bright green color inside the artwork itself.",
    "magenta": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key magenta (#FF00FF) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no numbers. Do not use any magenta or pink-purple color inside the artwork itself.",
}

HEAD = ("Use your built-in image generation tool to create the image (do not write code to draw it). Then copy the final generated PNG file "
        "into the current working directory at the path given below (create folders if needed). Do not modify anything else. Reply with only the saved path.")


def main():
    for t in TYPES:
        base = (P / f"enemy_{t}_parts.txt").read_text(encoding="utf-8").splitlines()
        order = next(l for l in base if l.startswith("(1)"))
        for e, (theme, palette, key) in ELEMENTS.items():
            text = "\n".join([
                HEAD,
                f"Save path: art_src/raw/enemy_{t}_parts_{e}.png",
                "Size: landscape 1536x1024.",
                "The attached image is a parts sheet of a monster for a 2D cutout bone rig. Recreate THE SAME sheet: the same monster, the SAME parts in the SAME order, "
                "the SAME positions, sizes, silhouettes and orientations on the sheet, so the new parts can replace the old ones on the same skeleton. "
                f"Only re-theme the monster as an elemental variant of {theme}. The element dressing must stay inside each part's silhouette or stick out only a little.",
                "Parts in reading order (left to right, top to bottom):",
                order,
                "RIGGING RULES (critical): every part is a SEPARATE piece with a wide empty gap (at least 70px) from every other piece, nothing touching or overlapping. "
                "Arm/leg segments STRAIGHT and pointing STRAIGHT DOWN with the joint end at the TOP and rounded caps at both ends. Torso upright facing right, head facing right with the neck end at the bottom.",
                "Art style: Tim Burton-like gothic, Don't Starve Together-like hand-drawn 2D paper cutout look, rough consistent-weight thick black outline around every shape, "
                f"slightly paper-grain texture INSIDE the shapes, low saturation, low brightness, {palette}.",
                BG[key],
            ]) + "\n"
            (P / f"enemy_{t}_parts_{e}.txt").write_text(text, encoding="utf-8")
    print("ok")


if __name__ == "__main__":
    main()
