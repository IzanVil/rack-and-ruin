using System.Collections.Generic;
using ServerGame.Core;
using ServerGame.Events;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ServerGame.UI
{
    // Monta y coordina la UI. Los valores se leen por sondeo cada frame; el bus solo
    // avisa de lo puntual: log, cierre de turno y fin de partida.
    public sealed class GameUi
    {
        readonly GameSession _session;
        readonly EventBus _bus;
        readonly UiLayout _layout;
        readonly HudView _hud;
        readonly RackView _rack;
        readonly InspectorView _inspector;
        readonly LogView _log;
        readonly UpgradesView _upgrades;
        readonly OverlayView _overlay;

        readonly RectTransform _sheet;

        readonly CanvasScaler _scaler;
        int _dragThreshold = -1;

        public Canvas Canvas { get; }
        public UiLayout Layout => _layout;

        public IReadOnlyList<LogEntry> LogEntries => _log.Entries;

        public System.Action RestartRequested;

        public GameUi(GameSession session, Transform parent, UiLayout layout,
            IReadOnlyList<LogEntry> previousLog = null)
        {
            _session = session;
            _bus = session.Bus;
            _layout = layout;

            var canvasGo = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.layer = LayerMask.NameToLayer("UI");

            Canvas = canvasGo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = false;

            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = layout.Reference;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.matchWidthOrHeight = layout.Match;
            _scaler.referencePixelsPerUnit = 100f;

            EnsureEventSystem();

            var canvasRect = (RectTransform)canvasGo.transform;

            var background = Ui.NewPanel("Background", canvasRect, UiTheme.Background, 0);
            Ui.Stretch(background.rectTransform);

            var root = Ui.NewRect("Root", canvasRect);
            Ui.Stretch(root, layout.Margin, layout.Margin, layout.Margin, layout.Margin);

            if (layout.Compact) BuildCompact(root, previousLog, out _hud, out _rack, out _log);
            else BuildWide(root, previousLog, out _hud, out _rack, out _inspector, out _log);

            _hud.UpgradesButton.OnClick(() => _upgrades.Toggle());

            if (layout.Compact)
            {
                _sheet = BuildSheet(canvasRect, out _inspector);
            }

            _upgrades = new UpgradesView(canvasRect, session, layout);
            _overlay = new OverlayView(canvasRect, layout);

            _bus.Logged += OnLogged;
            _bus.DayEnded += OnDayEnded;
            _bus.GameOver += OnGameOver;
        }

        void BuildWide(RectTransform root, IReadOnlyList<LogEntry> previousLog,
            out HudView hud, out RackView rack, out InspectorView inspector, out LogView log)
        {
            hud = new HudView(root, _session, _layout, null);

            var body = Ui.NewRect("Body", root);
            Ui.Stretch(body, 0f, 0f, _layout.HudTotalHeight + 6f, 0f);

            var inspectorHolder = Ui.NewRect("InspectorHolder", body);
            Ui.Right(inspectorHolder, _layout.InspectorWidth);
            inspector = new InspectorView(inspectorHolder, _session, _layout);

            var leftColumn = Ui.NewRect("LeftColumn", body);
            Ui.Stretch(leftColumn, 0f, _layout.InspectorWidth + _layout.Gap, 0f, 0f);

            var rackHolder = Ui.NewRect("RackHolder", leftColumn);
            Ui.Stretch(rackHolder, 0f, 0f, 0f, _layout.LogHeight + _layout.Gap);
            rack = new RackView(rackHolder, _session, _layout, _session.Select, () => _upgrades.Open());

            var logHolder = Ui.NewRect("LogHolder", leftColumn);
            Ui.Bottom(logHolder, _layout.LogHeight);
            log = new LogView(logHolder, _session.Bus, _layout, previousLog);
        }

        void BuildCompact(RectTransform root, IReadOnlyList<LogEntry> previousLog,
            out HudView hud, out RackView rack, out LogView log)
        {
            var controlBar = Ui.NewRect("ControlBar", root);
            Ui.Bottom(controlBar, _layout.ControlBarHeight);

            hud = new HudView(root, _session, _layout, controlBar);

            var body = Ui.NewRect("Body", root);
            Ui.Stretch(body, 0f, 0f, _layout.HudTotalHeight + 6f,
                _layout.ControlBarHeight + _layout.Gap);

            var logHolder = Ui.NewRect("LogHolder", body);
            Ui.Bottom(logHolder, _layout.LogHeight);
            log = new LogView(logHolder, _session.Bus, _layout, previousLog);

            var rackHolder = Ui.NewRect("RackHolder", body);
            Ui.Stretch(rackHolder, 0f, 0f, 0f, _layout.LogHeight + _layout.Gap);
            rack = new RackView(rackHolder, _session, _layout, OpenSheetFor, () => _upgrades.Open());
        }

        RectTransform BuildSheet(RectTransform canvasRect, out InspectorView inspector)
        {
            var sheet = Ui.NewRect("Sheet", canvasRect);
            Ui.Stretch(sheet);

            var dim = Ui.NewPanel("Dim", sheet, UiTheme.Overlay, 0);
            dim.raycastTarget = true;
            Ui.Stretch(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CloseSheet);

            var panel = Ui.NewPanel("Panel", sheet, UiTheme.Panel, UiTheme.RadiusPanel);
            panel.raycastTarget = true;
            Ui.Bottom(panel.rectTransform, _layout.SheetHeight);

            var close = Ui.NewButton("CloseSheet", panel.rectTransform, "VOLVER AL RACK",
                UiTheme.PanelRaised, UiTheme.TextPrimary, 15, UiTheme.RadiusSmall);
            var closeRect = close.Rect;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 12f);
            closeRect.sizeDelta = new Vector2(
                Mathf.Min(360f, _layout.Reference.x - _layout.Margin * 2f - 28f),
                _layout.Landscape ? 38f : 46f);
            close.OnClick(CloseSheet);

            var content = Ui.NewRect("InspectorHolder", panel.rectTransform);
            Ui.Stretch(content, 0f, 0f, 0f, (_layout.Landscape ? 38f : 46f) + 20f);
            inspector = new InspectorView(content, _session, _layout, ownPanel: false);

            sheet.gameObject.SetActive(false);
            return sheet;
        }

        void OpenSheetFor(ServerUnit unit)
        {
            _session.Select(unit);
            if (_sheet == null) return;
            _sheet.SetAsLastSibling();
            _sheet.gameObject.SetActive(true);
        }

        public void CloseSheet()
        {
            if (_sheet != null) _sheet.gameObject.SetActive(false);
        }

        bool SheetIsOpen => _sheet != null && _sheet.gameObject.activeSelf;

        public void Dispose()
        {
            _bus.Logged -= OnLogged;
            _bus.DayEnded -= OnDayEnded;
            _bus.GameOver -= OnGameOver;
            _log.Dispose();
        }

        public void ShowIntro(OverlayView.IntroOptions options) => _overlay.ShowIntro(options);

        public void ShowDaySummary(DaySummary summary) => OnDayEnded(summary);

        public void ShowGameOver(GameOverInfo info) => OnGameOver(info);

        static void OnLogged(LogEntry entry)
        {
            if (entry.Level == LogLevel.Critical) Sfx.Alert();
            else if (entry.Level == LogLevel.Success) Sfx.Success();
        }

        void OnDayEnded(DaySummary summary)
        {
            CloseSheet();
            _overlay.ShowDaySummary(summary, _session.StartNextDay, _upgrades.Open);
        }

        void OnGameOver(GameOverInfo info)
        {
            CloseSheet();
            _upgrades.Close();
            _overlay.ShowGameOver(info, _session.Mode, RunSeed.Label(_session.Seed, _session.Mode),
                () => RestartRequested?.Invoke(),
                () => Share.Copy(Share.ResultText(info, _session.Seed, _session.Mode)));
        }

        public void SkipIntro()
        {
            _overlay.Hide();
            _session.BeginRun();
        }

        public void OpenUpgradesForCapture() => _upgrades.Open();
        public void CloseUpgradesForCapture() => _upgrades.Close();
        public void OpenSheetForCapture() => OpenSheetFor(_session.Selected ?? _session.Rack[0]);

        public void Tick()
        {
            // El resumen de turno se cierra con su botón, pero si la partida avanza por
            // cualquier otra vía (herramientas, reinicio) no debe quedarse colgado encima.
            if (_overlay.IsOpen && _overlay.Current == OverlayView.Screen.DaySummary
                && _session.Phase == SessionPhase.Playing)
            {
                _overlay.Hide();
            }

            SyncDragThreshold();
            HandleInput();
            _hud.Refresh();
            _rack.Refresh();
            _inspector.Refresh();
            _upgrades.Refresh();
        }

        void SyncDragThreshold()
        {
            var events = EventSystem.current;
            if (events == null) return;

            int threshold = Mathf.Max(8, Mathf.RoundToInt(10f * _scaler.scaleFactor));
            if (threshold == _dragThreshold) return;

            _dragThreshold = threshold;
            events.pixelDragThreshold = threshold;
        }

        void HandleInput()
        {
            // Fuera del modo Play (pruebas en el editor) no hay entrada que leer.
            if (!Application.isPlaying) return;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (_overlay.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_upgrades.IsOpen) _upgrades.Close();
                else CloseSheet();
                return;
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                _upgrades.Toggle();
                return;
            }

            if (_upgrades.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Space)) _session.TogglePause();
            if (Input.GetKeyDown(KeyCode.Alpha1)) _session.SetSpeed(1f);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _session.SetSpeed(2f);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _session.SetSpeed(4f);
            if (Input.GetKeyDown(KeyCode.Tab)) _session.SelectNextProblem();
            if (Input.GetKeyDown(KeyCode.N)) Sfx.ToggleMute();

            var selected = _session.Selected;
            if (selected == null) return;

            if (Input.GetKeyDown(KeyCode.R)) _session.Execute(ServerActionId.Reboot, selected);
            if (Input.GetKeyDown(KeyCode.E)) _session.Execute(ServerActionId.Cool, selected);
            if (Input.GetKeyDown(KeyCode.A)) _session.Execute(ServerActionId.Repair, selected);
            if (Input.GetKeyDown(KeyCode.P)) _session.Execute(ServerActionId.Patch, selected);
#endif
        }

        /// <summary>Crea el EventSystem si la escena no trae uno. Elige el módulo de
        /// entrada adecuado por reflexión para funcionar tanto con el Input Manager
        /// clásico como con el paquete Input System.</summary>
        static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
#else
            if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
            var go = new GameObject("EventSystem", typeof(EventSystem));

            var inputSystemModule = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null) go.AddComponent(inputSystemModule);
            else go.AddComponent<StandaloneInputModule>();
        }
    }
}
