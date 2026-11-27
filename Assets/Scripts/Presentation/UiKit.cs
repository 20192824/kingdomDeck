using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    /// <summary>연출 시간 관리: 속도 배율과 건너뛰기를 모든 애니메이션이 함께 따른다.</summary>
    public static class Anim
    {
        public static float Speed = 1f;
        /// <summary>true면 진행 중인 애니메이션이 즉시 끝난다</summary>
        public static bool Skipping;

        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null)
        {
            float t = 0f;
            float d = Mathf.Max(0.0001f, duration);
            while (t < d && !Skipping)
            {
                float k = t / d;
                step(ease != null ? ease(k) : k);
                yield return null;
                t += Time.deltaTime * Speed;
            }
            step(1f);
        }

        public static IEnumerator Wait(float duration)
        {
            float t = 0f;
            while (t < duration && !Skipping)
            {
                yield return null;
                t += Time.deltaTime * Speed;
            }
        }

        public static float OutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);
        public static float InCubic(float k) => k * k * k;
        public static float InOutCubic(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
        public static float OutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        }
    }

    /// <summary>UI 색상표</summary>
    public static class Palette
    {
        public static readonly Color Bg = Hex("#1b1712");
        public static readonly Color Panel = Hex("#2a231b", 0.94f);
        public static readonly Color PanelDark = Hex("#17130e", 0.92f);
        public static readonly Color PanelLine = Hex("#6b5634");
        public static readonly Color Gold = Hex("#e8c25a");
        public static readonly Color GoldDim = Hex("#8a7337");
        public static readonly Color Text = Hex("#f3e7cf");
        public static readonly Color TextDim = Hex("#a8977a");
        public static readonly Color Player = Hex("#4f8fe6");
        public static readonly Color Enemy = Hex("#e0533f");
        public static readonly Color Good = Hex("#5fd07a");
        public static readonly Color Bad = Hex("#ff5a4a");
        public static readonly Color Button = Hex("#4a3b26");
        public static readonly Color ButtonHot = Hex("#7a5e2e");
        public static readonly Color Select = Hex("#4fd1ff");

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }
    }

    /// <summary>코드로 UI를 만드는 도구들 (모서리 둥근 스프라이트, 텍스트, 버튼 등)</summary>
    public static class UiKit
    {
        // ---------------- 글꼴 (TextMeshPro + 한글 Noto Sans KR) ----------------

        /// <summary>KingdomDeckGame이 시작할 때 넣어 준다. 비어 있으면 TMP 기본 글꼴(한글 없음)</summary>
        public static TMP_FontAsset FontRegular, FontBold;

        static TMP_FontAsset Regular => FontRegular != null ? FontRegular : TMP_Settings.defaultFontAsset;
        static TMP_FontAsset Bold => FontBold != null ? FontBold : Regular;

        public enum TextFx { None, Outline, Shadow }

        // 글꼴 × 효과별 공유 재질 (글자마다 재질을 만들면 묶어 그리기가 깨진다)
        static readonly Dictionary<string, Material> textMats = new Dictionary<string, Material>();

        public static Material TextMaterial(TMP_FontAsset font, TextFx fx)
        {
            if (font == null || fx == TextFx.None) return font != null ? font.material : null;
            string key = font.GetInstanceID() + ":" + fx;
            if (textMats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(font.material) { name = font.name + "_" + fx };
            if (fx == TextFx.Outline)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Outline);
                m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
                m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0, 0, 0, 0.95f));
                m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
            }
            else
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, 0.75f));
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
            }
            textMats[key] = m;
            return m;
        }

        static Sprite rounded, roundedSmall, circle, glow, soft;

        /// <summary>9-slice 둥근 사각형</summary>
        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 18));
        public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : (roundedSmall = MakeRounded(32, 7));
        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(64, false));
        public static Sprite Glow => glow != null ? glow : (glow = MakeRoundedGlow(96, 30));
        public static Sprite Soft => soft != null ? soft : (soft = MakeCircle(64, true));

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeRoundedGlow(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float inner = radius * 0.35f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Mathf.Clamp01((d - inner) / (radius - inner));
                    a = a * a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Sprite MakeCircle(int size, bool softEdge)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = softEdge ? Mathf.Pow(Mathf.Clamp01(1f - d / r), 1.6f) : Mathf.Clamp01(r - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>위→아래 그라데이션 텍스처 (카드 그림 배경 등)</summary>
        public static Texture2D Gradient(Color top, Color bottom, int h = 64)
        {
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++) tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (h - 1f)));
            tex.Apply();
            return tex;
        }

        // ---------------- 생성 도우미 ----------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        /// <summary>앵커 한 점 + 크기로 배치</summary>
        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float pad = 0f)
            => rt.Place(Vector2.zero, Vector2.one, new Vector2(pad, pad), new Vector2(-pad, -pad));

        public static Image Image(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sliced && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            return img;
        }

        public static TMP_Text Text(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            bool bold = style == FontStyle.Bold || style == FontStyle.BoldAndItalic;
            t.font = bold ? Bold : Regular;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = Align(align);
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.extraPadding = true;
            return t;
        }

        static TextAlignmentOptions Align(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        /// <summary>글자: 그림자 재질 / 그림: Shadow 컴포넌트</summary>
        public static T Shadowed<T>(this T graphic, float dist = 2f, float alpha = 0.75f) where T : Graphic
        {
            if (graphic is TMP_Text tmp) { tmp.fontSharedMaterial = TextMaterial(tmp.font, TextFx.Shadow); return graphic; }
            var s = graphic.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, alpha);
            s.effectDistance = new Vector2(dist, -dist);
            return graphic;
        }

        /// <summary>글자: 검은 외곽선 재질 / 그림: Outline 컴포넌트</summary>
        public static T Outlined<T>(this T graphic, Color color, float dist = 1.5f) where T : Graphic
        {
            if (graphic is TMP_Text tmp) { tmp.fontSharedMaterial = TextMaterial(tmp.font, TextFx.Outline); return graphic; }
            var o = graphic.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dist, -dist);
            return graphic;
        }

        public sealed class UiButton
        {
            public Button Button;
            public Image Bg;
            public TMP_Text Label;
            public RectTransform Rt => (RectTransform)Button.transform;
            public bool Interactable
            {
                get => Button.interactable;
                set
                {
                    Button.interactable = value;
                    Label.color = value ? Palette.Text : Palette.TextDim;
                }
            }
            public void SetActive(bool on) => Button.gameObject.SetActive(on);
        }

        public static UiButton Button(string name, Transform parent, string label, int fontSize, Action onClick, Color? color = null)
        {
            var img = Image(name, parent, color ?? Palette.Button, Rounded);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            colors.colorMultiplier = 1.4f;
            btn.colors = colors;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b, 0.5f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var txt = Text("Label", img.transform, label, fontSize, Palette.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            txt.rectTransform.Fill(4);
            txt.Shadowed(1.5f);
            if (onClick != null) btn.onClick.AddListener(() => { Sfx.Play(Sfx.Click, 0.5f); onClick(); });
            return new UiButton { Button = btn, Bg = img, Label = txt };
        }

        /// <summary>패널: 둥근 배경 + 금색 테두리</summary>
        public static Image Panel(string name, Transform parent, Color? color = null)
        {
            var img = Image(name, parent, color ?? Palette.Panel, Rounded);
            img.Outlined(new Color(Palette.PanelLine.r, Palette.PanelLine.g, Palette.PanelLine.b, 0.9f), 2f);
            return img;
        }
    }
}
