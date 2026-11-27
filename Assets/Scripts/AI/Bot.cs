using System;
using System.Collections.Generic;
using System.Linq;
using KingdomDeck.Engine;

namespace KingdomDeck.AI
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>사람(UI)과 AI가 같은 인터페이스로 엔진의 선택 요청에 답한다.</summary>
    public interface IPlayerAgent
    {
        Command Decide(Game game, PendingDecision decision);
    }

    /// <summary>
    /// 난이도별 AI.
    /// 쉬움: 8코인 드래곤 / 6코인 금화 / 3코인 은화.
    /// 보통: 쉬움 + 드로우 카드 1~2장, 후반 투석기.
    /// 어려움: 덱 구성·남은 공급처·성 체력 차이를 평가. 공격·폐기 카드 활용.
    /// </summary>
    public sealed class Bot : IPlayerAgent
    {
        public readonly Difficulty Level;
        public Bot(Difficulty level) { Level = level; }

        public static string LevelName(Difficulty d) => d == Difficulty.Easy ? "쉬움" : d == Difficulty.Normal ? "보통" : "어려움";

        // 행동 카드 우선순위 (높을수록 먼저)
        static readonly Dictionary<string, int> ActionPriority = new Dictionary<string, int>
        {
            { "village", 100 }, { "decree", 98 }, { "market", 95 }, { "watchtower", 93 }, { "warcouncil", 91 }, { "monastery", 90 },
            { "witch", 60 }, { "raiders", 58 }, { "banquet", 55 }, { "smithy", 50 }, { "mercenary", 45 }, { "moneychanger", 44 },
            { "attendant", 42 }, { "engineers", 41 },
            { "mine", 40 }, { "guild", 38 }, { "wall", 35 }, { "workshop", 30 }, { "altar", 20 },
        };

        static int Priority(string id) => ActionPriority.TryGetValue(id, out int pr) ? pr : 10;

        /// <summary>쓸모없는 카드 (배신자, 병기)</summary>
        static bool IsJunk(Game g, string id) => g.Card(id).Is(CardType.Traitor) || g.Card(id).Is(CardType.Siege);

        public Command Decide(Game g, PendingDecision d)
        {
            switch (d.Kind)
            {
                case DecisionKind.ActionPhase: return DecideAction(g, d);
                case DecisionKind.BuyPhase: return DecideBuy(g, d);
                case DecisionKind.ChooseCards: return DecideCards(g, d);
                case DecisionKind.ChooseSupply: return DecideSupply(g, d);
                case DecisionKind.YesNo: return Command.Answer(true); // 성벽은 항상 공개
                default: throw new ArgumentOutOfRangeException();
            }
        }

        // ------------------------------------------------------------------
        // 행동 단계
        // ------------------------------------------------------------------

        Command DecideAction(Game g, PendingDecision d)
        {
            var hand = g.Players[d.Player].Hand;
            int best = -1, bestScore = int.MinValue;
            for (int i = 0; i < hand.Count; i++)
            {
                if (!g.CanPlay(d.Player, i)) continue;
                string id = hand[i];
                int score = Priority(id);
                // 쓸모없는 상황이면 건너뛴다
                if (id == "mine" && !hand.Any(h => h == "copper" || h == "silver")) continue;
                if (id == "altar" && ChooseTrash(g, d.Player, hand, i, 4).Count == 0) continue;
                if (id == "moneychanger" && !hand.Contains("copper")) continue;
                if (id == "warcouncil" && !hand.Where((h, k) => k != i).Any(h => IsJunk(g, h))) continue;
                if (id == "decree" && !hand.Where((h, k) => k != i).Any(h => g.Card(h).Is(CardType.Action))) continue;
                if (id == "engineers" && RemodelScore(g, d.Player, hand, i).score < 30) continue;
                // 행동이 1개 남았으면 행동 +를 주는 카드를 먼저
                if (score > bestScore) { best = i; bestScore = score; }
            }
            return best >= 0 ? Command.Play(best) : Command.EndPhase();
        }

        // ------------------------------------------------------------------
        // 구매 단계
        // ------------------------------------------------------------------

        Command DecideBuy(Game g, PendingDecision d)
        {
            var hand = g.Players[d.Player].Hand;
            if (hand.Any(id => g.Card(id).Is(CardType.Treasure))) return Command.PlayAllTreasures();
            if (g.Buys <= 0) return Command.EndPhase();

            string pick = ChooseBuy(g, d.Player, g.Coins, true);
            if (pick != null && g.CanBuy(d.Player, pick)) return Command.Buy(pick);
            return Command.EndPhase();
        }

        /// <summary>coins로 살(얻을) 카드. 없으면 null.</summary>
        string ChooseBuy(Game g, int me, int coins, bool isPurchase)
        {
            Func<string, bool> ok = id => g.InSupply(id) && g.SupplyCount(id) > 0 && (isPurchase ? g.EffectiveCost(id) : g.Card(id).Cost) <= coins;
            switch (Level)
            {
                case Difficulty.Easy: return BuyEasy(ok);
                case Difficulty.Normal: return BuyNormal(g, me, ok);
                default: return BuyHard(g, me, coins, ok);
            }
        }

        static string BuyEasy(Func<string, bool> ok)
        {
            if (ok("dragon")) return "dragon";
            if (ok("gold")) return "gold";
            if (ok("silver")) return "silver";
            return null;
        }

        static readonly string[] DrawCards = { "smithy", "monastery", "witch", "banquet", "wall" };

        string BuyNormal(Game g, int me, Func<string, bool> ok)
        {
            var p = g.Players[me];
            int dragons = g.SupplyCount("dragon");
            if (ok("dragon")) return "dragon";
            if (dragons <= 4 && ok("catapult") && !ok("gold")) return "catapult";
            if (ok("gold")) return dragons <= 3 && ok("catapult") ? "catapult" : "gold";
            if (dragons <= 4 && ok("catapult")) return "catapult";
            int drawOwned = DrawCards.Sum(id => p.Count(id));
            if (drawOwned < (p.TotalCards >= 16 ? 2 : 1))
                foreach (var id in DrawCards)
                    if (id != "wall" && ok(id) && g.Card(id).Cost >= 4) return id;
            if (ok("silver")) return "silver";
            if (dragons <= 2 && ok("archers")) return "archers";
            return null;
        }

        string BuyHard(Game g, int me, int coins, Func<string, bool> ok)
        {
            var p = g.Players[me];
            int opp = Game.Opponent(me);
            int myHp = g.CastleHp(me), oppHp = g.CastleHp(opp);
            int dragons = g.SupplyCount("dragon");

            // 1) 성 함락으로 바로 이길 수 있으면 그 병기를 산다
            if (g.Options.InstantCastleEnd)
                foreach (var id in new[] { "archers", "volunteers", "catapult", "dragon" })
                    if (ok(id) && oppHp - PowerIfBought(g, me, id) <= 0 && myHp > 0) return id;

            // 2) 3더미 종료가 임박했고 이기는 중이면 마지막 더미를 비워서 끝낸다
            if (g.EmptyPiles == 2)
            {
                foreach (var id in g.SupplyOrder)
                    if (g.SupplyCount(id) == 1 && ok(id))
                    {
                        int gain = PowerIfBought(g, me, id); // 병기면 차이가 더 벌어진다
                        if ((myHp - (oppHp - gain)) > 0) return id;
                    }
            }

            // 3) 드래곤 소진 직전: 마지막 드래곤은 이길 때만
            if (ok("dragon"))
            {
                if (dragons == 1 && myHp <= oppHp - 6) { /* 사면 지면서 끝남 → 다른 걸 산다 */ }
                else if (p.Count("gold") > 0 || dragons <= 6 || TreasureValue(g, p) >= 16) return "dragon";
            }

            int witches = p.Count("witch");
            int terminals = p.AllCards.Count(id => g.Card(id).Is(CardType.Action) && !GivesActions(g, id));
            int deck = p.TotalCards;
            bool endgame = dragons <= 3 || g.EmptyPiles >= 2;
            bool lateGame = dragons <= 5;

            if (coins >= 6)
            {
                if (dragons <= 4 && ok("catapult")) return "catapult";
                if (ok("gold")) return "gold";
            }
            if (coins >= 5)
            {
                if (lateGame && ok("volunteers") && PowerIfBought(g, me, "volunteers") > 3) return "volunteers";
                if (lateGame && ok("catapult")) return "catapult";
                if (ok("witch") && witches < 2 && g.SupplyCount("traitor") > 2) return "witch";
                if (ok("market") && terminals >= 2) return "market";
                if (ok("monastery") && p.Count("monastery") < 2 && witches == 0) return "monastery";
                if (ok("banquet") && terminals < deck / 9 && witches == 0) return "banquet";
            }
            if (coins >= 4)
            {
                // 덱이 크면 의용군(10장당 1)이 투석기만큼 세다
                if (lateGame && ok("volunteers") && PowerIfBought(g, me, "volunteers") >= 3) return "volunteers";
                if (endgame && ok("archers") && coins == 4 && dragons <= 2) return "archers";
                bool witchAvailable = g.InSupply("witch") && g.SupplyCount("witch") > 0;
                if (witches == 0 && !witchAvailable && ok("smithy") && p.Count("smithy") < 1 + deck / 14) return "smithy";
                if (ok("mercenary") && p.Count("mercenary") < 1 && witches == 0 && p.Count("smithy") == 0) return "mercenary";
                if (ok("smithy") && terminals < deck / 11 && terminals < 2) return "smithy";
            }
            if (coins >= 3)
            {
                if (endgame && dragons <= 2 && ok("archers")) return "archers";
                if (ok("silver")) return "silver";
            }
            if (coins >= 2)
            {
                if (dragons <= 3 && ok("archers")) return "archers";
                // 배신자가 많이 들어왔으면 제단으로 정리
                if (ok("altar") && p.Count("altar") == 0 && p.Count("traitor") >= 2 && !lateGame) return "altar";
                if (ok("wall") && p.Count("wall") == 0 && OpponentHasAttack(g, opp) && !lateGame) return "wall";
            }
            return null;
        }

        /// <summary>이 카드를 사면 더해지는 공격력 (의용군은 사고 난 뒤 덱 크기로 계산)</summary>
        static int PowerIfBought(Game g, int me, string id)
        {
            var def = g.Card(id);
            int power = def.Power;
            if (def.PowerPerCards > 0)
            {
                int after = g.Players[me].TotalCards + 1;
                int owned = g.Players[me].Count(id);
                // 새 의용군 자신 + 이미 가진 의용군들이 10장 단위를 넘어 늘어나는 몫
                power += after / def.PowerPerCards + owned * (after / def.PowerPerCards - (after - 1) / def.PowerPerCards);
            }
            return power;
        }

        static bool OpponentHasAttack(Game g, int opp) => g.Players[opp].AllCards.Any(id => g.Card(id).Is(CardType.Attack));

        static bool GivesActions(Game g, string id) => g.Card(id).Effects.Any(e => e.Op == "addActions");

        static int TreasureValue(Game g, PlayerState p) => p.AllCards.Sum(id => g.Card(id).Coins);

        // ------------------------------------------------------------------
        // 손패 고르기 (폐기 / 버리기)
        // ------------------------------------------------------------------

        Command DecideCards(Game g, PendingDecision d)
        {
            var me = g.Players[d.Player];
            // 공개한 카드에서 고를 때는 그 목록, 아니면 손패
            var hand = d.From == Zone.Revealed ? me.Revealed : me.Hand;
            List<int> pick;
            switch (d.Op)
            {
                case "trashFromHand":
                    if (d.SourceCard == "altar") pick = ChooseTrash(g, d.Player, hand, -1, d.Max);
                    else if (d.SourceCard == "mine") pick = ChooseMineTrash(g, hand, d.CardChoices);
                    else if (d.SourceCard == "moneychanger") pick = d.CardChoices.Take(1).ToList();
                    else if (d.SourceCard == "engineers") pick = new List<int> { RemodelScore(g, d.Player, hand, -1).index };
                    else pick = ChooseDiscard(g, hand, d.CardChoices, d.Min);
                    break;
                case "discardAndDraw":
                    // 쓸모없는 카드(배신자, 병기)만 버리고 새로 뽑는다
                    pick = d.CardChoices.Where(i => IsJunk(g, hand[i])).ToList();
                    break;
                case "playActionTimes":
                {
                    // 두 번 쓰면 가장 좋은 카드 (드로우·공격 카드 우선)
                    int best = -1;
                    foreach (int i in d.CardChoices)
                        if (best < 0 || DoublePlayValue(hand[i]) > DoublePlayValue(hand[best])) best = i;
                    pick = best >= 0 ? new List<int> { best } : new List<int>();
                    break;
                }
                case "trashFromRevealed":
                {
                    int treasure = TreasureValue(g, me);
                    pick = d.CardChoices.Where(i => hand[i] == "traitor" || (Level != Difficulty.Easy && hand[i] == "copper" && treasure > 9)).ToList();
                    break;
                }
                case "discardFromRevealed":
                    pick = d.CardChoices.Where(i => g.Card(hand[i]).Is(CardType.Siege) || hand[i] == "copper").ToList();
                    break;
                case "returnRevealed":
                    pick = new List<int> { d.CardChoices.OrderByDescending(i => KeepValue(g, hand[i])).First() };
                    break;
                default:
                    pick = ChooseDiscard(g, hand, d.CardChoices, d.Min);
                    break;
            }

            // 최소/최대 맞추기
            pick = pick.Where(i => d.CardChoices.Contains(i)).Distinct().Take(d.Max).ToList();
            if (pick.Count < d.Min)
            {
                foreach (int i in d.CardChoices.OrderBy(i => KeepValue(g, hand[i])))
                {
                    if (pick.Count >= d.Min) break;
                    if (!pick.Contains(i)) pick.Add(i);
                }
            }
            return Command.Choose(pick.ToArray());
        }

        /// <summary>제단으로 폐기할 카드: 배신자 → (쉬운 난이도 제외) 동화 → 초반 궁수대</summary>
        List<int> ChooseTrash(Game g, int me, List<string> hand, int skipIndex, int max)
        {
            var p = g.Players[me];
            var result = new List<int>();
            int treasureValue = TreasureValue(g, p);
            int coppers = p.Count("copper");
            bool early = p.TurnsTaken < 8;
            for (int i = 0; i < hand.Count && result.Count < max; i++)
            {
                if (i == skipIndex) continue;
                string id = hand[i];
                if (id == "traitor") result.Add(i);
            }
            if (Level == Difficulty.Easy) return result;
            for (int i = 0; i < hand.Count && result.Count < max; i++)
            {
                if (i == skipIndex || result.Contains(i)) continue;
                string id = hand[i];
                // 재화 총합이 너무 줄지 않게 동화를 조금 남긴다
                if (id == "copper" && treasureValue - 1 >= 7 && coppers > 3) { result.Add(i); treasureValue--; coppers--; }
                else if (id == "archers" && early && Level == Difficulty.Hard && g.SupplyCount("dragon") >= 7) result.Add(i);
            }
            return result;
        }

        static int DoublePlayValue(string id)
        {
            switch (id)
            {
                case "witch": return 100;
                case "smithy": return 90;
                case "raiders": return 88;
                case "monastery": case "market": case "banquet": return 80;
                case "guild": case "mercenary": return 70;
                case "village": return 50;
                default: return Priority(id) / 2;
            }
        }

        /// <summary>공병대로 개조할 카드와 그 가치. 배신자 → (후반) 금화→드래곤 → (초반) 궁수대 → 동화</summary>
        (int index, int score) RemodelScore(Game g, int me, List<string> hand, int skipIndex)
        {
            int dragons = g.SupplyCount("dragon");
            bool late = dragons <= 4;
            bool early = g.Players[me].TurnsTaken < 8;
            int best = -1, bestScore = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (i == skipIndex) continue;
                string id = hand[i];
                int s;
                if (id == "traitor") s = 100;
                else if (id == "gold" && late && dragons > 0) s = 90;
                else if (id == "silver" && late && g.SupplyCount("catapult") > 0) s = 60;
                else if (id == "archers" && early && Level == Difficulty.Hard) s = 50;
                else if (id == "copper") s = late ? 35 : 30;
                else s = 5;
                if (best < 0 || s > bestScore) { best = i; bestScore = s; }
            }
            return (Math.Max(0, best), bestScore);
        }

        static List<int> ChooseMineTrash(Game g, List<string> hand, List<int> choices)
        {
            // 은화 → 금화가 가장 이득, 다음은 동화 → 은화
            if (choices.Any(i => hand[i] == "silver") && g.SupplyCount("gold") > 0) return new List<int> { choices.First(i => hand[i] == "silver") };
            if (choices.Any(i => hand[i] == "copper") && g.SupplyCount("silver") > 0) return new List<int> { choices.First(i => hand[i] == "copper") };
            return new List<int>();
        }

        static List<int> ChooseDiscard(Game g, List<string> hand, List<int> choices, int count)
        {
            return choices.OrderBy(i => KeepValue(g, hand[i])).Take(count).ToList();
        }

        /// <summary>손에 남겨 둘 가치 (낮을수록 먼저 버림): 배신자·병기 → 동화 → ...</summary>
        static int KeepValue(Game g, string id)
        {
            var c = g.Card(id);
            if (c.Is(CardType.Traitor)) return 0;
            if (c.Is(CardType.Siege)) return 1;
            if (c.Is(CardType.Treasure)) return 10 + c.Coins * 10;
            if (c.Is(CardType.Action)) return 15 + c.Cost * 4;
            return 5;
        }

        // ------------------------------------------------------------------
        // 공급처 고르기 (공방, 광산)
        // ------------------------------------------------------------------

        Command DecideSupply(Game g, PendingDecision d)
        {
            var choices = d.SupplyChoices;
            if (d.SourceCard == "mine")
            {
                string best = choices.OrderByDescending(id => g.Card(id).Coins).First();
                return Command.ChooseSupply(best);
            }
            int maxCost = choices.Max(id => g.Card(id).Cost);
            string pick = ChooseBuy(g, d.Player, maxCost, false);
            if (pick == null || !choices.Contains(pick))
            {
                // 기본값: 은화 → 가장 비싼 행동 카드 → 아무거나 (배신자/동화는 피함)
                if (choices.Contains("silver")) pick = "silver";
                else
                    pick = choices.Where(id => id != "traitor" && id != "copper")
                                  .OrderByDescending(id => g.Card(id).Cost).FirstOrDefault() ?? choices[0];
            }
            return Command.ChooseSupply(pick);
        }
    }

    /// <summary>AI끼리 자동 대전 (연출 없이)</summary>
    public static class AutoPlay
    {
        /// <summary>한 판을 끝까지 진행하고 게임을 돌려준다.</summary>
        public static Game Run(CardDatabase db, GameOptions options, IPlayerAgent a0, IPlayerAgent a1, int maxCommands = 20000)
        {
            var g = new Game(db, options);
            g.Start();
            var agents = new[] { a0, a1 };
            int n = 0;
            while (!g.IsOver)
            {
                if (++n > maxCommands) throw new InvalidOperationException("명령이 너무 많습니다 (무한 루프?)");
                var d = g.Pending;
                var c = agents[d.Player].Decide(g, d);
                if (!g.Submit(c)) throw new InvalidOperationException("AI가 잘못된 명령을 냈습니다: " + c + " / " + d + " / " + g.LastError);
            }
            return g;
        }
    }
}
