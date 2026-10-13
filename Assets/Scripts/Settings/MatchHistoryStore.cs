using System;
using UnityEngine;
using WarOfNyx.Core.Match;

namespace WarOfNyx.Settings
{
    /// <summary>
    /// Local match history on this device (spec 23 statistics, local display only). Entries hold
    /// server-reported values for online matches and are tagged by mode, so offline (Ghost Protocol)
    /// results never mix into online totals. Nothing here is uploaded or trusted for rankings.
    /// </summary>
    public static class MatchHistoryStore
    {
        private const string PrefsKey = "won.history.v1";
        private static MatchHistory _cache;

        public static MatchHistory Load()
        {
            if (_cache != null)
                return _cache;
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                _cache = string.IsNullOrEmpty(json) ? new MatchHistory() : JsonUtility.FromJson<MatchHistory>(json) ?? new MatchHistory();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WarOfNyx] Match history was unreadable and has been reset: {e.Message}");
                _cache = new MatchHistory();
            }
            return _cache;
        }

        public static void Add(MatchHistoryEntry entry)
        {
            var history = Load();
            history.Add(entry);
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(history));
            PlayerPrefs.Save();
        }

        public static string Summary(string mode)
        {
            var t = Load().Totals(mode);
            if (t.Matches == 0)
                return "No matches yet.";
            return $"{t.Matches} MATCHES  ·  {t.Wins} WINS ({t.WinRate * 100f:0}%)  ·  {t.Kills} KILLS  ·  K/D {t.KillDeathRatio:0.0}  ·  BEST {t.BestKills}  ·  AVG {MatchResultText.Clock(t.AverageSurvivalSeconds)}";
        }
    }
}
