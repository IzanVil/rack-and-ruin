using UnityEngine;

namespace ServerGame.Core
{
    public enum ServerState
    {
        Booting,
        Online,
        Rebooting,
        Maintenance,
        Offline,
        Failed
    }

    public enum TaskKind { None, Boot, Reboot, Repair, Patch, TierUpgrade, Replace }

    public sealed class ServerUnit
    {
        public const int MaxTier = 3;

        public readonly int Index;
        public readonly string Name;

        public int Tier { get; private set; } = 1;
        public ServerState State { get; private set; } = ServerState.Online;
        public float Health { get; private set; } = 100f;
        public float Temperature { get; private set; }
        // 0..1, recorta la capacidad hasta un 60%. Se limpia al reiniciar.
        public float MemoryLeak { get; private set; }
        // 0..100, deuda de parches de seguridad.
        public float Vulnerability { get; private set; }
        public float Load { get; internal set; }
        public float Uptime { get; private set; }
        public float CoolingCooldown { get; private set; }

        public TaskKind Task { get; private set; }
        public float TaskRemaining { get; private set; }
        public float TaskTotal { get; private set; }

        public float AlertFlash { get; internal set; }

        public ServerUnit(int index, GameConfig cfg)
        {
            Index = index;
            Name = "SRV-" + (index + 1).ToString("00");
            Temperature = cfg.ambientTemperature;
        }

        public bool IsServing => State == ServerState.Online;
        public bool IsBusy => Task != TaskKind.None;
        public bool IsFailed => State == ServerState.Failed;
        public float TierMultiplier => 1f + 0.65f * (Tier - 1);
        public float TaskProgress01 => TaskTotal <= 0f ? 0f : 1f - Mathf.Clamp01(TaskRemaining / TaskTotal);

        // Capacidad sin el recorte térmico. El calor se calcula sobre esta y no sobre la
        // efectiva, para no realimentar calor -> menos capacidad -> más calor.
        public float NominalCapacity(GameConfig cfg)
        {
            if (!IsServing) return 0f;
            float health = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(Health / 100f));
            float leak = 1f - Mathf.Clamp01(MemoryLeak) * 0.45f;
            return cfg.serverBaseCapacity * TierMultiplier * health * leak;
        }

        public float ThermalThrottle(GameConfig cfg)
        {
            if (Temperature <= cfg.throttleStartTemp) return 1f;
            float t = Mathf.InverseLerp(cfg.throttleStartTemp, cfg.criticalTemp, Temperature);
            return Mathf.Lerp(1f, cfg.minThermalThrottle, Mathf.Clamp01(t));
        }

        public float EffectiveCapacity(GameConfig cfg) => NominalCapacity(cfg) * ThermalThrottle(cfg);

        public float LoadRatio(GameConfig cfg)
        {
            float nominal = NominalCapacity(cfg);
            return nominal <= 0.01f ? 0f : Mathf.Clamp01(Load / nominal);
        }

        public bool IsOverheating(GameConfig cfg) => Temperature >= cfg.throttleStartTemp;
        public bool NeedsAttention(GameConfig cfg) =>
            IsFailed || Health < 35f || Vulnerability > 60f || MemoryLeak > 0.4f || IsOverheating(cfg);

        // devuelve true si el servidor se ha averiado en este tick
        public bool Tick(float dt, GameConfig cfg, float coolingMultiplier, float wearMultiplier,
            float leakMultiplier, float autoPatchPerSecond, System.Random rng)
        {
            AlertFlash = Mathf.Max(0f, AlertFlash - dt * 1.4f);
            CoolingCooldown = Mathf.Max(0f, CoolingCooldown - dt);

            bool justFailed = false;

            if (Task != TaskKind.None)
            {
                TaskRemaining -= dt;
                if (TaskRemaining <= 0f) CompleteTask(cfg);
            }

            UpdateTemperature(dt, cfg, coolingMultiplier);

            if (IsServing)
            {
                Uptime += dt;
                MemoryLeak = Mathf.Clamp01(MemoryLeak + cfg.memoryLeakPerSecond * leakMultiplier * dt);

                float wear = cfg.wearPerSecondAtFullLoad * LoadRatio(cfg);
                float overheat = Mathf.Max(0f, Temperature - cfg.throttleStartTemp);
                wear += overheat * cfg.heatWearMultiplier;
                Health = Mathf.Max(0f, Health - wear * wearMultiplier * dt);

                if (Health <= 0f)
                {
                    Fail();
                    justFailed = true;
                }
                else if (Health < cfg.suddenFailureHealthThreshold)
                {
                    float t = 1f - Health / Mathf.Max(1f, cfg.suddenFailureHealthThreshold);
                    if (rng.NextDouble() < cfg.suddenFailureChanceAtZeroHealth * t * dt)
                    {
                        Fail();
                        justFailed = true;
                    }
                }
            }

            if (State != ServerState.Failed && State != ServerState.Offline)
            {
                float delta = cfg.vulnerabilityPerSecond - autoPatchPerSecond;
                Vulnerability = Mathf.Clamp(Vulnerability + delta * dt, 0f, 100f);
            }

            if (!IsServing) Load = 0f;
            return justFailed;
        }

        void UpdateTemperature(float dt, GameConfig cfg, float coolingMultiplier)
        {
            float target;
            if (State == ServerState.Online)
                target = cfg.ambientTemperature + cfg.maxLoadTemperature * LoadRatio(cfg);
            else if (State == ServerState.Offline || State == ServerState.Failed)
                target = cfg.ambientTemperature;
            else
                target = cfg.ambientTemperature + 6f;

            if (Temperature < target)
                Temperature = Mathf.MoveTowards(Temperature, target, cfg.heatRatePerSecond * dt);
            else
                Temperature = Mathf.MoveTowards(Temperature, target,
                    cfg.coolRatePerSecond * Mathf.Max(0.15f, coolingMultiplier) * dt);
        }

        public void StartTask(TaskKind kind, float duration)
        {
            Task = kind;
            TaskTotal = Mathf.Max(0.01f, duration);
            TaskRemaining = TaskTotal;

            switch (kind)
            {
                case TaskKind.Boot: State = ServerState.Booting; break;
                case TaskKind.Reboot: State = ServerState.Rebooting; break;
                default: State = ServerState.Maintenance; break;
            }

            Load = 0f;
        }

        void CompleteTask(GameConfig cfg)
        {
            switch (Task)
            {
                case TaskKind.Boot:
                    State = ServerState.Online;
                    Uptime = 0f;
                    break;
                case TaskKind.Reboot:
                    State = ServerState.Online;
                    MemoryLeak = 0f;
                    Uptime = 0f;
                    Temperature = Mathf.Min(Temperature, cfg.ambientTemperature + 6f);
                    break;
                case TaskKind.Repair:
                    State = ServerState.Online;
                    Health = 100f;
                    break;
                case TaskKind.Patch:
                    State = ServerState.Online;
                    Vulnerability = 0f;
                    break;
                case TaskKind.TierUpgrade:
                    State = ServerState.Online;
                    Tier = Mathf.Min(MaxTier, Tier + 1);
                    Health = 100f;
                    break;
                case TaskKind.Replace:
                    State = ServerState.Online;
                    Health = 100f;
                    MemoryLeak = 0f;
                    Vulnerability = 0f;
                    Uptime = 0f;
                    Temperature = cfg.ambientTemperature;
                    break;
            }

            Task = TaskKind.None;
            TaskRemaining = 0f;
            TaskTotal = 0f;
        }

        public void ApplyCoolingBurst(GameConfig cfg)
        {
            Temperature = Mathf.Max(cfg.ambientTemperature, Temperature - cfg.coolingBurstDegrees);
            CoolingCooldown = cfg.coolingCooldownSeconds;
        }

        public void PowerOff()
        {
            if (IsFailed) return;
            CancelTask();
            State = ServerState.Offline;
            Load = 0f;
        }

        public void Damage(float amount, GameConfig cfg)
        {
            Health = Mathf.Max(0f, Health - amount);
            AlertFlash = 1f;
            if (Health <= 0f) Fail();
        }

        public void AddMemoryLeak(float amount)
        {
            MemoryLeak = Mathf.Clamp01(MemoryLeak + amount);
            AlertFlash = 1f;
        }

        public void Fail()
        {
            CancelTask();
            State = ServerState.Failed;
            Health = 0f;
            Load = 0f;
            AlertFlash = 1f;
        }

        void CancelTask()
        {
            Task = TaskKind.None;
            TaskRemaining = 0f;
            TaskTotal = 0f;
        }

        public string StateLabel()
        {
            switch (State)
            {
                case ServerState.Online: return "EN LÍNEA";
                case ServerState.Booting: return "ARRANCANDO";
                case ServerState.Rebooting: return "REINICIANDO";
                case ServerState.Maintenance: return MaintenanceLabel();
                case ServerState.Offline: return "APAGADO";
                default: return "AVERIADO";
            }
        }

        string MaintenanceLabel()
        {
            switch (Task)
            {
                case TaskKind.Repair: return "REPARANDO";
                case TaskKind.Patch: return "PARCHEANDO";
                case TaskKind.TierUpgrade: return "AMPLIANDO";
                case TaskKind.Replace: return "SUSTITUYENDO";
                default: return "MANTENIMIENTO";
            }
        }
    }
}
