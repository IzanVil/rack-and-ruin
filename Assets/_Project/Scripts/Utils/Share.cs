using ServerGame.Core;
using ServerGame.Events;
using UnityEngine;

namespace ServerGame.Utils
{
    public static class Share
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int SgCopyToClipboard(string text);
#endif

        public static string ResultText(GameOverInfo info, int seed, RunMode mode)
        {
            int daysCompleted = Mathf.Max(0, info.DaysSurvived - 1);
            string turns = daysCompleted == 1 ? "1 turno" : daysCompleted + " turnos";

            string racha = mode == RunMode.Daily && info.DailyStreak > 1
                ? "Racha: " + info.DailyStreak + " días seguidos\n"
                : string.Empty;

            return "UPTIME · Turno de Noche\n" +
                   RunSeed.Label(seed, mode) + "\n" +
                   turns + " · " + Fmt.Compact(info.TotalServed) + " peticiones · " +
                   Fmt.Money(info.Money) + "\n" +
                   "Puntuación " + Fmt.Thousands(info.Score) +
                   (info.IsNewRecord ? " (récord personal)" : string.Empty) + "\n" +
                   racha +
                   RunSeed.ShareUrl(seed);
        }

        public static bool Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

#if UNITY_WEBGL && !UNITY_EDITOR
            return SgCopyToClipboard(text) != 0;
#else
            GUIUtility.systemCopyBuffer = text;
            return true;
#endif
        }
    }
}
