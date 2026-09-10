using ServerGame.UI;
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

        [Tooltip("Semilla del generador de incidencias. 0 = aleatoria en cada partida.")]
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

        GameSession _session;
        GameUi _ui;
        Camera _camera;

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
#if UNITY_WEBGL && !UNITY_EDITOR
            SgWatchPageVisibility();
#endif
            if (createCameraIfMissing) EnsureCamera();
            StartNewRun();
        }

        void Update()
        {
            if (_session == null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Se consulta antes del Tick: si la pestaña estuvo escondida, la pausa deja
            // Speed a 0 y el Tick de este frame (el que trae el salto de tiempo) no simula.
            if (SgConsumePageWasHidden() != 0) _session.PauseFromBackground();
#endif
            _session.Tick(Mathf.Min(Time.unscaledDeltaTime, MaxFrameDelta));
            _ui.Tick();
        }

        void OnDestroy()
        {
            _ui?.Dispose();
        }

        void StartNewRun()
        {
            TeardownUi();

            int seed = randomSeed != 0 ? randomSeed : System.Environment.TickCount;
            _session = new GameSession(config, seed);

            _ui = new GameUi(_session, transform);
            _ui.RestartRequested = RestartNextFrame;
        }

        void TeardownUi()
        {
            if (_ui == null) return;
            _ui.Dispose();
            if (_ui.Canvas != null) Destroy(_ui.Canvas.gameObject);
            _ui = null;
        }

        // se aplaza un frame: la petición viene del click de un botón que se va a destruir
        void RestartNextFrame()
        {
            StartCoroutine(RestartRoutine());
        }

        System.Collections.IEnumerator RestartRoutine()
        {
            yield return null;
            StartNewRun();
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
