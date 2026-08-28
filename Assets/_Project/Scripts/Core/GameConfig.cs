using UnityEngine;

namespace ServerGame.Core
{
    /// <summary>Todos los números que definen el equilibrio del juego.
    /// Se puede crear un asset (Assets > Create > Server Game > Configuración) y
    /// asignarlo al GameBootstrap para ajustar la dificultad sin tocar código.</summary>
    [CreateAssetMenu(menuName = "Server Game/Configuración", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Partida")]
        [Tooltip("Duración de un turno en segundos de juego.")]
        public float dayLengthSeconds = 110f;
        public int startingMoney = 1200;
        public int startingServers = 5;
        public int maxServers = 14;
        public float startingReputation = 100f;

        [Header("Demanda de tráfico")]
        [Tooltip("Peticiones por segundo en el turno 1.")]
        public float baseDemand = 240f;
        public float demandGrowthPerDay = 45f;
        [Tooltip("Amplitud de la onda lenta de tráfico (0.18 = ±18 %).")]
        public float demandWaveAmplitude = 0.18f;
        public float demandWavePeriod = 26f;
        public float demandNoise = 0.06f;

        [Header("Economía")]
        [Tooltip("Ingreso por cada 1000 peticiones atendidas.")]
        public float revenuePerThousandServed = 58f;
        [Tooltip("Penalización por cada 1000 peticiones rechazadas.")]
        public float penaltyPerThousandDropped = 70f;
        [Tooltip("Coste de operación por servidor al cerrar el turno. Se multiplica por el nivel de la máquina.")]
        public float dailyOperatingCostPerServer = 70f;

        [Header("Reputación")]
        [Tooltip("Puntos de reputación perdidos por segundo si se cae el 100 % del tráfico.")]
        public float reputationLossAtFullDrop = 3.5f;
        public float reputationRecoveryPerSecond = 1.5f;
        public float breachReputationLoss = 14f;

        [Header("Servidores")]
        public float serverBaseCapacity = 120f;
        public float ambientTemperature = 22f;
        [Tooltip("Grados por encima del ambiente que alcanza un servidor al 100 % de carga.")]
        public float maxLoadTemperature = 62f;
        public float heatRatePerSecond = 9f;
        public float coolRatePerSecond = 7f;
        [Tooltip("A partir de esta temperatura el servidor reduce su rendimiento.")]
        public float throttleStartTemp = 76f;
        [Tooltip("Temperatura a la que el rendimiento cae al mínimo.")]
        public float criticalTemp = 94f;
        [Range(0.05f, 1f)] public float minThermalThrottle = 0.28f;
        public float wearPerSecondAtFullLoad = 0.16f;
        [Tooltip("Desgaste extra por cada grado por encima del umbral de throttling.")]
        public float heatWearMultiplier = 0.02f;
        public float memoryLeakPerSecond = 0.0028f;
        public float vulnerabilityPerSecond = 0.45f;
        [Tooltip("Probabilidad por segundo de avería súbita cuando la salud está a 0 %.")]
        public float suddenFailureChanceAtZeroHealth = 0.018f;
        [Tooltip("La avería súbita solo puede ocurrir por debajo de esta salud.")]
        public float suddenFailureHealthThreshold = 20f;

        [Header("Duración de las tareas (s)")]
        public float bootSeconds = 6f;
        public float rebootSeconds = 7f;
        public float repairSeconds = 14f;
        public float patchSeconds = 8f;
        public float tierUpgradeSeconds = 20f;
        public float replaceSeconds = 24f;

        [Header("Costes de acciones")]
        public int coolingBurstCost = 110;
        public float coolingBurstDegrees = 26f;
        [Tooltip("Segundos que tarda un servidor en admitir otra refrigeración forzada. " +
                 "Evita que se convierta en un botón que se pulsa sin parar.")]
        public float coolingCooldownSeconds = 16f;
        public int repairCostPerHealthPoint = 7;
        public int repairMinimumCost = 100;
        public int patchCost = 180;
        public int tierUpgradeBaseCost = 1300;
        public int replaceCost = 1500;

        [Header("Incidencias")]
        public float firstIncidentDelay = 24f;
        public float incidentIntervalBase = 31f;
        [Tooltip("Segundos que se restan al intervalo por cada turno superado.")]
        public float incidentIntervalPerDay = 1.8f;
        public float incidentIntervalMin = 11f;
        [Tooltip("Variación aleatoria del intervalo (0.3 = ±30 %).")]
        [Range(0f, 0.8f)] public float incidentIntervalJitter = 0.3f;

        /// <summary>Configuración por defecto cuando no se ha asignado ningún asset.</summary>
        public static GameConfig CreateDefault()
        {
            var cfg = CreateInstance<GameConfig>();
            cfg.name = "GameConfig (por defecto)";
            cfg.hideFlags = HideFlags.HideAndDontSave;
            return cfg;
        }

        void OnValidate()
        {
            dayLengthSeconds = Mathf.Max(10f, dayLengthSeconds);
            startingServers = Mathf.Clamp(startingServers, 1, Mathf.Max(1, maxServers));
            maxServers = Mathf.Clamp(maxServers, startingServers, 15);
            criticalTemp = Mathf.Max(throttleStartTemp + 1f, criticalTemp);
            serverBaseCapacity = Mathf.Max(1f, serverBaseCapacity);
            demandWavePeriod = Mathf.Max(1f, demandWavePeriod);
            incidentIntervalMin = Mathf.Max(3f, incidentIntervalMin);
        }
    }
}
