// 16차: 속성 그림을 데이터(GameDatabase.elements)에 연결하는 메뉴. 그림이 들어올 때마다 다시 실행하면 된다 (MCP execute_menu_item).
//  · Assets/Art/VFX/16/*.png (Blender 시트, element-vfx 세션): 가로 스트립을 정사각 칸으로 자르고 피벗은 art_src/vfx/16/sheets.json.
//      proc_<속성> → procFx · boss_<속성>_slam → boss.fx · boss_<속성>_bullet → boss.bullet · boss_<속성>_pool → boss.pool
//      decor_<속성>_loop → animated · bind_<속성>_burst → bindBurst   (각 <이름>_glow.png 는 같은 클립의 빛)
//  · Assets/sprites/enemy_<종류>_<파츠>_<속성>.png → skins (원래 파츠 enemy_<종류>_<파츠>.png 와 가져오기 설정·피벗을 맞춘다)
//  · Assets/sprites/decor_<속성>_<이름>.png → props
// 크기·세기 같은 수치는 건드리지 않는다 (fps 만 시트 메타에서 넣는다). 씬은 저장하지 않는다 (Unity 잠금을 잡은 쪽이 저장).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace TOD.EditorTools
{
    public static class ElementArtSetup
    {
        const string VfxDir = "Assets/Art/VFX/16";
        const string SheetMeta = "art_src/vfx/16/sheets.json";
        const string SpriteDir = "Assets/sprites";

        static readonly Dictionary<Element, string> Ids = new()
        {
            { Element.Fire, "fire" }, { Element.Ice, "ice" }, { Element.Lightning, "lightning" }, { Element.Light, "light" },
            { Element.Dark, "dark" }, { Element.Blood, "blood" }, { Element.Wind, "wind" }, { Element.Poison, "poison" },
        };

        [Serializable] class SheetInfo { public int frames; public int size; public float fps; public bool loop; public float[] pivot; }

        [MenuItem("TOD/Setup/16. Element Art")]
        public static void Run()
        {
            GameDatabase db = UnityEngine.Object.FindAnyObjectByType<GameDatabase>();
            if (db == null) { Debug.LogError("[TOD] GameDatabase 가 열린 씬에 없습니다"); return; }
            Dictionary<string, SheetInfo> sheets = ReadSheets();
            int sliced = SliceSheets(sheets);
            int swapped = ImportSkins();
            Undo.RecordObject(db, "Element Art");
            db.elements ??= new ElementConfig();
            ElementConfig ec = db.elements;
            var defs = (ec.defs ?? Array.Empty<ElementDef>()).ToList();
            var report = new List<string>();
            foreach (var (element, id) in Ids)
            {
                ElementDef def = defs.Find(d => d.element == element);
                if (def == null) { def = new ElementDef { element = element, boss = new BossElementAttack() }; defs.Add(def); }
                def.boss ??= new BossElementAttack();
                int n = 0;
                n += Fill(ref def.procFx, $"proc_{id}", sheets);
                n += Fill(ref def.boss.fx, $"boss_{id}_slam", sheets);
                n += Fill(ref def.boss.bullet, $"boss_{id}_bullet", sheets);
                n += Fill(ref def.boss.pool, $"boss_{id}_pool", sheets);
                n += Fill(ref def.bindBurst, $"bind_{id}_burst", sheets);
                FlipClip loop = null;
                if (Fill(ref loop, $"decor_{id}_loop", sheets) > 0)
                {
                    AnimatedDecor existing = def.animated?.FirstOrDefault(a => a?.clip != null);
                    if (existing != null) { existing.clip.frames = loop.frames; existing.clip.glow = loop.glow; if (existing.clip.fps <= 0) existing.clip.fps = loop.fps; }
                    else def.animated = new[] { new AnimatedDecor { clip = loop } };
                    n++;
                }
                // 몬스터 파츠
                var skins = new List<SpriteSwap>();
                foreach (string path in Directory.GetFiles(SpriteDir, $"enemy_*_{id}.png"))
                {
                    string file = Path.GetFileNameWithoutExtension(path);
                    string basePath = $"{SpriteDir}/{file.Substring(0, file.Length - id.Length - 1)}.png";
                    Sprite from = AssetDatabase.LoadAssetAtPath<Sprite>(basePath), to = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'));
                    if (from != null && to != null) skins.Add(new SpriteSwap { from = from, to = to });
                }
                def.skins = skins.ToArray();
                // 정적 소품
                var props = def.props?.Where(p => p?.sprite != null).ToList() ?? new List<DecorDef>();
                foreach (string path in Directory.GetFiles(SpriteDir, $"decor_{id}_*.png"))
                {
                    if (Path.GetFileNameWithoutExtension(path).EndsWith("_loop")) continue;
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'));
                    if (sprite != null && props.All(p => p.sprite != sprite)) props.Add(new DecorDef { sprite = sprite, weight = 1 });
                }
                def.props = props.ToArray();
                report.Add($"{id}: 이펙트 {n}, 파츠 {skins.Count}, 소품 {props.Count}");
            }
            ec.defs = defs.ToArray();
            // 21차 증강 성장 (tools/render_augment_growth.py)
            db.visual ??= new VisualConfig();
            int growth = Fill(ref db.visual.growthBack, "growth_back", sheets) + Fill(ref db.visual.growthFront, "growth_front", sheets);
            report.Add($"증강 성장: {growth}");
            EditorUtility.SetDirty(db);
            EditorSceneManager.MarkSceneDirty(db.gameObject.scene);
            Debug.Log($"[TOD] Element Art: 시트 {sliced}장 자름, 파츠 {swapped}장 설정\n" + string.Join("\n", report));
        }

        static Dictionary<string, SheetInfo> ReadSheets()
        {
            var result = new Dictionary<string, SheetInfo>();
            if (!File.Exists(SheetMeta)) return result;
            // JsonUtility 는 최상위 사전을 못 읽어서 이름별로 잘라 읽는다: { "이름": { ... }, ... }
            string json = File.ReadAllText(SheetMeta);
            int i = 0;
            while ((i = json.IndexOf('"', i)) >= 0)
            {
                int end = json.IndexOf('"', i + 1);
                string key = json.Substring(i + 1, end - i - 1);
                int open = json.IndexOf('{', end);
                int colon = json.IndexOf(':', end);
                if (open < 0 || colon < 0 || colon > open) { i = end + 1; continue; }
                int depth = 0, close = open;
                for (; close < json.Length; close++) { if (json[close] == '{') depth++; else if (json[close] == '}' && --depth == 0) break; }
                result[key] = JsonUtility.FromJson<SheetInfo>(json.Substring(open, close - open + 1));
                i = close + 1;
            }
            return result;
        }

        // 가로 스트립 → 정사각 칸 (피벗은 시트 메타, 없으면 가운데)
        static int SliceSheets(Dictionary<string, SheetInfo> sheets)
        {
            if (!Directory.Exists(VfxDir)) return 0;
            int count = 0;
            foreach (string file in Directory.GetFiles(VfxDir, "*.png"))
            {
                string path = file.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                string sheet = name.EndsWith("_glow") ? name.Substring(0, name.Length - 5) : name;
                sheets.TryGetValue(sheet, out SheetInfo info);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                int frames = Mathf.Max(1, w / Mathf.Max(1, h));
                Vector2 pivot = info?.pivot is { Length: 2 } p ? new Vector2(p[0], p[1]) : new Vector2(.5f, .5f);
                bool dirty = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Multiple
                    || importer.mipmapEnabled || importer.maxTextureSize < Mathf.NextPowerOfTwo(Mathf.Max(w, h)) || !importer.alphaIsTransparency;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, Mathf.Min(16384, Mathf.NextPowerOfTwo(Mathf.Max(w, h))));
                importer.spritePixelsPerUnit = 100;

                var factory = new SpriteDataProviderFactories();
                factory.Init();
                ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                SpriteRect[] existing = provider.GetSpriteRects();
                var rects = new SpriteRect[frames];
                for (int i = 0; i < frames; i++)
                {
                    string frameName = $"{name}_{i}";
                    SpriteRect rect = existing.FirstOrDefault(r => r.name == frameName) ?? new SpriteRect { name = frameName, spriteID = GUID.Generate() };
                    if (rect.rect != new Rect(i * h, 0, h, h) || rect.pivot != pivot) dirty = true;
                    rect.rect = new Rect(i * h, 0, h, h);
                    rect.alignment = SpriteAlignment.Custom;
                    rect.pivot = pivot;
                    rects[i] = rect;
                }
                if (existing.Length != frames) dirty = true;
                if (!dirty) continue;
                provider.SetSpriteRects(rects);
                if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() is { } names)
                    names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
                provider.Apply();
                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        static Sprite[] Frames(string path)
        {
            if (!File.Exists(path)) return null;
            string name = Path.GetFileNameWithoutExtension(path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .Select(s => (s, ok: int.TryParse(s.name.Substring(s.name.LastIndexOf('_') + 1), out int k), k))
                .Where(t => t.ok && t.s.name.StartsWith(name + "_"))
                .OrderBy(t => t.k).Select(t => t.s).ToArray();
        }

        // 시트가 있으면 클립을 채운다 (없으면 그대로). 채웠으면 1.
        static int Fill(ref FlipClip clip, string sheet, Dictionary<string, SheetInfo> sheets)
        {
            Sprite[] frames = Frames($"{VfxDir}/{sheet}.png");
            if (frames == null || frames.Length == 0) return 0;
            clip ??= new FlipClip();
            clip.frames = frames;
            clip.glow = Frames($"{VfxDir}/{sheet}_glow.png") ?? Array.Empty<Sprite>();
            if (sheets.TryGetValue(sheet, out SheetInfo info) && info.fps > 0) clip.fps = info.fps;
            if (clip.tint.a <= 0) clip.tint = Color.white;
            return 1;
        }

        // 속성 파츠: 원래 파츠와 같은 가져오기 설정(크기, 피벗, PPU)으로 맞춘다 → 리그에 끼워도 크기와 회전 중심이 같다
        static int ImportSkins()
        {
            int count = 0;
            foreach (string id in Ids.Values)
            {
                foreach (string file in Directory.GetFiles(SpriteDir, $"enemy_*_{id}.png"))
                {
                    string path = file.Replace('\\', '/');
                    string name = Path.GetFileNameWithoutExtension(path);
                    string basePath = $"{SpriteDir}/{name.Substring(0, name.Length - id.Length - 1)}.png";
                    if (AssetImporter.GetAtPath(basePath) is not TextureImporter source || AssetImporter.GetAtPath(path) is not TextureImporter target) continue;
                    var settings = new TextureImporterSettings();
                    source.ReadTextureSettings(settings);
                    source.GetSourceTextureWidthAndHeight(out int bw, out int bh);
                    target.GetSourceTextureWidthAndHeight(out int tw, out int th);
                    var current = new TextureImporterSettings();
                    target.ReadTextureSettings(current);
                    // 그림 크기가 다르면 PPU 를 비율만큼 바꿔 같은 크기로 그려지게 한다 (피벗은 비율 좌표라 그대로)
                    float ppu = settings.spritePixelsPerUnit * (bh > 0 ? th / (float)bh : 1);
                    bool dirty = target.textureType != TextureImporterType.Sprite || current.spriteAlignment != settings.spriteAlignment
                        || current.spritePivot != settings.spritePivot || Mathf.Abs(current.spritePixelsPerUnit - ppu) > .01f || current.spriteMode != settings.spriteMode;
                    if (!dirty) continue;
                    settings.spritePixelsPerUnit = ppu;
                    target.SetTextureSettings(settings);
                    target.textureType = TextureImporterType.Sprite;
                    target.SaveAndReimport();
                    count++;
                }
            }
            return count;
        }
    }
}
