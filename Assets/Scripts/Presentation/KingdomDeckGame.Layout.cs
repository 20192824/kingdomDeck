using System.Collections;
using System.Collections.Generic;
using KingdomDeck.AI;
using KingdomDeck.Engine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    /// <summary>
    /// 세로(모바일) 화면 배치. 기준 해상도 1080×1920, 가로 폭에 맞춰 늘리고 남는 높이는 가운데 전장이 쓴다.
    /// 위에서부터: 상단 바 → AI 줄 → 전장 → 상태 줄 → 버튼 줄 → 손패.
    /// 공급처와 게임 기록은 버튼으로 여닫는 서랍.
    /// </summary>
    public partial class KingdomDeckGame
    {
        // 기준 해상도(폭 1080) 단위
        const float TopH = 110f, AiH = 150f, StatusH = 196f, ButtonsH = 130f, HandH = 400f;
        const float HandScale = 1.4f;

        Canvas canvas;
        RectTransform canvasRt, safeRt, fieldRect, fxLayer, handArea, playArea, myInfo, aiHandRow, coinsChip;
        readonly HpBar[] hpBars = new HpBar[2];
        TMP_Text aiNameText, aiInfoText, myInfoText, turnText, promptText, toastText, bannerText, logText;
        TMP_Text actionsText, buysText, coinsText, discountText, supplyInfoText;
        readonly Image[] phasePills = new Image[3];
        readonly TMP_Text[] phasePillTexts = new TMP_Text[3];
        GameObject discountChip;
        UiKit.UiButton playAllBtn, nextBtn, endTurnBtn, confirmBtn, yesBtn, noBtn, skipBtn, supplyBtn, logBtn;
        readonly UiKit.UiButton[] speedBtns = new UiKit.UiButton[3];
        RectTransform supplyList, supplyDrawer, logDrawer;
        readonly List<SupplyTile> tiles = new List<SupplyTile>();
        readonly List<CardView> handCards = new List<CardView>();
        readonly List<CardView> playCards = new List<CardView>();
        readonly List<Image> aiBacks = new List<Image>();
        CardView preview;
        GameObject previewDim;
        ScrollRect logScroll;
        readonly List<string> logLines = new List<string>();
        CanvasGroup bannerGroup, toastGroup;
        Coroutine toastRoutine;
        GameObject menuPanel, resultPanel;
        TMP_Text resultTitle, resultBody;
        UiKit.UiButton continueBtn, loadMenuBtn, saveMenuBtn, instantBtn, soundBtn;
        readonly UiKit.UiButton[] diffBtns = new UiKit.UiButton[3];

        bool supplyOpen, logOpen;
        /// <summary>이번 구매 단계에 사람이 공급처를 직접 닫았는지 (그러면 다시 자동으로 열지 않음)</summary>
        bool supplyDismissed;
        /// <summary>서랍을 자동으로 열었는지 (그럴 때만 자동으로 닫는다)</summary>
        bool supplyAuto;
        Rect lastSafeArea;

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
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 폭 기준: 길쭉한 폰일수록 전장이 길어진다
            canvasRt = (RectTransform)cgo.transform;

            // 노치·홈바를 피하는 안전 영역. 모든 UI는 이 안에 만든다.
            safeRt = UiKit.Rect("SafeArea", canvasRt).Fill();
            ApplySafeArea();

            fieldRect = UiKit.Rect("FieldRect", safeRt).Place(Vector2.zero, Vector2.one,
                new Vector2(0, HandH + ButtonsH + StatusH), new Vector2(0, -(TopH + AiH)));

            BuildTopBar();
            BuildAiStrip();
            BuildFieldOverlays();
            BuildStatusRow();
            BuildButtonRow();
            BuildHandArea();
            BuildSupplyDrawer();
            BuildLogDrawer();

            fxLayer = UiKit.Rect("FX", safeRt).Fill();
            BuildBannerAndToast();
            BuildMenu();
            BuildResult();
            SetInteractivityOff();
        }

        void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;
            if (sa == lastSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafeArea = sa;
            safeRt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safeRt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safeRt.offsetMin = safeRt.offsetMax = Vector2.zero;
        }

        RectTransform Band(string name, float bottom, float height)
        {
            return UiKit.Rect(name, safeRt).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, bottom), new Vector2(0, bottom + height));
        }

        void BuildTopBar()
        {
            var bar = UiKit.Image("TopBar", safeRt, Palette.Hex("#120e0a", 0.97f));
            bar.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -TopH), Vector2.zero);
            var line = UiKit.Image("Line", bar.transform, Palette.GoldDim);
            line.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 3));

            var menu = UiKit.Button("Menu", bar.transform, "메뉴", 28, () => ShowMenu(true));
            menu.Rt.At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(116, 80));

            turnText = UiKit.Text("Turn", bar.transform, "", 28, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Fit();
            turnText.rectTransform.At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(146, 0), new Vector2(250, 96));
            turnText.lineSpacing = -10f;

            string[] names = { "행동", "구매", "정리" };
            for (int i = 0; i < 3; i++)
            {
                var pill = UiKit.Image("Phase" + i, bar.transform, Palette.Button, UiKit.RoundedSmall);
                pill.rectTransform.At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(400 + i * 106, 0), new Vector2(98, 62));
                phasePills[i] = pill;
                phasePillTexts[i] = UiKit.Text("T", pill.transform, names[i], 26, Palette.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold).Fit();
                phasePillTexts[i].rectTransform.Fill();
            }

            skipBtn = UiKit.Button("Skip", bar.transform, "건너뛰기", 24, OnSkip);
            skipBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(138, 80));
            logBtn = UiKit.Button("Log", bar.transform, "기록", 26, () => SetLogOpen(!logOpen));
            logBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-162, 0), new Vector2(100, 80));
        }

        void BuildAiStrip()
        {
            var strip = UiKit.Rect("AiStrip", safeRt).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -(TopH + AiH)), new Vector2(0, -TopH));
            var bg = UiKit.Image("Bg", strip, Palette.Hex("#17130e", 0.92f));
            bg.rectTransform.Fill();

            hpBars[Ai] = HpBar.Create(strip, "AI의 성", Palette.Enemy, false);
            hpBars[Ai].Root.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(10, 10), new Vector2(560, -10));
            aiNameText = hpBars[Ai].Root.Find("Title").GetComponent<TMP_Text>();

            var aiPanel = UiKit.Panel("AiInfo", strip, Palette.PanelDark);
            aiPanel.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 1), new Vector2(570, 10), new Vector2(-10, -10));
            aiHandRow = UiKit.Rect("AiHand", aiPanel.transform).Place(new Vector2(0, 0.42f), Vector2.one, new Vector2(10, 0), new Vector2(-10, -6));
            aiInfoText = UiKit.Text("Text", aiPanel.transform, "", 21, Palette.Text, TextAnchor.MiddleCenter).Fit();
            aiInfoText.rectTransform.Place(Vector2.zero, new Vector2(1, 0.42f), new Vector2(6, 2), new Vector2(-6, 0));
        }

        void BuildFieldOverlays()
        {
            // 사용한 카드 (전장 아래쪽)
            var pa = UiKit.Image("PlayArea", fieldRect, Palette.Hex("#120e0a", 0.5f), UiKit.Rounded);
            pa.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 8), new Vector2(-10, 152));
            playArea = pa.rectTransform;
            var lbl = UiKit.Text("Label", pa.transform, "사용한 카드", 20, Palette.TextDim, TextAnchor.LowerRight).Fit();
            lbl.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 6), new Vector2(-14, 36));

            // 덱 위에서 꺼낸 카드 (망루로 확인 / 약탈대로 공개)
            var rv = UiKit.Panel("RevealRow", fieldRect, Palette.Hex("#120e0a", 0.9f));
            rv.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(620, 380));
            revealRow = rv.rectTransform;
            revealLabel = UiKit.Text("Label", rv.transform, "", 24, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).Fit();
            revealLabel.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -50), new Vector2(-10, -8));
            revealLabel.Shadowed();
            rv.gameObject.SetActive(false);
        }

        void BuildStatusRow()
        {
            var row = Band("StatusRow", HandH + ButtonsH, StatusH);
            var bg = UiKit.Image("Bg", row, Palette.Hex("#17130e", 0.95f));
            bg.rectTransform.Fill();

            // 윗줄: 내 성 체력 · 덱 정보 · 행동/구매/코인
            hpBars[Me] = HpBar.Create(row, "내 성", Palette.Player, false);
            hpBars[Me].Root.Place(new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(8, 4), new Vector2(430, -6));

            var info = UiKit.Panel("MyInfo", row, Palette.PanelDark);
            info.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(438, 4), new Vector2(588, -6));
            myInfo = info.rectTransform;
            myInfoText = UiKit.Text("Text", info.transform, "", 19, Palette.Text, TextAnchor.MiddleCenter).Fit();
            myInfoText.rectTransform.Fill(4);

            actionsText = ResourceChip(row, 598, "행동", Palette.Hex("#6fb0ff"), out _);
            buysText = ResourceChip(row, 758, "구매", Palette.Hex("#9be08a"), out _);
            coinsText = ResourceChip(row, 918, "코인", Palette.Gold, out coinsChip);

            var dChip = UiKit.Image("Discount", coinsChip, Palette.Hex("#8a2a5a"), UiKit.RoundedSmall);
            dChip.rectTransform.At(new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(4, 0), new Vector2(76, 30));
            discountText = UiKit.Text("Value", dChip.transform, "-1", 19, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Fit();
            discountText.rectTransform.Fill();
            discountChip = dChip.gameObject;
            discountChip.SetActive(false);

            // 아랫줄: 안내 문구 + 확인/예/아니오
            var prompt = UiKit.Panel("Prompt", row, Palette.Hex("#241c14", 0.95f));
            prompt.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(8, 8), new Vector2(-8, -4));
            promptText = UiKit.Text("Text", prompt.transform, "", 24, Palette.Text, TextAnchor.MiddleLeft, FontStyle.Bold).Fit();
            promptText.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-270, 0));
            promptText.Shadowed();

            confirmBtn = UiKit.Button("Confirm", prompt.transform, "확인", 28, OnConfirm, Palette.Hex("#2f6a3a"));
            confirmBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(160, 72));
            yesBtn = UiKit.Button("Yes", prompt.transform, "공개한다", 25, () => OnYesNo(true), Palette.Hex("#2f6a3a"));
            yesBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-142, 0), new Vector2(128, 72));
            noBtn = UiKit.Button("No", prompt.transform, "안 한다", 25, () => OnYesNo(false), Palette.Hex("#6a2a22"));
            noBtn.Rt.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(124, 72));
        }

        TMP_Text ResourceChip(Transform parent, float x, string label, Color color, out RectTransform chipRt)
        {
            var chip = UiKit.Image("Chip_" + label, parent, Palette.Hex("#241c14"), UiKit.RoundedSmall);
            chip.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 1), new Vector2(x, 4), new Vector2(x + 152, -6));
            chip.Outlined(new Color(color.r, color.g, color.b, 0.55f), 2f);
            var l = UiKit.Text("Label", chip.transform, label, 20, color, TextAnchor.MiddleCenter, FontStyle.Bold).Fit();
            l.rectTransform.Place(new Vector2(0, 0.62f), Vector2.one, new Vector2(4, 0), new Vector2(-4, -2));
            var v = UiKit.Text("Value", chip.transform, "0", 40, Palette.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Fit();
            v.rectTransform.Place(Vector2.zero, new Vector2(1, 0.66f), new Vector2(4, 2), new Vector2(-4, 0));
            v.Shadowed();
            chipRt = chip.rectTransform;
            return v;
        }

        void BuildButtonRow()
        {
            var row = Band("ButtonRow", HandH, ButtonsH);
            var bg = UiKit.Image("Bg", row, Palette.Hex("#0e0b08", 0.95f));
            bg.rectTransform.Fill();
            float x = 10;
            UiKit.UiButton Btn(string name, string label, float w, System.Action a, Color c)
            {
                var b = UiKit.Button(name, row, label, 29, a, c);
                b.Rt.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(x, 14), new Vector2(x + w, -14));
                x += w + 12;
                return b;
            }
            supplyBtn = Btn("Supply", "공급처", 230, () => SetSupplyOpen(!supplyOpen, true), Palette.Hex("#4a3b26"));
            playAllBtn = Btn("PlayAll", "재화 모두 내기", 290, OnPlayAll, Palette.Hex("#6a5220"));
            nextBtn = Btn("Next", "다음 단계 ▶", 250, OnNextPhase, Palette.Hex("#2f4a6a"));
            endTurnBtn = Btn("EndTurn", "턴 종료", 230, OnEndTurn, Palette.Hex("#6a2a22"));
        }

        void BuildHandArea()
        {
            var bg = UiKit.Image("HandBg", safeRt, Palette.Hex("#0e0b08", 0.94f));
            bg.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, HandH));
            handArea = Band("Hand", 0, HandH);
        }

        // ---------------- 공급처 서랍 ----------------

        void BuildSupplyDrawer()
        {
            var d = UiKit.Panel("SupplyDrawer", safeRt, Palette.Hex("#1b1610", 0.98f));
            d.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(6, HandH + ButtonsH + 4), new Vector2(-6, -TopH - 4));
            supplyDrawer = d.rectTransform;
            d.gameObject.AddComponent<CanvasGroup>();

            var head = UiKit.Text("Head", d.transform, "공급처", 36, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -80), new Vector2(-24, -10));
            head.Shadowed();
            supplyInfoText = UiKit.Text("Info", d.transform, "", 22, Palette.Text, TextAnchor.MiddleLeft).Fit();
            supplyInfoText.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(180, -80), new Vector2(-170, -10));
            var close = UiKit.Button("Close", d.transform, "닫기", 26, () => SetSupplyOpen(false, true));
            close.Rt.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(140, 66));
            var hint = UiKit.Text("Hint", d.transform, "누르면 구매 · 길게 누르면 카드 확대", 19, Palette.TextDim, TextAnchor.MiddleLeft).Fit();
            hint.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -112), new Vector2(-24, -80));

            var view = UiKit.Image("View", d.transform, Palette.Hex("#120e0a", 0.6f), UiKit.RoundedSmall);
            view.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -118));
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            supplyList = UiKit.Rect("Content", view.transform);
            supplyList.anchorMin = new Vector2(0, 1);
            supplyList.anchorMax = new Vector2(1, 1);
            supplyList.pivot = new Vector2(0.5f, 1);
            supplyList.offsetMin = Vector2.zero;
            supplyList.offsetMax = Vector2.zero;
            scroll.content = supplyList;
            scroll.viewport = view.rectTransform;

            d.gameObject.SetActive(false);
        }

        void BuildSupply()
        {
            foreach (var t in tiles) if (t != null) Destroy(t.gameObject);
            tiles.Clear();
            foreach (Transform c in supplyList) Destroy(c.gameObject);

            const float tileScale = 1.4f, tileW = 360f, tileH = 70f, gapY = 12f, colW = 516f;
            float y = 8;
            void Header(string text)
            {
                var t = UiKit.Text("Section", supplyList, text, 24, Palette.TextDim, TextAnchor.LowerLeft, FontStyle.Bold);
                t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -y - 40), new Vector2(0, -y));
                y += 46;
            }
            Header("기본 카드");
            int i = 0, col = 0;
            foreach (var id in game.SupplyOrder)
            {
                if (i == 7) { if (col == 1) { y += tileH * tileScale + gapY; col = 0; } Header("왕국 카드"); }
                var def = db[id];
                var tile = SupplyTile.Create(supplyList, def, library.Get(id), ArtStudio.Get(id, library));
                tile.Rt.anchorMin = tile.Rt.anchorMax = new Vector2(0, 1);
                tile.Rt.pivot = new Vector2(0, 1);
                tile.Rt.sizeDelta = new Vector2(tileW, tileH);
                tile.Rt.localScale = Vector3.one * tileScale;
                tile.Rt.anchoredPosition = new Vector2(10 + col * colW, -y);
                tile.Clicked += OnSupplyClicked;
                tile.Hovered += OnSupplyHover;
                tile.LongPressed += (t, on) => { if (on) ShowPreview(t.CardId); else HidePreview(); };
                tiles.Add(tile);
                col++;
                if (col == 2) { col = 0; y += tileH * tileScale + gapY; }
                i++;
            }
            if (col == 1) y += tileH * tileScale + gapY;
            supplyList.sizeDelta = new Vector2(0, y + 10);
        }

        /// <summary>공급처 서랍 열기/닫기. byUser=true면 사람이 직접 누른 것 (자동 열기를 멈춘다)</summary>
        void SetSupplyOpen(bool open, bool byUser = false)
        {
            if (byUser) { supplyDismissed = !open; supplyAuto = false; }
            if (open == supplyOpen) return;
            supplyOpen = open;
            if (open) SetLogOpen(false);
            AnimateDrawer(supplyDrawer, open);
            supplyBtn.Label.text = open ? "공급처 닫기" : "공급처";
        }

        void SetLogOpen(bool open)
        {
            if (open == logOpen) return;
            logOpen = open;
            if (open && supplyOpen) SetSupplyOpen(false, true);
            AnimateDrawer(logDrawer, open);
            logBtn.Label.text = open ? "닫기" : "기록";
            if (open) { Canvas.ForceUpdateCanvases(); logScroll.verticalNormalizedPosition = 0f; }
        }

        readonly Dictionary<RectTransform, Coroutine> drawerAnims = new Dictionary<RectTransform, Coroutine>();

        /// <summary>새 게임/불러오기 때: 애니메이션 없이 서랍을 모두 닫힌 상태로</summary>
        void ResetDrawers()
        {
            drawerAnims.Clear();
            foreach (var d in new[] { supplyDrawer, logDrawer })
            {
                var cg = d.GetComponent<CanvasGroup>();
                cg.alpha = 0;
                cg.blocksRaycasts = false;
                d.localScale = Vector3.one;
                d.gameObject.SetActive(false);
            }
            supplyOpen = logOpen = supplyAuto = supplyDismissed = false;
            supplyBtn.Label.text = "공급처";
            logBtn.Label.text = "기록";
        }

        void AnimateDrawer(RectTransform drawer, bool open)
        {
            // 열기/닫기가 빠르게 겹치면 앞 애니메이션을 멈추고 새로 시작 (반투명으로 남는 문제 방지)
            if (drawerAnims.TryGetValue(drawer, out var running) && running != null) StopCoroutine(running);
            drawerAnims[drawer] = StartCoroutine(SlideDrawer(drawer, open));
        }

        IEnumerator SlideDrawer(RectTransform drawer, bool open)
        {
            var cg = drawer.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = open;
            if (open) { drawer.gameObject.SetActive(true); drawer.SetAsLastSibling(); fxLayer.SetAsLastSibling(); }
            float t = 0, dur = 0.18f;
            float startAlpha = cg.alpha;
            float endAlpha = open ? 1f : 0f;
            while (t < dur)
            {
                float k = Anim.OutCubic(t / dur);
                float a = Mathf.Lerp(startAlpha, endAlpha, k);
                cg.alpha = a;
                drawer.localScale = new Vector3(1, Mathf.Lerp(0.94f, 1f, a), 1);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            cg.alpha = open ? 1 : 0;
            drawer.localScale = Vector3.one;
            if (!open) drawer.gameObject.SetActive(false);
        }

        // ---------------- 기록 서랍 ----------------

        void BuildLogDrawer()
        {
            var d = UiKit.Panel("LogDrawer", safeRt, Palette.Hex("#1b1610", 0.98f));
            d.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(6, HandH + ButtonsH + 4), new Vector2(-6, -TopH - 4));
            logDrawer = d.rectTransform;
            d.gameObject.AddComponent<CanvasGroup>();
            var head = UiKit.Text("Head", d.transform, "게임 기록", 36, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -80), new Vector2(-24, -10));
            var close = UiKit.Button("Close", d.transform, "닫기", 26, () => SetLogOpen(false));
            close.Rt.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(140, 66));

            var view = UiKit.Image("View", d.transform, Palette.Hex("#120e0a", 0.6f), UiKit.RoundedSmall);
            view.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -90));
            view.gameObject.AddComponent<RectMask2D>();
            logScroll = view.gameObject.AddComponent<ScrollRect>();
            logScroll.horizontal = false;
            logScroll.movementType = ScrollRect.MovementType.Clamped;
            logScroll.scrollSensitivity = 40f;
            var content = UiKit.Rect("Content", view.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(14, 0);
            content.offsetMax = new Vector2(-14, 0);
            logText = UiKit.Text("Log", content, "", 23, Palette.Text, TextAnchor.UpperLeft);
            logText.rectTransform.Fill();
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.padding = new RectOffset(0, 0, 8, 8);
            logScroll.content = content;
            logScroll.viewport = view.rectTransform;
            d.gameObject.SetActive(false);
        }

        // ---------------- 카드 크게 보기 (길게 누르기 / 마우스 올리기) ----------------

        void ShowPreview(string cardId)
        {
            HidePreview();
            var dim = UiKit.Image("PreviewDim", fxLayer, new Color(0, 0, 0, 0.55f));
            dim.rectTransform.Fill();
            dim.raycastTarget = false;
            previewDim = dim.gameObject;
            preview = CardView.Create(fxLayer, db[cardId], library.Get(cardId), ArtStudio.Get(cardId, library), 2.6f, true);
            preview.Detach();
            preview.Rt.anchorMin = preview.Rt.anchorMax = new Vector2(0.5f, 0.5f);
            preview.Rt.anchoredPosition = new Vector2(0, 120);
            preview.Rt.localScale = Vector3.one * 2.6f;
            foreach (var g in preview.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            Sfx.Play(Sfx.Card, 0.4f, 1.3f);
        }

        void HidePreview()
        {
            if (preview != null) Destroy(preview.gameObject);
            if (previewDim != null) Destroy(previewDim);
            preview = null;
            previewDim = null;
        }

        void OnSupplyHover(SupplyTile t, bool on)
        {
            // 마우스가 있는 환경(에디터/PC)에서만: 올리면 크게 보기
            if (Application.isMobilePlatform) return;
            if (on) ShowPreview(t.CardId); else HidePreview();
        }

        // ---------------- 배너 / 알림 / 메뉴 / 결과 ----------------

        void BuildBannerAndToast()
        {
            var b = UiKit.Image("Banner", fieldRect, Palette.Hex("#000000", 0.55f));
            b.rectTransform.At(new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 150));
            b.raycastTarget = false;
            bannerGroup = b.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0;
            bannerGroup.blocksRaycasts = false;
            bannerText = UiKit.Text("Text", b.transform, "", 72, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold).Fit();
            bannerText.rectTransform.Fill();
            bannerText.Outlined(new Color(0, 0, 0, 0.9f), 3f);

            var t = UiKit.Image("Toast", safeRt, Palette.Hex("#000000", 0.8f), UiKit.Rounded);
            t.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, HandH + ButtonsH + StatusH + 20), new Vector2(980, 76));
            t.raycastTarget = false;
            toastGroup = t.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;
            toastGroup.blocksRaycasts = false;
            toastText = UiKit.Text("Text", t.transform, "", 27, Palette.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Fit();
            toastText.rectTransform.Fill(8);
        }

        void BuildMenu()
        {
            var dim = UiKit.Image("Menu", canvasRt, Palette.Hex("#000000", 0.78f));
            dim.rectTransform.Fill();
            menuPanel = dim.gameObject;

            var p = UiKit.Panel("Box", dim.transform, Palette.Hex("#231c14", 0.98f));
            p.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 1480));
            float y = -40;
            TMP_Text Line(string name, string text, int size, Color color, float h, TextAnchor a = TextAnchor.MiddleCenter, FontStyle st = FontStyle.Normal)
            {
                var t = UiKit.Text(name, p.transform, text, size, color, a, st);
                t.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, y - h), new Vector2(-60, y));
                y -= h;
                return t;
            }
            Line("Title", "왕국의 덱", 112, Palette.Gold, 160, TextAnchor.MiddleCenter, FontStyle.Bold).Outlined(Color.black);
            Line("Sub", "병기를 사들여 상대의 성을 무너뜨려라!", 32, Palette.Text, 60);
            y -= 20;
            Line("Rules",
                "· 매 턴 <b>행동 → 구매 → 정리</b>. 손패 5장으로 시작합니다.\n" +
                "· <color=#ff9a8a><b>병기</b></color>(궁수대·투석기·드래곤 등)를 사는 순간 상대 성을 공격합니다. 하지만 병기는 덱 안에서는 쓸모없는 카드가 됩니다. 언제부터 살지가 핵심!\n" +
                "· <color=#c8a0ff><b>배신자</b></color>는 내 덱에 있는 동안 내 성 체력을 1 깎습니다.\n" +
                "· 상대 성의 체력을 0으로 만들면 승리! (시작 체력 30) 공급 더미 3개가 비어도 끝나며, 그때는 성 체력이 더 많은 쪽이 승리.",
                25, Palette.Text, 330, TextAnchor.UpperLeft);
            y -= 10;

            Line("DiffLabel", "AI 난이도", 30, Palette.Gold, 56, TextAnchor.MiddleLeft, FontStyle.Bold);
            string[] dn = { "쉬움", "보통", "어려움" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                diffBtns[i] = UiKit.Button("Diff" + i, p.transform, dn[i], 32, () => { difficulty = (Difficulty)k; SaveSettings(); UpdateMenuButtons(); });
                diffBtns[i].Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60 + i * 300, y - 8), new Vector2(280, 96));
            }
            y -= 120;
            instantBtn = UiKit.Button("Instant", p.transform, "", 27, () => { instantEnd = !instantEnd; SaveSettings(); UpdateMenuButtons(); });
            instantBtn.Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, y), new Vector2(880, 86));
            y -= 104;
            soundBtn = UiKit.Button("Sound", p.transform, "", 27, () => { Sfx.Muted = !Sfx.Muted; SaveSettings(); UpdateMenuButtons(); });
            soundBtn.Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, y), new Vector2(330, 86));
            var sl = UiKit.Text("SpeedLabel", p.transform, "연출 속도", 26, Palette.TextDim, TextAnchor.MiddleRight);
            sl.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(400, y), new Vector2(120, 86));
            float[] speeds = { 1f, 2f, 4f };
            for (int i = 0; i < 3; i++)
            {
                float sp = speeds[i];
                speedBtns[i] = UiKit.Button("Speed" + i, p.transform, sp + "x", 28, () => SetSpeed(sp));
                speedBtns[i].Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(536 + i * 138, y), new Vector2(128, 86));
            }
            y -= 104;
            saveMenuBtn = UiKit.Button("Save", p.transform, "저장", 28, OnSave);
            saveMenuBtn.Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(60, y), new Vector2(430, 86));
            loadMenuBtn = UiKit.Button("Load", p.transform, "불러오기", 28, OnLoad);
            loadMenuBtn.Rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(510, y), new Vector2(430, 86));

            var newBtn = UiKit.Button("New", p.transform, "새 게임", 40, NewGame, Palette.Hex("#6a5220"));
            newBtn.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-225, 70), new Vector2(430, 130));
            continueBtn = UiKit.Button("Continue", p.transform, "계속하기", 38, () => ShowMenu(false), Palette.Hex("#2f4a6a"));
            continueBtn.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(225, 70), new Vector2(430, 130));
            UpdateSpeedButtons();
        }

        void BuildResult()
        {
            var dim = UiKit.Image("Result", canvasRt, Palette.Hex("#000000", 0.65f));
            dim.rectTransform.Fill();
            resultPanel = dim.gameObject;
            var p = UiKit.Panel("Box", dim.transform, Palette.Hex("#231c14", 0.98f));
            p.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(980, 900));
            resultTitle = UiKit.Text("Title", p.transform, "", 120, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            resultTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -200), new Vector2(0, -30));
            resultTitle.Outlined(Color.black);
            resultBody = UiKit.Text("Body", p.transform, "", 32, Palette.Text, TextAnchor.UpperCenter);
            resultBody.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(40, 190), new Vector2(-40, -220));
            var again = UiKit.Button("Again", p.transform, "다시 하기", 38, NewGame, Palette.Hex("#6a5220"));
            again.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-220, 50), new Vector2(400, 120));
            var menu = UiKit.Button("Menu", p.transform, "메뉴로", 38, () => { resultPanel.SetActive(false); ShowMenu(true); });
            menu.Rt.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(220, 50), new Vector2(400, 120));
            resultPanel.SetActive(false);
        }

        // ---------------- 덱 위 공개 카드 ----------------

        RectTransform revealRow;
        TMP_Text revealLabel;
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
            const float scale = 1.35f;
            int n = view.Revealed.Length;
            for (int i = 0; i < n; i++)
            {
                string id = view.Revealed[i];
                var cv = CardView.Create(revealRow, hidden ? null : db[id], hidden ? null : library.Get(id), hidden ? null : ArtStudio.Get(id, library), scale);
                cv.Rt.anchorMin = cv.Rt.anchorMax = new Vector2(0.5f, 0.5f);
                cv.SetBase(new Vector2((i - (n - 1) / 2f) * (CardView.W * scale + 20), -24), scale, 0, true);
                cv.HoverScale = 1.2f;
                cv.HoverLift = 20f;
                if (p == Me) cv.Clicked += OnRevealClicked;
                else cv.SetInteractable(false);
                if (!hidden) cv.LongPressed += (c, on) => { if (on) ShowPreview(c.CardId); else HidePreview(); };
                revealCards.Add(cv);
            }
            revealRow.sizeDelta = new Vector2(Mathf.Max(620, n * (CardView.W * scale + 20) + 50), 380);
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

        /// <summary>구매/공급처 선택 때 서랍을 자동으로 열고, 끝나면 닫는다</summary>
        void UpdateSupplyDrawer()
        {
            if (game == null) return;
            var d = game.Pending;
            bool choose = HumanReady && d.Kind == DecisionKind.ChooseSupply;
            bool buy = HumanReady && d.Kind == DecisionKind.BuyPhase && game.Buys > 0 && game.Coins > 0;
            if (choose || (buy && !supplyDismissed))
            {
                if (!supplyOpen) { SetSupplyOpen(true); supplyAuto = true; }
            }
            else if (supplyOpen && supplyAuto)
            {
                // 자동으로 연 서랍은 구매가 끝나면(구매 0, 턴 넘어감) 자동으로 닫는다
                SetSupplyOpen(false);
                supplyAuto = false;
            }

            int buyable = 0;
            if (HumanReady && d.Kind == DecisionKind.BuyPhase)
                foreach (var t in tiles) if (game.CanBuy(Me, t.CardId) && db[t.CardId].Cost > 0) buyable++;
            if (!supplyOpen) supplyBtn.Label.text = buyable > 0 ? "공급처 <color=#ffd860>(" + buyable + ")</color>" : "공급처";
            if (HumanReady && d.Kind == DecisionKind.ChooseSupply)
                supplyInfoText.text = "<color=#9be08a>" + d.Reason + "</color>";
            else if (HumanReady && d.Kind == DecisionKind.BuyPhase)
                supplyInfoText.text = "코인 <color=#ffd860><b>" + game.Coins + "</b></color>  ·  구매 <b>" + game.Buys + "</b>" + (game.CostDiscount > 0 ? "  ·  <color=#ff9ad0>다음 구매 -" + game.CostDiscount + "</color>" : "");
            else
                supplyInfoText.text = "<color=#a8977a>지금은 살 수 없어요</color>";
        }
    }
}
