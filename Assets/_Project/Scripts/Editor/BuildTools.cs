using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ServerGame.EditorTools
{
    /// <summary>Compilación del ejecutable desde línea de comandos.
    ///
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod ServerGame.EditorTools.BuildTools.BuildLinux \
    ///         -buildOutput /ruta/Uptime</summary>
    public static class BuildTools
    {
        [MenuItem("Server Game/Compilar ejecutable (Linux)", false, 60)]
        public static void BuildLinuxFromMenu()
        {
            string path = EditorUtility.SaveFilePanel("Compilar UPTIME", "", "Uptime", "x86_64");
            if (string.IsNullOrEmpty(path)) return;
            Build(path, BuildTarget.StandaloneLinux64);
        }

        public static void BuildLinux()
        {
            string output = ReadArgument("-buildOutput") ?? "Build/Uptime.x86_64";
            bool ok = Build(output, BuildTarget.StandaloneLinux64);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("Server Game/Compilar para web (WebGL)", false, 61)]
        public static void BuildWebFromMenu()
        {
            string path = EditorUtility.SaveFolderPanel("Carpeta de salida WebGL", "", "WebGL");
            if (string.IsNullOrEmpty(path)) return;
            ConfigureWebGL();
            Build(path, BuildTarget.WebGL);
        }

        public static void BuildWeb()
        {
            string output = ReadArgument("-buildOutput") ?? "Build/WebGL";
            ConfigureWebGL();
            bool ok = Build(output, BuildTarget.WebGL);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        // ajustes fijados por código para que la build sea reproducible
        static void ConfigureWebGL()
        {
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

            // Brotli + fallback JS: funciona aunque el hosting no mande Content-Encoding
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;   // fallback JS si falta la cabecera
            PlayerSettings.WebGL.dataCaching = true;
            // Plantilla propia (Assets/WebGLTemplates/Uptime): portada con la identidad
            // del juego, barra de carga y encuadre 16:9 responsive.
            PlayerSettings.WebGL.template = "PROJECT:Uptime";
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;

            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);

            // El juego se diseñó a 1600x900; se mantiene esa proporción por defecto.
            PlayerSettings.defaultWebScreenWidth = 1600;
            PlayerSettings.defaultWebScreenHeight = 900;
        }

        static bool Build(string outputPath, BuildTarget target)
        {
            // Si el módulo de la plataforma no está instalado, BuildPlayer lanza una
            // excepción y devuelve un report vacío que, por defecto, parece "Succeeded".
            // Se comprueba antes para no dar un falso positivo.
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError("Build FALLIDA: el módulo de " + target +
                               " no está instalado en este editor.");
                return false;
            }

            SceneBuilder.CreateMainSceneSilent();

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.MainScenePath },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log("Build " + summary.result + " · " + summary.totalSize / (1024 * 1024) + " MB · " +
                      summary.totalErrors + " errores · " + outputPath);

            // "Succeeded" con errores no es un éxito: exigimos las dos cosas.
            return summary.result == BuildResult.Succeeded && summary.totalErrors == 0;
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
