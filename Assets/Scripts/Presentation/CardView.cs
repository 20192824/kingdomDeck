using System;
using KingdomDeck.Engine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    public enum CardHighlight { None, Playable, Buyable, Selected, Danger }

    /// <summary>UI 카드 한 장. 코드로 만든다. 마우스를 올리면 확대된다.</summary>
    /// <summary>
    /// 길게 누르기 판정. 0.4초 이상 누르고 있고 끌기(스크롤)가 시작되지 않았으면 길게 누른 것.
    /// </summary>
    public sealed class LongPressTracker
    {
        public float Delay = 0.4f;
        PointerEventData data;
        float downTime;
        bool pressing, fired, suppressClick;

        public void Down(PointerEventData e)
        {
            data = e;
            downTime = Time.unscaledTime;
            pressing = true;
            fired = false;
        }

        /// <summary>손을 뗌. 길게 누르기가 진행 중이었으면 true</summary>
        public bool Up()
        {
            bool wasFired = pressing && fired;
            pressing = false;
            suppressClick = wasFired;
            return wasFired;
        }

        /// <summary>매 프레임 호출. 이번 프레임에 길게 누르기가 시작되면 true</summary>
        public bool Tick()
        {
            if (!pressing || fired) return false;
            if (data != null && data.dragging) { pressing = false; return false; }
            if (Time.unscaledTime - downTime < Delay) return false;
            fired = true;
            return true;
        }

        public bool ConsumeClick()
        {
            bool s = suppressClick;
            suppressClick = false;
            return s;
        }
    }

    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public const float W = 150f, H = 214f;

        public string CardId { get; private set; }
        public int HandIndex = -1;
        public bool HoverZoom = true;
        public float HoverScale = 1.55f;
        public float HoverLift = 90f;

        public event Action<CardView> Clicked;
        public event Action<CardView, bool> Hovered;

        bool fullText;
        RectTransform rt;
        /// <summary>보이는 카드 그림 전체. 마우스를 올리면 이것만 위로 올라가고 커진다.</summary>
        RectTransform body;
        /// <summary>마우스/터치 감지 영역. 제자리에 있고, 카드가 올라가 있는 동안은 위로 늘어난다 (깜빡임 방지).</summary>
        Image hit;
        Image glow, dim, frame;
        TMP_Text nameText;
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

        /// <param name="fullText">true면 효과 전체 문장(크게 보기용), false면 짧은 요약(손패 등)</param>
        public static CardView Create(Transform parent, CardDef def, CardVisual vis, Texture art, float scale = 1f, bool fullText = false)
        {
            var root = UiKit.Rect("Card_" + (def != null ? def.Id : "back"), parent);
            root.sizeDelta = new Vector2(W, H);
            var cv = root.gameObject.AddComponent<CardView>();
            cv.rt = root;
            cv.group = root.gameObject.AddComponent<CanvasGroup>();
            cv.hit = UiKit.Image("Hit", root, new Color(0, 0, 0, 0));
            cv.hit.rectTransform.Fill();
            cv.hit.raycastTarget = true;
            cv.body = UiKit.Rect("Visual", root).Fill();
            cv.fullText = fullText;
            cv.Build(def, vis, art);
            if (Application.isMobilePlatform) cv.HoverZoom = false; // 터치에는 마우스 올리기가 없다 (길게 누르기로 확대)
            cv.SetBase(Vector2.zero, scale, 0f, true);
            return cv;
        }

        void Build(CardDef def, CardVisual vis, Texture art)
        {
            CardId = def?.Id;
            glow = UiKit.Image("Glow", body, new Color(1, 1, 1, 0), UiKit.Glow);
            glow.rectTransform.Fill(-26);
            glow.raycastTarget = false;

            Color frameCol = vis != null ? vis.frameColor : Palette.GoldDim;
            frame = UiKit.Image("Frame", body, frameCol, UiKit.Rounded);
            frame.rectTransform.Fill();
            frame.Outlined(new Color(0, 0, 0, 0.6f), 2f);
            frame.raycastTarget = false;

            if (def == null)
            {
                BuildBack();
                return;
            }

            var parchment = UiKit.Image("Parchment", body, Palette.Hex("#efe2c2"), UiKit.Rounded);
            parchment.rectTransform.Fill(6);
            parchment.raycastTarget = false;

            // 이름 띠
            var band = UiKit.Image("NameBand", body, Color.Lerp(frameCol, Color.black, 0.25f), UiKit.RoundedSmall);
            band.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -36), new Vector2(-8, -8));
            band.raycastTarget = false;
            nameText = UiKit.Text("Name", band.transform, def.Name, 18, Palette.Hex("#fff6e0"), TextAnchor.MiddleCenter, FontStyle.Bold).Fit(0.55f);
            nameText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(34, 0), new Vector2(-4, 0));
            nameText.Shadowed(1.2f);

            // 그림
            var artBg = UiKit.Image("ArtBg", body, vis != null ? vis.artColor : Color.gray, UiKit.RoundedSmall);
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
            var typeText = UiKit.Text("Type", body, def.TypeName, 12, Palette.Hex("#5a4630"), TextAnchor.MiddleCenter, FontStyle.Bold);
            typeText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -140), new Vector2(-8, -122));

            // 효과 글
            string desc = !fullText && !string.IsNullOrEmpty(def.Short) ? def.Short : def.Text;
            if (def.Is(CardType.Treasure)) desc = "";
            if (def.Is(CardType.Siege))
                desc = fullText ? (def.PowerPerCards > 0 ? "덱이 커질수록 강해지는 부대\n(내 덱 " + def.PowerPerCards + "장당 공격력 1)" : "구매하는 순간\n상대 성을 공격!")
                                : (def.PowerPerCards > 0 ? "덱이 클수록\n강해짐" : "사는 순간\n성 공격!");
            if (def.Is(CardType.Traitor)) desc = fullText ? "덱에 있는 동안\n성벽을 무너뜨린다" : "덱에 있으면\n내 성 피해";
            bool isShort = desc != def.Text;
            // 요약은 굵고 크게, 전체 문장은 칸에 맞춰 자동으로 줄인다
            var bodyText = UiKit.Text("Body", this.body, desc, isShort ? 17 : 13, Palette.Hex("#3a2a1a"), TextAnchor.MiddleCenter,
                isShort ? FontStyle.Bold : FontStyle.Normal).Fit(0.5f);
            bodyText.lineSpacing = -8f;
            float bottom = def.Is(CardType.Siege) || def.Is(CardType.Traitor) ? 40 : 8;
            bodyText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(10, bottom), new Vector2(-10, -140));

            if (def.Is(CardType.Treasure))
            {
                var coinBig = UiKit.Image("CoinBig", body, Palette.Hex("#f2c744"), UiKit.Circle);
                coinBig.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(54, 54));
                coinBig.Outlined(Palette.Hex("#7a5a10"), 2f);
                var v = UiKit.Text("Value", coinBig.transform, "+" + def.Coins, 24, Palette.Hex("#4a3008"), TextAnchor.MiddleCenter, FontStyle.Bold);
                v.rectTransform.Fill();
            }
            if (def.Is(CardType.Siege))
            {
                var badge = UiKit.Image("Power", body, Palette.Hex("#8c1c1c"), UiKit.RoundedSmall);
                badge.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(96, 28));
                string label = def.PowerPerCards > 0 ? def.PowerPerCards + "장당 공격력 1" : "공격력 " + def.Power;
                if (def.PowerPerCards > 0) badge.rectTransform.sizeDelta = new Vector2(146, 28);
                var v = UiKit.Text("Value", badge.transform, label, 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Fit(0.6f);
                v.rectTransform.Fill();
            }
            if (def.Is(CardType.Traitor))
            {
                var badge = UiKit.Image("Self", body, Palette.Hex("#3a1a4a"), UiKit.RoundedSmall);
                badge.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(96, 28));
                var v = UiKit.Text("Value", badge.transform, "내 성 -" + def.SelfDamage, 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Fit(0.6f);
                v.rectTransform.Fill();
            }

            // 비용 동전
            var coin = UiKit.Image("Cost", body, Palette.Hex("#f2c744"), UiKit.Circle);
            coin.rectTransform.At(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(19, -21), new Vector2(40, 40));
            coin.Outlined(Palette.Hex("#6a4a08"), 2f);
            coin.raycastTarget = false;
            var cost = UiKit.Text("Value", coin.transform, def.Cost.ToString(), 20, Palette.Hex("#3a2404"), TextAnchor.MiddleCenter, FontStyle.Bold);
            cost.rectTransform.Fill();

            dim = UiKit.Image("Dim", body, new Color(0, 0, 0, 0), UiKit.Rounded);
            dim.rectTransform.Fill();
            dim.raycastTarget = false;
        }

        void BuildBack()
        {
            backFace = frame.gameObject;
            frame.color = Palette.Hex("#3a2a4a");
            var inner = UiKit.Image("Inner", body, Palette.Hex("#5a3a6a"), UiKit.Rounded);
            inner.rectTransform.Fill(9);
            inner.raycastTarget = false;
            var emblem = UiKit.Image("Emblem", inner.transform, Palette.Gold, UiKit.Circle);
            emblem.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
            emblem.raycastTarget = false;
            var crown = UiKit.Text("Crown", emblem.transform, "왕", 34, Palette.Hex("#3a2a4a"), TextAnchor.MiddleCenter, FontStyle.Bold);
            crown.rectTransform.Fill();
            dim = UiKit.Image("Dim", body, new Color(0, 0, 0, 0), UiKit.Rounded);
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
            hit.raycastTarget = on;
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
            // 제자리(배치)는 root: 위치·크기·기울기
            rt.anchoredPosition = curPos + new Vector2(0, curSel * 34f);
            rt.localScale = new Vector3(curScale, curScale, 1f);
            rt.localRotation = Quaternion.Euler(0, 0, curRot);
            // 마우스를 올렸을 때 연출은 body만: 위로 올라가고, 커지고, 똑바로 선다
            float sc = Mathf.Max(0.0001f, curScale);
            float lift = HoverLift * k / sc;
            float hs = Mathf.Lerp(1f, HoverScale, k);
            body.anchoredPosition = new Vector2(0, lift);
            body.localScale = new Vector3(hs, hs, 1f);
            body.localRotation = Quaternion.Euler(0, 0, -curRot * k);
            // 감지 영역을 올라간 카드까지 덮도록 늘린다 (아래쪽은 그대로)
            float growX = W * (hs - 1f) / 2f, growY = H * (hs - 1f) / 2f;
            hit.rectTransform.offsetMin = new Vector2(-growX, 0);
            hit.rectTransform.offsetMax = new Vector2(growX, lift + growY);
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
            if (press.ConsumeClick()) return; // 길게 누른 뒤 손을 뗀 것은 클릭이 아니다
            Clicked?.Invoke(this);
        }

        // ---------------- 길게 누르기 (모바일에서 카드 크게 보기) ----------------

        /// <summary>길게 누르기 시작(true) / 손 뗌(false)</summary>
        public event Action<CardView, bool> LongPressed;
        readonly LongPressTracker press = new LongPressTracker();

        public void OnPointerDown(PointerEventData e) => press.Down(e);
        public void OnPointerUp(PointerEventData e) { if (press.Up()) LongPressed?.Invoke(this, false); }

        void LateUpdate()
        {
            if (press.Tick()) LongPressed?.Invoke(this, true);
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
