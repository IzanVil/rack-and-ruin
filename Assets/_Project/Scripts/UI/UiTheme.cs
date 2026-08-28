using UnityEngine;

namespace ServerGame.UI
{
    /// <summary>Paleta y tipografía. Un único sitio donde tocar el aspecto del juego.</summary>
    public static class UiTheme
    {
        public static readonly Color Background   = Hex("0B0E14");
        public static readonly Color PanelDeep    = Hex("10141C");
        public static readonly Color Panel        = Hex("161C26");
        public static readonly Color PanelRaised  = Hex("1D2531");
        public static readonly Color PanelHover   = Hex("263041");
        public static readonly Color Line         = Hex("2A3341");

        public static readonly Color TextPrimary  = Hex("E6EDF3");
        public static readonly Color TextMuted    = Hex("8B98A8");
        public static readonly Color TextDim      = Hex("5C6878");

        public static readonly Color Accent       = Hex("38BDF8");
        public static readonly Color AccentDeep   = Hex("0E5A78");
        public static readonly Color Ok           = Hex("34D399");
        public static readonly Color Warn         = Hex("FBBF24");
        public static readonly Color Danger       = Hex("F87171");
        public static readonly Color Critical     = Hex("EF4444");
        public static readonly Color Money        = Hex("FDE68A");

        public static readonly Color Track        = Hex("222A36");
        public static readonly Color Overlay      = new Color(0.02f, 0.03f, 0.05f, 0.86f);

        public const int RadiusCard = 10;
        public const int RadiusPanel = 12;
        public const int RadiusSmall = 6;
        public const int RadiusPill = 9;

        static Font _font;

        // fuente integrada de Unity, sin assets externos
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return _font;
            }
        }

        /// <summary>Color del indicador de carga: verde -> ámbar -> rojo.</summary>
        public static Color LoadColor(float ratio01)
        {
            if (ratio01 < 0.65f) return Ok;
            if (ratio01 < 0.88f) return Warn;
            return Danger;
        }

        /// <summary>Color del indicador de temperatura según los umbrales de la config.</summary>
        public static Color TempColor(float celsius, float throttleStart, float critical)
        {
            if (celsius < throttleStart - 14f) return Accent;
            if (celsius < throttleStart) return Warn;
            return celsius < critical ? Danger : Critical;
        }

        public static Color HealthColor(float health100)
        {
            if (health100 > 60f) return Ok;
            if (health100 > 30f) return Warn;
            return Danger;
        }

        public static Color ReputationColor(float rep100)
        {
            if (rep100 > 60f) return Ok;
            if (rep100 > 30f) return Warn;
            return Critical;
        }

        public static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var c)) return c;
            return Color.magenta;
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
