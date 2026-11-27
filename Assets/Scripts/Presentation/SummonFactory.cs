using System.Collections.Generic;
using UnityEngine;

namespace KingdomDeck.Presentation
{
    /// <summary>
    /// 기본 도형으로 만드는 로우폴리 임시 모델 (소환물, 성, 나무 등).
    /// 에디터 메뉴에서 이 모델들을 프리팹으로 저장하고, 나중에 진짜 모델로 교체하면 된다.
    /// </summary>
    public static class SummonFactory
    {
        // ---------------- 재질 ----------------

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Shader lit, unlit;

        public static Material Mat(Color c, float smooth = 0.25f, float metallic = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + smooth.ToString("0.00") + metallic.ToString("0.00");
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(lit) { name = "KD_" + key };
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            mats[key] = m;
            return m;
        }

        public static Material Mat(string hex, float smooth = 0.25f, float metallic = 0f) => Mat(Palette.Hex(hex), smooth, metallic);

        /// <summary>빛나는 재질 (조명 영향 없음)</summary>
        public static Material Glow(Color c)
        {
            string key = "glow" + ColorUtility.ToHtmlStringRGBA(c);
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(unlit) { name = "KD_" + key };
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            mats[key] = m;
            return m;
        }

        public static Material Glow(string hex) => Glow(Palette.Hex(hex));

        public static IEnumerable<Material> AllMaterials => mats.Values;

        // ---------------- 도형 ----------------

        static Mesh cone, wedge;

        public static Mesh ConeMesh
        {
            get
            {
                if (cone != null) return cone;
                const int seg = 12;
                var v = new List<Vector3>();
                var t = new List<int>();
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                    var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                    int b = v.Count;
                    v.Add(p0); v.Add(new Vector3(0, 1, 0)); v.Add(p1);
                    t.Add(b); t.Add(b + 1); t.Add(b + 2);
                    b = v.Count;
                    v.Add(p0); v.Add(p1); v.Add(Vector3.zero);
                    t.Add(b); t.Add(b + 1); t.Add(b + 2);
                }
                cone = DoubleSided("KD_Cone", v, t);
                return cone;
            }
        }

        /// <summary>삼각 지붕 (프리즘). 바닥 1x1, 높이 1, 길이 방향 z.</summary>
        public static Mesh WedgeMesh
        {
            get
            {
                if (wedge != null) return wedge;
                var a = new Vector3(-0.5f, 0, -0.5f); var b = new Vector3(0.5f, 0, -0.5f); var c = new Vector3(0, 1, -0.5f);
                var d = new Vector3(-0.5f, 0, 0.5f); var e = new Vector3(0.5f, 0, 0.5f); var f = new Vector3(0, 1, 0.5f);
                var v = new List<Vector3> { a, c, b, d, e, f, a, d, f, a, f, c, b, c, f, b, f, e, a, b, e, a, e, d };
                var t = new List<int>();
                for (int i = 0; i < 6; i++) t.Add(i);
                for (int i = 6; i < v.Count; i++) t.Add(i);
                wedge = DoubleSided("KD_Wedge", v, t);
                return wedge;
            }
        }

        /// <summary>감는 방향을 신경 쓰지 않도록 앞뒷면을 모두 만든다</summary>
        static Mesh DoubleSided(string name, List<Vector3> v, List<int> t)
        {
            int n = v.Count;
            var verts = new List<Vector3>(v);
            verts.AddRange(v);
            var tris = new List<int>(t);
            for (int i = 0; i < t.Count; i += 3)
            {
                tris.Add(t[i] + n);
                tris.Add(t[i + 2] + n);
                tris.Add(t[i + 1] + n);
            }
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject MeshObj(Mesh mesh, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, string name = "Mesh")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Cube(Transform p, Vector3 pos, Vector3 s, Material m, Vector3? e = null) => Prim(PrimitiveType.Cube, p, pos, s, m, e);
        static GameObject Cyl(Transform p, Vector3 pos, Vector3 s, Material m, Vector3? e = null) => Prim(PrimitiveType.Cylinder, p, pos, s, m, e);
        static GameObject Sph(Transform p, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Sphere, p, pos, s, m);
        static GameObject Cap(Transform p, Vector3 pos, Vector3 s, Material m, Vector3? e = null) => Prim(PrimitiveType.Capsule, p, pos, s, m, e);
        static GameObject Cone(Transform p, Vector3 pos, Vector3 s, Material m, Vector3? e = null) => MeshObj(ConeMesh, p, pos, s, m, e, "Cone");
        static GameObject Roof(Transform p, Vector3 pos, Vector3 s, Material m, Vector3? e = null) => MeshObj(WedgeMesh, p, pos, s, m, e, "Roof");

        static Transform Root(string name)
        {
            var go = new GameObject(name);
            return go.transform;
        }

        // ---------------- 카드별 소환물 ----------------

        public static GameObject Build(string id)
        {
            switch (id)
            {
                case "copper": return Coins("#c47a45", 3);
                case "silver": return Coins("#d9dfe6", 5);
                case "gold": return Coins("#f2c744", 8);
                case "archers": return Archers();
                case "catapult": return Catapult();
                case "dragon": return Dragon();
                case "traitor": return Traitor();
                case "wall": return WallSegment();
                case "altar": return Altar();
                case "village": return Village();
                case "workshop": return Workshop();
                case "smithy": return Smithy();
                case "guild": return Wagon();
                case "mercenary": return Mercenary();
                case "market": return Market();
                case "monastery": return Monastery();
                case "mine": return Mine();
                case "witch": return Witch();
                case "banquet": return Banquet();
                case "warcouncil": return WarCouncil();
                case "attendant": return Attendant();
                case "engineers": return Engineers();
                case "moneychanger": return MoneyChanger();
                case "decree": return Decree();
                case "volunteers": return Volunteers();
                case "raiders": return Raiders();
                case "watchtower": return Watchtower();
                default:
                {
                    var r = Root("Summon_" + id);
                    Cube(r, new Vector3(0, 0.5f, 0), Vector3.one, Mat("#888888"));
                    return r.gameObject;
                }
            }
        }

        static GameObject Coins(string hex, int count)
        {
            var r = Root("Summon_Coins");
            var m = Mat(hex, 0.8f, 0.9f);
            var rnd = new System.Random(count);
            for (int i = 0; i < count; i++)
            {
                float x = (float)(rnd.NextDouble() - 0.5) * 0.8f, z = (float)(rnd.NextDouble() - 0.5) * 0.8f;
                int stack = 1 + rnd.Next(3);
                for (int k = 0; k < stack; k++)
                    Cyl(r, new Vector3(x, 0.05f + k * 0.11f, z), new Vector3(0.45f, 0.05f, 0.45f), m);
            }
            // 세워진 큰 동전 하나
            var big = Cyl(r, new Vector3(0, 0.55f, 0.1f), new Vector3(0.7f, 0.06f, 0.7f), m, new Vector3(80, 0, 0));
            Cyl(big.transform, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 0.5f, 0.6f), Mat(Color.Lerp(Palette.Hex(hex), Color.black, 0.25f), 0.6f, 0.9f));
            return r.gameObject;
        }

        public static GameObject Soldier(Transform parent, Vector3 pos, string tunic, string helmet, float scale = 1f)
        {
            var s = new GameObject("Soldier").transform;
            s.SetParent(parent, false);
            s.localPosition = pos;
            s.localScale = Vector3.one * scale;
            Cap(s, new Vector3(0, 0.45f, 0), new Vector3(0.38f, 0.38f, 0.32f), Mat(tunic));
            Sph(s, new Vector3(0, 0.95f, 0), new Vector3(0.28f, 0.28f, 0.28f), Mat("#e8c39e"));
            Cone(s, new Vector3(0, 1.02f, 0), new Vector3(0.32f, 0.2f, 0.32f), Mat(helmet, 0.6f, 0.7f));
            return s.gameObject;
        }

        static GameObject Archers()
        {
            var r = Root("Summon_Archers");
            var wood = Mat("#7a5230");
            for (int i = 0; i < 3; i++)
            {
                var s = Soldier(r, new Vector3((i - 1) * 0.55f, 0, (i == 1 ? 0.25f : 0)), "#3f6b3a", "#8a8a8a");
                // 활
                Cube(s.transform, new Vector3(0.24f, 0.6f, 0.08f), new Vector3(0.04f, 0.7f, 0.04f), wood, new Vector3(0, 0, 8));
                Cube(s.transform, new Vector3(0.21f, 0.6f, 0.08f), new Vector3(0.01f, 0.66f, 0.01f), Mat("#e8e0c8"));
            }
            return r.gameObject;
        }

        static GameObject Catapult()
        {
            var r = Root("Summon_Catapult");
            var wood = Mat("#7a5230");
            var dark = Mat("#4a301c");
            Cube(r, new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.18f, 1.4f), wood);
            foreach (var x in new[] { -0.5f, 0.5f })
                foreach (var z in new[] { -0.5f, 0.5f })
                    Cyl(r, new Vector3(x, 0.25f, z), new Vector3(0.42f, 0.05f, 0.42f), dark, new Vector3(0, 0, 90));
            Cube(r, new Vector3(-0.3f, 0.65f, 0.1f), new Vector3(0.1f, 0.6f, 0.1f), dark);
            Cube(r, new Vector3(0.3f, 0.65f, 0.1f), new Vector3(0.1f, 0.6f, 0.1f), dark);
            var arm = new GameObject("Arm").transform;
            arm.SetParent(r, false);
            arm.localPosition = new Vector3(0, 0.9f, 0.1f);
            arm.localRotation = Quaternion.Euler(-35, 0, 0);
            Cube(arm, new Vector3(0, 0, -0.45f), new Vector3(0.1f, 0.1f, 1.2f), wood);
            Cube(arm, new Vector3(0, 0.05f, -1.0f), new Vector3(0.3f, 0.1f, 0.3f), dark);
            Sph(arm, new Vector3(0, 0.2f, -1.0f), new Vector3(0.28f, 0.28f, 0.28f), Mat("#8a8a80"));
            return r.gameObject;
        }

        static GameObject Dragon()
        {
            var r = Root("Summon_Dragon");
            var body = Mat("#9b1d1d", 0.45f);
            var belly = Mat("#e0a040", 0.3f);
            var horn = Mat("#f0e0c0");
            Cap(r, new Vector3(0, 0, 0), new Vector3(0.7f, 0.9f, 0.7f), body, new Vector3(90, 0, 0));
            Cap(r, new Vector3(0, -0.12f, 0.05f), new Vector3(0.5f, 0.7f, 0.45f), belly, new Vector3(90, 0, 0));
            // 목과 머리
            Cap(r, new Vector3(0, 0.35f, 0.75f), new Vector3(0.32f, 0.45f, 0.32f), body, new Vector3(45, 0, 0));
            var head = new GameObject("Head").transform;
            head.SetParent(r, false);
            head.localPosition = new Vector3(0, 0.62f, 1.05f);
            Cube(head, Vector3.zero, new Vector3(0.38f, 0.3f, 0.5f), body);
            Cube(head, new Vector3(0, -0.06f, 0.32f), new Vector3(0.3f, 0.16f, 0.3f), body);
            Cone(head, new Vector3(-0.12f, 0.12f, -0.1f), new Vector3(0.08f, 0.3f, 0.08f), horn, new Vector3(-40, 0, 0));
            Cone(head, new Vector3(0.12f, 0.12f, -0.1f), new Vector3(0.08f, 0.3f, 0.08f), horn, new Vector3(-40, 0, 0));
            Sph(head, new Vector3(-0.14f, 0.06f, 0.12f), Vector3.one * 0.08f, Glow("#ffd040"));
            Sph(head, new Vector3(0.14f, 0.06f, 0.12f), Vector3.one * 0.08f, Glow("#ffd040"));
            var mouth = new GameObject("Mouth").transform;
            mouth.SetParent(head, false);
            mouth.localPosition = new Vector3(0, -0.06f, 0.5f);
            // 꼬리
            Cap(r, new Vector3(0, -0.05f, -0.95f), new Vector3(0.28f, 0.5f, 0.28f), body, new Vector3(100, 0, 0));
            Cap(r, new Vector3(0, -0.15f, -1.4f), new Vector3(0.16f, 0.4f, 0.16f), body, new Vector3(105, 0, 0));
            Cone(r, new Vector3(0, -0.2f, -1.75f), new Vector3(0.25f, 0.25f, 0.08f), body, new Vector3(-90, 0, 0));
            // 날개 (피벗 기준으로 펄럭임)
            foreach (int side in new[] { -1, 1 })
            {
                var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
                pivot.SetParent(r, false);
                pivot.localPosition = new Vector3(0.25f * side, 0.25f, 0.1f);
                Cube(pivot, new Vector3(0.75f * side, 0, 0), new Vector3(1.5f, 0.04f, 0.9f), Mat("#6e1414", 0.3f));
                Cube(pivot, new Vector3(1.45f * side, 0.02f, -0.15f), new Vector3(0.6f, 0.05f, 0.7f), Mat("#6e1414", 0.3f), new Vector3(0, 20 * side, 0));
                Cube(pivot, new Vector3(0.75f * side, 0.03f, 0.4f), new Vector3(1.5f, 0.07f, 0.07f), body);
            }
            // 다리
            foreach (int side in new[] { -1, 1 })
            {
                Cap(r, new Vector3(0.28f * side, -0.38f, 0.3f), new Vector3(0.14f, 0.2f, 0.14f), body);
                Cap(r, new Vector3(0.28f * side, -0.38f, -0.35f), new Vector3(0.16f, 0.22f, 0.16f), body);
            }
            r.gameObject.AddComponent<Flapper>();
            return r.gameObject;
        }

        static GameObject Traitor()
        {
            var r = Root("Summon_Traitor");
            Cap(r, new Vector3(0, 0.5f, 0), new Vector3(0.45f, 0.5f, 0.4f), Mat("#2a1e32"));
            Cone(r, new Vector3(0, 0.9f, 0), new Vector3(0.42f, 0.45f, 0.42f), Mat("#3a2848"));
            Sph(r, new Vector3(-0.07f, 0.98f, 0.17f), Vector3.one * 0.06f, Glow("#ff3030"));
            Sph(r, new Vector3(0.07f, 0.98f, 0.17f), Vector3.one * 0.06f, Glow("#ff3030"));
            Cube(r, new Vector3(0.28f, 0.55f, 0.15f), new Vector3(0.05f, 0.35f, 0.02f), Mat("#cfd6dd", 0.9f, 0.8f), new Vector3(20, 0, -20));
            return r.gameObject;
        }

        public static GameObject WallSegment()
        {
            var r = Root("Summon_Wall");
            var stone = Mat("#8d8f94");
            var dark = Mat("#6c6e73");
            Cube(r, new Vector3(0, 0.55f, 0), new Vector3(2.4f, 1.1f, 0.5f), stone);
            for (int i = 0; i < 6; i++)
                Cube(r, new Vector3(-1.0f + i * 0.4f, 1.25f, 0), new Vector3(0.22f, 0.3f, 0.5f), i % 2 == 0 ? stone : dark);
            for (int i = 0; i < 3; i++)
                Cube(r, new Vector3(-0.8f + i * 0.8f, 0.35f + (i % 2) * 0.35f, -0.26f), new Vector3(0.5f, 0.22f, 0.03f), dark);
            return r.gameObject;
        }

        static GameObject Altar()
        {
            var r = Root("Summon_Altar");
            var stone = Mat("#c9c2b0");
            Cube(r, new Vector3(0, 0.15f, 0), new Vector3(1.2f, 0.3f, 0.9f), stone);
            Cube(r, new Vector3(0, 0.45f, 0), new Vector3(0.8f, 0.3f, 0.6f), stone);
            Cyl(r, new Vector3(-0.35f, 0.75f, 0), new Vector3(0.12f, 0.3f, 0.12f), stone);
            Cyl(r, new Vector3(0.35f, 0.75f, 0), new Vector3(0.12f, 0.3f, 0.12f), stone);
            Sph(r, new Vector3(0, 0.95f, 0), Vector3.one * 0.4f, Glow("#fff2b0"));
            Sph(r, new Vector3(0, 0.95f, 0), Vector3.one * 0.25f, Glow("#ffffff"));
            return r.gameObject;
        }

        static void House(Transform parent, Vector3 pos, float s, string wall, string roof)
        {
            var h = new GameObject("House").transform;
            h.SetParent(parent, false);
            h.localPosition = pos;
            h.localScale = Vector3.one * s;
            Cube(h, new Vector3(0, 0.35f, 0), new Vector3(0.8f, 0.7f, 0.7f), Mat(wall));
            Roof(h, new Vector3(0, 0.7f, 0), new Vector3(0.95f, 0.5f, 0.8f), Mat(roof), new Vector3(0, 90, 0));
            Cube(h, new Vector3(0, 0.2f, -0.36f), new Vector3(0.2f, 0.4f, 0.02f), Mat("#4a301c"));
        }

        static GameObject Village()
        {
            var r = Root("Summon_Village");
            House(r, new Vector3(-0.6f, 0, 0.1f), 1f, "#e8dcc0", "#a04030");
            House(r, new Vector3(0.55f, 0, 0.3f), 0.85f, "#e0d0b0", "#7a4a2a");
            House(r, new Vector3(0, 0, -0.45f), 0.75f, "#f0e6d0", "#b05a30");
            Cyl(r, new Vector3(0.1f, 0.15f, 0.15f), new Vector3(0.3f, 0.15f, 0.3f), Mat("#8a8a8a"));
            return r.gameObject;
        }

        static GameObject Workshop()
        {
            var r = Root("Summon_Workshop");
            Cube(r, new Vector3(0, 0.3f, 0.2f), new Vector3(1.2f, 0.08f, 0.6f), Mat("#8a6a42"));
            foreach (var x in new[] { -0.5f, 0.5f }) Cube(r, new Vector3(x, 0.13f, 0.2f), new Vector3(0.08f, 0.3f, 0.5f), Mat("#5a4028"));
            var s = Soldier(r, new Vector3(0, 0, -0.35f), "#6a5a8a", "#6a4a2a");
            var hammer = new GameObject("Hammer").transform;
            hammer.SetParent(s.transform, false);
            hammer.localPosition = new Vector3(0.25f, 0.7f, 0.1f);
            Cube(hammer, new Vector3(0, 0.2f, 0), new Vector3(0.05f, 0.4f, 0.05f), Mat("#7a5230"));
            Cube(hammer, new Vector3(0, 0.42f, 0), new Vector3(0.22f, 0.1f, 0.1f), Mat("#808890", 0.7f, 0.8f));
            hammer.gameObject.AddComponent<Swinger>();
            return r.gameObject;
        }

        static GameObject Smithy()
        {
            var r = Root("Summon_Smithy");
            var iron = Mat("#4a4c52", 0.6f, 0.8f);
            Cube(r, new Vector3(0, 0.2f, 0), new Vector3(0.4f, 0.4f, 0.4f), Mat("#5a4028"));
            Cube(r, new Vector3(0, 0.5f, 0), new Vector3(0.8f, 0.2f, 0.35f), iron);
            Cone(r, new Vector3(0.5f, 0.42f, 0), new Vector3(0.25f, 0.35f, 0.2f), iron, new Vector3(0, 0, -90));
            Cube(r, new Vector3(-0.05f, 0.62f, 0), new Vector3(0.45f, 0.05f, 0.08f), Glow("#ff8a20"));
            Sph(r, new Vector3(-0.7f, 0.25f, 0.2f), Vector3.one * 0.5f, Glow("#ff6a10"));
            Sph(r, new Vector3(-0.7f, 0.35f, 0.2f), Vector3.one * 0.3f, Glow("#ffd060"));
            return r.gameObject;
        }

        static GameObject Wagon()
        {
            var r = Root("Summon_Wagon");
            var wood = Mat("#8a6034");
            Cube(r, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.35f, 1.4f), wood);
            foreach (var x in new[] { -0.5f, 0.5f })
                foreach (var z in new[] { -0.45f, 0.45f })
                    Cyl(r, new Vector3(x, 0.28f, z), new Vector3(0.5f, 0.04f, 0.5f), Mat("#4a301c"), new Vector3(0, 0, 90));
            Cyl(r, new Vector3(0, 0.7f, 0), new Vector3(0.95f, 0.65f, 0.95f), Mat("#e8dcc0"), new Vector3(90, 0, 0));
            Cube(r, new Vector3(0.1f, 0.75f, 0.85f), new Vector3(0.3f, 0.3f, 0.3f), Mat("#b08040"));
            Sph(r, new Vector3(-0.15f, 0.8f, 0.85f), Vector3.one * 0.22f, Mat("#c03030"));
            Cube(r, new Vector3(0, 0.4f, 1.0f), new Vector3(0.06f, 0.06f, 0.8f), wood);
            return r.gameObject;
        }

        static GameObject Mercenary()
        {
            var r = Root("Summon_Mercenary");
            var s = Soldier(r, Vector3.zero, "#7a2a2a", "#606870", 1.25f);
            Cube(s.transform, new Vector3(0.3f, 0.75f, 0.1f), new Vector3(0.06f, 0.8f, 0.03f), Mat("#cfd6dd", 0.9f, 0.9f), new Vector3(0, 0, -15));
            Cube(s.transform, new Vector3(0.27f, 0.38f, 0.1f), new Vector3(0.2f, 0.04f, 0.04f), Mat("#e0b040", 0.7f, 0.8f), new Vector3(0, 0, -15));
            Cyl(s.transform, new Vector3(-0.25f, 0.5f, 0.1f), new Vector3(0.45f, 0.03f, 0.45f), Mat("#8a3020"), new Vector3(90, 0, 90));
            return r.gameObject;
        }

        static GameObject Market()
        {
            var r = Root("Summon_Market");
            foreach (var x in new[] { -0.55f, 0.55f })
                foreach (var z in new[] { -0.35f, 0.35f })
                    Cyl(r, new Vector3(x, 0.45f, z), new Vector3(0.06f, 0.45f, 0.06f), Mat("#6a4a2a"));
            Roof(r, new Vector3(0, 0.9f, 0), new Vector3(1.3f, 0.45f, 0.9f), Mat("#c03838"), new Vector3(0, 90, 0));
            Roof(r, new Vector3(0, 0.905f, 0), new Vector3(1.32f, 0.44f, 0.3f), Mat("#f0e6d0"), new Vector3(0, 90, 0));
            Cube(r, new Vector3(0, 0.35f, 0), new Vector3(1.0f, 0.1f, 0.5f), Mat("#8a6034"));
            Sph(r, new Vector3(-0.3f, 0.47f, 0), Vector3.one * 0.15f, Mat("#e04020"));
            Sph(r, new Vector3(-0.15f, 0.47f, 0.05f), Vector3.one * 0.15f, Mat("#f0c020"));
            Sph(r, new Vector3(0.2f, 0.47f, -0.05f), Vector3.one * 0.15f, Mat("#60b030"));
            Cube(r, new Vector3(0.75f, 0.18f, -0.5f), Vector3.one * 0.35f, Mat("#a07848"));
            return r.gameObject;
        }

        static GameObject Monastery()
        {
            var r = Root("Summon_Monastery");
            var wall = Mat("#d8d0c0");
            Cube(r, new Vector3(-0.2f, 0.4f, 0), new Vector3(1.2f, 0.8f, 0.8f), wall);
            Roof(r, new Vector3(-0.2f, 0.8f, 0), new Vector3(0.95f, 0.45f, 1.3f), Mat("#5a6a8a"), new Vector3(0, 90, 0));
            Cube(r, new Vector3(0.6f, 0.75f, 0), new Vector3(0.45f, 1.5f, 0.45f), wall);
            Cone(r, new Vector3(0.6f, 1.5f, 0), new Vector3(0.6f, 0.6f, 0.6f), Mat("#5a6a8a"));
            var bell = Sph(r, new Vector3(0.6f, 1.2f, -0.2f), Vector3.one * 0.2f, Mat("#e0b040", 0.8f, 0.9f));
            bell.AddComponent<Swinger>().amplitude = 25f;
            Cube(r, new Vector3(-0.2f, 0.3f, -0.41f), new Vector3(0.25f, 0.5f, 0.02f), Mat("#4a301c"));
            return r.gameObject;
        }

        static GameObject Mine()
        {
            var r = Root("Summon_Mine");
            var rock = Mat("#6a645e");
            Sph(r, new Vector3(0, 0.3f, 0.2f), new Vector3(1.3f, 0.8f, 1.0f), rock);
            Sph(r, new Vector3(-0.5f, 0.2f, -0.1f), new Vector3(0.6f, 0.5f, 0.6f), Mat("#7a746e"));
            Sph(r, new Vector3(0.2f, 0.55f, -0.15f), new Vector3(0.2f, 0.25f, 0.2f), Glow("#40e0ff"));
            Sph(r, new Vector3(-0.2f, 0.45f, -0.25f), new Vector3(0.15f, 0.2f, 0.15f), Glow("#ff50c0"));
            var s = Soldier(r, new Vector3(0.6f, 0, -0.45f), "#6a5a3a", "#e0c040", 0.9f);
            var pick = new GameObject("Pickaxe").transform;
            pick.SetParent(s.transform, false);
            pick.localPosition = new Vector3(-0.2f, 0.65f, 0.1f);
            Cube(pick, new Vector3(0, 0.22f, 0), new Vector3(0.05f, 0.45f, 0.05f), Mat("#7a5230"));
            Cube(pick, new Vector3(0, 0.45f, 0), new Vector3(0.4f, 0.06f, 0.06f), Mat("#808890", 0.7f, 0.8f));
            pick.gameObject.AddComponent<Swinger>();
            return r.gameObject;
        }

        static GameObject Witch()
        {
            var r = Root("Summon_Witch");
            Cap(r, new Vector3(0, 0.55f, 0), new Vector3(0.45f, 0.5f, 0.4f), Mat("#3a2050"));
            Sph(r, new Vector3(0, 1.02f, 0), Vector3.one * 0.3f, Mat("#a8d098"));
            Cyl(r, new Vector3(0, 1.12f, 0), new Vector3(0.6f, 0.02f, 0.6f), Mat("#2a1838"));
            Cone(r, new Vector3(0, 1.12f, 0), new Vector3(0.35f, 0.6f, 0.35f), Mat("#2a1838"), new Vector3(-12, 0, 0));
            Cube(r, new Vector3(0, 0.35f, 0), new Vector3(0.06f, 0.06f, 1.3f), Mat("#7a5230"));
            Cone(r, new Vector3(0, 0.35f, -0.7f), new Vector3(0.3f, 0.35f, 0.3f), Mat("#c0a060"), new Vector3(90, 0, 0));
            Sph(r, new Vector3(0.3f, 0.75f, 0.2f), Vector3.one * 0.18f, Glow("#80ff60"));
            return r.gameObject;
        }

        static GameObject Banquet()
        {
            var r = Root("Summon_Banquet");
            Cube(r, new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.08f, 0.7f), Mat("#8a5a30"));
            Cube(r, new Vector3(0, 0.46f, 0), new Vector3(1.65f, 0.06f, 0.4f), Mat("#c03040"));
            foreach (var x in new[] { -0.7f, 0.7f })
                foreach (var z in new[] { -0.25f, 0.25f })
                    Cube(r, new Vector3(x, 0.2f, z), new Vector3(0.08f, 0.45f, 0.08f), Mat("#5a3a20"));
            for (int i = 0; i < 4; i++)
            {
                float x = -0.55f + i * 0.37f;
                Cyl(r, new Vector3(x, 0.5f, -0.18f), new Vector3(0.22f, 0.01f, 0.22f), Mat("#e8e8e8"));
                Cyl(r, new Vector3(x + 0.1f, 0.58f, 0.18f), new Vector3(0.06f, 0.08f, 0.06f), Mat("#e0b040", 0.8f, 0.9f));
            }
            Sph(r, new Vector3(0, 0.6f, 0), new Vector3(0.45f, 0.25f, 0.3f), Mat("#b0602a"));
            Sph(r, new Vector3(-0.35f, 0.55f, 0.05f), Vector3.one * 0.14f, Mat("#d04020"));
            Sph(r, new Vector3(0.35f, 0.55f, 0.05f), Vector3.one * 0.14f, Mat("#80b030"));
            return r.gameObject;
        }

        static GameObject WarCouncil()
        {
            var r = Root("Summon_WarCouncil");
            var wood = Mat("#6a4a2a");
            Cube(r, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.08f, 0.9f), wood);
            foreach (var x in new[] { -0.6f, 0.6f })
                foreach (var z in new[] { -0.35f, 0.35f })
                    Cube(r, new Vector3(x, 0.2f, z), new Vector3(0.08f, 0.42f, 0.08f), Mat("#4a301c"));
            Cube(r, new Vector3(0, 0.5f, 0), new Vector3(1.1f, 0.01f, 0.7f), Mat("#e8d8a8"));
            Cube(r, new Vector3(-0.15f, 0.51f, 0.05f), new Vector3(0.4f, 0.01f, 0.25f), Mat("#7a9a5a"));
            Cube(r, new Vector3(0.25f, 0.51f, -0.1f), new Vector3(0.3f, 0.01f, 0.2f), Mat("#6a8ab0"));
            foreach (var (pos, col) in new[] { (new Vector3(-0.3f, 0.6f, 0.1f), "#4f8fe6"), (new Vector3(0.35f, 0.6f, -0.15f), "#e0533f"), (new Vector3(0.1f, 0.6f, 0.2f), "#e0533f") })
            {
                Cyl(r, pos, new Vector3(0.02f, 0.1f, 0.02f), Mat("#3a2a1a"));
                Cube(r, pos + new Vector3(0.05f, 0.07f, 0), new Vector3(0.1f, 0.06f, 0.01f), Mat(col));
            }
            Cyl(r, new Vector3(0.55f, 0.56f, 0.28f), new Vector3(0.06f, 0.06f, 0.06f), Mat("#f0e8d0"));
            Sph(r, new Vector3(0.55f, 0.65f, 0.28f), Vector3.one * 0.06f, Glow("#ffc040"));
            return r.gameObject;
        }

        static GameObject Attendant()
        {
            var r = Root("Summon_Attendant");
            var s = Soldier(r, Vector3.zero, "#4a6aa0", "#e0c040", 1.1f);
            Cone(s.transform, new Vector3(0.12f, 0.95f, 0.25f), new Vector3(0.12f, 0.5f, 0.12f), Mat("#e0b040", 0.8f, 0.9f), new Vector3(70, 0, 0));
            Cube(s.transform, new Vector3(-0.2f, 0.55f, 0.1f), new Vector3(0.18f, 0.24f, 0.02f), Mat("#f0e6d0"), new Vector3(0, 0, 10));
            return r.gameObject;
        }

        static GameObject Engineers()
        {
            var r = Root("Summon_Engineers");
            var wood = Mat("#7a5230");
            foreach (var x in new[] { -0.5f, 0.5f })
                foreach (var z in new[] { -0.3f, 0.3f })
                    Cube(r, new Vector3(x, 0.6f, z), new Vector3(0.06f, 1.2f, 0.06f), wood);
            Cube(r, new Vector3(0, 0.9f, 0), new Vector3(1.1f, 0.05f, 0.66f), wood);
            Cube(r, new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.6f, 0.5f), Mat("#a7a39a"));
            var arm = new GameObject("Crane").transform;
            arm.SetParent(r, false);
            arm.localPosition = new Vector3(0.5f, 1.2f, 0);
            Cube(arm, new Vector3(-0.4f, 0.1f, 0), new Vector3(1.0f, 0.06f, 0.06f), wood, new Vector3(0, 0, -10));
            Cube(arm, new Vector3(-0.85f, -0.15f, 0), new Vector3(0.01f, 0.45f, 0.01f), Mat("#d0c0a0"));
            Cube(arm, new Vector3(-0.85f, -0.42f, 0), new Vector3(0.22f, 0.16f, 0.22f), Mat("#a7a39a"));
            arm.gameObject.AddComponent<Swinger>().amplitude = 6f;
            var s = Soldier(r, new Vector3(0.75f, 0, -0.4f), "#8a6a3a", "#e0c040", 0.85f);
            var hammer = new GameObject("Hammer").transform;
            hammer.SetParent(s.transform, false);
            hammer.localPosition = new Vector3(0.25f, 0.7f, 0.1f);
            Cube(hammer, new Vector3(0, 0.2f, 0), new Vector3(0.05f, 0.4f, 0.05f), wood);
            Cube(hammer, new Vector3(0, 0.42f, 0), new Vector3(0.22f, 0.1f, 0.1f), Mat("#808890", 0.7f, 0.8f));
            hammer.gameObject.AddComponent<Swinger>();
            return r.gameObject;
        }

        static GameObject MoneyChanger()
        {
            var r = Root("Summon_MoneyChanger");
            Cube(r, new Vector3(0, 0.4f, 0), new Vector3(1.4f, 0.08f, 0.7f), Mat("#5a3a20"));
            Cube(r, new Vector3(0, 0.2f, 0), new Vector3(1.2f, 0.36f, 0.5f), Mat("#4a301c"));
            var brass = Mat("#e0b040", 0.8f, 0.9f);
            Cyl(r, new Vector3(0, 0.7f, 0), new Vector3(0.04f, 0.3f, 0.04f), brass);
            var beam = new GameObject("Beam").transform;
            beam.SetParent(r, false);
            beam.localPosition = new Vector3(0, 1.0f, 0);
            Cube(beam, Vector3.zero, new Vector3(0.9f, 0.03f, 0.03f), brass);
            foreach (var x in new[] { -0.42f, 0.42f })
            {
                Cube(beam, new Vector3(x, -0.15f, 0), new Vector3(0.01f, 0.3f, 0.01f), brass);
                Cyl(beam, new Vector3(x, -0.3f, 0), new Vector3(0.28f, 0.02f, 0.28f), brass);
            }
            beam.gameObject.AddComponent<Swinger>().amplitude = 5f;
            var copper = Mat("#c47a45", 0.8f, 0.9f);
            var gold = Mat("#f2c744", 0.8f, 0.9f);
            for (int k = 0; k < 4; k++) Cyl(r, new Vector3(-0.5f, 0.46f + k * 0.05f, 0.15f), new Vector3(0.16f, 0.02f, 0.16f), copper);
            for (int k = 0; k < 3; k++) Cyl(r, new Vector3(0.5f, 0.46f + k * 0.05f, 0.15f), new Vector3(0.16f, 0.02f, 0.16f), gold);
            return r.gameObject;
        }

        static GameObject Decree()
        {
            var r = Root("Summon_Decree");
            var red = Mat("#8a1e2a");
            var gold = Mat("#e0b040", 0.8f, 0.9f);
            Cube(r, new Vector3(0, 0.1f, 0), new Vector3(1.2f, 0.2f, 1.0f), Mat("#6a5a4a"));
            Cube(r, new Vector3(0, 0.45f, 0), new Vector3(0.8f, 0.5f, 0.6f), gold);
            Cube(r, new Vector3(0, 0.72f, 0), new Vector3(0.7f, 0.06f, 0.5f), red);
            Cube(r, new Vector3(0, 1.2f, 0.27f), new Vector3(0.8f, 1.1f, 0.1f), gold);
            Cube(r, new Vector3(0, 1.15f, 0.21f), new Vector3(0.6f, 0.85f, 0.03f), red);
            Sph(r, new Vector3(-0.4f, 1.8f, 0.27f), Vector3.one * 0.14f, gold);
            Sph(r, new Vector3(0.4f, 1.8f, 0.27f), Vector3.one * 0.14f, gold);
            foreach (var x in new[] { -0.42f, 0.42f }) Cube(r, new Vector3(x, 0.85f, 0), new Vector3(0.08f, 0.2f, 0.55f), gold);
            // 왕관
            var crown = new GameObject("Crown").transform;
            crown.SetParent(r, false);
            crown.localPosition = new Vector3(0, 0.8f, -0.05f);
            Cyl(crown, Vector3.zero, new Vector3(0.32f, 0.06f, 0.32f), gold);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI * 2 / 6;
                Cone(crown, new Vector3(Mathf.Cos(a) * 0.14f, 0.05f, Mathf.Sin(a) * 0.14f), new Vector3(0.07f, 0.14f, 0.07f), gold);
            }
            Sph(crown, new Vector3(0, 0.04f, -0.15f), Vector3.one * 0.06f, Glow("#ff3050"));
            return r.gameObject;
        }

        static GameObject Volunteers()
        {
            var r = Root("Summon_Volunteers");
            var wood = Mat("#7a5230");
            var iron = Mat("#909098", 0.6f, 0.7f);
            string[] tunics = { "#8a6a3a", "#6a7a3a", "#7a4a2a", "#5a6a4a" };
            for (int i = 0; i < 4; i++)
            {
                var s = Soldier(r, new Vector3((i - 1.5f) * 0.42f, 0, (i % 2) * 0.3f), tunics[i], "#d8c080", 0.85f);
                // 밀짚모자처럼 넓적하게
                var hat = s.transform.Find("Cone");
                if (hat != null) hat.localScale = new Vector3(0.55f, 0.14f, 0.55f);
                var fork = new GameObject("Pitchfork").transform;
                fork.SetParent(s.transform, false);
                fork.localPosition = new Vector3(0.24f, 0.1f, 0.12f);
                Cube(fork, new Vector3(0, 0.6f, 0), new Vector3(0.035f, 1.2f, 0.035f), wood);
                for (int t = -1; t <= 1; t++) Cube(fork, new Vector3(t * 0.06f, 1.28f, 0), new Vector3(0.02f, 0.18f, 0.02f), iron);
                Cube(fork, new Vector3(0, 1.19f, 0), new Vector3(0.15f, 0.02f, 0.02f), iron);
            }
            return r.gameObject;
        }

        static GameObject Raiders()
        {
            var r = Root("Summon_Raiders");
            var s = Soldier(r, Vector3.zero, "#3a2a24", "#6a2a20", 1.15f);
            Sph(s.transform, new Vector3(-0.3f, 0.65f, -0.15f), new Vector3(0.4f, 0.45f, 0.4f), Mat("#a08050"));
            Sph(s.transform, new Vector3(-0.3f, 0.92f, -0.15f), Vector3.one * 0.12f, Glow("#ffd040"));
            var axe = new GameObject("Axe").transform;
            axe.SetParent(s.transform, false);
            axe.localPosition = new Vector3(0.28f, 0.5f, 0.12f);
            Cube(axe, new Vector3(0, 0.25f, 0), new Vector3(0.05f, 0.6f, 0.05f), Mat("#5a3a20"));
            Cube(axe, new Vector3(0.08f, 0.5f, 0), new Vector3(0.16f, 0.2f, 0.03f), Mat("#c0c4c8", 0.8f, 0.8f));
            Cube(s.transform, new Vector3(0, 0.98f, 0.12f), new Vector3(0.3f, 0.07f, 0.06f), Mat("#202020"));
            return r.gameObject;
        }

        static GameObject Watchtower()
        {
            var r = Root("Summon_Watchtower");
            var wood = Mat("#7a5230");
            foreach (var x in new[] { -0.35f, 0.35f })
                foreach (var z in new[] { -0.35f, 0.35f })
                    Cube(r, new Vector3(x, 0.8f, z), new Vector3(0.09f, 1.6f, 0.09f), wood);
            Cube(r, new Vector3(0, 0.5f, -0.35f), new Vector3(0.75f, 0.05f, 0.05f), wood, new Vector3(0, 0, 40));
            Cube(r, new Vector3(0, 0.5f, 0.35f), new Vector3(0.75f, 0.05f, 0.05f), wood, new Vector3(0, 0, -40));
            Cube(r, new Vector3(0, 1.6f, 0), new Vector3(0.95f, 0.08f, 0.95f), Mat("#8a6034"));
            foreach (var z in new[] { -0.45f, 0.45f }) Cube(r, new Vector3(0, 1.75f, z), new Vector3(0.95f, 0.22f, 0.05f), Mat("#6a4a2a"));
            Cone(r, new Vector3(0, 2.35f, 0), new Vector3(1.2f, 0.6f, 1.2f), Mat("#5a6a8a"));
            foreach (var x in new[] { -0.4f, 0.4f })
                foreach (var z in new[] { -0.4f, 0.4f })
                    Cube(r, new Vector3(x, 2.05f, z), new Vector3(0.05f, 0.6f, 0.05f), wood);
            Soldier(r, new Vector3(0, 1.64f, 0), "#4a5a7a", "#909098", 0.6f);
            Sph(r, new Vector3(0.3f, 1.95f, -0.3f), Vector3.one * 0.1f, Glow("#ffc040"));
            return r.gameObject;
        }

        // ---------------- 성과 배경 ----------------

        /// <summary>성. 팀 색은 지붕과 깃발에 쓰인다.</summary>
        public static GameObject Castle(string name, Color team)
        {
            var r = Root(name);
            var stone = Mat("#a7a39a");
            var stoneDark = Mat("#8a867e");
            var roof = Mat(team, 0.35f);
            var gate = Mat("#3a2a1c");

            // 성벽 (사각형)
            float w = 3.6f, d = 2.6f, h = 1.3f;
            Cube(r, new Vector3(0, h / 2, -d / 2), new Vector3(w, h, 0.35f), stone);
            Cube(r, new Vector3(0, h / 2, d / 2), new Vector3(w, h, 0.35f), stone);
            Cube(r, new Vector3(-w / 2, h / 2, 0), new Vector3(0.35f, h, d), stone);
            Cube(r, new Vector3(w / 2, h / 2, 0), new Vector3(0.35f, h, d), stone);
            // 흉벽
            for (int i = 0; i < 9; i++)
            {
                float x = -w / 2 + 0.2f + i * (w - 0.4f) / 8f;
                Cube(r, new Vector3(x, h + 0.12f, -d / 2), new Vector3(0.2f, 0.25f, 0.36f), stoneDark);
                Cube(r, new Vector3(x, h + 0.12f, d / 2), new Vector3(0.2f, 0.25f, 0.36f), stoneDark);
            }
            // 성문
            Cube(r, new Vector3(0, 0.45f, -d / 2 - 0.02f), new Vector3(0.8f, 0.9f, 0.36f), gate);
            Cyl(r, new Vector3(0, 0.9f, -d / 2 - 0.02f), new Vector3(0.8f, 0.18f, 0.8f), gate, new Vector3(90, 0, 0));
            // 본성 (킵)
            Cube(r, new Vector3(0, 1.3f, 0.3f), new Vector3(1.6f, 2.6f, 1.3f), stone);
            for (int i = 0; i < 4; i++)
                Cube(r, new Vector3(-0.6f + i * 0.4f, 2.72f, -0.36f), new Vector3(0.22f, 0.25f, 0.2f), stoneDark);
            Roof(r, new Vector3(0, 2.6f, 0.3f), new Vector3(1.7f, 0.9f, 1.4f), roof, new Vector3(0, 0, 0));
            Cube(r, new Vector3(0, 1.8f, -0.36f), new Vector3(0.25f, 0.4f, 0.02f), Glow("#ffd890"));
            // 네 모서리 탑
            foreach (var x in new[] { -w / 2, w / 2 })
                foreach (var z in new[] { -d / 2, d / 2 })
                {
                    Cyl(r, new Vector3(x, 1.0f, z), new Vector3(0.8f, 1.0f, 0.8f), stone);
                    Cyl(r, new Vector3(x, 2.05f, z), new Vector3(0.95f, 0.08f, 0.95f), stoneDark);
                    Cone(r, new Vector3(x, 2.1f, z), new Vector3(1.0f, 1.0f, 1.0f), roof);
                }
            // 깃발
            Cyl(r, new Vector3(0, 3.8f, 0.3f), new Vector3(0.04f, 0.6f, 0.04f), Mat("#5a4028"));
            var flag = Cube(r, new Vector3(0.28f, 4.2f, 0.3f), new Vector3(0.55f, 0.32f, 0.02f), Mat(team, 0.2f));
            flag.name = "Flag";
            flag.AddComponent<Swinger>().amplitude = 8f;
            return r.gameObject;
        }

        public static GameObject Tree(Vector3 pos, float s, int seed)
        {
            var r = Root("Tree");
            r.position = pos;
            r.localScale = Vector3.one * s;
            Cyl(r, new Vector3(0, 0.4f, 0), new Vector3(0.18f, 0.4f, 0.18f), Mat("#5a3e24"));
            string[] greens = { "#2f6b2f", "#3a7a34", "#2a5e30", "#447f3a" };
            var g = Mat(greens[seed % greens.Length]);
            if (seed % 3 == 0)
            {
                Cone(r, new Vector3(0, 0.6f, 0), new Vector3(1.0f, 1.2f, 1.0f), g);
                Cone(r, new Vector3(0, 1.2f, 0), new Vector3(0.75f, 1.0f, 0.75f), g);
            }
            else
            {
                Sph(r, new Vector3(0, 1.2f, 0), new Vector3(1.1f, 1.0f, 1.1f), g);
                Sph(r, new Vector3(0.3f, 1.0f, 0.2f), new Vector3(0.7f, 0.6f, 0.7f), g);
            }
            return r.gameObject;
        }

        public static GameObject Rock(Vector3 pos, float s)
        {
            var r = Root("Rock");
            r.position = pos;
            Sph(r, Vector3.zero, new Vector3(1.0f, 0.6f, 0.8f) * s, Mat("#7d7a72"));
            return r.gameObject;
        }
    }
}
