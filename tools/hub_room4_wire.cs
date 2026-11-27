// 24차 넓은 재봉실 (hub_room4, 캔버스 2000 x 667): MCP execute_code 본문 (CodeDom C# 6). 플레이 모드가 아닐 때 보내고 씬을 저장한다.
// 옛 방(1000 폭)의 시설은 +500 옮기고, 수녀(축복)는 왼쪽 끝 기도 감실, 촛불 유령(기억)은 오른쪽 끝 촛불 제단으로.
var db = UnityEngine.Object.FindFirstObjectByType<TOD.GameDatabase>();
var h = db.hub;
UnityEditor.Undo.RecordObject(db, "hub room4");
var sprite = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/sprites/hub_room4.png").OfType<Sprite>().FirstOrDefault();
if (sprite == null) return "hub_room4 sprite missing";
h.backdrop = sprite;
h.backdropRect = new Rect(0, 0, 2000, 667);
h.walk = new Rect(70, 205, 1860, 645);
h.heroStart = new Vector2(1000, 560);
h.returnSpot = new Vector2(1000, 290);
h.atticReturn = new Vector2(725, 275);
h.cameraZoom = 1.06f;   // 예전과 같은 크기 (.95 는 벽 위쪽이 잘림)
var sb = new System.Text.StringBuilder();
foreach (var f in h.facilities)
{
    switch (f.station.ToString())
    {
        case "Tower": f.position = new Vector2(1000, 197); break;
        case "Attic": f.position = new Vector2(725, 207); break;
        case "Diary": f.position = new Vector2(1275, 207); break;
        case "Merchant": f.position = new Vector2(1315, 590); break;
        case "NpcGift": f.position = new Vector2(710, 625); break;
        case "NpcGamble": f.position = new Vector2(1760, 790); break;   // 상인과 붙어 있어 오른쪽 끝 바닥으로 (장난감 상자 1865)
        case "NpcBlessing":   // 왼쪽 끝 기도 감실: 기도대는 감실 안(발밑 200, 200), 수녀는 그 앞 오른쪽 러너 위
            f.position = new Vector2(300, 345);
            if (f.props != null && f.props.Length > 0) f.props[0].offset = new Vector2(-100, -145);
            break;
        case "NpcMemory":     // 오른쪽 끝 촛불 제단: 장식장은 벽 쪽(1775, 200), 유령은 그 앞 둥근 깔개 위
            f.position = new Vector2(1740, 355);
            if (f.props != null && f.props.Length > 0) f.props[0].offset = new Vector2(35, -155);
            break;
    }
    sb.AppendLine(f.station + " " + f.position + (f.props != null && f.props.Length > 0 ? " prop " + (f.position + f.props[0].offset) : ""));
}
UnityEditor.EditorUtility.SetDirty(db);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(db.gameObject.scene);
return sb.ToString();
