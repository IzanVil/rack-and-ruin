# UPTIME — Turno de Noche

Simulador de mantenimiento de un centro de datos, hecho en Unity 6 (C#).

Eres el único técnico de guardia. El balanceador reparte el tráfico entre los servidores
del rack; todo lo que no se atiende cuesta dinero y reputación. El tráfico crece cada
turno, el hardware se calienta, se desgasta y se avería, y las incidencias no avisan.
Si la reputación llega a cero, se acabó el contrato.

---

## Cómo jugar

**Esta carpeta ya es un proyecto de Unity funcionando.** Ábrela desde Unity Hub
(*Add project from disk*) con **Unity 6000.0.82f1** y pulsa **Play**.

También hay un ejecutable ya compilado:

```bash
./Build/Uptime.x86_64
```

La escena principal está en `Assets/_Project/Scenes/Main.unity` y ya está en
*Build Settings*. Aun así, el juego arranca solo aunque abras una escena vacía: si al
entrar en modo Play no existe ningún `GameBootstrap`, se crea uno
(`[RuntimeInitializeOnLoadMethod]` en `GameBootstrap.cs`).

### Controles

| Tecla | Acción |
|---|---|
| `Espacio` | Pausa / reanudar |
| `1` `2` `3` | Velocidad ×1, ×2, ×4 |
| `Tab` | Saltar al siguiente servidor con problemas |
| `M` | Abrir/cerrar la tienda de mejoras |
| `Esc` | Cerrar la tienda |
| `R` | Reiniciar el servidor seleccionado |
| `E` | Refrigeración forzada |
| `A` | Reparar hardware |
| `P` | Aplicar parches |
| `N` | Silenciar el sonido |

### Las cuatro amenazas

- **Calor.** Por encima de 76 °C el servidor rinde menos (*throttling*) y se desgasta
  más rápido. Se combate repartiendo mejor la carga, con refrigeración forzada (que
  tiene 16 s de recarga por máquina) o con la mejora de refrigeración líquida.
- **Fugas de memoria.** Recortan hasta un 45 % de la capacidad. Solo se limpian
  reiniciando, y reiniciar deja la máquina 7 s fuera del balanceador.
- **Deuda de parches.** Sube sola. Si un escaneo de seguridad encuentra una máquina por
  encima de 45 puntos, hay brecha: multa y −14 de reputación.
- **Desgaste.** A 0 % de salud la máquina se avería y hay que sustituirla (1.500 €).

### El dilema central

Para arreglar una máquina hay que sacarla del balanceador, y eso reduce la capacidad
justo cuando más falta hace. Mantener un margen de capacidad no es opcional: es lo que
te permite hacer mantenimiento sin que se caiga el servicio.

---

## Herramientas del menú *Server Game*

| Menú | Qué hace |
|---|---|
| **Ejecutar prueba de humo** | Juega 5 partidas automáticas, comprueba las invariantes del modelo, construye la interfaz entera y ejercita todas las acciones y mejoras. Informa de fallos y del equilibrio. |
| **Capturar pantallas** | Renderiza la interfaz a PNG a 1600×900 sin entrar en modo Play. |
| **Compilar ejecutable (Linux)** | Genera la build. |
| **Crear escena principal** | Regenera `Main.unity` y la añade a Build Settings. |
| **Crear asset de configuración** | Crea `Settings/GameConfig.asset` para tocar el equilibrio desde el Inspector. |

Todas funcionan también desde línea de comandos, por ejemplo:

```bash
UNITY=~/Unity/Hub/Editor/6000.0.82f1/Editor/Unity

# Prueba de humo (sale con código 1 si algo falla: sirve para CI)
"$UNITY" -batchmode -nographics -quit -projectPath . \
  -executeMethod ServerGame.EditorTools.SmokeTest.RunBatch -logFile -

# Compilar
"$UNITY" -batchmode -nographics -quit -projectPath . \
  -executeMethod ServerGame.EditorTools.BuildTools.BuildLinux \
  -buildOutput Build/Uptime.x86_64 -logFile -

# Capturas (necesita un servidor gráfico, sin -nographics)
"$UNITY" -batchmode -quit -projectPath . \
  -executeMethod ServerGame.EditorTools.ScreenshotTool.CaptureBatch \
  -screenshotOutput Screenshots -logFile -
```

---

## Arquitectura

```
Assets/_Project/Scripts/
├── Core/          Simulación y reglas. C# puro salvo GameBootstrap.
│   ├── GameBootstrap.cs   MonoBehaviour de entrada. Crea sesión + interfaz.
│   ├── GameSession.cs     Estado de la partida, ciclo de turnos, acciones, economía.
│   ├── GameConfig.cs      ScriptableObject con todos los números de equilibrio.
│   ├── ServerUnit.cs      Modelo de un servidor: térmica, desgaste, tareas.
│   ├── Rack.cs            Balanceo de carga por llenado con varias pasadas.
│   ├── IncidentSystem.cs  Generador de incidencias y efectos temporales.
│   └── Upgrades.cs        Catálogo de mejoras y modificadores derivados.
├── Events/
│   └── GameEvents.cs      Bus de eventos por instancia (no estático) y sus payloads.
├── UI/            Interfaz, construida enteramente por código.
│   ├── GameUi.cs          Monta el lienzo, coordina las vistas y el teclado.
│   ├── Ui.cs              Fábrica de widgets y helpers de anclaje.
│   ├── UiTheme.cs         Paleta y tipografía.
│   ├── HudView.cs         Barra superior y franja de incidencias.
│   ├── RackView.cs        Rejilla de servidores y bahías libres.
│   ├── ServerCardView.cs  Tarjeta de un servidor.
│   ├── InspectorView.cs   Panel de detalle y acciones.
│   ├── LogView.cs         Consola de eventos.
│   ├── UpgradesView.cs    Tienda modal.
│   └── OverlayView.cs     Intro, cierre de turno y fin de partida.
├── Utils/
│   ├── Fmt.cs             Formateo de números y tiempos.
│   ├── TextureFactory.cs  Sprites generados por código (rectángulos redondeados, SDF).
│   └── Sfx.cs             Sonido sintetizado, sin archivos de audio.
└── Editor/
    ├── SmokeTest.cs       Prueba de humo y banco de pruebas de equilibrio.
    ├── SceneBuilder.cs    Creación de la escena y del asset de configuración.
    ├── BuildTools.cs      Compilación del ejecutable.
    └── ScreenshotTool.cs  Capturas renderizadas a PNG.
```

### Decisiones que conviene conocer

- **Cero assets binarios.** Sprites, sonidos y tipografía se generan en tiempo de
  ejecución. No hay prefabs ni texturas que se puedan corromper al reimportar, y la
  carpeta `Assets/_Project` se puede copiar tal cual a otro proyecto.
- **La lógica no sabe nada de la interfaz.** `GameSession.GetActions(unit)` devuelve qué
  acciones existen, cuánto cuestan y por qué están o no disponibles; la interfaz solo las
  pinta. Añadir una acción no obliga a tocar la UI.
- **Bus de eventos por instancia.** Un bus estático dejaría suscriptores colgados entre
  partidas cuando el *domain reload* está desactivado. Los eventos puntuales (log, cierre
  de turno, fin de partida) van por el bus; los valores numéricos se leen por sondeo cada
  frame, que con este número de widgets sale más barato y más simple.
- **Paso de simulación troceado.** `GameSession.Tick` parte el delta en pasos de 50 ms
  como máximo, así la física térmica y el desgaste no dependen de los FPS ni se rompen
  a velocidad ×4.
- **Sin *assembly definitions*.** Todo compila en `Assembly-CSharp`, que referencia
  `UnityEngine.UI` automáticamente en cualquier versión de Unity.
- **El modo de entrada está en «Both»** (`activeInputHandler: 2`), para que convivan los
  atajos del Input Manager clásico y el módulo de UI del nuevo Input System.

---

## Ajustar la dificultad

Menú **Server Game → Crear asset de configuración** genera
`Assets/_Project/Settings/GameConfig.asset`. Asígnalo al campo *Config* del objeto
`[Server Game]` de la escena y toca los valores desde el Inspector.

Los números actuales están validados con la prueba de humo: cinco partidas automáticas
con un jugador que hace mantenimiento razonable aguantan de media **7,6 turnos**
(peor 7, mejor 9), y un jugador que no hace nada muere en el turno 3.

Las palancas con más efecto:

| Campo | Qué hace |
|---|---|
| `demandGrowthPerDay` | Cuánto sube el tráfico cada turno. Es la dificultad principal. |
| `serverBaseCapacity` | Peticiones por segundo de un servidor de nivel 1. |
| `revenuePerThousandServed` | Ritmo al que entra el dinero. |
| `wearPerSecondAtFullLoad` / `heatWearMultiplier` | Cada cuánto hay que reparar. |
| `memoryLeakPerSecond` | Cada cuánto hay que reiniciar. |
| `incidentIntervalBase` | Frecuencia de las incidencias. |
| `coolingCooldownSeconds` | Cuánto se puede abusar de la refrigeración forzada. |

Después de cambiar cualquier cosa, **vuelve a pasar la prueba de humo**: te dirá si el
juego se ha vuelto imposible o trivial.
