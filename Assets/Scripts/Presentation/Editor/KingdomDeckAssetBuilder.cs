using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomDeck.Engine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KingdomDeck.Presentation.EditorTools
{
    /// <summary>
    /// 메뉴: 왕국의 덱 → 연출 데이터 생성 / 씬 만들기.
    /// 카드별 CardVisual(ScriptableObject)과 임시 소환 프리팹을 만든다. 이미 있는 에셋은 덮어쓰지 않고 비어 있는 칸만 채운다.
    /// </summary>
    public static class KingdomDeckAssetBuilder
    {
        const string CardsJson = "Assets/Data/cards.json";
        const string VisualDir = "Assets/Data/CardVisuals";
        const string PrefabDir = "Assets/Prefabs/Summons";
        const string MatDir = "Assets/Materials/KingdomDeck";
        const string LibraryPath = "Assets/Data/CardVisuals/CardVisualLibrary.asset";
        const string ScenePath = "Assets/Scenes/KingdomDeck.unity";

        [MenuItem("왕국의 덱/연출 데이터 생성")]
        public static void BuildVisuals()
        {
            var db = CardDatabase.FromJson(File.ReadAllText(CardsJson));
            EnsureDir(VisualDir);
            EnsureDir(PrefabDir);
            EnsureDir(MatDir);

            var library = AssetDatabase.LoadAssetAtPath<CardVisualLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<CardVisualLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var savedMats = new Dictionary<Material, Material>();
            foreach (var card in db.All)
            {
                string vPath = VisualDir + "/" + card.Id + ".asset";
                var vis = AssetDatabase.LoadAssetAtPath<CardVisual>(vPath);
                if (vis == null)
                {
                    vis = ScriptableObject.CreateInstance<CardVisual>();
                    vis.cardId = card.Id;
                    CardVisualDefaults.Apply(vis);
                    AssetDatabase.CreateAsset(vis, vPath);
                }

                string pPath = PrefabDir + "/Summon_" + card.Id + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                if (prefab == null)
                {
                    var go = SummonFactory.Build(card.Id);
                    go.name = "Summon_" + card.Id;
                    PersistMaterials(go, savedMats);
                    PersistMeshes(go);
                    prefab = PrefabUtility.SaveAsPrefabAsset(go, pPath);
                    Object.DestroyImmediate(go);
                }
                if (vis.summonPrefab == null)
                {
                    vis.summonPrefab = prefab;
                    EditorUtility.SetDirty(vis);
                }
                if (!library.visuals.Contains(vis)) library.visuals.Add(vis);
            }
            library.visuals.RemoveAll(v => v == null);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("왕국의 덱: 카드 연출 " + library.visuals.Count + "개, 소환 프리팹 생성 완료");
        }

        /// <summary>
        /// 모든 소환 프리팹을 SummonFactory 코드로 다시 만들어 같은 경로에 덮어쓴다 (GUID 유지 → CardVisual 연결 그대로).
        /// 임시 모델 코드를 고친 뒤 쓴다. 직접 손본 프리팹이 있으면 덮어써지니 주의.
        /// </summary>
        [MenuItem("왕국의 덱/소환 프리팹 다시 만들기")]
        public static void RebuildSummonPrefabs()
        {
            var db = CardDatabase.FromJson(File.ReadAllText(CardsJson));
            EnsureDir(PrefabDir);
            EnsureDir(MatDir);
            var savedMats = new Dictionary<Material, Material>();
            int n = 0;
            foreach (var card in db.All)
            {
                string pPath = PrefabDir + "/Summon_" + card.Id + ".prefab";
                var go = SummonFactory.Build(card.Id);
                go.name = "Summon_" + card.Id;
                PersistMaterials(go, savedMats);
                PersistMeshes(go);
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, pPath);
                Object.DestroyImmediate(go);
                var vis = AssetDatabase.LoadAssetAtPath<CardVisual>(VisualDir + "/" + card.Id + ".asset");
                if (vis != null && vis.summonPrefab == null) { vis.summonPrefab = prefab; EditorUtility.SetDirty(vis); }
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("왕국의 덱: 소환 프리팹 " + n + "개 다시 만듦");
        }

        static void PersistMaterials(GameObject go, Dictionary<Material, Material> saved)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || AssetDatabase.Contains(m)) continue;
                    if (!saved.TryGetValue(m, out var asset))
                    {
                        string path = MatDir + "/" + m.name.Replace("#", "") + ".mat";
                        asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (asset == null)
                        {
                            asset = new Material(m);
                            AssetDatabase.CreateAsset(asset, path);
                        }
                        saved[m] = asset;
                    }
                    mats[i] = asset;
                }
                r.sharedMaterials = mats;
            }
        }

        static void PersistMeshes(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || AssetDatabase.Contains(mesh)) continue;
                string path = MatDir + "/" + mesh.name + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (asset == null)
                {
                    asset = Object.Instantiate(mesh);
                    AssetDatabase.CreateAsset(asset, path);
                }
                mf.sharedMesh = asset;
            }
        }

        const string FontRegularSrc = "Assets/Fonts/NotoSansKR-Regular.otf";
        const string FontBoldSrc = "Assets/Fonts/NotoSansKR-Bold.otf";
        const string FontRegularAsset = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
        const string FontBoldAsset = "Assets/Fonts/NotoSansKR-Bold SDF.asset";

        /// <summary>Noto Sans KR로 TextMeshPro 동적 글꼴 에셋을 만들고, 열린 씬의 게임 컴포넌트에 연결한다.</summary>
        [MenuItem("왕국의 덱/한글 폰트 에셋 만들기")]
        public static void BuildFonts()
        {
            var regular = MakeFontAsset(FontRegularSrc, FontRegularAsset);
            var bold = MakeFontAsset(FontBoldSrc, FontBoldAsset);
            // TMP 기본 글꼴에 한글이 없을 때 대신 쓰도록 등록
            var settings = TMPro.TMP_Settings.instance;
            if (settings != null && TMPro.TMP_Settings.fallbackFontAssets != null && regular != null && !TMPro.TMP_Settings.fallbackFontAssets.Contains(regular))
            {
                TMPro.TMP_Settings.fallbackFontAssets.Add(regular);
                EditorUtility.SetDirty(settings);
            }
            var game = Object.FindFirstObjectByType<KingdomDeckGame>();
            if (game != null)
            {
                var so = new SerializedObject(game);
                so.FindProperty("fontRegular").objectReferenceValue = regular;
                so.FindProperty("fontBold").objectReferenceValue = bold;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
                EditorSceneManager.SaveScene(game.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("왕국의 덱: 한글 폰트 에셋 준비 완료" + (game != null ? " (씬에 연결함)" : ""));
        }

        static TMPro.TMP_FontAsset MakeFontAsset(string src, string dst)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(dst);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(src);
            if (font == null) { Debug.LogError("폰트 파일이 없습니다: " + src); return null; }
            var fa = TMPro.TMP_FontAsset.CreateFontAsset(font, 72, 14, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                TMPro.AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(dst);
            AssetDatabase.CreateAsset(fa, dst);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        const string ArtDir = "Assets/Data/CardVisuals/Art";

        /// <summary>
        /// 카드 그림을 소환 모델로 찍어 PNG로 저장하고 CardVisual.art에 연결한다.
        /// 게임 시작 때마다 찍지 않아도 되어 모바일에서 빨라진다. 이미 그림이 있는 카드도 다시 찍는다.
        /// (직접 그린 그림으로 바꾸려면 CardVisual.art에 그 텍스처를 넣으면 된다)
        /// </summary>
        [MenuItem("왕국의 덱/카드 그림 굽기")]
        public static void BakeCardArt()
        {
            EnsureDir(ArtDir);
            var library = AssetDatabase.LoadAssetAtPath<CardVisualLibrary>(LibraryPath);
            if (library == null) { BuildVisuals(); library = AssetDatabase.LoadAssetAtPath<CardVisualLibrary>(LibraryPath); }
            int n = 0;
            foreach (var vis in library.visuals)
            {
                if (vis == null) continue;
                string path = ArtDir + "/" + vis.cardId + ".png";
                // 직접 넣은 그림(다른 경로)은 건드리지 않는다
                if (vis.art != null && AssetDatabase.GetAssetPath(vis.art) != path) continue;
                var tex = ArtStudio.RenderArt(vis.cardId, vis);
                if (tex == null) continue;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
                vis.art = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                EditorUtility.SetDirty(vis);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("왕국의 덱: 카드 그림 " + n + "장 구움 → " + ArtDir);
        }

        [MenuItem("왕국의 덱/씬 만들기")]
        public static void BuildScene()
        {
            if (AssetDatabase.LoadAssetAtPath<CardVisualLibrary>(LibraryPath) == null) BuildVisuals();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("KingdomDeckGame");
            var game = go.AddComponent<KingdomDeckGame>();
            var so = new SerializedObject(game);
            so.FindProperty("cardsJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(CardsJson);
            so.FindProperty("library").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CardVisualLibrary>(LibraryPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            Debug.Log("왕국의 덱: 씬 생성 완료 → " + ScenePath);
        }

        static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
