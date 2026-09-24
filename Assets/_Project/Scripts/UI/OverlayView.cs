using System;
using ServerGame.Core;
using ServerGame.Events;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.UI
{
    // Modal reutilizable: intro, cierre de turno y fin de partida.
    public sealed class OverlayView
    {
        /// <summary>Qué pantalla ocupa el modal en este momento.</summary>
        public enum Screen { None, Intro, DaySummary, GameOver }

        public struct IntroOptions
        {
            public string Note;
            public string PrimaryLabel;
            public Action OnPrimary;
            public string SecondaryLabel;
            public Action OnSecondary;
        }

        const int MaxRows = 8;

        readonly RectTransform _root;
        readonly Text _title;
        readonly Text _subtitle;
        readonly Text _body;
        readonly Text _bodyRight;
        readonly RectTransform _rowsBox;
        readonly Text[] _rowLabels = new Text[MaxRows];
        readonly Text[] _rowValues = new Text[MaxRows];
        readonly UiButton _primary;
        readonly UiButton _secondary;

        Action _primaryAction;
        Action _secondaryAction;
        readonly bool _compact;
        readonly bool _twoColumnBody;

        public bool IsOpen => _root.gameObject.activeSelf;
        public Screen Current { get; private set; } = Screen.None;

        public OverlayView(Transform parent, UiLayout layout)
        {
            bool compact = layout.Compact;
            _compact = compact;
            float panelW = layout.ModalSize.x;
            float panelH = layout.ModalSize.y;
            float pad = compact ? 20f : 36f;
            float inner = panelW - pad * 2f;

            bool stackButtons = compact && !layout.Landscape;
            float buttonH = layout.Landscape ? 44f : 48f;
            float secondaryBottom = compact ? 18f : 32f;
            float primaryBottom = stackButtons ? secondaryBottom + buttonH + 10f : secondaryBottom;
            float contentTop = compact ? 112f : 126f;
            float contentBottom = primaryBottom + buttonH + (compact ? 16f : 14f);
            float contentHeight = panelH - contentTop - contentBottom;

            _root = Ui.NewRect("Overlay", parent);
            Ui.Stretch(_root);

            var dim = Ui.NewPanel("Dim", _root, UiTheme.Overlay, 0);
            dim.raycastTarget = true;
            Ui.Stretch(dim.rectTransform);

            var panel = Ui.NewPanel("Panel", _root, UiTheme.Panel, UiTheme.RadiusPanel);
            panel.raycastTarget = true;
            var panelRect = Ui.Center(panel.rectTransform, panelW, panelH);

            _title = Ui.NewText("Title", panelRect, string.Empty, compact ? 22 : 30,
                UiTheme.TextPrimary, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Place(_title.rectTransform, pad, compact ? 22f : 30f, inner, 36f);

            _subtitle = Ui.NewText("Subtitle", panelRect, string.Empty, compact ? 13 : 14,
                UiTheme.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
            Ui.Place(_subtitle.rectTransform, pad, compact ? 58f : 72f, inner, compact ? 50f : 44f);

            RectTransform contentHost = panelRect;

            _twoColumnBody = layout.Landscape;
            float bodyWidth = _twoColumnBody ? inner * 0.5f - 12f : inner;

            _body = Ui.NewText("Body", contentHost, string.Empty, compact ? 13 : 14,
                UiTheme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
            if (_twoColumnBody)
            {
                Ui.Place(_body.rectTransform, pad, contentTop, bodyWidth, contentHeight);
                _bodyRight = Ui.NewText("BodyRight", panelRect, string.Empty, 13,
                    UiTheme.TextPrimary, TextAnchor.UpperLeft, FontStyle.Normal, wrap: true);
                Ui.Place(_bodyRight.rectTransform, pad + inner * 0.5f + 12f, contentTop,
                    bodyWidth, contentHeight);
            }
            else
            {
                Ui.Place(_body.rectTransform, pad, contentTop, inner, contentHeight);
            }

            _rowsBox = Ui.NewRect("Rows", panelRect);
            Ui.Place(_rowsBox, pad, contentTop, inner, contentHeight);

            if (layout.Landscape)
            {
                var grid = _rowsBox.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(inner * 0.5f - 10f, 28f);
                grid.spacing = new Vector2(20f, 6f);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Vertical;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
                grid.constraintCount = 4;
            }
            else
            {
                Ui.VBox(_rowsBox, 5f);
            }

            float labelInset = layout.Landscape ? 110f : compact ? 130f : 220f;
            for (int i = 0; i < MaxRows; i++)
            {
                var row = Ui.NewRect("Row" + i, _rowsBox);
                Ui.Fixed(row, height: compact ? 28f : 24f);

                _rowLabels[i] = Ui.NewText("Label", row, string.Empty, compact ? 13 : 14,
                    UiTheme.TextMuted);
                Ui.Stretch(_rowLabels[i].rectTransform, 0f, labelInset, 0f, 0f);

                _rowValues[i] = Ui.NewText("Value", row, string.Empty, compact ? 15 : 16,
                    UiTheme.TextPrimary, TextAnchor.MiddleRight, FontStyle.Bold);
                Ui.Stretch(_rowValues[i].rectTransform, 0f, 0f, 0f, 0f);
            }

            _primary = Ui.NewButton("Primary", panelRect, string.Empty, UiTheme.AccentDeep,
                UiTheme.TextPrimary, 16, UiTheme.RadiusSmall);
            PlaceButton(_primary, stackButtons, pad, primaryBottom,
                stackButtons ? inner : compact ? inner * 0.5f - 6f : 300f, buttonH,
                alignRight: true);
            _primary.OnClick(() => _primaryAction?.Invoke());

            _secondary = Ui.NewButton("Secondary", panelRect, string.Empty, UiTheme.PanelRaised,
                UiTheme.TextPrimary, 15, UiTheme.RadiusSmall);
            PlaceButton(_secondary, stackButtons, pad, secondaryBottom,
                stackButtons ? inner : compact ? inner * 0.5f - 6f : 260f, buttonH,
                alignRight: false);
            _secondary.OnClick(() => _secondaryAction?.Invoke());

            _root.gameObject.SetActive(false);
        }

        static void PlaceButton(UiButton button, bool stacked, float pad, float bottom,
            float width, float height, bool alignRight)
        {
            float anchorX = stacked ? 0f : (alignRight ? 1f : 0f);
            var rect = button.Rect;
            rect.anchorMin = new Vector2(anchorX, 0f);
            rect.anchorMax = new Vector2(anchorX, 0f);
            rect.pivot = new Vector2(anchorX, 0f);
            rect.anchoredPosition = new Vector2(anchorX == 0f ? pad : -pad, bottom);
            rect.sizeDelta = new Vector2(width, height);
        }

        static string Join(params string[] parts)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        void SetBodyActive(bool active)
        {
            _body.gameObject.SetActive(active);
            if (_bodyRight != null) _bodyRight.gameObject.SetActive(active);
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

        static string BestLabel(RunMode mode)
        {
            switch (mode)
            {
                case RunMode.Daily: return "Mejor en el turno del día";
                case RunMode.Shared: return "Mejor en turnos compartidos";
                default: return "Mejor en partida libre";
            }
        }

        public static IntroOptions NewRunIntro(int seed, RunMode mode, Action onStart,
            Action onDailyRun, Action onFreeRun, int dailyStreak = 0)
        {
            var options = new IntroOptions
            {
                PrimaryLabel = "EMPEZAR EL TURNO 1",
                OnPrimary = onStart,
                SecondaryLabel = "PARTIDA LIBRE",
                OnSecondary = onFreeRun
            };

            switch (mode)
            {
                case RunMode.Daily:
                    options.Note = RunSeed.Label(seed, mode) +
                        (dailyStreak > 1 ? " · llevas " + dailyStreak + " días seguidos" : "") +
                        ". Hoy todo el mundo juega este mismo rack: las mismas incidencias, " +
                        "en el mismo momento y sobre las mismas máquinas.";
                    options.PrimaryLabel = "EMPEZAR EL TURNO DEL DÍA";
                    break;

                case RunMode.Shared:
                    options.Note = RunSeed.Label(seed, mode) +
                        ". Te han pasado esta partida exacta: tu resultado y el suyo se pueden comparar.";
                    break;

                default:
                    options.Note = RunSeed.Label(seed, mode) + ".";
                    options.SecondaryLabel = "TURNO DEL DÍA";
                    options.OnSecondary = onDailyRun;
                    break;
            }

            return options;
        }

        public static IntroOptions ResumeIntro(int seed, RunMode mode, int day, Action onStartOver)
        {
            return new IntroOptions
            {
                Note = "Partida recuperada · " + RunSeed.Label(seed, mode) +
                       ". Sigue en pausa donde la dejaste.",
                PrimaryLabel = "CONTINUAR · TURNO " + day,
                OnPrimary = null,
                SecondaryLabel = "EMPEZAR DE CERO",
                OnSecondary = onStartOver
            };
        }

        public void ShowIntro(IntroOptions options)
        {
            _title.text = "UPTIME · <color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Accent) + ">TURNO DE NOCHE</color>";
            _subtitle.text = "Eres el único técnico de guardia del centro de datos. Mantén el servicio en pie " +
                             "mientras el tráfico crece turno tras turno.";

            _rowsBox.gameObject.SetActive(false);
            SetBodyActive(true);
            string nota = string.IsNullOrEmpty(options.Note)
                ? string.Empty
                : "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Accent) + "><b>" +
                  options.Note + "</b></color>";

            string bucle =
                "<b>El bucle</b>\n" +
                "El balanceador reparte el tráfico entre los servidores en línea. Todo lo que no se atiende " +
                "cuesta dinero y reputación. Si la reputación llega a cero, se acabó el contrato.";

            string amenazas =
                "<b>Lo que se te va a romper</b>\n" +
                "• <color=#F87171>Calor</color>: por encima de 76 °C el servidor rinde menos y se desgasta más rápido.\n" +
                "• <color=#FBBF24>Fugas de memoria</color>: recortan capacidad. Se limpian reiniciando.\n" +
                "• <color=#FBBF24>Deuda de parches</color>: si sube demasiado, acabarás con una brecha de seguridad.\n" +
                "• <color=#F87171>Desgaste</color>: a 0 % de salud la máquina se avería y hay que sustituirla.";

            string controles = _compact
                ? "<b>En el móvil</b>\nToca una máquina para ver su detalle y actuar sobre ella."
                : "<b>Atajos</b>\n" +
                  "espacio pausa · 1 2 3 velocidad · tab siguiente incidencia · m mejoras\n" +
                  "r reiniciar · e refrigerar · a reparar · p parchear";

            if (_twoColumnBody)
            {
                _body.text = Join(nota, bucle);
                _bodyRight.text = Join(amenazas, controles);
            }
            else
            {
                _body.text = Join(nota, bucle, amenazas, controles);
            }

            _primary.Label.text = options.PrimaryLabel;
            var onPrimary = options.OnPrimary;
            _primaryAction = () => { Hide(); onPrimary?.Invoke(); };

            bool hasSecondary = !string.IsNullOrEmpty(options.SecondaryLabel);
            _secondary.Rect.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                _secondary.Label.text = options.SecondaryLabel;
                var onSecondary = options.OnSecondary;
                _secondaryAction = () => { Hide(); onSecondary?.Invoke(); };
            }

            Show(Screen.Intro);
        }

        public void ShowDaySummary(DaySummary summary, Action onContinue, Action onUpgrades)
        {
            bool good = summary.Sla >= 0.99f;
            _title.text = "TURNO " + summary.Day + " CERRADO";
            _subtitle.text = good
                ? "Servicio impecable. El cliente no se ha enterado de nada."
                : "El servicio ha tenido cortes. Mañana entrará más tráfico, no menos.";

            SetBodyActive(false);
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
            _secondary.Label.text = _compact ? "MEJORAS" : "MEJORAS  [M]";
            _secondaryAction = () => onUpgrades?.Invoke();

            Show(Screen.DaySummary);
        }

        public void ShowGameOver(GameOverInfo info, RunMode mode, string runLabel,
            Action onRestart, Func<bool> onShare)
        {
            _title.text = "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Critical) + ">" + info.Title + "</color>";
            _subtitle.text = info.Reason;

            SetBodyActive(false);
            _rowsBox.gameObject.SetActive(true);
            ClearRows();

            SetRow(0, "Turnos completados", (info.DaysSurvived - 1).ToString(), UiTheme.TextPrimary);
            SetRow(1, "Peticiones atendidas en total", Fmt.Compact(info.TotalServed), UiTheme.TextPrimary);
            SetRow(2, "Caja final", Fmt.Money(info.Money), UiTheme.Money);
            SetRow(3, "Puntuación", Fmt.Thousands(info.Score), UiTheme.Accent);
            SetRow(4, info.IsNewRecord ? "¡NUEVO RÉCORD!" : BestLabel(mode),
                Fmt.Thousands(info.BestScore), info.IsNewRecord ? UiTheme.Ok : UiTheme.TextMuted);

            int next = 5;
            if (mode == RunMode.Daily)
            {
                string racha = info.DailyStreak == 1
                    ? "1 día"
                    : info.DailyStreak + " días seguidos";
                if (!_compact) racha += "  ·  " + info.DailyRuns + " en total";
                SetRow(next++, "Racha del turno del día", racha,
                    info.DailyStreak > 1 ? UiTheme.Ok : UiTheme.TextMuted);
            }

            SetRow(next, "Partida", runLabel, UiTheme.TextMuted);

            _primary.Label.text = "VOLVER A EMPEZAR";
            _primaryAction = () => { Hide(); onRestart?.Invoke(); };

            _secondary.Rect.gameObject.SetActive(true);
            _secondary.Label.text = "COPIAR RESULTADO";
            _secondaryAction = () =>
            {
                bool copied = onShare != null && onShare();
                _secondary.Label.text = copied ? "RESULTADO COPIADO" : "NO SE PUDO COPIAR";
            };

            Show(Screen.GameOver);
        }
    }
}
