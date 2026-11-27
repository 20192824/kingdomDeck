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
