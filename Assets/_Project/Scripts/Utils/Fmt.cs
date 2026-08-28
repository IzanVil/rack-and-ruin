using System.Globalization;
using UnityEngine;

namespace ServerGame.Utils
{
    /// <summary>Formateo de texto para la interfaz. Todo en un solo sitio para que
    /// los números se lean igual en toda la pantalla.</summary>
    public static class Fmt
    {
        static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        /// <summary>1234.5 -> "1.234 €" (separador de miles con punto, estilo ES).</summary>
        public static string Money(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            string sign = rounded < 0 ? "-" : string.Empty;
            return sign + Thousands(Mathf.Abs(rounded)) + " €";
        }

        public static string MoneySigned(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            string sign = rounded < 0 ? "-" : "+";
            return sign + Thousands(Mathf.Abs(rounded)) + " €";
        }

        /// <summary>Peticiones por segundo, compactado: "1,2k req/s".</summary>
        public static string Rate(float reqPerSecond) => Compact(reqPerSecond) + " req/s";

        /// <summary>Número compacto: 950 -> "950", 1234 -> "1,2k", 2450000 -> "2,4M".</summary>
        public static string Compact(float value)
        {
            float abs = Mathf.Abs(value);
            if (abs >= 1_000_000f) return (value / 1_000_000f).ToString("0.#", Ci).Replace('.', ',') + "M";
            if (abs >= 1_000f) return (value / 1_000f).ToString("0.#", Ci).Replace('.', ',') + "k";
            return Mathf.RoundToInt(value).ToString(Ci);
        }

        /// <summary>Entero con separador de miles: 12345 -> "12.345".</summary>
        public static string Thousands(int value)
        {
            return value.ToString("#,0", Ci).Replace(',', '.');
        }

        /// <summary>0.873 -> "87,3 %".</summary>
        public static string Percent01(float value01, int decimals = 1)
        {
            string fmt = decimals <= 0 ? "0" : "0." + new string('#', decimals);
            return (value01 * 100f).ToString(fmt, Ci).Replace('.', ',') + " %";
        }

        /// <summary>0..100 -> "87 %".</summary>
        public static string Percent100(float value100) => Mathf.RoundToInt(value100).ToString(Ci) + " %";

        public static string Temp(float celsius) => Mathf.RoundToInt(celsius).ToString(Ci) + " °C";

        /// <summary>Segundos -> "01:23".</summary>
        public static string Clock(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = Mathf.FloorToInt(seconds);
            return (total / 60).ToString("00", Ci) + ":" + (total % 60).ToString("00", Ci);
        }

        public static string Seconds(float seconds) => Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString(Ci) + " s";
    }
}
