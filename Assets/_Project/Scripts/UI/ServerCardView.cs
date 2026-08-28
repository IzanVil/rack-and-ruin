using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>Tarjeta de un servidor dentro del rack. Se construye una vez y se
    /// refresca cada frame leyendo el modelo; nunca instancia nada en Refresh.</summary>
    public sealed class ServerCardView
    {
        public const float Width = 224f;
        public const float Height = 162f;

        public readonly RectTransform Root;
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

        public ServerCardView(Transform parent, ServerUnit unit, System.Action<ServerUnit> onClick)
        {
            Unit = unit;

            Root = Ui.NewRect("Card_" + unit.Name, parent);

            _border = Ui.NewPanel("Border", Root, UiTheme.Line, UiTheme.RadiusCard + 2);
            Ui.Stretch(_border.rectTransform, -2f, -2f, -2f, -2f);

            _background = Ui.NewPanel("Bg", Root, UiTheme.PanelRaised, UiTheme.RadiusCard);
            _background.raycastTarget = true;
            var button = _background.gameObject.AddComponent<Button>();
            button.targetGraphic = _background;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick(Unit));

            _name = Ui.NewText("Name", Root, unit.Name, 16, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(_name.rectTransform, 12f, 9f, 110f, 20f);

            _tier = Ui.NewText("Tier", Root, string.Empty, 12, UiTheme.Accent,
                TextAnchor.MiddleRight, FontStyle.Bold);
            Ui.Place(_tier.rectTransform, Width - 78f, 9f, 66f, 20f);

            var pill = Ui.NewPanel("StatePill", Root, UiTheme.Ok, UiTheme.RadiusPill);
            Ui.Place(pill.rectTransform, 12f, 32f, 108f, 17f);
            _statePillBg = pill;
            _statePill = Ui.NewText("Text", pill.rectTransform, "EN LÍNEA", 11, UiTheme.Background,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(_statePill.rectTransform, 4f, 4f, 0f, 0f);

            _loadValue = BuildRow(out _loadBar, "Load", "CARGA", 55f);
            _tempValue = BuildRow(out _tempBar, "Temp", "TEMP", 81f);
            _healthValue = BuildRow(out _healthBar, "Health", "SALUD", 107f);

            _badges = Ui.NewText("Badges", Root, string.Empty, 11, UiTheme.TextMuted,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(_badges.rectTransform, 12f, 131f, Width - 24f, 16f);

            _taskBar = Ui.NewBar("TaskBar", Root, UiTheme.Track, 2);
            Ui.Place(_taskBar.Rect, 12f, 150f, Width - 24f, 4f);
        }

        Text BuildRow(out Bar bar, string id, string caption, float y)
        {
            var label = Ui.NewText(id + "Caption", Root, caption, 10, UiTheme.TextDim,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(label.rectTransform, 12f, y, 70f, 12f);

            var value = Ui.NewText(id + "Value", Root, string.Empty, 12, UiTheme.TextPrimary,
                TextAnchor.MiddleRight, FontStyle.Bold);
            Ui.Place(value.rectTransform, Width - 100f, y - 1f, 88f, 14f);

            bar = Ui.NewBar(id + "Bar", Root, UiTheme.Track, 3);
            Ui.Place(bar.Rect, 12f, y + 14f, Width - 24f, 6f);
            return value;
        }

        public void Refresh(GameSession session)
        {
            var cfg = session.Config;
            var unit = Unit;

            _tier.text = "NIVEL " + unit.Tier;

            // --- estado ---
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

            // --- barras ---
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

            // --- avisos ---
            _badges.text = BuildBadges(unit, cfg);

            // --- tarea en curso ---
            if (unit.IsBusy)
            {
                _taskBar.Rect.gameObject.SetActive(true);
                _taskBar.Set(unit.TaskProgress01, UiTheme.Accent);
            }
            else
            {
                _taskBar.Rect.gameObject.SetActive(false);
            }

            // --- selección y alerta ---
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
