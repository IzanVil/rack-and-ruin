using System.Collections.Generic;
using ServerGame.Events;
using ServerGame.UI;
using ServerGame.Utils;
using UnityEngine;

namespace ServerGame.Core
{
    // Punto de entrada. Si la escena no trae un GameBootstrap, AutoBoot crea uno,
    // así el juego arranca incluso desde una escena vacía.
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Server Game/Game Bootstrap")]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Configuración de equilibrio. Si se deja vacío se usan los valores por defecto.")]
        [SerializeField] GameConfig config;

        [Tooltip("Fuerza una semilla concreta, útil para depurar. 0 = la que toque: la del " +
                 "día, o la que venga en la URL con ?seed=")]
        [SerializeField] int randomSeed;

        [Tooltip("Crea una cámara si la escena no tiene ninguna, para evitar el aviso de Unity.")]
        [SerializeField] bool createCameraIfMissing = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void SgWatchPageVisibility();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int SgConsumePageWasHidden();
#endif

        // Techo del tiempo real que se simula en un frame. GameSession.Tick trocea el delta
        // en pasos de 50 ms, pero topa en 16 pasos: pasados 0,8 s de tiempo simulado los
        // pasos empiezan a crecer y la térmica se integra con una resolución mucho peor que
        // la de diseño. Como la velocidad llega a x4, el techo en tiempo real es 0,8/4.
        // Por debajo de 5 FPS el juego va a cámara lenta, que es preferible a integrar mal.
        const float MaxFrameDelta = 0.2f;

        const float AutosaveInterval = 5f;

        GameSession _session;
        GameUi _ui;
        TooSmallView _tooSmall;
        Camera _camera;
        float _sinceAutosave;

        bool _resumedRun;
        LayoutKind _layoutKind = LayoutKind.Wide;
        bool _showingTooSmall;
        DaySummary? _openSummary;
        GameOverInfo? _openGameOver;

        public GameSession Session => _session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
#if UNITY_2023_1_OR_NEWER
            var existing = FindFirstObjectByType<GameBootstrap>();
#else
            var existing = FindObjectOfType<GameBootstrap>();
#endif
            if (existing != null) return;

            var go = new GameObject("[Server Game]");
            go.AddComponent<GameBootstrap>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Viewport.Watch();
#if UNITY_WEBGL && !UNITY_EDITOR
            SgWatchPageVisibility();
#endif
            if (createCameraIfMissing) EnsureCamera();
            Boot();
        }

        void Update()
        {
            if (_session == null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Se consulta antes del Tick: si la pestaña estuvo escondida, la pausa deja
            // Speed a 0 y el Tick de este frame (el que trae el salto de tiempo) no simula.
            if (SgConsumePageWasHidden() != 0)
            {
                _session.PauseFromBackground();
                SaveNow();
            }
#endif
            SyncLayout();
            if (_showingTooSmall) return;

            _session.Tick(Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta));
            _ui.Tick();
            Autosave();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveNow();
        }

        void OnApplicationQuit() => SaveNow();

        void OnDestroy()
        {
            _ui?.Dispose();
        }

        void Boot()
        {
            if (randomSeed != 0)
            {
                StartRun(new GameSession(config, randomSeed, RunMode.Free));
                return;
            }

            int shared = RunSeed.FromUrl();
            var save = SaveGame.Read();

            if (shared != 0 && save != null && save.seed != shared) save = null;

            if (save != null)
            {
                StartRun(new GameSession(config, save), resumed: true);
                return;
            }

            StartRun(shared != 0
                ? new GameSession(config, shared, RunMode.Shared)
                : new GameSession(config, RunSeed.Today(), RunMode.Daily));
        }

        void StartRun(GameSession session, bool resumed = false, bool autoBegin = false)
        {
            TeardownUi();

            _session = session;
            _sinceAutosave = 0f;
            _resumedRun = resumed;
            _openSummary = null;
            _openGameOver = null;

            _layoutKind = CurrentLayoutKind();
            BuildUi(null);

            _session.Bus.DayEnded += OnDayEnded;
            _session.Bus.GameOver += OnGameOver;

            if (autoBegin)
            {
                _session.BeginRun();
                return;
            }

            _ui.ShowIntro(BuildIntro(resumed));
        }

        void BuildUi(IReadOnlyList<LogEntry> previousLog)
        {
            _ui = new GameUi(_session, transform, UiLayout.Of(_layoutKind), previousLog);
            _ui.RestartRequested = () => Defer(StartOver);
        }

        LayoutKind CurrentLayoutKind()
        {
            var size = Viewport.CssSize();
            return UiLayout.KindFor(size.x, size.y);
        }

        void SyncLayout()
        {
            var size = Viewport.CssSize();
            bool tooSmall = UiLayout.IsTooSmall(size.x, size.y);

            if (tooSmall != _showingTooSmall)
            {
                _showingTooSmall = tooSmall;
                if (tooSmall)
                {
                    _session.PauseFromBackground();
                    SaveNow();
                    _tooSmall = new TooSmallView(transform);
                }
                else
                {
                    _tooSmall?.Dispose();
                    _tooSmall = null;
                }
            }

            if (_showingTooSmall) return;

            var kind = UiLayout.KindFor(size.x, size.y);
            if (kind == _layoutKind) return;

            _layoutKind = kind;
            RebuildUi();
        }

        void RebuildUi()
        {
            var previousLog = _ui?.LogEntries;
            var carried = previousLog != null ? new List<LogEntry>(previousLog) : null;

            if (_ui != null)
            {
                _ui.Dispose();
                if (_ui.Canvas != null) Destroy(_ui.Canvas.gameObject);
            }

            BuildUi(carried);

            if (_session.Phase == SessionPhase.Intro) _ui.ShowIntro(BuildIntro(_resumedRun));
            else if (_session.Phase == SessionPhase.DayReview && _openSummary.HasValue)
                _ui.ShowDaySummary(_openSummary.Value);
            else if (_session.Phase == SessionPhase.GameOver && _openGameOver.HasValue)
                _ui.ShowGameOver(_openGameOver.Value);
        }

        OverlayView.IntroOptions BuildIntro(bool resumed)
        {
            if (resumed)
            {
                return OverlayView.ResumeIntro(_session.Seed, _session.Mode, _session.Day,
                    () => Defer(StartOver));
            }

            int streak = RunHistory.DailyStreak(RunHistory.Load(), System.DateTime.UtcNow);

            return OverlayView.NewRunIntro(_session.Seed, _session.Mode, _session.BeginRun,
                () => Defer(StartDailyRun), () => Defer(StartFreeRun), streak);
        }

        void StartOver()
        {
            SaveGame.Clear();
            Boot();
        }

        void StartFreeRun()
        {
            SaveGame.Clear();
            StartRun(new GameSession(config, RunSeed.Random(), RunMode.Free), autoBegin: true);
        }

        void StartDailyRun()
        {
            SaveGame.Clear();
            StartRun(new GameSession(config, RunSeed.Today(), RunMode.Daily), autoBegin: true);
        }

        void Autosave()
        {
            if (_session.Phase != SessionPhase.Playing || _session.IsPaused) return;

            _sinceAutosave += Time.unscaledDeltaTime;
            if (_sinceAutosave < AutosaveInterval) return;

            _sinceAutosave = 0f;
            SaveNow();
        }

        void SaveNow()
        {
            var data = _session?.CaptureSave();
            if (data != null) SaveGame.Write(data);
        }

        void OnDayEnded(DaySummary summary)
        {
            _openSummary = summary;
            SaveNow();
        }

        void OnGameOver(GameOverInfo info)
        {
            _openGameOver = info;
            SaveGame.Clear();
        }

        void TeardownUi()
        {
            if (_session != null)
            {
                _session.Bus.DayEnded -= OnDayEnded;
                _session.Bus.GameOver -= OnGameOver;
            }

            _tooSmall?.Dispose();
            _tooSmall = null;
            _showingTooSmall = false;

            if (_ui == null) return;
            _ui.Dispose();
            if (_ui.Canvas != null) Destroy(_ui.Canvas.gameObject);
            _ui = null;
        }

        // se aplaza un frame: la petición viene del click de un botón que se va a destruir
        void Defer(System.Action action)
        {
            StartCoroutine(DeferRoutine(action));
        }

        System.Collections.IEnumerator DeferRoutine(System.Action action)
        {
            yield return null;
            action();
        }

        void EnsureCamera()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = UiTheme.Background;
                return;
            }

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = UiTheme.Background;
            _camera.orthographic = true;
            _camera.cullingMask = 0;

#if UNITY_2023_1_OR_NEWER
            if (FindFirstObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
#else
            if (FindObjectOfType<AudioListener>() == null) go.AddComponent<AudioListener>();
#endif
        }
    }
}
