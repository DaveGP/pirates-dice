using NUnit.Framework;
using PiratesDice.Core;

namespace PiratesDice.Tests
{
    public class BidRulesTests
    {
        static readonly BidContext Wild = new BidContext(onesWild: true, totalDice: 20);
        static readonly BidContext NotWild = new BidContext(onesWild: false, totalDice: 20);

        [Test]
        public void OpeningOnOnesIsNotAllowedWhenOnesAreWild()
        {
            Assert.That(BidRules.IsValid(null, new Bid(2, 1), Wild), Is.False);
            Assert.That(BidRules.IsValid(null, new Bid(2, 1), NotWild), Is.True);
        }

        [TestCase(3, 4, 4, 4, true, TestName = "Higher quantity, same face")]
        [TestCase(3, 4, 3, 5, true, TestName = "Same quantity, higher face")]
        [TestCase(3, 4, 3, 3, false, TestName = "Same quantity, lower face")]
        [TestCase(3, 4, 3, 4, false, TestName = "Same bid")]
        [TestCase(3, 4, 2, 6, false, TestName = "Lower quantity")]
        public void NormalRaises(int q, int f, int nq, int nf, bool expected)
        {
            Assert.That(BidRules.IsValid(new Bid(q, f), new Bid(nq, nf), Wild), Is.EqualTo(expected));
        }

        [TestCase(5, 3, 3, true, TestName = "Five 3s to three 1s (half, rounded up)")]
        [TestCase(5, 3, 2, false, TestName = "Five 3s to two 1s is too few")]
        [TestCase(4, 3, 2, true, TestName = "Four 3s to two 1s")]
        public void SwitchingToOnesHalvesTheQuantity(int q, int f, int ones, bool expected)
        {
            Assert.That(BidRules.IsValid(new Bid(q, f), new Bid(ones, 1), Wild), Is.EqualTo(expected));
        }

        [TestCase(3, 7, true, TestName = "Three 1s to seven of a face (double plus one)")]
        [TestCase(3, 6, false, TestName = "Three 1s to six of a face is too few")]
        public void SwitchingOffOnesDoublesPlusOne(int ones, int q, bool expected)
        {
            Assert.That(BidRules.IsValid(new Bid(ones, 1), new Bid(q, 4), Wild), Is.EqualTo(expected));
        }

        [Test]
        public void OnesToOnesMustRaiseQuantity()
        {
            Assert.That(BidRules.IsValid(new Bid(3, 1), new Bid(4, 1), Wild), Is.True);
            Assert.That(BidRules.IsValid(new Bid(3, 1), new Bid(3, 1), Wild), Is.False);
        }

        [Test]
        public void WithoutWildOnesTheOneFaceIsJustTheLowest()
        {
            Assert.That(BidRules.IsValid(new Bid(3, 1), new Bid(3, 2), NotWild), Is.True);
            Assert.That(BidRules.IsValid(new Bid(3, 2), new Bid(3, 1), NotWild), Is.False);
            Assert.That(BidRules.IsValid(new Bid(3, 6), new Bid(4, 1), NotWild), Is.True);
        }

        [Test]
        public void QuantityCannotExceedDiceOnTheTable()
        {
            var ctx = new BidContext(true, totalDice: 6);
            Assert.That(BidRules.IsValid(null, new Bid(6, 2), ctx), Is.True);
            Assert.That(BidRules.IsValid(null, new Bid(7, 2), ctx), Is.False);
            Assert.That(BidRules.MinimumQuantity(new Bid(6, 6), 3, ctx), Is.EqualTo(0));
        }

        [Test]
        public void PalificoLocksTheFaceUnlessTheBidderHasOneDie()
        {
            var locked = new BidContext(false, 20, palificoRound: true, bidderMayChangeFace: false);
            var free = new BidContext(false, 20, palificoRound: true, bidderMayChangeFace: true);
            Assert.That(BidRules.IsValid(new Bid(2, 4), new Bid(3, 4), locked), Is.True);
            Assert.That(BidRules.IsValid(new Bid(2, 4), new Bid(2, 5), locked), Is.False);
            Assert.That(BidRules.IsValid(new Bid(2, 4), new Bid(3, 5), locked), Is.False);
            Assert.That(BidRules.IsValid(new Bid(2, 4), new Bid(2, 5), free), Is.True);
        }

        [Test]
        public void MinimumQuantityFindsTheSmallestLegalBid()
        {
            Assert.That(BidRules.MinimumQuantity(new Bid(4, 5), 6, Wild), Is.EqualTo(4));
            Assert.That(BidRules.MinimumQuantity(new Bid(4, 5), 3, Wild), Is.EqualTo(5));
            Assert.That(BidRules.MinimumQuantity(new Bid(4, 5), 1, Wild), Is.EqualTo(2));
            Assert.That(BidRules.MinimumQuantity(null, 2, Wild), Is.EqualTo(1));
            Assert.That(BidRules.MinimumQuantity(null, 1, Wild), Is.EqualTo(0));
        }

        [Test]
        public void WildOnesCountTowardOtherFacesButNotTowardOnes()
        {
            int[] dice = { 1, 1, 3, 3, 5 };
            Assert.That(BidRules.CountMatches(dice, 3, onesWild: true), Is.EqualTo(4));
            Assert.That(BidRules.CountMatches(dice, 3, onesWild: false), Is.EqualTo(2));
            Assert.That(BidRules.CountMatches(dice, 1, onesWild: true), Is.EqualTo(2));
        }
    }
}
