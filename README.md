# La Pichi Ruleta

Mod para Cuphead 1.3.4 que traslada al juego la lógica de la ruleta de
`gilomx.com`. Funciona tanto con el juego base como con The Delicious Last
Course: detecta automáticamente si el DLC está disponible y sólo incluye
contenido que esa instalación puede cargar.

La interfaz utiliza las fuentes y los iconos originales del menú de
equipamiento de Cuphead, combinados con los retratos de la ruleta web. Al
terminar el giro, el mod equipa el resultado y carga directamente el combate.

Consulta [CHANGELOG.md](CHANGELOG.md) para ver el historial de cambios.

Para extender los indicadores durante una pelea, consulta
[HUD_INTEGRATION.md](HUD_INTEGRATION.md) antes de modificar las capas o el layout.

## Controles

- `F6`: abrir o cerrar la ruleta.
- Mando: mantener el gatillo izquierdo y pulsar el botón que tengas asignado
  para abrir la Equip Card.
- `↑` `↓`: moverse entre las opciones y la acción principal.
- `←` `→`: cambiar el valor de la opción seleccionada.
- `Enter`: cambiar una opción o confirmar `¡GIRAR!`/`¡JUGAR!`.
- `Esc`: cerrar la tarjeta.
- Cruceta o stick: moverse y cambiar opciones con el mando.
- Botón de confirmar: cambiar una opción o confirmar `¡GIRAR!`/`¡JUGAR!`.
- `ZR`/`RT`/`R2`: volver a girar si ya existe un resultado y la carga
  automática está desactivada.
- `F7`: volver a girar en ese mismo caso desde el teclado.

La combinación de mando se reconoce únicamente cuando `LT` y el botón Equip
proceden del mismo joystick. Equip sin `LT` conserva la Equip Card nativa, y
`Shift` tampoco se confunde con el atajo aunque haya un gatillo sostenido. El
indicador inferior derecho cambia entre `F6` y la combinación física del mando;
cuando se permite volver a girar, muestra `F7` o el gatillo derecho según el
último dispositivo usado.

El giro dura cinco segundos y después detiene, uno por segundo, jefe, armas,
súper, amuleto y reto. La opción `RETO` añade las restricciones de la ruleta
web.

## Retos

El catálogo actual incluye:

- `NO DASH`
- `NO MINIAVIÓN`
- `SOLO BALAS DE MINIAVIÓN`
- `NO DISPARO BOMBAS`
- `SIN PEASHOOTER`
- `NO EX`
- `BLANCO Y NEGRO`
- `MAMÁ ESCUCHO BORROSO`
- `VOLTEADA DE CABEZA`
- `UNA VIDA Y TE CALLAS`
- `LLUVIA DE TINTA`
- `DISPAROS REBAJADOS`
- `MODO TIESO`

Los retos compatibles se filtran automáticamente según el jefe y si el
combate es terrestre o aéreo. `MODO TIESO` simula mantener pulsado el fijado
mientras el personaje toca el suelo y bloquea el dash. Todavía se puede saltar
y dirigir el movimiento en el aire. Durante las salas aéreas de Rey Dado,
conserva el mismo nombre y se adapta bloqueando el miniavión.

## Creator Tools y OBS

Desde el mapa abre `Pausa > LA PICHI RULETA > STREAM OVERLAY`, activa el
overlay y selecciona `COPIAR URL`. En OBS añade una Fuente de navegador, pega
esa dirección y usa el mismo ancho y alto que tu lienzo, por ejemplo
1920 × 1080. El fondo ya es transparente y no necesita CSS personalizado.

`VISTA PREVIA` permite colocar la fuente mientras configuras OBS y se apaga
automáticamente al salir. Tamaño, orden, alineación, opacidad y logo se
actualizan en vivo. El menú está localizado en los doce idiomas de Cuphead y
conserva el formato nativo `ETIQUETA: VALOR`.

El servidor escucha únicamente en `127.0.0.1`: no necesita internet ni una
cuenta. Su puerto preferido es `18081`; si ya está ocupado, el mod elige y
guarda otro automáticamente. Por eso conviene usar siempre `COPIAR URL` en vez
de escribir la dirección manualmente.

`AL REINTENTAR` ofrece dos comportamientos; `REAPARECER` es el predeterminado:

- `MANTENER`: conserva el resultado visible durante el reintento.
- `REAPARECER`: completa la salida y reproduce una sola entrada con el HUD del
  intento siguiente. El logo no aparece entre ambas.

Rey Dado se trata como una sola sesión: los cambios internos de tablero y
minijefe conservan el overlay sin repetir su entrada; un `Reintentar` o
`Reiniciar` real sí respeta la opción anterior. Si `LOGO` está activo, aparece
después de que el HUD termina de salir, durante la pantalla de calificación, y
continúa al volver al mapa.

El servidor también publica `/config`, un panel React para configurar la
ruleta sin recargar la aplicación. Permite activar o excluir retos, preparar un
resultado forzado y consultar el estado global de guardado. Los retos excluidos
siguen girando visualmente, pero no pueden ser elegidos; siempre debe quedar al
menos uno activo en Avión, Tierra y Ambos. Su comportamiento y reglas de
desarrollo están documentados en [Reglas del panel](creator-tools-ui/PANEL_RULES.md).

## Instalación

El ZIP publicado ya incluye BepInEx x64 y el mod. Cierra Cuphead, extrae su
contenido directamente en la carpeta del juego y acepta combinar carpetas y
reemplazar los archivos del mod. El paquete no incluye configuraciones,
partidas, logs ni otros mods.

Para una instalación manual:

1. Instala BepInEx 5 x64 en la carpeta de Cuphead y ejecuta el juego una vez.
2. Compila el proyecto o descarga una versión publicada.
3. Coloca la DLL y la carpeta `assets` juntas en:

   `Cuphead\BepInEx\plugins\GilomxBossRoulette`

4. Inicia una partida guardada, entra al mapa y pulsa `F6`.

El archivo de configuración se crea en:

`BepInEx\config\mx.gilomx.cuphead.bossroulette.cfg`

## Documentación del proyecto

Para añadir artículos a Creator Tools, consulta primero la
[guía del catálogo de interacciones](INTERACTION_CATALOG.md). Define la
arquitectura común, la etiqueta del donador, su seguimiento, el fade de muerte,
la limpieza y las pruebas obligatorias para todos los elementos nuevos.

Las propuestas que todavía no forman parte del mod se conservan en
[Ideas para versiones futuras](FUTURE_IDEAS.md). Este archivo también registra
la investigación y las pruebas necesarias antes de implementar cada idea.

## Compilación

La ruta predeterminada del proyecto es la instalación habitual de Steam:

```powershell
dotnet build -c Release
```

Para otra instalación:

```powershell
dotnet build -c Release -p:CupheadDir="D:\Juegos\Cuphead"
```

## Notas

- Los retos se muestran y aplican durante el combate cuando la opción `RETO`
  está activada.
- Sin el DLC, la animación y el resultado excluyen automáticamente sus jefes,
  armas y amuletos. Las tres posiciones de súper pertenecen al juego base.
- La ruleta puede prestar cualquier objeto del catálogo disponible aunque no se
  haya comprado todavía.
- El equipamiento anterior de ambos jugadores se restaura al ganar o abandonar
  el nivel; al perder y reintentar se conserva el resultado de la ruleta.
- Si la ruleta selecciona al Diablo, después de mostrar y guardar la calificación
  se vuelve al último mapa. El epílogo original sólo se omite en esa partida de
  ruleta; derrotarlo desde el recorrido normal conserva el final completo del
  juego.
- Si la ruleta selecciona a Chef Saleroso, después de mostrar y guardar la
  calificación vuelve directamente al mapa del DLC, sin reproducir su historia
  final. Una victoria normal conserva esa historia. Su HUD de ruleta permanece
  visible hasta que comienza el cambio real a la pantalla de calificación.
- Mientras ese resultado temporal siga activo, la Equip Card no puede abrirse
  desde la pantalla de derrota; vuelve a estar disponible al salir al mapa.
- En el mapa, la Equip Card conserva su entrada y funcionamiento nativos aunque
  Creator Tools esté activo. Sólo el pulso exacto de `LT + Equip` se reserva
  para la ruleta; Equip sin gatillo sigue perteneciendo al juego.
- Los jugadores no pueden caminar por el mapa mientras la ruleta está abierta;
  el movimiento se recupera inmediatamente al cerrarla.
- Enter/Z y el botón de aceptar no atraviesan la ruleta: una puerta situada
  detrás no puede abrir su selector de nivel mientras la card está visible.
- Al ganar o abandonar el combate elegido, Cuphead vuelve a la isla y a la
  entrada nativa de ese jefe, aunque la ruleta se haya abierto en otro punto o
  en otra isla.
- Durante el combate, el resultado de la ruleta aparece en el margen inferior
  derecho y alineado verticalmente con el HUD de vida: tiro A, tiro B, súper,
  amuleto, reto y el nombre del reto. En niveles de avión sólo aparecen amuleto
  y reto. Los iconos usan 70% de opacidad y mantienen estático su primer frame.
  Si participan dos jugadores, esa misma fila se centra en el espacio libre
  entre las vidas/cartas de P1 y P2, sin invadir ninguno de los dos HUD. El
  margen derecho original se conserva sin cambios cuando sólo participa P1.
- Los iconos entran uno por uno con el pulso de selección de la ruleta y el
  texto aparece al final. Los nombres largos ajustan su tamaño sin perder el
  margen derecho. El reto Blanco y negro también desatura este HUD agregado.
- Cada círculo reproduce `impact_01.wav` al aparecer al inicio del intento; el
  texto del reto entra sin sonido.
- Durante el combate la fila utiliza un Canvas aislado para no destellar con
  los parry, pero respeta la visibilidad de `LevelHUD` para que las transiciones
  de iris y los apagados de fase la oculten junto al HUD original.
- Al vencer una copia visual ya preparada permanece fija dentro del HUD nativo,
  se oscurece junto con las vidas y desaparece antes de la pantalla de
  resultados, sin parpadeos ni cambios de posición.
- Al pausar o perder, este HUD queda delante del oscurecimiento y detrás de la
  tarjeta del menú.
- Cuphead usa su armamento de avión automáticamente en los combates aéreos.
- Cada reto utiliza una animación propia de tres frames a la misma velocidad
  visual que las armas, los súper y los amuletos de la tarjeta.
- El reto `Blanco y negro` utiliza un AssetBundle de 5 KB compilado con Unity
  2017.4.9f1 para realizar una transición continua y termina usando el filtro
  nativo del juego. No oculta la pelea ni cambia la preferencia visual guardada.
- La Reliquia Maldita y la Reliquia Divina son resultados separados. Ambas
  equipan el amuleto nativo `charm_curse`, pero durante la inicialización del
  combate la Maldita usa su grado inicial y la Divina su grado máximo.
- Este ajuste es temporal: el mod no desbloquea objetos ni modifica las compras,
  los puntos de mejora de la reliquia o el progreso guardado.
