# Plan de implementación — Bajoterra Arena (shooter 3ª persona 3v3, backend Java)

## 1. Contexto

**De qué trata Bajoterra (Slugterra):** serie animada canadiense (2012–2016). Bajo la Tierra existe un mundo de cavernas habitado por **babosas** (*slugs*), criaturas diminutas con poderes. Se disparan desde **blásters** como munición viva y, al alcanzar cierta velocidad (≈100 mph), se transforman en versiones más fuertes (velocimorph/megamorph). Eli Shane (15 años) hereda el rol de "Shane", guardián de Bajoterra, y forma la **Banda Shane** con Kord (troll mecánico), Trixie (experta en babosas) y Pronto (molenoide rastreador). El villano es el **Dr. Blakk**, que convierte babosas en **Ghouls** con agua oscura. Hay babosas de ataque (Infurnus/Burpy, Hop Rock, Thresher, Tazerling/Joules), de control (Arachnet, Xmitter) y de **soporte** (Doc, que cura; Armashelt, tanque defensivo). Fuentes: [Wikipedia – Slugterra](https://en.wikipedia.org/wiki/Slugterra), [SlugTerra Wiki](https://slugterra.fandom.com/wiki/Slugs), [Common Sense Media](https://www.commonsensemedia.org/es/resenas-tv/bajoterra).

**Qué se construye:** el cerebro del juego en **Java** (simulación autoritativa + bots + reglas + red). El apartado visual en **Unity (C#) queda fuera de esta fase**; Unity será un cliente que envía inputs y dibuja snapshots. Decisiones ya confirmadas con el usuario:
- **Servidor Java autoritativo + cliente Unity por red** (Unity usa C#, así no se duplica lógica).
- **Reglas por defecto:** rondas sin reaparición, **mejor de 5** (gana quien llegue a 3). Las reglas son intercambiables (Strategy).

**Estado del entorno:** `/Users/daniel/Documents/BajoTerra` está vacío (proyecto desde cero, no es repo git). Instalado: JDK 26.0.1, Maven 3.9.16, Gradle 9.6. **El SDK más reciente es JDK 27 (27.0.1, Homebrew `openjdk`)**: en M0 se actualiza (`brew upgrade openjdk`, con permiso del usuario) y todo el proyecto compila con `maven.compiler.release=27` (una sola propiedad en el POM padre para saltar a futuras versiones). Se usa **Maven**. Sin funciones *preview/incubator*: solo características finalizadas del lenguaje (records, sealed, *pattern matching* en `switch`, record patterns, etc.).

## 2. Requisitos → dónde se resuelven

| Requisito del usuario | Componente | Cómo se verifica |
|---|---|---|
| Tercera persona, disparar babosas a jugadores/bots | `GameSimulation`, `FireController`, `ProjectilePool`, protocolo `InputFrame` (aim yaw/pitch + origen de cámara) | `FireFlowTest`, partida end-to-end con cliente headless |
| 3 vs 3 por equipos | `Team`, `Match`, `EliminationRoundsRules`, `BotFiller` (completa con bots) | `MatchFlowTest` (best-of-5, 3v3) |
| Agacharse, rodar, saltar; **agregar movimientos fácil** | `MoveDefinition` + `MoveBehavior` + `MoveRegistry` + `LocomotionStateMachine`; cada personaje tiene su `moveSet` en JSON | `ExtensibilityTest` registra un movimiento nuevo (test-only) sin tocar el core |
| Municiones diversas, cada una con efecto; **agregar babosas/efectos fácil** | `SlugDefinition` (JSON) + `Effect` (Strategy/Composite) + `EffectRegistry` por SPI | `ContentContractTest` (todo contenido carga y simula 1 impacto) + `ExtensibilityTest` |
| 10 babosas por jugador; usada ⇒ bloqueada **3 tiros más** | `Loadout` (10 `SlugSlot`) + contador de tiros por combatiente | `LoadoutCooldownTest` (tabla de casos) + jqwik (invariantes) |
| Tiempo entre disparos | `FireController.nextFireAllowedTick` (blaster × multiplicador de la babosa) | `FireRateTest` |
| Derrota = escudo **y** vida en 0 | `Vitals` + `DamagePipeline` (escudo absorbe primero, desborda a vida) | `VitalsPropertyTest` |
| Restaurar vida/escudo con **objetos** o **disparo aliado** | `Pickup` entities + babosas con `targeting: ALLIES` (Doc, Aquabeek, Armashelt) — ambos reusan los mismos `Effect` | `HealFlowTest`, `PickupTest` |
| Patrones de diseño | §5 | `ArchUnitTest` (reglas de capas/extensión) |
| Calidad y velocidad de ejecución | §9 (calidad) y §10 (rendimiento) | `mvn verify` + suite JMH + soak |
| **Mapa con obstáculos que bloquean las babosas** | `GameMap.obstacles` (AABB con `blocksProjectiles/blocksMovement/blocksLineOfSight/height`), colisión barrida de proyectiles, `onWorldHit` en la babosa, bots con línea de vista y coberturas | `ObstacleBlockingTest` (un proyectil a velocidad máxima nunca atraviesa un muro; cobertura baja bloquea disparos planos pero no arcos) |
| Java con el SDK más reciente | **JDK 27**, `--release 27`, sin *preview* | Spike de compatibilidad de herramientas en M0 |

## 3. Arquitectura general

```
 Unity (C#, fase posterior)            Servidor Java (esta fase)
┌───────────────────────┐   inputs   ┌─────────────────────────────────────────────┐
│ Render, cámara 3P,    │ ─────────▶ │ bajoterra-server: Transport → InputBuffer   │
│ VFX, HUD, predicción  │            │   → GameSimulation (bajoterra-core, 60 Hz)  │
│ local de movimiento   │ ◀───────── │   → SnapshotBuilder (20 Hz) + EventBuffer   │
└───────────────────────┘ snapshots  └─────────────────────────────────────────────┘
        ▲ contrato único: bajoterra-protocol (.proto → Java y C#)
```

**Módulos Maven** (`com.bajoterra`):

| Módulo | Contenido | Dependencias |
|---|---|---|
| `bajoterra-core` | Simulación pura: sin red, sin hilos, sin reloj de pared, determinista. Incluye contenido (JSON) y bots | Jackson (solo carga) |
| `bajoterra-protocol` | `game.proto`, codec, interfaz `Transport` | protobuf-java |
| `bajoterra-server` | `GameServer`, `RoomManager`, `TickScheduler`, `TcpTransport`, `LoopbackTransport`, `BotMatchRunner` (main), `HeadlessClient` | core, protocol |
| `bajoterra-bench` | Benchmarks JMH y soak/load | core, server |

**Tick fijo 60 Hz** (dt = 16,67 ms); snapshots a 20 Hz; Unity interpola ~100 ms. **Orden de sistemas por tick** (todo en `GameSimulation.tick()`):
1. Drenar `CommandQueue` (validar: vivo, no aturdido, secuencia, rate-limit de giro).
2. `BotBrain.think()` (cada 6 ticks = 10 Hz; emite los mismos `Command` que un humano).
3. `LocomotionStateMachine` (salto/agacharse/rodar, `StatModifier`s).
4. `FireController` (intervalo + slot disponible ⇒ spawn de proyectil desde pool).
5. `ProjectilePool.step()` con colisión barrida (esfera vs **obstáculos del mapa** y vs cápsulas de combatientes, rejilla espacial) — evita *tunneling* a ≥45 m/s. Se resuelve el **primer impacto a lo largo del recorrido** (un obstáculo más cercano que un combatiente lo protege).
6. `HitResolver`: si el primer impacto es un obstáculo ⇒ el proyectil termina y dispara `onWorldHit` (p. ej. Hop Rock explota contra el muro); si es un combatiente ⇒ aplica `TargetingRule` (daño solo a enemigos, curas solo a aliados) → `EffectContext` → `Effect.apply`.
7. Tick de `StatusEffect`s (DoT, regen, slow, stun, overshield).
8. `Pickup`s (recoger/reaparecer).
9. Comprobación de derrota + `MatchRules` (fin de ronda/partida).
10. Volcado del `EventBuffer` → snapshot.

**Determinismo:** RNG sembrado, sin `HashMap` en el hot-path, sin tiempo real ⇒ *replay* (semilla + comandos) reproduce la partida bit a bit (base de tests dorados).

## 4. Diseño del dominio

### 4.1 Vida y escudo
`Vitals{hp, maxHp, shield, maxShield}`. Regla: el daño consume **escudo primero**, el sobrante pasa a vida; daño `piercing` ignora un % del escudo. **Derrotado ⇔ shield == 0 && hp == 0**. Curar (`Heal`) y recargar (`RestoreShield`) respetan máximos; `Overshield` es escudo temporal que decae. Todo daño pasa por `DamagePipeline` (Chain of Responsibility): multiplicador elemental → vulnerabilidades por estado (p. ej. Congelado ×1,25 a fuego) → invulnerabilidad (i-frames del rodar) → fuego amigo → `Vitals`.

### 4.2 Babosas y efectos (extensibles)
- **`SlugDefinition`** (inmutable, Flyweight, cargada de JSON): `id, name, element, targeting (ENEMIES | ALLIES | ALLIES_AND_SELF | ANY), projectile{speed, gravity, radius, lifetimeTicks, pierce}, fireDelayMultiplier, reloadShotsOverride?, onHit[], onWorldHit[], onTick[], onExpire[], pierceWorld (por defecto false)`. Por defecto **toda babosa choca y se detiene contra los obstáculos** (también las de curación/protección).
- **`Effect`** (Strategy) = `apply(EffectContext)`. Tipos base: **instantáneos** (`damage, heal, restore_shield, knockback, explosion/AoE`), **de duración** (`burn, slow, freeze, stun, regen, overshield, blind`) vía `AbstractStatusEffect` (Template Method: `onApply/onTick/onExpire`, política de apilado `REFRESH | STACK(n) | REPLACE_IF_STRONGER | IGNORE`) y **compuestos** (`CompositeEffect`, `AreaEffect(radius, falloff, child)`, `ConditionalEffect`, `ChainEffect`).
- **`EffectRegistry`**: `type → EffectFactory`, descubierto por **`ServiceLoader` + `@EffectType("…")`** ⇒ añadir un efecto no edita ninguna clase existente (Open/Closed). `EffectContext` es un objeto reutilizable (cero asignaciones).
- **`ElementChart`**: matriz de multiplicadores en `elements.json` (fuego/agua/tierra/aire/energía + hielo/metal/luz; **Ghoul/agua oscura** queda como contenido posterior).

Ejemplo — **nueva babosa = solo JSON** (`content/slugs/infurnus.json`):
```json
{ "id": "infurnus", "name": "Infurnus", "element": "FIRE", "targeting": "ENEMIES",
  "projectile": { "speed": 45, "gravity": 4, "radius": 0.25, "lifetimeTicks": 120, "pierce": 0 },
  "fireDelayMultiplier": 1.0,
  "onHit": [ { "type": "damage", "amount": 18 },
             { "type": "burn", "dps": 4, "durationTicks": 180, "stacking": "REFRESH" } ] }
```
**Nuevo efecto = 1 clase** (+ JSON que lo referencie):
```java
@EffectType("poison")
public final class PoisonEffect extends AbstractStatusEffect {
    public static PoisonEffect fromSpec(EffectSpec s) { /* lee dps, durationTicks */ }
    @Override protected void onTick(EffectContext c) { c.damage(dpsPerTick, DamageType.POISON); }
}
```

**Catálogo inicial (≈12; nombres de la serie, números = *placeholders* de balance en JSON, verificar contra la wiki en M3):**

| id | Rol | Efecto |
|---|---|---|
| `infurnus` (Burpy) | Daño | 18 daño + quemadura 4/s·3 s |
| `flaringo` | Daño rápido | 14 daño, `fireDelayMultiplier` 0,6 |
| `hop_rock` | Área | explosión r=3 m, 25 daño con caída + knockback |
| `thresher` | Perforante | 22 daño, atraviesa 1 objetivo, ignora 50 % escudo |
| `rammstone` | Pesado | 35 daño, proyectil lento, delay ×1,5 |
| `tazerling` (Joules) | Control | 12 daño + aturdimiento 0,8 s + cadena a enemigo cercano |
| `arachnet` | Control | red: lentitud 60 % 3 s |
| `frostfang`/hielo | Control | congela 1,5 s + vulnerabilidad a fuego |
| `tormato` | Reposición | knockback fuerte + 10 daño |
| `doc` | **Curación** (aliado) | cura 35 HP |
| `aquabeek` | **Escudo** (aliado) | restaura 30 escudo + limpia quemadura |
| `armashelt` | **Protección** (aliado) | overshield 40 durante 8 s |

(`boon_doc` = regeneración 8/s·5 s, `phosphoro` = ceguera, como contenido extra.) Proyectiles con `targeting: ALLIES` **atraviesan** enemigos y solo colisionan con aliados/mundo.

### 4.3 Munición: 10 babosas, bloqueo por 3 tiros, intervalo entre disparos
- `Loadout` = 10 `SlugSlot{slugId, firedAtShot}`; `LoadoutValidator` (tamaño == 10, máx. repetidas configurable, `loadoutSize > reloadShots` para evitar bloqueo total).
- Cada combatiente lleva `shotCounter` (se incrementa por disparo). Al disparar el slot en el tiro **n**: `firedAtShot = n`; el slot vuelve a estar disponible cuando `shotCounter >= n + reloadShots` (reloadShots = 3 ⇒ los tiros n+1, n+2, n+3 usan otras babosas y la bloqueada sirve desde el n+4). **Supuesto explícito** — parámetro `reloadShots` en configuración. Con 10 slots nunca hay más de 3 bloqueadas.
- La babosa disparada **no se consume** (se recicla por el ciclo de bloqueo): siempre hay 10.
- `FireController`: dispara solo si `tick >= nextFireAllowedTick` (intervalo base del blaster ≈ 0,35 s × `fireDelayMultiplier` de la babosa), slot disponible, vivo, sin stun y estado de movimiento que lo permita. `SlotSelectionPolicy` (Strategy): `Manual` (slot elegido / siguiente-anterior saltando bloqueados) y auto-avance al siguiente disponible tras disparar.
- El snapshot incluye por slot `available/shotsUntilReady` para el HUD de Unity.

### 4.4 Movimientos (fáciles de ampliar)
- `MoveDefinition` (JSON): `id, durationTicks, cooldownTicks, allowedFrom[], blocksFiring, tags[], modifiers[]` (p. ej. `speedMultiplier`, `hitboxHeightMultiplier`, `invulnerable` en una ventana) + `MoveBehavior` opcional (código) con `onStart/onTick/onEnd` para impulsos (salto, esquive).
- `LocomotionStateMachine` (State pattern, tabla de transiciones **desde datos**): `Idle/Walk → Crouch | Roll | Airborne`. Movimiento y efectos de estado comparten el mismo pipeline de `StatModifier`s (agacharse = hitbox ×0,6 y velocidad ×0,5; rodar = i-frames + impulso).
- MVP: **Jump, Crouch, Roll**. Cada `CharacterDefinition` lista su `moveSet`. Demostración de extensibilidad: **Dash** para Pronto (solo JSON + 1 clase corta). Posteriores: Slide, DoubleJump, deflexión "Slug Fu" (Junjie).
- Protocolo genérico: el input lleva `useMoveId` ⇒ **agregar un movimiento no cambia el protocolo**.

### 4.5 Personajes (datos)
`CharacterDefinition{id, maxHp, maxShield, speed, hitbox, moveSet[], blaster{fireIntervalTicks, reloadShots}, passive?}`. Roster MVP: **Eli** (equilibrado), **Kord** (tanque, lento), **Trixie** (soporte: +25 % potencia de curas), **Pronto** (rápido, hitbox pequeña, Dash).

### 4.6 Objetos (pickups)
`PickupDefinition{effect: EffectSpec, respawnTicks}` (botiquín, celda de escudo, overshield). Reutilizan los mismos `Effect` que las babosas ⇒ curar por objeto y por aliado es el mismo código.

### 4.7 Reglas de partida
`MatchRules` (Strategy) + `MatchStateMachine` (State): `Lobby → Countdown → RoundActive → RoundEnd → MatchEnd`. Por defecto `EliminationRoundsRules`: gana la ronda el equipo que elimina a los 3 rivales; derrotados esperan a la siguiente ronda; tiempo agotado ⇒ gana el equipo con más (HP+escudo) restante. Mejor de 5. `TeamDeathmatchRespawnRules` queda como **extensión opcional** (no implementada salvo que se pida).

### 4.8 Bots
`BotBrain implements InputSource` (el humano usa `NetworkInputSource`; el servidor los trata igual). **Behavior Tree** (Composite) con *blackboard*: percepción (línea de vista por raycast contra obstáculos con `blocksLineOfSight`, eventos de disparo; no dispara sin línea de tiro libre), elección de objetivo (Strategy: más cercano / menor vida / mayor amenaza), **selección de babosa por utilidad** (`SlugUtilityScorer`: aliado < 40 % ⇒ curar; enemigo congelado ⇒ daño; grupo ⇒ área), movimiento por grafo de navegación (A\*), uso de cobertura (puntos de cobertura precalculados desde los obstáculos; agacharse tras obstáculos bajos, asomarse para disparar), esquive (rodar ante proyectil entrante). `BotProfile` (reacción, error de puntería, intervalo de decisión) define dificultad. `BotFiller` completa equipos a 3. Piensan a 10 Hz para ahorrar CPU.

### 4.9 Mundo
`GameMap` en JSON: suelo, **obstáculos**, spawns por equipo, puntos de pickup, grafo de navegación. Contrato para que Unity exporte los colliders de la escena en una fase posterior.

**Obstáculos (bloquean babosas):** cada `Obstacle` es un AABB (extensible a otras formas luego) con propiedades en datos:
`{ id, min, max, blocksProjectiles: true, blocksMovement: true, blocksLineOfSight: true }`.
- **Muro/pilar alto:** bloquea movimiento, proyectiles y línea de vista.
- **Cobertura baja** (altura ≈1,0 m): bloquea proyectiles que pasan por debajo de su altura; un combatiente **agachado** (hitbox ≈1,0 m) queda protegido de disparos planos, pero las babosas con `gravity` alto (en arco) pueden pasar por encima ⇒ el agacharse y la gravedad de cada babosa tienen valor táctico.
- **Barrera transparente** (opcional): bloquea movimiento pero no línea de vista; o a la inversa. Se logra solo combinando banderas.
- **Obstáculo destructible** (extensión posterior): `hp` + `onDestroyed`.
- Validación de mapa (`ContentValidator`): spawns/pickups no dentro de obstáculos, grafo de navegación sin nodos dentro de sólidos, cada equipo con ≥3 spawns y camino de A\* entre ambos lados.
- Colisión: `SpatialGrid` de obstáculos; el barrido de esfera devuelve el **tiempo de impacto más temprano** entre obstáculos y combatientes. Los combatientes también colisionan con obstáculos con `blocksMovement`.

## 5. Patrones de diseño (dónde y por qué)

| Patrón | Uso |
|---|---|
| **Strategy** | `Effect`, `TargetingRule`, `DamageModifier`, `MatchRules`, `SlotSelectionPolicy`, selección de objetivo de bots, `Transport` |
| **Command** | Todo input (humano o bot) es un `Command` en cola ⇒ determinismo, replay, validación |
| **State** | `LocomotionStateMachine`, `MatchStateMachine` |
| **Observer / Event bus** | `EventBuffer` (anillo sin asignaciones) → snapshots, estadísticas, percepción de bots, logs |
| **Factory + Registry (+ SPI)** | `EffectRegistry`, `MoveRegistry`, `SlugRegistry`, `CharacterRegistry`, `ContentPack` |
| **Composite** | `CompositeEffect`, nodos del Behavior Tree |
| **Chain of Responsibility / Decorator** | `DamagePipeline`, pila de `StatModifier` |
| **Template Method** | `AbstractStatusEffect` |
| **Builder** | `MatchConfig`, `SlugDefinition`, `LoadoutBuilder` |
| **Flyweight + Object Pool** | definiciones compartidas; pools de proyectiles, `StatusInstance`, eventos |
| **Facade** | `GameSimulation` (`submit`, `tick`, `snapshot`) y `GameServer` |
| **Adapter** | `Transport` (Loopback, TCP; UDP después) |

Se evitan patrones sin función (sin Visitor/Mediator "por si acaso"). `ArchUnit` impone: `core` no depende de `server/protocol`; los `Effect` solo se crean vía registry; nada en `core.sim` usa `System.currentTimeMillis`.

## 6. Estructura del repositorio

```
BajoTerra/
├─ pom.xml                      (padre: JDK 27 / release=27, versiones, plugins de calidad)
├─ bajoterra-core/src/main/java/com/bajoterra/core/
│  ├─ sim/ command/ entity/ stats/ ammo/ slug/ effect/(+builtin) combat/
│  ├─ move/(+builtin) character/ world/ rules/ ai/ event/ content/(+spi)
│  └─ resources/content/{slugs,moves,characters,items,maps}/*.json, elements.json
├─ bajoterra-protocol/src/main/proto/game.proto
├─ bajoterra-server/            (GameServer, RoomManager, TickScheduler, TcpTransport, BotMatchRunner, HeadlessClient)
├─ bajoterra-bench/             (JMH + soak + comparador de regresiones)
└─ docs/{architecture.md, protocol.md, content-authoring.md}
```

## 7. Protocolo de red (contrato con Unity)

- **Fuente única:** `game.proto` (protobuf genera Java y C#). Campo `protocolVersion` en el *handshake*; test de snapshot dorado contra deriva del contrato.
- **Cliente → servidor:** `Hello`, `JoinRoom`, `SelectLoadout{characterId, slugIds[10]}`, `Ready`, `InputFrame{seq, moveXY, aimYaw, aimPitch, cameraOrigin, buttons(fire,jump,crouch,roll), useMoveId, selectedSlot}`.
- **Servidor → cliente:** `MatchState`, `Snapshot{tick, ackSeq, combatants[pos, rot, vitals, locomotionState, statuses, slots[disponibilidad]], projectiles[], pickups[]}`, `EventBatch{shot, hit, damage, heal, defeat, roundEnd}`.
- **Transporte v1:** TCP no bloqueante (NIO, framing length-prefixed), un hilo de E/S → cola sin bloqueo → hilo de simulación. `UdpTransport` queda tras la misma interfaz si las medidas de latencia lo exigen. Snapshots completos (≈2 KB @20 Hz); deltas solo si el benchmark lo justifica.
- **Puntería 3ª persona:** el cliente envía el rayo de cámara; el servidor calcula el origen en el blaster y corrige la dirección hacia el punto apuntado.

## 8. Hitos (en orden; cada uno termina con `mvn verify` en verde)

| # | Hito | Entregables | Criterio de salida |
|---|---|---|---|
| M0 | Bootstrap | Actualizar a **JDK 27** (con permiso), POM multimódulo (`release=27`), JUnit5/AssertJ/jqwik/ArchUnit/JMH/JaCoCo/SpotBugs/Checkstyle/PIT, esqueleto de docs, CI (`mvn verify`) | Build vacío en verde **con JDK 27**; *spike* de compatibilidad: cada herramienta de calidad lee bytecode de Java 27 (si alguna no, se sustituye o se aplica con perfil documentado); **pedir permiso para `git init`** |
| M1 | Núcleo de combate | `Vitals`, `DamagePipeline`, `ElementChart`, framework `Effect` + Damage/Heal/RestoreShield, `ContentLoader` + `ContentValidator` | Property tests de vida/escudo (nunca <0, ≤ máx, derrota exacta) |
| M2 | Munición | `Loadout` 10 slots, bloqueo 3 tiros, `FireController`, selección | Tabla de casos de cooldown (n+4) + jqwik |
| M3 | Mundo y proyectiles | Mapa con **obstáculos** (muro, pilar, cobertura baja), rejilla, colisión barrida, `ProjectilePool`, `onWorldHit`, `TargetingRule`, estados (Burn/Slow/Stun/Overshield/Regen/AoE/Knockback), catálogo ≈12 babosas, mapa de prueba "Caverna" | Cada babosa carga y simula 1 impacto (`ContentContractTest`); `ObstacleBlockingTest` en verde (ninguna babosa atraviesa un obstáculo bloqueante ni a velocidad máxima) |
| M4 | Locomoción | State machine + Jump/Crouch/Roll + `StatModifier`, personajes MVP, Dash de Pronto | `ExtensibilityTest` añade movimiento/efecto/babosa de prueba **sin editar el core** |
| M5 | Reglas y objetos | Pickups, `EliminationRoundsRules` best-of-5, `MatchStateMachine`, eventos | Partida 3v3 completa simulada de inicio a fin |
| M6 | Bots | Percepción, Behavior Tree, utilidad de babosas, A\*, perfiles, `BotFiller`, `BotMatchRunner` | 100 partidas bot-vs-bot sin excepciones; en espejo cada equipo gana ≈50 ± 8 % |
| M7 | Protocolo y servidor | `.proto`, codec, `Transport` (Loopback+TCP), `TickScheduler`, salas, `InputBuffer`, `HeadlessClient`, `docs/protocol.md` | Cliente headless juega una partida real contra servidor + bots |
| M8 | Rendimiento | Suite JMH, auditoría de asignaciones, decisión AoS vs SoA, soak/load, GC, *gate* de regresión | Presupuestos de §10 cumplidos |
| M9 | Cierre de calidad | PIT, umbrales de cobertura, guía de contenido, pasada de balance, paquete de traspaso a Unity (`.proto`, esquema de mapa, ids de contenido) | Release `v0.1`, checklist de §9 completo |

**Extensiones posteriores (no incluidas):** reglas con reaparición, carga/transformación velocimorph–megamorph, babosas Ghoul, Mecha Beasts, UDP, compensación de latencia, snapshots delta, implementación Unity.

## 9. Plan de calidad

- **Pruebas:** JUnit 5 + AssertJ (unitarias); **jqwik** (propiedades: vida/escudo, cooldown de slots, conservación de las 10 babosas); **replay dorado** (semilla + comandos ⇒ hash final fijo); **contrato de contenido** (parametrizado sobre todo JSON/efecto/movimiento registrado); `ExtensibilityTest` (prueba de Open/Closed); pruebas de protocolo (ida y vuelta, snapshot dorado).
- **Análisis estático:** `-Xlint:all -Werror`, SpotBugs, Checkstyle, ArchUnit.
- **Cobertura y mutación:** JaCoCo (puerta: core ≥ 85 % líneas / 75 % ramas) y **PIT** sobre `combat`, `ammo`, `effect` (mutación ≥ 80 %).
- **Validación de contenido:** esquema JSON + semántica (tipo de efecto desconocido, valores negativos, ciclos, 10 slots, ids duplicados) con mensajes que citan archivo y campo.
- **Documentación:** `architecture.md` (diagramas), `protocol.md`, `content-authoring.md` (recetas "cómo agregar babosa / efecto / movimiento / personaje").

## 10. Plan de rendimiento

**Presupuesto inicial** (partida 3v3 con bots, hasta 64 proyectiles vivos; se calibra con la línea base de M0/M8): tick medio ≤ 0,25 ms, p99 ≤ 1 ms (de 16,67 ms disponibles); **≈0 B/tick asignados** en régimen estable; codificar snapshot ≤ 50 µs; pensar de un bot ≤ 100 µs.

- **Técnicas:** pools (proyectiles, estados, eventos), sin streams/lambdas/boxing en el tick, definiciones por índice de array (no `HashMap`), rejilla espacial, bots a 10 Hz. `ProjectilePool` se prueba en AoS vs **SoA** y se conserva la opción más simple si la ganancia es <10 %.
- **Medición:** JMH (`TickBenchmark` con 0/32/64 proyectiles, `CollisionBenchmark` (con 10/50/200 obstáculos), `DamagePipelineBenchmark`, `EffectApplyBenchmark`, `LoadoutBenchmark`, `SnapshotCodecBenchmark`, `BotThinkBenchmark`) con `-prof gc`; `AllocationRegressionTest` (bytes asignados por hilo con `ThreadMXBean` en 10 000 ticks tras *warm-up*); JFR para *hot spots*.
- **Soak/carga:** 10 min simulados bot-vs-bot (histograma HdrHistogram de tiempos de tick, heap sin crecimiento) y N partidas concurrentes por servidor para medir escalado; comparar G1 vs ZGC generacional. **Aprovechar el JDK 27:** medir `-XX:+UseCompactObjectHeaders` (menos memoria/caché en pools de objetos), la caché AOT (arranque del servidor) y el GC por defecto; adoptar solo lo que el benchmark respalde.
- **Puerta de regresión:** script que compara el JSON de JMH con `bench/baseline.json` y falla con >15 % de empeoramiento.

## 11. Riesgos y supuestos

| Riesgo / supuesto | Mitigación |
|---|---|
| Java en el servidor, C# en Unity | Contrato `.proto` único + test dorado; Unity solo dibuja |
| *Tunneling* de proyectiles rápidos a través de obstáculos finos | Colisión barrida (tiempo de impacto más temprano) + test con velocidad máxima y muros de grosor mínimo + grosor mínimo validado en `ContentValidator` |
| Herramientas de calidad (ArchUnit, SpotBugs, PIT, JaCoCo, Mockito/ByteBuddy) pueden no soportar aún el bytecode de JDK 27 | *Spike* en M0 con versiones más recientes; si una falla, se reemplaza (p. ej. reglas de arquitectura con `jdeps`/tests propios) o se compila ese análisis con perfil aparte. El código del juego se queda en `release=27` |
| JDK 27 no es LTS (soporte corto) | Solo características finalizadas; subir de versión = cambiar una propiedad y pasar `mvn verify` |
| Pausas de GC en el tick | Cero asignaciones + medición de p99 + prueba de GC |
| Deriva de balance | Todo en JSON; `BotMatchRunner` mide win-rate y tiempo-para-matar |
| Nombres/valores de babosas inexactos | Son datos editables; verificar contra la wiki en M3 |
| **Supuesto:** "3 tiros más" = disponible tras 3 disparos adicionales (usable en el n+4) | Parámetro `reloadShots`; confirmar al revisar |
| **Supuesto:** 60 Hz de simulación, snapshots 20 Hz, TCP primero | Configurables; UDP detrás de `Transport` |
| **Supuesto:** Maven, JDK 27, Jackson + protobuf | Estándar; cambiables en M0 |

## 12. Verificación de punta a punta

0. `java -version` ⇒ 27.x y `mvn -v` usando ese JDK (en Homebrew `openjdk` es *keg-only*: exportar `JAVA_HOME=/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home`).
1. `mvn -q verify` — tests, ArchUnit, SpotBugs, Checkstyle, JaCoCo (puertas), `ExtensibilityTest` y `ObstacleBlockingTest`.
2. `mvn -pl bajoterra-server exec:java -Dexec.mainClass=com.bajoterra.server.BotMatchRunner -Dexec.args="--seed 42 --matches 100"` — imprime win-rate por equipo, duración de ronda y tiempo-para-matar.
3. `mvn -pl bajoterra-bench package && java -jar bajoterra-bench/target/benchmarks.jar -prof gc -rf json -rff out.json` + script comparador contra el baseline.
4. Arrancar `GameServer` y conectar `HeadlessClient` (1 humano simulado + 5 bots): ver snapshots a 20 Hz, estado de los 10 slots y fin de partida best-of-5.
5. Matriz de trazabilidad (§2): cada requisito del usuario tiene al menos un test con nombre asociado.
