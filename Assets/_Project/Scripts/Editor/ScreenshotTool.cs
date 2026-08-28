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

                // lienzo en espacio de mundo al tamaño de referencia exacto
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
                BuildInterestingState(session, cfg);
                ui.Tick();
                Render(camera, rt, Path.Combine(folder, "02-partida.png"));

                ui.OpenUpgradesForCapture();
                ui.Tick();
                Render(camera, rt, Path.Combine(folder, "03-mejoras.png"));
                ui.CloseUpgradesForCapture();

                // Se deja correr hasta el cierre del turno para capturar el resumen.
                for (int i = 0; i < 40000 && session.Phase == SessionPhase.Playing; i++)
                    session.Tick(0.05f);
                ui.Tick();
                Render(camera, rt, Path.Combine(folder, "04-resumen.png"));

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

        // lleva la partida a un momento con tensión para las capturas
        static void BuildInterestingState(GameSession session, GameConfig cfg)
        {
            session.Grant(12000f);

            var newServer = UpgradeState.Find(UpgradeId.NewServer);
            for (int i = 0; i < 4; i++) session.TryBuyUpgrade(newServer);
            session.TryBuyUpgrade(UpgradeState.Find(UpgradeId.Cooling));
            session.TryBuyUpgrade(UpgradeState.Find(UpgradeId.LoadBalancer));

            // Dos turnos sin mantenimiento: el hardware se calienta y se desgasta.
            int guard = 0;
            while (session.Day < 3 && guard++ < 40000)
            {
                session.Tick(0.05f);
                if (session.Phase == SessionPhase.DayReview) session.StartNextDay();
                else if (session.Phase != SessionPhase.Playing) return;
            }

            for (int i = 0; i < 600 && session.Phase == SessionPhase.Playing; i++)
                session.Tick(0.05f);

            if (session.Rack.Count < 7) return;

            session.Incidents.Trigger(IncidentId.Ddos, session);
            session.Rack[1].Damage(68f, cfg);
            session.Rack[4].Fail();
            session.Execute(ServerActionId.Reboot, session.Rack[5]);
            session.Execute(ServerActionId.Patch, session.Rack[6]);

            for (int i = 0; i < 60 && session.Phase == SessionPhase.Playing; i++)
                session.Tick(0.05f);

            session.Select(session.Rack[1]);
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
