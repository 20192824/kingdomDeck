using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomDeck.AI;
using KingdomDeck.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    /// <summary>화면 구성과 그리기</summary>
    public partial class KingdomDeckGame
    {
        const float TopH = 64f, LeftW = 372f, RightW = 340f, HandH = 250f, StatusH = 104f;

        Canvas canvas;
        RectTransform canvasRt, fieldRect, fxLayer, handArea, playArea, myInfo, aiHandRow, coinsChip;
        HpBar[] hpBars = new HpBar[2];
        Text aiNameText, aiInfoText, myInfoText, turnText, promptText, toastText, bannerText, logText;
        Text actionsText, buysText, coinsText, discountText;
        Image[] phasePills = new Image[3];
        Text[] phasePillTexts = new Text[3];
        GameObject discountChip;
        UiKit.UiButton playAllBtn, nextBtn, endTurnBtn, confirmBtn, yesBtn, noBtn, skipBtn;
        UiKit.UiButton[] speedBtns = new UiKit.UiButton[3];
        RectTransform supplyList;
        readonly List<SupplyTile> tiles = new List<SupplyTile>();
        readonly List<CardView> handCards = new List<CardView>();
        readonly List<CardView> playCards = new List<CardView>();
        readonly List<Image> aiBacks = new List<Image>();
        CardView preview;
        ScrollRect logScroll;
        readonly List<string> logLines = new List<string>();
        CanvasGroup bannerGroup, toastGroup;
        Coroutine toastRoutine;
        GameObject menuPanel, resultPanel;
        Text resultTitle, resultBody;
        UiKit.UiButton continueBtn, loadMenuBtn, instantBtn, soundBtn;
        UiKit.UiButton[] diffBtns = new UiKit.UiButton[3];

        // =====================================================================
        // 만들기
        // =====================================================================

        void BuildUi()
        {
            var cgo = new GameObject("KingdomDeckCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRt = (RectTransform)cgo.transform;

            fieldRect = UiKit.Rect("FieldRect", canvasRt).Place(Vector2.zero, Vector2.one, new Vector2(LeftW, HandH + StatusH), new Vector2(-RightW, -TopH));

            BuildTopBar();
            BuildSupplyPanel();
            BuildRightPanel();
            BuildStatusRow();
            BuildFieldOverlays();
            BuildHandArea();

            fxLayer = UiKit.Rect("FX", canvasRt).Fill();
            BuildBannerAndToast();
            BuildMenu();
            BuildResult();
            SetInteractivityOff();
        }

        void BuildTopBar()
        {
            var bar = UiKit.Image("TopBar", canvasRt, Palette.Hex("#120e0a", 0.96f));
            bar.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -TopH), Vector2.zero);
            var line = UiKit.Image("Line", bar.transform, Palette.GoldDim);
            line.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 2));

            var title = UiKit.Text("Title", bar.transform, "왕국의 덱", 30, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Place(Vector2.zero, new Vector2(0, 1), new Vector2(22, 0), new Vector2(200, 0));
            title.Shadowed(2f);

            turnText = UiKit.Text("Turn", bar.transform, "", 20, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            turnText.rectTransform.Place(Vector2.zero, new Vector2(0, 1), new Vector2(205, 0), new Vector2(430, 0));

            string[] names = { "행동", "구매", "정리" };
            for (int i = 0; i < 3; i++)
            {
                var pill = UiKit.Image("Phase" + i, bar.transform, Palette.Button, UiKit.RoundedSmall);
                pill.rectTransform.At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(440 + i * 96, 0), new Vector2(88, 38));
                phasePills[i] = pill;
                phasePillTexts[i] = UiKit.Text("T", pill.transform, names[i], 19, Palette.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
                phasePillTexts[i].rectTransform.Fill();
            }

            actionsText = ResourceChip(bar.transform, 760, "행동", Palette.Hex("#6fb0ff"), out _);
            buysText = ResourceChip(bar.transform, 900, "구매", Palette.Hex("#9be08a"), out _);
            coinsText = ResourceChip(bar.transform, 1040, "코인", Palette.Gold, out coinsChip);
            discountText = ResourceChip(bar.transform, 1180, "할인", Palette.Hex("#ff9ad0"), out var dChip);
            discountChip = dChip.gameObject;
            discountChip.SetActive(false);

            float x = -16;
            UiKit.UiButton Btn(string label, float w, System.Action a)
            {
                var b = UiKit.Button(label, bar.transform, label, 17, a);
                b.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(w, 40));
                x -= w + 8;
                return b;
            }
            Btn("메뉴", 70, () => ShowMenu(true));
            Btn("불러오기", 96, OnLoad);
            Btn("저장", 70, OnSave);
            skipBtn = Btn("건너뛰기", 100, OnSkip);
            x -= 8;
            speedBtns[2] = Btn("4x", 50, () => SetSpeed(4f));
            speedBtns[1] = Btn("2x", 50, () => SetSpeed(2f));
            speedBtns[0] = Btn("1x", 50, () => SetSpeed(1f));
            var sl = UiKit.Text("SpeedLabel", bar.transform, "연출 속도", 15, Palette.TextDim, TextAnchor.MiddleRight);
            sl.rectTransform.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(x, 0), new Vector2(80, 40));
            UpdateSpeedButtons();
        }

        Text ResourceChip(Transform parent, float x, string label, Color color, out RectTransform chipRt)
        {
            var chip = UiKit.Image("Chip_" + label, parent, Palette.Hex("#241c14"), UiKit.RoundedSmall);
            chip.rectTransform.At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(128, 42));
            chip.Outlined(new Color(color.r, color.g, color.b, 0.5f), 1.5f);
            var l = UiKit.Text("Label", chip.transform, label, 16, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            l.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-50, 0));
            var v = UiKit.Text("Value", chip.transform, "0", 26, Palette.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            v.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(50, 0), new Vector2(-12, 0));
            v.Shadowed(1.5f);
            chipRt = chip.rectTransform;
            return v;
        }

        void BuildSupplyPanel()
        {
            var panel = UiKit.Panel("SupplyPanel", canvasRt);
            panel.rectTransform.Place(Vector2.zero, new Vector2(0, 1), new Vector2(6, 6), new Vector2(LeftW - 6, -TopH - 6));
            var head = UiKit.Text("Head", panel.transform, "공급처", 22, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -40), new Vector2(-16, -6));
            var hint = UiKit.Text("Hint", panel.transform, "클릭해서 구매 · 남은 수량", 13, Palette.TextDim, TextAnchor.MiddleRight);
            hint.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -40), new Vector2(-16, -6));
            supplyList = UiKit.Rect("List", panel.transform).Place(Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -44));
        }

        void BuildSupply()
        {
            foreach (var t in tiles) if (t != null) Destroy(t.gameObject);
            tiles.Clear();
            foreach (Transform c in supplyList) Destroy(c.gameObject);

            float y = 0;
            const float h = 51f, gap = 3f;
            void Header(string text)
            {
                var t = UiKit.Text("Section", supplyList, text, 14, Palette.TextDim, TextAnchor.LowerLeft, FontStyle.Bold);
                t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -y - 22), new Vector2(0, -y));
                y += 24;
            }
            Header("기본 카드");
            int i = 0;
            foreach (var id in game.SupplyOrder)
            {
                if (i == 7) Header("왕국 카드");
                var def = db[id];
                var tile = SupplyTile.Create(supplyList, def, library.Get(id), ArtStudio.Get(id, library));
                tile.Rt.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -y - h), new Vector2(0, -y));
                tile.Clicked += OnSupplyClicked;
                tile.Hovered += OnSupplyHover;
                tiles.Add(tile);
                y += h + gap;
                i++;
            }
        }

        void OnSupplyHover(SupplyTile t, bool on)
        {
            if (preview != null) { Destroy(preview.gameObject); preview = null; }
            if (!on) return;
            preview = CardView.Create(fxLayer, db[t.CardId], library.Get(t.CardId), ArtStudio.Get(t.CardId, library), 1.45f);
            preview.Detach();
            var pos = ToFx(ScreenOf(t.Rt));
            float halfH = CardView.H * 1.45f / 2f;
            float minY = -fxLayer.rect.height / 2 + halfH + 10, maxY = fxLayer.rect.height / 2 - halfH - TopH - 6;
            preview.Rt.anchoredPosition = new Vector2(-fxLayer.rect.width / 2 + LeftW + CardView.W * 1.45f / 2 + 14, Mathf.Clamp(pos.y, minY, maxY));
            preview.Rt.localScale = Vector3.one * 1.45f;
            foreach (var g in preview.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        }

        void BuildRightPanel()
        {
            var panel = UiKit.Panel("LogPanel", canvasRt);
            panel.rectTransform.Place(new Vector2(1, 0), Vector2.one, new Vector2(-RightW + 6, HandH + 2), new Vector2(-6, -TopH - 6));
            var head = UiKit.Text("Head", panel.transform, "게임 기록", 22, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -40), new Vector2(-16, -6));

            var view = UiKit.Image("View", panel.transform, Palette.Hex("#120e0a", 0.6f), UiKit.RoundedSmall);
            view.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -44));
            view.gameObject.AddComponent<RectMask2D>();
            logScroll = view.gameObject.AddComponent<ScrollRect>();
            logScroll.horizontal = false;
            logScroll.movementType = ScrollRect.MovementType.Clamped;
            logScroll.scrollSensitivity = 30f;
            var content = UiKit.Rect("Content", view.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(8, 0);
            content.offsetMax = new Vector2(-8, 0);
            logText = UiKit.Text("Log", content, "", 15, Palette.Text, TextAnchor.UpperLeft);
            logText.rectTransform.Fill();
            logText.lineSpacing = 1.05f;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.padding = new RectOffset(0, 0, 6, 6);
            logScroll.content = content;
            logScroll.viewport = view.rectTransform;

            // 버튼
            var btnPanel = UiKit.Panel("ButtonPanel", canvasRt);
            btnPanel.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-RightW + 6, 6), new Vector2(-6, HandH - 4));
            playAllBtn = UiKit.Button("PlayAll", btnPanel.transform, "재화 모두 내기", 22, OnPlayAll, Palette.Hex("#6a5220"));
            playAllBtn.Rt.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -78), new Vector2(-14, -14));
            nextBtn = UiKit.Button("Next", btnPanel.transform, "다음 단계 ▶", 22, OnNextPhase, Palette.Hex("#2f4a6a"));
            nextBtn.Rt.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -154), new Vector2(-14, -90));
            endTurnBtn = UiKit.Button("EndTurn", btnPanel.transform, "턴 종료", 22, OnEndTurn, Palette.Hex("#6a2a22"));
            endTurnBtn.Rt.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -230), new Vector2(-14, -166));
        }

        void BuildStatusRow()
        {
            var row = UiKit.Rect("StatusRow", canvasRt).Place(Vector2.zero, Vector2.one, new Vector2(LeftW, HandH), new Vector2(-RightW, 0));
            row.offsetMax = new Vector2(-RightW, -(1080 - HandH - StatusH));
            row.anchorMax = new Vector2(1, 0);
            row.offsetMin = new Vector2(LeftW, HandH);
            row.offsetMax = new Vector2(-RightW, HandH + StatusH);

            var bg = UiKit.Image("Bg", row, Palette.Hex("#17130e", 0.85f));
            bg.rectTransform.Fill();

            hpBars[Me] = HpBar.Create(row, "내 성", Palette.Player, false);
            hpBars[Me].Root.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(8, 8), new Vector2(340, -8));

            var info = UiKit.Panel("MyInfo", row, Palette.PanelDark);
            info.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(348, 8), new Vector2(478, -8));
            myInfo = info.rectTransform;
            myInfoText = UiKit.Text("Text", info.transform, "", 15, Palette.Text, TextAnchor.MiddleCenter);
            myInfoText.rectTransform.Fill(6);

            var prompt = UiKit.Panel("Prompt", row, Palette.Hex("#241c14", 0.95f));
            prompt.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(486, 8), new Vector2(-8, -8));
            promptText = UiKit.Text("Text", prompt.transform, "", 19, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            promptText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-260, 0));
            promptText.Shadowed(1.5f);

            confirmBtn = UiKit.Button("Confirm", prompt.transform, "확인", 20, OnConfirm, Palette.Hex("#2f6a3a"));
            confirmBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(120, 52));
            yesBtn = UiKit.Button("Yes", prompt.transform, "공개한다", 19, () => OnYesNo(true), Palette.Hex("#2f6a3a"));
            yesBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-136, 0), new Vector2(116, 52));
            noBtn = UiKit.Button("No", prompt.transform, "안 한다", 19, () => OnYesNo(false), Palette.Hex("#6a2a22"));
            noBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(110, 52));
        }

        void BuildFieldOverlays()
        {
            // AI 성 (왼쪽 위)
            hpBars[Ai] = HpBar.Create(fieldRect, "AI의 성", Palette.Enemy, false);
            hpBars[Ai].Root.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(340, 84));
            aiNameText = hpBars[Ai].Root.Find("Title").GetComponent<Text>();

            // AI 손패 (오른쪽 위)
            var aiPanel = UiKit.Panel("AiInfo", fieldRect, Palette.PanelDark);
            aiPanel.rectTransform.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -10), new Vector2(300, 84));
            aiHandRow = UiKit.Rect("AiHand", aiPanel.transform).Place(new Vector2(0, 0.42f), Vector2.one, new Vector2(10, 0), new Vector2(-10, -6));
            aiInfoText = UiKit.Text("Text", aiPanel.transform, "", 15, Palette.Text, TextAnchor.MiddleCenter);
            aiInfoText.rectTransform.Place(Vector2.zero, new Vector2(1, 0.42f), new Vector2(6, 2), new Vector2(-6, 0));

            // 사용한 카드 (전장 아래쪽 오른편)
            var pa = UiKit.Image("PlayArea", fieldRect, Palette.Hex("#120e0a", 0.55f), UiKit.Rounded);
            pa.rectTransform.At(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 10), new Vector2(430, 136));
            playArea = pa.rectTransform;
            var lbl = UiKit.Text("Label", pa.transform, "사용한 카드", 13, Palette.TextDim, TextAnchor.UpperLeft);
            lbl.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -20), new Vector2(-10, -3));

            // 덱 위에서 꺼낸 카드 (망루로 확인 / 약탈대로 공개) — 가운데에 잠깐 나타난다
            var rv = UiKit.Panel("RevealRow", fieldRect, Palette.Hex("#120e0a", 0.88f));
            rv.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(460, 270));
            revealRow = rv.rectTransform;
            revealLabel = UiKit.Text("Label", rv.transform, "", 17, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            revealLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -36), new Vector2(-10, -6));
            revealLabel.Shadowed();
            rv.gameObject.SetActive(false);
        }

        RectTransform revealRow;
        Text revealLabel;
        readonly List<CardView> revealCards = new List<CardView>();
        string revealKey = "";

        void RenderRevealed(Snapshot s)
        {
            // 내 카드가 공개 중이면 그쪽을, 아니면 AI 쪽을 보여준다
            int p = s.Players[Me].Revealed.Length > 0 ? Me : s.Players[Ai].Revealed.Length > 0 ? Ai : -1;
            if (p < 0)
            {
                if (revealRow.gameObject.activeSelf) { foreach (var c in revealCards) Destroy(c.gameObject); revealCards.Clear(); revealKey = ""; revealRow.gameObject.SetActive(false); }
                return;
            }
            var view = s.Players[p];
            bool hidden = p == Ai && view.RevealedPrivate;
            string key = p + (hidden ? "h" : "") + ":" + string.Join(",", view.Revealed);
            revealRow.gameObject.SetActive(true);
            revealRow.SetAsLastSibling();
            string owner = p == Me ? "내" : "AI";
            revealLabel.text = view.RevealedPrivate
                ? (p == Me ? "덱 위 카드 (나만 볼 수 있음)" : "AI가 덱 위 카드를 확인하는 중")
                : owner + " 덱 위 카드 공개!";
            if (key == revealKey) return;
            revealKey = key;
            foreach (var c in revealCards) Destroy(c.gameObject);
            revealCards.Clear();
            const float scale = 0.95f;
            int n = view.Revealed.Length;
            for (int i = 0; i < n; i++)
            {
                string id = view.Revealed[i];
                var cv = CardView.Create(revealRow, hidden ? null : db[id], hidden ? null : library.Get(id), hidden ? null : ArtStudio.Get(id, library), scale);
                cv.Rt.anchorMin = cv.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                cv.SetBase(new Vector2((i - (n - 1) / 2f) * (CardView.W * scale + 14), -16), scale, 0, true);
                cv.HoverScale = 1.3f;
                cv.HoverLift = 20f;
                if (p == Me) cv.Clicked += OnRevealClicked;
                else cv.SetInteractable(false);
                revealCards.Add(cv);
            }
            revealRow.sizeDelta = new Vector2(Mathf.Max(460, n * (CardView.W * scale + 14) + 40), 270);
        }

        void OnRevealClicked(CardView cv)
        {
            if (!HumanReady) return;
            var d = game.Pending;
            if (d.Kind != DecisionKind.ChooseCards || d.From != Zone.Revealed) return;
            int i = revealCards.IndexOf(cv);
            if (i < 0 || !d.CardChoices.Contains(i)) return;
            if (selected.Contains(i)) selected.Remove(i);
            else
            {
                if (selected.Count >= d.Max)
                {
                    if (d.Max == 1) selected.Clear();
                    else return;
                }
                selected.Add(i);
            }
            Sfx.Play(Sfx.Click, 0.5f);
            RefreshInteractivity();
        }

        void BuildHandArea()
        {
            var bg = UiKit.Image("HandBg", canvasRt, Palette.Hex("#0e0b08", 0.9f));
            bg.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(LeftW, 0), new Vector2(-RightW, HandH));
            handArea = UiKit.Rect("Hand", canvasRt).Place(Vector2.zero, new Vector2(1, 0), new Vector2(LeftW, 0), new Vector2(-RightW, HandH));
        }

        void BuildBannerAndToast()
        {
            var b = UiKit.Image("Banner", fieldRect, Palette.Hex("#000000", 0.55f));
            b.rectTransform.At(new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 110));
            b.raycastTarget = false;
            bannerGroup = b.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0;
            bannerGroup.blocksRaycasts = false;
            bannerText = UiKit.Text("Text", b.transform, "", 56, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            bannerText.rectTransform.Fill();
            bannerText.Outlined(new Color(0, 0, 0, 0.9f), 3f);

            var t = UiKit.Image("Toast", canvasRt, Palette.Hex("#000000", 0.75f), UiKit.Rounded);
            t.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((LeftW - RightW) / 2f, HandH + StatusH + 16), new Vector2(720, 50));
            t.raycastTarget = false;
            toastGroup = t.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;
            toastGroup.blocksRaycasts = false;
            toastText = UiKit.Text("Text", t.transform, "", 20, Palette.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastText.rectTransform.Fill(6);
        }

        void BuildMenu()
        {
            var dim = UiKit.Image("Menu", canvasRt, Palette.Hex("#000000", 0.72f));
            dim.rectTransform.Fill();
            menuPanel = dim.gameObject;

            var p = UiKit.Panel("Box", dim.transform, Palette.Hex("#231c14", 0.98f));
            p.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 760));

            var title = UiKit.Text("Title", p.transform, "왕국의 덱", 84, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -24));
            title.Outlined(new Color(0, 0, 0, 0.9f), 3f);
            var sub = UiKit.Text("Sub", p.transform, "병기를 사들여 상대의 성을 무너뜨려라!", 22, Palette.Text, TextAnchor.MiddleCenter);
            sub.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), new Vector2(0, -130));

            var rules = UiKit.Text("Rules", p.transform,
                "· 매 턴 <b>행동 → 구매 → 정리</b>. 손패 5장으로 시작합니다.\n" +
                "· <color=#ff9a8a><b>병기</b></color>(궁수대·투석기·드래곤)를 사는 순간 상대 성을 공격합니다.\n" +
                "  하지만 병기는 덱 안에서는 쓸모없는 카드가 됩니다. 언제부터 살지가 핵심!\n" +
                "· <color=#c8a0ff><b>배신자</b></color>는 내 덱에 있는 동안 내 성 체력을 1 깎습니다.\n" +
                "· 드래곤이 모두 팔리거나 공급 더미 3개가 비면 끝. 성 체력이 더 많은 쪽이 승리.",
                17, Palette.Text, TextAnchor.UpperLeft);
            rules.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -330), new Vector2(-60, -190));
            rules.lineSpacing = 1.15f;

            var dl = UiKit.Text("DiffLabel", p.transform, "AI 난이도", 20, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            dl.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -390), new Vector2(-60, -350));
            string[] dn = { "쉬움", "보통", "어려움" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                diffBtns[i] = UiKit.Button("Diff" + i, p.transform, dn[i], 22, () => { difficulty = (Difficulty)k; SaveSettings(); UpdateMenuButtons(); });
                diffBtns[i].Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60 + i * 238, -400), new Vector2(222, 56));
            }
            instantBtn = UiKit.Button("Instant", p.transform, "", 18, () => { instantEnd = !instantEnd; SaveSettings(); UpdateMenuButtons(); });
            instantBtn.Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, -476), new Vector2(460, 50));
            soundBtn = UiKit.Button("Sound", p.transform, "", 18, () => { Sfx.Muted = !Sfx.Muted; SaveSettings(); UpdateMenuButtons(); });
            soundBtn.Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(536, -476), new Vector2(224, 50));

            var newBtn = UiKit.Button("New", p.transform, "새 게임", 28, NewGame, Palette.Hex("#6a5220"));
            newBtn.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-250, 120), new Vector2(230, 70));
            loadMenuBtn = UiKit.Button("Load", p.transform, "불러오기", 26, OnLoad);
            loadMenuBtn.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(230, 70));
            continueBtn = UiKit.Button("Continue", p.transform, "계속하기", 26, () => ShowMenu(false), Palette.Hex("#2f4a6a"));
            continueBtn.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(250, 120), new Vector2(230, 70));
            var foot = UiKit.Text("Foot", p.transform, "Space: 다음 단계/턴 종료   Enter: 확인   Esc: 메뉴", 15, Palette.TextDim, TextAnchor.MiddleCenter);
            foot.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 70));
        }

        void BuildResult()
        {
            var dim = UiKit.Image("Result", canvasRt, Palette.Hex("#000000", 0.6f));
            dim.rectTransform.Fill();
            resultPanel = dim.gameObject;
            var p = UiKit.Panel("Box", dim.transform, Palette.Hex("#231c14", 0.98f));
            p.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(700, 470));
            resultTitle = UiKit.Text("Title", p.transform, "", 80, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -20));
            resultTitle.Outlined(new Color(0, 0, 0, 0.9f), 3f);
            resultBody = UiKit.Text("Body", p.transform, "", 22, Palette.Text, TextAnchor.UpperCenter);
            resultBody.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 110), new Vector2(-40, -140));
            resultBody.lineSpacing = 1.2f;
            var again = UiKit.Button("Again", p.transform, "다시 하기", 26, NewGame, Palette.Hex("#6a5220"));
            again.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-130, 30), new Vector2(230, 64));
            var menu = UiKit.Button("Menu", p.transform, "메뉴로", 26, () => { resultPanel.SetActive(false); ShowMenu(true); });
            menu.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(130, 30), new Vector2(230, 64));
            resultPanel.SetActive(false);
        }

        // =====================================================================
        // 메뉴 / 결과
        // =====================================================================

        void ShowMenu(bool on)
        {
            if (!on && game == null) return;
            menuOpen = on;
            menuPanel.SetActive(on);
            if (on) { menuPanel.transform.SetAsLastSibling(); UpdateMenuButtons(); }
            needsRefresh = true;
        }

        void UpdateMenuButtons()
        {
            for (int i = 0; i < 3; i++)
            {
                bool sel = (int)difficulty == i;
                diffBtns[i].Bg.color = sel ? Palette.Hex("#8a6a20") : Palette.Button;
                diffBtns[i].Label.color = sel ? Color.white : Palette.TextDim;
            }
            instantBtn.Label.text = "성 체력 0 → 즉시 종료: " + (instantEnd ? "<color=#9be08a>켜짐</color>" : "<color=#ff9a8a>꺼짐</color>");
            soundBtn.Label.text = "효과음: " + (Sfx.Muted ? "<color=#ff9a8a>꺼짐</color>" : "<color=#9be08a>켜짐</color>");
            continueBtn.Interactable = game != null && !game.IsOver;
            loadMenuBtn.Interactable = File.Exists(SavePath);
        }

        void ShowResult()
        {
            if (resultPanel.activeSelf) return;
            resultPanel.SetActive(true);
            resultPanel.transform.SetAsLastSibling();
            int w = game.Winner;
            resultTitle.text = w == Me ? "승리!" : w == Ai ? "패배..." : "무승부";
            resultTitle.color = w == Me ? Palette.Gold : w == Ai ? Palette.Bad : Palette.Text;
            string reason;
            switch (game.EndReason)
            {
                case EndReason.DragonsGone: reason = "드래곤 더미가 모두 소진되었습니다"; break;
                case EndReason.ThreePiles: reason = "공급 더미 3개가 소진되었습니다"; break;
                case EndReason.CastleDestroyed: reason = (game.CastleHp(Me) <= 0 ? "내 성이" : "AI의 성이") + " 함락되었습니다!"; break;
                default: reason = "턴 제한에 도달했습니다"; break;
            }
            string tie = game.CastleHp(Me) == game.CastleHp(Ai) && w >= 0 ? "\n(체력이 같아 턴을 덜 진행한 쪽 승리)" : "";
            resultBody.text = reason + tie + "\n\n" +
                "<color=#8fc0ff>내 성 " + game.CastleHp(Me) + "</color>   :   <color=#ff9a8a>" + game.CastleHp(Ai) + " AI의 성</color>\n" +
                "<size=18><color=#a8977a>진행한 턴: 나 " + game.Players[Me].TurnsTaken + " / AI " + game.Players[Ai].TurnsTaken +
                "   ·   덱: 나 " + game.Players[Me].TotalCards + "장 / AI " + game.Players[Ai].TotalCards + "장\n" +
                "AI 난이도: " + Bot.LevelName(difficulty) + "</color></size>";
            Sfx.Play(w == Me ? Sfx.Win : Sfx.Lose, 0.9f);
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { /* 무시 */ }
        }

        void UpdateSpeedButtons()
        {
            float[] v = { 1f, 2f, 4f };
            for (int i = 0; i < 3; i++)
            {
                if (speedBtns[i] == null) continue;
                bool sel = Mathf.Approximately(Anim.Speed, v[i]);
                speedBtns[i].Bg.color = sel ? Palette.Hex("#8a6a20") : Palette.Button;
                speedBtns[i].Label.color = sel ? Color.white : Palette.TextDim;
            }
        }

        // =====================================================================
        // 상태 그리기
        // =====================================================================

        void ApplySnapshot(Snapshot s, bool drawAnim = false)
        {
            shown = s;
            ApplyTopBar(s);
            ApplySupply(s);
            RenderHand(s.Players[Me].Hand, drawAnim);
            RenderPlayArea(s.Players[s.Current].InPlay);
            var me = s.Players[Me];
            var ai = s.Players[Ai];
            myInfoText.text = "<b>덱</b> " + me.DeckCount + "\n<b>버림</b> " + me.DiscardCount + "\n<color=#a8977a>전체 " + me.TotalCards + "장</color>";
            aiInfoText.text = "손패 " + ai.Hand.Length + "  ·  덱 " + ai.DeckCount + "  ·  버림 " + ai.DiscardCount + "  ·  전체 " + ai.TotalCards;
            RenderAiHand(ai.Hand.Length);
            RenderRevealed(s);
        }

        void ApplyTopBar(Snapshot s)
        {
            string who = s.Current == Me ? "<color=#8fc0ff>나의 턴</color>" : "<color=#ff9a8a>AI의 턴</color>";
            turnText.text = "턴 " + s.TurnNumber + "  " + who;
            Color team = s.Current == Me ? Palette.Player : Palette.Enemy;
            for (int i = 0; i < 3; i++)
            {
                bool on = (i == 0 && s.Phase == Phase.Action) || (i == 1 && s.Phase == Phase.Buy) || (i == 2 && s.Phase == Phase.Cleanup);
                phasePills[i].color = on ? team : Palette.Hex("#241c14");
                phasePillTexts[i].color = on ? Color.white : Palette.TextDim;
            }
            actionsText.text = s.Actions.ToString();
            buysText.text = s.Buys.ToString();
            coinsText.text = s.Coins.ToString();
            discountChip.SetActive(s.CostDiscount > 0);
            discountText.text = "-" + s.CostDiscount;
        }

        void ApplySupply(Snapshot s)
        {
            for (int i = 0; i < tiles.Count && i < s.Supply.Length; i++) tiles[i].SetCount(s.Supply[i]);
        }

        void RenderHand(string[] hand, bool drawAnim)
        {
            // 같은 카드는 재사용, 새 카드는 덱 위치에서 날아온다
            var old = new List<CardView>(handCards);
            var next = new List<CardView>();
            for (int i = 0; i < hand.Length; i++)
            {
                int found = -1;
                if (i < old.Count && old[i] != null && old[i].CardId == hand[i]) found = i;
                else found = old.FindIndex(c => c != null && c.CardId == hand[i]);
                if (found >= 0)
                {
                    next.Add(old[found]);
                    old[found] = null;
                }
                else
                {
                    var cv = CardView.Create(handArea, db[hand[i]], library.Get(hand[i]), ArtStudio.Get(hand[i], library));
                    cv.Clicked += OnHandCardClicked;
                    Vector2 from = drawAnim ? HandLocal(ScreenOf(myInfo)) : new Vector2(0, -260);
                    cv.SetBase(from, drawAnim ? 0.4f : 1f, 0, true);
                    next.Add(cv);
                }
            }
            foreach (var c in old) if (c != null) Destroy(c.gameObject);
            handCards.Clear();
            handCards.AddRange(next);
            LayoutHand(false);
        }

        void LayoutHand(bool snap)
        {
            int n = handCards.Count;
            if (n == 0) return;
            float width = handArea.rect.width > 10 ? handArea.rect.width : 1208f;
            float spacing = n > 1 ? Mathf.Min(CardView.W + 10, (width - CardView.W - 40) / (n - 1)) : 0;
            float total = spacing * (n - 1);
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) - 0.5f : 0f;
                var cv = handCards[i];
                cv.HandIndex = i;
                cv.Rt.anchorMin = cv.Rt.anchorMax = new Vector2(0.5f, 0);
                cv.Rt.pivot = new Vector2(0.5f, 0.5f);
                float x = -total / 2 + i * spacing;
                float y = CardView.H / 2 + 18 - Mathf.Abs(t) * Mathf.Abs(t) * 30f * Mathf.Min(1, n / 6f);
                cv.SetBase(new Vector2(x, y), 1f, -t * 8f * Mathf.Min(1, n / 6f), snap);
                cv.transform.SetSiblingIndex(i);
            }
        }

        Vector2 HandLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handArea, screen, null, out var local);
            // 손패 카드 앵커는 (0.5, 0) 기준
            return local + new Vector2(0, handArea.rect.height * handArea.pivot.y);
        }

        void RenderPlayArea(string[] inPlay)
        {
            bool same = inPlay.Length == playCards.Count;
            for (int i = 0; same && i < inPlay.Length; i++) same = playCards[i] != null && playCards[i].CardId == inPlay[i];
            if (same) return;
            foreach (var c in playCards) if (c != null) Destroy(c.gameObject);
            playCards.Clear();
            int n = inPlay.Length;
            const float s = 0.5f;
            float width = playArea.rect.width - 24;
            float spacing = n > 1 ? Mathf.Min(CardView.W * s + 6, (width - CardView.W * s) / (n - 1)) : 0;
            for (int i = 0; i < n; i++)
            {
                var cv = CardView.Create(playArea, db[inPlay[i]], library.Get(inPlay[i]), ArtStudio.Get(inPlay[i], library), s);
                cv.HoverScale = 2.6f;
                cv.HoverLift = 120f;
                cv.Rt.anchorMin = cv.Rt.anchorMax = new Vector2(0, 0.5f);
                cv.SetBase(new Vector2(12 + CardView.W * s / 2 + i * spacing, -8), s, 0, true);
                playCards.Add(cv);
            }
        }

        void RenderAiHand(int count)
        {
            int show = Mathf.Min(count, 12);
            while (aiBacks.Count < show)
            {
                var b = UiKit.Image("Back", aiHandRow, Palette.Hex("#5a3a6a"), UiKit.RoundedSmall);
                b.Outlined(Palette.Gold, 1.2f);
                b.raycastTarget = false;
                aiBacks.Add(b);
            }
            for (int i = 0; i < aiBacks.Count; i++)
            {
                aiBacks[i].gameObject.SetActive(i < show);
                float spacing = show > 1 ? Mathf.Min(30f, 250f / show) : 0;
                aiBacks[i].rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (show - 1) / 2f) * spacing, 0), new Vector2(26, 38));
            }
        }

        void PulseAiHand()
        {
            if (aiBacks.Count == 0) return;
            var last = aiBacks.LastOrDefault(b => b.gameObject.activeSelf);
            if (last != null) StartCoroutine(Pulse(last.rectTransform));
        }

        IEnumerator Pulse(RectTransform rt)
        {
            yield return Anim.Run(0.2f, k => { if (rt != null) rt.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(k * Mathf.PI)); });
        }

        // =====================================================================
        // 선택 가능 표시
        // =====================================================================

        void SetInteractivityOff()
        {
            foreach (var c in handCards) { c.SetHighlight(CardHighlight.None); c.SetDim(false); c.Selected = 0; }
            foreach (var c in revealCards) { c.SetHighlight(CardHighlight.None); c.Selected = 0; }
            foreach (var t in tiles) t.SetState(CardHighlight.None, false);
            playAllBtn.Interactable = nextBtn.Interactable = endTurnBtn.Interactable = false;
            confirmBtn.SetActive(false);
            yesBtn.SetActive(false);
            noBtn.SetActive(false);
            if (game != null && !game.IsOver)
                promptText.text = game.Current == Ai ? "<color=#ff9a8a>AI가 행동하는 중...</color>" : "<color=#a8977a>...</color>";
        }

        void RefreshInteractivity()
        {
            if (game == null) return;
            if (!HumanReady)
            {
                SetInteractivityOff();
                if (game.IsOver) promptText.text = "게임 종료";
                return;
            }
            var d = game.Pending;
            var hand = game.Players[Me].Hand;
            bool hasTreasure = hand.Any(id => db[id].Is(CardType.Treasure));
            confirmBtn.SetActive(d.Kind == DecisionKind.ChooseCards);
            yesBtn.SetActive(d.Kind == DecisionKind.YesNo);
            noBtn.SetActive(d.Kind == DecisionKind.YesNo);
            playAllBtn.Interactable = (d.Kind == DecisionKind.ActionPhase || d.Kind == DecisionKind.BuyPhase) && hasTreasure;
            nextBtn.Interactable = d.Kind == DecisionKind.ActionPhase;
            endTurnBtn.Interactable = d.Kind == DecisionKind.ActionPhase || d.Kind == DecisionKind.BuyPhase;

            for (int i = 0; i < handCards.Count; i++)
            {
                var cv = handCards[i];
                var def = db[cv.CardId];
                var h = CardHighlight.None;
                bool dimmed = false;
                cv.Selected = 0;
                switch (d.Kind)
                {
                    case DecisionKind.ActionPhase:
                        if (def.Is(CardType.Action)) h = game.Actions > 0 ? CardHighlight.Playable : CardHighlight.None;
                        else if (!def.Is(CardType.Treasure)) dimmed = true;
                        break;
                    case DecisionKind.BuyPhase:
                        if (def.Is(CardType.Treasure)) h = CardHighlight.Playable; else dimmed = true;
                        break;
                    case DecisionKind.ChooseCards:
                        if (d.From != Zone.Hand || !d.CardChoices.Contains(i)) dimmed = true;
                        else if (selected.Contains(i)) { h = IsTrashChoice(d) ? CardHighlight.Danger : CardHighlight.Selected; cv.Selected = 1; }
                        break;
                    default:
                        dimmed = d.Kind == DecisionKind.ChooseSupply;
                        break;
                }
                cv.SetHighlight(h);
                cv.SetDim(dimmed);
            }

            // 덱 위에서 꺼낸 카드 (망루)
            bool fromRevealed = d.Kind == DecisionKind.ChooseCards && d.From == Zone.Revealed;
            for (int i = 0; i < revealCards.Count; i++)
            {
                var cv = revealCards[i];
                bool sel = fromRevealed && selected.Contains(i);
                cv.Selected = 0;
                cv.SetHighlight(sel ? (IsTrashChoice(d) ? CardHighlight.Danger : CardHighlight.Selected) : fromRevealed ? CardHighlight.Playable : CardHighlight.None);
                cv.SetDim(false);
                if (sel) cv.Selected = 0.6f;
            }

            foreach (var t in tiles)
            {
                var h = CardHighlight.None;
                bool dimmed = false;
                if (d.Kind == DecisionKind.BuyPhase)
                {
                    if (game.CanBuy(Me, t.CardId)) h = CardHighlight.Buyable; else dimmed = true;
                }
                else if (d.Kind == DecisionKind.ChooseSupply)
                {
                    if (d.SupplyChoices.Contains(t.CardId)) h = CardHighlight.Selected; else dimmed = true;
                }
                else if (game.SupplyCount(t.CardId) <= 0) dimmed = true;
                t.SetState(h, dimmed);
            }

            string src = d.SourceCard != null && db.Contains(d.SourceCard) ? "[" + db[d.SourceCard].Name + "] " : "";
            switch (d.Kind)
            {
                case DecisionKind.ActionPhase:
                    promptText.text = "<color=#6fb0ff>행동 단계</color>  행동 카드를 눌러 사용하세요 (남은 행동 " + game.Actions + ")\n<size=15><color=#a8977a>재화를 누르면 바로 구매 단계로 넘어갑니다</color></size>";
                    break;
                case DecisionKind.BuyPhase:
                    promptText.text = "<color=#9be08a>구매 단계</color>  " +
                        (hasTreasure ? "재화를 내고 " : "") + "공급처에서 살 카드를 누르세요 (코인 " + game.Coins + ", 구매 " + game.Buys + ")";
                    if (game.CostDiscount > 0) promptText.text += "\n<size=15><color=#ff9ad0>다음 구매 비용 -" + game.CostDiscount + "</color></size>";
                    break;
                case DecisionKind.ChooseCards:
                {
                    string range = d.Min == d.Max ? d.Min + "장" : (d.Min == 0 ? "최대 " + d.Max + "장" : d.Min + "~" + d.Max + "장");
                    string who = d.Player == Me && game.Current == Ai ? "<color=#ff9a8a>공격받음!</color> " : "";
                    string where = d.From == Zone.Revealed ? "가운데 덱 위 카드에서 " : "손패에서 ";
                    promptText.text = who + src + d.Reason + "\n<size=15><color=#a8977a>" + where + range + "을 고른 뒤 확인 (선택 " + selected.Count + "장)</color></size>";
                    confirmBtn.Interactable = selected.Count >= d.Min && selected.Count <= d.Max;
                    confirmBtn.Label.text = selected.Count == 0 && d.Min == 0 ? "안 함" : "확인";
                    break;
                }
                case DecisionKind.ChooseSupply:
                    promptText.text = src + d.Reason + "\n<size=15><color=#a8977a>공급처에서 빛나는 카드를 고르세요</color></size>";
                    break;
                case DecisionKind.YesNo:
                    bool reaction = d.Op == "reactionPrompt";
                    promptText.text = (reaction ? "<color=#ff9a8a>공격받음!</color> " : src) + d.Reason;
                    yesBtn.Label.text = reaction ? "공개한다" : "사용한다";
                    noBtn.Label.text = "안 한다";
                    break;
            }
        }

        static bool IsTrashChoice(PendingDecision d) => d.Op == "trashFromHand" || d.Op == "trashFromRevealed";

        // =====================================================================
        // 기록
        // =====================================================================

        void ClearLog()
        {
            logLines.Clear();
            logText.text = "";
        }

        void Log(string line)
        {
            logLines.Add(line);
            if (logLines.Count > 160) logLines.RemoveRange(0, logLines.Count - 160);
            logText.text = string.Join("\n", logLines);
            Canvas.ForceUpdateCanvases();
            logScroll.verticalNormalizedPosition = 0f;
        }

        void LogEvent(GameEvent e, string color = null)
        {
            string c = color ?? (e.Player == Me ? "#9cc6ff" : e.Player == Ai ? "#ffa898" : "#f3e7cf");
            string text = e.Text;
            if (e.Type == GameEventType.TurnStarted) text = "<b>" + text + "</b>";
            Log("<color=" + c + ">" + text + "</color>");
        }

        // =====================================================================
        // 연출 도우미
        // =====================================================================

        static Vector2 ScreenOf(RectTransform rt)
        {
            Vector3 w = rt.TransformPoint(rt.rect.center);
            return new Vector2(w.x, w.y);
        }

        Vector2 ToFx(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxLayer, screen, null, out var local);
            return local;
        }

        Vector2 PileScreenPos(int player, bool deck)
        {
            if (player == Me) return ScreenOf(myInfo) + new Vector2(0, deck ? 20 : -20);
            return ScreenOf(aiHandRow);
        }

        CardView MakeGhost(string cardId, Vector2 screen, float scale)
        {
            var cv = CardView.Create(fxLayer, db[cardId], library.Get(cardId), ArtStudio.Get(cardId, library), scale);
            cv.Detach();
            cv.Rt.anchorMin = cv.Rt.anchorMax = new Vector2(0.5f, 0.5f);
            cv.Rt.anchoredPosition = ToFx(screen);
            cv.Rt.localScale = Vector3.one * scale;
            foreach (var g in cv.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            ghosts.Add(cv.gameObject);
            return cv;
        }

        IEnumerator MoveGhost(CardView ghost, Vector2 fromScreen, Vector2 toScreen, float s0, float s1, float dur)
        {
            Vector2 a = ToFx(fromScreen), b = ToFx(toScreen);
            yield return Anim.Run(dur, k =>
            {
                if (ghost == null) return;
                ghost.Rt.anchoredPosition = Vector2.Lerp(a, b, k) + new Vector2(0, Mathf.Sin(k * Mathf.PI) * 30f);
                ghost.Rt.localScale = Vector3.one * Mathf.Lerp(s0, s1, k);
            }, Anim.OutCubic);
        }

        void DestroyGhost(CardView ghost)
        {
            if (ghost == null) return;
            ghosts.Remove(ghost.gameObject);
            Destroy(ghost.gameObject);
        }

        void ClearGhosts()
        {
            foreach (var g in ghosts) if (g != null) Destroy(g);
            ghosts.Clear();
        }

        void FloatText(string text, Color color, Vector2 screen, int size)
        {
            var t = UiKit.Text("Float", fxLayer, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.Outlined(new Color(0, 0, 0, 0.9f), 2.5f);
            t.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ToFx(screen), new Vector2(400, 80));
            StartCoroutine(FloatRoutine(t));
        }

        IEnumerator FloatRoutine(Text t)
        {
            var start = t.rectTransform.anchoredPosition;
            float time = 0, life = 1.3f;
            Color c = t.color;
            while (time < life && t != null)
            {
                float k = time / life;
                t.rectTransform.anchoredPosition = start + new Vector2(0, 70f * Anim.OutCubic(k));
                t.rectTransform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) * 3));
                t.color = new Color(c.r, c.g, c.b, k > 0.7f ? (1 - k) / 0.3f : 1f);
                yield return null;
                time += Time.deltaTime * Mathf.Max(1f, Anim.Speed * 0.7f);
            }
            if (t != null) Destroy(t.gameObject);
        }

        IEnumerator Banner(string text, Color color, float hold)
        {
            bannerText.text = text;
            bannerText.color = color;
            bannerGroup.transform.SetAsLastSibling();
            yield return Anim.Run(0.18f, k => { bannerGroup.alpha = k; bannerGroup.transform.localScale = new Vector3(1, Mathf.Lerp(0.3f, 1, k), 1); });
            yield return Anim.Wait(hold);
            yield return Anim.Run(0.2f, k => bannerGroup.alpha = 1 - k);
            bannerGroup.alpha = 0;
        }

        void Toast(string text, Color color)
        {
            toastText.text = text;
            toastText.color = color;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine());
        }

        IEnumerator ToastRoutine()
        {
            toastGroup.alpha = 1;
            yield return new WaitForSeconds(1.6f);
            float t = 0;
            while (t < 0.3f) { toastGroup.alpha = 1 - t / 0.3f; t += Time.deltaTime; yield return null; }
            toastGroup.alpha = 0;
        }

        // =====================================================================
        // 전장 카메라 영역을 가운데 칸에 맞춘다
        // =====================================================================

        void LateUpdate()
        {
            if (field == null || field.Cam == null || fieldRect == null) return;
            var corners = new Vector3[4];
            fieldRect.GetWorldCorners(corners);
            float x0 = corners[0].x / Screen.width, y0 = corners[0].y / Screen.height;
            float x1 = corners[2].x / Screen.width, y1 = corners[2].y / Screen.height;
            // 아래쪽 상태줄 뒤까지 살짝 이어서 그린다
            field.Cam.rect = new Rect(Mathf.Clamp01(x0), Mathf.Clamp01(y0), Mathf.Clamp01(x1 - x0), Mathf.Clamp01(y1 - y0));
        }
    }
}
