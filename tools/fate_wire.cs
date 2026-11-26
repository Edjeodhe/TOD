// 24차 미래 보기 셀 애니 연결: MCP execute_code 본문(CodeDom C# 6 에서도 돌게 지역 함수 대신 람다). 보내기 전에 __sin__ 을 폴더 이름(greed), __Sin__ 을 Element 이름(Greed)으로 바꿈.
string dir = "Assets/Art/Fate/__sin__";
var paths = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Select(g => UnityEditor.AssetDatabase.GUIDToAssetPath(g)).OrderBy(p => p).ToList();
foreach (var p in paths) {
  var imp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(p);
  bool ch = imp.mipmapEnabled || imp.npotScale != UnityEditor.TextureImporterNPOTScale.None || imp.wrapMode != TextureWrapMode.Clamp || imp.maxTextureSize != 1024 || imp.alphaSource != UnityEditor.TextureImporterAlphaSource.None;
  if (ch) { imp.mipmapEnabled = false; imp.npotScale = UnityEditor.TextureImporterNPOTScale.None; imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 1024; imp.alphaSource = UnityEditor.TextureImporterAlphaSource.None; imp.filterMode = FilterMode.Bilinear; imp.textureCompression = UnityEditor.TextureImporterCompression.Compressed; imp.SaveAndReimport(); }
}
System.Func<string, Texture2D[]> Load = pre => paths.Where(q => System.IO.Path.GetFileName(q).StartsWith(pre + "_")).Select(q => UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(q)).ToArray();
var intro = Load("action"); var frames = Load("loop");
var t = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } }).First(x => x.Name == "FateOrbView");
var lt = t.Assembly.GetType("TOD.FateLoop"); var et = t.Assembly.GetTypes().First(x => x.Name == "Element" && x.IsEnum);
Component c = null; foreach (var o in Resources.FindObjectsOfTypeAll(t)) { var cc = (Component)o; if (cc.gameObject.scene.IsValid()) c = cc; }
UnityEditor.Undo.RecordObject(c, "fate loop __Sin__");
var old = (System.Array)t.GetField("loops").GetValue(c);
var list = new System.Collections.ArrayList(); object L = null;
foreach (var l in old) { if (lt.GetField("sin").GetValue(l).ToString() == "__Sin__") L = l; list.Add(l); }
if (L == null) { L = System.Activator.CreateInstance(lt); lt.GetField("sin").SetValue(L, System.Enum.Parse(et, "__Sin__")); list.Add(L); }
lt.GetField("intro").SetValue(L, intro); lt.GetField("frames").SetValue(L, frames);
lt.GetField("fps").SetValue(L, 12f); lt.GetField("ticks").SetValue(L, 2); lt.GetField("ghost").SetValue(L, .45f);
var arr = System.Array.CreateInstance(lt, list.Count); list.CopyTo(arr); t.GetField("loops").SetValue(c, arr);
UnityEditor.EditorUtility.SetDirty(c); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
var sb = new System.Text.StringBuilder();
foreach (var l in arr) sb.AppendLine(lt.GetField("sin").GetValue(l) + " intro " + ((System.Array)lt.GetField("intro").GetValue(l)).Length + " frames " + ((System.Array)lt.GetField("frames").GetValue(l)).Length);
return sb.ToString();
