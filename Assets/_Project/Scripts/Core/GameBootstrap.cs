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
            if (createCameraIfMissing) EnsureCamera();
            StartNewRun();
        }

        void Update()
        {
            if (_session == null) return;
            _session.Tick(Time.unscaledDeltaTime);
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
