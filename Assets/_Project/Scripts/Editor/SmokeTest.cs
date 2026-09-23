using System;
using System.Text;
using ServerGame.Core;
using ServerGame.UI;
using UnityEditor;
using UnityEngine.EventSystems;
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
            failures += RunSeedPlumbing(log);
            failures += RunLayoutDecisions(log);
            failures += RunDeterminism(log);
            failures += RunSaveRoundTrip(log);
            failures += RunUiConstruction(log);
            failures += RunActionCoverage(log);

            log.AppendLine();
            log.AppendLine(failures == 0
                ? "===== RESULTADO: CORRECTO (0 fallos) ====="
                : "===== RESULTADO: " + failures + " FALLO(S) =====");

            report = log.ToString();
            return failures;
        }

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

        // jugador automático: mantiene el hardware y compra capacidad
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

        static int RunSeedPlumbing(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("--- Semillas y enlaces ---");

            const string Base = "https://izanvil.github.io/rack-and-ruin/";
            int failures = 0;

            failures += Check(log, RunSeed.SeedIn(Base + "?seed=20260921") == 20260921,
                "No se lee la semilla de un enlace normal.");
            failures += Check(log, RunSeed.SeedIn(Base + "?utm=x&seed=1234") == 1234,
                "No se lee la semilla si va detrás de otro parámetro.");
            failures += Check(log, RunSeed.SeedIn(Base + "?seed=1234#arriba") == 1234,
                "El ancla de la URL se cuela dentro de la semilla.");
            failures += Check(log, RunSeed.SeedIn(Base + "?myseed=1234") == 0,
                "Un parámetro que solo acaba en 'seed=' se confunde con el nuestro.");
            failures += Check(log, RunSeed.SeedIn(Base + "?seed=abc") == 0,
                "Una semilla que no es un número debería ignorarse.");
            failures += Check(log, RunSeed.SeedIn(Base) == 0 && RunSeed.SeedIn(null) == 0,
                "Sin parámetro debería no haber semilla.");
            failures += Check(log, RunSeed.SeedIn(Base + "?seed=-7") == -7,
                "Una semilla negativa es válida y debería leerse.");

            failures += Check(log,
                RunSeed.ShareUrlFrom(Base + "?seed=1111", 2222) == Base + "?seed=2222",
                "El enlace compartido arrastra la semilla anterior.");
            failures += Check(log, RunSeed.ShareUrlFrom(null, 42) == RunSeed.PublicUrl + "?seed=42",
                "Fuera del navegador el enlace debería caer en la dirección pública.");
            failures += Check(log,
                RunSeed.SeedIn(RunSeed.ShareUrlFrom(Base, 20260921)) == 20260921,
                "El enlace que se genera no se puede volver a leer.");

            int daily = RunSeed.ForDate(new DateTime(2026, 9, 21));
            failures += Check(log, daily == 20260921, "La semilla del día no es la fecha: " + daily);
            failures += Check(log, RunSeed.DateLabel(daily) == "21/09/2026",
                "La fecha de la semilla se muestra mal: " + RunSeed.DateLabel(daily));
            failures += Check(log, RunSeed.DateLabel(1234) == "1234",
                "Una semilla que no es una fecha no debería fingir serlo.");

            if (failures == 0) log.AppendLine("  Enlaces con semilla: leídos y generados correctamente.");
            return failures;
        }

        static GameSession Play(GameConfig cfg, int seed, int steps)
        {
            var session = new GameSession(cfg, seed, RunMode.Daily);
            session.BeginRun();

            for (int i = 0; i < steps; i++)
            {
                if (session.Phase == SessionPhase.GameOver) break;
                if (session.Phase == SessionPhase.DayReview)
                {
                    session.StartNextDay();
                    continue;
                }

                AutoPlay(session, cfg);
                session.Tick(Step);
            }

            return session;
        }

        static int RunDeterminism(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("--- Determinismo de la semilla ---");

            var cfg = GameConfig.CreateDefault();
            int failures = 0;

            try
            {
                const int Steps = 6000;
                var a = Play(cfg, 20260921, Steps);
                var b = Play(cfg, 20260921, Steps);

                failures += Check(log, a.Day == b.Day,
                    "Turnos distintos con la misma semilla: " + a.Day + " y " + b.Day);
                failures += Check(log, a.Rack.Count == b.Rack.Count,
                    "Racks de distinto tamaño: " + a.Rack.Count + " y " + b.Rack.Count);
                failures += Check(log, a.Money == b.Money,
                    "Cajas distintas: " + a.Money + " y " + b.Money);
                failures += Check(log, a.Reputation == b.Reputation,
                    "Reputaciones distintas: " + a.Reputation + " y " + b.Reputation);
                failures += Check(log, a.TotalServedRequests == b.TotalServedRequests,
                    "Tráfico atendido distinto: " + a.TotalServedRequests + " y " + b.TotalServedRequests);

                for (int i = 0; i < a.Rack.Count && failures == 0; i++)
                {
                    failures += Check(log, a.Rack[i].State == b.Rack[i].State &&
                                           a.Rack[i].Health == b.Rack[i].Health,
                        a.Rack[i].Name + " terminó en estados distintos.");
                }

                var other = Play(cfg, 20260922, Steps);
                failures += Check(log,
                    other.Money != a.Money || other.TotalServedRequests != a.TotalServedRequests,
                    "Dos semillas distintas dan exactamente la misma partida.");

                if (failures == 0)
                    log.AppendLine("  Misma semilla, misma partida (turno " + a.Day + ", " +
                                   Mathf.RoundToInt(a.Money) + " € de caja).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }

            return failures;
        }

        static int RunSaveRoundTrip(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("--- Guardar y reanudar ---");

            var cfg = GameConfig.CreateDefault();
            int failures = 0;

            try
            {
                var original = Play(cfg, 4242, 3000);

                failures += Check(log, original.Phase == SessionPhase.Playing,
                    "La partida de prueba no llegó a un punto guardable (" + original.Phase + ").");
                if (failures > 0) return failures;

                var captured = original.CaptureSave();
                failures += Check(log, captured != null,
                    "CaptureSave devolvió null con una partida en curso.");
                if (failures > 0) return failures;

                var data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(captured));
                var restored = new GameSession(cfg, data);

                failures += Check(log, restored.Seed == original.Seed, "La semilla no sobrevive al guardado.");
                failures += Check(log, restored.Mode == original.Mode, "El modo de partida no sobrevive al guardado.");
                failures += Check(log, restored.Day == original.Day, "El turno no sobrevive al guardado.");
                failures += Check(log, restored.Rack.Count == original.Rack.Count,
                    "El rack cambia de tamaño al recuperarlo: " + restored.Rack.Count +
                    " en vez de " + original.Rack.Count);
                failures += Check(log, restored.IsPaused,
                    "La partida recuperada debería volver en pausa.");
                failures += Check(log, Near(restored.Money, original.Money, 1f),
                    "La caja no sobrevive al guardado: " + restored.Money + " en vez de " + original.Money);
                failures += Check(log, Near(restored.Reputation, original.Reputation, 0.1f),
                    "La reputación no sobrevive al guardado.");
                failures += Check(log,
                    restored.Upgrades.Level(UpgradeId.NewServer) == original.Upgrades.Level(UpgradeId.NewServer),
                    "Las mejoras no sobreviven al guardado.");

                for (int i = 0; i < restored.Rack.Count && failures == 0; i++)
                {
                    var before = original.Rack[i];
                    var after = restored.Rack[i];
                    failures += Check(log, before.State == after.State && before.Task == after.Task &&
                                           before.Tier == after.Tier && Near(before.Health, after.Health, 0.05f),
                        after.Name + " no vuelve como estaba: " + after.StateLabel() +
                        " frente a " + before.StateLabel());
                }

                if (failures > 0) return failures;

                restored.SetSpeed(1f);
                for (int i = 0; i < 1200; i++)
                {
                    original.Tick(Step);
                    restored.Tick(Step);
                }

                failures += Check(log, original.Day == restored.Day,
                    "Tras reanudar van por turnos distintos: " + original.Day + " y " + restored.Day);
                failures += Check(log, original.Rack.FailedCount == restored.Rack.FailedCount,
                    "Tras reanudar se han averiado máquinas distintas.");
                failures += Check(log, NearRelative(original.Money, restored.Money, 0.02f),
                    "Tras reanudar las cajas divergen: " + original.Money + " y " + restored.Money);
                failures += Check(log,
                    NearRelative(original.TotalServedRequests, restored.TotalServedRequests, 0.02f),
                    "Tras reanudar el tráfico atendido diverge.");

                if (failures == 0)
                    log.AppendLine("  Partida guardada en el turno " + data.day + " con " +
                                   data.servers.Length + " servidores y recuperada sin desviarse.");
            }
            catch (Exception e)
            {
                log.AppendLine("  FALLO al guardar o recuperar la partida: " + e);
                failures++;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }

            return failures;
        }

        static bool Near(float a, float b, float tolerance) => Mathf.Abs(a - b) <= tolerance;

        static bool NearRelative(float a, float b, float tolerance)
        {
            float scale = Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
            return Mathf.Abs(a - b) / scale <= tolerance;
        }

        static int RunLayoutDecisions(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("--- Elección de disposición ---");

            int failures = 0;

            failures += Check(log, UiLayout.KindFor(1600f, 900f) == LayoutKind.Wide,
                "Un escritorio normal debería usar la disposición ancha.");
            failures += Check(log, UiLayout.KindFor(1280f, 800f) == LayoutKind.Wide,
                "Un portátil debería usar la disposición ancha.");
            failures += Check(log, UiLayout.KindFor(390f, 844f) == LayoutKind.Compact,
                "Un móvil en vertical debería usar la disposición compacta.");
            failures += Check(log, UiLayout.KindFor(768f, 1024f) == LayoutKind.Compact,
                "Una tableta en vertical debería usar la disposición compacta.");
            failures += Check(log, UiLayout.KindFor(600f, 900f) == LayoutKind.Compact,
                "Una ventana estrecha y alta debería usar la disposición compacta.");

            failures += Check(log, UiLayout.KindFor(844f, 390f) == LayoutKind.CompactLandscape,
                "Un móvil tumbado debería usar la disposición tumbada.");
            failures += Check(log, UiLayout.KindFor(932f, 430f) == LayoutKind.CompactLandscape,
                "Un móvil grande tumbado debería usar la disposición tumbada.");
            failures += Check(log, UiLayout.KindFor(568f, 320f) == LayoutKind.CompactLandscape,
                "El móvil más pequeño tumbado debería usar la disposición tumbada.");
            failures += Check(log, UiLayout.KindFor(900f, 420f) == LayoutKind.CompactLandscape,
                "Una ventana apaisada y baja debería usar la disposición tumbada.");

            failures += Check(log, UiLayout.KindFor(0f, 0f) == LayoutKind.Wide,
                "Sin medida de ventana debería caerse del lado de la disposición ancha.");

            failures += Check(log, !UiLayout.IsTooSmall(568f, 320f),
                "El móvil más pequeño tumbado debería poder jugar.");
            failures += Check(log, !UiLayout.IsTooSmall(320f, 568f),
                "El móvil más pequeño en vertical debería poder jugar.");
            failures += Check(log, UiLayout.IsTooSmall(280f, 400f),
                "Una ventana de 280 px de ancho no da para jugar.");
            failures += Check(log, UiLayout.IsTooSmall(600f, 200f),
                "Una ventana de 200 px de alto no da para jugar.");

            failures += CheckGridFits(log, UiLayout.CompactLayout, "vertical");
            failures += CheckGridFits(log, UiLayout.LandscapeLayout, "tumbada");

            var landscape = UiLayout.LandscapeLayout;
            float used = landscape.Margin * 2f + landscape.HudTotalHeight + landscape.Gap +
                         landscape.LogHeight + landscape.Gap + landscape.ControlBarHeight;
            float bodyHeight = landscape.Reference.y - used;
            failures += Check(log, bodyHeight >= landscape.CardSize.y,
                "Tumbado no queda alto ni para una fila de tarjetas: " + bodyHeight +
                " frente a " + landscape.CardSize.y + ".");

            if (failures == 0)
                log.AppendLine("  Ventanas clasificadas; las rejillas caben y tumbado quedan " +
                               Mathf.RoundToInt(bodyHeight) + " unidades para el rack.");

            return failures;
        }

        static int CheckGridFits(StringBuilder log, UiLayout layout, string name)
        {
            float needed = layout.CardSize.x * layout.CardColumns +
                           layout.CardSpacing * (layout.CardColumns - 1);
            float available = layout.Reference.x - layout.Margin * 2f - 20f;
            return Check(log, needed <= available,
                "Las tarjetas no caben a lo ancho en la disposición " + name + ": hacen falta " +
                needed + " y hay " + available + ".");
        }

        static int RunUiConstruction(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("--- Construcción de la interfaz ---");

            int failures = 0;
            failures += BuildAndExercise(log, UiLayout.Wide, "ancha");
            failures += BuildAndExercise(log, UiLayout.CompactLayout, "vertical");
            failures += BuildAndExercise(log, UiLayout.LandscapeLayout, "tumbada");
            return failures;
        }

        static int BuildAndExercise(StringBuilder log, UiLayout layout, string name)
        {
            var cfg = GameConfig.CreateDefault();
            var host = new GameObject("SmokeTestUiHost");
            GameUi ui = null;
            int failures = 0;

            try
            {
                var session = new GameSession(cfg, 7);
                ui = new GameUi(session, host.transform, layout);
                log.AppendLine("  Disposición " + name + ": " +
                               CountDescendants(ui.Canvas.transform) + " objetos de UI.");

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

                var events = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
                failures += Check(log, events != null, "No se creó ningún EventSystem.");
                if (events != null)
                {
                    var module = events.GetComponent<BaseInputModule>();
                    failures += Check(log, module != null,
                        "El EventSystem se quedó sin módulo de entrada: la interfaz no respondería.");
                    if (module != null)
                        log.AppendLine("  Módulo de entrada: " + module.GetType().Name + ".");
                }

                ui.OpenUpgradesForCapture();
                ui.Tick();
                ui.CloseUpgradesForCapture();
                ui.OpenSheetForCapture();
                ui.Tick();
                ui.CloseSheet();
                ui.Tick();
            }
            catch (Exception e)
            {
                log.AppendLine("  FALLO al construir o refrescar la interfaz " + name + ": " + e);
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
                unit.Tick(0.5f, cfg, 1f, 0f, 0f, 0f, new Rng(1));
        }

        static void GiveMoney(GameSession session, float amount) => session.Grant(amount);

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
