// MCP 도구로 만들 수 없는 프로젝트 설정·파이프라인 에셋을 메뉴로 실행한다 (HANDOFF.md 작업 규칙).
// 씬의 게임 오브젝트는 여기서 만들지 않는다 — 그건 MCP로 직접 만든다.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace TOD.EditorTools
{
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string PipelinePath = SettingsDir + "/URP2D.asset";
        const string RendererPath = SettingsDir + "/Renderer2D.asset";
        const string PostFxMaterialPath = SettingsDir + "/PostFX.mat";
        const string PanelSettingsPath = "Assets/UI/PanelSettings.asset";
        const string ThemePath = "Assets/UI/Theme.tss";
        const float PixelsPerUnit = 100f;

        // 회피 통그림의 발끝(픽셀) — 웹판 render.js dodgeMeta 와 같은 값.
        static readonly Dictionary<string, Vector2> DodgeFeet = new()
        {
            { "hero_dodge_1", new Vector2(190, 466) },
            { "hero_dodge_2", new Vector2(292, 470) },
            { "hero_dodge_3", new Vector2(287.6f, 456) },
        };

        [MenuItem("TOD/Setup/1. Texture Imports")]
        public static void ConfigureTextureImports()
        {
            string[] folders = { "Assets/sprites", "Assets/vfx", "Assets/Art", "Assets/ref" };
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/Art/VFX/16/")) continue;   // 16차 Blender 시트는 TOD/Setup/16. Element Art 가 자른다 (여기서 Single 로 되돌리지 않게)
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && Configure(importer, path))
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/vfx/flip" }))
            {
                SliceFlipbook(AssetDatabase.GUIDToAssetPath(guid));
            }
            // 피해 숫자 아틀라스: 기본 + 속성·치명타별로 구운 것(tools/bake_element_fonts.py)이 모두 같은 글자 배치라 같은 JSON 으로 자른다
            const string fontJson = "art_src/fonts/damage_font.json";
            if (File.Exists(fontJson))
                foreach (string fontAtlas in Directory.GetFiles("Assets/Art/Generated", "damage_font*.png"))
                    SliceGlyphAtlas(fontAtlas.Replace('\\', '/'), fontJson);
            Debug.Log($"[TOD] Texture imports configured ({changed} changed)");
        }

        static bool Configure(TextureImporter importer, string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            bool flip = path.StartsWith("Assets/vfx/flip/");
            bool tiled = name == "floor" || name == "dash" || (name.StartsWith("bind_") && name.Contains("_thread"));   // 결박 실 몸통 리본은 길이 방향으로 반복

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = flip || name.StartsWith("damage_font") ? SpriteImportMode.Multiple : SpriteImportMode.Single;   // 그림 글꼴 아틀라스는 여러 장(잘라 둔 글자 유지)
            importer.spritePixelsPerUnit = PixelsPerUnit;
            // 스프라이트는 화면에서 3~7배 줄여 그리므로 밉맵으로 자글거림을 막는다 (바이어스는 0, 음수로 날카롭게 하면 움직일 때 지글거림).
            importer.mipmapEnabled = !flip && name != "dash" && name != "white";
            importer.mipMapBias = 0f;   // (2026-11-24) -0.6 → 0: 줄여 그린 배경의 가는 선이 카메라가 움직일 때 지글거렸다
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = name == "dash" ? FilterMode.Point : FilterMode.Trilinear;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // Tiled 그리기와 절단 셰이더가 사각 메시를 쓴다
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            if (name.StartsWith("enemy_"))
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;   // 적은 발밑 기준으로 그린다
            }
            else if (DodgeFeet.TryGetValue(name, out Vector2 foot))
            {
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(foot.x / w, 1f - foot.y / h);
            }
            importer.SetTextureSettings(settings);
            return true;
        }

        // 그림 글꼴 아틀라스(tools/bake_damage_font.py 가 만든 png + json)를 글자 스프라이트(name_<코드>)로 자른다. 피벗은 가운데.
        [System.Serializable] class GlyphRect { public string ch; public int x, y, w, h; }
        [System.Serializable] class GlyphAtlas { public int width, height; public GlyphRect[] glyphs; }

        static void SliceGlyphAtlas(string path, string jsonPath)
        {
            var atlas = JsonUtility.FromJson<GlyphAtlas>(File.ReadAllText(jsonPath));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            string baseName = Path.GetFileNameWithoutExtension(path);
            SpriteRect[] existing = provider.GetSpriteRects();
            var rects = new SpriteRect[atlas.glyphs.Length];
            for (int i = 0; i < atlas.glyphs.Length; i++)
            {
                GlyphRect g = atlas.glyphs[i];
                string name = $"{baseName}_{(int)g.ch[0]:x}";
                SpriteRect rect = existing.FirstOrDefault(r => r.name == name) ?? new SpriteRect { name = name, spriteID = GUID.Generate() };
                rect.rect = new Rect(g.x, atlas.height - g.y - g.h, g.w, g.h);   // json 은 위에서 아래, Unity 는 아래에서 위
                rect.alignment = SpriteAlignment.Center;
                rect.pivot = new Vector2(.5f, .5f);
                rects[i] = rect;
            }
            provider.SetSpriteRects(rects);
            if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() is { } names)
                names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        // 플립북 가로 스트립(정사각 프레임 N장)을 name_0..name_N-1 스프라이트로 자른다.
        static void SliceFlipbook(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int w, out int h);
            int frames = Mathf.Max(1, w / h);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            string baseName = Path.GetFileNameWithoutExtension(path);
            SpriteRect[] existing = provider.GetSpriteRects();
            var rects = new SpriteRect[frames];
            for (int i = 0; i < frames; i++)
            {
                string frameName = $"{baseName}_{i}";
                SpriteRect rect = existing.FirstOrDefault(r => r.name == frameName) ?? new SpriteRect { name = frameName, spriteID = GUID.Generate() };
                rect.rect = new Rect(i * h, 0, h, h);
                rect.alignment = SpriteAlignment.Center;
                rect.pivot = new Vector2(.5f, .5f);
                rects[i] = rect;
            }
            provider.SetSpriteRects(rects);
            if (provider.GetDataProvider<ISpriteNameFileIdDataProvider>() is { } names)
            {
                names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            }
            provider.Apply();
            importer.SaveAndReimport();
        }

        [MenuItem("TOD/Setup/2. Render Pipeline (URP 2D)")]
        public static void ConfigureRenderPipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            // 쿼터뷰 Y정렬: 화면 아래(월드 y가 작을수록)에 있는 스프라이트를 나중에 그린다.
            var so = new SerializedObject(renderer);
            so.FindProperty("m_TransparencySortMode").enumValueIndex = (int)TransparencySortMode.CustomAxis;
            so.FindProperty("m_TransparencySortAxis").vector3Value = Vector3.up;
            so.ApplyModifiedPropertiesWithoutUndo();

            var material = AssetDatabase.LoadAssetAtPath<Material>(PostFxMaterialPath);
            if (material != null && !renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().Any())
            {
                var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "PostFX";
                feature.passMaterial = material;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
                feature.fetchColorBuffer = true;
                AssetDatabase.AddObjectToAsset(feature, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var data = new SerializedObject(renderer);
                SerializedProperty features = data.FindProperty("m_RendererFeatures");
                SerializedProperty map = data.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (material == null)
            {
                Debug.LogWarning($"[TOD] {PostFxMaterialPath} not found — post FX feature skipped (create it via MCP, then rerun)");
            }
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[TOD] URP 2D pipeline configured");
        }

        [MenuItem("TOD/Setup/3. UI Panel Settings")]
        public static void ConfigurePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            Debug.Log("[TOD] UI panel settings configured");
        }

        [MenuItem("TOD/Setup/4. Player Settings")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.runInBackground = true;   // 창이 뒤에 있어도 플레이가 멈추지 않게 (검증·녹화용)
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.productName = "욕망의 탑";
            AssetDatabase.SaveAssets();
            Debug.Log("[TOD] Player settings configured");
        }

        // 플레이 중 게임 뷰(UI 포함)를 Temp/tod_capture.png 로 저장 — 검증용.
        [MenuItem("TOD/Debug/Capture Game View")]
        public static void CaptureGameView()
        {
            string path = Path.GetFullPath("Temp/tod_capture.png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[TOD] capture requested: {path}");
        }
    }
}
