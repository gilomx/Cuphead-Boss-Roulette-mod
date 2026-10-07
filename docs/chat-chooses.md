# El chat elige

Evento Live del Dashboard en `/config/chat-chooses`. El mod conserva las rondas
y votos; cerrar la página no cancela el evento. TikFinity y el companion deben
estar conectados para recibir votos reales. El simulador admite eventos Chat
para probarlo sin directo.

## Flujo

Antes de iniciar, elegir **Con reto** o **Sin reto**. La elección se guarda en
la configuración del mod y queda bloqueada hasta detener o terminar el evento.
Se inicia desde el mapa, sin otra ruleta cargando ni otro Evento Live activo.

1. Jefe: hasta seis candidatos aleatorios, distintos y disponibles según DLC.
2. Disparo 1: hasta seis disparos reales; no participa Nada.
3. Disparo 2: hasta seis candidatos, sin repetir el primero. Nada entra en el
   sorteo como cualquier candidato, no ocupa un espacio garantizado.
4. Super: siempre I, II, III y Nada, en ese orden. Sus números de voto son
   1, 2, 3 y 4 respectivamente.
5. Amuleto: hasta seis candidatos aleatorios, incluyendo Nada en el sorteo.
   En tierra se excluyen las reliquias Maldita y Divina porque cambian los
   disparos y anularían las decisiones previas del chat.
6. Reto: sólo en Con reto, hasta seis candidatos habilitados y compatibles con
   el jefe; Nada participa en el sorteo.

Los jefes de avión pasan directamente del jefe al amuleto: conservan su
equipamiento nativo y sólo admiten retos compatibles con avión.
Son tres rondas con reto o dos sin reto. La tarjeta del juego muestra Nada
en Disparo 1, Disparo 2 y Súper; internamente mantiene el equipo actual y al
cargar no modifica los disparos, el súper ni los indicadores de cambio de
ninguno de los dos jugadores. Sólo aplica el amuleto y reto elegidos.

El chat escribe únicamente el número mostrado. Cada usuario tiene un voto por
ronda; otro número sustituye el anterior. El botón del jugador indica la
elección actual y lo que sigue. Cierra inmediatamente los votos y revela al
ganador durante 1.5 segundos. Si hay empate, se sortea entre los líderes; si
nadie votó, se sortea entre todos los candidatos mostrados. Los votos y mensajes
anteriores al comienzo de la siguiente ronda no se arrastran a ella.

Al terminar, la tarjeta espera 1.5 segundos continuos con el juego enfocado y
el mapa disponible. Luego entra mediante la misma animación de la ruleta,
incluyendo su inclinación aleatoria y sonido. Si se pierde el foco durante la
espera, ésta se reinicia, incluso si Unity dejó de actualizar en segundo plano.
No se anima anticipadamente fuera de foco ni permite abrir o jugar durante
la espera. Los fotogramas largos tampoco saltan toda la entrada.
La tarjeta muestra las decisiones del chat ya elegidas. Sólo ofrece el ajuste de dificultad y **Jugar**; oculta
los interruptores de reto y carga automática. Conserva la última dificultad
guardada por el jugador. Cambiarla mantiene intacto el resultado. El panel web
recuerda revisarla antes de jugar. No existe cuenta ni inicio automático:
el resultado espera hasta que el jugador confirme en el juego.

Enter/aceptar sobre **Jugar** (también F7 o el atajo de mando) inicia la carga
habitual, con equipamiento, dificultad, HUD, reintentos y regreso al mapa.
F6 permite cerrar y reabrir la tarjeta sin cambiar lo elegido. La navegación
recorre únicamente dificultad y Jugar. Si el mapa no está disponible, conserva
la selección hasta regresar. Al volver de la batalla se libera el evento.
Detener también libera la reserva inmediatamente y cancela una carga aún
pendiente; no cierra el juego ni interrumpe una batalla ya cargada.

## OBS y pruebas

Abrir **Configurar overlay** desde el evento o seleccionar **El chat elige**
en el Diseñador de overlays. La capa tiene posición, tamaño, opacidad, colores,
etapa y animaciones propios en cada perfil. Ancho y
alto se pueden ajustar por separado desde el inspector. Las fuentes comunes de
OBS son `/overlay/vertical` (1080×1920) y `/overlay/horizontal` (1920×1080);
copiar su URL desde el diseñador. Los perfiles guardados con sólo Batalla y
Farmeando taps incorporan la capa sin cambiar las posiciones existentes.

Las opciones y el resultado usan imágenes circulares sobre fondo transparente.
El resumen final no muestra etiquetas. Su jefe mide 240 px frente a los
280 px anteriores; el equipo mide 150 px por artículo. Entre las imágenes del
jefe y del equipo quedan sólo 18 px, sin reservar alturas para los títulos
eliminados. Armas, supers, amuletos y jefes conservan las imágenes originales
del catálogo; el hueco vacío conserva su icono nativo. Los retos usan los PNG
editados de `creator-tools/chat-chooses-modifiers`. Todos llevan el mismo borde
circular fino: 2 px sobre un icono base de 72 px, que escala con el conjunto.
El borde se aplica en la votación y en el resultado, también a los jefes.
**Marco de la imagen** permite cambiar su color sin recolorear la ilustración
ni añadir brillo difuso. El tono predeterminado es el original
`#d3af93`; los colores personalizados guardados se conservan. El resumen usa
ese color normal sin destacar un ganador. Al iniciar la partida o detener el evento desde
el resumen, los retratos salen uno por uno (240 ms, separados por 60 ms)
antes de ocultar la capa. Una nueva ronda espera esa salida y entra con los
datos más recientes. Desactivar animaciones o preferir movimiento reducido
oculta inmediatamente; la cancelación durante las rondas mantiene prioridad.
El overlay no muestra nombres de artículos ni el título del evento; durante
la votación muestra **Votaciones** (72 px) sobre la etapa actual (48 px).
Los retos del evento live en el panel y del overlay usan los mismos archivos
editados, tanto en las opciones de votación como en la selección final. La tarjeta del juego
conserva sus gráficos nativos:

| Reto | PNG en `assets/creator-tools/chat-chooses-modifiers` |
| --- | --- |
| Blanco y negro | `blanco-y-negro.png` |
| Pantalla invertida | `pantalla-invertida.png` |
| Mamá escucho borroso / RGB | `pantalla-rgb.png` |
| Sin Peashooter | `sin-peashooter.png` |
| Sin miniavión (compatibilidad) | `sin-miniavion.png` |
| NO EX | `sin-ex.png` |
| NO DASH | `sin-dash.png` |
| Sin bombas | `sin-bombas.png` |
| Solo balas de miniavión | `solo-miniavion.png` |
| Modo tieso | `modo-tieso.png` |
| Lluvia de tinta | `lluvia-de-tinta.png` |
| Una vida y te callas | `una-vida.png` |
| Disparos rebajados | `mitad-de-dano.png` |

Los círculos usan columnas de su
mismo diámetro, separadas por un espacio pequeño. El marco deja 18 px de margen
interno para las insignias y el movimiento, sin un segundo margen en el escalado.
El perfil horizontal parte de 1360×320. Al cargar el antiguo marco de
1360×460 en su posición inicial se compacta y conserva el original en `.bak`;
los marcos personalizados o bloqueados mantienen sus dimensiones.
El número de opción mide 96 px y aparece centrado en la parte inferior del retrato con una
sombra paralela muy leve, sin contorno negro. La insignia superior muestra sus
votos; **Borde del círculo de votos** configura su color por separado del marco
de la imagen, con blanco `#ffffff` predeterminado y 5 px de grosor frente a los
4 px anteriores. Conserva ese color al destacar líderes y ganadores. Ambas cifras crecen, y cada cambio de
votos desliza la cifra anterior y la nueva dentro de la insignia. Cada ronda
sale de forma escalonada antes de mostrar la entrada escalonada de la siguiente;
los votos recibidos durante la salida se conservan en la nueva vista.
Desactivar animaciones o preferir movimiento reducido aplica los cambios
inmediatamente. El marco de la imagen y el fondo de la insignia cambian suavemente
al color de **Líder y ganador** según entra o cambia cada voto. Los empates
resaltan a todos los líderes con votos; en una ronda vacía no se resalta ninguno
hasta revelar el ganador. El overlay muestra los votos de cada opción, sin
instrucciones, barras, porcentajes ni total general, y ya no muestra cuenta de inicio.
El panel del evento reúne todos sus controles en un contenedor común y muestra
los candidatos en una sola fila compacta.
El formato ancho dispone los candidatos en una fila; los demás usan dos filas.
Fuera de las rondas y de la presentación final la capa queda vacía.
La fuente independiente `/chat-chooses-overlay` sigue disponible por
compatibilidad; `?lang=en` cambia sus textos al inglés.

El inspector permite probar cada ronda con dos a seis opciones, añadir un voto,
mostrar el ganador y el resultado final. El Súper conserva sus cuatro candidatos
en el diseñador. **Mostrar simulación en OBS**
envía estos datos de prueba a la fuente común mientras el diseñador está abierto.
Para emitir votos al evento real, usar **Simular evento → Voto del chat** desde
el Dashboard: un perfil equivale a una identidad y puede cambiar su voto.
**Voto del chat** no muestra Cantidad: cada envío es un mensaje de una persona.
Repetir el mismo número conserva su voto; otro número lo mueve. Para sumar un
voto adicional se usa otro perfil. El retraso permite programar el mensaje.
La lectura de fechas ISO es explícita para aceptar los milisegundos del
simulador en el Mono antiguo de Cuphead, conservando el rechazo de mensajes
anteriores a la ronda y de fechas inválidas.

El paquete **La Pichi Ruleta · Dev** añade **Votos de prueba** al panel del
evento. Al pulsarlo en una ronda abierta, envía entre 50 y 80 mensajes de
usuarios distintos durante diez segundos, con pausas variables. Un candidato
aleatorio recibe una pequeña mayoría del lote para reducir empates; no cambia
los votos que ya existían. El panel muestra enviados, total y tiempo restante.
Se puede repetir al terminar y usar en cada ronda, incluidas las de avión.
Avanzar o detener cancela los mensajes pendientes y evita arrastrarlos a la
siguiente ronda. Usa el conteo y registro reales de mensajes simulados, sin
necesitar un live, foco del navegador ni fotogramas de Unity.

El publicador Dev activa `LauncherDevBuild=true` y el símbolo
`PICHI_LAUNCHER_DEV`. Las compilaciones normales ocultan el botón y rechazan
`operation=test_votes` también en el backend. El fixture local de UI permite
comprobar ambos modos con `CREATOR_TOOLS_DEV_TOOLS=0` para ocultarlo.

- `dotnet run --project CreatorToolsRuntimeTests/ChatChooses/ChatChooses.csproj`
- `dotnet run --project CreatorToolsRuntimeTests/CreatorToolsRuntimeTests.csproj`
- `dotnet run --project TikFinityCompanion/tests/LaPichiRuleta.TikFinity.Tests.csproj`
- `npm run build` desde `creator-tools-ui`.
- `npm run test:chat-chooses-overlay` desde `creator-tools-ui`.
- `node --test scripts/test-chat-test-votes.mjs` desde `creator-tools-ui`.

El publicador canónico es `pwsh -NoProfile -File ./tools/deploy-launcher-dev.ps1`.
Los nuevos assets deben estar en el índice de Git antes de empaquetar. No se
instala en Cuphead original ni se cierra o arranca una sesión para verificarlo.
