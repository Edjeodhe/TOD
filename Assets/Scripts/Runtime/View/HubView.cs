// 15차 재봉실 허브: 방 배경, 시설 그림, 걸어 다니는 주인공, 카메라.
// 오브젝트(Hub, Backdrop, Facilities/FacilityTemplate)는 MCP 로 만들었고, 시설 그림은 템플릿을 데이터 수만큼 복제한다.
// 주인공은 전투장의 Arena/Hero 를 한 번 복제해 같은 리그·동작으로 그린다(템플릿 복제). 적과 전투는 없다.
// 17차: 방이 둘이다. 재봉실 본방(HubConfig 의 옛 필드)과 다락 방(HubConfig.attic). 같은 배경 렌더러에 그림만 바꿔 깔고,
// 시설 그림은 두 방 것을 모두 복제해 두고 지금 방 것만 켠다.
using System.Collections.Generic;
using UnityEngine;

namespace TOD
{
    public class HubView : MonoBehaviour
    {
        public GameDatabase db;
        public HeroView heroTemplate;         // Arena/Hero (복제 원본)
        public CameraRig cameraRig;
        public SpriteRenderer backdrop;
        public SpriteRenderer facilityTemplate;
        public SpriteRenderer shadowTemplate;  // 20차: 시설 발밑 그림자 (soft_disc, 배경 바로 위 층. 꺼 둔 템플릿을 시설마다 복제)
        public SpriteRenderer castTemplate;    // 20차: 시설 드리운 그림자 (SpriteFX _Cast. 시설 그림을 뒤집어 납작하게, 꺼 둔 템플릿을 복제)
        public Transform facilityRoot;

        HeroView hero;
        public MerchantTalk merchant { get; set; }   // 16차: 상인 단추 자세와 트윈 (GameController 가 넣음)
        public MoiraRig moira;                // 19차: 다락의 모이라 뼈대 리그 (씬 Hub/Moira). 있으면 Moira 시설 그림 대신 그린다
        public FateOrbView fateOrb;           // 22차 미래 보기: 점성구 줌, 구슬 속 영상 (씬 Hub/Moira/FateOrb)
        [Header("23차 만난 NPC (NpcKind 순서, 씬 Hub/Npcs). 시설 자리는 hub.facilities 의 Npc* station")]
        public CutoutRig[] npcRigs;
        public System.Func<HubStation, bool> stationVisible;   // false 면 그 시설은 숨고 말도 못 건다 (아직 못 만난 NPC). GameController 가 넣음
        public NpcKind? npcTalking;           // 지금 말하는 NPC (말하기 자세)
        readonly Player player = new();
        readonly Dictionary<HubStation, SpriteRenderer> renderers = new();
        readonly Dictionary<HubStation, SpriteRenderer> shadows = new();
        readonly Dictionary<HubStation, SpriteRenderer> casts = new();
        // 24차 구석 가구: 시설(NPC, 상인)마다 HubFacility.props 를 facilityTemplate·shadowTemplate·castTemplate 복제로 그린다
        class PropView { public HubFacility f; public HubProp p; public SpriteRenderer sr, shadow, cast; }
        readonly List<PropView> props = new();
        MaterialPropertyBlock block;
        bool placed;
        HubRoom main;                          // 재봉실 본방 (HubConfig 옛 필드를 묶은 것)
        public bool InAttic { get; private set; }
        public HubRoom Room => InAttic && H.attic != null ? H.attic : main;
        static readonly int BrightId = Shader.PropertyToID("_Bright"), SatId = Shader.PropertyToID("_Sat");
        static readonly int CastId = Shader.PropertyToID("_Cast"), FillId = Shader.PropertyToID("_Fill");

        HubConfig H => db.hub;
        public Player Player => player;
        public HubFacility Near { get; private set; }

        public void Init()
        {
            block = new MaterialPropertyBlock();
            hero = Instantiate(heroTemplate.gameObject, transform).GetComponent<HeroView>();
            hero.name = "Hero";
            hero.Init(db);
            main = new HubRoom
            {
                title = H.title, subtitle = H.subtitle, backdrop = H.backdrop, backdropRect = H.backdropRect, walk = H.walk,
                heroStart = H.heroStart, cameraZoom = H.cameraZoom, facilities = H.facilities ?? new HubFacility[0], hint = H.hint,
            };
            facilityTemplate.gameObject.SetActive(false);
            if (shadowTemplate != null) shadowTemplate.gameObject.SetActive(false);
            if (castTemplate != null) castTemplate.gameObject.SetActive(false);
            foreach (HubRoom room in Rooms())
                foreach (HubFacility f in room.facilities ?? new HubFacility[0])
                {
                    SpriteRenderer sr = Instantiate(facilityTemplate, facilityRoot);
                    sr.name = f.station.ToString();
                    renderers[f.station] = sr;
                    Paint(f, sr);
                    if (shadowTemplate != null && f.shadowSize.x > 0 && f.station != HubStation.Moira)
                    {
                        SpriteRenderer sh = Instantiate(shadowTemplate, facilityRoot);
                        sh.name = f.station + "Shadow";
                        shadows[f.station] = sh;
                        PlaceShadow(f, sh, sr);
                    }
                    if (castTemplate != null && f.station is not (HubStation.Moira or HubStation.AtticExit))
                    {
                        SpriteRenderer cast = Instantiate(castTemplate, facilityRoot);
                        cast.name = f.station + "Cast";
                        casts[f.station] = cast;
                    }
                    for (int k = 0; f.props != null && k < f.props.Length; k++)
                    {
                        HubProp p = f.props[k];
                        if (p == null || p.sprite == null) continue;
                        var v = new PropView { f = f, p = p, sr = Instantiate(facilityTemplate, facilityRoot) };
                        v.sr.name = $"{f.station}Prop{k}";
                        PaintProp(v);
                        if (shadowTemplate != null && p.shadowSize.x > 0) { v.shadow = Instantiate(shadowTemplate, facilityRoot); v.shadow.name = v.sr.name + "Shadow"; }
                        if (castTemplate != null) { v.cast = Instantiate(castTemplate, facilityRoot); v.cast.name = v.sr.name + "Cast"; }
                        props.Add(v);
                    }
                }
            ShowRoom();
        }

        IEnumerable<HubRoom> Rooms()
        {
            yield return main;
            if (H.attic != null && H.attic.facilities != null && H.attic.facilities.Length > 0) yield return H.attic;
        }

        // 지금 방의 배경을 깔고 그 방 시설만 켠다.
        void ShowRoom()
        {
            HubRoom room = Room;
            if (backdrop != null && room.backdrop != null)
            {
                backdrop.sprite = room.backdrop;
                Rect r = room.backdropRect;
                Vector2 size = room.backdrop.bounds.size * Space.Ppu;
                backdrop.transform.localPosition = Space.Canvas(r.center);
                backdrop.transform.localScale = new Vector3(r.width / size.x, r.height / size.y, 1);
            }
            var here = new HashSet<HubStation>();
            foreach (HubFacility f in room.facilities ?? new HubFacility[0]) here.Add(f.station);
            foreach (var pair in renderers) pair.Value.gameObject.SetActive(here.Contains(pair.Key) && !(pair.Key == HubStation.Moira && moira != null) && !IsNpc(pair.Key));
            foreach (var pair in shadows) pair.Value.gameObject.SetActive(here.Contains(pair.Key) && Visible(pair.Key));
            foreach (var pair in casts) pair.Value.gameObject.SetActive(here.Contains(pair.Key));
            if (moira != null)
            {
                moira.gameObject.SetActive(here.Contains(HubStation.Moira));
                HubFacility f = Facility(HubStation.Moira);
                if (f != null)   // 시설 자리(바닥 좌표)에 발밑, 시설 높이에 맞춘 배율
                {
                    moira.transform.localPosition = Space.Ground(f.position.x, f.position.y);
                    float s = moira.height > 0 ? f.height / moira.height : 1;
                    moira.transform.localScale = new Vector3(s * (f.flip ? -1 : 1), s, 1);
                }
            }
            PlaceNpcs();
            Near = null;
        }

        // ---------------------------------------------------------------- 23차 만난 NPC
        public static bool IsNpc(HubStation s) => s is HubStation.NpcBlessing or HubStation.NpcGift or HubStation.NpcMemory or HubStation.NpcGamble;
        public static NpcKind NpcOf(HubStation s) => (NpcKind)((int)s - (int)HubStation.NpcBlessing);
        bool Visible(HubStation s) => stationVisible == null || stationVisible(s);

        // 지금 방에 있고 만난 NPC 만 켜서 시설 자리에 발밑, 데이터 높이에 맞춘 배율
        void PlaceNpcs()
        {
            if (npcRigs == null) return;
            for (int i = 0; i < npcRigs.Length; i++)
            {
                CutoutRig rig = npcRigs[i];
                if (rig == null) continue;
                HubStation station = (HubStation)((int)HubStation.NpcBlessing + i);
                HubFacility f = System.Array.Find(Room.facilities ?? new HubFacility[0], x => x.station == station);
                bool on = f != null && Visible(station);
                if (rig.gameObject.activeSelf != on) rig.gameObject.SetActive(on);
                if (shadows.TryGetValue(station, out SpriteRenderer sh) && sh.gameObject.activeSelf != on) sh.gameObject.SetActive(on);
                if (!on) continue;
                rig.transform.localPosition = Space.Ground(f.position.x, f.position.y);
                float s = rig.height > 0 ? f.height / rig.height : 1;
                rig.transform.localScale = new Vector3(s * (f.flip ? -1 : 1), s, 1);
                // 24차: 시설·구석 가구와 같은 규칙으로 발밑 높이에 따라 주인공 앞(1)/뒤(-1)
                if (rig.group != null && hero != null) rig.group.sortingOrder = rig.transform.position.y < hero.transform.position.y ? 1 : -1;
                FacilityLook look = H.look;
                if (look != null && look.brightness > 0) { rig.tint = look.tint; rig.saturation = look.saturation; rig.brightness = look.brightness * (f.brightness > 0 ? f.brightness : 1); }
            }
        }

        // 매 프레임 (대화 상자가 열려도 움직인다)
        public void AnimateNpcs(float dt)
        {
            if (npcRigs == null) return;
            PlaceNpcs();
            for (int i = 0; i < npcRigs.Length; i++)
            {
                CutoutRig rig = npcRigs[i];
                if (rig == null || !rig.gameObject.activeInHierarchy) continue;
                HubStation station = (HubStation)((int)HubStation.NpcBlessing + i);
                rig.Talking = npcTalking == (NpcKind)i;
                rig.Animate(dt, Near != null && Near.station == station ? H.highlight : 1);
            }
        }

        // 말풍선 자리: 시설 그림 위쪽 끝에서 bubbleLift 위 (캔버스 px)
        public Vector2? NpcHead(NpcKind kind, float bubbleLift)
        {
            HubFacility f = Facility((HubStation)((int)HubStation.NpcBlessing + (int)kind));
            if (f == null) return null;
            return new Vector2(f.position.x, Space.ProjectY(f.position.y) - f.height - bubbleLift);
        }

        // 17차: 다락 방으로 올라가기(true) / 재봉실로 내려오기(false). 내려오면 다락 사다리 앞(atticReturn)에 선다.
        public void SetRoom(bool attic)
        {
            if (attic && (H.attic == null || H.attic.facilities == null || H.attic.facilities.Length == 0)) return;
            InAttic = attic;
            ShowRoom();
            Vector2 at = attic ? H.attic.heroStart : H.atticReturn;
            player.x = at.x; player.y = at.y;
            player.motion = 0;
            Combat.Act(player, HeroAction.Idle);
        }

        // 시설 그림을 데이터 자리·크기로 다시 놓는다 (merchant 세션이 자세 그림을 바꿀 때도 부른다).
        public void Paint(HubFacility f, SpriteRenderer sr)
        {
            sr.sprite = f.sprite;
            float scale = f.sprite != null ? f.height / (f.sprite.bounds.size.y * Space.Ppu) : 1;
            sr.transform.localScale = new Vector3(scale * (f.flip ? -1 : 1), scale, 1);
            // 20차: 그림 아래 끝이 데이터 자리에 닿게 (그림은 가운데 피벗으로 가져와서, 피벗 높이만큼 올린다. 전투의 상인 그리기와 같은 방식)
            sr.transform.localPosition = Space.Ground(f.position.x, f.position.y) + Vector3.up * PivotLift(f.sprite, scale);
        }

        // 피벗(그림 px)이 아래 끝에서 얼마나 위인지 (월드 단위, 배율 적용)
        static float PivotLift(Sprite sprite, float scale) => sprite != null ? sprite.pivot.y / sprite.pixelsPerUnit * Mathf.Abs(scale) : 0;

        // 그림의 실제 아래 끝 가운데 (facilityRoot 좌표). 시설 그림은 가운데 피벗으로 가져와서 데이터 자리 = 그림 가운데다.
        static Vector3 Foot(SpriteRenderer sr)
        {
            Vector3 p = sr.transform.localPosition;
            if (sr.sprite == null) return p;
            return new Vector3(p.x, p.y - sr.sprite.pivot.y / sr.sprite.pixelsPerUnit * Mathf.Abs(sr.transform.localScale.y), p.z);
        }

        // 20차: 발밑 그림자 = 부드러운 타원 (그림 아래 끝 가운데 + shadowOffset, 지름 shadowSize 캔버스 px)
        void PlaceShadow(HubFacility f, SpriteRenderer sh, SpriteRenderer sr) =>
            PlaceShadow(sh, sr != null ? Foot(sr) : Space.Ground(f.position.x, f.position.y), f.shadowOffset, f.shadowSize);

        void PlaceShadow(SpriteRenderer sh, Vector3 foot, Vector2 offset, Vector2 size)
        {
            sh.transform.localPosition = foot + new Vector3(offset.x, -offset.y, 0) / Space.Ppu;
            Vector2 px = sh.sprite != null ? (Vector2)sh.sprite.bounds.size * Space.Ppu : Vector2.one;
            sh.transform.localScale = new Vector3(size.x / px.x, size.y / px.y, 1);
            FacilityLook look = H.look;
            if (look != null) sh.color = look.shadowColor;
        }

        // 24차 구석 가구: 시설 자리 + offset 에 그림 아래 가운데, 높이 height (시설 Paint 와 같은 방식)
        void PaintProp(PropView v)
        {
            HubProp p = v.p;
            v.sr.sprite = p.sprite;
            float scale = p.height / (p.sprite.bounds.size.y * Space.Ppu);
            v.sr.transform.localScale = new Vector3(scale * (p.flip ? -1 : 1), scale, 1);
            Vector2 at = v.f.position + p.offset;
            v.sr.transform.localPosition = Space.Ground(at.x, at.y) + Vector3.up * PivotLift(p.sprite, scale);
        }

        // 20차 드리운 그림자: 시설 그림(상인은 지금 자세)을 발밑에서 뒤집어 castLength 만큼 납작하게 앞으로 눕힌다.
        // 방 가운데에서 바깥쪽으로 castLean 만큼 기운다. 끝으로 갈수록 옅고 흐릿한 건 SpriteFX _Cast 가 맡는다.
        void UpdateCasts()
        {
            FacilityLook look = H.look;
            if (look == null || look.castLength <= 0) { foreach (var pair in casts) pair.Value.enabled = false; return; }
            foreach (HubFacility f in Room.facilities)
                if (casts.TryGetValue(f.station, out SpriteRenderer cast) && renderers.TryGetValue(f.station, out SpriteRenderer sr)) Cast(cast, sr, f.position.x, look);
            foreach (PropView v in props)   // 24차 구석 가구
                if (v.cast != null && v.cast.gameObject.activeSelf) Cast(v.cast, v.sr, v.f.position.x + v.p.offset.x, look);
        }

        void Cast(SpriteRenderer cast, SpriteRenderer sr, float x, FacilityLook look)
        {
            cast.enabled = sr.sprite != null;
            if (sr.sprite == null) return;
            cast.sprite = sr.sprite;
            cast.flipX = sr.flipX;
            Vector3 s = sr.transform.localScale;
            cast.transform.localScale = new Vector3(s.x, -Mathf.Abs(s.y) * look.castLength, 1);
            float side = Mathf.Sign(x - Room.backdropRect.center.x);
            float angle = Mathf.Atan(look.castLean) * side;
            cast.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            // 뒤집힌 그림의 가까운 끝(그림 아래 끝)이 시설 발밑에 오도록 가운데를 옮긴다 (기운 만큼 돌려서)
            Vector3 foot = Foot(sr);
            float near = sr.sprite.pivot.y / sr.sprite.pixelsPerUnit * Mathf.Abs(s.y) * look.castLength;
            cast.transform.localPosition = foot + new Vector3(Mathf.Sin(angle) * near, -Mathf.Cos(angle) * near, 0);
            cast.color = new Color(1, 1, 1, look.castColor.a);
            block.Clear();
            block.SetVector(CastId, new Vector4(1, look.castFade, look.castBlur, 0));
            block.SetColor(FillId, look.castColor);
            cast.SetPropertyBlock(block);
        }

        public SpriteRenderer FacilityRenderer(HubStation station) => renderers.TryGetValue(station, out SpriteRenderer sr) ? sr : null;

        public HubFacility Facility(HubStation station)
        {
            foreach (HubRoom room in Rooms())
            {
                HubFacility f = System.Array.Find(room.facilities ?? new HubFacility[0], x => x.station == station);
                if (f != null) return f;
            }
            return null;
        }

        // 허브에 들어올 때: 처음이면 시작 자리, 등반에서 돌아왔으면 탑 입구 앞(귀환 자리)
        public void Enter(bool fromRun)
        {
            if (!placed || fromRun)
            {
                if (InAttic) { InAttic = false; ShowRoom(); }
                Vector2 at = placed ? H.returnSpot : H.heroStart;
                player.x = at.x; player.y = at.y;
                placed = true;
            }
            player.motion = 0;
            player.facing = 1;
            Combat.Act(player, HeroAction.Idle);
        }

        // 한 프레임: move = 입력 방향(-1~1), canMove = 대화 상자 등이 없을 때
        // dash = 이번 프레임에 대시를 눌렀는지. 돌려주는 값 = 대시를 시작했는지 (소리는 GameController 가 낸다)
        public bool Tick(float dt, Vector2 move, bool canMove, float clock, bool dash = false)
        {
            HeroConfig hc = db.hero;
            if (!canMove) { move = Vector2.zero; dash = false; }
            bool moving = move.sqrMagnitude > 0;
            if (moving) move.Normalize();
            player.dashCooldown = Mathf.Max(0, player.dashCooldown - dt);
            bool dashed = dash && player.dash <= 0 && player.dashCooldown <= 0 && StartDash(moving, move);
            if (player.dash > 0) UpdateDash(dt);
            else
            {
                player.motion += ((moving ? 1 : 0) - player.motion) * Mathf.Min(1, dt * hc.motionLerp);
                player.x += move.x * H.walkSpeed * dt;
                player.y += move.y * H.walkSpeed * H.verticalFactor * dt;
            }
            Block();
            Rect walk = Room.walk;
            player.x = Mathf.Clamp(player.x, walk.xMin, walk.xMax);
            player.y = Mathf.Clamp(player.y, walk.yMin, walk.yMax);
            if (moving) player.strideTime += dt * (hc.strideBase + hc.strideFactor);
            player.actionTime += dt;
            if (player.dash <= 0 && (player.action != HeroAction.Dash || player.actionTime >= hc.dashHold))
            {
                HeroAction state = moving ? HeroAction.Run : HeroAction.Idle;
                if (player.action != state) Combat.Act(player, state);
                if (move.x != 0) player.facing = Mathf.Sign(move.x);
            }

            HeroFrame frame = hero.Pose(player, player.actionTime);
            hero.Apply(player, frame);
            hero.Render(player, 1, db.palette.shadow);

            Near = Nearest();
            // 24차: 시설 그림은 가운데 피벗이라 Y 정렬(렌더러 2D 사용자 축)이 그림 가운데 높이로 돼서, 상인 발보다 조금 뒤에 서도 주인공이 앞에 그려졌다.
            // → 발밑 높이로 주인공보다 앞(아래)이면 위, 뒤면 아래에 그린다 (주인공 리그는 발밑 피벗, 같은 Actors 층 order 0).
            float heroFoot = hero.transform.position.y;
            foreach (HubFacility f in Room.facilities)
            {
                if (!renderers.TryGetValue(f.station, out SpriteRenderer sr)) continue;
                sr.sortingOrder = facilityRoot.TransformPoint(Foot(sr)).y < heroFoot ? 1 : -1;
                // 20차: 방 빛에 맞춰 색을 곱하고 채도, 밝기를 낮춘다 (가까이 가면 highlight 만큼 밝게)
                FacilityLook look = H.look;
                bool styled = look != null && look.brightness > 0;
                sr.color = styled ? look.tint : Color.white;
                block.Clear();
                block.SetFloat(SatId, styled ? look.saturation : 1);
                block.SetFloat(BrightId, (f == Near ? H.highlight : 1) * (styled ? look.brightness : 1) * (f.brightness > 0 ? f.brightness : 1));
                sr.SetPropertyBlock(block);
                if (shadows.TryGetValue(f.station, out SpriteRenderer sh)) PlaceShadow(f, sh, sr);
            }
            TickProps(heroFoot);   // 24차 구석 가구
            AnimateMerchant(clock);
            UpdateCasts();
            float zoom = Room.cameraZoom > 0 ? Room.cameraZoom : 1;
            cameraRig.Apply(Space.ToCanvas(player.x, player.y), Room.backdropRect, zoom, 0, null, clock);
            return dashed;
        }

        // 재봉실 대시: 전투 대시(Combat.StartDash/UpdateDash)와 같은 거리, 시간, 자세. 대시 칸 제한과 잔상, 먼지 이펙트는 없고 연달아 쓸 때 최소 간격(dashMinCooldown)만 둔다.
        bool StartDash(bool moving, Vector2 move)
        {
            FeelConfig f = db.feel;
            if (f == null || f.dashDuration <= 0) return false;
            Vector2 dir = moving ? move : new Vector2(player.facing, 0);   // 23차: 서 있으면 보는 쪽으로 나감 (예전엔 보는 쪽의 반대로 물러남)
            player.dx = dir.x; player.dy = dir.y;
            player.dash = f.dashDuration;
            player.dashDone = 0;
            player.dashCooldown = f.dashMinCooldown;
            if (moving && player.dx != 0) player.facing = Mathf.Sign(player.dx);   // 전투와 같이: 움직이면 가는 쪽을 보고, 서 있으면 보는 쪽 그대로 그쪽으로 나감
            player.dashFacing = player.facing;
            Combat.Act(player, HeroAction.Dash);
            return true;
        }

        void UpdateDash(float dt)
        {
            FeelConfig f = db.feel;
            float step = Mathf.Min(dt, player.dash);
            float u = Mathf.Clamp01(1 - (player.dash - step) / f.dashDuration);
            float travel = f.dashDistance * (1 - Mathf.Pow(1 - u, f.dashEase));
            float moved = travel - player.dashDone;
            player.dashDone = travel;
            player.x += player.dx * moved;
            player.y += player.dy * moved * H.verticalFactor;
            player.dash = Mathf.Max(0, player.dash - dt);
            player.motion = 1;
        }

        // 19차 모이라: 방이 보이는 동안 매 프레임 (모이라 카드 창이 열려 걷기가 멈춰도 움직인다)
        public void AnimateMoira(float dt)
        {
            if (moira == null || !moira.gameObject.activeInHierarchy) return;
            moira.Animate(dt, Near != null && Near.station == HubStation.Moira ? H.highlight : 1);
        }

        // 시설 바닥 타원 안으로 들어가면 타원 가장자리로 밀어낸다.
        void Block()
        {
            foreach (HubFacility f in Room.facilities)
            {
                if (!Visible(f.station)) continue;
                Push(f.position + f.blockOffset, f.block);
                for (int k = 0; f.props != null && k < f.props.Length; k++)   // 24차 구석 가구
                    if (f.props[k] != null) Push(f.position + f.props[k].offset + f.props[k].blockOffset, f.props[k].block);
            }
        }

        void Push(Vector2 c, Vector2 r)
        {
            if (r.x <= 0 || r.y <= 0) return;
            float nx = (player.x - c.x) / r.x, ny = (player.y - c.y) / r.y;
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            if (d >= 1) return;
            if (d < 1e-4f) { nx = 0; ny = 1; d = 1; }
            player.x = c.x + nx / d * r.x;
            player.y = c.y + ny / d * r.y;
        }

        // 24차 구석 가구: 지금 방에 있고 시설이 보일 때(만난 NPC)만 켜고, 방 빛을 곱하고, 발밑 높이로 주인공 앞/뒤를 정한다
        void TickProps(float heroFoot)
        {
            var here = new HashSet<HubFacility>(Room.facilities ?? new HubFacility[0]);
            FacilityLook look = H.look;
            bool styled = look != null && look.brightness > 0;
            foreach (PropView v in props)
            {
                bool on = here.Contains(v.f) && Visible(v.f.station);
                if (v.sr.gameObject.activeSelf != on) v.sr.gameObject.SetActive(on);
                if (v.shadow != null && v.shadow.gameObject.activeSelf != on) v.shadow.gameObject.SetActive(on);
                if (v.cast != null && v.cast.gameObject.activeSelf != on) v.cast.gameObject.SetActive(on);
                if (!on) continue;
                v.sr.sortingOrder = facilityRoot.TransformPoint(Foot(v.sr)).y < heroFoot ? 1 : -1;
                v.sr.color = styled ? look.tint : Color.white;
                block.Clear();
                block.SetFloat(SatId, styled ? look.saturation : 1);
                block.SetFloat(BrightId, (Near == v.f ? H.highlight : 1) * (styled ? look.brightness : 1));
                v.sr.SetPropertyBlock(block);
                if (v.shadow != null) PlaceShadow(v.shadow, Foot(v.sr), v.p.shadowOffset, v.p.shadowSize);
            }
        }

        HubFacility Nearest()
        {
            HubFacility best = null;
            float bestD = float.MaxValue;
            foreach (HubFacility f in Room.facilities)
            {
                if (!Visible(f.station)) continue;   // 23차: 아직 못 만난 NPC
                float radius = f.radius > 0 ? f.radius : H.interactRadius;
                float d = Vector2.Distance(new Vector2(player.x, player.y), f.position);
                if (d <= radius && d < bestD) { best = f; bestD = d; }
            }
            return best;
        }

        // 16차: 상인 단추 = 지금 자세 그림 + 숨쉬기, 튐, 기울기 (피벗이 발밑이라 발을 축으로)
        void AnimateMerchant(float clock)
        {
            HubFacility f = Facility(HubStation.Merchant);
            SpriteRenderer sr = FacilityRenderer(HubStation.Merchant);
            if (merchant == null || f == null || sr == null || InAttic) return;
            MerchantFrame frame = merchant.Pose(clock);
            Sprite sprite = merchant.Sprite(frame.mood) ?? f.sprite;
            if (sprite == null) return;
            sr.sprite = sprite;
            float scale = f.height / (sprite.bounds.size.y * Space.Ppu);
            sr.transform.localScale = new Vector3(scale * frame.scaleX * (f.flip ? -1 : 1), scale * frame.scaleY, 1);
            sr.transform.localRotation = Quaternion.Euler(0, 0, -frame.angle);
            sr.transform.localPosition = Space.Ground(f.position.x, f.position.y, frame.hop) + Vector3.up * PivotLift(sprite, scale * frame.scaleY);   // 20차: 발밑 = 데이터 자리
        }

        // 상인 말풍선 자리: 머리 위 bubbleLift (캔버스 px)
        public Vector2? MerchantHead()
        {
            HubFacility f = Facility(HubStation.Merchant);
            if (f == null) return null;
            return new Vector2(f.position.x, Space.ProjectY(f.position.y) - f.height - db.merchant.bubbleLift);
        }

        // 꼬리표 자리: 시설 그림 위쪽 끝에서 promptLift 위 (캔버스 px)
        public Vector2 PromptAnchor(HubFacility f) => new(f.position.x, Space.ProjectY(f.position.y) - f.height - f.promptLift);
    }
}
