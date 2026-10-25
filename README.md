# Bajoterra Arena

Shooter 3v3 en tercera persona inspirado en **Bajoterra (Slugterra)**: se disparan babosas con efectos
distintos (daño, control, curación, protección) desde un blaster, contra jugadores u otros bots.

Este repositorio contiene el **backend en Java** (simulación autoritativa, bots, reglas y red). El cliente
visual será **Unity (C#)** y se conecta por red; no forma parte de esta fase.

- Plan completo: [docs/plan.md](docs/plan.md)
- Arquitectura: [docs/architecture.md](docs/architecture.md)
- Protocolo de red (contrato con Unity): [docs/protocol.md](docs/protocol.md)
- Cómo agregar babosas, efectos, movimientos y personajes: [docs/content-authoring.md](docs/content-authoring.md)

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
mvn verify                                                  # compila (-Xlint -Werror), pruebas, JaCoCo, Checkstyle, SpotBugs
mvn -pl bajoterra-core org.pitest:pitest-maven:mutationCoverage   # pruebas de mutación del core
java -jar bajoterra-bench/target/benchmarks.jar                    # benchmarks JMH (tras mvn package)
```

## Módulos

| Módulo | Rol |
|---|---|
| `bajoterra-core` | Simulación pura y determinista (sin red, sin hilos, sin reloj de pared) |
| `bajoterra-protocol` | Contrato de red servidor Java ↔ cliente Unity |
| `bajoterra-server` | Servidor headless, salas, transportes, runner de partidas con bots |
| `bajoterra-bench` | Benchmarks JMH, soak/carga y comparador de regresiones |
