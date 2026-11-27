using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomDeck.Engine
{
    /// <summary>
    /// 왕국의 덱 게임 엔진. 순수 C# (UnityEngine 참조 없음).
    /// 사용법: new Game(db, options) → Start() → Pending을 보고 Submit(Command) 반복.
    /// 상태가 바뀔 때마다 Events에 이벤트가 쌓이고 EventRaised가 호출된다.
    /// </summary>
    public sealed partial class Game
    {
        /// <summary>성 체력 기준값. 시작 덱의 궁수대 3장(공격력 3) 때문에 실제 시작 체력은 BaseHp - 3 = 40.</summary>
        public const int BaseHp = 43;
        /// <summary>게임 시작 시 성 체력</summary>
        public const int StartingHp = BaseHp - 3;
        public const int KingdomSize = 10;
        public const int HandSize = 5;

        public readonly CardDatabase Db;
        public readonly GameOptions Options;
        public readonly Rng Rng;
        public readonly PlayerState[] Players = new PlayerState[2];
        public readonly List<string> SupplyOrder = new List<string>();
        public readonly List<string> Kingdom = new List<string>();
        public readonly List<string> Trash = new List<string>();
        readonly Dictionary<string, int> supply = new Dictionary<string, int>();

        public int Current { get; private set; }
        public int FirstPlayer { get; private set; }
        public Phase Phase { get; private set; } = Phase.Setup;
        public int Actions { get; private set; }
        public int Buys { get; private set; }
        public int Coins { get; private set; }
        /// <summary>다음 구매 1회에 적용되는 비용 감소</summary>
        public int CostDiscount { get; private set; }
        /// <summary>전체 진행된 턴 수 (양쪽 합)</summary>
        public int TurnNumber { get; private set; }

        public bool IsOver { get; private set; }
        /// <summary>승자 (-1 = 무승부)</summary>
        public int Winner { get; private set; } = -1;
        public EndReason EndReason { get; private set; }

        public PendingDecision Pending { get; private set; }
        public string LastError { get; private set; }

        public readonly List<GameEvent> Events = new List<GameEvent>();
        public readonly List<Command> CommandLog = new List<Command>();
        public event Action<GameEvent> EventRaised;

        // 효과 실행 스택 (마지막 원소가 다음에 실행됨)
        readonly List<EffectStep> work = new List<EffectStep>();
        EffectStep awaiting;
        // 카드 한 장의 효과 진행 중 기억해 둘 값
        int lastTrashedCost = -1;
        bool trashedSomething;
        string lastDiscardedTop;
        // 마지막으로 이벤트로 알린 성 체력 (변화가 생기면 피해/수리 이벤트를 낸다)
        readonly int[] knownHp = new int[2];
        bool started;

        public Game(CardDatabase db, GameOptions options = null)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            Options = options ?? new GameOptions();
            Rng = new Rng(Options.Seed);
            for (int i = 0; i < 2; i++)
                Players[i] = new PlayerState { Index = i, Name = Options.PlayerNames != null && Options.PlayerNames.Length > i ? Options.PlayerNames[i] : "플레이어 " + (i + 1) };
        }

        // =====================================================================
        // 조회
        // =====================================================================

        public CardDef Card(string id) => Db[id];
        public static int Opponent(int p) => 1 - p;
        public PlayerState CurrentPlayer => Players[Current];

        public int SupplyCount(string id) => supply.TryGetValue(id, out int n) ? n : 0;
        public bool InSupply(string id) => supply.ContainsKey(id);
        public int EmptyPiles => supply.Values.Count(n => n <= 0);

        /// <summary>플레이어 덱 전체의 병기 공격력 합 (= 원작의 승점)</summary>
        public int SiegePower(int p)
        {
            int sum = 0;
            foreach (var id in Players[p].AllCards) sum += CardPower(p, id);
            return sum;
        }

        /// <summary>p가 가진 카드 한 장의 공격력 (의용군처럼 덱 크기에 따라 변하는 카드 포함)</summary>
        public int CardPower(int p, string id)
        {
            var def = Db[id];
            int power = def.Power;
            if (def.PowerPerCards > 0) power += Players[p].TotalCards / def.PowerPerCards;
            return power;
        }

        public int TraitorDamage(int p)
        {
            int sum = 0;
            foreach (var id in Players[p].AllCards) sum += Db[id].SelfDamage;
            return sum;
        }

        /// <summary>내 성 체력 = 43 − (상대 덱의 병기 공격력 합) − (내 덱의 배신자 수)</summary>
        public int CastleHp(int p) => BaseHp - SiegePower(Opponent(p)) - TraitorDamage(p);

        public int EffectiveCost(string id) => Math.Max(0, Db[id].Cost - CostDiscount);

        public bool CanPlay(int player, int handIndex)
        {
            if (Pending == null || Pending.Player != player) return false;
            var hand = Players[player].Hand;
            if (handIndex < 0 || handIndex >= hand.Count) return false;
            var def = Db[hand[handIndex]];
            if (Pending.Kind == DecisionKind.ActionPhase) return def.Is(CardType.Action) && Actions > 0;
            if (Pending.Kind == DecisionKind.BuyPhase) return def.Is(CardType.Treasure);
            return false;
        }

        public bool CanBuy(int player, string id)
        {
            return Pending != null && Pending.Kind == DecisionKind.BuyPhase && Pending.Player == player
                   && Buys > 0 && SupplyCount(id) > 0 && EffectiveCost(id) <= Coins;
        }

        // =====================================================================
        // 준비
        // =====================================================================

        public void Start()
        {
            if (started) throw new InvalidOperationException("이미 시작된 게임입니다");
            started = true;

            // 왕국 카드 선택 (저장 재생과 난수 순서를 맞추기 위해 섞기는 항상 한다)
            var pool = Db.KingdomPool.Select(c => c.Id).ToList();
            Rng.Shuffle(pool);
            if (Options.Kingdom != null && Options.Kingdom.Count > 0)
            {
                foreach (var id in Options.Kingdom)
                {
                    if (!Db.Contains(id) || Db[id].Basic) throw new ArgumentException("왕국 카드가 아닙니다: " + id);
                    Kingdom.Add(id);
                }
            }
            else
            {
                Kingdom.AddRange(pool.Take(Math.Min(KingdomSize, pool.Count)));
            }
            Kingdom.Sort((a, b) => Db[a].Cost != Db[b].Cost ? Db[a].Cost.CompareTo(Db[b].Cost) : string.CompareOrdinal(a, b));

            foreach (var c in Db.Basics) { SupplyOrder.Add(c.Id); supply[c.Id] = c.Supply; }
            foreach (var id in Kingdom) { SupplyOrder.Add(id); supply[id] = Db[id].Supply; }

            // 시작 덱: 동화 7 + 궁수대 3 (공급처와 별도)
            foreach (var p in Players)
            {
                for (int i = 0; i < 7; i++) p.Deck.Add("copper");
                for (int i = 0; i < 3; i++) p.Deck.Add("archers");
                Rng.Shuffle(p.Deck);
            }

            int randomFirst = Rng.Next(2);
            FirstPlayer = Options.FirstPlayer >= 0 ? Options.FirstPlayer % 2 : randomFirst;
            Current = FirstPlayer;
            Emit(new GameEvent { Type = GameEventType.GameStarted, Player = FirstPlayer });

            foreach (var p in Players) Draw(p.Index, HandSize);
            for (int i = 0; i < 2; i++) knownHp[i] = CastleHp(i);

            StartTurn(FirstPlayer);
            Advance();
        }

        // =====================================================================
        // 명령 처리
        // =====================================================================

        /// <summary>현재 선택 요청에 답한다. 잘못된 명령이면 false (LastError에 이유).</summary>
        public bool Submit(Command c)
        {
            LastError = null;
            if (c == null) return Fail("명령이 없습니다");
            if (IsOver) return Fail("게임이 끝났습니다");
            if (Pending == null) return Fail("기다리는 선택이 없습니다");
            var d = Pending;

            switch (d.Kind)
            {
                case DecisionKind.ActionPhase:
                    if (c.Type == CommandType.Play)
                    {
                        if (!CanPlay(d.Player, c.Index)) return Fail("낼 수 없는 카드입니다");
                        Record(c);
                        PlayAction(c.Index);
                    }
                    else if (c.Type == CommandType.EndPhase)
                    {
                        Record(c);
                        SetPhase(Phase.Buy);
                    }
                    else return Fail("행동 단계에서는 할 수 없는 명령입니다: " + c.Type);
                    break;

                case DecisionKind.BuyPhase:
                    if (c.Type == CommandType.Play)
                    {
                        if (!CanPlay(d.Player, c.Index)) return Fail("낼 수 없는 카드입니다");
                        Record(c);
                        PlayTreasure(c.Index);
                    }
                    else if (c.Type == CommandType.PlayAllTreasures)
                    {
                        Record(c);
                        var hand = CurrentPlayer.Hand;
                        for (int i = hand.Count - 1; i >= 0; i--)
                            if (Db[hand[i]].Is(CardType.Treasure)) PlayTreasure(i);
                    }
                    else if (c.Type == CommandType.Buy)
                    {
                        if (c.CardId == null || !InSupply(c.CardId)) return Fail("공급처에 없는 카드입니다");
                        if (Buys <= 0) return Fail("구매 횟수가 없습니다");
                        if (SupplyCount(c.CardId) <= 0) return Fail("더미가 비었습니다");
                        if (EffectiveCost(c.CardId) > Coins) return Fail("코인이 부족합니다");
                        Record(c);
                        BuyCard(c.CardId);
                    }
                    else if (c.Type == CommandType.EndPhase)
                    {
                        Record(c);
                        Cleanup();
                    }
                    else return Fail("구매 단계에서는 할 수 없는 명령입니다: " + c.Type);
                    break;

                case DecisionKind.ChooseCards:
                {
                    if (c.Type != CommandType.ChooseCards) return Fail("카드를 골라야 합니다");
                    var idx = c.Indices ?? new int[0];
                    if (idx.Length < d.Min || idx.Length > d.Max) return Fail(d.Min == d.Max ? d.Min + "장을 골라야 합니다" : d.Min + "~" + d.Max + "장을 골라야 합니다");
                    if (idx.Distinct().Count() != idx.Length) return Fail("같은 카드를 두 번 고를 수 없습니다");
                    foreach (int i in idx) if (!d.CardChoices.Contains(i)) return Fail("고를 수 없는 카드입니다");
                    Record(c);
                    ResolveAwaiting(c);
                    break;
                }

                case DecisionKind.ChooseSupply:
                    if (c.Type != CommandType.ChooseSupply) return Fail("공급처 카드를 골라야 합니다");
                    if (!d.SupplyChoices.Contains(c.CardId)) return Fail("고를 수 없는 카드입니다");
                    Record(c);
                    ResolveAwaiting(c);
                    break;

                case DecisionKind.YesNo:
                    if (c.Type != CommandType.YesNo) return Fail("예/아니오로 답해야 합니다");
                    Record(c);
                    ResolveAwaiting(c);
                    break;
            }

            RunWork();
            Advance();
            return true;
        }

        bool Fail(string msg)
        {
            LastError = msg;
            return false;
        }

        void Record(Command c)
        {
            CommandLog.Add(c);
            Pending = null;
        }

        /// <summary>효과가 모두 끝났으면 다음 기본 선택(행동/구매 단계)을 준비한다.</summary>
        void Advance()
        {
            while (!IsOver && Pending == null && work.Count == 0 && awaiting == null)
            {
                if (Phase == Phase.Action)
                {
                    bool hasAction = Actions > 0 && CurrentPlayer.Hand.Any(id => Db[id].Is(CardType.Action));
                    if (hasAction)
                    {
                        Pending = new PendingDecision { Kind = DecisionKind.ActionPhase, Player = Current, Reason = "행동 카드를 내거나 구매 단계로 넘어가세요" };
                        return;
                    }
                    SetPhase(Phase.Buy);
                    continue;
                }
                if (Phase == Phase.Buy)
                {
                    Pending = new PendingDecision { Kind = DecisionKind.BuyPhase, Player = Current, Reason = "재화를 내고 카드를 구매하세요" };
                    return;
                }
                return;
            }
        }

        // =====================================================================
        // 턴 진행
        // =====================================================================

        void StartTurn(int p)
        {
            Current = p;
            TurnNumber++;
            Actions = 1;
            Buys = 1;
            Coins = 0;
            CostDiscount = 0;
            Emit(new GameEvent { Type = GameEventType.TurnStarted, Player = p, Value = Players[p].TurnsTaken + 1 });
            SetPhase(Phase.Action);
            EmitResources();
        }

        void SetPhase(Phase ph)
        {
            Phase = ph;
            Emit(new GameEvent { Type = GameEventType.PhaseChanged, Player = Current, Phase = ph });
        }

        void PlayAction(int handIndex)
        {
            var p = CurrentPlayer;
            string id = p.Hand[handIndex];
            p.Hand.RemoveAt(handIndex);
            p.InPlay.Add(id);
            Actions--;
            Emit(new GameEvent { Type = GameEventType.CardPlayed, Player = Current, CardId = id, Index = handIndex, From = Zone.Hand, To = Zone.InPlay });
            EmitResources();
            PushPlay(Current, id, false);
        }

        void PlayTreasure(int handIndex)
        {
            var p = CurrentPlayer;
            string id = p.Hand[handIndex];
            p.Hand.RemoveAt(handIndex);
            p.InPlay.Add(id);
            Coins += Db[id].Coins;
            Emit(new GameEvent { Type = GameEventType.CardPlayed, Player = Current, CardId = id, Index = handIndex, From = Zone.Hand, To = Zone.InPlay, Amount = Db[id].Coins });
            EmitResources();
        }

        void BuyCard(string id)
        {
            int cost = EffectiveCost(id);
            Coins -= cost;
            Buys--;
            CostDiscount = 0;
            Emit(new GameEvent { Type = GameEventType.CardBought, Player = Current, CardId = id, Amount = cost });
            EmitResources();
            Gain(Current, id, Zone.Discard);
        }

        void Cleanup()
        {
            SetPhase(Phase.Cleanup);
            var p = CurrentPlayer;
            while (p.InPlay.Count > 0)
            {
                string id = p.InPlay[p.InPlay.Count - 1];
                p.InPlay.RemoveAt(p.InPlay.Count - 1);
                p.Discard.Add(id);
                Emit(new GameEvent { Type = GameEventType.CardDiscarded, Player = Current, CardId = id, From = Zone.InPlay, To = Zone.Discard });
            }
            while (p.Hand.Count > 0)
            {
                int last = p.Hand.Count - 1;
                string id = p.Hand[last];
                p.Hand.RemoveAt(last);
                p.Discard.Add(id);
                Emit(new GameEvent { Type = GameEventType.CardDiscarded, Player = Current, CardId = id, Index = last, From = Zone.Hand, To = Zone.Discard });
            }
            Actions = 0; Buys = 0; Coins = 0; CostDiscount = 0;
            EmitResources();
            Draw(Current, HandSize);
            p.TurnsTaken++;

            if (SupplyCount("dragon") <= 0 && InSupply("dragon")) { EndGame(EndReason.DragonsGone); return; }
            if (EmptyPiles >= 3) { EndGame(EndReason.ThreePiles); return; }
            if (TurnNumber >= Options.TurnLimit) { EndGame(EndReason.TurnLimit); return; }

            StartTurn(Opponent(Current));
        }

        void EndGame(EndReason reason)
        {
            if (IsOver) return;
            IsOver = true;
            EndReason = reason;
            Phase = Phase.GameOver;
            Pending = null;
            awaiting = null;
            work.Clear();

            int hp0 = CastleHp(0), hp1 = CastleHp(1);
            if (hp0 != hp1) Winner = hp0 > hp1 ? 0 : 1;
            else if (Players[0].TurnsTaken != Players[1].TurnsTaken) Winner = Players[0].TurnsTaken < Players[1].TurnsTaken ? 0 : 1;
            else Winner = -1;

            Emit(new GameEvent { Type = GameEventType.GameEnded, Player = Winner, Phase = Phase.GameOver });
        }

        void CheckInstantEnd()
        {
            if (!Options.InstantCastleEnd || IsOver) return;
            if (CastleHp(0) <= 0 || CastleHp(1) <= 0) EndGame(EndReason.CastleDestroyed);
        }

        // =====================================================================
        // 카드 이동
        // =====================================================================

        public void Draw(int player, int n)
        {
            var p = Players[player];
            for (int k = 0; k < n; k++)
            {
                if (p.Deck.Count == 0)
                {
                    if (p.Discard.Count == 0) return;
                    int count = p.Discard.Count;
                    p.Deck.AddRange(p.Discard);
                    p.Discard.Clear();
                    Rng.Shuffle(p.Deck);
                    Emit(new GameEvent { Type = GameEventType.Shuffled, Player = player, Amount = count });
                }
                string id = p.Deck[p.Deck.Count - 1];
                p.Deck.RemoveAt(p.Deck.Count - 1);
                p.Hand.Add(id);
                Emit(new GameEvent { Type = GameEventType.CardDrawn, Player = player, CardId = id, From = Zone.Deck, To = Zone.Hand, Index = p.Hand.Count - 1 });
            }
        }

        void Gain(int player, string id, Zone to)
        {
            if (SupplyCount(id) <= 0) return;
            supply[id]--;
            var p = Players[player];
            switch (to)
            {
                case Zone.Hand: p.Hand.Add(id); break;
                case Zone.Deck: p.Deck.Add(id); break;
                default: to = Zone.Discard; p.Discard.Add(id); break;
            }
            Emit(new GameEvent { Type = GameEventType.CardGained, Player = player, CardId = id, From = Zone.Supply, To = to, Value = supply[id] });

            int power = CardPower(player, id);
            if (Db[id].Is(CardType.Siege) && power > 0)
                Emit(new GameEvent { Type = GameEventType.SiegeAttack, Player = player, CardId = id, Amount = power });
            SyncHp(id);
            CheckInstantEnd();
        }

        void TrashFromHand(int player, int handIndex)
        {
            var p = Players[player];
            string id = p.Hand[handIndex];
            p.Hand.RemoveAt(handIndex);
            Trash.Add(id);
            Emit(new GameEvent { Type = GameEventType.CardTrashed, Player = player, CardId = id, Index = handIndex, From = Zone.Hand, To = Zone.Trash });
            SyncHp(id);
        }

        /// <summary>
        /// 카드를 얻거나 폐기한 뒤 호출. 성 체력이 바뀌었으면 피해(CastleDamaged)/수리(CastleRepaired) 이벤트를 낸다.
        /// (병기 획득·폐기, 배신자, 의용군처럼 덱 크기에 따라 변하는 공격력까지 한 곳에서 처리)
        /// </summary>
        void SyncHp(string causeCard)
        {
            // 공격받는 쪽을 먼저 알린다 (내가 얻은 카드 → 상대 성)
            for (int k = 0; k < 2; k++)
            {
                int p = k;
                int now = CastleHp(p);
                int diff = now - knownHp[p];
                if (diff == 0) continue;
                knownHp[p] = now;
                Emit(new GameEvent
                {
                    Type = diff < 0 ? GameEventType.CastleDamaged : GameEventType.CastleRepaired,
                    Player = p, CardId = causeCard, Amount = Math.Abs(diff), Value = now,
                });
            }
        }

        void DiscardFromHand(int player, int handIndex)
        {
            var p = Players[player];
            string id = p.Hand[handIndex];
            p.Hand.RemoveAt(handIndex);
            p.Discard.Add(id);
            Emit(new GameEvent { Type = GameEventType.CardDiscarded, Player = player, CardId = id, Index = handIndex, From = Zone.Hand, To = Zone.Discard });
        }

        // =====================================================================
        // 테스트 지원 (턴 시작 직후 상태에서만 사용)
        // =====================================================================

        /// <summary>테스트용: 손패를 바꾸고 현재 플레이어의 행동 단계를 처음부터 다시 연다.</summary>
        public void DebugSetHand(int player, params string[] cards)
        {
            var p = Players[player];
            p.Hand.Clear();
            p.Hand.AddRange(cards);
            DebugResyncHp();
            if (player == Current && !IsOver && awaiting == null && work.Count == 0)
            {
                Pending = null;
                Phase = Phase.Action;
                Actions = 1; Buys = 1; Coins = 0; CostDiscount = 0;
                Advance();
            }
        }

        /// <summary>테스트용: 덱을 바꾼다. 배열의 첫 원소가 맨 위(먼저 뽑힘).</summary>
        public void DebugSetDeck(int player, params string[] topFirst)
        {
            var p = Players[player];
            p.Deck.Clear();
            for (int i = topFirst.Length - 1; i >= 0; i--) p.Deck.Add(topFirst[i]);
            DebugResyncHp();
        }

        /// <summary>테스트용: 카드 목록을 직접 바꾼 뒤, 이후 피해/수리 이벤트가 그 변화까지 섞지 않도록 기준 체력을 다시 잡는다.</summary>
        public void DebugResyncHp()
        {
            for (int i = 0; i < 2; i++) knownHp[i] = CastleHp(i);
        }

        /// <summary>테스트용: 공급처 수량을 바꾼다.</summary>
        public void DebugSetSupply(string id, int count)
        {
            if (!supply.ContainsKey(id)) throw new ArgumentException("공급처에 없는 카드: " + id);
            supply[id] = count;
        }

        // =====================================================================
        // 이벤트
        // =====================================================================

        void EmitResources()
        {
            Emit(new GameEvent { Type = GameEventType.ResourcesChanged, Player = Current });
        }

        void Emit(GameEvent e)
        {
            e.Seq = Events.Count;
            e.Text = Describe(e);
            if (Options.CaptureSnapshots) e.Snap = TakeSnapshot();
            Events.Add(e);
            EventRaised?.Invoke(e);
        }

        public Snapshot TakeSnapshot()
        {
            var s = new Snapshot
            {
                Players = new PlayerView[2],
                Supply = new int[SupplyOrder.Count],
                Current = Current,
                Phase = Phase,
                Actions = Actions,
                Buys = Buys,
                Coins = Coins,
                CostDiscount = CostDiscount,
                TurnNumber = TurnNumber,
                TrashCount = Trash.Count,
            };
            for (int i = 0; i < SupplyOrder.Count; i++) s.Supply[i] = supply[SupplyOrder[i]];
            for (int i = 0; i < 2; i++)
            {
                var p = Players[i];
                s.Players[i] = new PlayerView
                {
                    Hand = p.Hand.ToArray(),
                    InPlay = p.InPlay.ToArray(),
                    Revealed = p.Revealed.ToArray(),
                    RevealedPrivate = p.RevealedPrivate,
                    DeckCount = p.Deck.Count,
                    DiscardCount = p.Discard.Count,
                    TopDiscard = p.Discard.Count > 0 ? p.Discard[p.Discard.Count - 1] : null,
                    Hp = CastleHp(i),
                    TurnsTaken = p.TurnsTaken,
                    TotalCards = p.TotalCards,
                };
            }
            return s;
        }

        string Name(string id) => id != null && Db.Contains(id) ? Db[id].Name : id;

        string Describe(GameEvent e)
        {
            string who = e.Player >= 0 && e.Player < 2 ? Players[e.Player].Name : "";
            switch (e.Type)
            {
                case GameEventType.GameStarted: return "게임 시작! 선 플레이어: " + who;
                case GameEventType.TurnStarted: return "── " + who + "의 " + e.Value + "번째 턴 ──";
                case GameEventType.PhaseChanged:
                    switch (e.Phase)
                    {
                        case Phase.Action: return who + ": 행동 단계";
                        case Phase.Buy: return who + ": 구매 단계";
                        case Phase.Cleanup: return who + ": 정리 단계";
                        default: return who + ": " + e.Phase;
                    }
                case GameEventType.CardDrawn: return who + ": 카드 1장 뽑음";
                case GameEventType.Shuffled: return who + ": 버림 더미 " + e.Amount + "장을 섞어 새 덱으로";
                case GameEventType.CardPlayed:
                    if (e.From == Zone.InPlay) return who + ": [" + Name(e.CardId) + "] 한 번 더 사용";
                    if (e.From == Zone.Discard) return who + ": 버린 [" + Name(e.CardId) + "] 바로 사용";
                    return who + ": [" + Name(e.CardId) + "] 사용";
                case GameEventType.CardRevealed:
                    return e.Private ? who + ": 덱 위 카드 1장 확인" : who + ": 덱 위 [" + Name(e.CardId) + "] 공개";
                case GameEventType.CardReturned:
                    return who + ": 카드 1장을 덱 위로 되돌림";
                case GameEventType.CardBought: return who + ": [" + Name(e.CardId) + "] 구매 (" + e.Amount + "코인)";
                case GameEventType.CardGained: return who + ": [" + Name(e.CardId) + "] 획득" + (e.To == Zone.Hand ? " (손으로)" : "");
                case GameEventType.CardTrashed: return who + ": [" + Name(e.CardId) + "] 폐기";
                case GameEventType.CardDiscarded: return who + ": " + (e.From == Zone.Deck ? "덱 맨 위 " : "") + "[" + Name(e.CardId) + "] 버림";
                case GameEventType.ResourcesChanged: return who + ": 행동 " + Actions + " / 구매 " + Buys + " / 코인 " + Coins;
                case GameEventType.SiegeAttack: return who + "의 [" + Name(e.CardId) + "] 출격! 공격력 " + e.Amount;
                case GameEventType.CastleDamaged: return who + "의 성 피해 -" + e.Amount + " (체력 " + e.Value + ")";
                case GameEventType.CastleRepaired: return who + "의 성 수리 +" + e.Amount + " (체력 " + e.Value + ")";
                case GameEventType.AttackBlocked: return who + ": [" + Name(e.CardId) + "] 공개! 공격을 막았다";
                case GameEventType.GameEnded:
                {
                    string reason;
                    switch (EndReason)
                    {
                        case EndReason.DragonsGone: reason = "드래곤 더미 소진"; break;
                        case EndReason.ThreePiles: reason = "공급 더미 3개 소진"; break;
                        case EndReason.CastleDestroyed: reason = "성 함락"; break;
                        case EndReason.TurnLimit: reason = "턴 제한"; break;
                        default: reason = ""; break;
                    }
                    string result = Winner < 0 ? "무승부" : Players[Winner].Name + " 승리!";
                    return "게임 종료 (" + reason + ") — " + result + "  [" + Players[0].Name + " " + CastleHp(0) + " : " + CastleHp(1) + " " + Players[1].Name + "]";
                }
                default: return e.Type.ToString();
            }
        }
    }
}
