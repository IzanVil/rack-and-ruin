using System;
using System.Text;
using ServerGame.Core;
using ServerGame.UI;
using UnityEditor;
using UnityEngine;

namespace ServerGame.EditorTools
{
    /// <summary>Prueba de humo sin modo Play: ejecuta la simulación completa durante
    /// varios turnos con un jugador automático, comprueba las invariantes del modelo y
    /// además construye la interfaz entera para detectar referencias nulas.
    ///
    /// Desde consola:
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod ServerGame.EditorTools.SmokeTest.RunBatch</summary>
    public static class SmokeTest
    {
        const int DaysToPlay = 14;
        const float Step = 0.05f;

        [MenuItem("Server Game/Ejecutar prueba de humo", false, 40)]
        public static void RunFromMenu()
        {
            int failures = Run(out string report);
            if (failures == 0) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static void RunBatch()
        {
            int failures = Run(out string report);
            Debug.Log(report);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static int Run(out string report)
        {
            var log = new StringBuilder();
            int failures = 0;

            log.AppendLine("===== PRUEBA DE HUMO: UPTIME =====");

            failures += RunSimulation(log);
            failures += RunUiConstruction(log);
            failures += RunActionCoverage(log);

            log.AppendLine();
            log.AppendLine(failures == 0
                ? "===== RESULTADO: CORRECTO (0 fallos) ====="
                : "===== RESULTADO: " + failures + " FALLO(S) =====");

            report = log.ToString();
            return failures;
        }

        // ------------------------------------------------------------------ simulación

        static readonly int[] Seeds = { 20260828, 7, 31337, 4242, 99001 };

        static int RunSimulation(StringBuilder log)
        {
            int failures = 0;
            log.AppendLine();
            log.AppendLine("--- Partidas automáticas (" + Seeds.Length + " semillas, " +
                           DaysToPlay + " turnos como máximo) ---");

            var reached = new int[Seeds.Length];
            for (int s = 0; s < Seeds.Length; s++)
            {
                failures += RunOneGame(Seeds[s], log, out reached[s]);
                if (failures > 0) return failures;
            }

            float average = 0f;
            int worst = int.MaxValue, best = 0;
            for (int i = 0; i < reached.Length; i++)
            {
                average += reached[i];
                if (reached[i] < worst) worst = reached[i];
                if (reached[i] > best) best = reached[i];
            }
            average /= reached.Length;

            log.AppendLine(string.Format(
                "  RESUMEN: turno alcanzado  medio {0:0.0}  peor {1}  mejor {2}", average, worst, best));

            if (average < 6f)
                log.AppendLine("  AVISO: el jugador automático aguanta muy poco. El juego puede ser demasiado duro.");
            else if (average >= DaysToPlay - 0.01f)
                log.AppendLine("  AVISO: el jugador automático no muere nunca. El juego puede ser demasiado fácil.");

            return failures;
        }

        static int RunOneGame(int seed, StringBuilder log, out int daysReached)
        {
            int failures = 0;
            var cfg = GameConfig.CreateDefault();
            var session = new GameSession(cfg, seed);
            session.BeginRun();

            log.AppendLine("  · semilla " + seed);

            int guard = 0;
            int maxIterations = DaysToPlay * Mathf.CeilToInt(cfg.dayLengthSeconds / Step) + 5000;

            while (session.Day <= DaysToPlay && session.Phase != SessionPhase.GameOver && guard++ < maxIterations)
            {
                if (session.Phase == SessionPhase.DayReview)
                {
                    log.AppendLine(string.Format(
                        "    T{0,2}  SLA {1,6:0.00}%  caja {2,7:0}  rep {3,5:0.0}  servidores {4,2}  cap {5,5:0} / dem {6,5:0}",
                        session.Day, session.DaySla * 100f, session.Money, session.Reputation,
                        session.Rack.Count, session.Capacity, session.Demand));

                    if (session.Day >= DaysToPlay) break;
                    session.StartNextDay();
                    continue;
                }

                AutoPlay(session, cfg);
                session.Tick(Step);
                failures += CheckInvariants(session, cfg, log);
                if (failures > 0) break;
            }

            if (guard >= maxIterations)
            {
                log.AppendLine("  FALLO: la partida no avanzó (posible bucle infinito).");
                failures++;
            }

            daysReached = session.Day;
            log.AppendLine("    final: turno " + session.Day + " (" + session.Phase + "), " +
                           Mathf.RoundToInt(session.TotalServedRequests) + " peticiones atendidas");

            UnityEngine.Object.DestroyImmediate(cfg);
            return failures;
        }

        /// <summary>Jugador automático sencillo: mantiene el hardware y compra capacidad.
        /// No pretende jugar bien, solo ejercitar todos los caminos del código.</summary>
        static void AutoPlay(GameSession session, GameConfig cfg)
        {
            // Prioridad 1: capacidad. Sin margen no se puede hacer mantenimiento.
            var newServer = UpgradeState.Find(UpgradeId.NewServer);
            if (session.Capacity < session.Demand * 1.6f && session.CanBuy(newServer))
            {
                session.TryBuyUpgrade(newServer);
                return;
            }

            bool headroom = session.Capacity > session.Demand * 1.15f;
            bool cheapHeadroom = session.Capacity > session.Demand * 1.05f;

            for (int i = 0; i < session.Rack.Count; i++)
            {
                var unit = session.Rack[i];

                if (unit.IsFailed)
                {
                    session.Execute(ServerActionId.Replace, unit);
                    continue;
                }

                if (unit.Temperature > 84f && session.Money > cfg.coolingBurstCost * 4)
                {
                    session.Execute(ServerActionId.Cool, unit);
                    continue;
                }

                if (unit.IsBusy) continue;

                if (cheapHeadroom && unit.MemoryLeak > 0.35f) { session.Execute(ServerActionId.Reboot, unit); break; }
                if (!headroom) continue;
                if (unit.Health < 45f) { session.Execute(ServerActionId.Repair, unit); break; }
                if (unit.Vulnerability > 55f) { session.Execute(ServerActionId.Patch, unit); break; }
            }

            // Con caja de sobra invierte en mejoras, pero guarda siempre un colchón
            // para poder sustituir una máquina averiada.
            if (session.Money < cfg.replaceCost + 2500f) return;
            foreach (var def in UpgradeState.Catalog)
            {
                if (def.Id == UpgradeId.NewServer) continue;
                if (!session.CanBuy(def)) continue;
                session.TryBuyUpgrade(def);
                return;
            }
        }

        static int CheckInvariants(GameSession session, GameConfig cfg, StringBuilder log)
        {
            int failures = 0;

            failures += Check(log, IsFinite(session.Money) && session.Money >= 0f,
                "Caja inválida: " + session.Money);
            failures += Check(log, IsFinite(session.Reputation) && session.Reputation >= 0f && session.Reputation <= 100f,
                "Reputación fuera de rango: " + session.Reputation);
            failures += Check(log, IsFinite(session.Demand) && session.Demand >= 0f,
                "Demanda inválida: " + session.Demand);
            failures += Check(log, IsFinite(session.Served) && session.Served >= -0.01f,
                "Tráfico atendido inválido: " + session.Served);
            failures += Check(log, session.Served <= session.Demand + 0.5f,
                "Se atiende más tráfico del que entra: " + session.Served + " > " + session.Demand);
            failures += Check(log, IsFinite(session.Capacity) && session.Capacity >= 0f,
                "Capacidad inválida: " + session.Capacity);
            failures += Check(log, session.DaySla >= 0f && session.DaySla <= 1.0001f,
                "SLA fuera de rango: " + session.DaySla);

            for (int i = 0; i < session.Rack.Count; i++)
            {
                var unit = session.Rack[i];
                failures += Check(log, IsFinite(unit.Health) && unit.Health >= 0f && unit.Health <= 100f,
                    unit.Name + ": salud fuera de rango (" + unit.Health + ")");
                failures += Check(log, IsFinite(unit.Temperature) && unit.Temperature >= cfg.ambientTemperature - 1f
                                       && unit.Temperature < 200f,
                    unit.Name + ": temperatura fuera de rango (" + unit.Temperature + ")");
                failures += Check(log, unit.MemoryLeak >= 0f && unit.MemoryLeak <= 1f,
                    unit.Name + ": fuga de memoria fuera de rango (" + unit.MemoryLeak + ")");
                failures += Check(log, unit.Vulnerability >= 0f && unit.Vulnerability <= 100f,
                    unit.Name + ": vulnerabilidad fuera de rango (" + unit.Vulnerability + ")");
                failures += Check(log, unit.Load >= -0.01f && unit.Load <= unit.EffectiveCapacity(cfg) + 0.5f,
                    unit.Name + ": carga por encima de su capacidad (" + unit.Load + ")");
                if (failures > 0) break;
            }

            return failures;
        }

        // ------------------------------------------------------------------ interfaz

        static int RunUiConstruction(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("--- Construcción de la interfaz ---");

            var cfg = GameConfig.CreateDefault();
            var host = new GameObject("SmokeTestUiHost");
            GameUi ui = null;
            int failures = 0;

            try
            {
                var session = new GameSession(cfg, 7);
                ui = new GameUi(session, host.transform);
                log.AppendLine("  Lienzo creado: " + CountDescendants(ui.Canvas.transform) + " objetos de UI.");

                session.BeginRun();

                // Varios refrescos en distintos estados para tocar todas las ramas.
                for (int i = 0; i < 10; i++)
                {
                    session.Tick(Step);
                    ui.Tick();
                }

                session.Select(session.Rack[0]);
                session.Execute(ServerActionId.Reboot, session.Rack[0]);
                ui.Tick();

                session.Rack[1].Fail();
                session.SetSpeed(0f);
                ui.Tick();

                log.AppendLine("  Refrescos de interfaz: correctos.");
            }
            catch (Exception e)
            {
                log.AppendLine("  FALLO al construir o refrescar la interfaz: " + e);
                failures++;
            }
            finally
            {
                ui?.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(cfg);
            }

            return failures;
        }

        // ------------------------------------------------------------------ acciones

        static int RunActionCoverage(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("--- Cobertura de acciones y mejoras ---");

            var cfg = GameConfig.CreateDefault();
            var session = new GameSession(cfg, 99);
            session.BeginRun();
            int failures = 0;

            try
            {
                // Todas las mejoras hasta el máximo, con caja infinita.
                foreach (var def in UpgradeState.Catalog)
                {
                    int guard = 0;
                    while (session.Upgrades.NextCost(def, cfg) >= 0 && guard++ < 40)
                    {
                        GiveMoney(session, 100000f);
                        if (!session.TryBuyUpgrade(def))
                        {
                            log.AppendLine("  FALLO: no se pudo comprar " + def.Name);
                            failures++;
                            break;
                        }
                    }
                    log.AppendLine("  " + def.Name.PadRight(28) + " nivel " +
                                   session.Upgrades.Level(def.Id) + " / " +
                                   session.Upgrades.MaxLevelOf(def, cfg));
                }

                // Los servidores recién comprados arrancan con una tarea de "boot":
                // se deja correr la simulación unos segundos para que entren en línea.
                for (int i = 0; i < 120; i++) session.Tick(0.1f);

                // Cada acción sobre un servidor, comprobando que la lista es coherente.
                var unit = session.Rack[0];
                var actions = session.GetActions(unit);
                failures += Check(log, actions.Count == 7,
                    "Se esperaban 7 acciones, hay " + actions.Count);

                foreach (var id in (ServerActionId[])Enum.GetValues(typeof(ServerActionId)))
                {
                    GiveMoney(session, 100000f);
                    var target = FreshOnlineServer(session, cfg);

                    if (target == null)
                    {
                        log.AppendLine("  FALLO: no queda ningún servidor libre para probar " + id);
                        failures++;
                        continue;
                    }

                    if (id == ServerActionId.Replace) target.Fail();
                    if (id == ServerActionId.Repair) target.Damage(50f, cfg);
                    if (id == ServerActionId.Patch) RaiseVulnerability(target, session, cfg);

                    bool ok = session.Execute(id, target);
                    log.AppendLine("  " + id.ToString().PadRight(14) + (ok ? "ejecutada" : "NO DISPONIBLE"));
                    if (!ok) failures++;
                }
            }
            catch (Exception e)
            {
                log.AppendLine("  FALLO durante la cobertura de acciones: " + e);
                failures++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }

            return failures;
        }

        /// <summary>Servidor en línea, sin tareas y sin ampliar: el estado desde el que
        /// cualquier acción debe estar disponible.</summary>
        static ServerUnit FreshOnlineServer(GameSession session, GameConfig cfg)
        {
            for (int i = 0; i < session.Rack.Count; i++)
            {
                var unit = session.Rack[i];
                if (unit.IsBusy || unit.IsFailed) continue;
                if (unit.State != ServerState.Online) continue;
                if (unit.Tier >= ServerUnit.MaxTier) continue;
                return unit;
            }
            return null;
        }

        static void RaiseVulnerability(ServerUnit unit, GameSession session, GameConfig cfg)
        {
            // La vulnerabilidad solo sube con el tiempo: se avanza la simulación del propio
            // servidor sin tocar el resto de la partida.
            for (int i = 0; i < 400; i++)
                unit.Tick(0.5f, cfg, 1f, 0f, 0f, 0f, new System.Random(1));
        }

        static void GiveMoney(GameSession session, float amount) => session.Grant(amount);

        // ------------------------------------------------------------------ utilidades

        static int Check(StringBuilder log, bool condition, string message)
        {
            if (condition) return 0;
            log.AppendLine("  FALLO: " + message);
            return 1;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static int CountDescendants(Transform root)
        {
            int count = 1;
            for (int i = 0; i < root.childCount; i++) count += CountDescendants(root.GetChild(i));
            return count;
        }
    }
}
