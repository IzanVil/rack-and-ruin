using System.Text;
using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    public sealed class HudView
    {
        readonly GameSession _session;
        readonly UiLayout _layout;
        readonly StringBuilder _sb = new StringBuilder(160);

        Text _dayValue;
        Text _moneyValue;
        Text _reputationValue;
        Bar _reputationBar;
        Text _demandValue;
        Text _demandDetail;
        Text _slaValue;
        Bar _slaBar;
        Text _clockValue;
        Bar _clockBar;

        UiButton[] _speedButtons;
        readonly float[] _speedValues = { 0f, 1f, 2f, 4f };

        readonly Image _stripBg;
        readonly Text _stripText;

        public UiButton UpgradesButton { get; private set; }
#if !UNITY_WEBGL
        public UiButton ExitButton { get; private set; }
#endif

        public HudView(Transform parent, GameSession session, UiLayout layout, Transform controlsParent)
        {
            _session = session;
            _layout = layout;

            var panel = Ui.NewPanel("Hud", parent, UiTheme.Panel, UiTheme.RadiusPanel);
            Ui.Top(panel.rectTransform, layout.HudBarHeight);
            var root = panel.rectTransform;

            if (layout.Landscape) BuildLandscapeBlocks(root);
            else if (layout.Compact) BuildCompactBlocks(root);
            else BuildWideBlocks(root);

            BuildControls(root, controlsParent);

            _stripBg = Ui.NewPanel("Strip", parent, UiTheme.PanelDeep, UiTheme.RadiusSmall);
            Ui.Top(_stripBg.rectTransform, layout.HudStripHeight, layout.HudBarHeight + 6f);
            _stripText = Ui.NewText("StripText", _stripBg.rectTransform, string.Empty,
                layout.Compact ? 11 : 12, UiTheme.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Stretch(_stripText.rectTransform, layout.Compact ? 10f : 14f,
                layout.Compact ? 10f : 14f, 0f, 0f);
        }

        void BuildWideBlocks(RectTransform root)
        {
            var title = Ui.NewText("Title", root, "UPTIME", 22, UiTheme.Accent,
                TextAnchor.LowerLeft, FontStyle.Bold);
            Ui.Place(title.rectTransform, 20f, 14f, 200f, 26f);

            var subtitle = Ui.NewText("Subtitle", root, "TURNO DE NOCHE", 10, UiTheme.TextDim,
                TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Place(subtitle.rectTransform, 21f, 42f, 200f, 14f);

            _dayValue = Block(root, 150f, 14f, "TURNO", 120f, 20, out _);
            _moneyValue = Block(root, 270f, 14f, "CAJA", 150f, 20, out _);
            _moneyValue.color = UiTheme.Money;
            _reputationValue = Block(root, 420f, 14f, "REPUTACIÓN", 150f, 20, out _reputationBar, true);
            _demandValue = Block(root, 570f, 14f, "DEMANDA", 165f, 20, out _);
            _demandDetail = Ui.NewText("DemandDetail", root, string.Empty, 11, UiTheme.TextMuted);
            Ui.Place(_demandDetail.rectTransform, 570f, 52f, 200f, 14f);
            _slaValue = Block(root, 760f, 14f, "SLA DEL TURNO", 150f, 20, out _slaBar, true);
            _clockValue = Block(root, 910f, 14f, "TIEMPO RESTANTE", 150f, 20, out _clockBar, true);
        }

        void BuildCompactBlocks(RectTransform root)
        {
            const float RowA = 6f;
            const float RowB = 52f;
            const float BarOffset = 39f;

            _dayValue = Block(root, 12f, RowA, "TURNO", 52f, 17, out _, false, BarOffset);
            _moneyValue = Block(root, 68f, RowA, "CAJA", 116f, 17, out _, false, BarOffset);
            _moneyValue.color = UiTheme.Money;
            _reputationValue = Block(root, 188f, RowA, "REPUT.", 98f, 17, out _reputationBar, true, BarOffset);
            _slaValue = Block(root, 292f, RowA, "SLA", 100f, 17, out _slaBar, true, BarOffset);

            _demandValue = Block(root, 12f, RowB, "DEMANDA", 130f, 17, out _, false, BarOffset);
            _demandDetail = Ui.NewText("DemandDetail", root, string.Empty, 11, UiTheme.TextMuted);
            Ui.Place(_demandDetail.rectTransform, 148f, RowB + 14f, 140f, 18f);
            _clockValue = Block(root, 292f, RowB, "TIEMPO", 100f, 17, out _clockBar, true, BarOffset);
        }

        void BuildLandscapeBlocks(RectTransform root)
        {
            const float Row = 7f;
            const float BarOffset = 38f;

            _dayValue = Block(root, 12f, Row, "TURNO", 50f, 17, out _, false, BarOffset);
            _moneyValue = Block(root, 66f, Row, "CAJA", 112f, 17, out _, false, BarOffset);
            _moneyValue.color = UiTheme.Money;
            _reputationValue = Block(root, 182f, Row, "REPUT.", 96f, 17, out _reputationBar, true, BarOffset);
            _demandValue = Block(root, 282f, Row, "DEMANDA", 122f, 17, out _, false, BarOffset);
            _demandDetail = Ui.NewText("DemandDetail", root, string.Empty, 11, UiTheme.TextMuted);
            Ui.Place(_demandDetail.rectTransform, 408f, Row + 21f, 150f, 16f);
            _slaValue = Block(root, 566f, Row, "SLA", 96f, 17, out _slaBar, true, BarOffset);
            _clockValue = Block(root, 666f, Row, "TIEMPO", 96f, 17, out _clockBar, true, BarOffset);
        }

        void BuildControls(RectTransform hudRoot, Transform controlsParent)
        {
            // La versión web se cierra desde la pestaña del navegador; Application.Quit()
            // no hace nada ahí, así que el botón de salir solo tiene sentido en la app nativa.
#if !UNITY_WEBGL
            float exitReserved = _layout.Compact ? 0f : 88f;
#else
            const float exitReserved = 0f;
#endif
            Transform controls;
            if (controlsParent != null)
            {
                controls = controlsParent;
            }
            else
            {
                var holder = Ui.NewRect("Controls", hudRoot);
                Ui.Right(holder, 400f + exitReserved, 16f, 16f, 16f);
                controls = holder;
            }

            float buttonHeight = _layout.Landscape ? 34f : _layout.Compact ? 40f : 38f;
            float speedWidth = _layout.Landscape ? 56f : _layout.Compact ? 62f : 52f;
            float speedStep = speedWidth + (_layout.Compact ? 8f : 6f);
            float upgradesWidth = _layout.Landscape ? 110f : _layout.Compact ? 118f : 150f;

#if !UNITY_WEBGL
            if (!_layout.Compact)
            {
                ExitButton = Ui.NewButton("Exit", controls, "SALIR",
                    UiTheme.PanelRaised, UiTheme.Danger, 13, UiTheme.RadiusSmall);
                var exitRect = ExitButton.Rect;
                exitRect.anchorMin = new Vector2(0f, 0.5f);
                exitRect.anchorMax = new Vector2(0f, 0.5f);
                exitRect.pivot = new Vector2(0f, 0.5f);
                exitRect.anchoredPosition = Vector2.zero;
                exitRect.sizeDelta = new Vector2(80f, 38f);
                ExitButton.OnClick(RequestExit);
            }
#endif

            UpgradesButton = Ui.NewButton("Upgrades", controls,
                _layout.Compact ? "MEJORAS" : "MEJORAS  [M]",
                UiTheme.AccentDeep, UiTheme.TextPrimary, 14, UiTheme.RadiusSmall);
            var upRect = UpgradesButton.Rect;
            float upAnchorX = _layout.Compact ? 1f : 0f;
            upRect.anchorMin = new Vector2(upAnchorX, 0.5f);
            upRect.anchorMax = new Vector2(upAnchorX, 0.5f);
            upRect.pivot = new Vector2(upAnchorX, 0.5f);
            upRect.anchoredPosition = new Vector2(_layout.Compact ? 0f : exitReserved, 0f);
            upRect.sizeDelta = new Vector2(upgradesWidth, buttonHeight);

            string[] labels = { "II", "1×", "2×", "4×" };
            _speedButtons = new UiButton[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var button = Ui.NewButton("Speed" + i, controls, labels[i],
                    UiTheme.PanelRaised, UiTheme.TextPrimary, 15, UiTheme.RadiusSmall);
                var rect = button.Rect;
                float anchorX = _layout.Compact ? 0f : 1f;
                rect.anchorMin = new Vector2(anchorX, 0.5f);
                rect.anchorMax = new Vector2(anchorX, 0.5f);
                rect.pivot = new Vector2(anchorX, 0.5f);
                rect.anchoredPosition = _layout.Compact
                    ? new Vector2(i * speedStep, 0f)
                    : new Vector2(-(labels.Length - 1 - i) * speedStep, 0f);
                rect.sizeDelta = new Vector2(speedWidth, buttonHeight);

                float speed = _speedValues[i];
                button.OnClick(() => _session.SetSpeed(speed));
                _speedButtons[i] = button;
            }
        }

        Text Block(RectTransform parent, float x, float y, string caption, float width,
            int valueSize, out Bar bar, bool withBar = false, float barOffset = 41f)
        {
            var captionText = Ui.NewText(caption + "Caption", parent, caption, 10, UiTheme.TextDim,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(captionText.rectTransform, x, y, width, 12f);

            var value = Ui.NewText(caption + "Value", parent, "—", valueSize, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(value.rectTransform, x, y + 13f, width, 24f);

            if (withBar)
            {
                bar = Ui.NewBar(caption + "Bar", parent, UiTheme.Track, 2);
                Ui.Place(bar.Rect, x, y + barOffset, width - 20f, 5f);
                bar.Set(0f, UiTheme.Accent);
            }
            else
            {
                bar = null;
            }

            return value;
        }

        public void Refresh()
        {
            var session = _session;

            _dayValue.text = session.Day.ToString();

            _moneyValue.text = Fmt.Money(session.Money);

            _reputationValue.text = Mathf.RoundToInt(session.Reputation).ToString();
            var repColor = UiTheme.ReputationColor(session.Reputation);
            _reputationValue.color = repColor;
            _reputationBar.Set(session.Reputation / 100f, repColor);

            _demandValue.text = Fmt.Rate(session.Demand);
            float dropped = session.Dropped;
            if (dropped > 0.5f)
            {
                _demandDetail.text = "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Critical) + ">" +
                                     Fmt.Compact(dropped) + " req/s rechazadas</color>";
                _demandValue.color = UiTheme.Critical;
            }
            else
            {
                _demandDetail.text = Fmt.Compact(session.Served) + " req/s atendidas";
                _demandValue.color = UiTheme.TextPrimary;
            }

            float sla = session.DaySla;
            _slaValue.text = Fmt.Percent01(sla, 2);
            var slaColor = sla >= 0.99f ? UiTheme.Ok : sla >= 0.95f ? UiTheme.Warn : UiTheme.Critical;
            _slaValue.color = slaColor;
            _slaBar.Set(sla, slaColor);

            _clockValue.text = Fmt.Clock(session.DayTimeRemaining);
            _clockBar.Set(session.DayProgress01, UiTheme.Accent);

            for (int i = 0; i < _speedButtons.Length; i++)
            {
                bool active = Mathf.Approximately(session.Speed, _speedValues[i]);
                _speedButtons[i].SetBaseColor(active ? UiTheme.AccentDeep : UiTheme.PanelRaised);
                _speedButtons[i].Label.color = active ? UiTheme.Accent : UiTheme.TextMuted;
                _speedButtons[i].SetInteractable(session.Phase == SessionPhase.Playing);
            }

            UpgradesButton.SetInteractable(session.Phase == SessionPhase.Playing ||
                                           session.Phase == SessionPhase.DayReview);

            RefreshStrip();
        }

        void RefreshStrip()
        {
            var effects = _session.Incidents.Active;
            if (effects.Count == 0)
            {
                _stripBg.color = UiTheme.PanelDeep;
                _stripText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.TextDim) +
                                  ">Sin incidencias activas.</color>";
                return;
            }

            _sb.Length = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (i > 0) _sb.Append("     ");
                var color = effect.DemandMultiplier > 1.6f || effect.CoolingMultiplier < 1f
                    ? UiTheme.Critical
                    : UiTheme.Warn;
                _sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append(">▲ ")
                   .Append(effect.Label.ToUpperInvariant())
                   .Append("  ").Append(Fmt.Seconds(effect.Remaining))
                   .Append("</color>");
            }

            _stripBg.color = new Color(UiTheme.Critical.r, UiTheme.Critical.g, UiTheme.Critical.b, 0.12f);
            _stripText.text = _sb.ToString();
        }

#if !UNITY_WEBGL
        static void RequestExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
#endif
    }
}
