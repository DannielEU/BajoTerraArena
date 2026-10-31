# Bajoterra Arena

Shooter 3v3 en tercera persona inspirado en **Bajoterra (Slugterra)**: se disparan babosas con efectos distintos
(daño, control, curación, protección) desde un blaster contra otros jugadores o bots.

Este repositorio contiene el **servidor en Java** (simulación autoritativa, bots, reglas y red) y el **cliente Unity (C#)**
que se conecta por red (carpeta [`unity/`](unity/README.md)).

## Qué hace el juego

- **3 vs 3** por equipos, jugadores humanos y/o **bots** (los huecos se rellenan solos).
- Movimientos: **saltar, agacharse, rodar** (y *dash* para Pronto). Agregar uno nuevo es un archivo JSON.
- **10 babosas por jugador**, cada una con su efecto. Al disparar una queda **bloqueada durante los 3 tiros siguientes**;
  además hay un **tiempo mínimo entre disparos**.
- Derrota = escudo **y** vida en cero. Se restauran con **objetos** del mapa o con la **babosa de un aliado**
  (curación, escudo, protección).
- **Mapas con obstáculos** que bloquean las babosas (muros, pilares, coberturas bajas que no frenan los tiros en arco).
- **Una arena nueva en cada partida** (`mapgen/ArenaGenerator`): piezas diseñadas (pilares, bloques, cajas, muros,
  colinas y balcones con rampas) reflejadas en las dos mitades para que sea justa, y solo se acepta si se puede
  recorrer entera, los spawns no se ven, hay cobertura en todo el recorrido y ningún pasillo largo queda abierto.
  `--map caverna` juega el mapa hecho a mano.
- Rondas sin reaparición, **mejor de 5**.
- **Grenuke**, la babosa granada: se pega donde choca y explota a los 1,6 s.
- **Phosphoro**, la babosa cegadora: la más lenta; al pasar cerca de un enemigo se queda flotando delante de él (o se
  pega donde cae), avisa a lo grande (brilla cada vez más y zumba cada vez más agudo) y a los 1,5 s estalla en un
  destello que **ciega a todo el que lo esté mirando** en su haz; la única forma de librarse es no verla (darse la
  vuelta o tener una pared en medio). Los cegados ven la pantalla en blanco; los bots cegados se tambalean sin disparar.
- **Frostcrawler**, la babosa de hielo: levanta un **muro de hielo** donde cae para cubrirse; aguanta 2 impactos o se
  derrite a los 8 s.
- **Infurnus** deja un **muro de fuego** en el suelo por donde pasa, que quema a los enemigos que lo crucen.
- **Las 16 babosas en 3D**, modeladas en Blender a partir de su arte oficial: vuelan como ellas mismas y aparecen en la
  sala al elegir las 10.
- **Un pequeño charco** decorativo en cada arena, con ondas de verdad: salpica al pisarlo, deja estela, rocía cuando una
  babosa pasa rasante y hace "plop" cuando una cae dentro.
- **Golpe crítico** (×1,5) al acertar en la cabeza y **Coraje** para el equipo que se queda en desventaja numérica.
- **Dificultad adaptativa** contra bots: tras cada ronda, los bots del equipo que gana juegan peor y los del que
  pierde, mejor; la sala recuerda el nivel para la partida siguiente, de modo que las partidas tienden al 50/50 sea
  cual sea el nivel del jugador (`BotDirector`, `BotFiller`).

## Documentación

| Documento | Contenido |
|---|---|
| [docs/plan.md](docs/plan.md) | plan de implementación aprobado (contexto de la serie, diseño, hitos) |
| [docs/architecture.md](docs/architecture.md) | módulos, tick de simulación, patrones de diseño, hilos |
| [docs/protocol.md](docs/protocol.md) | contrato de red con Unity |
| [docs/content-authoring.md](docs/content-authoring.md) | cómo agregar babosas, efectos, movimientos, personajes, objetos y mapas |
| [docs/content-catalog.md](docs/content-catalog.md) | catálogo actual de contenido (generado) |
| [docs/performance.md](docs/performance.md) | presupuesto de rendimiento y números medidos |
| [docs/unity-handoff.md](docs/unity-handoff.md) | estado del cliente Unity y siguientes pasos |
| [unity/README.md](unity/README.md) | cómo abrir y jugar el cliente, su arquitectura y cómo se verifica sin Unity |

## Requisitos

- **JDK 27** (el SDK más reciente; el build lo exige con `maven-enforcer`)
- Maven 3.9+

En Homebrew el JDK es *keg-only*; si `java -version` no muestra 27:

```sh
export JAVA_HOME=/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home
export PATH="$JAVA_HOME/bin:$PATH"
```

## Comandos

```sh
mvn verify                                  # compila (-Xlint -Werror), pruebas, cobertura, Checkstyle, SpotBugs
mvn -Pmutation verify                       # añade pruebas de mutación del combate/munición/efectos (≥ 80 %)

mvn -q -DskipTests package                  # genera bajoterra-server/target/bajoterra-server-0.1.0-SNAPSHOT-all.jar

# 100 partidas bot-vs-bot en espejo: balance, duración y velocidad de simulación
java -cp bajoterra-server/target/bajoterra-server-0.1.0-SNAPSHOT-all.jar \
     com.bajoterra.server.BotMatchRunner --matches 100 --seed 42

# servidor de juego (TCP 7777) para el cliente Unity; por defecto cada partida en una arena generada nueva
# (--map caverna: el mapa fijo), --rules quick juega una sola ronda de 15 s, --seed fija la serie de arenas
java -jar bajoterra-server/target/bajoterra-server-0.1.0-SNAPSHOT-all.jar --port 7777 [--map aleatoria|caverna] [--rules standard|quick] [--seed n]

# catálogo de contenido en Markdown (docs/content-catalog.md)
java -cp bajoterra-server/target/bajoterra-server-0.1.0-SNAPSHOT-all.jar \
     com.bajoterra.server.ContentCatalogPrinter docs/content-catalog.md

# benchmarks JMH + comparación con la línea base (falla si algo empeora más de 25 %)
bajoterra-bench/scripts/run-benchmarks.sh
java -cp bajoterra-bench/target/benchmarks.jar com.bajoterra.bench.SoakRunner 30     # 30 min simulados
java -cp bajoterra-bench/target/benchmarks.jar com.bajoterra.bench.LoadRunner 200     # 200 partidas a la vez
```

## Cliente Unity

```sh
unity/scripts/verify.sh       # Game.cs al día, jar del servidor, 94 pruebas C# (incluida una partida real), compilación de la capa visual
unity/scripts/unity-test.sh   # dentro del editor: 86 pruebas EditMode + PlayMode (partida real contra bots), con capturas opcionales
```

Abrir `unity/BajoTerraClient` con **Unity 6.6 (6000.6.7f1)** y pulsar Play (ver [unity/README.md](unity/README.md)). La
lógica de red, predicción e interpolación está verificada contra el servidor real, y la capa visual se ha ejecutado en
Unity 6.6 (menú, lobby y una partida contra bots sin errores en consola). El proyecto incluye además el MCP de Unity
para manejar el editor desde Claude Code (opcional, ver el README de `unity/`).

## Módulos

| Módulo | Rol |
|---|---|
| `bajoterra-core` | Simulación pura y determinista (sin red, sin hilos, sin reloj de pared) |
| `bajoterra-protocol` | Contrato de red servidor Java ↔ cliente Unity (`game.proto`) |
| `bajoterra-server` | Servidor headless, salas, transportes, runner de partidas con bots |
| `bajoterra-bench` | Benchmarks JMH, soak/carga y comparador de regresiones |
| `unity/` | Cliente Unity (C#) y los proyectos .NET que lo verifican sin el editor |

## Calidad

- Compilación con `-Xlint:all -Werror`; Checkstyle y SpotBugs (0 avisos); reglas de arquitectura con ArchUnit.
- Pruebas unitarias, de propiedades (jqwik), de contrato de contenido, de repetición determinista y de integración por TCP.
- Cobertura mínima exigida: core 85 % líneas / 75 % ramas; mutación ≥ 80 % en combate, munición y efectos.
- Cero asignaciones de memoria por tick en régimen estable (`AllocationRegressionTest`).
