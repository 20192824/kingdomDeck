using System.Collections.Generic;
using UnityEngine;

namespace KingdomDeck.Presentation
{
    /// <summary>
    /// 카드 그림이 없을 때, 소환물 모델을 멀리 떨어진 촬영장에서 찍어 카드 그림으로 쓴다.
    /// </summary>
    public static class ArtStudio
    {
        static readonly Dictionary<string, Texture> cache = new Dictionary<string, Texture>();

        public static Texture Get(string cardId, CardVisualLibrary lib)
        {
            if (cache.TryGetValue(cardId, out var t) && t != null) return t;
            var vis = lib != null ? lib.Get(cardId) : null;
            if (vis != null && vis.art != null) { cache[cardId] = vis.art; return vis.art; }
            t = Render(cardId, vis);
            cache[cardId] = t;
            return t;
        }

        /// <summary>카드 그림을 새로 찍는다 (에디터에서 PNG로 미리 구울 때 사용)</summary>
        public static Texture2D RenderArt(string cardId, CardVisual vis) => Render(cardId, vis);

        static Texture2D Render(string cardId, CardVisual vis)
        {
            const int w = 256, h = 160;
            Vector3 origin = new Vector3(5000, 0, 5000);

            var stage = new GameObject("ArtStage");
            stage.transform.position = origin;
            GameObject model = vis != null && vis.summonPrefab != null ? Object.Instantiate(vis.summonPrefab) : SummonFactory.Build(cardId);
            model.transform.SetParent(stage.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0, cardId == "dragon" ? 210 : 200, 0);
            foreach (var b in model.GetComponentsInChildren<MonoBehaviour>()) if (b != null) b.enabled = false;

            // 모델 크기에 맞춰 카메라 거리 조정
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float size = Mathf.Max(bounds.size.x, bounds.size.y * 1.4f, bounds.size.z, 0.5f);

            var camGo = new GameObject("ArtCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = vis != null ? vis.artColor : Color.gray;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            Vector3 dir = new Vector3(0, 0.45f, -1f).normalized;
            cam.transform.position = bounds.center + dir * size * 2.1f;
            cam.transform.LookAt(bounds.center);

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            Texture2D tex = null;
            try
            {
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Art_" + cardId, wrapMode = TextureWrapMode.Clamp };
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("카드 그림 촬영 실패 (" + cardId + "): " + e.Message);
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(stage);
            }
            return tex;
        }
    }
}
