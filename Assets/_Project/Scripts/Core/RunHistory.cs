using System;
using System.Collections.Generic;
using UnityEngine;

namespace ServerGame.Core
{
    [Serializable]
    public sealed class RunResult
    {
        public int seed;
        public int mode;
        public int days;
        public int score;
        public float served;
        public float money;
    }

    [Serializable]
    public sealed class RunHistoryData
    {
        public int version;
        public int legacyBest;
        public List<RunResult> runs = new List<RunResult>();
    }

    public static class RunHistory
    {
        public const int Version = 1;
        public const string Key = "ServerGame.History";

        public const string LegacyBestKey = "ServerGame.BestScore";

        public const int MaxRuns = 60;

        public static RunHistoryData Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<RunHistoryData>(json);
                    if (data != null && data.version == Version)
                    {
                        if (data.runs == null) data.runs = new List<RunResult>();
                        return data;
                    }
                }
                catch (Exception)
                {
                }
            }

            return new RunHistoryData
            {
                version = Version,
                legacyBest = PlayerPrefs.GetInt(LegacyBestKey, 0)
            };
        }

        public static void Save(RunHistoryData data)
        {
            if (data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Add(RunHistoryData data, RunResult result)
        {
            if (data == null || result == null) return;
            data.runs.Add(result);
            if (data.runs.Count > MaxRuns) data.runs.RemoveRange(0, data.runs.Count - MaxRuns);
        }

        public static int BestScore(RunHistoryData data, RunMode mode)
        {
            int best = data != null ? data.legacyBest : 0;
            if (data == null) return best;

            for (int i = 0; i < data.runs.Count; i++)
                if ((RunMode)data.runs[i].mode == mode && data.runs[i].score > best)
                    best = data.runs[i].score;

            return best;
        }

        public static int DailyRuns(RunHistoryData data)
        {
            if (data == null) return 0;
            int n = 0;
            for (int i = 0; i < data.runs.Count; i++)
                if ((RunMode)data.runs[i].mode == RunMode.Daily) n++;
            return n;
        }

        public static int DailyStreak(RunHistoryData data, DateTime today)
        {
            if (data == null || data.runs.Count == 0) return 0;

            var jugados = new HashSet<int>();
            for (int i = 0; i < data.runs.Count; i++)
                if ((RunMode)data.runs[i].mode == RunMode.Daily) jugados.Add(data.runs[i].seed);

            if (jugados.Count == 0) return 0;

            var dia = today.Date;
            if (!jugados.Contains(RunSeed.ForDate(dia)))
            {
                dia = dia.AddDays(-1);
                if (!jugados.Contains(RunSeed.ForDate(dia))) return 0;
            }

            int racha = 0;
            while (jugados.Contains(RunSeed.ForDate(dia)))
            {
                racha++;
                dia = dia.AddDays(-1);
            }
            return racha;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
