"""23차-boss7: 칠죄종 보스 7종 파츠 시트 프롬프트 (종류마다 다른 괴물, 뼈대는 기존 셋 중 하나)

python tools/make_boss7_prompts.py  → tools/codex_prompts/boss7_<죄종>.txt
돌리기: sh tools/codex_prompts/run_boss7.sh [죄종...]   굽기: python tools/bake_boss7_art.py

뼈대(참고 시트)는 기존 몬스터 리그를 그대로 쓴다 → 관절 자리가 같아서 파츠만 바꿔 끼운다.
  robe    = enemy_boss_parts.png     (머리, 몸통, 치마, 위팔, 아래팔)      : 교만, 시기, 색욕, 나태
  stalker = enemy_stalker_parts.png  (머리, 몸통, 위팔, 아래팔, 허벅지, 정강이) : 탐욕, 식탐
  charger = enemy_charger_parts.png  (머리, 몸통, 위팔, 아래팔, 허벅지, 정강이, 발) : 분노
시기는 초록 몸이라 자홍 키, 나머지는 초록 키.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
P = ROOT / "tools/codex_prompts"

HEAD = ("Use your built-in image generation tool to create the image (do not write code to draw it). Then copy the final generated PNG file "
        "into the current working directory at the path given below (create folders if needed). Do not modify anything else. Reply with only the saved path.")
STYLE = ("Art style: Tim Burton-like gothic, Don't Starve Together-like hand-drawn 2D paper cutout look, rough consistent-weight thick black outline around every shape, "
         "slightly paper-grain texture INSIDE the shapes, moderate saturation (the sin color should read clearly), not neon")
BG = {
    "green": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key green (#00FF00) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no letters, no numbers, no grid lines. Do not use any bright green color inside the artwork itself.",
    "magenta": "BACKGROUND RULE (critical): the background must be ONE perfectly flat solid pure chroma-key magenta (#FF00FF) everywhere, no gradient, no texture, no vignette, no drop shadows, no ground shadow, no text, no labels, no letters, no numbers, no grid lines. Do not use any magenta, hot pink or pink-purple color inside the artwork itself.",
}
KEY = {"envy": "magenta"}
REF = {"robe": "art_src/raw/enemy_boss_parts.png", "stalker": "art_src/raw/enemy_stalker_parts.png", "charger": "art_src/raw/enemy_charger_parts.png"}
SIZE = {"robe": "portrait 1024x1536", "stalker": "landscape 1536x1024", "charger": "landscape 1536x1024"}

# 죄종: (뼈대, 한 줄 컨셉, 파츠 읽기 순서 설명, 팔레트)
BOSSES = {
    "pride": ("robe",
              "PRIDE - the Peacock Mirror King, a towering haughty monarch. His face is a cracked oval silver hand-mirror mask with a cold sneer painted on it, "
              "framed by a big fan crest of peacock feathers with eye-spots, a very tall thin pointed gold crown on top. The robe torso is a royal purple doublet "
              "with gold filigree, a huge stiff white ruff collar and rows of gold medals. The lower part is a long sweeping train of fanned peacock tail feathers "
              "(deep teal and purple eye-spots) under an ermine-trimmed purple mantle",
              "(1) head: the mirror-mask face with the peacock feather crest and the tall gold crown, neck end at the bottom  (2) upper body: the purple doublet torso with ruff collar and shoulders, without arms  "
              "(3) lower body: the long peacock-feather train and mantle hanging down (no legs visible), its top end is the waist  (4) upper arm: purple puffed sleeve with gold bands  "
              "(5) forearm with a long white-gloved hand holding a slender gold scepter that points straight down along the arm",
              "royal purple / polished gold / deep teal / ermine white palette (#24143A, #6B3FA0, #D9A93A, #2E7F86, #EDE6D6)"),
    "greed": ("stalker",
              "GREED - the Crowned Ogre Mammon, a huge brutish hunched ogre king. Ugly fat ogre head with two big lower tusks, a greedy drooling grin, small glowing gold-coin eyes, "
              "a small heavy jeweled gold crown tilted on his bald head. Massive hunched bare tan-grey ogre chest and belly with a thick gold chain necklace with a padlock, "
              "a big patched coin sack strapped on his back spilling gold coins, a leather belt with a huge gold buckle and coin pouches. Thick muscular arms with gold arm rings, "
              "huge fist with many gold rings. Thick legs in torn brown leather breeches, big bare ogre feet with a gold anklet. His skin is tan-grey, NOT green",
              "(1) head: the crowned tusked ogre head facing right, neck end at the bottom  (2) torso: the huge hunched ogre chest and belly with the coin sack on the back, without arms or legs  "
              "(3) upper arm: thick muscular ogre upper arm with a gold arm ring  (4) forearm with the huge ringed ogre fist  (5) thigh: thick leg in torn brown breeches  (6) shin with the big bare ogre foot",
              "tan-grey ogre skin / dark umber leather / bright shiny gold palette (#2A2018, #8C7A66, #5A3A1E, #D9A520, #FFD24A)"),
    "envy": ("robe",
             "ENVY - the Green-Eyed Serpent Queen, a lamia witch. A gaunt pale grey-green woman's face glancing sideways with huge jealous glowing yellow-green eyes, "
             "hair made of many thin writhing green snakes, a crown of thorny brambles studded with little staring eyes. The torso is a dark emerald scaled bodice wrapped in thorn vines, "
             "with many small eyes on it glancing sideways. Instead of a robe, the lower body is a long thick coiled serpent tail with dark emerald scales and a pale belly, "
             "coiling once and tapering to a tip at the bottom. Thin scaly arms wrapped in thorn vines, long thin clawed fingers dripping green poison",
             "(1) head: the snake-haired jealous witch face with the bramble crown, neck end at the bottom  (2) upper body: the scaled emerald bodice with thorn vines and eyes, without arms  "
             "(3) lower body: the long coiled serpent tail hanging down (no legs), its top end is the waist  (4) upper arm: thin scaly arm wrapped in a thorn vine  "
             "(5) forearm with the long thin clawed hand dripping poison",
             "black-olive / dark emerald / envy green / sickly yellow-green palette (#111710, #1F3A26, #3E6B38, #6FA84A, #C8D86A)"),
    "wrath": ("charger",
              "WRATH - the Burning Bull Berserker, a raging minotaur charging forward. A furious bull head with huge curved black horns glowing red-hot at the tips, "
              "flaring nostrils blowing red steam, blazing red eyes, a broken iron nose ring. A massive forward-leaning dark crimson hairy bull body with cracked glowing lava veins, "
              "scarred, broken chains and shackles hanging from it, a burning mane. Muscular arms with spiked iron bracers and big clawed fists, "
              "hoofed bull legs with iron shackles, burning embers",
              "(1) head: the raging horned bull head facing right with the neck end at the bottom left  (2) body: the huge forward-leaning hairy bull torso with burning mane and chains, without arms or legs  "
              "(3) upper arm with a spiked iron bracer  (4) forearm with the big clawed fist  (5) thigh: the muscular bull thigh  (6) shin: the bull shin with an iron shackle  (7) foot: the cloven black hoof",
              "black-maroon / dark crimson / blood red / ember orange palette (#1A0A0A, #4A1010, #A3201E, #E4572E)"),
    "lust": ("robe",
             "LUST - the Moth Lady of Velvet, a beautiful but eerie noble woman with moth features. A pale porcelain masquerade face with heavy-lidded eyes and dark red lips, "
             "feathery moth antennae, a veil and a rose-red heart-shaped hat. The torso is a tight deep rose satin corset with lace and a heart brooch, "
             "with big soft dusty rose moth wings folded on the back of the torso. The lower body is a wide frilly layered ball gown skirt of rose satin and black lace with hearts. "
             "Long sleeves of sheer lace, long fingers with painted nails and a rose",
             "(1) head: the masquerade moth-lady face with antennae, veil and heart hat, neck end at the bottom  (2) upper body: the rose corset torso with the folded moth wings on its back, without arms  "
             "(3) lower body: the wide frilly ball gown skirt hanging down (no legs visible), its top end is the waist  (4) upper arm: puffed sheer lace sleeve  "
             "(5) forearm with the long elegant gloved hand holding a red rose",
             "black / wine / deep rose / dusty pink palette (#1A0A12, #5A1030, #B8306A, #E890B0)"),
    "gluttony": ("stalker",
                 "GLUTTONY - the Swine Head Chef, an enormously fat pig-headed butcher cook. A greasy pig head with a huge gaping mouth full of crooked teeth, "
                 "a long drooling tongue, tiny greedy eyes and a crooked dirty chef's toque. A gigantic round bloated belly torso stretching a stained apron and a napkin bib, "
                 "a string of sausages around the neck, bits of food and crumbs stuck on it. Fat stubby arms with rolled-up sleeves, a fat hand gripping a big meat cleaver "
                 "pointing straight down. Short fat legs in checkered trousers, round pig trotters",
                 "(1) head: the drooling pig head with the chef's toque facing right, neck end at the bottom  (2) torso: the gigantic bloated belly with the stained apron and sausage string, without arms or legs  "
                 "(3) upper arm: fat arm with a rolled-up sleeve  (4) forearm with the fat hand gripping a meat cleaver pointing down  (5) thigh: short fat leg in checkered trousers  (6) shin with the round pig trotter",
                 "greasy brown / pink-grey pig skin / orange / stained white palette (#2A1A10, #B88A7A, #E07A2A, #E8DCC0)"),
    "sloth": ("robe",
              "SLOTH - the Dreaming Slug Abbot, a gigantic sleepy slug-monk. A droopy heavy-lidded drowsy face half asleep with a long drool string, "
              "a very long floppy striped nightcap with a pom-pom, soft snail eye-stalks drooping. The torso is a sagging blue-grey monk habit with a patchwork quilt shawl "
              "and a stopped pocket watch hanging from it. Instead of a robe the lower body is a huge soft slimy blue-grey slug body with a big spiral snail shell on its back "
              "patched like a quilt, oozing a little slime at the bottom. Limp heavy arms in loose sleeves, a soft hand hugging a small pillow",
              "(1) head: the drowsy slug-monk face with the long striped nightcap, neck end at the bottom  (2) upper body: the sagging monk habit with the quilt shawl and stopped watch, without arms  "
              "(3) lower body: the soft slug body with the big quilted spiral shell, hanging down (no legs), its top end is the waist  (4) upper arm: limp loose sleeve  "
              "(5) forearm with the soft hand hugging a small pillow",
              "dusty blue-grey / pale lavender / quilt patch palette (#1A1E26, #4A5A70, #6F8FA8, #B8C4D6)"),
}


def prompt(sin):
    rig, concept, order, palette = BOSSES[sin]
    return "\n".join([
        HEAD,
        f"Save path: art_src/raw/boss7_{sin}.png",
        f"Size: {SIZE[rig]}.",
        "The attached image is a parts sheet of a monster for a 2D cutout bone rig. Create a NEW sheet for the same skeleton: the SAME number of parts in the SAME order, "
        "at the SAME positions on the sheet, with similar sizes and the SAME orientations, so the new parts fit the same bones and joints. "
        f"But draw a completely DIFFERENT creature, a floor boss of the seven deadly sins: {concept}. "
        "It must read at a glance even when shown small in the game: strong silhouette, the big iconic features clearly visible, strong dominant sin color. "
        "Body parts may be bulkier or stick out beyond the original part silhouette (up to about 25%), but every joint end must stay where it is.",
        "Parts in reading order (left to right, top to bottom):",
        order,
        "RIGGING RULES (critical): every part is a SEPARATE piece with a wide empty gap (at least 70px) from every other piece, nothing touching or overlapping. "
        "Arm/leg segments STRAIGHT and pointing STRAIGHT DOWN with the joint end at the TOP and rounded caps at both ends. Body facing right, head facing right with the neck end at the bottom.",
        f"{STYLE}, {palette}.",
        BG[KEY.get(sin, "green")],
    ]) + "\n"


if __name__ == "__main__":
    for s in BOSSES:
        (P / f"boss7_{s}.txt").write_text(prompt(s), encoding="utf-8")
        print("wrote", f"boss7_{s}.txt", BOSSES[s][0])
