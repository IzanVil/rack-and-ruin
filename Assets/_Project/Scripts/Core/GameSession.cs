using System.Collections.Generic;
using ServerGame.Events;
using UnityEngine;

namespace ServerGame.Core
{
    public enum SessionPhase { Intro, Playing, DayReview, GameOver }

    public enum ServerActionId { Reboot, Cool, Repair, Patch, Power, TierUpgrade, Replace }

    /// <summary>Descripción de una acción tal y como debe pintarla la interfaz.
    /// La lógica de disponibilidad vive aquí, no en la UI.</summary>
    public struct ServerActionInfo
    {
        public ServerActionId Id;
        public string Label;
        public string Hint;
        public int Cost;
        public float Duration;
        public bool Enabled;
        public string DisabledReason;
    }

    /// <summary>Estado y reglas de una partida. No hereda de MonoBehaviour: el
    /// GameBootstrap se limita a llamar a Tick() y la interfaz solo lee de aquí.</summary>
    public sealed class GameSession
    {
        public const string BestScoreKey = "ServerGame.BestScore";

        public readonly GameConfig Config;
        public readonly EventBus Bus = new EventBus();
        public readonly Rack Rack = new Rack();
        public readonly UpgradeState Upgrades = new UpgradeState();

        readonly IncidentSystem _incidents;
        readonly System.Random _rng;
        readonly List<ServerActionInfo> _actionBuffer = new List<ServerActionInfo>(8);

        public SessionPhase Phase { get; private set; } = SessionPhase.Intro;
        public float Money { get; private set; }
        public float Reputation { get; private set; }
        public int Day { get; private set; } = 1;
        public float DayTime { get; private set; }
        public float Speed { get; private set; } = 1f;

        /// <summary>Demanda actual en peticiones por segundo, incidencias incluidas.</summary>
        public float Demand { get; private set; }
        public float Served { get; private set; }
        public float Dropped => Mathf.Max(0f, Demand - Served);
        public float Capacity { get; private set; }

        public float DayServedRequests { get; private set; }
        public float DayDroppedRequests { get; private set; }
        public float DayRevenue { get; private set; }
        public float DayPenalties { get; private set; }
        public float DaySpending { get; private set; }
        public float TotalServedRequests { get; private set; }

        public ServerUnit Selected { get; private set; }
        public IncidentSystem Incidents => _incidents;
        public float DayProgress01 => Mathf.Clamp01(DayTime / Config.dayLengthSeconds);
        public float DayTimeRemaining => Mathf.Max(0f, Config.dayLengthSeconds - DayTime);
        public bool IsPaused => Speed <= 0f;

        /// <summary>Porcentaje de peticiones atendidas en el turno en curso.</summary>
        public float DaySla
        {
            get
            {
                float total = DayServedRequests + DayDroppedRequests;
                return total <= 0f ? 1f : DayServedRequests / total;
            }
        }

        public GameSession(GameConfig config, int seed)
        {
            Config = config != null ? config : GameConfig.CreateDefault();
            _rng = new System.Random(seed);
            _incidents = new IncidentSystem(_rng);

            Money = Config.startingMoney;
            Reputation = Config.startingReputation;

            for (int i = 0; i < Config.startingServers; i++) Rack.Add(Config);
            Selected = Rack.Count > 0 ? Rack[0] : null;

            _incidents.BeginDay(Config, Day);
        }

        // ------------------------------------------------------------------ bucle principal

        public void Tick(float deltaTime)
        {
            if (Phase != SessionPhase.Playing || Speed <= 0f) return;

            // Se trocea el paso para que a ×4 la simulación térmica y el desgaste
            // sigan siendo estables y no dependan de los FPS.
            float scaled = deltaTime * Speed;
            const float maxStep = 0.05f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(scaled / maxStep), 1, 16);
            float step = scaled / steps;

            for (int i = 0; i < steps; i++)
            {
                Simulate(step);
                if (Phase != SessionPhase.Playing) return;
            }
        }

        void Simulate(float dt)
        {
            DayTime += dt;
            _incidents.Tick(dt, this);

            Demand = ComputeDemand();

            float cooling = Upgrades.CoolingMultiplier * _incidents.CoolingMultiplier;
            float wear = Upgrades.WearMultiplier;
            float leak = Upgrades.LeakMultiplier;
            float autoPatch = Upgrades.AutoPatchPerSecond;

            for (int i = 0; i < Rack.Count; i++)
            {
                var unit = Rack[i];
                if (unit.Tick(dt, Config, cooling, wear, leak, autoPatch, _rng))
                {
                    Bus.RaiseServerAlert(unit, LogLevel.Critical);
                    Bus.Log(unit.Name + " se ha averiado por desgaste. Hay que sustituirlo.",
                        LogLevel.Critical, Day, DayTime);
                    Bus.RaiseChanged();
                }
            }

            Capacity = Rack.TotalEffectiveCapacity(Config);
            Served = Rack.Distribute(Demand, Config, Upgrades.BalanceQuality, DayTime);

            float servedRequests = Served * dt;
            float droppedRequests = Dropped * dt;

            DayServedRequests += servedRequests;
            DayDroppedRequests += droppedRequests;
            TotalServedRequests += servedRequests;

            float revenue = servedRequests / 1000f * Config.revenuePerThousandServed;
            float penalty = droppedRequests / 1000f * Config.penaltyPerThousandDropped;
            DayRevenue += revenue;
            DayPenalties += penalty;
            Money = Mathf.Max(0f, Money + revenue - penalty);

            UpdateReputation(dt);
            CheckFailStates();

            if (Phase == SessionPhase.Playing && DayTime >= Config.dayLengthSeconds) EndDay();
        }

        float ComputeDemand()
        {
            float baseline = Config.baseDemand + Config.demandGrowthPerDay * (Day - 1);

            // Onda lenta: la carga sube y baja a lo largo del turno.
            float wave = 1f + Config.demandWaveAmplitude *
                Mathf.Sin(DayTime / Mathf.Max(1f, Config.demandWavePeriod) * Mathf.PI * 2f);

            // Rampa de calentamiento al empezar el turno, para que no entre de golpe.
            float warmup = Mathf.Clamp01(DayTime / 6f);
            warmup = 0.55f + 0.45f * warmup;

            float noise = 1f + (Mathf.PerlinNoise(DayTime * 0.7f, Day * 11.3f) - 0.5f) * 2f * Config.demandNoise;

            return Mathf.Max(0f, baseline * wave * warmup * noise * _incidents.DemandMultiplier);
        }

        void UpdateReputation(float dt)
        {
            float dropRatio = Demand <= 0.01f ? 0f : Mathf.Clamp01(Dropped / Demand);
            if (dropRatio > 0.01f)
                AdjustReputation(-Config.reputationLossAtFullDrop * dropRatio * dt);
            else
                AdjustReputation(Config.reputationRecoveryPerSecond * dt);
        }

        void CheckFailStates()
        {
            if (Reputation > 0f)
            {
                bool allDown = Rack.OnlineCount == 0 && Rack.FailedCount == Rack.Count && Rack.Count > 0;
                if (!allDown || Money >= Config.replaceCost) return;
                TriggerGameOver("BANCARROTA TÉCNICA",
                    "Todo el rack está averiado y no queda dinero para sustituir ni una sola máquina.");
                return;
            }

            TriggerGameOver("CONTRATO RESCINDIDO",
                "La reputación llegó a cero. El cliente se ha llevado su tráfico a otro proveedor.");
        }

        // ------------------------------------------------------------------ ciclo de turnos

        void EndDay()
        {
            DayTime = Config.dayLengthSeconds;
            Speed = 0f;
            Phase = SessionPhase.DayReview;

            float sla = DaySla;
            float operating = OperatingCost();
            float bonus = SlaBonus(sla);

            if (Money >= operating)
            {
                Money -= operating;
            }
            else
            {
                // No se puede pagar la operación: en vez de bloquear la partida con dinero
                // negativo, el impago se cobra en reputación.
                Money = 0f;
                AdjustReputation(-10f);
                Bus.Log("No hay caja para cubrir los gastos de operación. Reputación -10.",
                    LogLevel.Critical, Day, DayTime);
            }

            Grant(bonus);
            if (sla >= 0.999f) AdjustReputation(5f);
            else if (sla >= 0.99f) AdjustReputation(2f);

            var summary = new DaySummary(Day, DayServedRequests, DayDroppedRequests, DayRevenue,
                DayPenalties + DaySpending, operating, bonus, sla, Reputation, Money);

            Bus.Log("Fin del turno " + Day + ". SLA " + (sla * 100f).ToString("0.0") + " %.",
                sla >= 0.99f ? LogLevel.Success : LogLevel.Warning, Day, DayTime);

            if (Reputation <= 0f)
            {
                TriggerGameOver("CONTRATO RESCINDIDO",
                    "La reputación llegó a cero al cerrar el turno.");
                return;
            }

            Bus.RaiseDayEnded(summary);
            Bus.RaiseChanged();
        }

        /// <summary>Prima por cumplir el nivel de servicio. Es del orden de medio turno de
        /// ingresos, así que jugar limpio compensa frente a ir apagando fuegos.</summary>
        float SlaBonus(float sla)
        {
            if (sla >= 0.999f) return 300f + Day * 45f;
            if (sla >= 0.99f) return 180f + Day * 25f;
            if (sla >= 0.95f) return 80f;
            return 0f;
        }

        /// <summary>Coste de mantener el rack encendido un turno. Las máquinas ampliadas
        /// consumen más, así que crecer no sale gratis.</summary>
        float OperatingCost()
        {
            float total = 0f;
            for (int i = 0; i < Rack.Count; i++)
                total += Config.dailyOperatingCostPerServer * Rack[i].TierMultiplier;
            return total;
        }

        /// <summary>Arranca el primer turno desde la pantalla de introducción.</summary>
        public void BeginRun()
        {
            if (Phase != SessionPhase.Intro) return;
            Phase = SessionPhase.Playing;
            Speed = 1f;
            Bus.Log("Turno 1. El tráfico empieza a entrar. Buena suerte.", LogLevel.Info, Day, 0f);
            Bus.RaiseChanged();
        }

        public void StartNextDay()
        {
            if (Phase != SessionPhase.DayReview) return;

            Day++;
            DayTime = 0f;
            DayServedRequests = 0f;
            DayDroppedRequests = 0f;
            DayRevenue = 0f;
            DayPenalties = 0f;
            DaySpending = 0f;
            _incidents.BeginDay(Config, Day);

            Phase = SessionPhase.Playing;
            Speed = 1f;

            Bus.Log("Turno " + Day + ". Demanda base: " +
                    Mathf.RoundToInt(Config.baseDemand + Config.demandGrowthPerDay * (Day - 1)) + " req/s.",
                LogLevel.Info, Day, 0f);
            Bus.RaiseChanged();
        }

        void TriggerGameOver(string title, string reason)
        {
            if (Phase == SessionPhase.GameOver) return;

            Phase = SessionPhase.GameOver;
            Speed = 0f;

            int score = Mathf.RoundToInt(Day * 1000f + TotalServedRequests / 100f + Money);
            int best = PlayerPrefs.GetInt(BestScoreKey, 0);
            bool record = score > best;
            if (record)
            {
                best = score;
                PlayerPrefs.SetInt(BestScoreKey, best);
                PlayerPrefs.Save();
            }

            Bus.Log(title + " " + reason, LogLevel.Critical, Day, DayTime);
            Bus.RaiseGameOver(new GameOverInfo(title, reason, Day, TotalServedRequests, Money, score, best, record));
            Bus.RaiseChanged();
        }

        // ------------------------------------------------------------------ control

        public void SetSpeed(float speed)
        {
            if (Phase != SessionPhase.Playing) return;
            Speed = Mathf.Clamp(speed, 0f, 4f);
        }

        public void TogglePause()
        {
            if (Phase != SessionPhase.Playing) return;
            Speed = Speed <= 0f ? 1f : 0f;
        }

        public void Select(ServerUnit unit)
        {
            if (unit == null) return;
            Selected = unit;
        }

        /// <summary>Selecciona el siguiente servidor que necesita atención (tecla Tab).</summary>
        public void SelectNextProblem()
        {
            if (Rack.Count == 0) return;
            int start = Selected != null ? Selected.Index : -1;
            for (int offset = 1; offset <= Rack.Count; offset++)
            {
                var candidate = Rack[(start + offset + Rack.Count) % Rack.Count];
                if (!candidate.NeedsAttention(Config)) continue;
                Selected = candidate;
                return;
            }
            Selected = Rack[(start + 1 + Rack.Count) % Rack.Count];
        }

        // ------------------------------------------------------------------ acciones

        public int RepairCost(ServerUnit unit) =>
            Mathf.Max(Config.repairMinimumCost,
                Mathf.RoundToInt((100f - unit.Health) * Config.repairCostPerHealthPoint));

        public int TierUpgradeCost(ServerUnit unit) => Config.tierUpgradeBaseCost * unit.Tier;

        /// <summary>Rellena y devuelve la lista de acciones para un servidor.
        /// Se reutiliza el mismo buffer en cada llamada para no generar basura por frame.</summary>
        public List<ServerActionInfo> GetActions(ServerUnit unit)
        {
            _actionBuffer.Clear();
            if (unit == null) return _actionBuffer;

            bool busy = unit.IsBusy;
            bool failed = unit.IsFailed;
            string busyReason = "Ocupado: " + unit.StateLabel().ToLowerInvariant();

            Add(ServerActionId.Reboot, "Reiniciar", "Limpia la fuga de memoria y enfría. Sin coste.",
                0, Config.rebootSeconds,
                unit.State == ServerState.Online && !busy,
                failed ? "Averiado" : busy ? busyReason : "Solo si está en línea");

            Add(ServerActionId.Cool, "Refrigerar", "Baja " + Mathf.RoundToInt(Config.coolingBurstDegrees) +
                    " °C al instante. Efecto inmediato.",
                Config.coolingBurstCost, 0f,
                !failed && unit.CoolingCooldown <= 0f && Money >= Config.coolingBurstCost,
                failed ? "Averiado"
                    : unit.CoolingCooldown > 0f ? "Circuito saturado: " + Mathf.CeilToInt(unit.CoolingCooldown) + " s"
                    : "Fondos insuficientes");

            int repairCost = RepairCost(unit);
            Add(ServerActionId.Repair, "Reparar", "Devuelve la salud del hardware al 100 %.",
                repairCost, Config.repairSeconds,
                unit.State == ServerState.Online && !busy && unit.Health < 99.5f && Money >= repairCost,
                failed ? "Averiado: hay que sustituirlo"
                    : busy ? busyReason
                    : unit.Health >= 99.5f ? "El hardware está intacto"
                    : Money < repairCost ? "Fondos insuficientes" : "Solo si está en línea");

            Add(ServerActionId.Patch, "Parchear", "Elimina la deuda de seguridad acumulada.",
                Config.patchCost, Config.patchSeconds,
                unit.State == ServerState.Online && !busy && unit.Vulnerability > 1f && Money >= Config.patchCost,
                failed ? "Averiado"
                    : busy ? busyReason
                    : unit.Vulnerability <= 1f ? "Ya está parcheado"
                    : Money < Config.patchCost ? "Fondos insuficientes" : "Solo si está en línea");

            bool isOff = unit.State == ServerState.Offline;
            Add(ServerActionId.Power, isOff ? "Encender" : "Apagar",
                isOff ? "Vuelve a ponerlo en el balanceador." : "Lo saca del balanceador: no se desgasta y se enfría.",
                0, isOff ? Config.bootSeconds : 0f,
                !failed && !busy,
                failed ? "Averiado" : busyReason);

            int tierCost = TierUpgradeCost(unit);
            Add(ServerActionId.TierUpgrade,
                unit.Tier >= ServerUnit.MaxTier ? "Nivel máximo" : "Ampliar a nivel " + (unit.Tier + 1),
                "+65 % de capacidad base y hardware nuevo.",
                tierCost, Config.tierUpgradeSeconds,
                unit.State == ServerState.Online && !busy && unit.Tier < ServerUnit.MaxTier && Money >= tierCost,
                failed ? "Averiado"
                    : busy ? busyReason
                    : unit.Tier >= ServerUnit.MaxTier ? "Ya está al máximo"
                    : Money < tierCost ? "Fondos insuficientes" : "Solo si está en línea");

            Add(ServerActionId.Replace, "Sustituir", "Instala una máquina nueva en la misma bahía.",
                Config.replaceCost, Config.replaceSeconds,
                failed && !busy && Money >= Config.replaceCost,
                !failed ? "Solo para servidores averiados"
                    : busy ? busyReason : "Fondos insuficientes");

            return _actionBuffer;
        }

        void Add(ServerActionId id, string label, string hint, int cost, float duration,
            bool enabled, string disabledReason)
        {
            _actionBuffer.Add(new ServerActionInfo
            {
                Id = id,
                Label = label,
                Hint = hint,
                Cost = cost,
                Duration = duration,
                Enabled = enabled,
                DisabledReason = enabled ? string.Empty : disabledReason
            });
        }

        public bool Execute(ServerActionId id, ServerUnit unit)
        {
            if (unit == null || Phase != SessionPhase.Playing) return false;

            var actions = GetActions(unit);
            ServerActionInfo info = default;
            bool found = false;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i].Id != id) continue;
                info = actions[i];
                found = true;
                break;
            }

            if (!found || !info.Enabled) return false;
            if (info.Cost > 0 && !Spend(info.Cost, info.Label.ToLowerInvariant() + " " + unit.Name)) return false;

            switch (id)
            {
                case ServerActionId.Reboot:
                    unit.StartTask(TaskKind.Reboot, Config.rebootSeconds);
                    Log(unit.Name + ": reinicio en curso (" + Fmt(Config.rebootSeconds) + ").", LogLevel.Info);
                    break;
                case ServerActionId.Cool:
                    unit.ApplyCoolingBurst(Config);
                    Log(unit.Name + ": refrigeración forzada aplicada.", LogLevel.Success);
                    break;
                case ServerActionId.Repair:
                    unit.StartTask(TaskKind.Repair, Config.repairSeconds);
                    Log(unit.Name + ": reparación de hardware en curso.", LogLevel.Info);
                    break;
                case ServerActionId.Patch:
                    unit.StartTask(TaskKind.Patch, Config.patchSeconds);
                    Log(unit.Name + ": aplicando parches de seguridad.", LogLevel.Info);
                    break;
                case ServerActionId.Power:
                    if (unit.State == ServerState.Offline)
                    {
                        unit.StartTask(TaskKind.Boot, Config.bootSeconds);
                        Log(unit.Name + ": arrancando.", LogLevel.Info);
                    }
                    else
                    {
                        unit.PowerOff();
                        Log(unit.Name + ": apagado y fuera del balanceador.", LogLevel.Warning);
                    }
                    break;
                case ServerActionId.TierUpgrade:
                    unit.StartTask(TaskKind.TierUpgrade, Config.tierUpgradeSeconds);
                    Log(unit.Name + ": ampliación de hardware en curso.", LogLevel.Info);
                    break;
                case ServerActionId.Replace:
                    unit.StartTask(TaskKind.Replace, Config.replaceSeconds);
                    Log(unit.Name + ": sustitución en curso.", LogLevel.Info);
                    break;
            }

            Bus.RaiseChanged();
            return true;
        }

        // ------------------------------------------------------------------ mejoras

        public bool CanBuy(UpgradeDef def)
        {
            int cost = Upgrades.NextCost(def, Config);
            return cost >= 0 && Money >= cost && Phase != SessionPhase.GameOver;
        }

        public bool TryBuyUpgrade(UpgradeDef def)
        {
            if (def == null || !CanBuy(def)) return false;

            int cost = Upgrades.NextCost(def, Config);
            if (!Spend(cost, def.Name)) return false;

            Upgrades.Increment(def.Id);

            if (def.Id == UpgradeId.NewServer)
            {
                var unit = Rack.Add(Config);
                unit.StartTask(TaskKind.Boot, Config.bootSeconds);
                Log("Servidor " + unit.Name + " instalado y arrancando.", LogLevel.Success);
            }
            else
            {
                Log("Mejora contratada: " + def.Name + " (nivel " + Upgrades.Level(def.Id) + ").", LogLevel.Success);
            }

            Bus.RaiseChanged();
            return true;
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>Ingresa dinero en caja (primas, ajustes, herramientas de prueba).</summary>
        public void Grant(float amount)
        {
            if (amount <= 0f) return;
            Money += amount;
        }

        /// <summary>Descuenta dinero si hay saldo. Devuelve false si no llega.</summary>
        public bool Spend(float amount, string concept)
        {
            if (amount <= 0f) return true;
            if (Money < amount) return false;
            Money -= amount;
            DaySpending += amount;
            return true;
        }

        public void AdjustReputation(float delta)
        {
            Reputation = Mathf.Clamp(Reputation + delta, 0f, 100f);
        }

        void Log(string message, LogLevel level) => Bus.Log(message, level, Day, DayTime);

        static string Fmt(float seconds) => Mathf.RoundToInt(seconds) + " s";
    }
}
