using System.Collections.Generic;
using ServerGame.Events;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    // Número fijo de líneas reutilizadas: no crea ni destruye objetos en partida.
    public sealed class LogView
    {
        readonly EventBus _bus;
        readonly int _visibleLines;
        readonly Text[] _lines;
        readonly List<LogEntry> _entries;

        public IReadOnlyList<LogEntry> Entries => _entries;

        public LogView(Transform parent, EventBus bus, UiLayout layout,
            IReadOnlyList<LogEntry> previous = null)
        {
            bool compact = layout.Compact;
            _bus = bus;
            _visibleLines = layout.LogLines;
            _lines = new Text[_visibleLines];
            _entries = new List<LogEntry>(_visibleLines * 2);

            float pad = compact ? 12f : 18f;
            float firstLine = compact ? 8f : 30f;
            float lineStep = compact ? 15f : 16f;

            var panel = Ui.NewPanel("LogPanel", parent, UiTheme.Panel, UiTheme.RadiusPanel);
            Ui.Stretch(panel.rectTransform);

            if (!compact)
            {
                var title = Ui.NewText("Title", panel.rectTransform, "CONSOLA DE EVENTOS", 11,
                    UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
                Ui.Place(title.rectTransform, pad, 10f, 300f, 14f);

                var hint = Ui.NewText("Hint", panel.rectTransform,
                    "espacio pausa · 1 2 3 velocidad · tab siguiente incidencia · m mejoras", 11,
                    UiTheme.TextDim, TextAnchor.MiddleRight);
                var hintRect = hint.rectTransform;
                hintRect.anchorMin = new Vector2(1f, 1f);
                hintRect.anchorMax = new Vector2(1f, 1f);
                hintRect.pivot = new Vector2(1f, 1f);
                hintRect.anchoredPosition = new Vector2(-pad, -10f);
                hintRect.sizeDelta = new Vector2(560f, 14f);
            }

            for (int i = 0; i < _visibleLines; i++)
            {
                var line = Ui.NewText("Line" + i, panel.rectTransform, string.Empty,
                    compact ? 11 : 12, UiTheme.TextMuted, TextAnchor.MiddleLeft);

                // Fila anclada al borde superior y estirada a lo ancho del panel.
                float top = firstLine + i * lineStep;
                var rect = line.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.offsetMin = new Vector2(pad, -top - lineStep);
                rect.offsetMax = new Vector2(-pad, -top);

                _lines[i] = line;
            }

            if (previous != null)
            {
                for (int i = 0; i < previous.Count; i++) _entries.Add(previous[i]);
                Trim();
                Render();
            }

            bus.Logged += OnLogged;
        }

        public void Dispose()
        {
            _bus.Logged -= OnLogged;
        }

        void OnLogged(LogEntry entry)
        {
            _entries.Add(entry);
            Trim();
            Render();
        }

        void Trim()
        {
            if (_entries.Count > _visibleLines)
                _entries.RemoveRange(0, _entries.Count - _visibleLines);
        }

        void Render()
        {
            for (int i = 0; i < _visibleLines; i++)
            {
                // la línea 0 es la más reciente
                int entryIndex = _entries.Count - 1 - i;
                if (entryIndex < 0)
                {
                    _lines[i].text = string.Empty;
                    continue;
                }

                var entry = _entries[entryIndex];
                float fade = 1f - i / (float)_visibleLines * 0.55f;
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
