using System;
using System.Globalization;
using UnityEngine;

namespace ServerGame.Core
{
    public enum RunMode
    {
        Daily,
        Shared,
        Free
    }

    public static class RunSeed
    {
        public const string PublicUrl = "https://izanvil.github.io/rack-and-ruin/";

        const string QueryKey = "seed=";

        public static int Today() => ForDate(DateTime.UtcNow);

        public static int ForDate(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        public static int Random() =>
            Mathf.Abs(Environment.TickCount % 90000000) + 10000000;

        public static int FromUrl() => SeedIn(Application.absoluteURL);

        public static int SeedIn(string url)
        {
            if (string.IsNullOrEmpty(url)) return 0;

            int query = url.IndexOf('?');
            if (query < 0) return 0;

            string[] parts = url.Substring(query + 1).Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (!part.StartsWith(QueryKey, StringComparison.OrdinalIgnoreCase)) continue;

                string value = part.Substring(QueryKey.Length);
                int hash = value.IndexOf('#');
                if (hash >= 0) value = value.Substring(0, hash);

                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed)
                    && seed != 0)
                {
                    return seed;
                }
            }

            return 0;
        }

        public static string Label(int seed, RunMode mode)
        {
            switch (mode)
            {
                case RunMode.Daily: return "Turno del día " + DateLabel(seed);
                case RunMode.Shared: return "Turno compartido #" + seed;
                default: return "Partida libre · semilla " + seed;
            }
        }

        public static string DateLabel(int seed)
        {
            int year = seed / 10000;
            int month = seed / 100 % 100;
            int day = seed % 100;

            if (year < 1 || year > 9999 || month < 1 || month > 12 ||
                day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                return seed.ToString(CultureInfo.InvariantCulture);
            }

            return day.ToString("00", CultureInfo.InvariantCulture) + "/" +
                   month.ToString("00", CultureInfo.InvariantCulture) + "/" +
                   year.ToString(CultureInfo.InvariantCulture);
        }

        public static string ShareUrl(int seed) => ShareUrlFrom(Application.absoluteURL, seed);

        public static string ShareUrlFrom(string current, int seed)
        {
            string url = string.IsNullOrEmpty(current) ? PublicUrl : current;

            int cut = url.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) url = url.Substring(0, cut);

            return url + "?seed=" + seed.ToString(CultureInfo.InvariantCulture);
        }
    }
}
