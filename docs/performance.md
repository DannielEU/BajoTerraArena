# Rendimiento

Todas las cifras son **medidas**, no estimadas: JDK 27.0.1 (Homebrew), Apple M4 de 10 núcleos, macOS. Los números
absolutos dependen de la máquina; lo que se versiona es la línea base
(`bajoterra-bench/baseline/baseline.json`) para detectar regresiones.

## Presupuesto frente a resultado

El servidor simula a 60 ticks por segundo: **16,67 ms por tick**. El presupuesto del plan era mucho más estricto.

| Métrica | Presupuesto | Medido | Dónde |
|---|---|---|---|
| Tick medio de una partida 3v3 con bots | ≤ 250 µs | **2,5 µs** (0 proyectiles extra) – 3,9 µs (64 extra) | `TickBenchmark` |
| Tick, percentil 99 (soak de 30 min simulados) | ≤ 1 ms | **7,8 µs** (p99,9: 13 µs; máx.: 46 µs) | `SoakRunner`, `PerformanceBudgetTest` |
| Memoria asignada por tick (régimen estable) | ≈ 0 B | **< 0,1 B** (JMH) / ≤ 1 B (prueba) | `-prof gc`, `AllocationRegressionTest` |
| Recolecciones de GC durante 30 min simulados | – | **0** | `SoakRunner` |
| Crecimiento de heap en 30 min | – | **−0,5 MB** (sin crecimiento) | `SoakRunner` |
| Construir + serializar un snapshot | ≤ 50 µs | **0,8 µs + 0,4 µs** | `NetworkBenchmark` |
| Una decisión de bot | ≤ 100 µs | **0,14 µs** | `BotBenchmark` |
| Tamaño de un snapshot 3v3 | – | **841 B de media (máx. 1,3 KB)** ⇒ 16 KiB/s por jugador | medido en una partida |

El tick tiene un margen de **más de 4 000×** frente a su presupuesto de frame (16,67 ms / 3,9 µs).

## Escalado

`LoadRunner` simula 200 partidas 3v3 de bots a la vez y mide los ticks por segundo sostenidos (dividido entre 60 =
partidas que podrían correr en tiempo real a la vez, sin contar la red):

| Hilos | Ticks/s | Partidas simultáneas en tiempo real |
|---|---|---|
| 1 | 278 000 | 4 600 |
| 2 | 578 000 | 9 600 |
| 4 | 1 158 000 | 19 300 |
| 8 | 1 760 000 | 29 300 |

Escala casi linealmente porque cada partida es independiente (sin estado compartido mutable). El cuello de botella
real de un servidor será la red (≈ 16 KiB/s por jugador de salida), no la simulación.

`BotMatchRunner` juega partidas completas a ≈ **39 000× tiempo real sumando los 10 hilos** (≈ 3 900× por hilo: una
partida de ~5 minutos simulados tarda ~75 ms de un solo hilo). En 400 partidas espejo: 50,7 % de victorias para el
equipo A y 49,8 % para quien ocupa el lado 0, 4,2 rondas por partida y 20 % de puntería.

## Micro-benchmarks (JMH, `-prof gc`)

| Benchmark | Resultado |
|---|---|
| Colisión de un proyectil por tick (esfera barrida), 10 / 50 / 200 obstáculos | 4,9 / 5,9 / 9,7 ns |
| Línea de visión, 10 / 50 / 200 obstáculos | 14 / 20 / 41 ns |
| Empujar a un combatiente fuera de obstáculos | 12 / 19 / 33 ns |
| Pipeline de daño (elemento + vulnerabilidad + invulnerabilidad + fuego amigo) | 14 ns |
| Aplicar daño + quemadura | 26 ns |
| Aplicar una explosión en área (daño + empuje) | 64 ns |
| Disparar y rotar la munición (10 babosas) | 3,3 ns |
| Buscar contenido por id (texto) / por índice | 1,8 / 1,1 ns |
| Decodificar un snapshot (cliente Java) | 2,8 µs |

Todas las operaciones del camino caliente asignan **0 bytes** (`gc.alloc.rate.norm ≈ 0`).

## Decisiones tomadas con números

- **Proyectiles: objetos vs. arreglos paralelos (SoA).** 62,8 ns frente a 64,4 ns para 64 proyectiles: diferencia
  del 2,6 % a favor de los objetos. Regla del plan: SoA solo si gana por más del 10 %. **Se mantienen objetos**
  (`Projectile` en un `ProjectilePool`), que son más legibles.
- **Recolector de basura y opciones del JDK 27.** Con cero asignaciones en el tick el GC casi no actúa, y el soak da lo
  mismo con G1, ZGC, Serial o Parallel (media 2,6–2,7 µs, 0 recolecciones). `-XX:+UseCompactObjectHeaders` tampoco
  cambia nada medible (los objetos del bucle caliente están preasignados). **No se adopta ninguna opción especial**;
  la combinación `UseZGC + UseCompactObjectHeaders` ni siquiera arranca en esta compilación del JDK.
- **Trigonometría propia (`Trig`).** `StrictMath.sin/cos` asigna un arreglo al reducir el rango (rompía el
  cero-basura) y `Math.sin/cos` puede variar entre CPUs (rompería la repetición exacta). `Trig` solo usa operaciones
  IEEE exactas y cuesta ~10 % más que `Math` en el tick.
- **Bots a 10 Hz.** Cada bot decide cada 6 ticks; una decisión cuesta 0,14 µs, así que el coste de la IA es
  despreciable frente al tick.

## Cómo reproducir

```sh
mvn -q -DskipTests package
bajoterra-bench/scripts/run-benchmarks.sh                 # JMH + comparación con la línea base (falla si algo empeora > 25 %)
bajoterra-bench/scripts/run-benchmarks.sh --update        # adopta la ejecución actual como nueva línea base
java -cp bajoterra-bench/target/benchmarks.jar com.bajoterra.bench.SoakRunner 30      # histograma de 30 min simulados
java -cp bajoterra-bench/target/benchmarks.jar com.bajoterra.bench.LoadRunner 200      # 200 partidas a la vez
java -XX:+UseZGC -cp bajoterra-bench/target/benchmarks.jar com.bajoterra.bench.SoakRunner 30   # otra opción de JVM
```

La comparación también vigila la memoria: falla si un benchmark que no asignaba empieza a asignar (≥ 1 B/op).
La línea base es de una máquina concreta; en una máquina distinta conviene regenerarla (`--update`) antes de usarla como
puerta. Las pruebas `PerformanceBudgetTest` y `AllocationRegressionTest` sí se ejecutan en cualquier máquina con los
límites del presupuesto, que son holgados a propósito.
