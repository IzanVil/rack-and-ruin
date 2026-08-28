using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>Panel lateral con el detalle del servidor seleccionado y sus acciones.
    /// Los botones se crean una vez y se refrescan a partir de GameSession.GetActions,
    /// de modo que las reglas de disponibilidad viven en la lógica, no aquí.</summary>
    public sealed class InspectorView
    {
        const int ActionCount = 7;

        readonly GameSession _session;
        readonly Text _title;
        readonly Image _statePillBg;
        readonly Text _statePill;
        readonly StatRow[] _stats;
        readonly UiButton[] _actions = new UiButton[ActionCount];
        readonly ServerActionId[] _actionIds = new ServerActionId[ActionCount];
        readonly Text _footer;

        sealed class StatRow
        {
            public Text Label;
            public Text Value;
        }

        public InspectorView(Transform parent, GameSession session)
        {
            _session = session;

            var panel = Ui.NewPanel("Inspector", parent, UiTheme.Panel, UiTheme.RadiusPanel);
            Ui.Stretch(panel.rectTransform);

            var content = Ui.NewRect("Content", panel.rectTransform);
            Ui.Stretch(content, 16f, 16f, 16f, 14f);
            Ui.VBox(content, 8f);

            _title = Ui.NewText("Title", content, "SRV-01", 21, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Fixed(_title, height: 26f);

            var pillHolder = Ui.NewRect("PillHolder", content);
            Ui.Fixed(pillHolder, height: 19f);
            _statePillBg = Ui.NewPanel("Pill", pillHolder, UiTheme.Ok, UiTheme.RadiusPill);
            var pillRect = _statePillBg.rectTransform;
            pillRect.anchorMin = new Vector2(0f, 0f);
            pillRect.anchorMax = new Vector2(0f, 1f);
            pillRect.pivot = new Vector2(0f, 0.5f);
            pillRect.offsetMin = new Vector2(0f, 0f);
            pillRect.offsetMax = new Vector2(130f, 0f);
            _statePill = Ui.NewText("Text", pillRect, "EN LÍNEA", 11, UiTheme.Background,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Stretch(_statePill.rectTransform, 6f, 6f, 0f, 0f);

            Ui.Divider(content, UiTheme.Line);

            var statsBox = Ui.NewRect("Stats", content);
            Ui.VBox(statsBox, 3f);
            string[] captions =
            {
                "Capacidad efectiva", "Carga actual", "Temperatura", "Salud del hardware",
                "Memoria filtrada", "Deuda de parches", "Tiempo sin reiniciar"
            };
            _stats = new StatRow[captions.Length];
            for (int i = 0; i < captions.Length; i++) _stats[i] = BuildStatRow(statsBox, captions[i]);
            Ui.Fixed(statsBox, height: captions.Length * 19f + (captions.Length - 1) * 3f);

            Ui.Divider(content, UiTheme.Line);

            var actionsCaption = Ui.NewText("ActionsCaption", content, "ACCIONES", 11,
                UiTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Fixed(actionsCaption, height: 14f);

            var actionsBox = Ui.NewRect("Actions", content);
            Ui.VBox(actionsBox, 5f);
            for (int i = 0; i < ActionCount; i++)
            {
                var button = Ui.NewButton("Action" + i, actionsBox, "—", UiTheme.PanelRaised,
                    UiTheme.TextPrimary, 14, UiTheme.RadiusSmall, withSubLabel: true);
                Ui.Fixed(button.Rect, height: 42f);
                int index = i;
                button.OnClick(() => _session.Execute(_actionIds[index], _session.Selected));
                _actions[i] = button;
            }
            Ui.Fixed(actionsBox, height: ActionCount * 42f + (ActionCount - 1) * 5f);

            _footer = Ui.NewText("Footer", content, string.Empty, 11, UiTheme.TextDim,
                TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
            Ui.Fixed(_footer, height: 30f);
        }

        StatRow BuildStatRow(Transform parent, string caption)
        {
            var row = Ui.NewRect("Row_" + caption, parent);
            Ui.Fixed(row, height: 19f);

            var label = Ui.NewText("Label", row, caption, 12, UiTheme.TextMuted);
            Ui.Stretch(label.rectTransform, 0f, 110f, 0f, 0f);

            var value = Ui.NewText("Value", row, "—", 13, UiTheme.TextPrimary,
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
                button.Label.text = info.Label + HotkeyFor(info.Id);
                button.SubLabel.text = info.Enabled ? CostLine(info) : info.DisabledReason;
                button.SubLabel.color = info.Enabled ? UiTheme.TextMuted : UiTheme.TextDim;
                button.Label.color = info.Enabled ? UiTheme.TextPrimary : UiTheme.TextDim;
                button.SetBaseColor(info.Enabled ? UiTheme.PanelRaised : UiTheme.PanelDeep);
                button.SetInteractable(info.Enabled && _session.Phase == SessionPhase.Playing);
            }

            _footer.text = unit.NeedsAttention(cfg)
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Warn) + ">Esta máquina necesita atención.</color>"
                : "Tab salta al siguiente servidor con problemas.";
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
