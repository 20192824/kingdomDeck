using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomDeck.Engine
{
    /// <summary>효과 스택에 쌓이는 한 단계</summary>
    sealed class EffectStep
    {
        public int Player;
        public EffectBlock Block;
        public string Source;
        /// <summary>내부 단계 ("reactionPrompt") — null이면 Block.Op 사용</summary>
        public string Special;
        public string Op => Special ?? Block.Op;
    }

    public sealed partial class Game
    {
        void PushEffects(int player, List<EffectBlock> blocks, string source)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
                work.Add(new EffectStep { Player = player, Block = blocks[i], Source = source });
        }

        void RunWork()
        {
            while (!IsOver && awaiting == null && work.Count > 0)
            {
                var step = work[work.Count - 1];
                work.RemoveAt(work.Count - 1);
                if (!RunStep(step))
                {
                    awaiting = step;
                    return;
                }
            }
        }

        void ResolveAwaiting(Command c)
        {
            var step = awaiting;
            awaiting = null;
            if (step != null) ResolveStep(step, c);
        }

        /// <summary>단계를 실행한다. 선택이 필요하면 Pending을 세우고 false.</summary>
        bool RunStep(EffectStep s)
        {
            var b = s.Block;
            var p = Players[s.Player];
            switch (s.Op)
            {
                case "drawCards":
                    Draw(s.Player, b.N);
                    return true;

                case "addActions":
                    Actions += b.N;
                    EmitResources();
                    return true;

                case "addBuys":
                    Buys += b.N;
                    EmitResources();
                    return true;

                case "addCoins":
                    Coins += b.N;
                    EmitResources();
                    return true;

                case "costReduction":
                    CostDiscount += b.N;
                    EmitResources();
                    return true;

                case "gainCard":
                {
                    if (b.IfTrashed && !trashedSomething) return true;
                    if (b.CardId != null)
                    {
                        Gain(s.Player, b.CardId, b.To);
                        return true;
                    }
                    int limit = int.MaxValue;
                    if (b.MaxCost >= 0) limit = b.MaxCost;
                    if (b.CostUpToTrashedPlus >= 0) limit = Math.Min(limit, lastTrashedCost + b.CostUpToTrashedPlus);
                    var options = SupplyOrder.Where(id => SupplyCount(id) > 0 && Db[id].Cost <= limit
                                                          && (b.Filter == CardType.None || Db[id].Is(b.Filter))).ToList();
                    if (options.Count == 0) return true;
                    Pending = new PendingDecision
                    {
                        Kind = DecisionKind.ChooseSupply, Player = s.Player, From = Zone.Supply, Min = 1, Max = 1,
                        Reason = b.Reason ?? "획득할 카드", SourceCard = s.Source, SupplyChoices = options,
                    };
                    return false;
                }

                case "trashFromHand":
                {
                    var eligible = new List<int>();
                    for (int i = 0; i < p.Hand.Count; i++)
                        if (b.Filter == CardType.None || Db[p.Hand[i]].Is(b.Filter)) eligible.Add(i);
                    int max = Math.Min(b.Max, eligible.Count);
                    if (max <= 0) { trashedSomething = false; return true; }
                    Pending = new PendingDecision
                    {
                        Kind = DecisionKind.ChooseCards, Player = s.Player, From = Zone.Hand,
                        Min = Math.Min(b.Min, max), Max = max,
                        Reason = b.Reason ?? "폐기할 카드", SourceCard = s.Source, CardChoices = eligible,
                    };
                    return false;
                }

                case "discardDownTo":
                {
                    int need = p.Hand.Count - b.N;
                    if (need <= 0) return true;
                    Pending = new PendingDecision
                    {
                        Kind = DecisionKind.ChooseCards, Player = s.Player, From = Zone.Hand, Min = need, Max = need,
                        Reason = b.Reason ?? ("버릴 카드 " + need + "장"), SourceCard = s.Source,
                        CardChoices = Enumerable.Range(0, p.Hand.Count).ToList(),
                    };
                    return false;
                }

                case "attack":
                {
                    int target = Opponent(s.Player);
                    bool canBlock = Players[target].Hand.Any(id => Db[id].BlocksAttacks);
                    if (canBlock)
                        work.Add(new EffectStep { Player = target, Block = b, Source = s.Source, Special = "reactionPrompt" });
                    else
                        PushEffects(target, b.Effects, s.Source);
                    return true;
                }

                case "reactionPrompt":
                {
                    string blocker = p.Hand.First(id => Db[id].BlocksAttacks);
                    Pending = new PendingDecision
                    {
                        Kind = DecisionKind.YesNo, Player = s.Player, From = Zone.Hand, Min = 0, Max = 1,
                        Reason = "[" + Db[blocker].Name + "]을(를) 공개해 [" + Db[s.Source].Name + "]의 공격을 막을까요?",
                        SourceCard = s.Source,
                    };
                    return false;
                }

                default:
                    throw new InvalidOperationException("알 수 없는 효과 블록: " + s.Op);
            }
        }

        void ResolveStep(EffectStep s, Command c)
        {
            var b = s.Block;
            switch (s.Op)
            {
                case "gainCard":
                    Gain(s.Player, c.CardId, b.To);
                    break;

                case "trashFromHand":
                {
                    var idx = (c.Indices ?? new int[0]).OrderByDescending(i => i).ToList();
                    trashedSomething = idx.Count > 0;
                    lastTrashedCost = -1;
                    foreach (int i in idx)
                    {
                        lastTrashedCost = Math.Max(lastTrashedCost, Db[Players[s.Player].Hand[i]].Cost);
                        TrashFromHand(s.Player, i);
                    }
                    break;
                }

                case "discardDownTo":
                    foreach (int i in (c.Indices ?? new int[0]).OrderByDescending(i => i)) DiscardFromHand(s.Player, i);
                    break;

                case "reactionPrompt":
                    if (c.Yes)
                    {
                        string blocker = Players[s.Player].Hand.First(id => Db[id].BlocksAttacks);
                        Emit(new GameEvent { Type = GameEventType.AttackBlocked, Player = s.Player, CardId = blocker, Amount = 0 });
                    }
                    else
                    {
                        PushEffects(s.Player, b.Effects, s.Source);
                    }
                    break;

                default:
                    throw new InvalidOperationException("선택이 필요 없는 효과입니다: " + s.Op);
            }
            CheckInstantEnd();
        }
    }
}
