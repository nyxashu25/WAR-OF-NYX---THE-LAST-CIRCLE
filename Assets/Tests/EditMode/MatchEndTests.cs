using System.Linq;
using NUnit.Framework;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Match;

namespace WarOfNyx.Tests
{
    /// <summary>Phase 9: alive tracking, placements, last survivor, simultaneous deaths, results, history.</summary>
    public class MatchEndTests
    {
        private static MatchResolver Match(int players, float start = 100f)
        {
            var m = new MatchResolver(start);
            for (int i = 1; i <= players; i++)
                m.AddParticipant(i, "P" + i);
            return m;
        }

        [Test]
        public void PlacementsCountDownAndTheLastSurvivorWins()
        {
            var m = Match(4);
            m.RecordElimination(3, 10, 130f, 1, DamageKind.Weapon);
            m.ResolveUpTo(10, 130f);
            Assert.AreEqual(4, m.Standings.Single(r => r.PlayerId == 3).Placement, "first out = last place");
            Assert.AreEqual(3, m.AliveCount);
            Assert.IsFalse(m.Finished);

            m.RecordElimination(2, 20, 150f, 1, DamageKind.Zone);
            m.RecordElimination(4, 30, 170f, 1, DamageKind.Melee);
            m.ResolveUpTo(30, 170f);

            Assert.IsTrue(m.Finished);
            Assert.AreEqual(1, m.Winner.PlayerId);
            Assert.AreEqual(1, m.Winner.Placement);
            Assert.IsFalse(m.Winner.WonByTieBreak);
            Assert.AreEqual(70f, m.Winner.SurvivalSeconds, 1e-3f);
            CollectionAssert.AreEqual(new[] { 1, 4, 2, 3 }, m.Standings.Select(r => r.PlayerId));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, m.Standings.Select(r => r.Placement));
        }

        [Test]
        public void EliminationsAreOnlyPlacedOnceTheirTickIsComplete()
        {
            var m = Match(3);
            m.RecordElimination(2, 50, 120f, 1, DamageKind.Weapon);
            m.ResolveUpTo(49, 120f);
            Assert.AreEqual(0, m.Standings.Single(r => r.PlayerId == 2).Placement, "tick 50 isn't resolved yet");
            m.ResolveUpTo(50, 120f);
            Assert.AreEqual(3, m.Standings.Single(r => r.PlayerId == 2).Placement);
        }

        [Test]
        public void SameTickEliminationsUseTheTieBreak()
        {
            var m = Match(4);
            m.UpdateStats(2, kills: 1, damage: 80f);
            m.UpdateStats(3, kills: 1, damage: 120f);
            m.UpdateStats(4, kills: 3, damage: 10f);
            foreach (int id in new[] { 2, 3, 4 })
                m.RecordElimination(id, 77, 200f, -1, DamageKind.Zone);
            m.ResolveUpTo(77, 200f);

            Assert.IsTrue(m.Finished, "one survivor left");
            Assert.AreEqual(1, m.Winner.PlayerId);
            // Same tick: kills first (4), then damage (3 over 2).
            Assert.AreEqual(2, m.Standings.Single(r => r.PlayerId == 4).Placement);
            Assert.AreEqual(3, m.Standings.Single(r => r.PlayerId == 3).Placement);
            Assert.AreEqual(4, m.Standings.Single(r => r.PlayerId == 2).Placement);
        }

        [Test]
        public void EveryoneFallingTogetherStillProducesExactlyOneWinner()
        {
            var m = Match(3);
            m.RecordElimination(3, 5, 110f, 1, DamageKind.Weapon);
            m.ResolveUpTo(5, 110f);
            m.UpdateStats(1, 1, 50f);
            m.UpdateStats(2, 1, 50f);
            // Both remaining players die in the closed circle in the same tick, identical stats.
            m.RecordElimination(1, 900, 640f, -1, DamageKind.Zone);
            m.RecordElimination(2, 900, 640f, -1, DamageKind.Zone);
            m.ResolveUpTo(900, 640f);

            Assert.IsTrue(m.Finished);
            Assert.AreEqual(1, m.Winner.PlayerId, "deterministic: equal stats → lower player id");
            Assert.IsTrue(m.Winner.WonByTieBreak);
            Assert.AreEqual(1, m.Records.Count(r => r.Winner));
            Assert.AreEqual(2, m.Standings.Single(r => r.PlayerId == 2).Placement);
        }

        [Test]
        public void LeaversCountAsEliminated()
        {
            var m = Match(2);
            m.RecordLeft(2, 12, 140f);
            m.ResolveUpTo(12, 140f);
            Assert.IsTrue(m.Finished);
            Assert.AreEqual(1, m.Winner.PlayerId);
            Assert.IsTrue(m.Standings.Single(r => r.PlayerId == 2).Left);
        }

        [Test]
        public void NothingChangesAfterTheEnd()
        {
            var m = Match(2);
            m.RecordElimination(2, 1, 101f, 1, DamageKind.Weapon);
            m.ResolveUpTo(1, 101f);
            Assert.IsFalse(m.RecordElimination(1, 2, 102f, -1, DamageKind.Zone), "the winner can't die after the end");
            Assert.AreEqual(1, m.Winner.PlayerId);
        }

        [Test]
        public void SoloTestMatchEndsWithoutAWinnerWhenThePlayerFalls()
        {
            var m = Match(1);
            m.ResolveUpTo(1, 100f);
            Assert.IsFalse(m.Finished, "a one-player test room doesn't end instantly");
            m.RecordElimination(1, 9, 150f, -1, DamageKind.Zone);
            m.ResolveUpTo(9, 150f);
            Assert.IsTrue(m.Finished);
            Assert.IsNull(m.Winner);
        }

        [Test]
        public void KillNoticesFirstBloodThenMultiKills()
        {
            var n = new KillNotices();
            Assert.AreEqual("FIRST BLOOD", n.OnElimination(1, 2, 10f));
            Assert.IsNull(n.OnElimination(3, 4, 12f));
            Assert.AreEqual("DOUBLE ELIMINATION", n.OnElimination(1, 5, 15f));
            Assert.AreEqual("TRIPLE ELIMINATION", n.OnElimination(1, 6, 19f));
            Assert.IsNull(n.OnElimination(1, 7, 40f), "outside the 10 s window");
            Assert.IsNull(n.OnElimination(-1, 8, 41f), "environmental eliminations have no killer");
        }

        [Test]
        public void ResultTexts()
        {
            Assert.AreEqual("4:05", MatchResultText.Clock(245f));
            Assert.AreEqual("#3 of 12", MatchResultText.Placement(3, 12));
            Assert.AreEqual("LAST SURVIVOR", MatchResultText.VictorySubtitle);
        }

        [Test]
        public void HistoryKeepsOnlineAndOfflineSeparateAndBounded()
        {
            var h = new MatchHistory();
            h.Add(new MatchHistoryEntry { mode = "online", placement = 1, won = true, kills = 4, damage = 300f, survivalSeconds = 500f });
            h.Add(new MatchHistoryEntry { mode = "online", placement = 7, kills = 1, damage = 90f, survivalSeconds = 100f });
            h.Add(new MatchHistoryEntry { mode = "offline", placement = 1, won = true, kills = 20 });

            var online = h.Totals("online");
            Assert.AreEqual(2, online.Matches);
            Assert.AreEqual(1, online.Wins);
            Assert.AreEqual(5, online.Kills);
            Assert.AreEqual(4, online.BestKills);
            Assert.AreEqual(1, online.TopFive);
            Assert.AreEqual(300f, online.AverageSurvivalSeconds, 1e-3f);
            Assert.AreEqual(0.5f, online.WinRate, 1e-6f);
            Assert.AreEqual(1, h.Totals("offline").Matches, "offline results never mix into online totals");

            for (int i = 0; i < 80; i++)
                h.Add(new MatchHistoryEntry());
            Assert.AreEqual(MatchHistory.MaxEntries, h.entries.Count);
        }

        [Test]
        public void LifecycleEndsShowsResultsAndReturnsToTheLobby()
        {
            var rules = new MatchRules { minPlayersToStart = 2, maxPlayers = 2, lobbyFillSeconds = 0f, resultsSeconds = 15f };
            var lc = new MatchLifecycle(rules, privateRoom: false);
            bool reset = false;
            float endsAt = 0f;
            lc.MatchEnded += t => endsAt = t;
            lc.ResetToLobby += () => reset = true;

            lc.TryAddPlayer(1, 0f);
            lc.TryAddPlayer(2, 0f);
            lc.MarkLoaded(1, 1f);
            lc.MarkLoaded(2, 1f);
            lc.Update(10f);
            Assert.AreEqual(MatchPhase.Active, lc.Phase);

            Assert.IsTrue(lc.EndMatch(300f));
            Assert.AreEqual(MatchPhase.Ended, lc.Phase);
            Assert.AreEqual(315f, endsAt);
            Assert.IsFalse(lc.InputUnlocked, "combat and movement stop at the end");
            Assert.IsFalse(lc.EndMatch(301f), "only once");

            lc.Update(310f);
            Assert.IsFalse(reset);
            lc.Update(315f);
            Assert.IsTrue(reset);
            Assert.AreEqual(MatchPhase.WaitingForPlayers, lc.Phase);
            Assert.AreEqual(2, lc.PlayerCount, "connected players stay for the next match");
        }
    }
}
