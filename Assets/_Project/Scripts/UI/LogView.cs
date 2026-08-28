using System.Collections.Generic;
using ServerGame.Events;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>Consola de eventos. Mantiene un número fijo de líneas y las reutiliza:
    /// no crea ni destruye objetos durante la partida.</summary>
    public sealed class LogView
    {
        public const float Height = 170f;
        const int VisibleLines = 8;

        readonly Text[] _lines = new Text[VisibleLines];
        readonly List<LogEntry> _entries = new List<LogEntry>(VisibleLines * 2);

        public LogView(Transform parent, EventBus bus)
        {
            var panel = Ui.NewPanel("LogPanel", parent, UiTheme.Panel, UiTheme.RadiusPanel);
            Ui.Stretch(panel.rectTransform);

            var title = Ui.NewText("Title", panel.rectTransform, "CONSOLA DE EVENTOS", 11,
                UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(title.rectTransform, 18f, 10f, 300f, 14f);

            var hint = Ui.NewText("Hint", panel.rectTransform,
                "espacio pausa · 1 2 3 velocidad · tab siguiente incidencia · m mejoras", 11,
                UiTheme.TextDim, TextAnchor.MiddleRight);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(1f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(1f, 1f);
            hintRect.anchoredPosition = new Vector2(-18f, -10f);
            hintRect.sizeDelta = new Vector2(560f, 14f);

            for (int i = 0; i < VisibleLines; i++)
            {
                var line = Ui.NewText("Line" + i, panel.rectTransform, string.Empty, 12,
                    UiTheme.TextMuted, TextAnchor.MiddleLeft);

                // Fila anclada al borde superior y estirada a lo ancho del panel.
                float top = 30f + i * 16f;
                var rect = line.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.offsetMin = new Vector2(18f, -top - 16f);
                rect.offsetMax = new Vector2(-18f, -top);

                _lines[i] = line;
            }

            bus.Logged += OnLogged;
        }

        void OnLogged(LogEntry entry)
        {
            _entries.Add(entry);
            if (_entries.Count > VisibleLines) _entries.RemoveRange(0, _entries.Count - VisibleLines);
            Render();
        }

        void Render()
        {
            for (int i = 0; i < VisibleLines; i++)
            {
                // La línea 0 es la más reciente: se recorre la lista hacia atrás.
                int entryIndex = _entries.Count - 1 - i;
                if (entryIndex < 0)
                {
                    _lines[i].text = string.Empty;
                    continue;
                }

                var entry = _entries[entryIndex];
                float fade = 1f - i / (float)VisibleLines * 0.55f;
                var color = ColorFor(entry.Level);
                color.a = fade;

                _lines[i].color = color;
                _lines[i].text = "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.TextDim) + ">[T" +
                                 entry.Day + " " + Fmt.Clock(entry.DayTime) + "]</color>  " + entry.Message;
            }
        }

        static Color ColorFor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Success: return UiTheme.Ok;
                case LogLevel.Warning: return UiTheme.Warn;
                case LogLevel.Critical: return UiTheme.Critical;
                default: return UiTheme.TextMuted;
            }
        }
    }
}
