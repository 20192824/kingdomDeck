using System;
using System.Collections.Generic;

namespace KingdomDeck.Engine
{
    [Flags]
    public enum CardType
    {
        None = 0,
        Treasure = 1,   // 재화
        Siege = 2,      // 병기
        Traitor = 4,    // 배신자
        Action = 8,     // 행동
        Attack = 16,    // 공격
        Reaction = 32,  // 반응
    }

    /// <summary>
    /// 카드 효과의 기본 블록. cards.json의 "effects" 항목 하나.
    /// op: drawCards, addActions, addBuys, addCoins, gainCard, trashFromHand, discardDownTo, attack, costReduction
    /// </summary>
    public sealed class EffectBlock
    {
        public string Op;
        public int N;
        public int Min;
        public int Max;
        /// <summary>gainCard: 정해진 카드 ID (예: 배신자)</summary>
        public string CardId;
        /// <summary>gainCard: 비용 상한 (-1 = 없음)</summary>
        public int MaxCost = -1;
        /// <summary>gainCard: 방금 폐기한 카드 비용 + X 이하 (-1 = 사용 안 함)</summary>
        public int CostUpToTrashedPlus = -1;
        /// <summary>카드 종류 조건 (None = 조건 없음)</summary>
        public CardType Filter;
        /// <summary>gainCard 위치: Discard / Hand / Deck</summary>
        public Zone To = Zone.Discard;
        /// <summary>gainCard: 같은 카드 효과에서 무언가를 폐기했을 때만</summary>
        public bool IfTrashed;
        /// <summary>선택 요청에 보여줄 이유 문구</summary>
        public string Reason;
        /// <summary>attack: 상대에게 적용할 효과들</summary>
        public List<EffectBlock> Effects = new List<EffectBlock>();

        public static EffectBlock FromJson(Dictionary<string, object> d)
        {
            var b = new EffectBlock
            {
                Op = MiniJson.Str(d, "op"),
                N = MiniJson.Int(d, "n"),
                Min = MiniJson.Int(d, "min"),
                Max = MiniJson.Int(d, "max"),
                CardId = MiniJson.Str(d, "card"),
                MaxCost = MiniJson.Int(d, "maxCost", -1),
                CostUpToTrashedPlus = MiniJson.Int(d, "costUpToTrashedPlus", -1),
                IfTrashed = MiniJson.Bool(d, "ifTrashed"),
                Reason = MiniJson.Str(d, "reason"),
            };
            string filter = MiniJson.Str(d, "filter");
            if (!string.IsNullOrEmpty(filter)) b.Filter = CardDef.ParseTypes(filter.Split(','));
            string to = MiniJson.Str(d, "to");
            if (!string.IsNullOrEmpty(to)) b.To = ParseZone(to);
            var effects = MiniJson.List(d, "effects");
            if (effects != null)
                foreach (var e in effects) b.Effects.Add(FromJson((Dictionary<string, object>)e));
            if (string.IsNullOrEmpty(b.Op)) throw new FormatException("효과 블록에 op가 없습니다");
            return b;
        }

        static Zone ParseZone(string s)
        {
            switch (s.ToLowerInvariant())
            {
                case "hand": return Zone.Hand;
                case "deck": return Zone.Deck;
                case "discard": return Zone.Discard;
                default: throw new FormatException("알 수 없는 위치: " + s);
            }
        }
    }

    public sealed class ReactionDef
    {
        /// <summary>현재는 "opponentAttack"만 지원</summary>
        public string Trigger;
        /// <summary>현재는 "blockAttack"만 지원</summary>
        public string Effect;
    }

    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public CardType Types;
        public int Cost;
        public int Coins;
        /// <summary>병기 공격력 (= 승점)</summary>
        public int Power;
        /// <summary>배신자: 내 성 체력 감소량</summary>
        public int SelfDamage;
        public int Supply = 10;
        public bool Basic;
        public string Text = "";
        public List<EffectBlock> Effects = new List<EffectBlock>();
        public ReactionDef Reaction;

        public bool Is(CardType t) => (Types & t) != 0;

        public bool BlocksAttacks => Reaction != null && Reaction.Effect == "blockAttack";

        public string TypeName
        {
            get
            {
                var parts = new List<string>();
                if (Is(CardType.Treasure)) parts.Add("재화");
                if (Is(CardType.Siege)) parts.Add("병기");
                if (Is(CardType.Traitor)) parts.Add("배신자");
                if (Is(CardType.Action)) parts.Add("행동");
                if (Is(CardType.Attack)) parts.Add("공격");
                if (Is(CardType.Reaction)) parts.Add("반응");
                return string.Join("·", parts);
            }
        }

        public static CardType ParseTypes(IEnumerable<string> names)
        {
            CardType t = CardType.None;
            foreach (var raw in names)
            {
                string n = raw.Trim();
                if (n.Length == 0) continue;
                if (!Enum.TryParse(n, true, out CardType one)) throw new FormatException("알 수 없는 카드 종류: " + n);
                t |= one;
            }
            return t;
        }

        public static CardDef FromJson(Dictionary<string, object> d)
        {
            var c = new CardDef
            {
                Id = MiniJson.Str(d, "id"),
                Name = MiniJson.Str(d, "name"),
                Cost = MiniJson.Int(d, "cost"),
                Coins = MiniJson.Int(d, "coins"),
                Power = MiniJson.Int(d, "power"),
                SelfDamage = MiniJson.Int(d, "selfDamage"),
                Supply = MiniJson.Int(d, "supply", 10),
                Basic = MiniJson.Bool(d, "basic"),
                Text = MiniJson.Str(d, "text", ""),
            };
            if (string.IsNullOrEmpty(c.Id)) throw new FormatException("카드에 id가 없습니다");
            var types = MiniJson.List(d, "types");
            if (types != null)
            {
                var names = new List<string>();
                foreach (var t in types) names.Add(t.ToString());
                c.Types = ParseTypes(names);
            }
            var effects = MiniJson.List(d, "effects");
            if (effects != null)
                foreach (var e in effects) c.Effects.Add(EffectBlock.FromJson((Dictionary<string, object>)e));
            var reaction = MiniJson.Obj(d, "reaction");
            if (reaction != null)
                c.Reaction = new ReactionDef { Trigger = MiniJson.Str(reaction, "trigger"), Effect = MiniJson.Str(reaction, "effect") };
            return c;
        }
    }

    public sealed class CardDatabase
    {
        readonly Dictionary<string, CardDef> byId = new Dictionary<string, CardDef>();
        public readonly List<CardDef> All = new List<CardDef>();

        public CardDef this[string id]
        {
            get
            {
                if (id == null || !byId.TryGetValue(id, out var c)) throw new KeyNotFoundException("알 수 없는 카드 ID: " + id);
                return c;
            }
        }

        public bool Contains(string id) => id != null && byId.ContainsKey(id);

        public IEnumerable<CardDef> Basics { get { foreach (var c in All) if (c.Basic) yield return c; } }
        public IEnumerable<CardDef> KingdomPool { get { foreach (var c in All) if (!c.Basic) yield return c; } }

        public void Add(CardDef c)
        {
            if (byId.ContainsKey(c.Id)) throw new FormatException("카드 ID 중복: " + c.Id);
            byId[c.Id] = c;
            All.Add(c);
        }

        public static CardDatabase FromJson(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var db = new CardDatabase();
            foreach (var o in MiniJson.List(root, "cards")) db.Add(CardDef.FromJson((Dictionary<string, object>)o));
            // 효과에서 참조하는 카드 ID 검증
            foreach (var c in db.All) foreach (var e in c.Effects) Validate(db, c, e);
            return db;
        }

        static void Validate(CardDatabase db, CardDef c, EffectBlock e)
        {
            if (e.CardId != null && !db.Contains(e.CardId))
                throw new FormatException(c.Id + " 효과가 없는 카드를 참조합니다: " + e.CardId);
            foreach (var sub in e.Effects) Validate(db, c, sub);
        }
    }
}
