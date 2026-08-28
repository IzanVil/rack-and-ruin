using System.Collections.Generic;
using UnityEngine;

namespace ServerGame.Core
{
    public enum UpgradeId
    {
        Cooling,
        LoadBalancer,
        Monitoring,
        AutoPatch,
        Ups,
        Redundancy,
        DdosShield,
        NewServer
    }

    public sealed class UpgradeDef
    {
        public UpgradeId Id;
        public string Name;
        public string Description;
        public int[] Costs;              // uno por nivel; null si el coste es dinámico
        public bool Repeatable;          // NewServer: el tope depende de GameConfig.maxServers

        public int MaxLevel => Costs?.Length ?? 0;
    }

    /// <summary>Catálogo de mejoras permanentes y niveles comprados.</summary>
    public sealed class UpgradeState
    {
        public static readonly UpgradeDef[] Catalog =
        {
            new UpgradeDef
            {
                Id = UpgradeId.Cooling, Name = "Refrigeración líquida",
                Description = "Disipa el calor un 45 % más rápido por nivel.",
                Costs = new[] { 900, 1800, 3200 }
            },
            new UpgradeDef
            {
                Id = UpgradeId.LoadBalancer, Name = "Balanceador inteligente",
                Description = "Reparte el tráfico de forma más uniforme y evita puntos calientes.",
                Costs = new[] { 750, 1550, 2900 }
            },
            new UpgradeDef
            {
                Id = UpgradeId.Monitoring, Name = "Monitorización proactiva",
                Description = "Las fugas de memoria crecen un 35 % más despacio por nivel.",
                Costs = new[] { 800, 1700 }
            },
            new UpgradeDef
            {
                Id = UpgradeId.AutoPatch, Name = "Parcheo automático",
                Description = "Corrige vulnerabilidades sin intervención (0,30 pts/s por nivel).",
                Costs = new[] { 1000, 2100 }
            },
            new UpgradeDef
            {
                Id = UpgradeId.Redundancy, Name = "Componentes redundantes",
                Description = "El hardware se desgasta un 22 % más despacio por nivel.",
                Costs = new[] { 1150, 2400 }
            },
            new UpgradeDef
            {
                Id = UpgradeId.DdosShield, Name = "Mitigación DDoS",
                Description = "Absorbe un 30 % del tráfico malicioso por nivel.",
                Costs = new[] { 1300, 2700 }
            },
            new UpgradeDef
            {
                Id = UpgradeId.Ups, Name = "SAI redundante",
                Description = "Inmuniza el rack frente a los picos de tensión.",
                Costs = new[] { 2100 }
            },
            new UpgradeDef
            {
                Id = UpgradeId.NewServer, Name = "Servidor adicional",
                Description = "Añade una máquina nueva al rack. Cada una encarece la siguiente.",
                Repeatable = true
            }
        };

        readonly Dictionary<UpgradeId, int> _levels = new Dictionary<UpgradeId, int>();

        public int Level(UpgradeId id) => _levels.TryGetValue(id, out var lvl) ? lvl : 0;

        public void Increment(UpgradeId id) => _levels[id] = Level(id) + 1;

        public void Reset() => _levels.Clear();

        public static UpgradeDef Find(UpgradeId id)
        {
            for (int i = 0; i < Catalog.Length; i++)
                if (Catalog[i].Id == id) return Catalog[i];
            return null;
        }

        /// <summary>Nivel máximo alcanzable teniendo en cuenta los repetibles.</summary>
        public int MaxLevelOf(UpgradeDef def, GameConfig cfg)
        {
            if (def.Id == UpgradeId.NewServer) return Mathf.Max(0, cfg.maxServers - cfg.startingServers);
            return def.MaxLevel;
        }

        /// <summary>Coste del siguiente nivel, o -1 si ya está al máximo.</summary>
        public int NextCost(UpgradeDef def, GameConfig cfg)
        {
            int lvl = Level(def.Id);
            if (lvl >= MaxLevelOf(def, cfg)) return -1;

            if (def.Id == UpgradeId.NewServer)
                return 1200 + 420 * lvl;

            return def.Costs[lvl];
        }

        // ------------------------------------------------------------- modificadores derivados

        public float CoolingMultiplier => 1f + 0.45f * Level(UpgradeId.Cooling);
        public float BalanceQuality => Mathf.Clamp01(0.50f + 0.17f * Level(UpgradeId.LoadBalancer));
        public float LeakMultiplier => Mathf.Max(0.1f, 1f - 0.35f * Level(UpgradeId.Monitoring));
        public float AutoPatchPerSecond => 0.30f * Level(UpgradeId.AutoPatch);
        public float WearMultiplier => Mathf.Max(0.1f, 1f - 0.22f * Level(UpgradeId.Redundancy));
        public float DdosAbsorption => Mathf.Clamp01(0.30f * Level(UpgradeId.DdosShield));
        public bool HasUps => Level(UpgradeId.Ups) > 0;

        /// <summary>Texto del efecto actual, para mostrarlo en la tienda.</summary>
        public string EffectSummary(UpgradeId id, GameConfig cfg)
        {
            switch (id)
            {
                case UpgradeId.Cooling: return "Refrigeración ×" + CoolingMultiplier.ToString("0.00");
                case UpgradeId.LoadBalancer: return "Precisión del reparto " + Mathf.RoundToInt(BalanceQuality * 100f) + " %";
                case UpgradeId.Monitoring: return "Fugas ×" + LeakMultiplier.ToString("0.00");
                case UpgradeId.AutoPatch: return AutoPatchPerSecond <= 0f ? "Sin efecto" : AutoPatchPerSecond.ToString("0.00") + " pts/s";
                case UpgradeId.Redundancy: return "Desgaste ×" + WearMultiplier.ToString("0.00");
                case UpgradeId.DdosShield: return Mathf.RoundToInt(DdosAbsorption * 100f) + " % absorbido";
                case UpgradeId.Ups: return HasUps ? "Instalado" : "No instalado";
                case UpgradeId.NewServer: return Level(UpgradeId.NewServer) + cfg.startingServers + " / " + cfg.maxServers + " servidores";
                default: return string.Empty;
            }
        }
    }
}
