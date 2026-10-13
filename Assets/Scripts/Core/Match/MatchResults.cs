using System;
using System.Collections.Generic;
using System.Linq;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Core.Match
{
    /// <summary>One participant's match record (server-side; replicated pieces are shown in results).</summary>
    public sealed class ParticipantRecord
    {
        public int PlayerId;
        public string Name = string.Empty;
        public int Kills;
        public float Damage;
        public bool Alive = true;
        /// <summary>Left the match (counts as an elimination for placement).</summary>
        public bool Left;
        public int EliminatedTick = -1;
        public float EliminatedAt;
        public int KillerId = -1;
        public DamageKind Cause;
        /// <summary>1 = winner. 0 = not placed yet.</summary>
        public int Placement;
        public bool Winner;
        /// <summary>Won because every remaining player fell in the same tick and the tie-break chose them.</summary>
        public bool WonByTieBreak;
        public float SurvivalSeconds;
    }

    /// <summary>
    /// Server-authoritative end of a match (spec 21, Phase 9): alive tracking, placements, the last
    /// survivor, and a deterministic tie-break for eliminations in the same server tick.
    ///
    /// Tie policy (same tick): more kills ranks higher, then more damage dealt, then the lower
    /// player id (earlier join). If every remaining player falls in the same tick, the best-ranked
    /// of them is the winner (marked <see cref="ParticipantRecord.WonByTieBreak"/>), so a match
    /// always ends with exactly one winner.
    ///
    /// Eliminations are recorded as they happen and resolved per tick with <see cref="ResolveUpTo"/>,
    /// called once per server tick for the PREVIOUS tick, so everything that happened in a tick is
    /// known before placements are given.
    /// </summary>
    public sealed class MatchResolver
    {
        public const int MinParticipantsForVictory = 2;

        private readonly Dictionary<int, ParticipantRecord> _records = new Dictionary<int, ParticipantRecord>();
        private readonly List<ParticipantRecord> _pending = new List<ParticipantRecord>();

        public MatchResolver(float matchStart)
        {
            MatchStart = matchStart;
        }

        public float MatchStart { get; }
        public bool Finished { get; private set; }
        public float EndedAt { get; private set; }
        public ParticipantRecord Winner { get; private set; }
        public int ParticipantCount => _records.Count;
        public int AliveCount => _records.Values.Count(r => r.Alive);

        public IEnumerable<ParticipantRecord> Records => _records.Values;

        /// <summary>Everyone ordered by placement (placed first, best first), then unplaced survivors.</summary>
        public IReadOnlyList<ParticipantRecord> Standings =>
            _records.Values.OrderBy(r => r.Placement == 0 ? int.MaxValue : r.Placement).ThenBy(r => r.PlayerId).ToList();

        public bool TryGet(int playerId, out ParticipantRecord record) => _records.TryGetValue(playerId, out record);

        public void AddParticipant(int playerId, string name)
        {
            if (Finished || _records.ContainsKey(playerId))
                return;
            _records[playerId] = new ParticipantRecord { PlayerId = playerId, Name = name ?? string.Empty };
        }

        /// <summary>Latest trusted server stats (used for tie-breaks and results).</summary>
        public void UpdateStats(int playerId, int kills, float damage)
        {
            if (_records.TryGetValue(playerId, out var r))
            {
                r.Kills = Math.Max(0, kills);
                r.Damage = float.IsNaN(damage) ? 0f : Math.Max(0f, damage);
            }
        }

        public bool RecordElimination(int playerId, int tick, float time, int killerId, DamageKind cause)
        {
            if (Finished || !_records.TryGetValue(playerId, out var r) || !r.Alive)
                return false;
            r.Alive = false;
            r.EliminatedTick = tick;
            r.EliminatedAt = time;
            r.KillerId = killerId;
            r.Cause = cause;
            r.SurvivalSeconds = Math.Max(0f, time - MatchStart);
            _pending.Add(r);
            return true;
        }

        public bool RecordLeft(int playerId, int tick, float time)
        {
            if (!RecordElimination(playerId, tick, time, -1, DamageKind.Environment))
                return false;
            _records[playerId].Left = true;
            return true;
        }

        /// <summary>
        /// Gives placements to everyone eliminated up to and including <paramref name="tick"/>, tick by
        /// tick in order, and ends the match when at most one player is alive.
        /// </summary>
        /// <returns>Records that received a placement in this call.</returns>
        public IReadOnlyList<ParticipantRecord> ResolveUpTo(int tick, float now)
        {
            var placed = new List<ParticipantRecord>();
            if (Finished)
                return placed;

            foreach (var group in _pending.Where(p => p.EliminatedTick <= tick).GroupBy(p => p.EliminatedTick).OrderBy(g => g.Key).ToList())
            {
                int aliveAfter = AliveCount; // pending ones are already not alive
                // Players eliminated in later ticks are still "alive" at this tick for placement purposes.
                aliveAfter += _pending.Count(p => p.EliminatedTick > group.Key);
                var ranked = group.OrderByDescending(p => p.Kills).ThenByDescending(p => p.Damage).ThenBy(p => p.PlayerId).ToList();

                bool everyoneFell = aliveAfter == 0 && _records.Count >= MinParticipantsForVictory;
                for (int i = 0; i < ranked.Count; i++)
                {
                    ranked[i].Placement = aliveAfter + 1 + i;
                    placed.Add(ranked[i]);
                }
                foreach (var r in ranked)
                    _pending.Remove(r);

                if (everyoneFell)
                {
                    var best = ranked[0];
                    best.Winner = true;
                    best.WonByTieBreak = ranked.Count > 1;
                    Finish(best, now);
                    return placed;
                }
            }

            int alive = AliveCount + _pending.Count;
            if (_pending.Count == 0 && alive <= 1 && _records.Count >= MinParticipantsForVictory)
            {
                var survivor = _records.Values.FirstOrDefault(r => r.Alive);
                if (survivor != null)
                {
                    survivor.Placement = 1;
                    survivor.Winner = true;
                    survivor.SurvivalSeconds = Math.Max(0f, now - MatchStart);
                    placed.Add(survivor);
                    Finish(survivor, now);
                }
            }
            else if (_pending.Count == 0 && alive == 0 && _records.Count > 0)
            {
                // Solo test match: the only player fell. No winner, but the match is over.
                Finish(null, now);
            }

            return placed;
        }

        private void Finish(ParticipantRecord winner, float now)
        {
            Finished = true;
            EndedAt = now;
            Winner = winner;
            foreach (var r in _records.Values.Where(r => r.Alive))
                r.SurvivalSeconds = Math.Max(0f, now - MatchStart);
        }
    }

    /// <summary>Feed highlights (spec 19): FIRST BLOOD, DOUBLE / TRIPLE ELIMINATION. Server-side, per match.</summary>
    public sealed class KillNotices
    {
        public const float MultiKillWindowSeconds = 10f;

        private bool _firstBloodDone;
        private readonly Dictionary<int, List<float>> _recent = new Dictionary<int, List<float>>();

        /// <returns>A notice for this elimination, or null.</returns>
        public string OnElimination(int killerId, int victimId, float time)
        {
            if (killerId < 0 || killerId == victimId)
                return null;

            if (!_recent.TryGetValue(killerId, out var times))
                _recent[killerId] = times = new List<float>();
            times.RemoveAll(t => time - t > MultiKillWindowSeconds);
            times.Add(time);

            if (!_firstBloodDone)
            {
                _firstBloodDone = true;
                return "FIRST BLOOD";
            }
            switch (times.Count)
            {
                case 1: return null;
                case 2: return "DOUBLE ELIMINATION";
                case 3: return "TRIPLE ELIMINATION";
                default: return "NYX RAMPAGE";
            }
        }
    }

    /// <summary>Victory screen and results text (spec 21).</summary>
    public static class MatchResultText
    {
        public const string VictoryTitle = "WAR OF NYX";
        public const string VictorySubtitle = "LAST SURVIVOR";
        public const string VictoryLine = "VICTORY ACHIEVED";

        public static string Clock(float seconds)
        {
            int s = Math.Max(0, (int)Math.Round(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        public static string Placement(int placement, int participants) =>
            placement <= 0 ? "-" : participants > 0 ? $"#{placement} of {participants}" : $"#{placement}";
    }

    /// <summary>One finished match on this device (spec 23: statistics; server-reported values, local display only).</summary>
    [Serializable]
    public sealed class MatchHistoryEntry
    {
        /// <summary>"online" or "offline": kept separate (offline results never count as online results).</summary>
        public string mode = "online";
        public long unixTime;
        public int placement;
        public int participants;
        public int kills;
        public float damage;
        public float survivalSeconds;
        public bool won;
        public string eliminatedBy = string.Empty;
    }

    /// <summary>Totals over a list of matches (spec 23 statistics).</summary>
    public struct MatchTotals
    {
        public int Matches;
        public int Wins;
        public int Kills;
        public int Deaths;
        public int TopFive;
        public float Damage;
        public int BestKills;
        public float AverageSurvivalSeconds;

        public float WinRate => Matches > 0 ? Wins / (float)Matches : 0f;
        public float KillDeathRatio => Deaths > 0 ? Kills / (float)Deaths : Kills;
    }

    /// <summary>Bounded local match history with per-mode totals.</summary>
    [Serializable]
    public sealed class MatchHistory
    {
        public const int MaxEntries = 50;

        public List<MatchHistoryEntry> entries = new List<MatchHistoryEntry>();

        public void Add(MatchHistoryEntry entry)
        {
            if (entry == null)
                return;
            entries.Add(entry);
            while (entries.Count > MaxEntries)
                entries.RemoveAt(0);
        }

        public MatchTotals Totals(string mode)
        {
            var list = entries.Where(e => e != null && e.mode == mode).ToList();
            var t = new MatchTotals { Matches = list.Count };
            foreach (var e in list)
            {
                if (e.won) t.Wins++;
                else t.Deaths++;
                if (e.placement > 0 && e.placement <= 5) t.TopFive++;
                t.Kills += e.kills;
                t.Damage += e.damage;
                t.BestKills = Math.Max(t.BestKills, e.kills);
                t.AverageSurvivalSeconds += e.survivalSeconds;
            }
            if (list.Count > 0)
                t.AverageSurvivalSeconds /= list.Count;
            return t;
        }
    }
}
