using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomDeck.Presentation
{
    /// <summary>
    /// 가운데 3D 전장: 두 성, 소환물, 병기 공격·성 피격 연출.
    /// 0 = 플레이어(아래쪽), 1 = AI(위쪽).
    /// </summary>
    public class Battlefield : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        CardVisualLibrary library;

        readonly Transform[] castles = new Transform[2];
        readonly Vector3[] castleHome = new Vector3[2];
        readonly float[] smokeRate = new float[2];
        readonly bool[] fallen = new bool[2];
        readonly List<GameObject> temps = new List<GameObject>();
        Transform root;

        public static readonly Vector3 PlayerCastlePos = new Vector3(0, 0, -4.7f);
        public static readonly Vector3 EnemyCastlePos = new Vector3(0, 0, 5.0f);

        public void Init(Camera cam, CardVisualLibrary lib)
        {
            Cam = cam;
            library = lib;
            root = new GameObject("BattlefieldWorld").transform;
            root.SetParent(transform, false);

            SetupCamera();
            SetupLight();
            BuildGround();

            castles[0] = SummonFactory.Castle("PlayerCastle", Palette.Player).transform;
            castles[0].SetParent(root, true);
            castles[0].position = PlayerCastlePos;
            castles[0].rotation = Quaternion.Euler(0, 180, 0);
            castles[1] = SummonFactory.Castle("EnemyCastle", Palette.Enemy).transform;
            castles[1].SetParent(root, true);
            castles[1].position = EnemyCastlePos;
            for (int i = 0; i < 2; i++) castleHome[i] = castles[i].position;

            StartCoroutine(SmokeLoop(0));
            StartCoroutine(SmokeLoop(1));
        }

        void SetupCamera()
        {
            Cam.transform.position = new Vector3(0, 18f, -15.2f);
            Cam.transform.rotation = Quaternion.Euler(52f, 0, 0);
            Cam.fieldOfView = 31f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Palette.Hex("#1b1712");
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 200f;
        }

        void SetupLight()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun.intensity = 1.25f;
            sun.color = Palette.Hex("#fff1d8");
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.Hex("#9fb4d0");
            RenderSettings.ambientEquatorColor = Palette.Hex("#8a8a70");
            RenderSettings.ambientGroundColor = Palette.Hex("#3a3a2a");
        }

        void BuildGround()
        {
            var ground = SummonFactory.Prim(PrimitiveType.Plane, root, new Vector3(0, 0, 0), new Vector3(8, 1, 6), SummonFactory.Mat("#4f7a3a", 0.05f), null, "Ground");
            SummonFactory.Prim(PrimitiveType.Cube, root, new Vector3(0, 0.005f, 0.2f), new Vector3(1.8f, 0.01f, 10f), SummonFactory.Mat("#8f7650", 0.05f), null, "Road");
            SummonFactory.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.006f, 0.2f), new Vector3(4.2f, 0.005f, 3.2f), SummonFactory.Mat("#7d6a48", 0.05f), null, "FieldCenter");
            // 들판 무늬
            var rnd = new System.Random(3);
            for (int i = 0; i < 26; i++)
            {
                float x = (float)(rnd.NextDouble() - 0.5) * 30f, z = (float)(rnd.NextDouble() - 0.5) * 24f;
                if (Mathf.Abs(x) < 3f) continue;
                SummonFactory.Prim(PrimitiveType.Cylinder, root, new Vector3(x, 0.004f, z), new Vector3(2f + (float)rnd.NextDouble() * 3f, 0.003f, 1.5f + (float)rnd.NextDouble() * 2f),
                    SummonFactory.Mat(i % 2 == 0 ? "#577f3e" : "#48703a", 0.05f), null, "Patch");
            }
            // 나무와 바위
            for (int i = 0; i < 46; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side * (5.2f + (float)rnd.NextDouble() * 9f);
                float z = -9f + (float)rnd.NextDouble() * 22f;
                SummonFactory.Tree(new Vector3(x, 0, z), 0.8f + (float)rnd.NextDouble() * 0.7f, i).transform.SetParent(root, true);
            }
            for (int i = 0; i < 12; i++)
            {
                float x = (rnd.NextDouble() < 0.5 ? -1 : 1) * (2.2f + (float)rnd.NextDouble() * 3f);
                float z = -3f + (float)rnd.NextDouble() * 7f;
                SummonFactory.Rock(new Vector3(x, 0.05f, z), 0.25f + (float)rnd.NextDouble() * 0.35f).transform.SetParent(root, true);
            }
            ground.isStatic = true;
        }

        // =====================================================================
        // 위치
        // =====================================================================

        public Vector3 CastleTop(int p) => castleHome[p] + Vector3.up * 2.4f;
        public Vector3 CastleCenter(int p) => castleHome[p] + Vector3.up * 1.2f;
        /// <summary>성 앞 (상대 쪽 방향) 소환 위치</summary>
        public Vector3 Front(int p) => p == 0 ? new Vector3(0, 0, -2.0f) : new Vector3(0, 0, 2.3f);
        Vector3 Toward(int p) => p == 0 ? Vector3.forward : Vector3.back;

        public Vector2 ScreenPoint(Vector3 world)
        {
            var s = Cam.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        // =====================================================================
        // 소환물
        // =====================================================================

        GameObject Spawn(string cardId)
        {
            var vis = library != null ? library.Get(cardId) : null;
            GameObject go = vis != null && vis.summonPrefab != null ? Instantiate(vis.summonPrefab) : SummonFactory.Build(cardId);
            go.transform.SetParent(root, true);
            if (vis != null) go.transform.localScale = Vector3.one * vis.summonScale;
            temps.Add(go);
            return go;
        }

        void Kill(GameObject go)
        {
            if (go == null) return;
            temps.Remove(go);
            Destroy(go);
        }

        /// <summary>건너뛰기: 남아 있는 연출 오브젝트를 모두 지운다</summary>
        public void ClearTemps()
        {
            foreach (var t in temps) if (t != null) Destroy(t);
            temps.Clear();
            for (int i = 0; i < 2; i++) if (castles[i] != null && !fallen[i]) castles[i].position = castleHome[i];
        }

        /// <summary>행동 카드 소환: 성 앞에 솟아올랐다 사라진다</summary>
        public IEnumerator SummonAppear(string cardId, int player, float duration)
        {
            var go = Spawn(cardId);
            var t = go.transform;
            Vector3 pos = Front(player) + new Vector3(player == 0 ? -1.6f : 1.6f, 0, 0);
            t.position = pos;
            t.rotation = Quaternion.Euler(0, player == 0 ? 200 : 20, 0);
            Vector3 full = t.localScale;
            Burst(pos + Vector3.up * 0.1f, Palette.Hex("#d8c8a0"), 14, 2.2f, 0.12f, 0.5f, 3f);
            yield return Anim.Run(0.35f * duration, k => { t.localScale = full * Anim.OutBack(k); t.position = pos + Vector3.up * (1 - k) * -0.6f; });
            yield return Anim.Wait(0.4f * duration);
            yield return Anim.Run(0.25f * duration, k => t.localScale = full * (1 - Anim.InCubic(k)));
            Kill(go);
        }

        /// <summary>재화 사용: 동전이 튀어오름</summary>
        public IEnumerator CoinPop(string cardId, int player, int amount)
        {
            Vector3 pos = Front(player) + new Vector3(player == 0 ? 1.8f : -1.8f, 0, 0);
            var go = Spawn(cardId);
            var t = go.transform;
            t.position = pos;
            Vector3 full = t.localScale * 0.8f;
            Color c = cardId == "gold" ? Palette.Hex("#ffd84a") : cardId == "silver" ? Palette.Hex("#e8eef4") : Palette.Hex("#d88a50");
            Burst(pos + Vector3.up * 0.4f, c, 6 + amount * 5, 3.5f, 0.1f, 0.6f, 9f, true);
            yield return Anim.Run(0.25f, k => { t.localScale = full * Anim.OutBack(k); t.position = pos + Vector3.up * k * 0.5f; t.Rotate(0, 400 * Time.deltaTime, 0); });
            yield return Anim.Run(0.25f, k => { t.localScale = full * (1 - k); t.Rotate(0, 400 * Time.deltaTime, 0); });
            Kill(go);
        }

        /// <summary>병기 공격 (구매/획득 시)</summary>
        public IEnumerator SiegeAttack(string cardId, int attacker, System.Action onHit)
        {
            var vis = library != null ? library.Get(cardId) : null;
            var style = vis != null ? vis.style : SummonStyle.Appear;
            float dur = vis != null ? vis.duration : 1.2f;
            switch (style)
            {
                case SummonStyle.ArrowVolley: yield return ArrowVolley(cardId, attacker, dur, onHit); break;
                case SummonStyle.Catapult: yield return CatapultShot(cardId, attacker, dur, onHit); break;
                case SummonStyle.Dragon: yield return DragonAttack(cardId, attacker, dur, onHit); break;
                default:
                    yield return FlyToEnemy(cardId, attacker, dur, Palette.Hex("#ff8040"));
                    onHit?.Invoke();
                    break;
            }
        }

        static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float t) => Vector3.Lerp(Vector3.Lerp(a, c, t), Vector3.Lerp(c, b, t), t);

        IEnumerator ArrowVolley(string cardId, int attacker, float dur, System.Action onHit)
        {
            int target = 1 - attacker;
            var archers = Spawn(cardId);
            var at = archers.transform;
            Vector3 pos = Front(attacker);
            at.position = pos;
            at.rotation = Quaternion.LookRotation(Toward(attacker));
            Vector3 full = at.localScale;
            yield return Anim.Run(0.25f * dur, k => at.localScale = full * Anim.OutBack(k));
            Sfx.Play(Sfx.Arrows, 0.9f);

            const int n = 12;
            var arrows = new List<Transform>();
            var starts = new List<Vector3>();
            var ends = new List<Vector3>();
            var shaft = SummonFactory.Mat("#6a4a2a");
            var tip = SummonFactory.Mat("#d0d4d8", 0.8f, 0.8f);
            for (int i = 0; i < n; i++)
            {
                var a = new GameObject("Arrow").transform;
                a.SetParent(root, false);
                SummonFactory.Prim(PrimitiveType.Cube, a, Vector3.zero, new Vector3(0.04f, 0.04f, 0.55f), shaft);
                SummonFactory.Prim(PrimitiveType.Cube, a, new Vector3(0, 0, 0.3f), new Vector3(0.08f, 0.08f, 0.1f), tip);
                SummonFactory.Prim(PrimitiveType.Cube, a, new Vector3(0, 0, -0.25f), new Vector3(0.12f, 0.01f, 0.1f), SummonFactory.Mat("#f0f0f0"));
                temps.Add(a.gameObject);
                arrows.Add(a);
                starts.Add(pos + new Vector3(Random.Range(-0.7f, 0.7f), 0.9f, 0));
                ends.Add(CastleCenter(target) + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0f, 1.2f), Random.Range(-0.8f, 0.8f)));
                a.gameObject.SetActive(false);
            }
            float flight = 0.75f * dur;
            float stagger = 0.18f * dur;
            bool hit = false;
            yield return Anim.Run(flight + stagger, k =>
            {
                float time = k * (flight + stagger);
                for (int i = 0; i < n; i++)
                {
                    if (arrows[i] == null) continue;
                    float local = (time - stagger * i / n) / flight;
                    if (local < 0) continue;
                    if (local >= 1f)
                    {
                        if (arrows[i].gameObject.activeSelf)
                        {
                            Burst(ends[i], Palette.Hex("#e0c080"), 4, 1.5f, 0.07f, 0.3f, 6f);
                            arrows[i].gameObject.SetActive(false);
                        }
                        continue;
                    }
                    arrows[i].gameObject.SetActive(true);
                    Vector3 ctrl = (starts[i] + ends[i]) / 2 + Vector3.up * 4.5f;
                    Vector3 p = Bezier(starts[i], ctrl, ends[i], local);
                    Vector3 next = Bezier(starts[i], ctrl, ends[i], Mathf.Min(1f, local + 0.02f));
                    arrows[i].position = p;
                    if ((next - p).sqrMagnitude > 1e-6f) arrows[i].rotation = Quaternion.LookRotation(next - p);
                }
                if (!hit && time >= flight) { hit = true; onHit?.Invoke(); }
            });
            if (!hit) onHit?.Invoke();
            foreach (var a in arrows) if (a != null) Kill(a.gameObject);
            yield return Anim.Run(0.2f * dur, k => at.localScale = full * (1 - k));
            Kill(archers);
        }

        IEnumerator CatapultShot(string cardId, int attacker, float dur, System.Action onHit)
        {
            int target = 1 - attacker;
            var cat = Spawn(cardId);
            var ct = cat.transform;
            Vector3 pos = Front(attacker);
            ct.position = pos;
            ct.rotation = Quaternion.LookRotation(Toward(attacker));
            Vector3 full = ct.localScale;
            Burst(pos, Palette.Hex("#c8b890"), 12, 2f, 0.12f, 0.5f, 3f);
            yield return Anim.Run(0.25f * dur, k => ct.localScale = full * Anim.OutBack(k));

            var arm = ct.Find("Arm");
            Quaternion armStart = arm != null ? arm.localRotation : Quaternion.identity;
            Transform rockInArm = arm != null && arm.childCount > 2 ? arm.GetChild(2) : null;
            yield return Anim.Run(0.12f * dur, k => { if (arm != null) arm.localRotation = armStart * Quaternion.Euler(-25 * k, 0, 0); });
            Sfx.Play(Sfx.Whoosh, 0.8f);
            yield return Anim.Run(0.1f * dur, k => { if (arm != null) arm.localRotation = armStart * Quaternion.Euler(-25 + 110 * k, 0, 0); });
            Vector3 startRock = rockInArm != null ? rockInArm.position : pos + Vector3.up * 1.5f;
            if (rockInArm != null) rockInArm.gameObject.SetActive(false);

            var rock = SummonFactory.Prim(PrimitiveType.Sphere, root, startRock, Vector3.one * 0.45f, SummonFactory.Mat("#8a8a80"));
            temps.Add(rock);
            Vector3 end = CastleCenter(target) + Vector3.up * 0.6f;
            Vector3 ctrl = (startRock + end) / 2 + Vector3.up * 6f;
            yield return Anim.Run(0.55f * dur, k =>
            {
                rock.transform.position = Bezier(startRock, ctrl, end, k);
                rock.transform.Rotate(300 * Time.deltaTime, 120 * Time.deltaTime, 0);
            });
            Kill(rock);
            Burst(end, Palette.Hex("#9a948a"), 26, 5f, 0.18f, 0.8f, 9f);
            Burst(end, Palette.Hex("#ffb040"), 14, 4f, 0.12f, 0.4f, 2f, true);
            onHit?.Invoke();
            yield return Anim.Run(0.2f * dur, k => ct.localScale = full * (1 - k));
            Kill(cat);
        }

        IEnumerator DragonAttack(string cardId, int attacker, float dur, System.Action onHit)
        {
            int target = 1 - attacker;
            var dragon = Spawn(cardId);
            var dt = dragon.transform;
            Vector3 full = dt.localScale * 1.3f;
            dt.localScale = full;
            Vector3 start = castleHome[attacker] + new Vector3(-7f, 5f, attacker == 0 ? -3f : 3f);
            Vector3 hover = CastleTop(target) + new Vector3(0, 2.6f, attacker == 0 ? -3.2f : 3.2f);
            Vector3 ctrl = (start + hover) / 2 + new Vector3(4f, 3f, 0);
            Sfx.Play(Sfx.Whoosh, 1f, 0.7f);
            yield return Anim.Run(0.4f * dur, k =>
            {
                float e = Anim.InOutCubic(k);
                Vector3 p = Bezier(start, ctrl, hover, e);
                Vector3 n = Bezier(start, ctrl, hover, Mathf.Min(1, e + 0.03f));
                dt.position = p;
                Vector3 dir = n - p; dir.y *= 0.3f;
                if (dir.sqrMagnitude > 1e-5f) dt.rotation = Quaternion.LookRotation(dir);
            });
            // 성을 내려다보며 화염
            Vector3 look = CastleCenter(target) - dt.position;
            Quaternion fromRot = dt.rotation, toRot = Quaternion.LookRotation(look.normalized);
            yield return Anim.Run(0.1f * dur, k => dt.rotation = Quaternion.Slerp(fromRot, toRot, k));
            Sfx.Play(Sfx.Fire, 1f);
            var mouth = dt.Find("Head/Mouth");
            bool hit = false;
            float fireTime = 0.35f * dur;
            float spawnAcc = 0f;
            yield return Anim.Run(fireTime, k =>
            {
                spawnAcc += Time.deltaTime * Anim.Speed * 60f;
                while (spawnAcc > 1f)
                {
                    spawnAcc -= 1f;
                    Vector3 from = mouth != null ? mouth.position : dt.position;
                    Vector3 to = CastleCenter(target) + new Vector3(Random.Range(-1.4f, 1.4f), Random.Range(-0.3f, 1.2f), Random.Range(-0.8f, 0.8f));
                    StartCoroutine(FireBall(from, to));
                }
                dt.position += Vector3.up * Mathf.Sin(k * Mathf.PI * 4) * 0.004f;
                if (!hit && k > 0.35f) { hit = true; onHit?.Invoke(); }
            });
            if (!hit) onHit?.Invoke();
            Burst(CastleCenter(target), Palette.Hex("#ff6a20"), 30, 5f, 0.16f, 0.7f, 1f, true);
            // 날아감
            Vector3 exitPos = dt.position + new Vector3(8f, 6f, attacker == 0 ? 3f : -3f);
            Vector3 p0 = dt.position;
            yield return Anim.Run(0.3f * dur, k =>
            {
                dt.position = Vector3.Lerp(p0, exitPos, Anim.InCubic(k));
                Vector3 dir = exitPos - p0;
                dt.rotation = Quaternion.Slerp(dt.rotation, Quaternion.LookRotation(dir), 0.15f);
            });
            Kill(dragon);
        }

        IEnumerator FireBall(Vector3 from, Vector3 to)
        {
            var colors = new[] { "#ffd040", "#ff8a20", "#ff4a10" };
            var ball = SummonFactory.Prim(PrimitiveType.Sphere, root, from, Vector3.one * 0.25f, SummonFactory.Glow(colors[Random.Range(0, 3)]));
            temps.Add(ball);
            float t = 0;
            float life = 0.35f;
            while (t < life && ball != null && !Anim.Skipping)
            {
                float k = t / life;
                ball.transform.position = Vector3.Lerp(from, to, k);
                ball.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.75f, k);
                yield return null;
                t += Time.deltaTime * Anim.Speed;
            }
            Kill(ball);
        }

        /// <summary>공격 카드 사용: 소환물이 상대 성으로 날아가 공격</summary>
        public IEnumerator FlyToEnemy(string cardId, int attacker, float dur, Color impact)
        {
            int target = 1 - attacker;
            var go = Spawn(cardId);
            var t = go.transform;
            Vector3 full = t.localScale;
            Vector3 start = Front(attacker);
            t.position = start;
            t.rotation = Quaternion.LookRotation(Toward(attacker));
            yield return Anim.Run(0.25f * dur, k => t.localScale = full * Anim.OutBack(k));
            Sfx.Play(Sfx.Whoosh, 0.8f);
            Vector3 end = CastleCenter(target) + Vector3.up * 0.4f;
            Vector3 ctrl = (start + end) / 2 + Vector3.up * 3.5f;
            yield return Anim.Run(0.5f * dur, k =>
            {
                float e = Anim.InOutCubic(k);
                t.position = Bezier(start, ctrl, end, e);
                t.Rotate(0, 0, 0);
            });
            Sfx.Play(Sfx.Hit, 0.8f);
            Burst(end, impact, 24, 4.5f, 0.14f, 0.6f, 2f, true);
            yield return Anim.Run(0.25f * dur, k => t.localScale = full * (1 - k));
            Kill(go);
        }

        /// <summary>성 피격: 흔들림 + 파편</summary>
        public IEnumerator CastleHit(int p, int amount)
        {
            var c = castles[p];
            Sfx.Play(Sfx.Boom, Mathf.Clamp(0.5f + amount * 0.1f, 0.5f, 1f));
            Burst(CastleTop(p), Palette.Hex("#8a8680"), 8 + amount * 4, 4f, 0.16f, 0.8f, 9f);
            float strength = Mathf.Clamp(0.08f + amount * 0.04f, 0.1f, 0.35f);
            yield return Anim.Run(0.45f, k =>
            {
                if (c == null || fallen[p]) return;
                float s = strength * (1 - k);
                c.position = castleHome[p] + new Vector3(Mathf.Sin(k * 60f) * s, 0, Mathf.Cos(k * 47f) * s * 0.5f);
            });
            if (c != null && !fallen[p]) c.position = castleHome[p];
        }

        /// <summary>배신자: 성 안에서 폭발</summary>
        public IEnumerator TraitorExplosion(string cardId, int p, float dur)
        {
            var go = Spawn(cardId);
            var t = go.transform;
            Vector3 pos = castleHome[p] + Vector3.up * 1.4f + (p == 0 ? Vector3.forward : Vector3.back) * 1.2f;
            t.position = pos;
            t.rotation = Quaternion.Euler(0, p == 0 ? 0 : 180, 0);
            Vector3 full = t.localScale;
            yield return Anim.Run(0.3f * dur, k => t.localScale = full * Anim.OutBack(k));
            yield return Anim.Wait(0.2f * dur);
            Kill(go);
            Burst(pos, Palette.Hex("#b040ff"), 26, 5f, 0.16f, 0.6f, 2f, true);
            Burst(pos, Palette.Hex("#ff7030"), 18, 4f, 0.14f, 0.5f, 2f, true);
            yield return CastleHit(p, 1);
        }

        /// <summary>성 수리: 초록 반짝임</summary>
        public IEnumerator CastleRepair(int p)
        {
            Sfx.Play(Sfx.Repair, 0.8f);
            for (int i = 0; i < 3; i++)
            {
                Burst(CastleCenter(p) + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1f, 1f)), Palette.Hex("#70ff90"), 10, 2.2f, 0.1f, 0.8f, -2.5f, true);
                yield return Anim.Wait(0.12f);
            }
        }

        /// <summary>성벽 반응: 성 앞에 벽이 솟아 공격을 막음</summary>
        public IEnumerator WallBlock(string cardId, int p)
        {
            var go = Spawn(cardId);
            var t = go.transform;
            Vector3 pos = Front(p) + (p == 0 ? Vector3.back : Vector3.forward) * 0.6f;
            t.position = pos + Vector3.down * 1.4f;
            t.rotation = Quaternion.identity;
            t.localScale *= 1.6f;
            Sfx.Play(Sfx.Block, 1f);
            Burst(pos, Palette.Hex("#c8c0a8"), 18, 3f, 0.14f, 0.6f, 6f);
            yield return Anim.Run(0.35f, k => t.position = pos + Vector3.down * 1.4f * (1 - Anim.OutBack(k)));
            Burst(pos + Vector3.up * 1.5f, Palette.Hex("#80d0ff"), 20, 3f, 0.1f, 0.6f, 0f, true);
            yield return Anim.Wait(0.5f);
            yield return Anim.Run(0.3f, k => t.position = pos + Vector3.down * 1.6f * Anim.InCubic(k));
            Kill(go);
        }

        /// <summary>폐기: 제단의 빛이 카드를 태움</summary>
        public void TrashFlame(int p)
        {
            Vector3 pos = new Vector3(0, 0.6f, 0.2f);
            Burst(pos, Palette.Hex("#ff9030"), 18, 2.5f, 0.12f, 0.6f, -3f, true);
            Burst(pos, Palette.Hex("#fff0a0"), 10, 1.5f, 0.08f, 0.5f, -4f, true);
        }

        // =====================================================================
        // 성 상태 (연기, 함락)
        // =====================================================================

        public void SetCastleHp(int p, int hp, int maxHp = 50)
        {
            float lost = 1f - Mathf.Clamp01(hp / (float)maxHp);
            smokeRate[p] = lost < 0.3f ? 0f : (lost - 0.3f) * 6f;
            if (hp <= 0 && !fallen[p]) StartCoroutine(Collapse(p));
            if (hp > 0 && fallen[p])
            {
                fallen[p] = false;
                castles[p].position = castleHome[p];
                castles[p].rotation = Quaternion.Euler(0, p == 0 ? 180 : 0, 0);
            }
        }

        IEnumerator Collapse(int p)
        {
            fallen[p] = true;
            var c = castles[p];
            Burst(CastleCenter(p), Palette.Hex("#8a8680"), 40, 5f, 0.22f, 1.2f, 6f);
            Sfx.Play(Sfx.Boom, 1f, 0.7f);
            Vector3 start = c.position;
            Quaternion r0 = c.rotation;
            float t = 0;
            while (t < 1.2f)
            {
                float k = t / 1.2f;
                c.position = start + Vector3.down * 1.6f * k * k + new Vector3(Mathf.Sin(t * 50) * 0.05f, 0, 0);
                c.rotation = r0 * Quaternion.Euler(8 * k, 0, 5 * k);
                yield return null;
                t += Time.deltaTime;
            }
        }

        IEnumerator SmokeLoop(int p)
        {
            var mats = new[] { SummonFactory.Mat("#5a5550"), SummonFactory.Mat("#3a3632"), SummonFactory.Mat("#77716a") };
            float acc = 0;
            while (true)
            {
                acc += Time.deltaTime * smokeRate[p];
                while (acc > 1f)
                {
                    acc -= 1f;
                    StartCoroutine(SmokePuff(CastleTop(p) + new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(-1f, 0.2f), Random.Range(-1f, 1f)), mats[Random.Range(0, 3)]));
                }
                yield return null;
            }
        }

        IEnumerator SmokePuff(Vector3 pos, Material mat)
        {
            var puff = SummonFactory.Prim(PrimitiveType.Sphere, root, pos, Vector3.one * 0.2f, mat);
            puff.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float life = Random.Range(1.6f, 2.4f);
            float t = 0;
            Vector3 drift = new Vector3(Random.Range(0.2f, 0.5f), 1.1f, Random.Range(-0.2f, 0.2f));
            while (t < life)
            {
                float k = t / life;
                puff.transform.position = pos + drift * t;
                float s = Mathf.Lerp(0.25f, 0.9f, k) * (k > 0.75f ? (1 - k) * 4f : 1f);
                puff.transform.localScale = Vector3.one * s;
                yield return null;
                t += Time.deltaTime;
            }
            Destroy(puff);
        }

        // =====================================================================
        // 파티클 (작은 도형을 직접 움직임)
        // =====================================================================

        public void Burst(Vector3 pos, Color color, int count, float speed, float size, float life, float gravity, bool glow = false)
        {
            StartCoroutine(BurstRoutine(pos, color, count, speed, size, life, gravity, glow));
        }

        IEnumerator BurstRoutine(Vector3 pos, Color color, int count, float speed, float size, float life, float gravity, bool glow)
        {
            var mat = glow ? SummonFactory.Glow(color) : SummonFactory.Mat(color);
            var parts = new GameObject[count];
            var vel = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = SummonFactory.Prim(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, root, pos, Vector3.one * size, mat, Random.rotation.eulerAngles, "Particle");
                parts[i].GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                vel[i] = Random.onUnitSphere * speed * Random.Range(0.4f, 1f);
                vel[i].y = Mathf.Abs(vel[i].y) * 0.8f + speed * 0.3f;
                temps.Add(parts[i]);
            }
            float t = 0;
            while (t < life && !Anim.Skipping)
            {
                float dt = Time.deltaTime * Anim.Speed;
                float k = t / life;
                for (int i = 0; i < count; i++)
                {
                    if (parts[i] == null) continue;
                    vel[i].y -= gravity * dt;
                    var tr = parts[i].transform;
                    tr.position += vel[i] * dt;
                    if (tr.position.y < 0.03f) { tr.position = new Vector3(tr.position.x, 0.03f, tr.position.z); vel[i] *= 0.5f; }
                    tr.localScale = Vector3.one * size * (1 - k * k);
                }
                yield return null;
                t += dt;
            }
            foreach (var p in parts) Kill(p);
        }
    }
}
