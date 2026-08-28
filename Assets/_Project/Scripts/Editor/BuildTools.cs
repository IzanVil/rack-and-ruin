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

        static bool Build(string outputPath, BuildTarget target)
        {
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

            return summary.result == BuildResult.Succeeded;
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
