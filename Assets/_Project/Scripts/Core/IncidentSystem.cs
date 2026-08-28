using System.Collections.Generic;
using ServerGame.Events;
using UnityEngine;

namespace ServerGame.Core
{
    public enum IncidentId
    {
        TrafficSpike,
        Ddos,
        DiskFailure,
        MemoryLeakBurst,
        CoolingFailure,
        PowerSurge,
        SecurityProbe
    }

    /// <summary>Efecto temporal activo sobre toda la instalación.</summary>
    public sealed class ActiveEffect
    {
        public IncidentId Id;
        public string Label;
        public float Remaining;
        public float DemandMultiplier = 1f;
        public float CoolingMultiplier = 1f;
    }

    /// <summary>Genera las incidencias del turno. La frecuencia y la mezcla de sucesos
    /// se endurecen con cada turno superado.</summary>
    public sealed class IncidentSystem
    {
        readonly List<ActiveEffect> _active = new List<ActiveEffect>();
        readonly System.Random _rng;
        float _nextIn;

        public IncidentSystem(System.Random rng)
        {
            _rng = rng;
        }

        public IReadOnlyList<ActiveEffect> Active => _active;

        public float DemandMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < _active.Count; i++) m *= _active[i].DemandMultiplier;
                return m;
            }
        }

        public float CoolingMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < _active.Count; i++) m *= _active[i].CoolingMultiplier;
                return m;
            }
        }

        public void BeginDay(GameConfig cfg, int day)
        {
            _active.Clear();
            _nextIn = day <= 1 ? cfg.firstIncidentDelay : ScheduleInterval(cfg, day) * 0.7f;
        }

        public void Tick(float dt, GameSession session)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].Remaining -= dt;
                if (_active[i].Remaining <= 0f)
                {
                    session.Bus.Log("Incidencia resuelta: " + _active[i].Label + ".", LogLevel.Success,
                        session.Day, session.DayTime);
                    _active.RemoveAt(i);
                }
            }

            _nextIn -= dt;
            if (_nextIn > 0f) return;

            _nextIn = ScheduleInterval(session.Config, session.Day);
            Trigger(Roll(session.Day), session);
        }

        float ScheduleInterval(GameConfig cfg, int day)
        {
            float baseInterval = Mathf.Max(cfg.incidentIntervalMin,
                cfg.incidentIntervalBase - cfg.incidentIntervalPerDay * (day - 1));
            float jitter = 1f + ((float)_rng.NextDouble() * 2f - 1f) * cfg.incidentIntervalJitter;
            return Mathf.Max(4f, baseInterval * jitter);
        }

        /// <summary>Los primeros turnos son casi solo picos de tráfico; a partir del tercero
        /// entran las averías y los ataques.</summary>
        IncidentId Roll(int day)
        {
            float spike = 30f;
            float disk = day >= 2 ? 14f : 0f;
            float leak = day >= 2 ? 14f : 0f;
            float cooling = day >= 3 ? 12f : 0f;
            float ddos = day >= 5 ? 10f + day : 0f;
            float probe = day >= 4 ? 12f : 0f;
            float surge = day >= 5 ? 9f : 0f;

            float total = spike + disk + leak + cooling + ddos + probe + surge;
            float r = (float)_rng.NextDouble() * total;

            if ((r -= spike) < 0f) return IncidentId.TrafficSpike;
            if ((r -= disk) < 0f) return IncidentId.DiskFailure;
            if ((r -= leak) < 0f) return IncidentId.MemoryLeakBurst;
            if ((r -= cooling) < 0f) return IncidentId.CoolingFailure;
            if ((r -= ddos) < 0f) return IncidentId.Ddos;
            if ((r -= probe) < 0f) return IncidentId.SecurityProbe;
            return IncidentId.PowerSurge;
        }

        public void Trigger(IncidentId id, GameSession session)
        {
            var cfg = session.Config;
            var bus = session.Bus;
            int day = session.Day;
            float t = session.DayTime;

            switch (id)
            {
                case IncidentId.TrafficSpike:
                {
                    float mult = Mathf.Min(1.45f + 0.05f * day, 2.0f);
                    Push(new ActiveEffect
                    {
                        Id = id,
                        Label = "pico de tráfico",
                        Remaining = 18f,
                        DemandMultiplier = mult
                    });
                    bus.Log("Pico de tráfico: la demanda sube un " +
                            Mathf.RoundToInt((mult - 1f) * 100f) + " % durante 18 s.", LogLevel.Warning, day, t);
                    break;
                }

                case IncidentId.Ddos:
                {
                    float raw = Mathf.Min(1.7f + 0.06f * day, 2.6f);
                    float mitigated = 1f + (raw - 1f) * (1f - session.Upgrades.DdosAbsorption);
                    Push(new ActiveEffect
                    {
                        Id = id,
                        Label = "ataque DDoS",
                        Remaining = 15f,
                        DemandMultiplier = mitigated
                    });
                    string extra = session.Upgrades.DdosAbsorption > 0f
                        ? " La mitigación absorbe el " + Mathf.RoundToInt(session.Upgrades.DdosAbsorption * 100f) + " %."
                        : " Sin mitigación contratada.";
                    bus.Log("¡ATAQUE DDoS! Demanda ×" + mitigated.ToString("0.0") + "." + extra,
                        LogLevel.Critical, day, t);
                    break;
                }

                case IncidentId.DiskFailure:
                {
                    var victim = session.Rack.PickRandom(_rng, s => s.IsServing);
                    if (victim == null) { bus.Log("Alerta S.M.A.R.T. ignorada: no hay discos activos.", LogLevel.Info, day, t); break; }
                    victim.Damage(42f, cfg);
                    bus.RaiseServerAlert(victim, LogLevel.Critical);
                    bus.Log("Fallo de disco en " + victim.Name + ": salud del hardware -42.", LogLevel.Critical, day, t);
                    break;
                }

                case IncidentId.MemoryLeakBurst:
                {
                    var victim = session.Rack.PickRandom(_rng, s => s.IsServing);
                    if (victim == null) break;
                    victim.AddMemoryLeak(0.35f);
                    bus.RaiseServerAlert(victim, LogLevel.Warning);
                    bus.Log("Fuga de memoria en " + victim.Name + ". Un reinicio la limpia.", LogLevel.Warning, day, t);
                    break;
                }

                case IncidentId.CoolingFailure:
                {
                    Push(new ActiveEffect
                    {
                        Id = id,
                        Label = "avería de refrigeración",
                        Remaining = 26f,
                        CoolingMultiplier = 0.42f
                    });
                    bus.Log("Avería en la climatización: la disipación cae al 42 % durante 26 s.",
                        LogLevel.Critical, day, t);
                    break;
                }

                case IncidentId.PowerSurge:
                {
                    if (session.Upgrades.HasUps)
                    {
                        bus.Log("Pico de tensión absorbido por el SAI. Sin daños.", LogLevel.Success, day, t);
                        break;
                    }
                    var victim = session.Rack.PickRandom(_rng, s => !s.IsFailed);
                    if (victim == null) break;
                    victim.Fail();
                    bus.RaiseServerAlert(victim, LogLevel.Critical);
                    bus.Log("Pico de tensión: " + victim.Name + " ha quedado inutilizable. Requiere sustitución.",
                        LogLevel.Critical, day, t);
                    break;
                }

                case IncidentId.SecurityProbe:
                {
                    var target = session.Rack.MostVulnerable();
                    if (target == null || target.Vulnerability <= 45f)
                    {
                        bus.Log("Escaneo de puertos bloqueado. Los parches están al día.", LogLevel.Success, day, t);
                        break;
                    }
                    // Se cobra lo que haya en caja: si no llega, la multa no puede
                    // dejar la partida bloqueada con un cargo pendiente invisible.
                    float loss = Mathf.Min(session.Money, Mathf.Max(90f, session.Money * 0.12f));
                    session.Spend(loss, "brecha de seguridad");
                    session.AdjustReputation(-cfg.breachReputationLoss);
                    target.AlertFlash = 1f;
                    bus.RaiseServerAlert(target, LogLevel.Critical);
                    bus.Log("¡BRECHA DE SEGURIDAD en " + target.Name + "! Multa de " +
                            Mathf.RoundToInt(loss) + " € y reputación -" +
                            Mathf.RoundToInt(cfg.breachReputationLoss) + ".", LogLevel.Critical, day, t);
                    break;
                }
            }

            session.Bus.RaiseChanged();
        }

        void Push(ActiveEffect effect)
        {
            // Un mismo tipo de incidencia no se apila: se renueva la duración.
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != effect.Id) continue;
                _active[i].Remaining = Mathf.Max(_active[i].Remaining, effect.Remaining);
                _active[i].DemandMultiplier = effect.DemandMultiplier;
                _active[i].CoolingMultiplier = effect.CoolingMultiplier;
                return;
            }
            _active.Add(effect);
        }
    }
}
