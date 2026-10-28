# Catálogo de contenido

> Generado por `ContentCatalogPrinter`; no editar a mano. Los números son valores iniciales de balance y se ajustan en los JSON de `bajoterra-core/src/main/resources/content/`.

## Babosas (14)

| índice | id | elemento | rol | objetivo | velocidad | gravedad | retardo | efectos al impactar |
|---|---|---|---|---|---|---|---|---|
| 0 | `aquabeek` | WATER | SUPPORT | ALLIES | 40 m/s | 2 | ×1.0 | escudo +30 + limpia ["burn"] |
| 1 | `arachnet` | EARTH | CONTROL | ENEMIES | 40 m/s | 3 | ×1.0 | daño 6 + lentitud ×0.4 (3 s) |
| 2 | `armashelt` | EARTH | SUPPORT | ALLIES | 40 m/s | 2 | ×1.0 | escudo extra 40 (8 s) |
| 3 | `boon_doc` | ENERGY | SUPPORT | ALLIES | 40 m/s | 2 | ×1.2 | regeneración 8/s (5 s) |
| 4 | `doc` | ENERGY | SUPPORT | ALLIES | 40 m/s | 2 | ×1.0 | cura 35 |
| 5 | `flaringo` | FIRE | DAMAGE | ENEMIES | 55 m/s | 2 | ×0.6 | daño 14 |
| 6 | `frostfang` | ICE | CONTROL | ENEMIES | 45 m/s | 3 | ×1.0 | daño 8 + congela (1.5 s) |
| 7 | `hop_rock` | FIRE | DAMAGE | ENEMIES | 35 m/s | 10 | ×1.2 | área r=3.0 [daño 25 + empuje 8]; al chocar con un obstáculo: área r=3.0 [daño 25 + empuje 8]; al caducar: área r=3.0 [daño 25 + empuje 8] |
| 8 | `infurnus` | FIRE | DAMAGE | ENEMIES | 45 m/s | 4 | ×1.0 | daño 18 + quemadura 4/s (3 s) |
| 9 | `phosphoro` | LIGHT | CONTROL | ENEMIES | 50 m/s | 2 | ×1.0 | daño 5 + ciega (3 s) |
| 10 | `rammstone` | EARTH | DAMAGE | ENEMIES | 30 m/s | 6 | ×1.5 | daño 35 + empuje 4 |
| 11 | `tazerling` | ENERGY | CONTROL | ENEMIES | 55 m/s | 1 | ×1.0 | daño 12 + aturde (0.8 s) + cadena 1× [daño 12 + aturde (0.8 s)] |
| 12 | `thresher` | METAL | DAMAGE | ENEMIES | 50 m/s | 1 | ×1.0 | daño 22 (ignora 50% escudo); atraviesa 1 |
| 13 | `tormato` | AIR | CONTROL | ENEMIES | 40 m/s | 2 | ×1.0 | daño 10 + empuje 14 |

## Personajes (4)

| índice | id | vida | escudo | velocidad | afinidad | potencia de cura | movimientos | blaster |
|---|---|---|---|---|---|---|---|---|
| 0 | `eli` | 100 | 50 | 6.0 m/s | NEUTRAL | ×1.00 | jump, crouch, roll | 42 ticks, bloqueo 3 tiros |
| 1 | `kord` | 140 | 70 | 5.0 m/s | EARTH | ×1.00 | jump, crouch, roll | 42 ticks, bloqueo 3 tiros |
| 2 | `pronto` | 80 | 40 | 7.0 m/s | AIR | ×1.00 | jump, crouch, roll, dash | 42 ticks, bloqueo 3 tiros |
| 3 | `trixie` | 90 | 60 | 6.0 m/s | WATER | ×1.25 | jump, crouch, roll | 42 ticks, bloqueo 3 tiros |

## Movimientos (4)

| índice | id | duración | enfriamiento | tipo | desde | invulnerable | conducta |
|---|---|---|---|---|---|---|---|
| 0 | `crouch` | mientras se mantiene | 0.00 s | postura | GROUNDED | no | NoMoveBehavior |
| 1 | `dash` | 0.20 s | 2.00 s | acción | GROUNDED, AIRBORNE, CROUCHING | no | DriveBehavior |
| 2 | `jump` | 0.02 s | 0.35 s | acción | GROUNDED, CROUCHING | no | ImpulseBehavior |
| 3 | `roll` | 0.50 s | 1.20 s | acción | GROUNDED, CROUCHING | 0.05 s–0.35 s | DriveBehavior |

## Objetos (3)

| índice | id | requisito | reaparece | radio |
|---|---|---|---|---|
| 0 | `health_pack` | NEEDS_HP | 20.00 s | 1.2 m |
| 1 | `overshield_orb` | ANY | 30.00 s | 1.2 m |
| 2 | `shield_cell` | NEEDS_SHIELD | 20.00 s | 1.2 m |

## Mapas (1)

| índice | id | tamaño | obstáculos | spawns por equipo | objetos | nodos de navegación | puntos de cobertura |
|---|---|---|---|---|---|---|---|
| 0 | `caverna` | 60 × 40 m | 29 | 3 / 3 | 7 | 184 | 68 |

## Ventajas elementales

Multiplicador cuando el elemento atacante (fila) golpea a un personaje con la afinidad indicada (columna). Solo se listan los valores distintos de 1.

| ataca | contra | multiplicador |
|---|---|---|
| FIRE | WATER | ×0.80 |
| FIRE | ICE | ×1.25 |
| FIRE | METAL | ×0.90 |
| WATER | FIRE | ×1.25 |
| WATER | EARTH | ×0.90 |
| WATER | ENERGY | ×0.80 |
| EARTH | AIR | ×0.80 |
| EARTH | ENERGY | ×1.25 |
| EARTH | METAL | ×0.90 |
| AIR | FIRE | ×0.90 |
| AIR | EARTH | ×1.25 |
| ENERGY | WATER | ×1.25 |
| ENERGY | EARTH | ×0.80 |
| ENERGY | AIR | ×1.25 |
| ICE | FIRE | ×0.80 |
| ICE | AIR | ×1.25 |
| METAL | ENERGY | ×0.80 |
| METAL | ICE | ×1.25 |

## Tipos de efecto registrados

- `area`
- `blind`
- `burn`
- `chain`
- `cleanse`
- `conditional`
- `damage`
- `freeze`
- `heal`
- `knockback`
- `overshield`
- `regen`
- `restore_shield`
- `slow`
- `stun`
