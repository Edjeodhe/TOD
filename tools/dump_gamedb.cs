// GameDatabase 덤프 (tools/export_data_tables.py 의 입력). Unity 에서 MCP execute_code 로 이 본문을 그대로 실행한다 (읽기만 함).
// 결과: <프로젝트>/Temp/gamedb_dump.json (JsonUtility, enum 은 정수, Sprite 등 참조는 instanceID)
// 이 파일은 Assets 밖에 있어서 Unity 가 컴파일하지 않는다.
var db = UnityEngine.Object.FindFirstObjectByType<TOD.GameDatabase>();
if (db == null) return "no db";
string json = JsonUtility.ToJson(db, true);
string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Temp/gamedb_dump.json");
System.IO.File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
return path + " " + json.Length + " scene=" + db.gameObject.scene.path;
