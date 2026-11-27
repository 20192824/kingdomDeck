using System;
using KingdomDeck.Engine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    public enum CardHighlight { None, Playable, Buyable, Selected, Danger }

    /// <summary>UI 카드 한 장. 코드로 만든다. 마우스를 올리면 확대된다.</summary>
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public const float W = 150f, H = 214f;

        public string CardId { get; private set; }
        public int HandIndex = -1;
        public bool HoverZoom = true;
        public float HoverScale = 1.55f;
        public float HoverLift = 90f;

        public event Action<CardView> Clicked;
        public event Action<CardView, bool> Hovered;

        RectTransform rt;
        Image glow, dim, frame;
        Text nameText;
        GameObject backFace;
        CanvasGroup group;
        bool hovered;
        float hoverT;
        Vector2 basePos;
        float baseScale = 1f;
        float baseRot;
        bool interactable = true;
        int savedSibling = -1;

        public RectTransform Rt => rt;
        public Vector2 BasePos => basePos;

        public static CardView Create(Transform parent, CardDef def, CardVisual vis, Texture art, float scale = 1f)
        {
            var root = UiKit.Rect("Card_" + (def != null ? def.Id : "back"), parent);
            root.sizeDelta = new Vector2(W, H);
            var cv = root.gameObject.AddComponent<CardView>();
            cv.rt = root;
            cv.group = root.gameObject.AddComponent<CanvasGroup>();
            cv.Build(def, vis, art);
            cv.SetBase(Vector2.zero, scale, 0f, true);
            return cv;
        }

        void Build(CardDef def, CardVisual vis, Texture art)
        {
            CardId = def?.Id;
            glow = UiKit.Image("Glow", rt, new Color(1, 1, 1, 0), UiKit.Glow);
            glow.rectTransform.Fill(-26);
            glow.raycastTarget = false;

            Color frameCol = vis != null ? vis.frameColor : Palette.GoldDim;
            frame = UiKit.Image("Frame", rt, frameCol, UiKit.Rounded);
            frame.rectTransform.Fill();
            frame.Outlined(new Color(0, 0, 0, 0.6f), 2f);
            frame.raycastTarget = true;

            if (def == null)
            {
                BuildBack();
                return;
            }

            var parchment = UiKit.Image("Parchment", rt, Palette.Hex("#efe2c2"), UiKit.Rounded);
            parchment.rectTransform.Fill(6);
            parchment.raycastTarget = false;

            // 이름 띠
            var band = UiKit.Image("NameBand", rt, Color.Lerp(frameCol, Color.black, 0.25f), UiKit.RoundedSmall);
            band.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -36), new Vector2(-8, -8));
            band.raycastTarget = false;
            nameText = UiKit.Text("Name", band.transform, def.Name, def.Name.Length > 4 ? 15 : 18, Palette.Hex("#fff6e0"), TextAnchor.MiddleCenter, FontStyle.Bold);
            nameText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(26, 0), new Vector2(-4, 0));
            nameText.Shadowed(1.2f);

            // 그림
            var artBg = UiKit.Image("ArtBg", rt, vis != null ? vis.artColor : Color.gray, UiKit.RoundedSmall);
            artBg.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -122), new Vector2(-10, -40));
            artBg.raycastTarget = false;
            if (art != null)
            {
                var raw = UiKit.Rect("Art", artBg.transform).gameObject.AddComponent<RawImage>();
                raw.texture = art;
                raw.rectTransform.Fill(2);
                raw.raycastTarget = false;
                raw.uvRect = new Rect(0, 0.1f, 1, 0.8f);
            }

            // 종류
            var typeText = UiKit.Text("Type", rt, def.TypeName, 12, Palette.Hex("#5a4630"), TextAnchor.MiddleCenter, FontStyle.Bold);
            typeText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -140), new Vector2(-8, -122));

            // 효과 글
            string body = def.Text;
            if (def.Is(CardType.Treasure)) body = "";
            if (def.Is(CardType.Siege)) body = "구매하는 순간\n상대 성을 공격!";
            if (def.Is(CardType.Traitor)) body = "덱에 있는 동안\n성벽을 무너뜨린다";
            var bodyText = UiKit.Text("Body", rt, body, body.Length > 30 ? 11 : 13, Palette.Hex("#3a2a1a"), TextAnchor.MiddleCenter);
            float bottom = def.Is(CardType.Siege) || def.Is(CardType.Traitor) ? 40 : 8;
            bodyText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, bottom), new Vector2(-10, -140));
            bodyText.lineSpacing = 0.95f;

            if (def.Is(CardType.Treasure))
            {
                var coinBig = UiKit.Image("CoinBig", rt, Palette.Hex("#f2c744"), UiKit.Circle);
                coinBig.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(54, 54));
                coinBig.Outlined(Palette.Hex("#7a5a10"), 2f);
                var v = UiKit.Text("Value", coinBig.transform, "+" + def.Coins, 24, Palette.Hex("#4a3008"), TextAnchor.MiddleCenter, FontStyle.Bold);
                v.rectTransform.Fill();
            }
            if (def.Is(CardType.Siege))
            {
                var badge = UiKit.Image("Power", rt, Palette.Hex("#8c1c1c"), UiKit.RoundedSmall);
                badge.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(96, 28));
                var v = UiKit.Text("Value", badge.transform, "공격력 " + def.Power, 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                v.rectTransform.Fill();
            }
            if (def.Is(CardType.Traitor))
            {
                var badge = UiKit.Image("Self", rt, Palette.Hex("#3a1a4a"), UiKit.RoundedSmall);
                badge.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(96, 28));
                var v = UiKit.Text("Value", badge.transform, "내 성 -" + def.SelfDamage, 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                v.rectTransform.Fill();
            }

            // 비용 동전
            var coin = UiKit.Image("Cost", rt, Palette.Hex("#f2c744"), UiKit.Circle);
            coin.rectTransform.At(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(18, -20), new Vector2(36, 36));
            coin.Outlined(Palette.Hex("#6a4a08"), 2f);
            coin.raycastTarget = false;
            var cost = UiKit.Text("Value", coin.transform, def.Cost.ToString(), 20, Palette.Hex("#3a2404"), TextAnchor.MiddleCenter, FontStyle.Bold);
            cost.rectTransform.Fill();

            dim = UiKit.Image("Dim", rt, new Color(0, 0, 0, 0), UiKit.Rounded);
            dim.rectTransform.Fill();
            dim.raycastTarget = false;
        }

        void BuildBack()
        {
            backFace = frame.gameObject;
            frame.color = Palette.Hex("#3a2a4a");
            var inner = UiKit.Image("Inner", rt, Palette.Hex("#5a3a6a"), UiKit.Rounded);
            inner.rectTransform.Fill(9);
            inner.raycastTarget = false;
            var emblem = UiKit.Image("Emblem", inner.transform, Palette.Gold, UiKit.Circle);
            emblem.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            emblem.raycastTarget = false;
            var crown = UiKit.Text("Crown", emblem.transform, "왕", 34, Palette.Hex("#3a2a4a"), TextAnchor.MiddleCenter, FontStyle.Bold);
            crown.rectTransform.Fill();
            dim = UiKit.Image("Dim", rt, new Color(0, 0, 0, 0), UiKit.Rounded);
            dim.rectTransform.Fill();
            dim.raycastTarget = false;
            HoverZoom = false;
        }

        // ---------------- 상태 ----------------

        public void SetHighlight(CardHighlight h)
        {
            Color c;
            switch (h)
            {
                case CardHighlight.Playable: c = new Color(0.4f, 1f, 0.5f, 0.85f); break;
                case CardHighlight.Buyable: c = new Color(1f, 0.82f, 0.3f, 0.9f); break;
                case CardHighlight.Selected: c = new Color(0.3f, 0.85f, 1f, 1f); break;
                case CardHighlight.Danger: c = new Color(1f, 0.3f, 0.25f, 0.95f); break;
                default: c = new Color(1, 1, 1, 0); break;
            }
            glow.color = c;
        }

        public void SetDim(bool on)
        {
            if (dim != null) dim.color = new Color(0, 0, 0, on ? 0.45f : 0f);
        }

        public void SetInteractable(bool on)
        {
            interactable = on;
            frame.raycastTarget = on;
        }

        public float Alpha { get => group.alpha; set => group.alpha = value; }

        /// <summary>손패 배치 위치 (호버 확대는 이 위치를 기준으로)</summary>
        public void SetBase(Vector2 pos, float scale, float rotZ, bool snap)
        {
            basePos = pos;
            baseScale = scale;
            baseRot = rotZ;
            if (snap) Apply(true);
        }

        public float Selected { get; set; }

        void Update()
        {
            float target = hovered && HoverZoom && interactable ? 1f : 0f;
            hoverT = Mathf.MoveTowards(hoverT, target, Time.unscaledDeltaTime * 8f);
            Apply();
        }

        Vector2 curPos;
        float curScale = 1f, curRot, curSel;

        void Apply(bool snap = false)
        {
            if (rt == null) return;
            float follow = snap ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 13f * Mathf.Max(1f, Anim.Speed * 0.75f));
            curPos = Vector2.Lerp(curPos, basePos, follow);
            curScale = Mathf.Lerp(curScale, baseScale, follow);
            curRot = Mathf.Lerp(curRot, baseRot, follow);
            curSel = Mathf.Lerp(curSel, Selected, snap ? 1f : 0.3f);
            float k = Anim.OutCubic(hoverT);
            float lift = HoverLift * k + curSel * 34f;
            rt.anchoredPosition = curPos + new Vector2(0, lift);
            float s = curScale * Mathf.Lerp(1f, HoverScale, k);
            rt.localScale = new Vector3(s, s, 1f);
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(curRot, 0f, k));
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            if (HoverZoom) { savedSibling = transform.GetSiblingIndex(); transform.SetAsLastSibling(); }
            Hovered?.Invoke(this, true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovered = false;
            if (savedSibling >= 0 && transform.parent != null) transform.SetSiblingIndex(Mathf.Min(savedSibling, transform.parent.childCount - 1));
            savedSibling = -1;
            Hovered?.Invoke(this, false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable || e.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke(this);
        }

        /// <summary>애니메이션용으로 위치를 직접 움직일 때 (Update가 덮어쓰지 않도록)</summary>
        public void Detach()
        {
            enabled = false;
            HoverZoom = false;
            SetInteractable(false);
        }
    }
}
