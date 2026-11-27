using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomDeck.AI;
using KingdomDeck.Engine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace KingdomDeck.Presentation
{
    /// <summary>
    /// 왕국의 덱 화면 전체를 만들고 진행하는 컴포넌트.
    /// 엔진 이벤트를 큐에 쌓아 하나씩 연출하고, 연출이 끝난 뒤에만 사람/AI가 다음 선택을 한다.
    /// </summary>
    public partial class KingdomDeckGame : MonoBehaviour
    {
        [SerializeField] TextAsset cardsJson;
        [SerializeField] CardVisualLibrary library;
        [Tooltip("테스트용: 내 쪽도 AI(어려움)가 대신 둔다")]
        [SerializeField] bool autoPlayMe;
        Bot demoBot;

        const int Me = 0, Ai = 1;
        static string SavePath => Path.Combine(Application.persistentDataPath, "kingdom_deck_save.json");

        CardDatabase db;
        Game game;
        Bot bot;
        Difficulty difficulty = Difficulty.Normal;
        bool instantEnd = true;

        Battlefield field;
        readonly Queue<GameEvent> queue = new Queue<GameEvent>();
        readonly HashSet<int> consumed = new HashSet<int>();
        bool playing;
        bool skipAiTurn;
        bool skipQueue;
        float aiTimer;
        Snapshot shown;
        readonly int[] shownHp = { 50, 50 };
        readonly List<int> selected = new List<int>();
        readonly List<GameObject> ghosts = new List<GameObject>();
        bool menuOpen = true;
        bool needsRefresh;

        // =====================================================================
        // 시작
        // =====================================================================

        void Awake()
        {
            Application.targetFrameRate = 60;
            if (cardsJson == null) cardsJson = Resources.Load<TextAsset>("cards");
            if (cardsJson == null)
            {
                Debug.LogError("cards.json이 연결되지 않았습니다 (KingdomDeckGame.cardsJson)");
                enabled = false;
                return;
            }
            db = CardDatabase.FromJson(cardsJson.text);
            if (library == null) library = ScriptableObject.CreateInstance<CardVisualLibrary>();

            LoadSettings();
            EnsureEventSystem();

            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" }.GetComponent<Camera>();
            // 전장 카메라는 가운데 칸만 그리므로, 나머지 화면을 지우는 배경 카메라를 하나 둔다
            var bgCam = new GameObject("BackgroundCamera").AddComponent<Camera>();
            bgCam.depth = cam.depth - 1;
            bgCam.cullingMask = 0;
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = Palette.Bg;
            field = new GameObject("Battlefield").AddComponent<Battlefield>();
            field.Init(cam, library);

            BuildUi();
            ShowMenu(true);
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        void LoadSettings()
        {
            difficulty = (Difficulty)PlayerPrefs.GetInt("kd.difficulty", (int)Difficulty.Normal);
            instantEnd = PlayerPrefs.GetInt("kd.instantEnd", 1) == 1;
            Anim.Speed = PlayerPrefs.GetFloat("kd.speed", 1f);
            Sfx.Muted = PlayerPrefs.GetInt("kd.muted", 0) == 1;
        }

        void SaveSettings()
        {
            PlayerPrefs.SetInt("kd.difficulty", (int)difficulty);
            PlayerPrefs.SetInt("kd.instantEnd", instantEnd ? 1 : 0);
            PlayerPrefs.SetFloat("kd.speed", Anim.Speed);
            PlayerPrefs.SetInt("kd.muted", Sfx.Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void NewGame()
        {
            var opt = new GameOptions
            {
                Seed = DateTime.Now.Ticks & 0x7FFFFFFF,
                InstantCastleEnd = instantEnd,
                PlayerNames = new[] { "플레이어", "AI" },
            };
            BeginGame(new Game(db, opt), true);
        }

        void BeginGame(Game g, bool start)
        {
            StopAllCoroutines();
            Anim.Skipping = false;
            ClearGhosts();
            field.ClearTemps();
            queue.Clear();
            consumed.Clear();
            selected.Clear();
            playing = false;
            skipAiTurn = false;
            skipQueue = false;
            foreach (Transform c in fxLayer) Destroy(c.gameObject);
            preview = null;
            bannerGroup.alpha = 0;
            ClearLog();

            game = g;
            bot = new Bot(difficulty);
            game.EventRaised += e => queue.Enqueue(e);
            if (start) game.Start();

            BuildSupply();
            aiNameText.text = "AI (" + Bot.LevelName(difficulty) + ")";
            var snap = game.TakeSnapshot();
            if (start)
            {
                // 첫 이벤트들의 스냅샷 전에 빈 손으로 시작
                shown = null;
                for (int p = 0; p < 2; p++) { shownHp[p] = 50; hpBars[p].Set(50, true); field.SetCastleHp(p, 50); }
                RenderHand(new string[0], false);
                RenderPlayArea(new string[0]);
            }
            else
            {
                SyncAll(snap);
                Log("<color=#e8c25a>저장된 게임을 불러왔습니다.</color>");
            }
            ShowMenu(false);
            resultPanel.SetActive(false);
            needsRefresh = true;
        }

        // =====================================================================
        // 진행
        // =====================================================================

        void Update()
        {
            foreach (var b in hpBars) b?.Tick(Time.deltaTime);
            HandleKeys();
            if (game == null || menuOpen) return;

            if (!playing && queue.Count > 0)
            {
                StartCoroutine(PlayQueue());
                return;
            }
            if (playing || game.IsOver) return;

            var d = game.Pending;
            if (d == null) return;
            if (d.Player == Ai || (autoPlayMe && d.Player == Me))
            {
                var agent = d.Player == Ai ? bot : (demoBot ?? (demoBot = new Bot(Difficulty.Hard)));
                aiTimer += Time.deltaTime * Anim.Speed;
                float think = skipAiTurn ? 0f : (d.Kind == DecisionKind.BuyPhase && game.Players[Ai].Hand.Any(id => db[id].Is(CardType.Treasure)) ? 0.25f : 0.55f);
                if (aiTimer >= think)
                {
                    aiTimer = 0;
                    var c = agent.Decide(game, d);
                    if (!game.Submit(c))
                    {
                        Debug.LogWarning("AI 명령 실패: " + c + " — " + game.LastError);
                        if (!game.Submit(Command.EndPhase())) game.Submit(Command.Choose());
                    }
                }
                if (needsRefresh) { RefreshInteractivity(); needsRefresh = false; }
                return;
            }
            aiTimer = 0;
            if (needsRefresh) { RefreshInteractivity(); needsRefresh = false; }
        }

        bool HumanReady => game != null && !menuOpen && !playing && queue.Count == 0 && !game.IsOver && game.Pending != null && game.Pending.Player == Me;

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (menuOpen && game != null && !game.IsOver) ShowMenu(false);
                else ShowMenu(true);
            }
            if (menuOpen) return;
            if (kb.spaceKey.wasPressedThisFrame && HumanReady)
            {
                if (game.Pending.Kind == DecisionKind.ActionPhase) OnNextPhase();
                else if (game.Pending.Kind == DecisionKind.BuyPhase) OnEndTurn();
            }
            if (kb.enterKey.wasPressedThisFrame && HumanReady && game.Pending.Kind == DecisionKind.ChooseCards && confirmBtn.Interactable) OnConfirm();
        }

        void Submit(Command c)
        {
            if (!HumanReady) return;
            if (!game.Submit(c))
            {
                Toast(game.LastError ?? "할 수 없습니다", Palette.Bad);
                Sfx.Play(Sfx.Block, 0.4f, 1.6f);
                return;
            }
            selected.Clear();
            needsRefresh = true;
            SetInteractivityOff();
        }

        IEnumerator PlayQueue()
        {
            playing = true;
            SetInteractivityOff();
            while (queue.Count > 0)
            {
                var e = queue.Dequeue();
                if (consumed.Remove(e.Seq)) { if (e.Snap != null) ApplySnapshot(e.Snap); continue; }
                Anim.Skipping = skipQueue || skipAiTurn;
                yield return PlayEvent(e);
                if (e.Snap != null) ApplySnapshot(e.Snap);
            }
            Anim.Skipping = false;
            skipQueue = false;
            ClearGhosts();
            if (game.IsOver == false && game.Pending != null && game.Pending.Player == Me) skipAiTurn = false;
            // 연출이 다 끝나면 화면을 엔진 상태와 완전히 맞춘다
            SyncAll(game.TakeSnapshot());
            playing = false;
            needsRefresh = true;
            if (game.IsOver) ShowResult();
        }

        void SyncAll(Snapshot s)
        {
            ApplySnapshot(s);
            for (int p = 0; p < 2; p++)
            {
                if (shownHp[p] != s.Players[p].Hp) hpBars[p].Set(s.Players[p].Hp, false);
                shownHp[p] = s.Players[p].Hp;
                field.SetCastleHp(p, s.Players[p].Hp);
            }
        }

        // =====================================================================
        // 이벤트 연출
        // =====================================================================

        IEnumerator PlayEvent(GameEvent e)
        {
            string name = e.CardId != null && db.Contains(e.CardId) ? db[e.CardId].Name : "";
            switch (e.Type)
            {
                case GameEventType.GameStarted:
                    Log("<color=#e8c25a>" + e.Text + "</color>");
                    Log("<color=#a8977a>이번 왕국 카드: " + string.Join(", ", game.Kingdom.Select(id => db[id].Name)) + "</color>");
                    yield return Banner("전투 개시!", Palette.Gold, 0.9f);
                    break;

                case GameEventType.TurnStarted:
                    Log("");
                    LogEvent(e);
                    if (e.Player == Me) skipAiTurn = false;
                    Sfx.Play(Sfx.Chime, 0.6f, e.Player == Me ? 1.2f : 0.9f);
                    if (e.Snap != null) ApplySnapshot(e.Snap);
                    yield return Banner(e.Player == Me ? "나의 턴" : "AI의 턴", e.Player == Me ? Palette.Player : Palette.Enemy, 0.75f);
                    if (e.Player == Me) AutoSave();
                    break;

                case GameEventType.PhaseChanged:
                    if (e.Phase == Phase.Buy) LogEvent(e);
                    break;

                case GameEventType.CardDrawn:
                    if (e.Player == Me)
                    {
                        if (e.Snap != null) ApplySnapshot(e.Snap, drawAnim: true);
                        Sfx.Play(Sfx.Card, 0.5f, UnityEngine.Random.Range(0.9f, 1.15f));
                        yield return Anim.Wait(0.09f);
                    }
                    else
                    {
                        if (e.Snap != null) ApplySnapshot(e.Snap);
                        PulseAiHand();
                        yield return Anim.Wait(0.03f);
                    }
                    break;

                case GameEventType.Shuffled:
                    LogEvent(e);
                    Sfx.Play(Sfx.Shuffle, 0.7f);
                    FloatText("섞기!", Palette.Text, PileScreenPos(e.Player, true), 24);
                    yield return Anim.Wait(0.3f);
                    break;

                case GameEventType.CardPlayed:
                    LogEvent(e);
                    yield return PlayCardAnim(e);
                    break;

                case GameEventType.ResourcesChanged:
                    if (e.Snap != null) ApplyTopBar(e.Snap);
                    break;

                case GameEventType.CardBought:
                    LogEvent(e);
                    Sfx.Play(Sfx.Coin, 0.6f, 0.8f);
                    yield return FlyFromSupply(e.CardId, e.Player, Zone.Discard);
                    break;

                case GameEventType.CardGained:
                {
                    var prev = game.Events[Mathf.Max(0, e.Seq - 1)];
                    bool afterBuy = e.Seq > 0 && prev.Type == GameEventType.CardBought && prev.CardId == e.CardId;
                    if (!afterBuy)
                    {
                        LogEvent(e);
                        yield return FlyFromSupply(e.CardId, e.Player, e.To);
                    }
                    if (e.Snap != null) ApplySupply(e.Snap);
                    break;
                }

                case GameEventType.SiegeAttack:
                {
                    LogEvent(e);
                    GameEvent dmg = FindNext(e, GameEventType.CastleDamaged);
                    yield return field.SiegeAttack(e.CardId, e.Player, () =>
                    {
                        if (dmg != null) { consumed.Add(dmg.Seq); LogEvent(dmg); ShowDamage(dmg); StartCoroutine(field.CastleHit(dmg.Player, dmg.Amount)); }
                    });
                    yield return Anim.Wait(0.15f);
                    break;
                }

                case GameEventType.CastleDamaged:
                    LogEvent(e);
                    if (db[e.CardId].SelfDamage > 0)
                    {
                        ShowDamage(e, delay: true);
                        yield return field.TraitorExplosion(e.CardId, e.Player, library.Get(e.CardId).duration);
                    }
                    else
                    {
                        ShowDamage(e);
                        yield return field.CastleHit(e.Player, e.Amount);
                    }
                    break;

                case GameEventType.CastleRepaired:
                    LogEvent(e);
                    shownHp[e.Player] = e.Value;
                    hpBars[e.Player].Set(e.Value, false);
                    field.SetCastleHp(e.Player, e.Value);
                    FloatText("+" + e.Amount + " 수리", Palette.Good, field.ScreenPoint(field.CastleTop(e.Player)), 34);
                    yield return field.CastleRepair(e.Player);
                    break;

                case GameEventType.CardTrashed:
                    LogEvent(e);
                    yield return TrashAnim(e);
                    break;

                case GameEventType.CardDiscarded:
                    if (e.Snap != null && e.Snap.Phase == Phase.Cleanup)
                    {
                        yield return Anim.Wait(0.015f);
                    }
                    else
                    {
                        LogEvent(e);
                        yield return DiscardAnim(e);
                    }
                    break;

                case GameEventType.AttackBlocked:
                    LogEvent(e);
                    StartCoroutine(Banner(e.Player == Me ? "성벽이 공격을 막았다!" : "AI의 성벽이 공격을 막았다!", Palette.Select, 0.9f));
                    yield return field.WallBlock(e.CardId, e.Player);
                    break;

                case GameEventType.GameEnded:
                    LogEvent(e, "#e8c25a");
                    yield return Anim.Wait(0.4f);
                    break;
            }
        }

        GameEvent FindNext(GameEvent e, GameEventType type)
        {
            for (int i = e.Seq + 1; i < game.Events.Count && i <= e.Seq + 3; i++)
                if (game.Events[i].Type == type) return game.Events[i];
            return null;
        }

        void ShowDamage(GameEvent e, bool delay = false)
        {
            shownHp[e.Player] = e.Value;
            hpBars[e.Player].Set(e.Value, false);
            field.SetCastleHp(e.Player, e.Value);
            FloatText("-" + e.Amount, Palette.Bad, field.ScreenPoint(field.CastleTop(e.Player)) + new Vector2(0, 20), 46 + e.Amount * 2);
        }

        IEnumerator PlayCardAnim(GameEvent e)
        {
            var def = db[e.CardId];
            var vis = library.Get(e.CardId);
            CardView ghost;
            Vector2 start;
            if (e.Player == Me && e.Index >= 0 && e.Index < handCards.Count && handCards[e.Index].CardId == e.CardId)
            {
                var src = handCards[e.Index];
                start = ScreenOf(src.Rt);
                handCards.RemoveAt(e.Index);
                Destroy(src.gameObject);
                LayoutHand(false);
                ghost = MakeGhost(e.CardId, start, 1f);
            }
            else
            {
                start = ScreenOf(e.Player == Me ? myInfo : aiHandRow);
                ghost = MakeGhost(e.CardId, start, 0.5f);
            }

            Sfx.Play(Sfx.Card, 0.7f);
            Vector2 target = ScreenOf(playArea);
            if (def.Is(CardType.Treasure))
            {
                if (vis.sfx != null) Sfx.Play(vis.sfx); else Sfx.Play(Sfx.Coin, 0.45f, UnityEngine.Random.Range(0.95f, 1.1f));
                StartCoroutine(field.CoinPop(e.CardId, e.Player, def.Coins));
                FloatText("+" + def.Coins, Palette.Gold, ScreenOf(coinsChip) + new Vector2(0, -110), 30);
                yield return MoveGhost(ghost, start, target, 1f, 0.55f, 0.16f);
                DestroyGhost(ghost);
                yield break;
            }

            // 행동 카드: 크게 보여준 뒤 사용한 카드 자리로
            Vector2 center = ScreenOf(fieldRect) + new Vector2(0, -40);
            yield return MoveGhost(ghost, start, center, e.Player == Me ? 1f : 0.5f, 1.35f, 0.22f);
            if (vis.sfx != null) Sfx.Play(vis.sfx);
            if (vis.style == SummonStyle.FlyToEnemy)
                StartCoroutine(field.FlyToEnemy(e.CardId, e.Player, vis.duration, e.CardId == "witch" ? Palette.Hex("#b050ff") : Palette.Hex("#ff6040")));
            else
                StartCoroutine(field.SummonAppear(e.CardId, e.Player, vis.duration));
            yield return Anim.Wait(e.Player == Me ? 0.35f : 0.75f);
            yield return MoveGhost(ghost, center, target, 1.35f, 0.55f, 0.18f);
            DestroyGhost(ghost);
            if (vis.style == SummonStyle.FlyToEnemy) yield return Anim.Wait(0.35f * vis.duration);
        }

        IEnumerator FlyFromSupply(string cardId, int player, Zone to)
        {
            var tile = tiles.FirstOrDefault(t => t.CardId == cardId);
            Vector2 start = tile != null ? ScreenOf(tile.Rt) : ScreenOf(fieldRect);
            Vector2 end = to == Zone.Hand && player == Me ? ScreenOf(handArea) : PileScreenPos(player, false);
            var ghost = MakeGhost(cardId, start, 0.4f);
            Vector2 mid = Vector2.Lerp(start, ScreenOf(fieldRect), 0.6f);
            yield return MoveGhost(ghost, start, mid, 0.4f, 1.1f, 0.2f);
            yield return Anim.Wait(0.2f);
            yield return MoveGhost(ghost, mid, end, 1.1f, 0.35f, 0.22f);
            DestroyGhost(ghost);
        }

        IEnumerator TrashAnim(GameEvent e)
        {
            Vector2 start;
            CardView ghost;
            if (e.Player == Me && e.Index >= 0 && e.Index < handCards.Count && handCards[e.Index].CardId == e.CardId)
            {
                var src = handCards[e.Index];
                start = ScreenOf(src.Rt);
                handCards.RemoveAt(e.Index);
                Destroy(src.gameObject);
                LayoutHand(false);
                ghost = MakeGhost(e.CardId, start, 1f);
            }
            else
            {
                start = ScreenOf(aiHandRow);
                ghost = MakeGhost(e.CardId, start, 0.5f);
            }
            Vector2 center = ScreenOf(fieldRect) + new Vector2(UnityEngine.Random.Range(-60f, 60f), -20);
            yield return MoveGhost(ghost, start, center, ghost.Rt.localScale.x, 1f, 0.2f);
            Sfx.Play(Sfx.Trash, 0.7f);
            field.TrashFlame(e.Player);
            var img = ghost.GetComponentsInChildren<Graphic>();
            yield return Anim.Run(0.4f, k =>
            {
                if (ghost == null) return;
                ghost.Alpha = 1 - k;
                ghost.Rt.localScale = Vector3.one * (1f - 0.4f * k);
                foreach (var g in img) if (g != null) g.color = new Color(g.color.r, g.color.g * (1 - k * 0.04f), g.color.b * (1 - k * 0.06f), g.color.a);
            });
            DestroyGhost(ghost);
        }

        IEnumerator DiscardAnim(GameEvent e)
        {
            Vector2 start;
            CardView ghost;
            if (e.Player == Me && e.Index >= 0 && e.Index < handCards.Count && handCards[e.Index].CardId == e.CardId)
            {
                var src = handCards[e.Index];
                start = ScreenOf(src.Rt);
                handCards.RemoveAt(e.Index);
                Destroy(src.gameObject);
                LayoutHand(false);
                ghost = MakeGhost(e.CardId, start, 1f);
            }
            else
            {
                start = ScreenOf(aiHandRow);
                ghost = MakeGhost(e.CardId, start, 0.5f);
            }
            Sfx.Play(Sfx.Card, 0.6f, 0.8f);
            yield return MoveGhost(ghost, start, PileScreenPos(e.Player, false), ghost.Rt.localScale.x, 0.3f, 0.22f);
            DestroyGhost(ghost);
        }

        // =====================================================================
        // 사람의 선택
        // =====================================================================

        void OnHandCardClicked(CardView cv)
        {
            if (!HumanReady) return;
            int i = handCards.IndexOf(cv);
            if (i < 0) return;
            var d = game.Pending;
            var def = db[cv.CardId];
            switch (d.Kind)
            {
                case DecisionKind.ActionPhase:
                    if (def.Is(CardType.Action)) Submit(Command.Play(i));
                    else if (def.Is(CardType.Treasure))
                    {
                        // 재화를 누르면 구매 단계로 넘어가며 바로 낸다
                        if (game.Submit(Command.EndPhase())) { needsRefresh = true; Submit(Command.Play(i)); }
                    }
                    break;
                case DecisionKind.BuyPhase:
                    if (def.Is(CardType.Treasure)) Submit(Command.Play(i));
                    break;
                case DecisionKind.ChooseCards:
                    if (!d.CardChoices.Contains(i)) return;
                    if (selected.Contains(i)) selected.Remove(i);
                    else
                    {
                        if (selected.Count >= d.Max)
                        {
                            if (d.Max == 1) selected.Clear();
                            else { Toast("최대 " + d.Max + "장까지 고를 수 있습니다", Palette.TextDim); return; }
                        }
                        selected.Add(i);
                    }
                    Sfx.Play(Sfx.Click, 0.5f);
                    RefreshInteractivity();
                    break;
            }
        }

        void OnSupplyClicked(SupplyTile t)
        {
            if (!HumanReady) return;
            var d = game.Pending;
            if (d.Kind == DecisionKind.ChooseSupply)
            {
                if (d.SupplyChoices.Contains(t.CardId)) Submit(Command.ChooseSupply(t.CardId));
                return;
            }
            if (d.Kind == DecisionKind.ActionPhase)
            {
                Toast("먼저 구매 단계로 넘어가세요 (재화를 내면 자동으로 넘어갑니다)", Palette.TextDim);
                return;
            }
            if (d.Kind == DecisionKind.BuyPhase)
            {
                if (game.CanBuy(Me, t.CardId)) Submit(Command.Buy(t.CardId));
                else if (game.Buys <= 0) Toast("구매 횟수가 없습니다", Palette.TextDim);
                else if (game.SupplyCount(t.CardId) <= 0) Toast("더미가 비었습니다", Palette.TextDim);
                else Toast("코인이 부족합니다 (" + game.EffectiveCost(t.CardId) + " 필요)", Palette.TextDim);
            }
        }

        void OnPlayAll()
        {
            if (!HumanReady) return;
            if (game.Pending.Kind == DecisionKind.ActionPhase && !game.Submit(Command.EndPhase())) return;
            needsRefresh = true;
            Submit(Command.PlayAllTreasures());
        }

        void OnNextPhase()
        {
            if (!HumanReady) return;
            if (game.Pending.Kind == DecisionKind.ActionPhase) Submit(Command.EndPhase());
        }

        void OnEndTurn()
        {
            if (!HumanReady) return;
            var k = game.Pending.Kind;
            if (k == DecisionKind.ActionPhase)
            {
                if (!game.Submit(Command.EndPhase())) return;
            }
            if (game.Pending != null && game.Pending.Kind == DecisionKind.BuyPhase) Submit(Command.EndPhase());
        }

        void OnConfirm()
        {
            if (!HumanReady || game.Pending.Kind != DecisionKind.ChooseCards) return;
            Submit(Command.Choose(selected.ToArray()));
        }

        void OnYesNo(bool yes)
        {
            if (!HumanReady || game.Pending.Kind != DecisionKind.YesNo) return;
            Submit(Command.Answer(yes));
        }

        void OnSkip()
        {
            if (game == null) return;
            if (game.Current == Ai || (game.Pending != null && game.Pending.Player == Ai)) skipAiTurn = true;
            if (playing)
            {
                skipQueue = true;
                Anim.Skipping = true;
                field.ClearTemps();
                ClearGhosts();
            }
        }

        void SetSpeed(float s)
        {
            Anim.Speed = s;
            SaveSettings();
            UpdateSpeedButtons();
        }

        // =====================================================================
        // 저장 / 불러오기
        // =====================================================================

        void AutoSave()
        {
            if (game == null || game.IsOver) return;
            try { WriteSave(); } catch (Exception ex) { Debug.LogWarning("자동 저장 실패: " + ex.Message); }
        }

        void WriteSave()
        {
            var save = SaveGame.From(game);
            save.Extra["difficulty"] = (int)difficulty;
            File.WriteAllText(SavePath, save.ToJson());
        }

        void OnSave()
        {
            if (game == null || game.IsOver) { Toast("저장할 게임이 없습니다", Palette.TextDim); return; }
            try
            {
                WriteSave();
                Toast("저장했습니다", Palette.Good);
            }
            catch (Exception ex) { Toast("저장 실패: " + ex.Message, Palette.Bad); }
        }

        void OnLoad()
        {
            if (!File.Exists(SavePath)) { Toast("저장된 게임이 없습니다", Palette.TextDim); return; }
            try
            {
                var save = SaveGame.FromJson(File.ReadAllText(SavePath));
                if (save.Extra.TryGetValue("difficulty", out var d) && d is double dd) difficulty = (Difficulty)(int)dd;
                var g = save.Restore(db);
                BeginGame(g, false);
                Toast("불러왔습니다", Palette.Good);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
                Toast("불러오기 실패: " + ex.Message, Palette.Bad);
            }
        }
    }
}
