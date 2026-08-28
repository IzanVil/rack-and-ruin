using ServerGame.Core;
using ServerGame.Events;
using ServerGame.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ServerGame.UI
{
    /// <summary>Monta y coordina toda la interfaz. Los valores numéricos se leen por
    /// sondeo en cada frame (son pocos widgets); los eventos del bus solo se usan para
    /// lo que ocurre de forma puntual: log, cierre de turno y fin de partida.</summary>
    public sealed class GameUi
    {
        const float Margin = 16f;
        const float InspectorWidth = 344f;

        readonly GameSession _session;
        readonly EventBus _bus;
        readonly HudView _hud;
        readonly RackView _rack;
        readonly InspectorView _inspector;
        readonly LogView _log;
        readonly UpgradesView _upgrades;
        readonly OverlayView _overlay;

        public Canvas Canvas { get; }

        /// <summary>Lo invoca el botón de reinicio de la pantalla de fin de partida.</summary>
        public System.Action RestartRequested;

        public GameUi(GameSession session, Transform parent)
        {
            _session = session;
            _bus = session.Bus;

            // --- lienzo ---
            var canvasGo = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.layer = LayerMask.NameToLayer("UI");

            Canvas = canvasGo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = false;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            EnsureEventSystem();

            var canvasRect = (RectTransform)canvasGo.transform;

            var background = Ui.NewPanel("Background", canvasRect, UiTheme.Background, 0);
            Ui.Stretch(background.rectTransform);

            var root = Ui.NewRect("Root", canvasRect);
            Ui.Stretch(root, Margin, Margin, Margin, Margin);

            // --- cabecera ---
            _hud = new HudView(root, session);
            _hud.UpgradesButton.OnClick(() => _upgrades.Toggle());

            // --- cuerpo ---
            var body = Ui.NewRect("Body", root);
            Ui.Stretch(body, 0f, 0f, HudView.TotalHeight + 6f, 0f);

            var inspectorHolder = Ui.NewRect("InspectorHolder", body);
            Ui.Right(inspectorHolder, InspectorWidth);
            _inspector = new InspectorView(inspectorHolder, session);

            var leftColumn = Ui.NewRect("LeftColumn", body);
            Ui.Stretch(leftColumn, 0f, InspectorWidth + 12f, 0f, 0f);

            var rackHolder = Ui.NewRect("RackHolder", leftColumn);
            Ui.Stretch(rackHolder, 0f, 0f, 0f, LogView.Height + 12f);
            _rack = new RackView(rackHolder, session, () => _upgrades.Open());

            var logHolder = Ui.NewRect("LogHolder", leftColumn);
            Ui.Bottom(logHolder, LogView.Height);
            _log = new LogView(logHolder, session.Bus);

            // --- modales (siempre por encima) ---
            _upgrades = new UpgradesView(canvasRect, session);
            _overlay = new OverlayView(canvasRect);

            _bus.Logged += OnLogged;
            _bus.DayEnded += OnDayEnded;
            _bus.GameOver += OnGameOver;

            _overlay.ShowIntro(session.BeginRun);
        }

        public void Dispose()
        {
            _bus.Logged -= OnLogged;
            _bus.DayEnded -= OnDayEnded;
            _bus.GameOver -= OnGameOver;
        }

        static void OnLogged(LogEntry entry)
        {
            if (entry.Level == LogLevel.Critical) Sfx.Alert();
            else if (entry.Level == LogLevel.Success) Sfx.Success();
        }

        void OnDayEnded(DaySummary summary)
        {
            _overlay.ShowDaySummary(summary, _session.StartNextDay, _upgrades.Open);
        }

        void OnGameOver(GameOverInfo info)
        {
            _upgrades.Close();
            _overlay.ShowGameOver(info, () => RestartRequested?.Invoke());
        }

        /// <summary>Cierra la pantalla de introducción y empieza la partida.
        /// Lo usan las herramientas de captura y cualquier arranque automático.</summary>
        public void SkipIntro()
        {
            _overlay.Hide();
            _session.BeginRun();
        }

        /// <summary>Abre la tienda de mejoras desde código (herramienta de capturas).</summary>
        public void OpenUpgradesForCapture() => _upgrades.Open();

        /// <summary>Cierra la tienda de mejoras desde código (herramienta de capturas).</summary>
        public void CloseUpgradesForCapture() => _upgrades.Close();

        public void Tick()
        {
            // El resumen de turno se cierra con su botón, pero si la partida avanza por
            // cualquier otra vía (herramientas, reinicio) no debe quedarse colgado encima.
            if (_overlay.IsOpen && _overlay.Current == OverlayView.Screen.DaySummary
                && _session.Phase == SessionPhase.Playing)
            {
                _overlay.Hide();
            }

            HandleInput();
            _hud.Refresh();
            _rack.Refresh();
            _inspector.Refresh();
            _upgrades.Refresh();
        }

        // ------------------------------------------------------------------ teclado

        void HandleInput()
        {
            // Fuera del modo Play (pruebas en el editor) no hay entrada que leer.
            if (!Application.isPlaying) return;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (_overlay.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_upgrades.IsOpen) _upgrades.Close();
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
