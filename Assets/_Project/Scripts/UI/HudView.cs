using System.Text;
using ServerGame.Core;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>Barra superior: caja, reputación, tráfico, reloj del turno y control
    /// de velocidad. Debajo, una franja con las incidencias activas.</summary>
    public sealed class HudView
    {
        public const float BarHeight = 76f;
        public const float StripHeight = 26f;
        public const float TotalHeight = BarHeight + StripHeight + 6f;

        readonly GameSession _session;
        readonly StringBuilder _sb = new StringBuilder(160);

        readonly Text _dayValue;
        readonly Text _moneyValue;
        readonly Text _reputationValue;
        readonly Bar _reputationBar;
        readonly Text _demandValue;
        readonly Text _demandDetail;
        readonly Text _slaValue;
        readonly Bar _slaBar;
        readonly Text _clockValue;
        readonly Bar _clockBar;

        readonly UiButton[] _speedButtons;
        readonly float[] _speedValues = { 0f, 1f, 2f, 4f };

        readonly Image _stripBg;
        readonly Text _stripText;

        public UiButton UpgradesButton { get; }

        public HudView(Transform parent, GameSession session)
        {
            _session = session;

            var panel = Ui.NewPanel("Hud", parent, UiTheme.Panel, UiTheme.RadiusPanel);
            Ui.Top(panel.rectTransform, BarHeight);
            var root = panel.rectTransform;

            var title = Ui.NewText("Title", root, "UPTIME", 22, UiTheme.Accent,
                TextAnchor.LowerLeft, FontStyle.Bold);
            Ui.Place(title.rectTransform, 20f, 14f, 200f, 26f);

            var subtitle = Ui.NewText("Subtitle", root, "TURNO DE NOCHE", 10, UiTheme.TextDim,
                TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Place(subtitle.rectTransform, 21f, 42f, 200f, 14f);

            _dayValue = Block(root, 150f, "TURNO", 120f, out _);
            _moneyValue = Block(root, 270f, "CAJA", 150f, out _);
            _moneyValue.color = UiTheme.Money;
            _reputationValue = Block(root, 420f, "REPUTACIÓN", 150f, out _reputationBar, true);
            _demandValue = Block(root, 570f, "DEMANDA", 165f, out _);
            _demandDetail = Ui.NewText("DemandDetail", root, string.Empty, 11, UiTheme.TextMuted);
            Ui.Place(_demandDetail.rectTransform, 570f, 52f, 200f, 14f);
            _slaValue = Block(root, 760f, "SLA DEL TURNO", 150f, out _slaBar, true);
            _clockValue = Block(root, 910f, "TIEMPO RESTANTE", 150f, out _clockBar, true);

            // --- controles de velocidad, anclados a la derecha ---
            var controls = Ui.NewRect("Controls", root);
            Ui.Right(controls, 400f, 16f, 16f, 16f);

            UpgradesButton = Ui.NewButton("Upgrades", controls, "MEJORAS  [M]",
                UiTheme.AccentDeep, UiTheme.TextPrimary, 14, UiTheme.RadiusSmall);
            var upRect = UpgradesButton.Rect;
            upRect.anchorMin = new Vector2(0f, 0.5f);
            upRect.anchorMax = new Vector2(0f, 0.5f);
            upRect.pivot = new Vector2(0f, 0.5f);
            upRect.anchoredPosition = new Vector2(0f, 0f);
            upRect.sizeDelta = new Vector2(150f, 38f);

            string[] labels = { "II", "1×", "2×", "4×" };
            _speedButtons = new UiButton[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var button = Ui.NewButton("Speed" + i, controls, labels[i],
                    UiTheme.PanelRaised, UiTheme.TextPrimary, 15, UiTheme.RadiusSmall);
                var rect = button.Rect;
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-(labels.Length - 1 - i) * 58f, 0f);
                rect.sizeDelta = new Vector2(52f, 38f);

                float speed = _speedValues[i];
                button.OnClick(() =>
                {
                    if (speed <= 0f) _session.SetSpeed(0f);
                    else _session.SetSpeed(speed);
                });
                _speedButtons[i] = button;
            }

            // --- franja de incidencias ---
            _stripBg = Ui.NewPanel("Strip", parent, UiTheme.PanelDeep, UiTheme.RadiusSmall);
            Ui.Top(_stripBg.rectTransform, StripHeight, BarHeight + 6f);
            _stripText = Ui.NewText("StripText", _stripBg.rectTransform, string.Empty, 12,
                UiTheme.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Stretch(_stripText.rectTransform, 14f, 14f, 0f, 0f);
        }

        /// <summary>Bloque de estadística: rótulo, valor y (opcionalmente) barra.</summary>
        Text Block(RectTransform parent, float x, string caption, float width, out Bar bar,
            bool withBar = false)
        {
            var captionText = Ui.NewText(caption + "Caption", parent, caption, 10, UiTheme.TextDim,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(captionText.rectTransform, x, 14f, width, 12f);

            var value = Ui.NewText(caption + "Value", parent, "—", 20, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(value.rectTransform, x, 27f, width, 24f);

            if (withBar)
            {
                bar = Ui.NewBar(caption + "Bar", parent, UiTheme.Track, 2);
                Ui.Place(bar.Rect, x, 55f, width - 20f, 5f);
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
    }
}
