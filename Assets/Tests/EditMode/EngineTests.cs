using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomDeck.AI;
using KingdomDeck.Engine;
using NUnit.Framework;

namespace KingdomDeck.Tests
{
    public static class TestUtil
    {
        static CardDatabase db;
        public static CardDatabase Db => db ?? (db = CardDatabase.FromJson(File.ReadAllText("Assets/Data/cards.json")));

        public static readonly List<string> KingdomA = new List<string>
            { "wall", "altar", "village", "workshop", "smithy", "guild", "mercenary", "market", "mine", "witch" };
        public static readonly List<string> KingdomB = new List<string>
            { "wall", "altar", "village", "workshop", "smithy", "guild", "market", "monastery", "witch", "banquet" };

        public static Game NewGame(long seed = 1, List<string> kingdom = null, bool instantEnd = true, int first = 0)
        {
            var g = new Game(Db, new GameOptions { Seed = seed, Kingdom = kingdom ?? KingdomA, InstantCastleEnd = instantEnd, FirstPlayer = first });
            g.Start();
            return g;
        }

        public static void Ok(Game g, Command c)
        {
            Assert.IsTrue(g.Submit(c), "명령 실패: " + c + " — " + g.LastError);
        }

        /// <summary>현재 플레이어의 손패를 정하고 행동 단계부터 다시 시작</summary>
        public static Game WithHand(Game g, params string[] hand)
        {
            g.DebugSetHand(g.Current, hand);
            return g;
        }

        public static int HandIndex(Game g, int player, string id) => g.Players[player].Hand.IndexOf(id);
    }

    // =====================================================================
    // 1단계: 엔진 + 기본 카드
    // =====================================================================
    public class EngineBasicsTests
    {
        [Test]
        public void CardDatabase_Loads27Cards()
        {
            var db = TestUtil.Db;
            Assert.AreEqual(27, db.All.Count);
            Assert.AreEqual(7, db.Basics.Count());
            Assert.AreEqual(20, db.KingdomPool.Count());
            Assert.AreEqual(10, db["volunteers"].PowerPerCards);
            Assert.AreEqual(6, db["dragon"].Power);
            Assert.AreEqual(46, db["copper"].Supply);
            Assert.IsTrue(db["wall"].BlocksAttacks);
        }

        [Test]
        public void Setup_StartingDecksHandsAndCastles()
        {
            var g = TestUtil.NewGame(seed: 7, kingdom: null);
            for (int p = 0; p < 2; p++)
            {
                Assert.AreEqual(5, g.Players[p].Hand.Count);
                Assert.AreEqual(5, g.Players[p].Deck.Count);
                Assert.AreEqual(7, g.Players[p].Count("copper"));
                Assert.AreEqual(3, g.Players[p].Count("archers"));
                Assert.AreEqual(30, g.CastleHp(p), "시작 체력은 30");
                Assert.AreEqual(30, Game.StartingHp);
            }
            Assert.AreEqual(17, g.SupplyOrder.Count);
            Assert.AreEqual(10, g.Kingdom.Count);
            Assert.AreEqual(1, g.TurnNumber);
            Assert.IsNotNull(g.Pending);
        }

        [Test]
        public void RandomKingdom_TenDistinctCards_SameSeedSameGame()
        {
            var a = new Game(TestUtil.Db, new GameOptions { Seed = 42 }); a.Start();
            var b = new Game(TestUtil.Db, new GameOptions { Seed = 42 }); b.Start();
            Assert.AreEqual(10, a.Kingdom.Distinct().Count());
            CollectionAssert.AreEqual(a.Kingdom, b.Kingdom);
            CollectionAssert.AreEqual(a.Players[0].Hand, b.Players[0].Hand);
            CollectionAssert.AreEqual(a.Players[1].Deck, b.Players[1].Deck);
            Assert.AreEqual(a.FirstPlayer, b.FirstPlayer);
        }

        [Test]
        public void Draw_ShufflesDiscardWhenDeckEmpty()
        {
            var g = TestUtil.NewGame();
            var p = g.Players[0];
            p.Deck.Clear();
            p.Discard.Clear();
            p.Discard.AddRange(new[] { "silver", "gold", "copper" });
            int before = g.Events.Count;
            g.Draw(0, 2);
            Assert.AreEqual(7, p.Hand.Count);
            Assert.AreEqual(1, p.Deck.Count);
            Assert.AreEqual(0, p.Discard.Count);
            Assert.IsTrue(g.Events.Skip(before).Any(e => e.Type == GameEventType.Shuffled));
        }

        [Test]
        public void Draw_StopsWhenDeckAndDiscardEmpty()
        {
            var g = TestUtil.NewGame();
            var p = g.Players[0];
            p.Deck.Clear();
            p.Discard.Clear();
            p.Deck.Add("silver");
            g.Draw(0, 3);
            Assert.AreEqual(6, p.Hand.Count);
            Assert.AreEqual(0, p.Deck.Count);
        }

        [Test]
        public void Turn_ActionBuyCleanup_DrawsFiveAndPassesTurn()
        {
            var g = TestUtil.WithHand(TestUtil.NewGame(), "copper", "copper", "copper", "archers", "archers");
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind, "행동 카드가 없으면 구매 단계로 바로 간다");
            TestUtil.Ok(g, Command.PlayAllTreasures());
            Assert.AreEqual(3, g.Coins);
            TestUtil.Ok(g, Command.Buy("silver"));
            Assert.AreEqual(0, g.Coins);
            Assert.AreEqual(0, g.Buys);
            Assert.IsFalse(g.Submit(Command.Buy("copper")), "구매 횟수가 없으면 살 수 없다");
            TestUtil.Ok(g, Command.EndPhase());
            Assert.AreEqual(1, g.Current);
            Assert.AreEqual(5, g.Players[0].Hand.Count);
            Assert.AreEqual(1, g.Players[0].TurnsTaken);
            Assert.AreEqual(1, g.Players[0].Count("silver"));
            Assert.AreEqual(39, g.SupplyCount("silver"));
        }

        [Test]
        public void Buy_RejectsWhenNotEnoughCoins()
        {
            var g = TestUtil.WithHand(TestUtil.NewGame(), "copper", "copper", "archers", "archers", "archers");
            TestUtil.Ok(g, Command.PlayAllTreasures());
            Assert.IsFalse(g.Submit(Command.Buy("silver")));
            Assert.IsNotNull(g.LastError);
        }

        [Test]
        public void CastleHp_SiegeBuyDamagesOpponent_TraitorDamagesSelf()
        {
            var g = TestUtil.WithHand(TestUtil.NewGame(), "gold", "silver", "copper", "copper", "copper");
            int opp = g.CastleHp(1), me = g.CastleHp(0);
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("catapult"));
            Assert.AreEqual(opp - 3, g.CastleHp(1), "투석기 공격력 3");
            Assert.AreEqual(me, g.CastleHp(0));
            var ev = g.Events.Last(e => e.Type == GameEventType.CastleDamaged);
            Assert.AreEqual(1, ev.Player);
            Assert.AreEqual(3, ev.Amount);
            Assert.AreEqual(opp - 3, ev.Value);
            Assert.IsTrue(g.Events.Any(e => e.Type == GameEventType.SiegeAttack && e.CardId == "catapult"));

            g.Players[1].Discard.Add("traitor");
            Assert.AreEqual(opp - 4, g.CastleHp(1), "배신자 1장당 -1");
        }

        [Test]
        public void CastleHp_MatchesVictoryPointDifference()
        {
            var g = TestUtil.NewGame();
            g.Players[0].Discard.AddRange(new[] { "dragon", "catapult", "traitor" });
            g.Players[1].Discard.AddRange(new[] { "archers", "traitor", "traitor" });
            int vp0 = 3 + 6 + 3 - 1, vp1 = 3 + 1 - 2;
            Assert.AreEqual(vp0 - vp1, g.CastleHp(0) - g.CastleHp(1));
        }

        [Test]
        public void DragonsGone_DoesNotEndGame()
        {
            // 2026-11-27 규칙 변경: 드래곤 더미가 비어도 게임은 계속된다
            var g = TestUtil.NewGame();
            g.DebugSetSupply("dragon", 1);
            TestUtil.WithHand(g, "gold", "gold", "silver", "copper", "copper");
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("dragon"));
            TestUtil.Ok(g, Command.EndPhase());
            Assert.AreEqual(0, g.SupplyCount("dragon"));
            Assert.IsFalse(g.IsOver, "드래곤 소진으로는 끝나지 않는다");
            Assert.AreEqual(1, g.Current);
            Assert.AreEqual(1, g.EmptyPiles, "빈 더미 수에는 센다");
        }

        [Test]
        public void DragonPile_CountsTowardThreeEmptyPiles()
        {
            var g = TestUtil.NewGame();
            g.DebugSetSupply("village", 0);
            g.DebugSetSupply("wall", 0);
            g.DebugSetSupply("dragon", 1);
            TestUtil.WithHand(g, "gold", "gold", "silver", "copper", "copper");
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("dragon"));
            Assert.IsFalse(g.IsOver, "턴이 끝나야 종료");
            TestUtil.Ok(g, Command.EndPhase());
            Assert.IsTrue(g.IsOver);
            Assert.AreEqual(EndReason.ThreePiles, g.EndReason);
            Assert.AreEqual(GameEventType.GameEnded, g.Events.Last().Type);
        }

        [Test]
        public void GameEnds_WhenThreePilesEmpty()
        {
            var g = TestUtil.NewGame();
            g.DebugSetSupply("village", 0);
            g.DebugSetSupply("wall", 0);
            g.DebugSetSupply("silver", 1);
            TestUtil.WithHand(g, "copper", "copper", "copper", "archers", "archers");
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("silver"));
            TestUtil.Ok(g, Command.EndPhase());
            Assert.IsTrue(g.IsOver);
            Assert.AreEqual(EndReason.ThreePiles, g.EndReason);
        }

        [Test]
        public void InstantEnd_WhenCastleFalls()
        {
            var g = TestUtil.NewGame();
            for (int i = 0; i < 4; i++) g.Players[0].Discard.Add("dragon"); // 상대 체력 33-3-24-3 = 3
            g.Players[0].Discard.Add("catapult");
            Assert.AreEqual(3, g.CastleHp(1));
            TestUtil.WithHand(g, "gold", "silver", "archers", "archers", "archers");
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("catapult"));
            Assert.IsTrue(g.IsOver, "체력 0 이하 → 즉시 종료");
            Assert.AreEqual(EndReason.CastleDestroyed, g.EndReason);
            Assert.AreEqual(0, g.Winner);
            Assert.IsNull(g.Pending);
        }

        [Test]
        public void StartingHp30_FourDragonsSurvive_FifthDestroys()
        {
            var g = TestUtil.WithHand(TestUtil.NewGame(), "gold", "gold", "silver", "copper", "copper");
            // 손패를 바꾸면 궁수대 수가 달라지므로, 내 병기를 궁수대 3장 + 드래곤 4장으로 정확히 맞춘다
            g.Players[0].Deck.RemoveAll(id => id == "archers");
            g.Players[0].Discard.Clear();
            g.Players[0].Discard.AddRange(new[] { "archers", "archers", "archers" });
            Assert.AreEqual(30, g.CastleHp(1));
            for (int i = 0; i < 4; i++) g.Players[0].Discard.Add("dragon");
            Assert.AreEqual(6, g.CastleHp(1), "30 - 드래곤 4장(24) = 6, 아직 버팀");
            Assert.IsFalse(g.IsOver);
            int before = g.CastleHp(1);
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("dragon"));
            Assert.AreEqual(before - 6, g.CastleHp(1));
            Assert.IsTrue(g.IsOver, "5번째 드래곤으로 함락 (30 - 30 = 0)");
            Assert.AreEqual(EndReason.CastleDestroyed, g.EndReason);
            Assert.AreEqual(0, g.Winner);
        }

        [Test]
        public void InstantEnd_CanBeTurnedOff()
        {
            var g = TestUtil.NewGame(instantEnd: false);
            for (int i = 0; i < 4; i++) g.Players[0].Discard.Add("dragon");
            g.Players[0].Discard.Add("catapult");
            TestUtil.WithHand(g, "gold", "silver", "archers", "archers", "archers");
            int before = g.CastleHp(1);
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("catapult"));
            Assert.IsFalse(g.IsOver);
            Assert.AreEqual(before - 3, g.CastleHp(1));
            Assert.LessOrEqual(g.CastleHp(1), 0);
        }

        [Test]
        public void TieBreak_FewerTurnsWins()
        {
            // 0번(선)이 마지막 더미를 비워 끝내면 0번은 1턴, 1번은 0턴 → 동점이면 1번 승
            var g = TestUtil.NewGame();
            g.DebugSetSupply("village", 0);
            g.DebugSetSupply("wall", 0);
            g.DebugSetSupply("dragon", 1);
            TestUtil.WithHand(g, "gold", "gold", "silver", "copper", "copper");
            foreach (var p in g.Players)
            {
                p.Discard.Clear();
                g.DebugSetDeck(p.Index, "copper", "copper", "copper", "copper", "copper");
            }
            g.Players[1].Hand.Clear();
            g.Players[1].Hand.AddRange(new[] { "copper", "copper", "copper", "copper", "copper" });
            g.Players[1].Discard.Add("dragon"); // 1번 미리 +6 → 0번이 드래곤 사면 동점
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("dragon"));
            TestUtil.Ok(g, Command.EndPhase());
            Assert.IsTrue(g.IsOver);
            Assert.AreEqual(g.CastleHp(0), g.CastleHp(1));
            Assert.AreEqual(1, g.Winner);
        }

        [Test]
        public void AiVsAi_BasicCardsOnly_FinishesGame()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var g = AutoPlay.Run(TestUtil.Db, new GameOptions { Seed = seed, CaptureSnapshots = false }, new Bot(Difficulty.Easy), new Bot(Difficulty.Easy));
                Assert.IsTrue(g.IsOver);
                Assert.AreNotEqual(EndReason.TurnLimit, g.EndReason, "seed " + seed);
                foreach (var p in g.Players)
                    foreach (var id in p.AllCards)
                        Assert.IsTrue(TestUtil.Db[id].Basic, "쉬움 AI는 기본 카드만 산다");
            }
        }

        [Test]
        public void Snapshot_ReflectsStateAfterEvent()
        {
            var g = TestUtil.NewGame();
            var last = g.Events.Last();
            Assert.IsNotNull(last.Snap);
            Assert.AreEqual(5, last.Snap.Players[0].Hand.Length);
            Assert.AreEqual(30, last.Snap.Players[1].Hp);
            Assert.AreEqual(g.SupplyOrder.Count, last.Snap.Supply.Length);
        }
    }

    // =====================================================================
    // 2단계: 왕국 카드 12종
    // =====================================================================
    public class KingdomCardTests
    {
        static Game Start(params string[] hand) => TestUtil.WithHand(TestUtil.NewGame(), hand);
        static Game StartB(params string[] hand) => TestUtil.WithHand(TestUtil.NewGame(kingdom: TestUtil.KingdomB), hand);

        [Test]
        public void Wall_DrawsTwo()
        {
            var g = Start("wall", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(0, "silver", "gold", "copper");
            TestUtil.Ok(g, Command.Play(0));
            CollectionAssert.AreEquivalent(new[] { "copper", "copper", "copper", "copper", "silver", "gold" }, g.Players[0].Hand);
        }

        [Test]
        public void Altar_TrashesUpToFour_AndTrashingSiegeRepairsOpponent()
        {
            var g = Start("altar", "copper", "copper", "archers", "traitor");
            g.Players[0].Discard.Add("traitor");
            int oppHp = g.CastleHp(1), myHp = g.CastleHp(0);
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.ChooseCards, g.Pending.Kind);
            Assert.AreEqual(0, g.Pending.Min);
            Assert.AreEqual(4, g.Pending.Max);
            Assert.IsFalse(g.Submit(Command.Choose(0, 1, 2, 3, 0)), "같은 카드 중복 불가");
            TestUtil.Ok(g, Command.Choose(0, 1, 2, 3)); // 동화 2, 궁수대, 배신자
            Assert.AreEqual(0, g.Players[0].Hand.Count);
            Assert.AreEqual(4, g.Trash.Count);
            Assert.AreEqual(oppHp + 1, g.CastleHp(1), "내 궁수대 폐기 → 상대 성 회복");
            Assert.AreEqual(myHp + 1, g.CastleHp(0), "배신자 폐기 → 내 성 회복");
            Assert.IsTrue(g.Events.Any(e => e.Type == GameEventType.CastleRepaired && e.Player == 1));
        }

        [Test]
        public void Altar_CanTrashNothing()
        {
            var g = Start("altar", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose());
            Assert.AreEqual(4, g.Players[0].Hand.Count);
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind);
        }

        [Test]
        public void Village_DrawOne_TwoActions()
        {
            var g = Start("village", "smithy", "copper", "copper", "copper");
            g.DebugSetDeck(0, "silver", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(2, g.Actions);
            Assert.AreEqual(5, g.Players[0].Hand.Count);
            Assert.AreEqual(DecisionKind.ActionPhase, g.Pending.Kind);
            TestUtil.Ok(g, Command.Play(TestUtil.HandIndex(g, 0, "smithy")));
            Assert.AreEqual(1, g.Actions);
            Assert.AreEqual(7, g.Players[0].Hand.Count);
        }

        [Test]
        public void Workshop_GainsCardCostingUpToFour()
        {
            var g = Start("workshop", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.ChooseSupply, g.Pending.Kind);
            CollectionAssert.Contains(g.Pending.SupplyChoices, "smithy");
            CollectionAssert.DoesNotContain(g.Pending.SupplyChoices, "gold");
            CollectionAssert.DoesNotContain(g.Pending.SupplyChoices, "market");
            Assert.IsFalse(g.Submit(Command.ChooseSupply("gold")));
            TestUtil.Ok(g, Command.ChooseSupply("smithy"));
            CollectionAssert.Contains(g.Players[0].Discard, "smithy");
            Assert.AreEqual(9, g.SupplyCount("smithy"));
        }

        [Test]
        public void Workshop_GainingSiegeAlsoAttacks()
        {
            var g = Start("workshop", "copper", "copper", "copper", "copper");
            int before = g.CastleHp(1);
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.ChooseSupply("archers"));
            Assert.AreEqual(before - 1, g.CastleHp(1));
            Assert.IsTrue(g.Events.Any(e => e.Type == GameEventType.SiegeAttack && e.CardId == "archers"));
        }

        [Test]
        public void Smithy_DrawsThree()
        {
            var g = Start("smithy", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(7, g.Players[0].Hand.Count);
            Assert.AreEqual(0, g.Actions);
        }

        [Test]
        public void Guild_PlusBuyPlusTwoCoins()
        {
            var g = Start("guild", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(2, g.Buys);
            Assert.AreEqual(2, g.Coins);
            TestUtil.Ok(g, Command.PlayAllTreasures());
            Assert.AreEqual(6, g.Coins);
            TestUtil.Ok(g, Command.Buy("silver"));
            TestUtil.Ok(g, Command.Buy("silver"));
            Assert.AreEqual(2, g.Players[0].Count("silver"));
        }

        [Test]
        public void Mercenary_OpponentDiscardsDownToThree()
        {
            var g = Start("mercenary", "copper", "copper", "copper", "copper");
            g.Players[1].Hand.Clear();
            g.Players[1].Hand.AddRange(new[] { "copper", "silver", "gold", "archers", "copper" });
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(2, g.Coins);
            Assert.AreEqual(DecisionKind.ChooseCards, g.Pending.Kind);
            Assert.AreEqual(1, g.Pending.Player, "상대가 고른다");
            Assert.AreEqual(2, g.Pending.Min);
            Assert.AreEqual(2, g.Pending.Max);
            Assert.IsFalse(g.Submit(Command.Choose(0)), "정확히 2장");
            TestUtil.Ok(g, Command.Choose(0, 3));
            CollectionAssert.AreEquivalent(new[] { "silver", "gold", "copper" }, g.Players[1].Hand);
            Assert.AreEqual(0, g.Pending.Player, "다시 내 차례");
        }

        [Test]
        public void Mercenary_NoDiscardIfOpponentHasThreeOrFewer()
        {
            var g = Start("mercenary", "copper", "copper", "copper", "copper");
            g.Players[1].Hand.RemoveRange(0, 2);
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(0, g.Pending.Player);
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind);
        }

        [Test]
        public void Market_CardActionBuyCoin()
        {
            var g = Start("market", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(1, g.Actions);
            Assert.AreEqual(2, g.Buys);
            Assert.AreEqual(1, g.Coins);
            Assert.AreEqual(5, g.Players[0].Hand.Count);
        }

        [Test]
        public void Monastery_DrawTwoPlusAction()
        {
            var g = StartB("monastery", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(1, g.Actions);
            Assert.AreEqual(6, g.Players[0].Hand.Count);
        }

        [Test]
        public void Mine_TrashCopper_GainSilverToHand()
        {
            var g = Start("mine", "copper", "archers", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.ChooseCards, g.Pending.Kind);
            CollectionAssert.AreEqual(new[] { 0 }, g.Pending.CardChoices, "재화만 고를 수 있다");
            TestUtil.Ok(g, Command.Choose(0));
            Assert.AreEqual(DecisionKind.ChooseSupply, g.Pending.Kind);
            CollectionAssert.Contains(g.Pending.SupplyChoices, "silver");
            CollectionAssert.DoesNotContain(g.Pending.SupplyChoices, "gold", "동화(0)+3 = 3까지");
            CollectionAssert.DoesNotContain(g.Pending.SupplyChoices, "village", "재화만");
            TestUtil.Ok(g, Command.ChooseSupply("silver"));
            CollectionAssert.Contains(g.Players[0].Hand, "silver");
            CollectionAssert.DoesNotContain(g.Players[0].Hand, "copper");
        }

        [Test]
        public void Mine_SilverToGold()
        {
            var g = Start("mine", "silver", "copper", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose(TestUtil.HandIndex(g, 0, "silver")));
            CollectionAssert.Contains(g.Pending.SupplyChoices, "gold");
            TestUtil.Ok(g, Command.ChooseSupply("gold"));
            CollectionAssert.Contains(g.Players[0].Hand, "gold");
        }

        [Test]
        public void Mine_TrashNothing_GainNothing()
        {
            var g = Start("mine", "silver", "copper", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose());
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind);
        }

        [Test]
        public void Witch_DrawTwo_OpponentGainsTraitor()
        {
            var g = Start("witch", "copper", "copper", "copper", "copper");
            int before = g.CastleHp(1);
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(6, g.Players[0].Hand.Count);
            Assert.AreEqual(1, g.Players[1].Count("traitor"));
            Assert.AreEqual(before - 1, g.CastleHp(1));
            Assert.AreEqual(9, g.SupplyCount("traitor"));
        }

        [Test]
        public void Witch_NoTraitorsLeft_NothingHappens()
        {
            var g = Start("witch", "copper", "copper", "copper", "copper");
            g.DebugSetSupply("traitor", 0);
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(0, g.Players[1].Count("traitor"));
        }

        [Test]
        public void Banquet_DrawTwoPlusBuy_NextPurchaseCostsOneLess()
        {
            var g = StartB("banquet", "copper", "copper", "archers", "archers");
            g.DebugSetDeck(0, "archers", "archers", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(2, g.Buys);
            Assert.AreEqual(1, g.CostDiscount);
            TestUtil.Ok(g, Command.PlayAllTreasures());
            Assert.AreEqual(2, g.Coins);
            Assert.AreEqual(2, g.EffectiveCost("silver"));
            TestUtil.Ok(g, Command.Buy("silver"));
            Assert.AreEqual(0, g.Coins);
            Assert.AreEqual(0, g.CostDiscount, "할인은 다음 구매 1회만");
            Assert.AreEqual(1, g.Players[0].Count("silver"));
        }

        [Test]
        public void Banquet_CostNeverBelowZero()
        {
            var g = StartB("banquet", "copper", "copper", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(0, g.EffectiveCost("copper"));
        }
    }

    // =====================================================================
    // 공격·반응 상호작용
    // =====================================================================
    public class AttackReactionTests
    {
        static Game WithOpponentWall(string attackCard)
        {
            var g = TestUtil.WithHand(TestUtil.NewGame(), attackCard, "copper", "copper", "copper", "copper");
            g.Players[1].Hand.Clear();
            g.Players[1].Hand.AddRange(new[] { "wall", "copper", "copper", "silver", "archers" });
            return g;
        }

        [Test]
        public void Wall_RevealBlocksWitch()
        {
            var g = WithOpponentWall("witch");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.YesNo, g.Pending.Kind);
            Assert.AreEqual(1, g.Pending.Player);
            TestUtil.Ok(g, Command.Answer(true));
            Assert.AreEqual(0, g.Players[1].Count("traitor"));
            Assert.IsTrue(g.Events.Any(e => e.Type == GameEventType.AttackBlocked && e.Player == 1));
            CollectionAssert.Contains(g.Players[1].Hand, "wall", "성벽은 공개만 하고 손에 남는다");
            Assert.AreEqual(6, g.Players[0].Hand.Count, "+2 카드는 그대로");
        }

        [Test]
        public void Wall_DeclineReveal_TakesAttack()
        {
            var g = WithOpponentWall("witch");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Answer(false));
            Assert.AreEqual(1, g.Players[1].Count("traitor"));
        }

        [Test]
        public void Wall_BlocksMercenary()
        {
            var g = WithOpponentWall("mercenary");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Answer(true));
            Assert.AreEqual(5, g.Players[1].Hand.Count);
            Assert.AreEqual(2, g.Coins, "공격이 막혀도 +2 코인은 받는다");
            Assert.AreEqual(0, g.Pending.Player);
        }

        [Test]
        public void Wall_InDiscardDoesNotBlock()
        {
            var g = TestUtil.WithHand(TestUtil.NewGame(), "witch", "copper", "copper", "copper", "copper");
            g.Players[1].Discard.Add("wall");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreNotEqual(DecisionKind.YesNo, g.Pending.Kind);
            Assert.AreEqual(1, g.Players[1].Count("traitor"));
        }

        [Test]
        public void Wall_DeclineMercenary_ThenChooseDiscards()
        {
            var g = WithOpponentWall("mercenary");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Answer(false));
            Assert.AreEqual(DecisionKind.ChooseCards, g.Pending.Kind);
            Assert.AreEqual(1, g.Pending.Player);
            TestUtil.Ok(g, Command.Choose(1, 4));
            Assert.AreEqual(3, g.Players[1].Hand.Count);
        }
    }

    // =====================================================================
    // 저장/불러오기, AI
    // =====================================================================
    public class SaveAndAiTests
    {
        [Test]
        public void SaveLoad_ReplayReproducesState()
        {
            var g = new Game(TestUtil.Db, new GameOptions { Seed = 99 });
            g.Start();
            var bots = new[] { new Bot(Difficulty.Hard), new Bot(Difficulty.Normal) };
            for (int i = 0; i < 150 && !g.IsOver; i++) Assert.IsTrue(g.Submit(bots[g.Pending.Player].Decide(g, g.Pending)));

            string json = SaveGame.From(g).ToJson();
            var restored = SaveGame.FromJson(json).Restore(TestUtil.Db);

            Assert.AreEqual(g.CastleHp(0), restored.CastleHp(0));
            Assert.AreEqual(g.CastleHp(1), restored.CastleHp(1));
            Assert.AreEqual(g.TurnNumber, restored.TurnNumber);
            Assert.AreEqual(g.Current, restored.Current);
            for (int p = 0; p < 2; p++)
            {
                CollectionAssert.AreEqual(g.Players[p].Hand, restored.Players[p].Hand);
                CollectionAssert.AreEqual(g.Players[p].Deck, restored.Players[p].Deck);
                CollectionAssert.AreEqual(g.Players[p].Discard, restored.Players[p].Discard);
            }
            foreach (var id in g.SupplyOrder) Assert.AreEqual(g.SupplyCount(id), restored.SupplyCount(id));
            Assert.AreEqual(g.Pending?.Kind, restored.Pending?.Kind);
        }

        [Test]
        public void AllDifficulties_FinishGamesWithoutErrors()
        {
            var levels = new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };
            int seed = 1000;
            foreach (var a in levels)
                foreach (var b in levels)
                    for (int k = 0; k < 4; k++)
                    {
                        var g = AutoPlay.Run(TestUtil.Db, new GameOptions { Seed = seed++, CaptureSnapshots = false }, new Bot(a), new Bot(b));
                        Assert.IsTrue(g.IsOver);
                    }
        }

        [Test]
        public void HardBeatsEasy_AtLeast70of100()
        {
            int hardWins = 0;
            for (int i = 0; i < 100; i++)
            {
                // 선후공을 번갈아가며
                int hardSeat = i % 2;
                var agents = new IPlayerAgent[2];
                agents[hardSeat] = new Bot(Difficulty.Hard);
                agents[1 - hardSeat] = new Bot(Difficulty.Easy);
                var g = AutoPlay.Run(TestUtil.Db, new GameOptions { Seed = 5000 + i, CaptureSnapshots = false, FirstPlayer = 0 }, agents[0], agents[1]);
                if (g.Winner == hardSeat) hardWins++;
            }
            TestContext.WriteLine("어려움 AI 승리: " + hardWins + " / 100");
            Assert.GreaterOrEqual(hardWins, 70, "어려움 AI가 쉬움 AI를 100판 중 70판 이상 이겨야 한다 (실제 " + hardWins + ")");
        }

        [Test]
        public void NormalBeatsEasy_MoreOftenThanNot()
        {
            int wins = 0;
            for (int i = 0; i < 60; i++)
            {
                int seat = i % 2;
                var agents = new IPlayerAgent[2];
                agents[seat] = new Bot(Difficulty.Normal);
                agents[1 - seat] = new Bot(Difficulty.Easy);
                var g = AutoPlay.Run(TestUtil.Db, new GameOptions { Seed = 9000 + i, CaptureSnapshots = false, FirstPlayer = 0 }, agents[0], agents[1]);
                if (g.Winner == seat) wins++;
            }
            TestContext.WriteLine("보통 AI 승리: " + wins + " / 60");
            Assert.Greater(wins, 30);
        }
    }
}
