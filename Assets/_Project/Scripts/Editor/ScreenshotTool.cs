using System;
using System.IO;
using ServerGame.Core;
using ServerGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ServerGame.EditorTools
{
    /// <summary>Renderiza la interfaz del juego a PNG sin entrar en modo Play. Sirve para
    /// revisar la maquetación y para generar capturas de promoción.
    ///
    ///   Unity -batchmode -quit -projectPath . \
    ///         -executeMethod ServerGame.EditorTools.ScreenshotTool.CaptureBatch \
    ///         -screenshotOutput /ruta/capturas</summary>
    public static class ScreenshotTool
    {
        const int Width = 1600;
        const int Height = 900;

        [MenuItem("Server Game/Capturar pantallas", false, 80)]
        public static void CaptureFromMenu()
        {
            string folder = EditorUtility.SaveFolderPanel("Guardar capturas en", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            if (Capture(folder)) EditorUtility.RevealInFinder(folder);
        }

        public static void CaptureBatch()
        {
            string folder = ReadArgument("-screenshotOutput") ?? "Screenshots";
            bool ok = Capture(folder);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Capture(string folder)
        {
            Directory.CreateDirectory(folder);

            GameConfig cfg = null;
            GameObject host = null;
            GameObject cameraGo = null;
            RenderTexture rt = null;
            GameUi ui = null;

            try
            {
                cfg = GameConfig.CreateDefault();
                var session = new GameSession(cfg, 20260828);

                host = new GameObject("ScreenshotHost");
                ui = new GameUi(session, host.transform);

                // El lienzo pasa a espacio de mundo con el tamaño de referencia exacto,
                // para que la captura reproduzca la maquetación píxel a píxel.
                var canvas = ui.Canvas;
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = new Vector2(Width, Height);
                canvasRect.position = Vector3.zero;
                canvasRect.localScale = Vector3.one;

                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;

                cameraGo = new GameObject("ScreenshotCamera");
                var camera = cameraGo.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.aspect = Width / (float)Height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = UiTheme.Background;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                canvas.worldCamera = camera;

                rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                camera.targetTexture = rt;

                ui.Tick();
                Render(camera, rt, Path.Combine(folder, "01-intro.png"));

                ui.SkipIntro();
                for (int i = 0; i < 900; i++)
                {
                    session.Tick(0.05f);
                    if (session.Phase != SessionPhase.Playing) break;
                }
                if (session.Rack.Count > 2) session.Select(session.Rack[2]);
                ui.Tick();
                Render(camera, rt, Path.Combine(folder, "02-partida.png"));

                ui.OpenUpgradesForCapture();
                ui.Tick();
                Render(camera, rt, Path.Combine(folder, "03-mejoras.png"));

                Debug.Log("Capturas guardadas en " + Path.GetFullPath(folder));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Fallo al capturar: " + e);
                return false;
            }
            finally
            {
                ui?.Dispose();
                if (rt != null) { RenderTexture.active = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (cameraGo != null) UnityEngine.Object.DestroyImmediate(cameraGo);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (cfg != null) UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        static void Render(Camera camera, RenderTexture rt, string path)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
