using System.Collections.Generic;
using UnityEngine;

namespace ServerGame.Core
{
    /// <summary>El rack: contiene los servidores y reparte el tráfico entre ellos.</summary>
    public sealed class Rack
    {
        readonly List<ServerUnit> _servers = new List<ServerUnit>();
        readonly List<float> _caps = new List<float>();
        readonly List<float> _weights = new List<float>();

        public IReadOnlyList<ServerUnit> Servers => _servers;
        public int Count => _servers.Count;
        public ServerUnit this[int i] => _servers[i];

        public ServerUnit Add(GameConfig cfg)
        {
            var unit = new ServerUnit(_servers.Count, cfg);
            _servers.Add(unit);
            return unit;
        }

        public float TotalEffectiveCapacity(GameConfig cfg)
        {
            float total = 0f;
            for (int i = 0; i < _servers.Count; i++) total += _servers[i].EffectiveCapacity(cfg);
            return total;
        }

        public int OnlineCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _servers.Count; i++) if (_servers[i].IsServing) n++;
                return n;
            }
        }

        public int FailedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _servers.Count; i++) if (_servers[i].IsFailed) n++;
                return n;
            }
        }

        /// <summary>Reparte la demanda entre los servidores en línea y devuelve cuántas
        /// peticiones por segundo se han podido atender realmente.</summary>
        /// <param name="balanceQuality">1 = reparto perfectamente proporcional.
        /// Por debajo de 1 el balanceador comete errores y sobrecarga a unos mientras
        /// deja a otros ociosos, que es lo que corrige la mejora del balanceador.</param>
        public float Distribute(float demand, GameConfig cfg, float balanceQuality, float time)
        {
            _caps.Clear();
            _weights.Clear();

            float totalCapacity = 0f;
            for (int i = 0; i < _servers.Count; i++)
            {
                float cap = _servers[i].EffectiveCapacity(cfg);
                _caps.Add(cap);
                totalCapacity += cap;
            }

            if (totalCapacity <= 0.01f || demand <= 0f)
            {
                for (int i = 0; i < _servers.Count; i++) _servers[i].Load = 0f;
                return 0f;
            }

            float target = Mathf.Min(demand, totalCapacity);
            float imbalance = Mathf.Clamp01(1f - balanceQuality);
            float weightSum = 0f;

            for (int i = 0; i < _servers.Count; i++)
            {
                float w = _caps[i];
                if (w > 0f && imbalance > 0f)
                {
                    // Ruido suave y determinista: el desequilibrio deriva poco a poco
                    // en vez de parpadear cada frame.
                    float noise = Mathf.PerlinNoise(i * 3.17f, time * 0.28f) - 0.5f;
                    w *= Mathf.Max(0.05f, 1f + noise * 2f * imbalance * 0.7f);
                }
                _weights.Add(w);
                weightSum += w;
            }

            for (int i = 0; i < _servers.Count; i++)
                _servers[i].Load = weightSum <= 0f ? 0f : target * (_weights[i] / weightSum);

            // Reparto por llenado: lo que sobra de los servidores al tope se pasa a los
            // que aún tienen holgura. Converge en pocas pasadas porque target <= capacidad total.
            for (int pass = 0; pass < 4; pass++)
            {
                float overflow = 0f;
                float headroom = 0f;

                for (int i = 0; i < _servers.Count; i++)
                {
                    float excess = _servers[i].Load - _caps[i];
                    if (excess > 0f)
                    {
                        overflow += excess;
                        _servers[i].Load = _caps[i];
                    }
                    else
                    {
                        headroom += -excess;
                    }
                }

                if (overflow <= 0.01f || headroom <= 0.01f) break;

                float spill = Mathf.Min(overflow, headroom);
                for (int i = 0; i < _servers.Count; i++)
                {
                    float free = _caps[i] - _servers[i].Load;
                    if (free > 0f) _servers[i].Load += spill * (free / headroom);
                }
            }

            float served = 0f;
            for (int i = 0; i < _servers.Count; i++) served += _servers[i].Load;
            return served;
        }

        /// <summary>Devuelve un servidor al azar que cumpla el filtro, o null.</summary>
        public ServerUnit PickRandom(System.Random rng, System.Func<ServerUnit, bool> filter)
        {
            int matches = 0;
            ServerUnit chosen = null;
            for (int i = 0; i < _servers.Count; i++)
            {
                if (!filter(_servers[i])) continue;
                matches++;
                // Muestreo de reservorio: una sola pasada, sin listas temporales.
                if (rng.Next(matches) == 0) chosen = _servers[i];
            }
            return chosen;
        }

        /// <summary>Servidor con la vulnerabilidad más alta entre los que atienden tráfico.</summary>
        public ServerUnit MostVulnerable()
        {
            ServerUnit worst = null;
            for (int i = 0; i < _servers.Count; i++)
            {
                var s = _servers[i];
                if (!s.IsServing) continue;
                if (worst == null || s.Vulnerability > worst.Vulnerability) worst = s;
            }
            return worst;
        }
    }
}
