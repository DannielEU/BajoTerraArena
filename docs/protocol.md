# Protocolo de red (contrato servidor Java ⇄ cliente Unity)

Fuente única de verdad: [`game.proto`](../bajoterra-protocol/src/main/protobuf/game.proto). De ahí se generan las
clases Java (módulo `bajoterra-protocol`) y las de **C#** para Unity (`Game.cs`, namespace `Bajoterra.Protocol`). Los
campos nunca se renumeran ni se reutilizan; los cambios compatibles solo añaden campos nuevos.

La versión actual del contrato es `ProtocolVersion.CURRENT = 1`. Los bytes de mensajes representativos están fijados en
`ProtocolContractTest` (si alguien cambia el formato por accidente, la prueba falla).

## Convenciones

- Unidades: metros, segundos, radianes. Sistema de Unity: **Y arriba, +Z adelante, +X derecha**.
- **Yaw 0 mira a +Z** y crece hacia +X. Pitch positivo mira hacia arriba.
- La simulación corre a **60 ticks/s**; el servidor envía **20 snapshots/s** (uno cada 3 ticks). El cliente
  interpola ~100 ms entre snapshots.
- El servidor es **autoritativo**: el cliente solo envía entradas y dibuja lo que recibe.

## Transporte y marco (TCP)

Cada mensaje viaja como un *frame*: **4 bytes con la longitud (big-endian)** seguidos de los bytes protobuf. Máximo
64 KiB por frame; una longitud inválida cierra la conexión. En C#:

```csharp
// Escritura
var bytes = message.ToByteArray();
var frame = new byte[4 + bytes.Length];
BinaryPrimitives.WriteInt32BigEndian(frame, bytes.Length);
bytes.CopyTo(frame, 4);
stream.Write(frame);

// Lectura: acumula bytes y extrae mientras haya 4 + longitud disponibles; luego ServerMessage.Parser.ParseFrom(payload)
```

El transporte está detrás de la interfaz `Transport`; un transporte UDP puede sustituir al TCP sin tocar la lógica.

## Flujo de una partida

```
Cliente                                   Servidor
  | Hello{version, name} ───────────────▶ |
  | ◀────────────── Welcome{rates, ContentManifest}
  | JoinRoom{room, team} ───────────────▶ |   (sala nueva si no existe; team -1 = balancear)
  | SelectLoadout{character, 10 slugs} ─▶ |   (lista vacía = loadout aleatorio equilibrado)
  | ◀──────────────────── RoomState (a todos) |
  | Ready{true} ────────────────────────▶ |   (la partida arranca cuando todos los humanos están listos;
  | ◀──────────────────── MatchStart{map, tu id, 6 slots}   los huecos se rellenan con bots hasta 3v3)
  | InputFrame × cada tick ─────────────▶ |
  | ◀──────────── Snapshot + EventBatch × 20/s |
  | ◀──────────────────────────── MatchEnd |   (la sala vuelve al lobby; se puede jugar otra)
```

- Si un humano se desconecta durante la partida, **un bot toma su personaje** y la partida continúa.
- `Ping{client_time}` se responde con `Pong{client_time, server_tick}`; sirve para medir latencia.
- Errores: `ServerError{code, message}`. Códigos: 1 versión incompatible (se desconecta), 2 falta `Hello`,
  3 sala no disponible, 4 loadout inválido, 5 personaje desconocido, 6 no estás en una sala, 7 mensaje inesperado.
  Cuando el servidor desconecta a un cliente, **primero vacía lo que tenía en cola para él**: el error 1 siempre llega
  antes del cierre (lo cubre `TcpServerIntegrationTest`).

## Contenido por índices

`Welcome.content` lista los ids de babosas, personajes, movimientos, mapas y objetos **en orden de índice**. Snapshots y
eventos hacen referencia a ellos por índice (`slug_index`, `active_move`, `pickup.index`…), así que el cliente solo
necesita ese manifiesto para asociar modelos, animaciones y efectos visuales por id. Añadir contenido nuevo no cambia
el protocolo.

## Datos para el cliente: contenido, mapa y estado local

Tres añadidos al contrato (solo campos nuevos; los mensajes anteriores no cambian de bytes) permiten que el cliente
dibuje la interfaz y prediga su movimiento **sin copiar nada del contenido del juego**:

| Mensaje | Dónde viaja | Qué contiene |
|---|---|---|
| `ContentDetails` | `Welcome.details` (una vez) | por índice, alineado con el `ContentManifest`: **babosas** (nombre, elemento, a quién afectan, rol, radio, velocidad), **personajes** (vida, escudo, velocidad, hitbox, movimientos en orden de `move_mask`, cadencia, recarga), **movimientos** (duración y enfriamiento en *ticks*, máscara de posturas desde las que se puede iniciar, multiplicadores, ventana de invulnerabilidad, conducta y sus parámetros) y **objetos** |
| `MapData` | `MatchStart.map` | límites, suelo, **cada obstáculo** (caja + qué bloquea: babosas, movimiento, visión), colocación de objetos (la posición en la lista es el `index` de `PickupState`) y puntos de aparición |
| `LocalState` | `Snapshot.local`, **solo en el snapshot de ese jugador** | el estado de movimiento que `CombatantState` no trae: velocidad vertical, empuje, si toca el suelo, multiplicador de velocidad por estados, aturdimiento, ticks transcurridos del movimiento activo, su dirección y los ticks que faltan para poder repetir cada movimiento |

`ContentManifest.element_ids` da los nombres de los elementos en orden (el `a` de un evento `DAMAGE` es uno de esos
índices). Las conductas de movimiento (`behavior_type` + `behavior_params`) que el cliente sabe reproducir son `impulse`
(`vertical`, `horizontal`) y `drive` (`speed`); una conducta con código propio llega con tipo vacío y el cliente
simplemente no la predice.

## Entrada del jugador (`InputFrame`)

Se envía **una vez por tick** mientras corre la partida.

| Campo | Significado |
|---|---|
| `sequence` | número creciente; el servidor lo devuelve en `Snapshot.ack_sequence` para reconciliar la predicción |
| `move_x`, `move_z` | -1…1; X = lateral (derecha +), Z = adelante |
| `aim_yaw`, `aim_pitch` | orientación de la cámara |
| `has_camera`, `cam_x/y/z` | **tercera persona**: origen de la cámara. El servidor traza el rayo de la cámara hasta el primer obstáculo y orienta la babosa desde el blaster hacia ese punto (la babosa llega adonde apunta la mira) |
| `fire` | botón de disparo mantenido |
| `move_mask` | el bit *i* pide el movimiento *i* del personaje (orden de `Slot.move_ids`). Agacharse se mantiene; saltar/rodar se activan al pulsar |
| `select_slot` | 0–9 elige ranura, -1 sin cambio |
| `cycle_slot` | -1/0/+1 cambia a la ranura disponible anterior/siguiente |

El servidor ignora lo no válido (valores NaN se tratan como 0, el pitch se limita, las ranuras bloqueadas no se pueden
seleccionar).

## Estado del mundo (`Snapshot`, 20 Hz)

- `match`: fase (`LOBBY, COUNTDOWN, ROUND_ACTIVE, ROUND_END, MATCH_END`), ticks restantes de la fase, ronda, rondas
  ganadas por equipo, ganador de la última ronda y de la partida.
- `combatants[6]`: posición (pies), yaw/pitch, `hp`, `shield`, `overshield`, `alive`, postura
  (`GROUNDED, AIRBORNE, CROUCHING, ACTION`), `active_move`, `hitbox_height` (se encoge al agacharse), `selected_slot`,
  `slots[10]` (`slug_index` + `shots_until_ready`: **0 = disponible**, 1–3 = tiros que faltan para recuperarla) y
  `statuses[]` (id, ticks restantes, acumulaciones) para los iconos de estado y `courage` (nivel de Coraje: cuántos
  compañeros de menos tiene su equipo, 0 = ninguno).
- `projectiles[]`: id, babosa, dueño, posición y velocidad (para extrapolar entre snapshots). Las babosas pegajosas
  (granadas) que ya se pegaron llevan `stuck = true`, `fuse_ticks` (ticks que faltan para la explosión) y `stuck_to`
  (combatiente al que van pegadas, o -1 si están en el escenario).
- `pickups[]`: disponibilidad de cada objeto del mapa.

Un snapshot completo de 3v3 ocupa del orden de 1,5–2 KB (≈ 40 KB/s por jugador).

## Eventos (`EventBatch`, junto a cada snapshot)

Sirven para efectos visuales y sonoros (no para el estado, que viene en el snapshot). Campos genéricos
`actor/subject/a/b/x/y/z/value` cuyo significado depende de `kind`:

| `kind` | actor | subject | a / b | x,y,z | value |
|---|---|---|---|---|---|
| `SHOT` | tirador | | slug_index / ranura | boca del blaster | ticks hasta el siguiente disparo |
| `PROJECTILE_HIT` | tirador | víctima | slug_index / 1 si fue a la cabeza (crítico) | punto de impacto | |
| `PROJECTILE_WORLD_HIT` | tirador | | slug_index | punto de impacto | |
| `PROJECTILE_EXPIRED` | tirador | | slug_index | posición | |
| `DAMAGE` | origen o -1 | víctima | elemento (ordinal) / 1 si es crítico | | daño efectivo |
| `HEAL`, `SHIELD_RESTORED`, `OVERSHIELD` | quien cura | objetivo | | | cantidad |
| `STATUS_APPLIED` / `STATUS_EXPIRED` | origen | objetivo | hash del id | | duración |
| `DEFEATED` | asesino o -1 | víctima | | posición de la víctima | |
| `PICKUP_TAKEN` / `PICKUP_RESPAWNED` | | quien lo recoge | índice del objeto | | |
| `MOVE_STARTED` / `MOVE_ENDED` | combatiente | | índice del movimiento | | |
| `KNOCKBACK` | | objetivo | | | fuerza |
| `EXPLOSION` | origen | | slug_index (-1 si no viene de una babosa) | centro | radio |
| `ROUND_STARTED` | | | ronda | | |
| `ROUND_ENDED` | | | equipo ganador (-1 empate) / ronda | | |
| `MATCH_ENDED` | | | equipo ganador (-1 empate) | | |
| `COURAGE` | | combatiente | nivel nuevo / nivel anterior | | escudo restaurado |

## Guía para el cliente Unity

La implementación de referencia está en [`unity/`](../unity/README.md). Resumen de las reglas que sigue:

1. **Predicción del movimiento propio:** cada tick aplica tu `InputFrame` localmente (con las reglas de movimiento que
   dicen `ContentDetails` y `MapData`) y guarda la entrada; al recibir un snapshot con `ack_sequence = n`, carga tu estado
   (`CombatantState` + `LocalState`) y reaplica las entradas posteriores a `n`. Si el resultado difiere de lo predicho,
   mezcla la corrección en ~100 ms (o salta si supera 2 m). Las constantes que el servidor no envía —gravedad 25 m/s²,
   decaimiento del empuje 0,92 por tick, altura de escalón 0,35 m y tirón hacia abajo de 0,35 m al bajar una rampa—
   están fijadas en `MovementModel` y las vigila una
   prueba contra movimiento grabado del servidor.
2. **Resto de combatientes y babosas:** interpola entre los dos snapshots que rodean `hora del servidor − 100 ms`; las
   babosas que solo aparecen en uno se extrapolan con su velocidad (máx. 250 ms).
3. **Eventos:** reprodúcelos cuando el reloj de interpolación llegue a su `tick` (tus propios disparos, al instante).
4. **HUD de babosas:** pinta las 10 ranuras con `shots_until_ready` (0 = lista) y resalta `selected_slot`.
5. **Cámara de 3ª persona:** envía `has_camera = true` con la posición real de la cámara y su yaw/pitch.
6. **Rampas:** `ObstacleInfo.rise` (0 = caja; 1 `+x`, 2 `-x`, 3 `+z`, 4 `-z`) convierte la caja en una cuña cuya
   superficie sube de `min_y` a `max_y` en esa dirección. El cliente la usa para dibujarla, para su colisión (los pies
   siguen la pendiente) y para la cámara; la física es la misma que la del servidor (`Obstacle.topAt`, `World`).
7. **Recarga del blaster:** el `value` del evento `SHOT` propio es la espera exacta en ticks (incluye el Coraje); sin
   él, `SlugInfo.fire_delay_multiplier` × `CharacterInfo.fire_interval_ticks`. El HUD la dibuja como un anillo
   alrededor de la mira.
8. **Cadencia:** un `InputFrame` por tick (60 Hz) con un acumulador de paso fijo; snapshots a 20 Hz.
9. **Granadas:** `SlugInfo.sticky`, `fuse_ticks` y `blast_radius` describen una babosa pegajosa; mientras está pegada
   (`ProjectileState.stuck`) el cliente marca en el suelo el círculo de `blast_radius` y avisa si `stuck_to` eres tú.
   Su explosión llega como `EXPLOSION` con `a` = su `slug_index`.
