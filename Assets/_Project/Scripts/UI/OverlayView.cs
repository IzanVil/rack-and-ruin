using System;
using ServerGame.Events;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>Modal reutilizable para la introducción, el cierre de turno y el fin de
    /// partida. Se construye una sola vez y cada pantalla rellena las mismas piezas.</summary>
    public sealed class OverlayView
    {
        /// <summary>Qué pantalla ocupa el modal en este momento.</summary>
        public enum Screen { None, Intro, DaySummary, GameOver }

        const int MaxRows = 8;

        readonly RectTransform _root;
        readonly Text _title;
        readonly Text _subtitle;
        readonly Text _body;
        readonly RectTransform _rowsBox;
        readonly Text[] _rowLabels = new Text[MaxRows];
        readonly Text[] _rowValues = new Text[MaxRows];
        readonly UiButton _primary;
        readonly UiButton _secondary;

        Action _primaryAction;
        Action _secondaryAction;

        public bool IsOpen => _root.gameObject.activeSelf;
        public Screen Current { get; private set; } = Screen.None;

        public OverlayView(Transform parent)
        {
            _root = Ui.NewRect("Overlay", parent);
            Ui.Stretch(_root);

            var dim = Ui.NewPanel("Dim", _root, UiTheme.Overlay, 0);
            dim.raycastTarget = true;
            Ui.Stretch(dim.rectTransform);

            var panel = Ui.NewPanel("Panel", _root, UiTheme.Panel, UiTheme.RadiusPanel);
            panel.raycastTarget = true;
            var panelRect = Ui.Center(panel.rectTransform, 760f, 540f);

            _title = Ui.NewText("Title", panelRect, string.Empty, 30, UiTheme.TextPrimary,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(_title.rectTransform, 36f, 30f, 690f, 36f);

            _subtitle = Ui.NewText("Subtitle", panelRect, string.Empty, 14, UiTheme.TextMuted,
                TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
            Ui.Place(_subtitle.rectTransform, 36f, 72f, 690f, 44f);

            _body = Ui.NewText("Body", panelRect, string.Empty, 14, UiTheme.TextPrimary,
                TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
            Ui.Place(_body.rectTransform, 36f, 126f, 690f, 300f);

            _rowsBox = Ui.NewRect("Rows", panelRect);
            Ui.Place(_rowsBox, 36f, 126f, 690f, 300f);
            Ui.VBox(_rowsBox, 5f);
            for (int i = 0; i < MaxRows; i++)
            {
                var row = Ui.NewRect("Row" + i, _rowsBox);
                Ui.Fixed(row, height: 24f);

                _rowLabels[i] = Ui.NewText("Label", row, string.Empty, 14, UiTheme.TextMuted);
                Ui.Stretch(_rowLabels[i].rectTransform, 0f, 220f, 0f, 0f);

                _rowValues[i] = Ui.NewText("Value", row, string.Empty, 16, UiTheme.TextPrimary,
                    TextAnchor.MiddleRight, FontStyle.Bold);
                Ui.Stretch(_rowValues[i].rectTransform, 0f, 0f, 0f, 0f);
            }

            _primary = Ui.NewButton("Primary", panelRect, string.Empty, UiTheme.AccentDeep,
                UiTheme.TextPrimary, 16, UiTheme.RadiusSmall);
            var primaryRect = _primary.Rect;
            primaryRect.anchorMin = new Vector2(1f, 0f);
            primaryRect.anchorMax = new Vector2(1f, 0f);
            primaryRect.pivot = new Vector2(1f, 0f);
            primaryRect.anchoredPosition = new Vector2(-36f, 32f);
            primaryRect.sizeDelta = new Vector2(300f, 48f);
            _primary.OnClick(() => _primaryAction?.Invoke());

            _secondary = Ui.NewButton("Secondary", panelRect, string.Empty, UiTheme.PanelRaised,
                UiTheme.TextPrimary, 15, UiTheme.RadiusSmall);
            var secondaryRect = _secondary.Rect;
            secondaryRect.anchorMin = new Vector2(0f, 0f);
            secondaryRect.anchorMax = new Vector2(0f, 0f);
            secondaryRect.pivot = new Vector2(0f, 0f);
            secondaryRect.anchoredPosition = new Vector2(36f, 32f);
            secondaryRect.sizeDelta = new Vector2(260f, 48f);
            _secondary.OnClick(() => _secondaryAction?.Invoke());

            _root.gameObject.SetActive(false);
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
            Current = Screen.None;
        }

        void Show(Screen screen)
        {
            Current = screen;
            _root.SetAsLastSibling();
            _root.gameObject.SetActive(true);
        }

        void ClearRows()
        {
            for (int i = 0; i < MaxRows; i++) _rowsBox.GetChild(i).gameObject.SetActive(false);
        }

        void SetRow(int index, string label, string value, Color valueColor)
        {
            if (index < 0 || index >= MaxRows) return;
            _rowsBox.GetChild(index).gameObject.SetActive(true);
            _rowLabels[index].text = label;
            _rowValues[index].text = value;
            _rowValues[index].color = valueColor;
        }

        // ------------------------------------------------------------------ pantallas

        public void ShowIntro(Action onStart)
        {
            _title.text = "UPTIME · <color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Accent) + ">TURNO DE NOCHE</color>";
            _subtitle.text = "Eres el único técnico de guardia del centro de datos. Mantén el servicio en pie " +
                             "mientras el tráfico crece turno tras turno.";

            _rowsBox.gameObject.SetActive(false);
            _body.gameObject.SetActive(true);
            _body.text =
                "<b>El bucle</b>\n" +
                "El balanceador reparte el tráfico entre los servidores en línea. Todo lo que no se atiende " +
                "cuesta dinero y reputación. Si la reputación llega a cero, se acabó el contrato.\n\n" +
                "<b>Lo que se te va a romper</b>\n" +
                "• <color=#F87171>Calor</color>: por encima de 76 °C el servidor rinde menos y se desgasta más rápido.\n" +
                "• <color=#FBBF24>Fugas de memoria</color>: recortan capacidad. Se limpian reiniciando.\n" +
                "• <color=#FBBF24>Deuda de parches</color>: si sube demasiado, acabarás con una brecha de seguridad.\n" +
                "• <color=#F87171>Desgaste</color>: a 0 % de salud la máquina se avería y hay que sustituirla.\n\n" +
                "<b>Atajos</b>\n" +
                "espacio pausa · 1 2 3 velocidad · tab siguiente incidencia · m mejoras\n" +
                "r reiniciar · e refrigerar · a reparar · p parchear";

            _primary.Label.text = "EMPEZAR EL TURNO 1";
            _primaryAction = () => { Hide(); onStart?.Invoke(); };
            _secondary.Rect.gameObject.SetActive(false);

            Show(Screen.Intro);
        }

        public void ShowDaySummary(DaySummary summary, Action onContinue, Action onUpgrades)
        {
            bool good = summary.Sla >= 0.99f;
            _title.text = "TURNO " + summary.Day + " CERRADO";
            _subtitle.text = good
                ? "Servicio impecable. El cliente no se ha enterado de nada."
                : "El servicio ha tenido cortes. Mañana entrará más tráfico, no menos.";

            _body.gameObject.SetActive(false);
            _rowsBox.gameObject.SetActive(true);
            ClearRows();

            var slaColor = summary.Sla >= 0.99f ? UiTheme.Ok : summary.Sla >= 0.95f ? UiTheme.Warn : UiTheme.Critical;
            SetRow(0, "Nivel de servicio (SLA)", Fmt.Percent01(summary.Sla, 2), slaColor);
            SetRow(1, "Peticiones atendidas", Fmt.Compact(summary.Served), UiTheme.TextPrimary);
            SetRow(2, "Peticiones rechazadas", Fmt.Compact(summary.Dropped),
                summary.Dropped > 1f ? UiTheme.Critical : UiTheme.TextPrimary);
            SetRow(3, "Ingresos por tráfico", Fmt.MoneySigned(summary.Revenue), UiTheme.Ok);
            SetRow(4, "Penalizaciones y gastos", Fmt.MoneySigned(-summary.Costs), UiTheme.Danger);
            SetRow(5, "Coste de operación del rack", Fmt.MoneySigned(-summary.OperatingCost), UiTheme.Danger);
            SetRow(6, "Prima por cumplimiento del SLA", Fmt.MoneySigned(summary.Bonus),
                summary.Bonus > 0f ? UiTheme.Ok : UiTheme.TextDim);
            SetRow(7, "Caja disponible", Fmt.Money(summary.MoneyAfter), UiTheme.Money);

            _primary.Label.text = "EMPEZAR EL TURNO " + (summary.Day + 1);
            _primaryAction = () => { Hide(); onContinue?.Invoke(); };

            _secondary.Rect.gameObject.SetActive(true);
            _secondary.Label.text = "MEJORAS  [M]";
            _secondaryAction = () => onUpgrades?.Invoke();

            Show(Screen.DaySummary);
        }

        public void ShowGameOver(GameOverInfo info, Action onRestart)
        {
            _title.text = "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Critical) + ">" + info.Title + "</color>";
            _subtitle.text = info.Reason;

            _body.gameObject.SetActive(false);
            _rowsBox.gameObject.SetActive(true);
            ClearRows();

            SetRow(0, "Turnos completados", (info.DaysSurvived - 1).ToString(), UiTheme.TextPrimary);
            SetRow(1, "Peticiones atendidas en total", Fmt.Compact(info.TotalServed), UiTheme.TextPrimary);
            SetRow(2, "Caja final", Fmt.Money(info.Money), UiTheme.Money);
            SetRow(3, "Puntuación", Fmt.Thousands(info.Score), UiTheme.Accent);
            SetRow(4, info.IsNewRecord ? "¡NUEVO RÉCORD!" : "Mejor puntuación",
                Fmt.Thousands(info.BestScore), info.IsNewRecord ? UiTheme.Ok : UiTheme.TextMuted);

            _primary.Label.text = "EMPEZAR DE CERO";
            _primaryAction = () => { Hide(); onRestart?.Invoke(); };
            _secondary.Rect.gameObject.SetActive(false);

            Show(Screen.GameOver);
        }
    }
}
