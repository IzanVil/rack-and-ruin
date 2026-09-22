using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    // Detalle del servidor seleccionado. Las acciones salen de GameSession.GetActions.
    public sealed class InspectorView
    {
        const int ActionCount = 7;

        readonly GameSession _session;
        readonly UiLayout _layout;
        Text _title;
        Image _statePillBg;
        Text _statePill;
        StatRow[] _stats;
        Text _footer;
        readonly UiButton[] _actions = new UiButton[ActionCount];
        readonly ServerActionId[] _actionIds = new ServerActionId[ActionCount];

        sealed class StatRow
        {
            public Text Label;
            public Text Value;
        }

        public InspectorView(Transform parent, GameSession session, UiLayout layout,
            bool ownPanel = true)
        {
            _session = session;
            _layout = layout;

            bool compact = layout.Compact;
            float pad = compact ? 14f : 16f;
            float statRowHeight = compact ? 21f : 19f;

            Transform host = parent;
            if (ownPanel)
            {
                var panel = Ui.NewPanel("Inspector", parent, UiTheme.Panel, UiTheme.RadiusPanel);
                Ui.Stretch(panel.rectTransform);
                host = panel.rectTransform;
            }

            float padBottom = compact ? 12f : 14f;
            float spacing = compact ? 7f : 8f;

            if (layout.InspectorTwoColumn)
            {
                BuildTwoColumn(host, layout, pad, padBottom, statRowHeight);
                return;
            }

            var content = Ui.NewRect("Content", host);
            Ui.Stretch(content, pad, pad, pad, padBottom);
            Ui.VBox(content, spacing);

            _title = Ui.NewText("Title", content, "SRV-01", compact ? 19 : 21, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Fixed(_title, height: 26f);

            var pillHolder = Ui.NewRect("PillHolder", content);
            Ui.Fixed(pillHolder, height: 19f);
            BuildPill(pillHolder);

            Ui.Divider(content, UiTheme.Line);

            var statsBox = Ui.NewRect("Stats", content);
            Ui.VBox(statsBox, 3f);
            _stats = new StatRow[StatCaptions.Length];
            for (int i = 0; i < StatCaptions.Length; i++)
                _stats[i] = BuildStatRow(statsBox, StatCaptions[i], statRowHeight, compact);
            Ui.Fixed(statsBox, height: StatCaptions.Length * statRowHeight +
                                       (StatCaptions.Length - 1) * 3f);

            Ui.Divider(content, UiTheme.Line);

            var actionsCaption = Ui.NewText("ActionsCaption", content, "ACCIONES", 11,
                UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Fixed(actionsCaption, height: 14f);

            var actionsBox = Ui.NewRect("Actions", content);
            Ui.VBox(actionsBox, 5f);
            for (int i = 0; i < ActionCount; i++) BuildAction(actionsBox, i, layout.ActionHeight);
            Ui.Fixed(actionsBox, height: ActionCount * layout.ActionHeight + (ActionCount - 1) * 5f);

            _footer = Ui.NewText("Footer", content, string.Empty, 11, UiTheme.TextDim,
                TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
            Ui.Fixed(_footer, height: 30f);
            if (compact) _footer.gameObject.SetActive(false);
        }

        void BuildTwoColumn(Transform host, UiLayout layout, float pad, float padBottom,
            float statRowHeight)
        {
            const float HeaderHeight = 26f;
            const float ColumnGap = 16f;
            float statsWidth = 380f;

            var content = Ui.NewRect("Content", host);
            Ui.Stretch(content, pad, pad, pad, padBottom);

            _title = Ui.NewText("Title", content, "SRV-01", 19, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(_title.rectTransform, 0f, 0f, 200f, HeaderHeight);

            var pillHolder = Ui.NewRect("PillHolder", content);
            Ui.Place(pillHolder, 210f, 4f, 200f, 19f);
            BuildPill(pillHolder);

            var body = Ui.NewRect("Body", content);
            Ui.Stretch(body, 0f, 0f, HeaderHeight + 8f, 0f);

            var statsBox = Ui.NewRect("Stats", body);
            Ui.Left(statsBox, statsWidth);
            Ui.VBox(statsBox, 3f);
            _stats = new StatRow[StatCaptions.Length];
            for (int i = 0; i < StatCaptions.Length; i++)
                _stats[i] = BuildStatRow(statsBox, StatCaptions[i], statRowHeight, true);

            var actionsBox = Ui.NewRect("Actions", body);
            Ui.Stretch(actionsBox, statsWidth + ColumnGap, 0f, 0f, 0f);
            var grid = actionsBox.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(202f, layout.ActionHeight);
            grid.spacing = new Vector2(6f, 5f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            for (int i = 0; i < ActionCount; i++) BuildAction(actionsBox, i, layout.ActionHeight);

            _footer = Ui.NewText("Footer", content, string.Empty, 11, UiTheme.TextDim,
                TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
            Ui.Place(_footer.rectTransform, 0f, 0f, 10f, 10f);
            _footer.gameObject.SetActive(false);
        }

        void BuildPill(RectTransform holder)
        {
            _statePillBg = Ui.NewPanel("Pill", holder, UiTheme.Ok, UiTheme.RadiusPill);
            var pillRect = _statePillBg.rectTransform;
            pillRect.anchorMin = new Vector2(0f, 0f);
            pillRect.anchorMax = new Vector2(0f, 1f);
            pillRect.pivot = new Vector2(0f, 0.5f);
            pillRect.offsetMin = new Vector2(0f, 0f);
            pillRect.offsetMax = new Vector2(130f, 0f);
            _statePill = Ui.NewText("Text", pillRect, "EN LÍNEA", 11, UiTheme.Background,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(_statePill.rectTransform, 6f, 6f, 0f, 0f);
        }

        void BuildAction(Transform parent, int index, float height)
        {
            var button = Ui.NewButton("Action" + index, parent, "—", UiTheme.PanelRaised,
                UiTheme.TextPrimary, 14, UiTheme.RadiusSmall, withSubLabel: true);
            Ui.Fixed(button.Rect, height: height);
            button.OnClick(() => _session.Execute(_actionIds[index], _session.Selected));
            _actions[index] = button;
        }

        static readonly string[] StatCaptions =
        {
            "Capacidad efectiva", "Carga actual", "Temperatura", "Salud del hardware",
            "Memoria filtrada", "Deuda de parches", "Tiempo sin reiniciar"
        };

        StatRow BuildStatRow(Transform parent, string caption, float height, bool compact)
        {
            var row = Ui.NewRect("Row_" + caption, parent);
            Ui.Fixed(row, height: height);

            var label = Ui.NewText("Label", row, caption, compact ? 13 : 12, UiTheme.TextMuted);
            Ui.Stretch(label.rectTransform, 0f, compact ? 130f : 110f, 0f, 0f);

            var value = Ui.NewText("Value", row, "—", compact ? 14 : 13, UiTheme.TextPrimary,
                TextAnchor.MiddleRight, FontStyle.Bold);
            Ui.Stretch(value.rectTransform, 0f, 0f, 0f, 0f);

            return new StatRow { Label = label, Value = value };
        }

        public void Refresh()
        {
            var unit = _session.Selected;
            if (unit == null) return;

            var cfg = _session.Config;

            _title.text = unit.Name + "   <color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Accent) +
                          "><size=13>NIVEL " + unit.Tier + "</size></color>";

            Color stateColor;
            switch (unit.State)
            {
                case ServerState.Online: stateColor = UiTheme.Ok; break;
                case ServerState.Offline: stateColor = UiTheme.TextDim; break;
                case ServerState.Failed: stateColor = UiTheme.Critical; break;
                default: stateColor = UiTheme.Accent; break;
            }
            _statePillBg.color = stateColor;
            _statePill.color = unit.State == ServerState.Offline ? UiTheme.TextPrimary : UiTheme.Background;
            _statePill.text = unit.IsBusy
                ? unit.StateLabel() + " · " + Fmt.Seconds(unit.TaskRemaining)
                : unit.StateLabel();

            float capacity = unit.EffectiveCapacity(cfg);
            float nominal = unit.NominalCapacity(cfg);
            float throttle = unit.ThermalThrottle(cfg);

            SetStat(0, Fmt.Rate(capacity),
                throttle < 0.995f ? UiTheme.Danger : UiTheme.TextPrimary);
            SetStat(1, unit.IsServing
                ? Fmt.Rate(unit.Load) + "  (" + Fmt.Percent01(nominal <= 0.01f ? 0f : unit.Load / nominal, 0) + ")"
                : "sin tráfico", UiTheme.LoadColor(unit.LoadRatio(cfg)));
            SetStat(2, Fmt.Temp(unit.Temperature) +
                (throttle < 0.995f ? "  (−" + Mathf.RoundToInt((1f - throttle) * 100f) + " % rend.)" : ""),
                UiTheme.TempColor(unit.Temperature, cfg.throttleStartTemp, cfg.criticalTemp));
            SetStat(3, Fmt.Percent100(unit.Health), UiTheme.HealthColor(unit.Health));
            SetStat(4, Fmt.Percent01(unit.MemoryLeak, 0),
                unit.MemoryLeak > 0.35f ? UiTheme.Danger : unit.MemoryLeak > 0.15f ? UiTheme.Warn : UiTheme.TextPrimary);
            SetStat(5, Mathf.RoundToInt(unit.Vulnerability) + " / 100",
                unit.Vulnerability > 60f ? UiTheme.Danger : unit.Vulnerability > 35f ? UiTheme.Warn : UiTheme.TextPrimary);
            SetStat(6, Fmt.Clock(unit.Uptime), UiTheme.TextMuted);

            var actions = _session.GetActions(unit);
            for (int i = 0; i < _actions.Length; i++)
            {
                var button = _actions[i];
                if (i >= actions.Count)
                {
                    button.Rect.gameObject.SetActive(false);
                    continue;
                }

                var info = actions[i];
                _actionIds[i] = info.Id;
                button.Rect.gameObject.SetActive(true);
                button.Label.text = _layout.Compact ? info.Label : info.Label + HotkeyFor(info.Id);
                button.SubLabel.text = info.Enabled ? CostLine(info) : info.DisabledReason;
                button.SubLabel.color = info.Enabled ? UiTheme.TextMuted : UiTheme.TextDim;
                button.Label.color = info.Enabled ? UiTheme.TextPrimary : UiTheme.TextDim;
                button.SetBaseColor(info.Enabled ? UiTheme.PanelRaised : UiTheme.PanelDeep);
                button.SetInteractable(info.Enabled && _session.Phase == SessionPhase.Playing);
            }

            _footer.text = unit.NeedsAttention(cfg)
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Warn) + ">Esta máquina necesita atención.</color>"
                : _layout.Compact ? string.Empty : "Tab salta al siguiente servidor con problemas.";
        }

        static string CostLine(ServerActionInfo info)
        {
            string cost = info.Cost > 0 ? Fmt.Money(info.Cost) : "gratis";
            if (info.Duration > 0.5f) cost += " · " + Fmt.Seconds(info.Duration) + " fuera de servicio";
            return cost;
        }

        static string HotkeyFor(ServerActionId id)
        {
            switch (id)
            {
                case ServerActionId.Reboot: return "   [R]";
                case ServerActionId.Cool: return "   [E]";
                case ServerActionId.Repair: return "   [A]";
                case ServerActionId.Patch: return "   [P]";
                default: return string.Empty;
            }
        }

        void SetStat(int index, string value, Color color)
        {
            _stats[index].Value.text = value;
            _stats[index].Value.color = color;
        }
    }
}
