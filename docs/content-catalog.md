# Catálogo de contenido

> Generado por `ContentCatalogPrinter`; no editar a mano. Los números son valores iniciales de balance y se ajustan en los JSON de `bajoterra-core/src/main/resources/content/`.

## Babosas (18)

| índice | id | elemento | rol | objetivo | velocidad | gravedad | retardo | efectos al impactar |
|---|---|---|---|---|---|---|---|---|
| 0 | `aquabeek` | WATER | SUPPORT | ALLIES | 44 m/s | 2 | ×1.0 | escudo +30 + limpia ["burn"] |
| 1 | `arachnet` | EARTH | CONTROL | ENEMIES | 44 m/s | 5 | ×1.0 | daño 6 + enredo 1,6 s + lentitud ×0.5 (3,2 s); al fallar, telaraña trampa r 1,6 m (15 s) |
| 2 | `armashelt` | EARTH | SUPPORT | ALLIES | 44 m/s | 2 | ×1.0 | escudo extra 40 (8 s) |
| 3 | `boon_doc` | ENERGY | SUPPORT | ALLIES | 44 m/s | 2 | ×1.2 | regeneración 8/s (5 s) |
| 4 | `bubbaleone` | WATER | CONTROL | ENEMIES | 39.6 m/s | 4 | ×1.1 | daño 6 + burbuja 2,4 s: flota 1,6 m sin moverse ni disparar; el primer golpe la revienta |
| 5 | `doc` | ENERGY | SUPPORT | ALLIES | 44 m/s | 2 | ×1.0 | cura 35 |
| 6 | `flaringo` | FIRE | DAMAGE | ENEMIES | 60.5 m/s | 2 | ×0.6 | daño 14 |
| 7 | `frostcrawler` | ICE | CONTROL | ENEMIES | 35.2 m/s | 12 | ×1.2 | daño 6 + levanta un muro de hielo (2 impactos, 8 s); al chocar con un obstáculo: levanta un muro de hielo (2 impactos, 8 s) |
| 8 | `frostfang` | ICE | CONTROL | ENEMIES | 49.5 m/s | 3 | ×1.0 | daño 8 + congela (1.5 s) |
| 9 | `grenuke` | FIRE | DAMAGE | ENEMIES | 30.8 m/s | 14 | ×1.3 | se pega donde choca y a los 1.6 s explota: área r=3.5 [daño 34 + empuje 9] |
| 10 | `hop_rock` | FIRE | DAMAGE | ENEMIES | 38.5 m/s | 10 | ×1.2 | área r=3.0 [daño 25 + empuje 8]; al chocar con un obstáculo: área r=3.0 [daño 25 + empuje 8]; al caducar: área r=3.0 [daño 25 + empuje 8] |
| 11 | `infurnus` | FIRE | DAMAGE | ENEMIES | 49.5 m/s | 4 | ×1.0 | daño 18 + quemadura 4/s (3 s); deja un muro de fuego de hasta 14 m que arde 3.5 s: daño 5 + quemadura 4/s (2 s) |
| 12 | `lariat` | WATER | CONTROL | ENEMIES | 55 m/s | 2 | ×1.1 | daño 7 + lazo: arrastra al rival hasta 2,5 m de quien disparó (0,7 s sin andar); alcance ~30 m (vida 0,55 s) |
| 13 | `phosphoro` | LIGHT | CONTROL | ENEMIES | 22 m/s | 9 | ×1.4 | se pega donde choca y a los 1.5 s explota: destello visible a 26 m [ciega (4 s)] a quien lo mire |
| 14 | `rammstone` | EARTH | DAMAGE | ENEMIES | 33 m/s | 6 | ×1.5 | daño 35 + empuje 4 |
| 15 | `tazerling` | ENERGY | CONTROL | ENEMIES | 60.5 m/s | 1 | ×1.0 | daño 12 + aturde (0.8 s) + cadena 1× [daño 12 + aturde (0.8 s)] |
| 16 | `thresher` | METAL | DAMAGE | ENEMIES | 55 m/s | 1 | ×1.0 | daño 22 (ignora 50% escudo); atraviesa 1 |
| 17 | `tormato` | AIR | CONTROL | ENEMIES | 41.8 m/s | 3 | ×1.15 | daño 8 + tornado r 3,5 m (3 s): atrae, eleva, 4 de daño cada 0,5 s, desvía babosas enemigas, salto tornado para aliados; con una babosa de hielo dentro se vuelve tornado congelante (congela 0,9 s + 3 de daño de hielo) |

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
- `flash`
- `freeze`
- `heal`
- `ice_wall`
- `knockback`
- `overshield`
- `regen`
- `restore_shield`
- `slow`
- `stun`
