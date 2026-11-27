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
        /// <summary>내부 단계 ("reactionPrompt", "beginPlay") — null이면 Block.Op 사용</summary>
        public string Special;
        /// <summary>beginPlay: "한 번 더 사용" 이벤트를 낼지</summary>
        public bool Announce;
        public string Op => Special ?? Block.Op;
    }

    public sealed partial class Game
    {
        void PushEffects(int player, List<EffectBlock> blocks, string source)
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
                work.Add(new EffectStep { Player = player, Block = blocks[i], Source = source });
        }

        /// <summary>
        /// 카드 한 번 사용분을 스택에 쌓는다. 맨 앞에 beginPlay 단계를 두어
        /// "이번 사용에서 폐기했는지" 같은 기억을 사용할 때마다 새로 시작한다.
        /// </summary>
        void PushPlay(int player, string cardId, bool announce)
        {
            PushEffects(player, Db[cardId].Effects, cardId);
            work.Add(new EffectStep { Player = player, Source = cardId, Special = "beginPlay", Announce = announce });
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

        PendingDecision ChooseFrom(EffectStep s, Zone from, List<int> choices, int min, int max, string reason)
        {
            return new PendingDecision
            {
                Kind = DecisionKind.ChooseCards, Player = s.Player, From = from, Min = min, Max = max,
                Reason = reason, SourceCard = s.Source, Op = s.Op, CardChoices = choices,
            };
        }

        /// <summary>단계를 실행한다. 선택이 필요하면 Pending을 세우고 false.</summary>
        bool RunStep(EffectStep s)
        {
            var b = s.Block;
            var p = Players[s.Player];
            // 같은 카드 효과에서 폐기한 카드가 있을 때만 실행하는 블록
            if (s.Special == null && b.IfTrashed && !trashedSomething) return true;

            switch (s.Op)
            {
                case "beginPlay":
                    lastTrashedCost = -1;
                    trashedSomething = false;
                    if (s.Announce)
                        Emit(new GameEvent { Type = GameEventType.CardPlayed, Player = s.Player, CardId = s.Source, From = Zone.InPlay, To = Zone.InPlay });
                    return true;

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
                        Reason = b.Reason ?? "획득할 카드", SourceCard = s.Source, Op = s.Op, SupplyChoices = options,
                    };
                    return false;
                }

                case "trashFromHand":
                {
                    var eligible = new List<int>();
                    for (int i = 0; i < p.Hand.Count; i++)
                    {
                        string id = p.Hand[i];
                        if (b.Filter != CardType.None && !Db[id].Is(b.Filter)) continue;
                        if (b.CardId != null && id != b.CardId) continue;
                        eligible.Add(i);
                    }
                    int max = Math.Min(b.Max, eligible.Count);
                    if (max <= 0) { trashedSomething = false; return true; }
                    Pending = ChooseFrom(s, Zone.Hand, eligible, Math.Min(b.Min, max), max, b.Reason ?? "폐기할 카드");
                    return false;
                }

                case "discardDownTo":
                {
                    int need = p.Hand.Count - b.N;
                    if (need <= 0) return true;
                    Pending = ChooseFrom(s, Zone.Hand, Enumerable.Range(0, p.Hand.Count).ToList(), need, need, b.Reason ?? ("버릴 카드 " + need + "장"));
                    return false;
                }

                case "discardAndDraw":
                    if (p.Hand.Count == 0) return true;
                    Pending = ChooseFrom(s, Zone.Hand, Enumerable.Range(0, p.Hand.Count).ToList(), 0, p.Hand.Count, b.Reason ?? "버리고 같은 수만큼 새로 뽑을 카드");
                    return false;

                case "playActionTimes":
                {
                    var actions = new List<int>();
                    for (int i = 0; i < p.Hand.Count; i++) if (Db[p.Hand[i]].Is(CardType.Action)) actions.Add(i);
                    if (actions.Count == 0) return true;
                    Pending = ChooseFrom(s, Zone.Hand, actions, 0, 1, b.Reason ?? ("" + b.N + "번 사용할 행동 카드"));
                    return false;
                }

                case "discardTop":
                    lastDiscardedTop = null;
                    for (int k = 0; k < Math.Max(1, b.N); k++)
                    {
                        if (!EnsureDeck(s.Player)) break;
                        string id = p.Deck[p.Deck.Count - 1];
                        p.Deck.RemoveAt(p.Deck.Count - 1);
                        p.Discard.Add(id);
                        lastDiscardedTop = id;
                        Emit(new GameEvent { Type = GameEventType.CardDiscarded, Player = s.Player, CardId = id, From = Zone.Deck, To = Zone.Discard });
                    }
                    return true;

                case "mayPlayDiscarded":
                {
                    string id = lastDiscardedTop;
                    if (id == null || p.Discard.Count == 0 || p.Discard[p.Discard.Count - 1] != id) return true;
                    if (b.Filter != CardType.None && !Db[id].Is(b.Filter)) return true;
                    Pending = new PendingDecision
                    {
                        Kind = DecisionKind.YesNo, Player = s.Player, From = Zone.Discard, Min = 0, Max = 1,
                        Reason = b.Reason ?? ("버린 [" + Db[id].Name + "]을(를) 바로 사용할까요? (행동 소모 없음)"),
                        SourceCard = s.Source, Op = s.Op,
                    };
                    return false;
                }

                case "lookAtTop":
                    RevealTop(s.Player, b.N, true);
                    return true;

                case "trashFromRevealed":
                case "discardFromRevealed":
                {
                    if (p.Revealed.Count == 0) return true;
                    string reason = b.Reason ?? (s.Op == "trashFromRevealed" ? "폐기할 카드" : "버릴 카드");
                    Pending = ChooseFrom(s, Zone.Revealed, Enumerable.Range(0, p.Revealed.Count).ToList(), 0, p.Revealed.Count, reason);
                    return false;
                }

                case "returnRevealed":
                    if (p.Revealed.Count == 0) return true;
                    if (p.Revealed.Count == 1)
                    {
                        ReturnRevealed(s.Player, 0);
                        return true;
                    }
                    Pending = ChooseFrom(s, Zone.Revealed, Enumerable.Range(0, p.Revealed.Count).ToList(), 1, 1, b.Reason ?? "덱 맨 위에 둘 카드 (나머지는 그 아래)");
                    return false;

                case "revealTopTrash":
                {
                    // 공격받은 쪽: 덱 위 N장 공개 → 조건에 맞는 재화 1장 폐기(가장 싼 것), 나머지 버림
                    RevealTop(s.Player, b.N, false);
                    int pick = -1;
                    for (int i = 0; i < p.Revealed.Count; i++)
                    {
                        var def = Db[p.Revealed[i]];
                        if (b.Filter != CardType.None && !def.Is(b.Filter)) continue;
                        if (b.Exclude != null && p.Revealed[i] == b.Exclude) continue;
                        if (pick < 0 || def.Cost < Db[p.Revealed[pick]].Cost) pick = i;
                    }
                    if (pick >= 0) TrashFromRevealed(s.Player, pick);
                    while (p.Revealed.Count > 0) DiscardFromRevealed(s.Player, p.Revealed.Count - 1);
                    return true;
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
                        SourceCard = s.Source, Op = s.Op,
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
            var p = Players[s.Player];
            var desc = (c.Indices ?? new int[0]).OrderByDescending(i => i).ToList();
            switch (s.Op)
            {
                case "gainCard":
                    Gain(s.Player, c.CardId, b.To);
                    break;

                case "trashFromHand":
                    trashedSomething = desc.Count > 0;
                    lastTrashedCost = -1;
                    foreach (int i in desc)
                    {
                        lastTrashedCost = Math.Max(lastTrashedCost, Db[p.Hand[i]].Cost);
                        TrashFromHand(s.Player, i);
                    }
                    break;

                case "discardDownTo":
                    foreach (int i in desc) DiscardFromHand(s.Player, i);
                    break;

                case "discardAndDraw":
                    foreach (int i in desc) DiscardFromHand(s.Player, i);
                    if (desc.Count > 0) Draw(s.Player, desc.Count);
                    break;

                case "playActionTimes":
                    if (desc.Count == 1)
                    {
                        int i = desc[0];
                        string id = p.Hand[i];
                        p.Hand.RemoveAt(i);
                        p.InPlay.Add(id);
                        Emit(new GameEvent { Type = GameEventType.CardPlayed, Player = s.Player, CardId = id, Index = i, From = Zone.Hand, To = Zone.InPlay });
                        // 스택이므로 마지막 사용분부터 쌓는다
                        for (int k = Math.Max(1, b.N) - 1; k >= 0; k--) PushPlay(s.Player, id, k > 0);
                    }
                    break;

                case "mayPlayDiscarded":
                    if (c.Yes)
                    {
                        string id = p.Discard[p.Discard.Count - 1];
                        p.Discard.RemoveAt(p.Discard.Count - 1);
                        p.InPlay.Add(id);
                        Emit(new GameEvent { Type = GameEventType.CardPlayed, Player = s.Player, CardId = id, From = Zone.Discard, To = Zone.InPlay });
                        PushPlay(s.Player, id, false);
                    }
                    break;

                case "trashFromRevealed":
                    foreach (int i in desc) TrashFromRevealed(s.Player, i);
                    break;

                case "discardFromRevealed":
                    foreach (int i in desc) DiscardFromRevealed(s.Player, i);
                    break;

                case "returnRevealed":
                {
                    // 고른 카드가 맨 위: 나머지를 먼저 덱에 올리고, 고른 카드를 마지막에
                    int top = desc.Count > 0 ? desc[0] : 0;
                    string topId = p.Revealed[top];
                    p.Revealed.RemoveAt(top);
                    while (p.Revealed.Count > 0) ReturnRevealed(s.Player, p.Revealed.Count - 1);
                    p.Revealed.Add(topId);
                    ReturnRevealed(s.Player, 0);
                    break;
                }

                case "reactionPrompt":
                    if (c.Yes)
                    {
                        string blocker = p.Hand.First(id => Db[id].BlocksAttacks);
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

        // ---------------- 공개한 카드 다루기 ----------------

        /// <summary>덱이 비었으면 버림 더미를 섞어 채운다. 둘 다 비었으면 false.</summary>
        bool EnsureDeck(int player)
        {
            var p = Players[player];
            if (p.Deck.Count > 0) return true;
            if (p.Discard.Count == 0) return false;
            int count = p.Discard.Count;
            p.Deck.AddRange(p.Discard);
            p.Discard.Clear();
            Rng.Shuffle(p.Deck);
            Emit(new GameEvent { Type = GameEventType.Shuffled, Player = player, Amount = count });
            return true;
        }

        void RevealTop(int player, int n, bool isPrivate)
        {
            var p = Players[player];
            p.RevealedPrivate = isPrivate;
            for (int k = 0; k < n; k++)
            {
                if (!EnsureDeck(player)) break;
                string id = p.Deck[p.Deck.Count - 1];
                p.Deck.RemoveAt(p.Deck.Count - 1);
                p.Revealed.Add(id);
                Emit(new GameEvent { Type = GameEventType.CardRevealed, Player = player, CardId = id, From = Zone.Deck, To = Zone.Revealed, Index = p.Revealed.Count - 1, Private = isPrivate });
            }
        }

        void TrashFromRevealed(int player, int index)
        {
            var p = Players[player];
            string id = p.Revealed[index];
            p.Revealed.RemoveAt(index);
            Trash.Add(id);
            Emit(new GameEvent { Type = GameEventType.CardTrashed, Player = player, CardId = id, Index = index, From = Zone.Revealed, To = Zone.Trash });
            SyncHp(id);
        }

        void DiscardFromRevealed(int player, int index)
        {
            var p = Players[player];
            string id = p.Revealed[index];
            p.Revealed.RemoveAt(index);
            p.Discard.Add(id);
            Emit(new GameEvent { Type = GameEventType.CardDiscarded, Player = player, CardId = id, Index = index, From = Zone.Revealed, To = Zone.Discard });
        }

        void ReturnRevealed(int player, int index)
        {
            var p = Players[player];
            string id = p.Revealed[index];
            p.Revealed.RemoveAt(index);
            p.Deck.Add(id);
            Emit(new GameEvent { Type = GameEventType.CardReturned, Player = player, CardId = id, Index = index, From = Zone.Revealed, To = Zone.Deck, Private = p.RevealedPrivate });
        }
    }
}
