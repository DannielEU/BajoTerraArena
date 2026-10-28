# Cliente Unity: estado y traspaso

El cliente existe en [`unity/`](../unity/README.md) (guía completa allí). Este documento resume qué hay, qué se verificó y
qué queda para quien lo abra en el editor.

## Qué está hecho

| Área | Estado |
|---|---|
| Red (TCP, tramas, handshake, ping), sesión, lobby, selección de personaje y de las 10 babosas | implementado y probado contra el servidor real |
| Predicción y reconciliación del movimiento propio | implementado; coincide con el servidor en 8 escenarios grabados y en una partida real (error mediano 0,000 m) |
| Interpolación de los demás, babosas, relojes y eventos | implementado y probado |
| Cámara en tercera persona con colisión contra muros y puntería por rayo de cámara | implementado y probado (geometría) |
| Mundo, personajes, babosas, efectos, audio, HUD, pausa, resultados, observador | implementado **y compilado**, no ejecutado en el editor |
| Lanzador del servidor local y menú del editor | implementado y compilado, no ejecutado |

## Qué dibuja el cliente (mapa de responsabilidades)

| Dato del servidor | Qué muestra Unity |
|---|---|
| `CombatantState` (pos, yaw, postura, `active_move`, `hitbox_height`) | cápsula del tamaño real, giro al rodar, caída al ser derrotado |
| `hp`, `shield`, `overshield` | barras sobre los rivales/aliados y en el HUD; burbuja de escudo |
| `slots[10]` con `shots_until_ready` | HUD de las 10 babosas: 0 = lista, 1–3 = bloqueada ("vuelve en N tiros") |
| `ProjectileState` | bola con estela del color del elemento (más blanca si ayuda a aliados) |
| `EventBatch` | chispas, explosiones (radio = `value`), números de daño/curación, sonido, kill feed, marcos de ronda |
| `statuses[]` | puntos de color sobre el personaje y chips en el HUD |
| `MatchInfo` | cuenta atrás, marcador de rondas (mejor de 5), temporizador |
| `MapData` | suelo, obstáculos (coloreados según lo que bloquean) y objetos |

## Qué NO hace el cliente

- No decide daño, impactos, curaciones, colisiones con babosas ni el bloqueo de 3 tiros: lo valida el servidor.
- No simula bots: son combatientes del servidor que producen las mismas entradas que un humano.
- No tiene copia del contenido del juego: personajes, movimientos y mapa llegan del servidor al conectar.

## Para quien lo abre en Unity por primera vez

1. Sigue [unity/README.md](../unity/README.md#cómo-abrirlo-y-jugar).
2. Revisa en **Play** lo que la compilación no puede garantizar: tamaño y legibilidad de la UI a distintas resoluciones,
   aspecto de partículas y colores, volumen relativo de los sonidos, giro/sensibilidad del ratón, comportamiento del cursor.
3. Si cambias algo del protocolo: `unity/scripts/sync-protocol.sh` y `unity/scripts/verify.sh`.

## Siguientes pasos naturales

- Sustituir `ArenaView`/`ProceduralAudio` por geometría y audio reales (los héroes ya son modelos de Blender con animaciones: [unity/art](../unity/art/README.md)).
- Una herramienta que exporte los colliders de una escena de Unity al JSON de mapa (cajas con tres banderas) para que el
  servidor los valide.
- Transformación visual de la babosa por velocidad (velocimorph/megamorph) cuando exista en el servidor.
- Transporte WebSocket si se quiere WebGL (el contrato no cambia; solo se añade otra implementación de `IClientTransport`).
