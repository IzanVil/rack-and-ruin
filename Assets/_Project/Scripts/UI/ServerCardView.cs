using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    // Se construye una vez y se refresca leyendo el modelo; Refresh no instancia nada.
    public sealed class ServerCardView
    {
        public readonly RectTransform Root;

        readonly float _width;
        readonly bool _dense;
        public ServerUnit Unit { get; private set; }

        readonly Image _border;
        readonly Image _background;
        readonly Text _name;
        readonly Text _tier;
        readonly Image _statePillBg;
        readonly Text _statePill;

        readonly Text _loadValue;
        readonly Text _tempValue;
        readonly Text _healthValue;
        readonly Bar _loadBar;
        readonly Bar _tempBar;
        readonly Bar _healthBar;

        readonly Text _badges;
        readonly Bar _taskBar;

        public ServerCardView(Transform parent, ServerUnit unit, System.Action<ServerUnit> onClick,
            UiLayout layout)
        {
            Unit = unit;
            _width = layout.CardSize.x;

            bool compact = layout.Compact;
            _dense = layout.DenseCards;

            float nameY = _dense ? 6f : compact ? 7f : 9f;
            float pillY = _dense ? 6f : compact ? 27f : 32f;
            float row0 = _dense ? 28f : compact ? 48f : 55f;
            float rowStep = _dense ? 20f : compact ? 24f : 26f;
            float badgesY = _dense ? 88f : compact ? 118f : 131f;
            float taskY = _dense ? 100f : compact ? 136f : 150f;
            int nameSize = _dense ? 14 : compact ? 15 : 16;
            int tierSize = _dense ? 9 : compact ? 10 : 12;
            int pillSize = _dense ? 9 : compact ? 10 : 11;
            int badgeSize = _dense ? 9 : compact ? 10 : 11;
            float pad = compact ? 10f : 12f;

            Root = Ui.NewRect("Card_" + unit.Name, parent);

            _border = Ui.NewPanel("Border", Root, UiTheme.Line, UiTheme.RadiusCard + 2);
            Ui.Stretch(_border.rectTransform, -2f, -2f, -2f, -2f);

            _background = Ui.NewPanel("Bg", Root, UiTheme.PanelRaised, UiTheme.RadiusCard);
            _background.raycastTarget = true;
            var button = _background.gameObject.AddComponent<Button>();
            button.targetGraphic = _background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick(Unit));
            _background.gameObject.AddComponent<PointerCursorHint>().Button = button;

            _name = Ui.NewText("Name", Root, unit.Name, nameSize, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(_name.rectTransform, pad, nameY, 110f, 20f);

            _tier = Ui.NewText("Tier", Root, string.Empty, tierSize, UiTheme.Accent,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            if (_dense) Ui.Place(_tier.rectTransform, pad + 54f, nameY, 46f, 18f);
            else Ui.Place(_tier.rectTransform, _width - 66f - pad, nameY, 66f, 20f);
            if (!_dense) _tier.alignment = TextAnchor.MiddleRight;

            var pill = Ui.NewPanel("StatePill", Root, UiTheme.Ok, UiTheme.RadiusPill);
            Ui.Place(pill.rectTransform, _dense ? _width - 84f - pad : pad, pillY,
                _dense ? 84f : compact ? 96f : 108f, _dense ? 15f : compact ? 16f : 17f);
            _statePillBg = pill;
            _statePill = Ui.NewText("Text", pill.rectTransform, "EN LÍNEA", pillSize, UiTheme.Background,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(_statePill.rectTransform, 4f, 4f, 0f, 0f);

            _loadValue = BuildRow(out _loadBar, "Load", "CARGA", row0, pad, compact);
            _tempValue = BuildRow(out _tempBar, "Temp", "TEMP", row0 + rowStep, pad, compact);
            _healthValue = BuildRow(out _healthBar, "Health", "SALUD", row0 + rowStep * 2f, pad, compact);

            _badges = Ui.NewText("Badges", Root, string.Empty, badgeSize, UiTheme.TextMuted,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(_badges.rectTransform, pad, badgesY, _width - pad * 2f, 16f);

            _taskBar = Ui.NewBar("TaskBar", Root, UiTheme.Track, 2);
            Ui.Place(_taskBar.Rect, pad, taskY, _width - pad * 2f, 4f);
        }

        Text BuildRow(out Bar bar, string id, string caption, float y, float pad, bool compact)
        {
            if (_dense)
            {
                var denseValue = Ui.NewText(id + "Value", Root, string.Empty, 11,
                    UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
                Ui.Place(denseValue.rectTransform, pad, y, 84f, 14f);

                bar = Ui.NewBar(id + "Bar", Root, UiTheme.Track, 3);
                Ui.Place(bar.Rect, pad + 88f, y + 5f, _width - pad * 2f - 88f, 5f);
                return denseValue;
            }

            var label = Ui.NewText(id + "Caption", Root, caption, compact ? 9 : 10, UiTheme.TextDim,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(label.rectTransform, pad, y, 70f, 12f);

            var value = Ui.NewText(id + "Value", Root, string.Empty, compact ? 11 : 12,
                UiTheme.TextPrimary, TextAnchor.MiddleRight, FontStyle.Bold);
            Ui.Place(value.rectTransform, _width - 88f - pad, y - 1f, 88f, 14f);

            bar = Ui.NewBar(id + "Bar", Root, UiTheme.Track, 3);
            Ui.Place(bar.Rect, pad, y + (compact ? 12f : 14f), _width - pad * 2f, compact ? 5f : 6f);
            return value;
        }

        public void Refresh(GameSession session)
        {
            var cfg = session.Config;
            var unit = Unit;

            _tier.text = "NIVEL " + unit.Tier;

            Color stateColor;
            switch (unit.State)
            {
                case ServerState.Online: stateColor = UiTheme.Ok; break;
                case ServerState.Offline: stateColor = UiTheme.TextDim; break;
                case ServerState.Failed: stateColor = UiTheme.Critical; break;
                default: stateColor = UiTheme.Accent; break;
            }
            _statePillBg.color = stateColor;
            _statePill.text = unit.StateLabel();
            _statePill.color = unit.State == ServerState.Offline ? UiTheme.TextPrimary : UiTheme.Background;

            float loadRatio = unit.LoadRatio(cfg);
            _loadValue.text = unit.IsServing ? Fmt.Compact(unit.Load) + " / " + Fmt.Compact(unit.EffectiveCapacity(cfg)) : "—";
            _loadBar.Set(unit.IsServing ? loadRatio : 0f, UiTheme.LoadColor(loadRatio));

            float tempRatio = Mathf.InverseLerp(cfg.ambientTemperature, cfg.criticalTemp, unit.Temperature);
            _tempValue.text = Fmt.Temp(unit.Temperature);
            var tempColor = UiTheme.TempColor(unit.Temperature, cfg.throttleStartTemp, cfg.criticalTemp);
            _tempValue.color = unit.IsOverheating(cfg) ? tempColor : UiTheme.TextPrimary;
            _tempBar.Set(tempRatio, tempColor);

            _healthValue.text = Fmt.Percent100(unit.Health);
            _healthBar.Set(unit.Health / 100f, UiTheme.HealthColor(unit.Health));

            _badges.text = BuildBadges(unit, cfg);

            if (unit.IsBusy)
            {
                _taskBar.Rect.gameObject.SetActive(true);
                _taskBar.Set(unit.TaskProgress01, UiTheme.Accent);
            }
            else
            {
                _taskBar.Rect.gameObject.SetActive(false);
            }

            bool selected = session.Selected == unit;
            Color border = selected ? UiTheme.Accent : UiTheme.Line;
            if (unit.AlertFlash > 0f) border = Color.Lerp(border, UiTheme.Critical, unit.AlertFlash);
            _border.color = border;

            _background.color = selected ? UiTheme.PanelHover : UiTheme.PanelRaised;
        }

        static string BuildBadges(ServerUnit unit, GameConfig cfg)
        {
            string result = string.Empty;

            if (unit.IsBusy)
                result += Tag(Fmt.Seconds(unit.TaskRemaining), UiTheme.Accent);

            if (unit.MemoryLeak > 0.15f)
                result += Tag("MEM " + Mathf.RoundToInt(unit.MemoryLeak * 100f) + "%",
                    unit.MemoryLeak > 0.4f ? UiTheme.Danger : UiTheme.Warn);

            if (unit.Vulnerability > 35f)
                result += Tag("CVE " + Mathf.RoundToInt(unit.Vulnerability),
                    unit.Vulnerability > 60f ? UiTheme.Danger : UiTheme.Warn);

            if (unit.IsOverheating(cfg) && !unit.IsBusy)
                result += Tag("THROTTLING", UiTheme.Danger);

            if (unit.IsFailed)
                result += Tag("SUSTITUIR", UiTheme.Critical);

            if (string.IsNullOrEmpty(result)) result = Tag("sin incidencias", UiTheme.TextDim);
            return result;
        }

        static string Tag(string text, Color color) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>  ";
    }
}
