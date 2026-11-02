# Bajoterra Arena

Shooter 3v3 en tercera persona inspirado en **Bajoterra (Slugterra)**: se disparan babosas con efectos distintos
(daño, control, curación, protección) desde un blaster contra otros jugadores o bots.

Este repositorio contiene el **servidor en Java** (simulación autoritativa, bots, reglas y red) y el **cliente Unity (C#)**
que se conecta por red (carpeta [`unity/`](unity/README.md)).

## Tráiler

[![Tráiler de Bajoterra Arena: haz clic para ver el video completo con sonido](video/trailer-preview.gif)](video/bajoterra-arena-trailer.mp4)

**[▶ Ver el tráiler completo (60 s, con sonido)](video/bajoterra-arena-trailer.mp4)**: movimientos, babosas, 3 contra
3 y disparo fusión, todo grabado del juego real. Cómo se hizo y cómo volver a grabarlo: [`video/README.md`](video/README.md).

## Qué hace el juego

- **3 vs 3** por equipos, jugadores humanos y/o **bots** (los huecos se rellenan solos).
- Movimientos: **saltar, agacharse, rodar, rebote en pared** (y *dash* para Pronto). Agregar uno nuevo es un archivo
  JSON. **Rebote en pared**: en el aire y pegado a un muro, una cobertura alta o el borde de la arena, vuelve a pulsar
  **Espacio** y sales impulsado hacia arriba y lejos del muro; hasta 2 rebotes antes de tocar el suelo y nunca dos
  seguidos en el mismo muro (entre dos muros enfrentados se sube como por una chimenea). Mantener Espacio pulsado
  desde el salto no rebota: hace falta una pulsación nueva. Los bots también rebotan para esquivar o desatascarse.
- **5 babosas por jugador**, cada una con su efecto. Al disparar una queda **bloqueada durante los 2 tiros siguientes**;
  además hay un **tiempo mínimo entre disparos**. Todas las babosas vuelan un 10 % más rápido que en la primera versión.
- Derrota = escudo **y** vida en cero. Se restauran con **objetos** del mapa o con la **babosa de un aliado**
  (curación, escudo, protección).
- **Mapas con obstáculos** que bloquean las babosas (muros, pilares, coberturas bajas que no frenan los tiros en arco).
- **Una arena nueva en cada partida** (`mapgen/ArenaGenerator`): piezas diseñadas (pilares, bloques, cajas, muros,
  colinas y balcones con rampas) reflejadas en las dos mitades para que sea justa, y solo se acepta si se puede
  recorrer entera, los spawns no se ven, hay cobertura en todo el recorrido y ningún pasillo largo queda abierto.
  Las arenas miden de 76×50 a 86×58 m. `--map caverna` juega el mapa hecho a mano (60×40 m).
- Rondas sin reaparición, **mejor de 5**.
- **Grenuke**, la babosa granada: se pega donde choca y explota a los 1,6 s.
- **Phosphoro**, la babosa cegadora: la más lenta; al pasar cerca de un enemigo se queda flotando delante de él (o se
  pega donde cae), avisa a lo grande (brilla cada vez más y zumba cada vez más agudo) y a los 1,5 s estalla en un
  destello que **ciega a todo el que lo esté mirando** en su haz; la única forma de librarse es no verla (darse la
  vuelta o tener una pared en medio). Los cegados ven la pantalla en blanco; los bots cegados se tambalean sin disparar.
- **Frostcrawler**, la babosa de hielo: levanta un **muro de hielo** donde cae para cubrirse; aguanta 2 impactos o se
  derrite a los 8 s.
- **Infurnus** deja un **muro de fuego** en el suelo por donde pasa, que quema a los enemigos que lo crucen.
- **Tormato**, la babosa tornado: donde cae (o sobre quien golpea) se convierte en un **tornado** que avanza unos
  segundos, arrastra a los rivales hacia su ojo, los levanta girando y les hace daño, y **desvía las babosas enemigas**
  que lo atraviesan (sirve de escudo). Quien lo lanzó y sus aliados que entren en su ojo salen **disparados hacia
  arriba** (salto tornado, para subir a balcones y colinas). Si pasa por un muro de fuego se vuelve un **tornado de
  fuego**; si se le dispara una **babosa de hielo** (Frostfang o Frostcrawler, de cualquier equipo) se la traga y se
  vuelve un **tornado congelante** que, además, congela y hace daño de hielo a los rivales de su dueño que gire en su
  ojo. Fuego y hielo se apagan entre sí: manda el último.
- **Bubbaleone**, la babosa burbuja (agua): un impacto directo encierra al rival en una **burbuja** que lo eleva 1,6 m y
  lo deja flotando sin poder moverse ni disparar durante 2,4 s, fuera de su cobertura. Es frágil: el primer golpe que
  reciba después de formarse la **revienta** y cae libre.
- **Lariat**, la babosa lazo (agua): de corto alcance (unos 30 m); un impacto directo le lanza una **cuerda viscosa** que
  **arrastra al rival hasta tu lado** (se queda a unos 2,5 m), saltando las coberturas bajas; mientras tira de él no
  puede andar, pero sí disparar.
- **Arachnet**, la babosa araña: un impacto directo **enreda** al rival (no puede andar, saltar ni rodar durante 1,6 s,
  pero sí disparar; el fuego le hace más daño mientras tanto). Si falla, teje una **telaraña trampa** en el suelo (o al
  pie del muro) que atrapa al primer enemigo que la pisa; cada lanzador mantiene 3 como máximo y el fuego las quema.
- **Disparo fusión** (Q o clic derecho): hacer daño o curar llena la barra de **fusión**; llena, dispara la babosa
  elegida **fusionada** con la siguiente del cinturón: un solo proyectil que aplica los efectos de las dos.
- **Cristal de megamorfosis**: aparece a los 25 s de cada ronda en la línea central; quien lo toma tiene 12 s de
  **babosas gigantes** (más grandes, ×1,35 de daño y disparo más rápido), pero brilla en morado y todos lo ven.
- **Las 18 babosas en 3D**, modeladas en Blender a partir de su arte oficial: vuelan como ellas mismas y aparecen en la
  sala al elegir las 5.
- **Bailes de celebración**: cada héroe sabe tres bailes (saltos con el bláster en alto, paso lateral y giro con pose
  final). Pulsa **B** para bailar (cada pulsación, el siguiente baile); moverte, disparar o usar un movimiento lo para.
  Al ganar una ronda, los supervivientes del equipo ganador lo celebran bailando solos.
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
