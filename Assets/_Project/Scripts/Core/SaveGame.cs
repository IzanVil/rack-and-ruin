using System;
using UnityEngine;

namespace ServerGame.Core
{
    [Serializable]
    public sealed class SavedServer
    {
        public int tier;
        public int state;
        public int task;
        public float health;
        public float temperature;
        public float memoryLeak;
        public float vulnerability;
        public float uptime;
        public float coolingCooldown;
        public float taskRemaining;
        public float taskTotal;
        public float load;
    }

    [Serializable]
    public sealed class SavedEffect
    {
        public int id;
        public string label;
        public float remaining;
        public float demandMultiplier;
        public float coolingMultiplier;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version;
        public int seed;
        public int mode;
        public int phase;
        public int rngState;

        public int day;
        public float dayTime;
        public float money;
        public float reputation;

        public float dayServed;
        public float dayDropped;
        public float dayRevenue;
        public float dayPenalties;
        public float daySpending;
        public float totalServed;

        public int selected;
        public int[] upgradeLevels;
        public SavedServer[] servers;

        public float incidentNextIn;
        public SavedEffect[] incidentEffects;
    }

    public static class SaveGame
    {
        public const int Version = 1;

        public const string Key = "ServerGame.Save";

        public static void Write(SaveData data)
        {
            if (data == null) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static SaveData Read()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                Clear();
                return null;
            }

            if (!IsUsable(data))
            {
                Clear();
                return null;
            }

            return data;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        static bool IsUsable(SaveData data)
        {
            if (data == null || data.version != Version) return false;
            if (data.servers == null || data.servers.Length == 0) return false;
            if (data.upgradeLevels == null) return false;
            if (data.day < 1) return false;

            var phase = (SessionPhase)data.phase;
            return phase == SessionPhase.Playing || phase == SessionPhase.DayReview;
        }
    }
}
