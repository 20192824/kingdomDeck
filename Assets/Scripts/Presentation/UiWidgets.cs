using System;
using KingdomDeck.Engine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    /// <summary>성 체력 바 (줄어든 부분이 흰색으로 천천히 따라온다)</summary>
    public class HpBar
    {
        public RectTransform Root;
        Image fill, ghost, back;
        TMP_Text value;
        float shown, ghostV;
        int target;
        float flash;
        const float Max = KingdomDeck.Engine.Game.StartingHp;

        public static HpBar Create(Transform parent, string title, Color color, bool alignRight)
        {
            var bar = new HpBar();
            var root = UiKit.Panel("HpBar", parent, Palette.PanelDark);
            bar.Root = root.rectTransform;

            var label = UiKit.Text("Title", root.transform, title, 26, Palette.Text, alignRight ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            label.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(14, 0), new Vector2(-14, -4));
            label.Shadowed();

            bar.back = UiKit.Image("Back", root.transform, Palette.Hex("#120e0a"), UiKit.RoundedSmall);
            bar.back.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(12, 8), new Vector2(-70, -2));
            bar.ghost = UiKit.Image("Ghost", bar.back.transform, new Color(1f, 0.95f, 0.85f, 0.9f), UiKit.RoundedSmall);
            bar.ghost.rectTransform.Fill(2);
            bar.fill = UiKit.Image("Fill", bar.back.transform, color, UiKit.RoundedSmall);
            bar.fill.rectTransform.Fill(2);
            var shine = UiKit.Image("Shine", bar.fill.transform, new Color(1, 1, 1, 0.18f), UiKit.RoundedSmall);
            shine.rectTransform.Place(new Vector2(0, 0.55f), Vector2.one, new Vector2(2, 0), new Vector2(-2, -1));

            bar.value = UiKit.Text("Value", root.transform, KingdomDeck.Engine.Game.StartingHp.ToString(), 42, Palette.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            bar.value.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0.62f), new Vector2(-74, 2), new Vector2(-6, 0));
            bar.value.Shadowed(2f);
            bar.shown = bar.ghostV = bar.target = KingdomDeck.Engine.Game.StartingHp;
            bar.Apply();
            return bar;
        }

        public void Set(int hp, bool instant)
        {
            if (hp < target) flash = 1f;
            target = hp;
            if (instant) { shown = ghostV = hp; }
            Apply();
        }

        public void Tick(float dt)
        {
            shown = Mathf.MoveTowards(shown, target, dt * 30f * Mathf.Max(1f, Anim.Speed));
            ghostV = ghostV > shown ? Mathf.MoveTowards(ghostV, shown, dt * 12f * Mathf.Max(1f, Anim.Speed)) : shown;
            flash = Mathf.MoveTowards(flash, 0, dt * 2.5f);
            Apply();
        }

        void Apply()
        {
            SetWidth(fill.rectTransform, Mathf.Clamp01(shown / Max));
            SetWidth(ghost.rectTransform, Mathf.Clamp01(ghostV / Max));
            value.text = Mathf.RoundToInt(shown).ToString();
            value.color = Color.Lerp(shown <= 15 ? Palette.Bad : Palette.Text, Color.white, flash);
            back.color = Color.Lerp(Palette.Hex("#120e0a"), Palette.Hex("#6a1010"), flash * 0.8f);
        }

        static void SetWidth(RectTransform rt, float k)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, k), 1);
            rt.offsetMin = new Vector2(2, 2);
            rt.offsetMax = new Vector2(-2, -2);
        }
    }

    /// <summary>공급처 한 줄 (카드 이름, 비용, 남은 수량)</summary>
    public class SupplyTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public string CardId;
        public event Action<SupplyTile> Clicked;
        public event Action<SupplyTile, bool> Hovered;

        Image bg, dim, chip;
        Outline outline;
        Color tint = Color.clear;
        TMP_Text count, nameText;
        bool hovered;
        int lastCount = -1;
        float pulse;

        public static SupplyTile Create(Transform parent, CardDef def, CardVisual vis, Texture art)
        {
            var bg = UiKit.Image("Supply_" + def.Id, parent, Palette.Hex("#2e261c"), UiKit.RoundedSmall);
            var tile = bg.gameObject.AddComponent<SupplyTile>();
            tile.CardId = def.Id;
            tile.bg = bg;

            tile.outline = bg.gameObject.AddComponent<Outline>();
            tile.outline.effectDistance = new Vector2(3, -3);
            tile.outline.effectColor = new Color(0, 0, 0, 0);

            tile.chip = UiKit.Image("Chip", bg.transform, vis.frameColor, UiKit.RoundedSmall);
            tile.chip.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 4), new Vector2(92, -4));
            tile.chip.raycastTarget = false;
            if (art != null)
            {
                var raw = UiKit.Rect("Art", tile.chip.transform).gameObject.AddComponent<RawImage>();
                raw.texture = art;
                raw.rectTransform.Fill(2);
                raw.uvRect = new Rect(0.12f, 0.2f, 0.76f, 0.62f);
                raw.raycastTarget = false;
            }

            var coin = UiKit.Image("Cost", bg.transform, Palette.Hex("#f2c744"), UiKit.Circle);
            coin.rectTransform.At(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(98 + 16, 0), new Vector2(30, 30));
            coin.Outlined(Palette.Hex("#6a4a08"), 1.5f);
            coin.raycastTarget = false;
            var cost = UiKit.Text("Value", coin.transform, def.Cost.ToString(), 17, Palette.Hex("#3a2404"), TextAnchor.MiddleCenter, FontStyle.Bold);
            cost.rectTransform.Fill();

            tile.nameText = UiKit.Text("Name", bg.transform, def.Name, 18, Palette.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            tile.nameText.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(136, 4), new Vector2(-56, -6));
            tile.nameText.Shadowed(1.2f);
            string sub = def.TypeName + (def.PowerPerCards > 0 ? "  덱 " + def.PowerPerCards + "장당 1"
                : def.Power > 0 ? "  공격력 " + def.Power : def.Coins > 0 ? "  +" + def.Coins + "코인" : "");
            var type = UiKit.Text("Type", bg.transform, sub, 12, Palette.TextDim, TextAnchor.LowerLeft);
            type.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(136, 5), new Vector2(-56, -4));

            tile.count = UiKit.Text("Count", bg.transform, "10", 22, Palette.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
            tile.count.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-56, 0), new Vector2(-10, 0));
            tile.count.Shadowed(1.5f);

            tile.dim = UiKit.Image("Dim", bg.transform, new Color(0, 0, 0, 0), UiKit.RoundedSmall);
            tile.dim.rectTransform.Fill();
            tile.dim.raycastTarget = false;
            return tile;
        }

        public RectTransform Rt => (RectTransform)transform;

        public void SetCount(int n)
        {
            if (lastCount >= 0 && n != lastCount) pulse = 1f;
            lastCount = n;
            count.text = n <= 0 ? "없음" : n.ToString();
            count.color = n <= 0 ? Palette.Bad : n <= 2 ? Palette.Hex("#ff9a50") : Palette.Gold;
        }

        public void SetState(CardHighlight highlight, bool dimmed)
        {
            Color c;
            switch (highlight)
            {
                case CardHighlight.Buyable: c = new Color(1f, 0.8f, 0.25f, 0.8f); break;
                case CardHighlight.Selected: c = new Color(0.3f, 0.85f, 1f, 0.9f); break;
                case CardHighlight.Playable: c = new Color(0.4f, 1f, 0.5f, 0.8f); break;
                default: c = new Color(1, 1, 1, 0); break;
            }
            outline.effectColor = c;
            tint = c.a > 0 ? new Color(c.r, c.g, c.b, 0.22f) : Color.clear;
            dim.color = new Color(0, 0, 0, dimmed ? 0.5f : 0f);
        }

        void Update()
        {
            pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime * 2f);
            Color baseCol = hovered ? Palette.Hex("#4a3c28") : Palette.Hex("#2e261c");
            if (tint.a > 0) baseCol = Color.Lerp(baseCol, new Color(tint.r, tint.g, tint.b, 1f), tint.a);
            bg.color = Color.Lerp(baseCol, Palette.Hex("#8a6a30"), pulse);
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; Hovered?.Invoke(this, true); }
        public void OnPointerExit(PointerEventData e) { hovered = false; Hovered?.Invoke(this, false); }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (press.ConsumeClick()) return;
            Clicked?.Invoke(this);
        }

        /// <summary>길게 누르기 시작(true) / 손 뗌(false)</summary>
        public event Action<SupplyTile, bool> LongPressed;
        readonly LongPressTracker press = new LongPressTracker();
        public void OnPointerDown(PointerEventData e) => press.Down(e);
        public void OnPointerUp(PointerEventData e) { if (press.Up()) LongPressed?.Invoke(this, false); }
        void LateUpdate() { if (press.Tick()) LongPressed?.Invoke(this, true); }
    }
}
