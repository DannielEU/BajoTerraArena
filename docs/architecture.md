# Arquitectura

> Documento vivo. El diseño completo y las decisiones están en [plan.md](plan.md); aquí se mantiene la
> vista actual de lo que ya existe en el código.

## Vista general

```mermaid
flowchart LR
    subgraph Unity["Unity (C#, fase posterior)"]
        R[Render / cámara 3P / HUD]
    end
    subgraph Server["bajoterra-server"]
        T[Transport] --> IB[InputBuffer] --> SIM
        SIM --> SB[SnapshotBuilder] --> T
    end
    subgraph Core["bajoterra-core"]
        SIM[GameSimulation 60 Hz]
    end
    R -- InputFrame --> T
    T -- Snapshot / EventBatch --> R
    P[(bajoterra-protocol<br/>game.proto)] -.contrato.- R
    P -.contrato.- T
```

## Reglas de dependencia (verificadas con ArchUnit)

- `core` no depende de `server` ni de `protocol`.
- `core` nunca lee el reloj de pared (`System.currentTimeMillis` / `nanoTime`): el tiempo es el número de tick.

## Estado por hito

| Hito | Estado |
|---|---|
| M0 Bootstrap | Hecho: JDK 27, POM multimódulo, calidad (Checkstyle, SpotBugs, JaCoCo, PIT, ArchUnit), JMH |
| M1 – M9 | Pendientes (ver [plan.md](plan.md) §8) |
