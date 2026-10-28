# Arte de los héroes (Blender)

Los cuatro héroes y sus blasters se hacen en Blender **con scripts** (`unity/art/blender/*.py`), a partir de las
imágenes de referencia de `images/` y del arte oficial de cada personaje de la serie, y se exportan a `unity/BajoTerraClient/Assets/Resources/Models/<id>.fbx`.
Esos FBX son los que usa el juego (`RiggedCharacterModel.cs`); si un personaje no tiene FBX, el cliente dibuja el
cuerpo de primitivas de siempre (`PrimitiveCharacterModel.cs`).

## Tamaños (igual que el hitbox del servidor)

| héroe | altura | radio | cómo se lee (sacado de los recortes del póster y del arte oficial) |
|---|---|---|---|
| Eli | 1.80 m | 0.40 | delgado y de piernas largas; pelo negro azulado: flequillo de mechones largos y puntiagudos desde su izquierda hasta su ceja y mejilla derechas, picos hacia atrás; cara angulosa con mentón en punta, cejas gruesas inclinadas hacia la nariz, iris azul claro bajo una línea de pestañas gruesa, sonrisa de lado; chaqueta negra con paneles naranjas a los lados y mangas gris claro; dos tarjetas azules de babosa en el pecho; pantalón azul acero con paneles oscuros y rodilleras; botas negras hasta la rodilla; Burpy en el hombro |
| Trixie | 1.70 m | 0.40 | bob cobrizo: un gran barrido por la frente hacia su derecha que cae por fuera del ojo, mechones laterales hasta la mandíbula y dos moños altos atrás; cara en corazón, ojos verdes con delineado y "colita", cejas finas arqueadas, labios rosas; camiseta oliva sobre corsé negro; pantalón gris claro con rodilleras y correa en el muslo; botas negras altas con puntera y suela blancas; guantes sin dedos; babosa verde |
| **Kord** | **2.35 m** | **0.65** | troll azul enorme y de torso masivo (piernas más delgadas); casco de pelo negro con el borde recto sobre las cejas, banda naranja y cuerno gris; cara alargada de mandíbula y mentón grandes y cuadrados, nariz ancha y corta, ojos ámbar con párpado caído, sonrisa de lado en media luna con los dientes de arriba, perilla naranja en triángulo; orejas de elfo en punta; chaleco negro con estrella naranja sobre camiseta blanca; bandolera de tubos azules; pantalón café oscuro y botas grises cortas |
| **Pronto** | **1.15 m** | **0.30** | molenoide (topo) pequeño y redondo: la parte baja de la cara **es un hocico** que sale hacia delante, con una almohadilla de nariz oscura en la punta, la sonrisa de dientes cuadrados cruzándolo y puntos de bigote a los lados; ojos grandes bajo párpados pesados encima del hocico y dos cejas negras arqueadas; gorro de aviador beige con gafas amarillas encima; camiseta de rayas anchas azul/blanco bajo un arnés naranja; guantes largos oscuros con coderas naranjas; short verde; latas de babosa en el cinturón; armazón de mochila |

## Caras (`bt_face.py`, `bt_heads.py`)

Cada cabeza es una malla densa que se **esculpe por código**: primero un perfil de anillos de mentón a coronilla (ancho,
fondo y desplazamiento hacia delante, como leer un turnaround) y luego golpes suaves (`Bump`) que sacan o hunden la cara:
arco de las cejas, cuencas de los ojos, puente y punta de la nariz, pómulos, labios y mentón. La cabeza esculpida queda
en un BVH, y todos los rasgos se apoyan **exactamente sobre su superficie**:

- `Head.patch`: una rejilla pegada a la piel (blanco del ojo abombado, iris, pupila, brillo, boca abierta, labios,
  dientes, pecas) con un faldón que se mete bajo la piel para que no se vea un hueco de lado;
- `Head.strip`: una banda en relieve a lo largo de una curva (pestañas, cejas, línea de la boca, pliegue del párpado).

Los ojos (`Eye`) son almendras con el iris recortado por los párpados (por eso se ven "a medio cerrar" como en la
serie), línea de pestañas con "colita" opcional, línea inferior, pliegue y párpado pesado inclinado (Kord, Pronto,
Burpy). Las cejas y pestañas son planas (unos 3 mm de relieve): en la serie son gruesas en el dibujo, no en volumen. `bt_heads.py` tiene la forma, la cara y el pelo de cada héroe, comparados con el arte oficial.

**Pelo sin calvas:** el pelo (y los gorros) es una cáscara que cubre todo el cráneo hasta una línea de nacimiento
(`bt_lib.hairline` o una curva propia, como el bob de Trixie, alta en la frente y las sienes y baja sobre las orejas) y
cada uno de sus puntos se empuja por fuera de la cabeza (`Head.outside`), así que la piel no puede asomar por ningún lado.
Encima van mechones curvos que se afinan en punta (`bt_face.lock`, controlados en coordenadas del cráneo con
`lock_sph`), que dan la silueta: el flequillo y los picos de Eli, el barrido y los moños de Trixie; las bandas (la de
Kord, el borde y la correa del gorro de Pronto) son tiras sin punta que siguen la línea del pelo (`hairline_band`).

## Babosas (`bt_slugs.py`)

Como en la serie: cuerpo blando y erguido que también es la cabeza, pie ensanchado con cola, aletas pequeñas a los
lados, **ojos grandes en la cara** (no en antenas), boca debajo y dos antenas largas arriba, más las marcas de cada
especie pintadas sobre la malla. Eli lleva a **Burpy** (Infurnus: naranja, llamas amarillas en la frente y la nuca,
panza clara en forma de llama, ceño fruncido), Trixie una babosa verde menta con capucha verde oscuro, Kord una de
capucha roja con cresta azul (Rammstone, boca abierta con dos dientes) y Pronto una azul. Usan la misma maquinaria de
caras que los héroes.

Kord y Pronto se cambiaron también en el servidor (`content/characters/kord.json` y `pronto.json`), así que lo que se ve
es donde las babosas pegan. Si cambias una medida: edita el JSON, la altura en `bt_chars.SPECS` y en
`RiggedCharacterModel.AuthoredHeights`, y vuelve a exportar (el modelo se escala a la altura real del servidor, pero
conviene que coincidan).

## Los blasters (de las imágenes `*_wepon.webp` y `Pronto_blaster.webp`)

Eli: carcasa naranja **en horquilla** (punta superior larga y otra inferior más corta), gran ventana azul con el tubo
de la babosa, estrella azul, disco con anillo de remaches, marco gris con guardamonte cuadrado y empuñadura oscura ·
Trixie: morro verde abombado con aletas, ventana azul, estrella naranja con borde negro y lente azul delante, cuerpo
plateado con aleta alta, disco grande bajo el morro y empuñadura negra curva · Kord: **cabeza de tiburón** con las dos
mandíbulas abiertas, dientes en sierra y encías rojas, ojo rojo, dos cañones con anillos naranjas y bandas cian, cuerpo
oscuro con estrella, disco grande atrás y empuñadura larga · Pronto: **rifle** con embudo de borde naranja, cámara de
vidrio con barra naranja, varilla superior, asa bajo el cañón, disco grande con mira encima, marco cuadrado alrededor de
la empuñadura y culata con 4 viales. Todos tienen el disco de babosa que brilla del color de la babosa elegida (material
`Chamber`) y los huesos `Chamber` y `Muzzle` como puntos de referencia.

**Ningún arma atraviesa el cuerpo.** `bt_chars.place_gun` prueba posiciones alrededor de la nominal de cada héroe (y,
para la mano izquierda, otros agarres del arma), de la más cercana a la más lejana, y se queda con la primera en la que
las dos manos llegan y **ningún vértice del arma** cae dentro del torso con su ropa, la cabeza con el pelo, las piernas o
los brazos (`body_hits`). Antes, si las manos no llegaban, el arma se acercaba al cuerpo hasta que llegaran: a Pronto el
rifle le quedaba metido en el torso (312 de 728 vértices dentro) y a Trixie y Kord también se les metía un poco. Los
brazos de Pronto (0.37 m) no alcanzan a sostener el rifle delante con las dos manos sin que la culata se le clave, así
que lo lleva **como en su arte oficial**: el rifle en la mano derecha a su costado (la culata pasa por fuera de él) y el
puño izquierdo en la cadera (`left_hand='hip'` en `SPECS`).

## Cómo regenerar

```sh
# todo (mallas + esqueleto + 13 clips + FBX), sin abrir Blender:
/Applications/Blender.app/Contents/MacOS/Blender -b -P unity/art/blender/bt_build.py -- all
# o un héroe:  ... -- kord
```

Desde la ventana de Scripting de Blender (o por MCP): `import bt_build; bt_build.build('eli')` lo deja en la escena
para verlo; `bt_build.export('eli')` lo exporta. Los archivos:

| archivo | qué hace |
|---|---|
| `bt_lib.py` | geometría: lofts (cuerpos), cascos convexos biselados (armas), materiales, pesos 1.0 por hueso |
| `bt_face.py` | cabezas esculpidas por código y rasgos sobre su superficie (ojos, cejas, bocas, labios, dientes), mechones de pelo |
| `bt_heads.py` | la cabeza, la cara y el pelo de cada héroe |
| `bt_slugs.py` | las babosas de los hombros |
| `bt_weapons.py` | los cuatro blasters |
| `bt_chars.py` | proporciones (`SPECS`), ropa y accesorios de cada héroe, colocación del arma sin atravesar el cuerpo e IK de brazos |
| `bt_rig.py` | esqueleto, solver de poses (IK de piernas), los clips y la exportación FBX |
| `bt_build.py` | orquesta todo |

## Esqueleto y animaciones

Los huesos se llaman igual en todos: `Root > Hips > Spine > Chest > Neck > Head`, brazos `UpperArm/LowerArm/Hand .L/.R`,
piernas `UpperLeg/LowerLeg/Foot .L/.R`, y `Chamber`/`Muzzle` bajo `Hand.R`. La pose de reposo es **apuntando** (las dos
manos en el blaster; Pronto con una sola): los clips solo añaden movimiento y el cliente inclina `Spine`/`Chest` con la cámara para apuntar.

| clip | duración | uso |
|---|---|---|
| `Idle`, `CrouchIdle` | 2.0 s | quieto de pie / agachado |
| `RunF/B/L/R`, `CrouchRunF/B/L/R` | 0.8 s (ciclo) | correr / avanzar agachado en las 4 direcciones (se mezclan según la velocidad real) |
| `Air` | 0.8 s | salto y caída |
| `Roll` | 0.5 s | voltereta hacia delante con las rodillas al pecho; dura lo mismo que `roll.json` |
| `Dead` | pose | derrotado (el cuerpo además cae de lado) |

**Agacharse**: la pose agachada se calibra por código para que la cabeza quede al 60 % de la altura (el hitbox de
`crouch.json`), con el arma siempre nivelada. **Rodar**: el cuerpo gira 360° con el torso encogido y se mantiene pegado
al suelo (la voltereta nunca supera el hitbox de 0.6). En Unity el peso de cada clip sale de datos reales del servidor:
velocidad y dirección, altura del hitbox, `active_move == roll` y la postura (ver `RiggedCharacterModel.Pose`).

## Materiales

El FBX solo lleva colores; el cliente los cambia por sus propios shaders según el **nombre**: `Glow*`/`Lens` son
emisivos, `Chamber` cambia con la babosa elegida, `Team` toma el color del equipo (brazaletes), `Metal`/`Steel*` son
metálicos y el resto son mates. El importador de Unity (`CharacterModelImport.cs`) fija rig genérico con avatar propio y
sin compresión de animación.

## Probarlo

`unity/scripts/unity-test.sh playmode` incluye `CharacterModelTests`: cada héroe carga su modelo, las alturas guardan
proporción (Kord > Eli > Trixie > Pronto), agacharse baja al 60 %, rodar da la vuelta y termina de pie, apuntar sube y
baja el cañón. Con `--screenshots DIR` además escribe una galería (`gallery-*.png`) con los cuatro de pie, agachados,
corriendo, rodando y derrotados.
