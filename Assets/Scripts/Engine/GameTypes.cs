using System.Collections.Generic;

namespace KingdomDeck.Engine
{
    /// <summary>Revealed = 덱에서 꺼내 공개하거나 확인 중인 카드 (망루, 약탈대)</summary>
    public enum Zone { Deck, Hand, Discard, InPlay, Trash, Supply, Revealed }

    public enum Phase { Setup, Action, Buy, Cleanup, GameOver }

    public enum EndReason { None, DragonsGone, ThreePiles, CastleDestroyed, TurnLimit }

    public enum GameEventType
    {
        GameStarted,
        TurnStarted,
        PhaseChanged,
        CardDrawn,
        Shuffled,
        CardPlayed,
        CardBought,
        CardGained,
        CardTrashed,
        CardDiscarded,
        ResourcesChanged,
        SiegeAttack,
        CastleDamaged,
        CastleRepaired,
        AttackBlocked,
        GameEnded,
        /// <summary>덱에서 카드를 꺼내 공개/확인 (Private면 본인만 봄)</summary>
        CardRevealed,
        /// <summary>공개한 카드를 덱 위로 되돌림</summary>
        CardReturned,
    }

    /// <summary>
    /// 엔진이 내보내는 이벤트. 연출 쪽은 이 이벤트를 큐에 쌓아 순서대로 재생한다.
    /// Snap은 "이 이벤트 직후의 공개 상태"라서 연출이 끝나면 화면을 이 상태로 맞추면 된다.
    /// </summary>
    public sealed class GameEvent
    {
        public int Seq;
        public GameEventType Type;
        public int Player = -1;
        public string CardId;
        /// <summary>장수, 피해량, 회복량 등</summary>
        public int Amount;
        /// <summary>이벤트 후 값 (성 체력, 공급처 남은 수량 등)</summary>
        public int Value;
        public Zone From;
        public Zone To;
        /// <summary>손패 위치 등 (해당될 때)</summary>
        public int Index = -1;
        public Phase Phase;
        /// <summary>본인만 볼 수 있는 정보 (상대 화면/로그에는 카드 이름을 숨김)</summary>
        public bool Private;
        public string Text;
        public Snapshot Snap;

        public override string ToString() => Text ?? Type.ToString();
    }

    public sealed class PlayerView
    {
        public string[] Hand;
        public string[] InPlay;
        public string[] Revealed;
        public bool RevealedPrivate;
        public int DeckCount;
        public int DiscardCount;
        public string TopDiscard;
        public int Hp;
        public int TurnsTaken;
        public int TotalCards;
    }

    /// <summary>화면에 보여줄 상태 스냅샷</summary>
    public sealed class Snapshot
    {
        public PlayerView[] Players;
        public int[] Supply; // Game.SupplyOrder 순서
        public int Current;
        public Phase Phase;
        public int Actions, Buys, Coins, CostDiscount;
        public int TurnNumber;
        public int TrashCount;
    }

    public enum DecisionKind
    {
        /// <summary>행동 단계: 행동 카드를 내거나 단계를 넘긴다</summary>
        ActionPhase,
        /// <summary>구매 단계: 재화를 내거나, 카드를 사거나, 턴을 끝낸다</summary>
        BuyPhase,
        /// <summary>손패에서 카드 고르기</summary>
        ChooseCards,
        /// <summary>공급처에서 카드 고르기</summary>
        ChooseSupply,
        /// <summary>예/아니오</summary>
        YesNo,
    }

    /// <summary>
    /// 엔진이 멈추고 답을 기다리는 선택 요청. 사람(UI)과 AI가 같은 방식으로 답한다.
    /// </summary>
    public sealed class PendingDecision
    {
        public DecisionKind Kind;
        public int Player;
        public Zone From = Zone.Hand;
        public int Min;
        public int Max;
        public string Reason;
        /// <summary>이 선택을 만든 카드 ID</summary>
        public string SourceCard;
        /// <summary>이 선택을 만든 효과 블록 (trashFromHand, discardAndDraw ...)</summary>
        public string Op;
        /// <summary>ChooseCards: 고를 수 있는 위치 (From이 Hand면 손패, Revealed면 공개한 카드)</summary>
        public List<int> CardChoices = new List<int>();
        /// <summary>ChooseSupply: 고를 수 있는 공급처 카드 ID</summary>
        public List<string> SupplyChoices = new List<string>();

        public override string ToString() => Kind + " p" + Player + " " + Reason;
    }

    public enum CommandType
    {
        /// <summary>손패 Index의 카드를 낸다 (행동 단계: 행동 카드, 구매 단계: 재화)</summary>
        Play,
        PlayAllTreasures,
        Buy,
        /// <summary>행동 단계 → 구매 단계, 구매 단계 → 턴 종료</summary>
        EndPhase,
        ChooseCards,
        ChooseSupply,
        YesNo,
    }

    /// <summary>엔진에 넘기는 답/명령. 저장 파일에는 이 명령 목록이 기록된다.</summary>
    public sealed class Command
    {
        public CommandType Type;
        public int Index = -1;
        public string CardId;
        public int[] Indices;
        public bool Yes;

        public static Command Play(int handIndex) => new Command { Type = CommandType.Play, Index = handIndex };
        public static Command PlayAllTreasures() => new Command { Type = CommandType.PlayAllTreasures };
        public static Command Buy(string cardId) => new Command { Type = CommandType.Buy, CardId = cardId };
        public static Command EndPhase() => new Command { Type = CommandType.EndPhase };
        public static Command Choose(params int[] handIndices) => new Command { Type = CommandType.ChooseCards, Indices = handIndices ?? new int[0] };
        public static Command ChooseSupply(string cardId) => new Command { Type = CommandType.ChooseSupply, CardId = cardId };
        public static Command Answer(bool yes) => new Command { Type = CommandType.YesNo, Yes = yes };

        public Dictionary<string, object> ToJson()
        {
            var d = new Dictionary<string, object> { { "t", Type.ToString() } };
            if (Index >= 0) d["i"] = Index;
            if (CardId != null) d["c"] = CardId;
            if (Indices != null) { var l = new List<object>(); foreach (int x in Indices) l.Add(x); d["ix"] = l; }
            if (Type == CommandType.YesNo) d["y"] = Yes;
            return d;
        }

        public static Command FromJson(Dictionary<string, object> d)
        {
            var c = new Command
            {
                Type = (CommandType)System.Enum.Parse(typeof(CommandType), MiniJson.Str(d, "t")),
                Index = MiniJson.Int(d, "i", -1),
                CardId = MiniJson.Str(d, "c"),
                Yes = MiniJson.Bool(d, "y"),
            };
            var ix = MiniJson.List(d, "ix");
            if (ix != null)
            {
                c.Indices = new int[ix.Count];
                for (int k = 0; k < ix.Count; k++) c.Indices[k] = (int)(double)ix[k];
            }
            return c;
        }

        public override string ToString()
        {
            switch (Type)
            {
                case CommandType.Play: return "Play(" + Index + ")";
                case CommandType.Buy: return "Buy(" + CardId + ")";
                case CommandType.ChooseCards: return "Choose(" + (Indices == null ? "" : string.Join(",", Indices)) + ")";
                case CommandType.ChooseSupply: return "ChooseSupply(" + CardId + ")";
                case CommandType.YesNo: return "YesNo(" + Yes + ")";
                default: return Type.ToString();
            }
        }
    }

    public sealed class PlayerState
    {
        public int Index;
        public string Name;
        /// <summary>덱. 마지막 원소가 맨 위.</summary>
        public readonly List<string> Deck = new List<string>();
        public readonly List<string> Hand = new List<string>();
        public readonly List<string> Discard = new List<string>();
        public readonly List<string> InPlay = new List<string>();
        /// <summary>덱에서 꺼내 확인 중인 카드 (아직 내 카드)</summary>
        public readonly List<string> Revealed = new List<string>();
        public bool RevealedPrivate;
        public int TurnsTaken;

        public IEnumerable<string> AllCards
        {
            get
            {
                foreach (var c in Deck) yield return c;
                foreach (var c in Hand) yield return c;
                foreach (var c in Discard) yield return c;
                foreach (var c in InPlay) yield return c;
                foreach (var c in Revealed) yield return c;
            }
        }

        public int Count(string cardId)
        {
            int n = 0;
            foreach (var c in AllCards) if (c == cardId) n++;
            return n;
        }

        public int TotalCards => Deck.Count + Hand.Count + Discard.Count + InPlay.Count + Revealed.Count;
    }

    public sealed class GameOptions
    {
        public long Seed = 1;
        /// <summary>어느 한쪽 성 체력이 0 이하가 되면 즉시 종료 (기본 켜짐)</summary>
        public bool InstantCastleEnd = true;
        /// <summary>지정하면 이 왕국 카드 10종 사용, 비우면 시드로 무작위</summary>
        public List<string> Kingdom;
        /// <summary>-1이면 시드로 무작위</summary>
        public int FirstPlayer = -1;
        public string[] PlayerNames = { "플레이어", "AI" };
        /// <summary>연출용 스냅샷 기록 (자동 대전 시 꺼서 속도 향상)</summary>
        public bool CaptureSnapshots = true;
        /// <summary>무한 게임 방지용 전체 턴 상한</summary>
        public int TurnLimit = 300;
    }
}
