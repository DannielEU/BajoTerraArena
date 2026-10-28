# Cómo agregar contenido

Todo lo que define el juego (babosas, efectos, movimientos, personajes, objetos y mapas) se agrega **sin editar
ninguna clase existente**:

| Quiero agregar… | Qué escribo | Archivos nuevos | Clases existentes editadas |
|---|---|---|---|
| Una babosa que combina efectos existentes | un JSON en `content/slugs/` | 1 | 0 |
| Un efecto nuevo (veneno, robo de vida…) | 1 clase con `@AutoService` + el JSON de la babosa que lo use | 2 | 0 |
| Un movimiento que reutiliza una conducta (deslizarse) | un JSON en `content/moves/` | 1 | 0 |
| Un movimiento con física propia (salto largo) | 1 clase `MoveBehavior` con `@AutoService` + JSON | 2 | 0 |
| Un personaje | un JSON en `content/characters/` | 1 | 0 |
| Un objeto (botiquín, celda de escudo) | un JSON en `content/items/` | 1 | 0 |
| Un mapa con obstáculos | un JSON en `content/maps/` | 1 | 0 |
| Un modo de juego | 1 clase que implementa `MatchRules` | 1 | 0 |

Los archivos JSON se descubren solos: cualquier `content/<tipo>/*.json` en el classpath (también dentro de otros
jars) se carga al arrancar. Los identificadores (`id`) deben ser únicos por tipo; el índice denso que usan los
snapshots se asigna ordenando los ids alfabéticamente.

> **Prueba que lo demuestra:** `ExtensibilityTest` (en `bajoterra-core/src/test`) agrega un efecto (`poison`), una
> babosa (`test_toxic`), una conducta de movimiento (`test_leap`), un movimiento solo-JSON (`test_slide`) y un
> personaje (`test_hopper`) desde fuera del núcleo y los juega con el juego sin modificar.

## 1. Nueva babosa (solo JSON)

`content/slugs/infurnus.json`:

```json
{
  "id": "infurnus",
  "name": "Infurnus",
  "element": "FIRE",
  "role": "DAMAGE",
  "targeting": "ENEMIES",
  "projectile": { "speed": 45, "gravity": 4, "radius": 0.25, "lifetimeSeconds": 2.5, "pierce": 0 },
  "fireDelayMultiplier": 1.0,
  "botWeight": 1.0,
  "onHit": [
    { "type": "damage", "amount": 18 },
    { "type": "burn", "dps": 4, "durationSeconds": 3, "stacking": "REFRESH" }
  ]
}
```

| Campo | Valores | Notas |
|---|---|---|
| `element` | `NEUTRAL FIRE WATER EARTH AIR ENERGY ICE METAL LIGHT` | afecta el multiplicador de daño (`elements.json`) |
| `role` | `DAMAGE CONTROL SUPPORT` | lo usan los bots y la generación de loadouts; `SUPPORT` exige `targeting` `ALLIES*` |
| `targeting` | `ENEMIES ALLIES ALLIES_AND_SELF ANY` | quién puede ser alcanzado; la babosa **atraviesa** a los demás |
| `projectile.speed` | 1–200 m/s | 45 m/s ≈ 100 mph, la velocidad de transformación de la serie |
| `projectile.gravity` | 0–60 m/s² | 0 = tiro recto, alto = parábola (pasa por encima de coberturas bajas) |
| `projectile.pierce` | 0–5 | combatientes adicionales que atraviesa tras el primero |
| `fireDelayMultiplier` | 0.1–5 | multiplica el intervalo entre disparos del blaster tras usarla |
| `reloadShots` | 0–9 | sobrescribe cuántos tiros debe esperar para volver a usarse (por defecto, el del blaster: 3) |
| `pierceWorld` | `true/false` | atraviesa obstáculos (por defecto no: toda babosa se detiene en los obstáculos) |
| `onHit` | lista de efectos | al alcanzar un combatiente válido |
| `onWorldHit` | lista de efectos | donde se detiene contra un obstáculo o el suelo (p. ej. explosión) |
| `onExpire` | lista de efectos | donde caduca en vuelo |
| `onTick` | lista de efectos | cada tick de vuelo (sin objetivo) |
| `botWeight` | 0.01–10 | cuánto prefieren los bots esta babosa |

**Reglas de 10 babosas:** cada jugador lleva exactamente 10 (se permiten repetidas, máximo 2 copias por defecto). Una
babosa disparada queda bloqueada durante los **3 tiros siguientes** y vuelve a estar disponible en el cuarto.

## 2. Efectos disponibles

Todos aceptan los campos comunes de duración cuando son estados: `durationSeconds` **o** `durationTicks`,
`stacking` (`REFRESH`, `STACK`, `REPLACE_IF_STRONGER`, `IGNORE`) y `maxStacks`.

| `type` | Parámetros | Qué hace |
|---|---|---|
| `damage` | `amount`, `element?`, `shieldPierce?` (0–1) | daño; el escudo absorbe primero, el resto va a la vida |
| `heal` | `amount` | cura vida (× potencia de curación de quien dispara) |
| `restore_shield` | `amount` | restaura escudo |
| `overshield` | `amount`, duración | escudo temporal que decae a cero |
| `knockback` | `force`, `lift?` | empuja desde el punto de impacto |
| `area` | `radius`, `falloff` (`LINEAR`/`NONE`), `minScale?`, `effects[]` | aplica los hijos a todos los combatientes válidos del radio |
| `chain` | `radius`, `jumps`, `decay`, `effects[]` | salta al enemigo más cercano que no haya sido alcanzado |
| `conditional` | `ifTargetHasStatus?`, `ifTargetHpBelow?`, `then[]`, `else[]` | elige rama según el objetivo |
| `cleanse` | `tags[]` | quita estados con alguna de esas etiquetas (`burn`, `cc`, `debuff`…) |
| `burn` | `dps`, duración | daño de fuego en el tiempo |
| `slow` | `factor` (0–1), duración | reduce la velocidad |
| `freeze` | `speedFactor?`, `vulnerableTo?`, `vulnerability?`, duración | casi inmoviliza y hace vulnerable a un elemento |
| `stun` | duración | no puede moverse, disparar ni iniciar movimientos |
| `regen` | `hps`, duración | cura en el tiempo |
| `blind` | `spread?`, duración | error aleatorio de puntería |

## 3. Nuevo efecto (una clase)

Ejemplo real, en `bajoterra-core/src/test/java/com/bajoterra/core/testext/PoisonEffect.java`:

```java
public final class PoisonEffect extends AbstractStatusEffect {
    private final double dps;
    public PoisonEffect(Params params, double dps) { super(params); this.dps = dps; }

    @Override protected void onApply(StatusInstance instance, EffectContext ctx) { instance.setValue(dps); }

    @Override protected void onPeriodic(StatusInstance instance, Combatant owner, CombatArena arena) {
        arena.damage(arena.byId(instance.sourceId()), owner, instance.value() * 15 / 60.0, Element.NEUTRAL, 1.0);
    }

    @AutoService(EffectFactory.class)          // se registra solo, sin tocar ninguna lista
    public static final class Factory implements EffectFactory {
        public String type() { return "poison"; }
        public Effect create(Spec spec, EffectRegistry registry) {
            return new PoisonEffect(Params.parse(spec, "poison", 15, "poison", "debuff"),
                    spec.requireDouble("dps", 0, 1000));
        }
    }
}
```

Y ya se puede usar en cualquier babosa: `{ "type": "poison", "dps": 6, "durationSeconds": 4 }`.

- Los efectos **instantáneos** implementan `Effect` directamente (`apply(EffectContext)`).
- Los **estados** (duran en el tiempo) extienden `AbstractStatusEffect` y sobrescriben los ganchos que necesiten:
  `onApply`, `onPeriodic`, `modify` (modifica `StatBlock`: velocidad, hitbox, invulnerabilidad, aturdimiento…) y `onExpire`.
- Las instancias de efectos son **inmutables y compartidas**: el estado por objetivo va en el `StatusInstance`.
- Valida siempre con `spec.requireDouble(campo, min, max)` etc.: los errores salen como
  `content/slugs/x.json.onHit[0].amount: -4.0 is outside [0.0, 10000.0]`.
- **No asignar memoria** en `apply`/`onPeriodic` (el tick no genera basura; lo vigila `AllocationRegressionTest`).

## 4. Nuevo movimiento

**Solo JSON** (`content/moves/slide.json`), reutilizando la conducta `drive`:

```json
{
  "id": "slide",
  "durationSeconds": 0.4,
  "cooldownSeconds": 1.0,
  "allowedFrom": ["GROUNDED"],
  "hitboxHeightMultiplier": 0.5,
  "behavior": { "type": "drive", "speed": 12.0 }
}
```

| Campo | Notas |
|---|---|
| `allowedFrom` | posturas desde las que puede empezar: `GROUNDED AIRBORNE CROUCHING ACTION` |
| `holdable` / `stance` | `holdable`: dura mientras se mantenga el botón (agacharse); `stance`: pone la postura `CROUCHING` |
| `interruptible` | otro movimiento puede reemplazarlo mientras está activo (por defecto, igual a `holdable`) |
| `blocksFiring` | no se puede disparar mientras dura (rodar) |
| `speedMultiplier`, `hitboxHeightMultiplier` | modificadores mientras está activo |
| `invulnerable` | `{ "fromSeconds": 0.05, "toSeconds": 0.35 }` ventana de invulnerabilidad |
| `behavior` | `impulse` (`vertical`, `horizontal`: empuje inicial, p. ej. saltar) o `drive` (`speed`: movimiento dirigido, p. ej. rodar/dash) |

Para añadirlo a un personaje, agrega su id a `"moves"` en el JSON del personaje (el orden define el bit del
`move_mask` en el protocolo: **agregar un movimiento no cambia el protocolo**).

**Con física propia:** implementa `MoveBehavior` + `MoveBehaviorFactory` con `@AutoService` (ejemplo:
`LeapBehavior` en `testext`).

## 5. Nuevo personaje

```json
{
  "id": "eli", "name": "Eli Shane",
  "maxHp": 100, "maxShield": 50, "speed": 6.0,
  "hitRadius": 0.4, "hitHeight": 1.8,
  "affinity": "NEUTRAL", "healPotency": 1.0,
  "moves": ["jump", "crouch", "roll"],
  "blaster": { "fireIntervalSeconds": 0.7, "reloadShots": 3 }
}
```

## 6. Nuevo objeto (pickup)

```json
{ "id": "health_pack", "respawnSeconds": 20, "radius": 1.2, "requirement": "NEEDS_HP",
  "effects": [ { "type": "heal", "amount": 40 } ] }
```

Usa los **mismos efectos** que las babosas: curar con un objeto y con la babosa de un aliado es el mismo código.
`requirement`: `ANY NEEDS_HP NEEDS_SHIELD NEEDS_HP_OR_SHIELD` (para no gastarlo a vida completa).

## 7. Nuevo mapa con obstáculos

```json
{
  "id": "caverna",
  "bounds": { "minX": -30, "maxX": 30, "minZ": -20, "maxZ": 20 },
  "obstacles": [
    { "id": "center_wall", "min": [-1, 0, 6], "max": [1, 4, 10] },
    { "id": "low_cover",   "min": [-1.5, 0, -2], "max": [1.5, 1.0, 2] },
    { "id": "fence", "min": [-8, 0, -6], "max": [-7.7, 1.5, -3],
      "blocksProjectiles": false, "blocksLineOfSight": false },
    { "id": "plateau", "min": [4, 0, -2], "max": [8, 2, 2] },
    { "id": "ramp_up",  "min": [-2, 0, -2], "max": [4, 2, 2], "ramp": "+x" }
  ],
  "spawns": { "0": [ { "pos": [-26, 0, -4], "yaw": 90 } ], "1": [ { "pos": [26, 0, -4], "yaw": 270 } ] },
  "pickups": [ { "item": "health_pack", "pos": [0, 0, 15] } ],
  "nav": { "cellSize": 3 }
}
```

**Esquema del mapa** (lo que el cargador exige):

| Campo | Tipo | Notas |
|---|---|---|
| `id` | texto | único entre mapas |
| `name` | texto (opcional) | nombre visible |
| `bounds` | `{minX, maxX, minZ, maxZ}` | metros; el suelo es el plano y = 0 |
| `obstacles[]` | `{id?, min:[x,y,z], max:[x,y,z], blocksProjectiles?, blocksMovement?, blocksLineOfSight?, ramp?}` | cajas alineadas a los ejes; grosor mínimo 0,2 m; con `ramp`, una rampa (ver abajo) |
| `spawns` | `{"0": [{pos:[x,y,z], yaw}], "1": [...]}` | mínimo 3 por equipo; `yaw` en grados |
| `pickups[]` | `{item, pos:[x,y,z]}` | `item` = id de un objeto definido |
| `nav.cellSize` | número 1–10 (opcional, por defecto 3) | separación de la malla de navegación generada |

- Cada obstáculo es una caja (`min`/`max`) con tres banderas, todas `true` por defecto:
  `blocksProjectiles` (detiene babosas), `blocksMovement` (no se puede atravesar), `blocksLineOfSight` (los bots no ven).
- **Cobertura baja** (altura ≈ 1 m): bloquea tiros planos; un combatiente agachado queda protegido, pero las babosas con
  `gravity` alto pasan por encima.
- **Rampas.** Con `"ramp": "+x"` (también `-x`, `+z`, `-z`) la caja se convierte en una cuña: la parte de abajo es plana en
  `min.y` y la superficie sube en línea recta desde `min.y` en un borde hasta `max.y` en el borde opuesto, en la dirección
  indicada. Los combatientes la suben y la bajan andando (los pies siguen la pendiente), y las babosas y la línea de vista
  chocan con la superficie inclinada. Pendiente máxima: 0,7 (subida / recorrido); para llegar a una plataforma de altura
  `h` sin saltar, la rampa termina a `h` justo pegada a ella. Una plataforma más alta que lo que alcanza un salto
  (≈ 1,8 m con el salto actual) solo se alcanza por rampa. Las rampas son sólidas y no cuentan como cobertura ni para la
  malla de navegación: **los bots se quedan en el suelo**.
- Un `pickup` puede ir sobre una plataforma poniendo su altura en `pos` (`[x, h, z]`); el validador acepta un punto que
  está sobre la superficie de un obstáculo, no dentro.
- La navegación de los bots y los puntos de cobertura se **generan solos** a partir de los obstáculos.
- El validador rechaza: spawns dentro de obstáculos, menos de 3 spawns por equipo, obstáculos más finos de 0,2 m,
  objetos no definidos y mapas donde un equipo no puede llegar al otro.
- `yaw` en grados (0 = +Z, 90 = +X).

## 8. Nuevo modo de juego

Implementa `MatchRules` (duraciones, `evaluateRound`, `isMatchOver`, `matchWinner`) y pásalo en
`MatchConfig.builder(...).rules(...)`. El modo por defecto es `EliminationRoundsRules.bestOfFive()`: rondas sin
reaparición, gana quien llegue a 3 rondas.

## 9. Qué te protege cuando te equivocas

- **Carga:** todos los problemas se reportan juntos, con archivo y campo (`ContentLoaderErrorsTest`).
- **Contrato automático:** `ContentContractTest` recorre *cada* babosa, personaje y mapa que exista, la dispara/juega y
  comprueba que funciona. Una babosa nueva queda cubierta sin escribir un test.
- **Registro:** `RegisteredTypesTest` falla si una fábrica de efectos/conductas olvidó `@AutoService`.
- **Arquitectura:** `ArchitectureTest` impide, entre otras cosas, instanciar efectos integrados fuera de sus fábricas,
  usar `System.currentTimeMillis`, `java.util.Random` o `Math.sin/cos…` (se usa `StrictMath` para que las partidas se
  repitan igual en cualquier CPU).
