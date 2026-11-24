"""22차 그림 프롬프트: 칠죄종 조각 아이콘, 신화 도안 6·7, 죄종 몬스터 파츠, 죄종 정적 소품

python tools/make_sin_prompts.py
→ tools/codex_prompts/sin_icons_<a..d>.txt, sin_patterns.txt, enemy_<종류>_parts_<죄종>.txt, decor_<죄종>.txt
돌리기: sh tools/codex_prompts/run_22.sh [icons|enemies|decor]   굽기: python tools/bake_22_art.py
시기(초록)는 자홍 키, 나머지는 초록 키.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
P = ROOT / "tools/codex_prompts"

HEAD = ("Use your built-in image generation tool to create the image (do not write code to draw it). Then copy the final generated PNG file "
        "into the current working directory at the path given below (create folders if needed). Do not modify anything else. Reply with only the saved path.")
STYLE = ("Art style: Tim Burton-like gothic, Don't Starve Together-like hand-drawn 2D paper cutout look, rough consistent-weight thick black outline around every shape, "
         "slightly paper-grain texture INSIDE the shapes, low saturation, low brightness")
BG = {
    "green": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key green (#00FF00) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no letters, no numbers, no grid lines. Do not use any bright green color inside the artwork itself.",
    "magenta": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key magenta (#FF00FF) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no letters, no numbers, no grid lines. Do not use any magenta, hot pink or pink-purple color inside the artwork itself.",
}
KEY = {"envy": "magenta"}

SINS = {
    "wrath": "WRATH (deep crimson #C62E2E accents, angry, wisps of hot red steam)",
    "sloth": "SLOTH (dusty blue-grey #6F8FA8 accents, sleepy, droopy and limp)",
    "envy": "ENVY (sickly envious green #4FA34A accents, sideways glancing jealous eyes)",
    "greed": "GREED (tarnished gold #D9A520 accents, gleaming coins and jewels)",
    "gluttony": "GLUTTONY (greasy orange #E07A2A accents, crumbs, food stains, gaping mouths)",
    "lust": "LUST (deep rose pink #D9418C accents, satin, lace, hearts and lips)",
    "pride": "PRIDE (royal purple #7B4FC9 accents with polished gold, crowns and mirrors)",
}

# 1, 2 일반 / 3 레어 / 4 유니크 / 5 레전더리 (HANDOFF 22차 조각 목록)
ICONS = {
    "wrath": ["a scorched brass thimble covered in black char marks, one thin curl of red smoke rising from it",
              "a dressmaker pin whose round head is a glowing cracked red-hot ember",
              "a round crimson pincushion with an angry furrowed scowling face, needles bristling out of it like spikes, little puffs of steam",
              "a pair of sharp tailor scissors, half open, the blades stained and dripping dark blood red",
              "a small antique black-and-crimson sewing machine roaring like a beast, its opening shaped like a howling fanged mouth, red glowing cracks, bursts of steam"],
    "sloth": ["a limp sagging skein of blue-grey yarn drooping lazily over itself",
              "a round blue-grey sewing button with two sleepy half-closed eyelids, wearing a tiny floppy nightcap",
              "a small soft pillow pouch with a loose drawstring, fluffy white stuffing spilling out",
              "a heavy dark iron plumb weight hanging from a huge thick knot of grey rope",
              "a wooden thread spool wound with blue-grey thread, peacefully asleep with closed eyes and a long floppy nightcap, a small dream cloud puff beside it (no letters)"],
    "envy": ["a green sewing button with one eye in its center glancing jealously sideways",
             "a little tangle of loose green thread snips and frayed ends",
             "someone else's fancy silver thimble, tied with a green ribbon and a small blank tag, looking stolen",
             "a bundle of green-headed basting pins jabbed hastily and crookedly into a scrap of cloth",
             "a long silver needle whose eye holds a tiny hand mirror reflecting a glaring green eye, thin green aura"],
    "greed": ["a round gold-leaf sewing button with four holes, gleaming",
              "a thread spool built from stacked gold coins, wound with gold thread",
              "a jeweled gold ring with a small blank paper pawn tag tied to it with string",
              "a ball of gold thread with little grasping thread hands reaching out of it",
              "a small ornate spinning wheel spinning glittering gold thread, gold coins scattered around its base"],
    "gluttony": ["a small cloth drawstring pouch bursting with bread crumbs and cookie bits",
                 "a spool of orange thread stained with greasy drips",
                 "a folded orange checkered tablecloth with food stains, its corner spilling endlessly",
                 "a dinner fork whose middle tine is a long sewing needle, a bite of roast meat skewered on it",
                 "a brass thimble with a gaping toothy mouth greedily swallowing a button, crumbs flying"],
    "lust": ["a deep rose pink satin ribbon bow",
             "a sewing button shaped like a tiny crystal perfume bottle, pink scent wisps curling out",
             "a small embroidery hoop with stitched rose pink lips in its center",
             "a single rose pink satin ballet slipper with its long ribbons twirling as if dancing",
             "two wooden spools bound together by entangled rose pink thread that forms a heart between them"],
    "pride": ["a polished royal purple sewing button with a gleaming star-shaped highlight",
              "a dressmaker pin whose head is a tiny golden crown",
              "a round tape measure case in purple and gold with a small vain hand mirror on its lid",
              "a purple velvet thimble standing proudly on a tall golden high heel",
              "a golden needle standing upright on a small purple velvet throne cushion, a tiny crown hanging at its eye"],
}
RARITY = "In each row the icons get more ornate and precious from left to right (1-2 common, 3 rare, 4 unique, 5 legendary with a faint glow of the set color)."

ICON_SHEETS = {"a": ["wrath", "sloth"], "b": ["greed", "gluttony"], "c": ["lust", "pride"], "d": ["envy"]}


def icon_sheet(key, sins):
    rows = []
    n = 1
    for r, s in enumerate(sins):
        rows.append(f"ROW {r + 1} = {SINS[s]}:")
        for d in ICONS[s]:
            rows.append(f"({n}) {d}")
            n += 1
    count = 5 * len(sins)
    grid = "5 columns x 2 rows" if len(sins) == 2 else "one row of 5 columns (centered vertically)"
    return "\n".join([
        HEAD,
        f"Save path: art_src/raw/sin_icons_{key}.png",
        "Size: landscape 1536x1024.",
        f"Create a game item icon sheet: {count} separate item icons in a {grid} grid, each icon about 220px, fully separated with wide empty gaps (at least 70px), "
        "nothing touching, each with its own thick black outline, three-quarter view, sewing-themed cursed trinkets of the seven deadly sins. Read order left to right, top to bottom:",
        *rows,
        RARITY,
        f"{STYLE}, muted dark palette (#16131B, #4A3D50 plus each set's accent color as the main color of the icon). Cute but eerie mood. Match the style of the reference image (an existing icon sheet from the same game).",
        BG[KEY.get(sins[0], "green")],
    ]) + "\n"


PATTERNS = "\n".join([
    HEAD,
    "Save path: art_src/raw/sin_patterns.png",
    "Size: landscape 1536x1024.",
    "The attached image is an existing icon sheet of keepsakes left behind by a missing old mouse TAILOR FATHER. Create TWO more icons in exactly the same style, "
    "size (each about 300px) and colors, side by side in one row (centered), fully separated with a wide empty gap, each with its own thick black outline, three-quarter view, "
    "warm and nostalgic but slightly eerie, ivory, brass and faded indigo (no set color):",
    "(1) THE FATHER'S EMPTY SPOOL: an old worn wooden thread spool that is completely empty, only one short frayed red thread end hanging from it, a tiny dent and old scratches",
    "(2) THE NAMELESS HOOP: a round wooden embroidery hoop stretched with blank yellowed linen, nothing embroidered except a faint chalk outline of a small empty name tag, a needle with red thread resting on the edge",
    f"{STYLE}. Cute but eerie mood.",
    BG["green"],
]) + "\n"

# 몬스터: 파츠 순서와 자리는 그대로, 죄종 옷과 장식은 새로 (16차 원본 시트를 참고 그림으로)
ENEMY = {
    "wrath": ("WRATH: a furious berserk look, torn crimson cloth with jagged red-hot cracked seams, short iron spikes and broken chain links stitched on, "
              "deep angry furrowed brows over burning red eyes, clenched clawed hands, little puffs of hot steam",
              "black-maroon / dark crimson / blood red palette (#170A0C, #4E1216, #8E2226, #C62E2E)"),
    "sloth": ("SLOTH: a lazy sleepy look, droopy limp blue-grey quilt patchwork cloth hanging loose, a long floppy striped nightcap on the head, heavy half-closed sleepy eyelids, "
              "a drool drop, cobwebs and dust on the cloth, pillow stuffing poking out of the seams, bandage-like slack wraps on the limbs",
              "slate / dusty blue-grey / pale grey palette (#151A20, #34424F, #6F8FA8, #B8C6D1)"),
    "envy": ("ENVY: a jealous look, sickly green rotting cloth covered with many small stitched-on extra eyes that all glance sideways, thorny bramble vines wrapping the limbs, "
             "a serpent-like green hood fringe, narrow glaring green eyes",
             "black-olive / swamp green / envy green palette (#111710, #263D22, #3E6B38, #4FA34A)"),
    "greed": ("GREED: a miserly look, tattered brown cloth with gold coins sewn on like scales, heavy gold chains and a small padlock, coin purse pouches hanging from the belt, "
              "gold rings on the claws, gleaming coin-shaped gold eyes",
              "dark umber / tarnished brass / gold palette (#1C150C, #4A3818, #8C6A22, #D9A520)"),
    "gluttony": ("GLUTTONY: a bloated hungry look, a round swollen bulging belly on the torso, a stained napkin bib tied at the neck, a huge gaping mouth with jagged teeth and a drooling tongue on the head, "
                 "a fork and spoon stuck into the cloth, food stains and crumbs, fat stubby limbs",
                 "dark brown / rust / greasy orange palette (#1B120C, #4C2A16, #8C4A1E, #E07A2A)"),
    "lust": ("LUST: a seductive look, deep rose silk and black lace with frilly ruffles, small heart-shaped stitched patches and ribbon bows, rose buds and thorns, "
             "long curled eyelashes over glowing pink eyes, a lipstick-red mouth",
             "dark plum / wine / rose pink palette (#1A0E16, #4A1A36, #8E2A5E, #D9418C)"),
    "pride": ("PRIDE: an arrogant royal look, rich royal purple velvet with polished gold trim and gold buttons, a small tilted golden crown on the head, a short ermine-trimmed royal cape "
              "draped over the shoulders of the torso, gold bracelets on the limbs, haughty half-lidded glowing violet eyes with the chin raised",
              "near-black / deep purple / royal purple / gold palette (#130E1C, #33224F, #7B4FC9, #D9B65A)"),
}


def enemy_prompt(t, s):
    base = (P / f"enemy_{t}_parts.txt").read_text(encoding="utf-8").splitlines()
    order = next(l for l in base if l.startswith("(1)"))
    theme, palette = ENEMY[s]
    return "\n".join([
        HEAD,
        f"Save path: art_src/raw/enemy_{t}_parts_{s}.png",
        "Size: landscape 1536x1024.",
        "The attached image is a parts sheet of a monster for a 2D cutout bone rig. Create a NEW sheet for the same skeleton: the SAME parts in the SAME order, "
        "at the SAME positions on the sheet, with the SAME sizes, proportions and orientations, so the new parts fit the same bones and joints. "
        f"Redesign the costume and look of the monster as a seven-deadly-sins variant: {theme}. "
        "Costume details may stick out a little beyond the original part silhouette (at most about 15%), but every joint end must stay where it is.",
        "Parts in reading order (left to right, top to bottom):",
        order,
        "RIGGING RULES (critical): every part is a SEPARATE piece with a wide empty gap (at least 70px) from every other piece, nothing touching or overlapping. "
        "Arm/leg segments STRAIGHT and pointing STRAIGHT DOWN with the joint end at the TOP and rounded caps at both ends. Torso upright facing right, head facing right with the neck end at the bottom.",
        f"{STYLE}, {palette}.",
        BG[KEY.get(s, "green")],
    ]) + "\n"


DECOR = {
    "wrath": (["smashed_mannequin", "broken_chair", "spiked_chains", "rage_crater"],
              "(1) a dress mannequin smashed and split, a pair of big scissors stabbed into it  (2) a splintered broken wooden chair kicked over  (3) a cluster of iron floor spikes wrapped in broken chains  "
              "(4) a flat floor decal: a cracked crater in the floor with dark red glowing cracks (seen from above, very flat)",
              "black-maroon / dark crimson / blood red"),
    "sloth": (["pillow_heap", "cobweb_armchair", "quilt_basket", "stopped_clock"],
              "(1) a lazy heap of soft pillows and a crumpled blanket  (2) a dusty sagging armchair covered in cobwebs  (3) a wicker basket with an unfinished quilt and abandoned needles hanging out  "
              "(4) a small stopped grandfather clock wrapped in cobwebs, its pendulum still",
              "slate / dusty blue-grey / pale grey"),
    "envy": (["eye_bramble", "peeping_wall", "snake_mannequin", "bile_puddle"],
             "(1) a thorny green bramble bush with many small eyes peeking out between the thorns  (2) a crumbling low stone wall piece with green eyes peering jealously through its cracks  "
             "(3) a dress mannequin with a green serpent coiled around it  (4) a flat floor decal: a puddle of green bile with a few staring eyes floating in it (seen from above, very flat)",
             "black-olive / swamp green / envy green"),
    "greed": (["coin_heap", "treasure_chest", "money_sacks", "golden_scale"],
              "(1) a heap of gold coins and gold buttons  (2) an open treasure chest overflowing with gold thread spools and coins, a padlock hanging from it  "
              "(3) a stack of fat tied money sacks (no letters, no symbols)  (4) a brass balance scale tipped by a pile of gold coins",
              "dark umber / tarnished brass / gold"),
    "gluttony": (["feast_table", "spilled_cauldron", "gnawed_bones", "cake_tower"],
                 "(1) a small overloaded table with a half-eaten roast, crumbs and greasy plates  (2) a toppled cauldron spilling thick orange stew  "
                 "(3) a pile of gnawed drumstick bones and cheese rinds  (4) a tall lopsided layer cake with cream drips and a giant fork stuck in it",
                 "dark brown / rust / greasy orange"),
    "lust": (["rose_vase", "velvet_chaise", "perfume_vanity", "lace_gown"],
             "(1) a vase of wilting deep rose pink roses with fallen petals  (2) a small rose velvet fainting couch with heart-shaped cushions  "
             "(3) a little vanity table with perfume bottles and a heart-shaped mirror  (4) a dress mannequin wearing a frilly rose pink lace gown",
             "dark plum / wine / rose pink"),
    "pride": (["throne", "vain_mirror", "royal_mannequin", "proud_bust"],
              "(1) a small gilded throne with a royal purple velvet seat  (2) a tall ornate gilded standing mirror  "
              "(3) a dress mannequin wearing a royal purple robe and a golden crown  (4) a marble pedestal with a proud mouse bust wearing a laurel crown, chin raised",
              "near-black / deep purple / royal purple / gold"),
}


def decor_prompt(s):
    names, desc, palette = DECOR[s]
    return "\n".join([
        HEAD,
        f"Save path: art_src/raw/decor_{s}.png",
        "Size: landscape 1536x1024.",
        "The attached image is the floor tile of the game, for color and style reference. Create FOUR separate decorative map props for a top-down-ish 3/4 view gothic arena floor, "
        "laid out in a 2 columns x 2 rows grid, each prop centered in its own cell, nothing touching the cell edges, wide empty gaps. Props in reading order:",
        desc,
        "They sit on the floor in 3/4 front view (decals are flat, seen from above). Medium size, readable silhouettes.",
        f"{STYLE}, {palette} palette. Cute but eerie mood.",
        BG[KEY.get(s, "green")],
    ]) + "\n"


def main():
    for k, sins in ICON_SHEETS.items():
        (P / f"sin_icons_{k}.txt").write_text(icon_sheet(k, sins), encoding="utf-8")
    (P / "sin_patterns.txt").write_text(PATTERNS, encoding="utf-8")
    for t in ["stalker", "charger", "shooter", "boss"]:
        for s in ENEMY:
            (P / f"enemy_{t}_parts_{s}.txt").write_text(enemy_prompt(t, s), encoding="utf-8")
    for s in DECOR:
        (P / f"decor_{s}.txt").write_text(decor_prompt(s), encoding="utf-8")
    print("ok")


if __name__ == "__main__":
    main()
