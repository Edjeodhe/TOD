"""16차: 층 속성 정적 장식 프롬프트 (속성 8종 × 소품 4개, 2×2 칸)

python tools/make_element_decor_prompts.py → tools/codex_prompts/decor_<속성>.txt
굽기: bake_16_art.py 의 bake_decor → Assets/sprites/decor_<속성>_<이름>.png
움직이는 장식(불꽃 일렁임 등)은 Blender(element-vfx 세션)가 따로 만든다. 여기 소품은 그 위에 얹힐 정지 그림이다.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
P = ROOT / "tools/codex_prompts"

DECOR = {
    "fire": (["brazier", "charred_mannequin", "ember_pile", "scorched_machine"],
             "(1) an iron brazier bowl on three legs full of glowing coals with a few small flames  (2) a charred, half-burnt dress mannequin with glowing ember cracks  (3) a low pile of smoldering embers, ash and burnt thread spools  (4) a scorched broken sewing machine with small flames on it",
             "charcoal / burnt red / ember orange", "green"),
    "ice": (["frozen_mannequin", "icicle_candle", "snow_yarn", "ice_crystals"],
            "(1) a dress mannequin frozen inside a block of ice  (2) an iron candlestick hung with long icicles, frozen candle  (3) a small snow drift with frozen yarn balls sticking out  (4) a cluster of sharp ice crystals growing from the floor",
            "dark navy / frost blue / pale ice", "green"),
    "lightning": (["spool_coil", "sparking_lamp", "copper_tangle", "struck_crack"],
                  "(1) a tall tesla-coil tower built from stacked thread spools and copper wire  (2) a broken street lamp with a cracked bulb crackling  (3) a tangled heap of copper wire and needles  (4) a flat floor decal: a scorched branching lightning crack burnt into the floor (seen from above, very flat)",
                  "dark violet / storm purple / electric yellow", "green"),
    "light": (["glass_shards", "candle_altar", "gilded_mirror", "praying_statue"],
              "(1) a pile of broken stained-glass shards  (2) a small stone altar crowded with lit white candles  (3) a tall gilded mirror on a stand glowing softly  (4) a small worn stone statue of a praying mouse with a halo",
              "dark umber / old gold / ivory", "green"),
    "dark": (["shadow_crack", "thorn_cage", "void_doll", "dark_obelisk"],
             "(1) a flat floor decal: a black crack in the floor with shadow tendrils creeping out (seen from above, very flat)  (2) a black thorny birdcage, empty, door open  (3) a slumped rag doll with hollow glowing violet eyes  (4) a small black stone obelisk with faint violet runes",
             "near-black / deep violet / violet glow", "green"),
    "blood": (["blood_pool", "rag_rack", "thread_cocoon", "bleeding_cushion"],
              "(1) a flat floor decal: a dark red blood pool with drips and a stray needle (seen from above, very flat)  (2) a wooden drying rack hung with blood-stained rags  (3) a cocoon tightly wound from red thread, hanging from a short hook post  (4) a split heart-shaped pincushion leaking red stuffing",
              "black-maroon / dried blood / crimson", "green"),
    "wind": (["pinwheel_stand", "torn_sail", "leaf_pile", "weathervane"],
             "(1) a tall stand with a big paper pinwheel  (2) a wooden post with a torn cloth sail flapping  (3) a swirling pile of dry leaves and ribbon scraps  (4) a crooked iron weathervane shaped like a mouse",
             "slate grey / grey-teal / pale sage", "magenta"),
    "poison": (["toxic_puddle", "mushroom_cluster", "cracked_vials", "rotting_stump"],
               "(1) a flat floor decal: a bubbling toxic acid-green puddle (seen from above, very flat)  (2) a cluster of sickly spotted toadstools  (3) a little wooden crate of cracked potion vials leaking green  (4) a rotting tree stump with puffing spore pods",
               "black-olive / moss / acid green", "magenta"),
}

HEAD = ("Use your built-in image generation tool to create the image (do not write code to draw it). Then copy the final generated PNG file "
        "into the current working directory at the path given below (create folders if needed). Do not modify anything else. Reply with only the saved path.")
BG = {
    "green": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key green (#00FF00) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no numbers, no grid lines. Do not use any bright green color inside the artwork itself.",
    "magenta": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key magenta (#FF00FF) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no numbers, no grid lines. Do not use any magenta or pink-purple color inside the artwork itself.",
}

for e, (names, desc, palette, key) in DECOR.items():
    (P / f"decor_{e}.txt").write_text("\n".join([
        HEAD,
        f"Save path: art_src/raw/decor_{e}.png",
        "Size: landscape 1536x1024.",
        "The attached image is the floor tile of the game, for color and style reference. Create FOUR separate decorative map props for a top-down-ish 3/4 view gothic arena floor, "
        "laid out in a 2 columns x 2 rows grid, each prop centered in its own cell, nothing touching the cell edges, wide empty gaps. Props in reading order:",
        desc,
        "They sit on the floor in 3/4 front view (decals are flat, seen from above). Medium size, readable silhouettes.",
        "Art style: Tim Burton-like gothic, Don't Starve Together-like hand-drawn 2D paper cutout look, rough consistent-weight thick black outline, "
        f"slightly paper-grain texture INSIDE the shapes, low saturation, low brightness, {palette} palette. Cute but eerie mood.",
        BG[key],
    ]) + "\n", encoding="utf-8")
print("ok")
