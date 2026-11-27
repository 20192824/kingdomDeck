using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomDeck.AI;
using KingdomDeck.Engine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    /// <summary>화면 구성과 그리기</summary>
    public partial class KingdomDeckGame
    {
        // =====================================================================
        // 메뉴 / 결과
        // =====================================================================

        void ShowMenu(bool on)
        {
            if (!on && game == null) return;
            menuOpen = on;
            menuPanel.SetActive(on);
            if (on) { menuPanel.transform.SetAsLastSibling(); UpdateMenuButtons(); HidePreview(); }
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
            saveMenuBtn.Interactable = game != null && !game.IsOver;
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
                "<size=26><color=#a8977a>진행한 턴: 나 " + game.Players[Me].TurnsTaken + " / AI " + game.Players[Ai].TurnsTaken +
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
            myInfoText.text = "덱 <b>" + me.DeckCount + "</b>\n버림 <b>" + me.DiscardCount + "</b>\n<color=#a8977a>전체 " + me.TotalCards + "</color>";
            aiInfoText.text = "손패 " + ai.Hand.Length + "  ·  덱 " + ai.DeckCount + "  ·  버림 " + ai.DiscardCount + "  ·  전체 " + ai.TotalCards;
            RenderAiHand(ai.Hand.Length);
            RenderRevealed(s);
        }

        void ApplyTopBar(Snapshot s)
        {
            string who = s.Current == Me ? "<color=#8fc0ff>나의 턴</color>" : "<color=#ff9a8a>AI의 턴</color>";
            turnText.text = "턴 " + s.TurnNumber + "\n" + who;
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
                    cv.LongPressed += (c, on) => { if (on) ShowPreview(c.CardId); else HidePreview(); };
                    cv.HoverLift = 150f;
                    cv.HoverScale = 1.35f;
                    Vector2 from = drawAnim ? HandLocal(ScreenOf(myInfo)) : new Vector2(0, -260);
                    cv.SetBase(from, drawAnim ? 0.4f : HandScale, 0, true);
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
            float width = handArea.rect.width > 10 ? handArea.rect.width : 1080f;
            float cw = CardView.W * HandScale;
            float spacing = n > 1 ? Mathf.Min(cw + 8, (width - cw - 24) / (n - 1)) : 0;
            float total = spacing * (n - 1);
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? i / (float)(n - 1) - 0.5f : 0f;
                var cv = handCards[i];
                cv.HandIndex = i;
                cv.Rt.anchorMin = cv.Rt.anchorMax = new Vector2(0.5f, 0);
                cv.Rt.pivot = new Vector2(0.5f, 0.5f);
                float x = -total / 2 + i * spacing;
                float y = HandH / 2f - 4 - Mathf.Abs(t) * Mathf.Abs(t) * 40f * Mathf.Min(1, n / 5f);
                cv.SetBase(new Vector2(x, y), HandScale, -t * 10f * Mathf.Min(1, n / 5f), snap);
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
            const float s = 0.6f;
            float width = playArea.rect.width - 24;
            float spacing = n > 1 ? Mathf.Min(CardView.W * s + 6, (width - CardView.W * s) / (n - 1)) : 0;
            for (int i = 0; i < n; i++)
            {
                var cv = CardView.Create(playArea, db[inPlay[i]], library.Get(inPlay[i]), ArtStudio.Get(inPlay[i], library), s);
                cv.HoverScale = 1f;
                cv.HoverLift = 0f;
                cv.LongPressed += (c, on) => { if (on) ShowPreview(c.CardId); else HidePreview(); };
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
                float spacing = show > 1 ? Mathf.Min(40f, 400f / show) : 0;
                aiBacks[i].rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (show - 1) / 2f) * spacing, 0), new Vector2(36, 52));
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
            RefreshPrompt();
            UpdateSupplyDrawer();
        }

        void RefreshPrompt()
        {
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
                    promptText.text = "<color=#6fb0ff>행동 단계</color>  행동 카드를 눌러 사용하세요 (남은 행동 " + game.Actions + ")\n<size=19><color=#a8977a>재화를 누르면 바로 구매 단계로 넘어갑니다</color></size>";
                    break;
                case DecisionKind.BuyPhase:
                    promptText.text = "<color=#9be08a>구매 단계</color>  " +
                        (hasTreasure ? "재화를 내고 " : "") + "공급처에서 살 카드를 누르세요 (코인 " + game.Coins + ", 구매 " + game.Buys + ")";
                    if (game.CostDiscount > 0) promptText.text += "\n<size=19><color=#ff9ad0>다음 구매 비용 -" + game.CostDiscount + "</color></size>";
                    break;
                case DecisionKind.ChooseCards:
                {
                    string range = d.Min == d.Max ? d.Min + "장" : (d.Min == 0 ? "최대 " + d.Max + "장" : d.Min + "~" + d.Max + "장");
                    string who = d.Player == Me && game.Current == Ai ? "<color=#ff9a8a>공격받음!</color> " : "";
                    string where = d.From == Zone.Revealed ? "가운데 덱 위 카드에서 " : "손패에서 ";
                    promptText.text = who + src + d.Reason + "\n<size=19><color=#a8977a>" + where + range + "을 고른 뒤 확인 (선택 " + selected.Count + "장)</color></size>";
                    confirmBtn.Interactable = selected.Count >= d.Min && selected.Count <= d.Max;
                    confirmBtn.Label.text = selected.Count == 0 && d.Min == 0 ? "안 함" : "확인";
                    break;
                }
                case DecisionKind.ChooseSupply:
                    promptText.text = src + d.Reason + "\n<size=19><color=#a8977a>공급처에서 빛나는 카드를 고르세요</color></size>";
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

        IEnumerator FloatRoutine(TMP_Text t)
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

        Image flashImg;

        /// <summary>화면 전체가 잠깐 번쩍 (피격 타격감)</summary>
        void Flash(Color c)
        {
            if (Anim.Skipping) return;
            if (flashImg == null)
            {
                flashImg = UiKit.Image("Flash", canvasRt, Color.clear);
                flashImg.rectTransform.Fill();
                flashImg.raycastTarget = false;
            }
            flashImg.transform.SetAsLastSibling();
            StartCoroutine(FlashRoutine(c));
        }

        IEnumerator FlashRoutine(Color c)
        {
            float t = 0, dur = 0.28f;
            while (t < dur)
            {
                float k = t / dur;
                flashImg.color = new Color(c.r, c.g, c.b, c.a * (1 - k) * (1 - k));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            flashImg.color = Color.clear;
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
            ApplySafeArea();
            var corners = new Vector3[4];
            fieldRect.GetWorldCorners(corners);
            float x0 = corners[0].x / Screen.width, y0 = corners[0].y / Screen.height;
            float x1 = corners[2].x / Screen.width, y1 = corners[2].y / Screen.height;
            // 아래쪽 상태줄 뒤까지 살짝 이어서 그린다
            field.Cam.rect = new Rect(Mathf.Clamp01(x0), Mathf.Clamp01(y0), Mathf.Clamp01(x1 - x0), Mathf.Clamp01(y1 - y0));
            field.FitAspect(field.Cam.aspect);
        }
    }
}
