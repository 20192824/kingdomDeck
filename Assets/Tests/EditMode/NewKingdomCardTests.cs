using System.Collections.Generic;
using System.Linq;
using KingdomDeck.AI;
using KingdomDeck.Engine;
using NUnit.Framework;

namespace KingdomDeck.Tests
{
    /// <summary>2026-11-27 추가한 왕국 카드 8종 (작전 회의, 시종, 공병대, 환전상, 왕의 칙령, 의용군, 약탈대, 망루)</summary>
    public class NewKingdomCardTests
    {
        public static readonly List<string> KingdomC = new List<string>
            { "warcouncil", "attendant", "engineers", "moneychanger", "decree", "volunteers", "raiders", "watchtower", "smithy", "wall" };

        static Game Start(params string[] hand) => TestUtil.WithHand(TestUtil.NewGame(kingdom: KingdomC), hand);
        static int Idx(Game g, string id) => g.Players[0].Hand.IndexOf(id);

        // ---------------- 작전 회의 ----------------

        [Test]
        public void WarCouncil_PlusAction_DiscardThenDrawSameNumber()
        {
            var g = Start("warcouncil", "traitor", "archers", "copper", "copper");
            g.DebugSetDeck(0, "silver", "gold", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(1, g.Actions, "+1 행동");
            Assert.AreEqual(DecisionKind.ChooseCards, g.Pending.Kind);
            Assert.AreEqual(0, g.Pending.Min);
            Assert.AreEqual(4, g.Pending.Max);
            TestUtil.Ok(g, Command.Choose(Idx(g, "traitor"), Idx(g, "archers")));
            CollectionAssert.AreEquivalent(new[] { "copper", "copper", "silver", "gold" }, g.Players[0].Hand);
            CollectionAssert.IsSubsetOf(new[] { "traitor", "archers" }, g.Players[0].Discard);
        }

        [Test]
        public void WarCouncil_DiscardNothing_DrawNothing()
        {
            var g = Start("warcouncil", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose());
            Assert.AreEqual(4, g.Players[0].Hand.Count);
        }

        // ---------------- 시종 ----------------

        [Test]
        public void Attendant_DiscardedAction_CanBePlayedWithoutAction()
        {
            var g = Start("attendant", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(0, "smithy", "silver", "silver", "silver", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(2, g.Coins);
            Assert.AreEqual(DecisionKind.YesNo, g.Pending.Kind);
            Assert.AreEqual(0, g.Actions, "시종에 행동을 다 썼다");
            TestUtil.Ok(g, Command.Answer(true));
            CollectionAssert.Contains(g.Players[0].InPlay, "smithy");
            CollectionAssert.DoesNotContain(g.Players[0].Discard, "smithy");
            Assert.AreEqual(7, g.Players[0].Hand.Count, "대장간 +3 카드");
            Assert.IsTrue(g.Events.Any(e => e.Type == GameEventType.CardPlayed && e.CardId == "smithy" && e.From == Zone.Discard));
        }

        [Test]
        public void Attendant_Decline_CardStaysInDiscard()
        {
            var g = Start("attendant", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(0, "smithy", "silver");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Answer(false));
            Assert.AreEqual("smithy", g.Players[0].Discard.Last());
            Assert.AreEqual(4, g.Players[0].Hand.Count);
        }

        [Test]
        public void Attendant_NonAction_JustDiscards()
        {
            var g = Start("attendant", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(0, "gold", "silver");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind, "행동 카드가 아니면 묻지 않는다");
            Assert.AreEqual("gold", g.Players[0].Discard.Last());
            Assert.AreEqual(2, g.Coins);
        }

        // ---------------- 공병대 ----------------

        [Test]
        public void Engineers_GoldIntoDragon()
        {
            var g = Start("engineers", "gold", "copper", "copper", "copper");
            int before = g.CastleHp(1);
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(1, g.Pending.Min, "반드시 1장 폐기");
            Assert.IsFalse(g.Submit(Command.Choose()));
            TestUtil.Ok(g, Command.Choose(Idx(g, "gold")));
            Assert.AreEqual(DecisionKind.ChooseSupply, g.Pending.Kind);
            CollectionAssert.Contains(g.Pending.SupplyChoices, "dragon", "금화 6 + 2 = 8");
            TestUtil.Ok(g, Command.ChooseSupply("dragon"));
            CollectionAssert.Contains(g.Trash, "gold");
            CollectionAssert.Contains(g.Players[0].Discard, "dragon");
            Assert.AreEqual(before - 6, g.CastleHp(1), "드래곤 획득 = 공격");
        }

        [Test]
        public void Engineers_CopperGainsUpToCostTwo()
        {
            var g = Start("engineers", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose(0));
            CollectionAssert.Contains(g.Pending.SupplyChoices, "archers");
            CollectionAssert.DoesNotContain(g.Pending.SupplyChoices, "silver");
        }

        // ---------------- 환전상 ----------------

        [Test]
        public void MoneyChanger_TrashCopper_PlusThree()
        {
            var g = Start("moneychanger", "copper", "silver", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            CollectionAssert.AreEqual(new[] { Idx(g, "copper") }, g.Pending.CardChoices, "동화만 고를 수 있다");
            TestUtil.Ok(g, Command.Choose(Idx(g, "copper")));
            Assert.AreEqual(3, g.Coins);
            CollectionAssert.Contains(g.Trash, "copper");
        }

        [Test]
        public void MoneyChanger_NoTrash_NoCoins()
        {
            var g = Start("moneychanger", "copper", "silver", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose());
            Assert.AreEqual(0, g.Coins);
        }

        [Test]
        public void MoneyChanger_NoCopperInHand_NothingHappens()
        {
            var g = Start("moneychanger", "silver", "silver", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind);
            Assert.AreEqual(0, g.Coins);
        }

        // ---------------- 왕의 칙령 ----------------

        [Test]
        public void Decree_PlaysSmithyTwice()
        {
            var g = Start("decree", "smithy", "copper", "copper", "copper");
            g.DebugSetDeck(0, "silver", "silver", "silver", "gold", "gold", "gold", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.ChooseCards, g.Pending.Kind);
            CollectionAssert.AreEqual(new[] { Idx(g, "smithy") }, g.Pending.CardChoices, "행동 카드만");
            TestUtil.Ok(g, Command.Choose(Idx(g, "smithy")));
            Assert.AreEqual(9, g.Players[0].Hand.Count, "동화 3 + 대장간 두 번 (6장)");
            CollectionAssert.AreEquivalent(new[] { "decree", "smithy" }, g.Players[0].InPlay);
            Assert.AreEqual(1, g.Events.Count(e => e.Type == GameEventType.CardPlayed && e.CardId == "smithy" && e.From == Zone.InPlay), "두 번째 사용 이벤트");
        }

        [Test]
        public void Decree_WithMine_EachPlayIsIndependent()
        {
            var g = Start("decree", "mine", "copper", "silver", "archers");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose(Idx(g, "mine")));
            // 첫 번째 광산: 동화 → 은화
            TestUtil.Ok(g, Command.Choose(Idx(g, "copper")));
            TestUtil.Ok(g, Command.ChooseSupply("silver"));
            // 두 번째 광산: 아무것도 폐기하지 않으면 얻지도 않는다 (첫 번째 폐기 기억이 남으면 안 됨)
            Assert.AreEqual(DecisionKind.ChooseCards, g.Pending.Kind);
            TestUtil.Ok(g, Command.Choose());
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind);
            CollectionAssert.AreEquivalent(new[] { "silver", "silver", "archers" }, g.Players[0].Hand);
        }

        [Test]
        public void Decree_WithMine_SilverThenGold()
        {
            var g = Start("decree", "mine", "copper", "archers", "archers");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose(Idx(g, "mine")));
            TestUtil.Ok(g, Command.Choose(Idx(g, "copper")));
            TestUtil.Ok(g, Command.ChooseSupply("silver"));
            TestUtil.Ok(g, Command.Choose(Idx(g, "silver")));
            CollectionAssert.Contains(g.Pending.SupplyChoices, "gold");
            TestUtil.Ok(g, Command.ChooseSupply("gold"));
            CollectionAssert.Contains(g.Players[0].Hand, "gold");
        }

        [Test]
        public void Decree_NoActionInHand_NothingHappens()
        {
            var g = Start("decree", "copper", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind);
        }

        [Test]
        public void Decree_TwiceWitch_TwoTraitors()
        {
            var g = Start("decree", "witch", "copper", "copper", "copper");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose(Idx(g, "witch")));
            Assert.AreEqual(2, g.Players[1].Count("traitor"));
        }

        // ---------------- 의용군 ----------------

        [Test]
        public void Volunteers_PowerIsOnePerTenCards()
        {
            var g = TestUtil.NewGame(kingdom: KingdomC);
            var p = g.Players[0];
            p.Discard.Add("volunteers");
            Assert.AreEqual(11, p.TotalCards);
            Assert.AreEqual(1, g.CardPower(0, "volunteers"));
            for (int i = 0; i < 9; i++) p.Discard.Add("copper");
            Assert.AreEqual(2, g.CardPower(0, "volunteers"), "20장 → 2");
            Assert.AreEqual(3 + 2, g.SiegePower(0), "궁수대 3 + 의용군 2");
        }

        [Test]
        public void Volunteers_BuyingAttacksWithCurrentPower()
        {
            var g = Start("gold", "copper", "copper", "copper", "copper");
            for (int i = 0; i < 9; i++) g.Players[0].Discard.Add("copper"); // 총 19장 → 사면 20장
            g.DebugResyncHp();
            int before = g.CastleHp(1);
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("volunteers"));
            Assert.AreEqual(before - 2, g.CastleHp(1));
            var atk = g.Events.Last(e => e.Type == GameEventType.SiegeAttack);
            Assert.AreEqual("volunteers", atk.CardId);
            Assert.AreEqual(2, atk.Amount);
        }

        [Test]
        public void Volunteers_CrossingTenCards_DamagesOpponentAgain()
        {
            var g = Start("gold", "copper", "copper", "copper", "copper");
            var p = g.Players[0];
            p.Discard.Add("volunteers");
            while (p.TotalCards < 29) p.Discard.Add("copper");
            g.DebugResyncHp();
            int before = g.CastleHp(1);
            TestUtil.Ok(g, Command.PlayAllTreasures());
            TestUtil.Ok(g, Command.Buy("silver")); // 30장이 되며 의용군 2 → 3
            Assert.AreEqual(before - 1, g.CastleHp(1));
            var dmg = g.Events.Last(e => e.Type == GameEventType.CastleDamaged);
            Assert.AreEqual(1, dmg.Player);
            Assert.AreEqual(1, dmg.Amount);
        }

        // ---------------- 약탈대 ----------------

        [Test]
        public void Raiders_GainGold_OpponentTrashesCheaperNonCopperTreasure()
        {
            var g = Start("raiders", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(1, "silver", "gold", "copper");
            TestUtil.Ok(g, Command.Play(0));
            CollectionAssert.Contains(g.Players[0].Discard, "gold");
            CollectionAssert.Contains(g.Trash, "silver", "은화와 금화 중 더 싼 은화를 폐기");
            CollectionAssert.Contains(g.Players[1].Discard, "gold");
            Assert.AreEqual(0, g.Players[1].Revealed.Count);
            Assert.AreEqual(2, g.Events.Count(e => e.Type == GameEventType.CardRevealed && e.Player == 1 && !e.Private));
        }

        [Test]
        public void Raiders_CopperIsNeverTrashed()
        {
            var g = Start("raiders", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(1, "copper", "archers", "silver");
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(0, g.Trash.Count);
            CollectionAssert.IsSubsetOf(new[] { "copper", "archers" }, g.Players[1].Discard);
            Assert.AreEqual("silver", g.Players[1].Deck.Last(), "3번째 카드는 그대로");
        }

        [Test]
        public void Raiders_BlockedByWall_StillGainsGold()
        {
            var g = Start("raiders", "copper", "copper", "copper", "copper");
            g.Players[1].Hand.Clear();
            g.Players[1].Hand.AddRange(new[] { "wall", "copper", "copper", "copper", "copper" });
            g.DebugSetDeck(1, "gold", "silver");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Answer(true));
            CollectionAssert.Contains(g.Players[0].Discard, "gold");
            Assert.AreEqual(0, g.Trash.Count);
            Assert.AreEqual(2, g.Players[1].Deck.Count);
        }

        // ---------------- 망루 ----------------

        [Test]
        public void Watchtower_TrashTraitor_ReturnsTheRest()
        {
            var g = Start("watchtower", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(0, "silver", "traitor", "gold", "copper");
            g.Players[0].Discard.Clear();
            g.DebugResyncHp();
            int myHp = g.CastleHp(0);
            TestUtil.Ok(g, Command.Play(0));
            Assert.AreEqual(1, g.Actions);
            CollectionAssert.Contains(g.Players[0].Hand, "silver", "+1 카드");
            Assert.AreEqual(Zone.Revealed, g.Pending.From);
            CollectionAssert.AreEqual(new[] { "traitor", "gold" }, g.Players[0].Revealed);
            var snap = g.TakeSnapshot();
            Assert.AreEqual(2, snap.Players[0].Revealed.Length);
            Assert.IsTrue(snap.Players[0].RevealedPrivate, "망루로 본 카드는 본인만 본다");

            TestUtil.Ok(g, Command.Choose(0)); // 배신자 폐기
            Assert.AreEqual(myHp + 1, g.CastleHp(0), "배신자 폐기 → 내 성 회복");
            TestUtil.Ok(g, Command.Choose());  // 버리지 않음 → 남은 1장은 자동으로 덱 위
            Assert.AreEqual("gold", g.Players[0].Deck.Last());
            Assert.AreEqual(0, g.Players[0].Revealed.Count);
            Assert.AreEqual(DecisionKind.BuyPhase, g.Pending.Kind);
        }

        [Test]
        public void Watchtower_ChooseOrder()
        {
            var g = Start("watchtower", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(0, "copper", "silver", "gold", "copper");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose());   // 폐기 없음
            TestUtil.Ok(g, Command.Choose());   // 버리기 없음
            Assert.AreEqual(1, g.Pending.Min);
            int goldIdx = g.Players[0].Revealed.IndexOf("gold");
            TestUtil.Ok(g, Command.Choose(goldIdx));
            var deck = g.Players[0].Deck;
            Assert.AreEqual("gold", deck[deck.Count - 1], "고른 카드가 맨 위");
            Assert.AreEqual("silver", deck[deck.Count - 2]);
        }

        [Test]
        public void Watchtower_DiscardBoth()
        {
            var g = Start("watchtower", "copper", "copper", "copper", "copper");
            g.DebugSetDeck(0, "copper", "archers", "catapult");
            TestUtil.Ok(g, Command.Play(0));
            TestUtil.Ok(g, Command.Choose());
            TestUtil.Ok(g, Command.Choose(0, 1));
            CollectionAssert.IsSubsetOf(new[] { "archers", "catapult" }, g.Players[0].Discard);
            Assert.AreEqual(0, g.Players[0].Revealed.Count);
        }

        // ---------------- AI / 저장 ----------------

        [Test]
        public void NewCards_AiGamesFinish_AndReplayMatches()
        {
            var levels = new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard };
            int seed = 300;
            foreach (var a in levels)
                foreach (var b in levels)
                    for (int k = 0; k < 3; k++)
                    {
                        var opt = new GameOptions { Seed = seed++, Kingdom = KingdomC, CaptureSnapshots = false };
                        var g = AutoPlay.Run(TestUtil.Db, opt, new Bot(a), new Bot(b));
                        Assert.IsTrue(g.IsOver);
                        var replay = SaveGame.FromJson(SaveGame.From(g).ToJson()).Restore(TestUtil.Db, false);
                        Assert.AreEqual(g.CastleHp(0), replay.CastleHp(0));
                        Assert.AreEqual(g.CastleHp(1), replay.CastleHp(1));
                        Assert.AreEqual(g.Winner, replay.Winner);
                    }
        }
    }
}
