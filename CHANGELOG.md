# Historial de cambios

## Siguiente versión — Panel de configuración web

- La SPA añade `/dashboard`, una vista operativa bilingüe con estado del motor,
  tarjetas de TikFinity/TikTok, Twitch y YouTube, resumen global, feed de
  eventos recientes y un simulador local. En esta primera etapa las tres
  conexiones aparecen como simuladas: todavía no inicia sesión en plataformas
  ni ejecuta reglas o interacciones reales.
- El mod incorpora el contrato normalizado v1 para eventos de streaming y un
  historial circular en memoria de 500 entradas. Las solicitudes del simulador
  se limitan y sólo se procesan desde `Update` de Unity; el hilo HTTP nunca toca
  objetos del juego.
- El servidor local usa exclusivamente `127.0.0.1:18081` para conservar las
  fuentes de OBS existentes y ya no cambia a otro puerto. Si está ocupado,
  `PANEL DE CONTROL` abre una página local con instrucciones para liberarlo y
  reintentar. El servidor permanece activo aunque el overlay esté desactivado;
  `ESTADO` controla únicamente si la fuente de OBS muestra contenido.
- Modo Molestoso ya no muestra el control para activar o desactivar la
  protección de transiciones. La protección permanece activa y la interfaz se
  conserva comentada en el código fuente para una futura herramienta de
  diagnóstico.
- El menú nativo `LA PICHI RULETA` añade `PANEL DE CONTROL` entre
  `STREAM OVERLAY` y `VOLVER`; al seleccionarlo cierra la pausa, regresa al
  mapa y después abre `/config` en el navegador.
- `/config` y `/dashboard` comparten una SPA en React con una estructura
  persistente preparada para añadir nuevas secciones sin detener conexiones o
  servicios globales.
- El panel está localizado exclusivamente en español e inglés y muestra un
  indicador global bajo el logo para conexión, guardado, confirmación y error.
- La herramienta de forzado usa selects compartidos, inicia cada campo en su
  primera opción compatible y reutiliza el icono vacío del Stream Overlay para
  las opciones `Nada`.
- Los retos se activan o desactivan con los iconos estáticos del overlay. La
  selección se guarda en `Juego/RetosDesactivados` y se aplica al resultado
  aleatorio real.
- Los retos desactivados continúan apareciendo durante la animación de giro,
  pero nunca pueden ser el resultado final.
- Debe permanecer al menos un reto activo en cada categoría: Avión, Tierra y
  Ambos. El panel bloquea el último y el mod valida la misma regla; una
  configuración antigua inválida se corrige al iniciar.
- El encabezado principal se alineó aproximadamente con la zona media del logo
  y el área de forzado reserva espacio inferior para desplegar los selects.
- La navegación lateral elimina el encabezado redundante `Creator Tools` y el
  acceso a `Abrir overlay`; sus cambios de sección ahora animan el tamaño del
  elemento activo. El selector ES/EN flota con transparencia en la esquina
  superior derecha y el pie acredita a gilo.mx con el año actual. Las vistas
  entran con una transición breve, el crédito principal gana jerarquía y los
  estados sincronizado y pendiente usan mensajes más claros; el pendiente
  conserva su color amarillo y ahora parpadea. El corazón del crédito usa rosa
  y el enlace adopta el mismo color mediante una transición al pasar el cursor.
- El código fuente vive en `creator-tools-ui`; `assets/creator-tools/config.*`
  conserva las salidas compiladas que sirve el servidor interno.

### Primeros artículos del catálogo de interacciones

- Las interacciones ya no dependen del tiempo adicional que daba abrir y girar
  la ruleta. Una entrada nativa a cualquier batalla o plataformas se registra
  tanto desde `_OnLevelStart` como mediante un respaldo sobre `Level.Current`;
  puede completar las precargas pendientes y despachar la cola en cuanto el
  nivel esté disponible, sin una espera inicial fija. Carga, pausa, derrota,
  resultados y mapas continúan bloqueando apariciones.
- Una precarga pendiente nunca abre una segunda copia del mismo nivel que el
  jugador está usando. Hilda, La pandilla raíz, Cagney y Hosco y Tosco capturan
  sus prefabs de la pelea real cuando corresponde; las demás capturas aditivas
  permanecen serializadas y protegidas por escena.
- La presentación compartida de nombres ya contiene dos colores de texto: el
  crema existente y un negro cálido alternativo `#181411`. La tabla de niveles
  alternativos queda vacía hasta aprobar qué jefes lo necesitan, por lo que
  este cambio no altera todavía ninguna pelea.
- La misma SPA sirve `/config`, `/config/roulette` y `/config/interactions`.
  El shell, sus proveedores, el indicador global y el selector ES/EN permanecen
  montados al cambiar entre Ruleta e Interacciones.
- La vista nueva reutiliza los tokens y componentes del panel React. El catálogo
  incluye los mini zepelines verde y morado de Hilda Berg y la zanahoria
  teledirigida de La pandilla raíz, además de la semilla azul de Clavel de
  Cagney y la luciérnaga incendiada de Hosco y Tosco; cada uno usa un frame
  original como preview local.
- El servidor expone una cola independiente para pruebas de interacciones. El
  hilo de red sólo encola el ID y el nombre; el controlador ejecuta el efecto
  desde el `Update` principal de Unity.
- El catálogo web usa tarjetas verticales pequeñas, con el preview arriba y el
  nombre debajo. Se retiró el indicador redundante `Ejecutando cola`; los
  estados operativos viven únicamente en la tabla de la cola.
- Debajo del catálogo se muestra una cola operativa amplia. La configuración y
  las pruebas forman una columna lateral ordenada; cada prueba permite indicar
  donador, cantidad y espera en segundos.
- El máximo simultáneo es persistente y configurable de 1 a 20. La cola admite
  hasta 200 entradas, lotes de 50 y esperas de hasta 3600 segundos; además
  separa cada despacho por al menos 0.35 segundos para evitar apariciones
  exactamente simultáneas.
- La prueba aleatoria elige entre todos los artículos disponibles del catálogo y
  seis nombres con intervalos de 1.25 a 3.25 segundos. Su interruptor cambia de
  estado de forma inmediata aunque el juego esté pausado, pero sólo genera
  mientras una partida puede recibir interacciones y nunca acumula un backlog
  automático. Los IDs y ejecutores forman el registro común: todo artículo
  futuro debe aparecer también en la prueba manual y entra automáticamente al
  conjunto aleatorio cuando está disponible.
- Las pruebas aparecen inmediatamente en la tabla aunque Unity esté pausado por
  perder el foco. React las marca como `Esperando al juego` y las sustituye por
  la cola autoritativa cuando el mod incrementa su revisión, sin duplicarlas.
- `hilda_purple_zeppelin` reutiliza `enemyPrefabA` y conserva su disparo
  individual; `hilda_green_zeppelin` reutiliza `enemyPrefabB` y conserva su
  ráfaga nativa. Durante Hilda llaman a `SummonEnemy()`; desde el mapa se
  precargan ambos prefabs de forma aditiva y se reutilizan en cualquier batalla
  o nivel de plataformas.
- `rootpack_homing_carrot` reutiliza el prefab
  `VeggiesLevelCarrotHomingProjectile` de la fase de Psycarrot. Conserva la
  persecución, colisión, daño, vida por dificultad y muerte originales; sólo se
  sustituye el padre por una instancia inerte persistente. No añade TTL: ocupa
  su cupo hasta morir por disparos, jugador, suelo o el respaldo nativo de 1000
  segundos, y se limpia al cambiar de nivel.
- `cagney_homing_plant` reutiliza la semilla nativa azul de Cagney y toda su
  transición a `FlowerLevelVenusSpawn`. En tierra aterriza sobre el piso real;
  en avión o al caer por un hueco cruza completamente el borde inferior, crece
  fuera de cámara y regresa persiguiendo al jugador. Una sola etiqueta permanece
  oculta durante la caída, se transfiere a la planta y aparece con un fade de
  0.45 segundos cuando ésta entra a pantalla. Acompaña sus primeros 0.55 segundos
  de crecimiento antes de fijar la separación.
- Si la semilla azul aterriza sobre una plataforma móvil, la semilla y la
  planta que brota del suelo siguen el punto de impacto durante el crecimiento.
  La mordelona queda libre desde que nace y conserva su persecución nativa. El
  anclaje de la planta de crecimiento se conserva hasta el evento que la hace
  desaparecer, sin heredar escala o rotación de la plataforma.
- Las etiquetas normalizan a positivo la escala mundial heredada. Las plantas
  conservan su orientación nativa izquierda/derecha sin volver espejo el nombre
  del donador.
- La planta de Cagney desplaza su etiqueta 10 px hacia arriba respecto a la
  posición compartida; su separación final queda en 24 px. La luciérnaga la
  desplaza 70 px hacia abajo y queda en -56 px. Ambos ajustes se expresan en
  píxeles de referencia y conservan el mismo tamaño aparente con cualquier zoom.
- Las precargas de escenas nativas ya bloquean también los lifecycle temporales
  de audio, pausa, HUD, jugadores, input y cámara. Así una captura de prefab no
  puede sustituir singletons de la pelea real ni alterar sonido, controles,
  disparos o pausa al entrar a un jefe de forma normal.
- `frogs_firefly` reutiliza `FrogsLevelTallFirefly` con vida, velocidad, daño,
  colisiones, muerte y seguimiento por fases de la dificultad actual. Nace con
  cuerpo y etiqueta completamente fuera del borde derecho, elige una altura
  segura y entra hacia un primer destino antes de repetir sus acercamientos al
  jugador. No tiene TTL agregado y conserva su cupo hasta morir.
- Su escala de cámara vive en un wrapper porque la corrutina nativa restablece
  `localScale.x` al comenzar; así conserva tamaño y colisión sin modificar el
  movimiento.
- Su primer destino horizontal ahora varía entre 78% y 84% del viewport. La
  entrada es más corta que el antiguo 72%, termina más cerca del borde derecho
  y conserva cuerpo y etiqueta dentro del área segura.
- La plantilla inactiva de la luciérnaga se activa únicamente alrededor de su
  `Create` nativo. Esto permite que `Init` conserve `initialMove_cr` y evita que
  el actor quede fuera de cámara ocupando indefinidamente un lugar de la cola.
- Creator Tools no despacha solicitudes con `CupheadTime.GlobalSpeed` en cero,
  incluido el cambio de foco entre el panel y el juego.
- Actor y etiqueta reafirman `ForegroundEffects` durante el gameplay para quedar
  delante de las capas de los jefes. Mientras existe una cobertura visible de
  `PlayerScreenEffectController`, bajan temporalmente a `Enemies` para quedar
  debajo de oscurecimientos, filtros y transformaciones.
- La tinta nativa de Barbasalada también activa esa prioridad cubierta durante
  todo su ciclo visible. Actor, etiqueta y proyectiles marcados quedan detrás
  desde la primera salpicadura hasta terminar el fundido y después recuperan
  `ForegroundEffects` automáticamente.
- Las balas creadas por `FireSingle` y `FireSpreadshot` heredan esa misma
  prioridad dinámica cuando el zepelín pertenece al catálogo. Los disparos de
  Hilda y los proyectiles ajenos no se modifican.
- Las escenas nativas de Hilda, La pandilla raíz, Cagney y Hosco y Tosco se precargan de forma
  aditiva desde el mapa bajo un coordinador único, por lo que nunca quedan dos cargas
  retenidas a la vez. Los lifecycle de cada escena se bloquean sólo durante su
  propia captura y luego se descargan las raíces temporales.
- El clon conserva sprites, controladores, clips, proyectil, efecto de disparo,
  piezas de muerte y propiedades originales de la dificultad actual. El nombre
  del donador es un `TextMeshPro` de mundo independiente con la fuente Memphis:
  captura una sola ancla sobre el sprite, sigue al actor sin saltar cuando
  cambian sus bounds y recibe los filtros de la cámara.
- Al destruirse el actor, el nombre queda inmóvil en su última posición y
  desvanece texto y contorno durante 0.6 segundos. El fade respeta
  `CupheadTime.GlobalSpeed`, por lo que también se congela durante pausa o
  derrota. Este contrato común está documentado en
  [INTERACTION_CATALOG.md](INTERACTION_CATALOG.md) para todos los artículos
  futuros.
- Cada zepelín elige una altura aleatoria dentro del rango seguro 120–610 y
  procura mantener 165 unidades respecto a actores activos. También toma una
  distancia de parada nativa, la desplaza hacia la derecha con variación
  adicional y la limita al rango 390–535; en
  Hilda aplica el valor después de `SummonEnemy()` porque el método original lo
  vuelve a sortear. La variante A mantiene además el contador nativo que alterna
  su proyectil rosa.
- La zanahoria elige cualquier X del borde superior. Después de aplicar la
  escala del nivel se desplaza por sus bounds hasta dejar el pixel más bajo 16
  unidades base fuera de cámara, de modo que cuerpo y nombre entran desde arriba
  sin aparecer de golpe. Prueba hasta 24 X para separarse de jugadores y actores
  donados; los zepelines también reservan la franja vertical ocupada por otros
  tipos del catálogo.
- Todos los actores del catálogo multiplican su escala nativa por la altura de
  cámara relativa al encuadre base de 720 unidades. Así conservan tamaño aparente
  y colisión en jefes con zoom alejado, incluido Chef Saleroso; la etiqueta usa
  el mismo factor para mantener su distancia visual.
- La planta de Cagney usa esa escala en un wrapper y conserva su root interno en
  escala nativa, porque su movimiento original multiplica la velocidad por
  `localScale.x`. Así se corrige el tamaño sin acelerar al enemigo.
- La compensación actual se aplica al actor raíz. Las balas que los zepelines
  crean como roots independientes todavía conservan su escala mundial nativa y
  quedan registradas como ajuste pendiente para cámaras alejadas.
- Ninguna interacción se despacha durante carga, pausa, derrota o cierre del
  nivel. Los actores que ya estaban en pantalla permanecen congelados al perder
  y se limpian al destruirse la escena.
- Se retiró el prototipo portátil que aproximaba el enemigo con una imagen y
  movimiento manual. Los actores jugables se construyen en memoria desde la
  instalación local; los PNG extraídos se usan únicamente como previews web.
- La coordinación vive en `CreatorToolsInteractionController.cs`; el ejecutor
  y la etiqueta del donador están separados bajo `Interactions`. No se añadió
  lógica de canje específica a `Plugin.cs`.

### Protección específica de cambios de fase

- No existe una protección genérica basada en el bloqueo de input del jugador:
  cada jefe se integra mediante señales propias para evitar falsos positivos.
  El interruptor temporal vive en Modo Molestoso, se activa al iniciar el mod y
  permite comparar una sesión con y sin estas protecciones.
- En el Diablo, sólo la transición 1→2 está cubierta. `StartTransform` abre la
  ventana, las apariciones continúan durante 6 segundos jugables y después se
  bloquean. `ZoomOut` activa el bloqueo inmediatamente si aún no ocurrió y
  limpia los actores activos; el despacho se reanuda cuando termina
  `disable_input_cr` y el juego devuelve el control.
- En Chef Saleroso, sólo la transición 1→2 está cubierta por ahora.
  `phase_one_to_two_cr` inicia la ventana; durante 2.5 segundos jugables siguen
  saliendo actores y después se bloquean nuevas apariciones. Cuando las manos
  cierran y cubren la cámara, `AniEvent_HandsClosed` limpia los objetos nativos
  de la fase y ahora también los actores activos de las interacciones. El
  despacho se reanuda en `AniEvent_RestorePlayers`, cuando Cuphead restaura
  arma, súper y control.
- En la transición 2→3 de Chef Saleroso, `OnPhaseThree` se ejecuta justo
  después de que el juego elimina los fuegos: en ese punto se limpian los
  actores activos y se bloquean nuevas apariciones sin agregar una espera
  artificial. El despacho se reanuda cuando termina `phase_two_to_three_cr`,
  después de retirar el fundido y activar el salero saltarín; el programador
  normal decide el siguiente intervalo.
- La transición 3→4 de Chef Saleroso permanece intacta y pendiente de
  observación manual.
- En Granitoviejo, `OldManLevelSockPuppetHandler.OnPhase3` limpia los actores
  activos y bloquea nuevas apariciones en el mismo punto donde comienza la
  destrucción de las marionetas y los elementos de la fase 2. El despacho se
  reanuda sin una espera adicional cuando termina `phase_3_trans_cr`, después
  del iris, al devolver el control e iniciar los ataques de la fase 3.

## 0.6.0 — La Pichi Ruleta (2026-08-18)

Esta versión reúne Modo Tieso, Creator Tools y su overlay para OBS, la
continuidad especial de Rey Dado, las animaciones corregidas de reintento y la
restauración completa de la Equip Card nativa.

### Distribución

- El instalable final se publica como `La-Pichi-Ruleta-0.6.0.zip`.
- Contiene BepInEx x64, la DLL 0.6.0, el README bilingüe y los 433 assets
  rastreados del mod; excluye configuraciones, logs, caché, temporales y dos
  prototipos de audio rechazados.
- El ZIP contiene 457 archivos y su SHA-256 es
  `1F9978FD3E671948177363635B57AA39CF2C15DEA9EC68112A8426C8935A38F0`.

### Equip Card nativa restaurada con Creator Tools

- Se retiraron por completo la activación/desactivación y los parches sobre
  `MapEquipUI`. La Equip Card vuelve a abrir, navegar y cerrar normalmente en
  el mapa aunque Creator Tools esté activo.
- El catálogo web de configuración forzada espera hasta que el mapa y su Equip
  Card hayan terminado de inicializarse antes de consultar el contenido de
  Cuphead. El servidor y el overlay pueden arrancar sin competir con esa ruta
  nativa.
- `F6` y la combinación de gatillo izquierdo + Equip Card abren o cierran la
  ruleta. El atajo de mando se intercepta dentro de la lectura nativa y sólo
  consume ese pulso cuando ambos botones proceden del mismo joystick.
- Equip sin gatillo, `Shift`, `Shift` con un gatillo físico sostenido y botones
  de jugadores distintos continúan llegando íntegros a la Equip Card. Mientras
  la ruleta está visible, su lectura se bloquea únicamente para impedir dos
  tarjetas simultáneas y se libera al terminar la salida.
- La corrección quedó validada manualmente con Creator Tools, Stream Overlay y
  la Equip Card funcionando en la misma sesión. La prueba con mando confirmó
  que `LT + Equip` abre la ruleta y Equip sin gatillo conserva la tarjeta
  nativa.

### Creator Tools: salida de reintento más rápida

- La salida terrestre de `Al reintentar: Reaparecer` baja de 1.33 a 1.05
  segundos con cinco iconos y texto de reto.
- Cada icono sale durante 260 ms con 180 ms entre elementos; el texto espera
  130 ms y sale durante 200 ms.
- Al comenzar el reintento se descarta el contador de revelado de la escena
  anterior. La entrada nueva ya no empieza con cinco elementos para reiniciarse
  cuando el HUD nativo vuelve a contar desde cero.
- Las entradas de combate siguen únicamente el contador publicado por Cuphead;
  sólo Vista previa programa su propia secuencia completa en el navegador.
- Los cambios internos de casilla de King Dice mantienen el overlay visible y
  conservan el HUD; sólo un `Reintentar` o `Reiniciar` explícito reproduce su
  salida y entrada.
- Si la escena nueva termina de cargar antes que la salida rápida, la entrada
  queda en espera hasta que salga el último elemento; ya no corta todos los
  iconos a mitad de la secuencia.
- En victoria, el logo espera un intervalo visible de 80 ms después de ocultar
  por completo el HUD de la ruleta; ya no comparte el mismo frame de salida.
- Al comenzar `WinScreen`, el logo se libera sin cerrar todavía la sesión
  interna: aparece durante la calificación y continúa al regresar al mapa.
- La salida aérea y las demás transiciones conservan sus tiempos anteriores.

### Equip Card nativa: experimento fallido, ya superado

- El punto de control retira la activación/desactivación directa de `MapEquipUI`
  y prueba un postfix sobre `MapEquipUI.get_CanPause()` mientras la ruleta posee
  la entrada o termina su salida.
- La prueba manual falló: con esta implementación la Equip Card nativa ya no se
  abre. Este cambio queda publicado sólo para continuar el diagnóstico y no
  debe considerarse una solución validada.
- Antes del experimento la tarjeta sí abría, pero podía mostrar sprites dañados,
  quedar sin navegación y no cerrar. El fallo de `Esc` que también abre Pausa
  sigue siendo una regresión separada que debe comprobarse después.

### Creator Tools: logo bloqueado durante partidas activas

- El estado del Stream Overlay ahora distingue explícitamente una partida
  activa de un HUD temporalmente oculto.
- Con `Al reintentar: Reaparecer`, la derrota retira el resultado y el nuevo
  intento reproduce su entrada sin mostrar el logo durante el intervalo.
- El logo sólo puede entrar cuando no existe una sesión de batalla activa.
- La salida terrestre de `Reaparecer` tarda ahora 1.33 segundos; la salida
  aérea conserva sus 770 ms y las demás salidas mantienen sus tiempos.

### Modo Tieso: compatibilidad con Rey Dado

- `StiffMode` queda aprobado funcionalmente para todos los jefes terrestres:
  fuerza el fijado mientras el jugador está en el suelo, bloquea el dash y
  conserva el control horizontal durante los saltos.
- En los subniveles aéreos `DicePalaceFlyingHorse` y
  `DicePalaceFlyingMemory`, el HUD conserva `MODO TIESO` y la restricción cambia
  únicamente a bloquear la transformación en miniavión.
- La prueba manual del recorrido quedó aceptada. La animación definitiva usa
  `locked_01..03.png` en la ruleta; el HUD conserva el primer frame y Creator
  Tools usa su icono estático de 82 x 82.
- `challenge.stiff_mode` ya usa sus traducciones finales en los doce idiomas;
  el nombre inglés visible es `LOCKED MODE`.

### Creator Tools localizado, centrado y con logo

- Las cinco etiquetas de retos nuevos y las 20 etiquetas revisadas de Creator
  Tools ya están activas en los doce idiomas de Cuphead. El menú convierte el
  resultado a mayúsculas para conservar el estilo nativo.
- Las filas de configuración se dibujan como una unidad `ETIQUETA: VALOR`, con
  ajuste de ancho y centrado óptico por glifos. Esto evita diferencias de
  posición entre idiomas, cambios de fuente dentro de una palabra y el
  parpadeo del valor anterior al cambiar una opción.
- Se añadió `LOGO` antes de `COPIAR URL`. Cuando el overlay está activo pero el
  HUD no debe mostrarse, aparecen el nombre del mod y su etiqueta; al alternar
  con la vista previa, la salida termina antes de comenzar la siguiente
  entrada. Ambos recursos flotan de forma leve e independiente y la etiqueta
  `MOD` usa 1.4 veces su tamaño original.
- La verificación actual conserva temporalmente `Ctrl+F8` para recorrer idiomas
  y fuerza Reina Abeja, Lanzaguisantes, segundo disparo vacío, Súper I,
  Afiladora y Lluvia de tinta. Ambos interruptores deben apagarse antes de una
  build pública.

## 0.5.131 - Personaje de la ruleta en la pantalla de calificación (2026-08-16)

- Antes de devolver el equipo prestado, el mod ahora conserva qué personaje
  terminó realmente la batalla. La pantalla de calificación recibe de nuevo ese
  dato y, en una partida individual, verifica que Cuphead, Mugman o Ms. Chalice
  haya quedado activo.
- La disponibilidad del DLC confirmada ahora también se conserva para las
  consultas internas de Cuphead, no sólo para el catálogo de Creator Tools.
  Una respuesta falsa transitoria impedía cargar el arte de Ms. Chalice en la
  calificación y su animación de regreso al mapa, aunque su objeto estuviera
  correctamente activo. Las instalaciones sin DLC no se modifican.
- La auditoría de llamadas de Creator Tools deja 25 IDs públicos pendientes
  para los doce idiomas: cinco retos nuevos y 20 cadenas visibles dentro del
  juego. Se retiraron ocho textos heredados del menú manual que ya no se llama.

## DLC al recuperar el foco (2026-08-16)

- Una detección positiva del DLC ahora permanece válida durante toda la sesión
  de Cuphead. `DLCManager` podía devolver temporalmente falso después de cambios
  de foco o escena y hacer desaparecer contenido ya confirmado.
- La misma detección positiva protege las llamadas nativas a
  `DLCManager.DLCEnabled()`, necesarias para cargar los recursos visuales del
  DLC en resultados y en el mapa.
- Las instalaciones sin DLC continúan usando únicamente el contenido base. El
  cambio sólo evita que una propiedad ya confirmada desaparezca a mitad del
  proceso.

## Creator Tools: menú integrado y overlay (2026-08-16)

- La entrada nativa del mapa se llama ahora `LA PICHI RULETA`. Su hub compacto
  usa la tarjeta principal de Opciones y abre `STREAM OVERLAY`; la configuración
  del overlay conserva la tarjeta grande, coloca `VISTA PREVIA` antes de
  `AL REINTENTAR`, añade `VOLVER` y mantiene `COPIAR URL` centrado como acción
  independiente. La vista previa se apaga al salir de esa pantalla.
- Los nombres de reto demasiado largos en el overlay reducen primero su tamaño
  y, si todavía no caben, se ajustan en varias líneas dentro del ancho seguro.

## Creator Tools: nombre y reintentos (2026-08-15)

- La entrada del menú y la tarjeta principal ahora se llaman `CREATOR TOOLS`;
  la función concreta se identifica como `OVERLAY DE RULETA` y abre su propia
  página nativa, dejando preparado el contenedor para futuras herramientas.
- Se agregó `AL REINTENTAR` con `REAPARECER` como comportamiento predeterminado
  para grabaciones y `MANTENER` para directos. Mantener evita que el overlay
  salga, pierda iconos o repita la entrada entre derrota y reintento; todavía
  sale normalmente al ganar, abandonar o sustituir la sesión de ruleta.
- La plantilla de traducción añade seis IDs por la nueva navegación y opción;
  Creator Tools pasa de 21 a 27 cadenas pendientes.

## Preparación de la nueva localización (2026-08-15)

- Los doce documentos activos separan ahora los 29 IDs ya aprobados de los
  32 pendientes: cinco retos nuevos y 27 cadenas visibles de Creator Tools.
- Se añadió `translations/LOCALIZATION_STATUS.md` como índice temporal.
- Las entregas históricas permanecen intactas; al aprobar los doce idiomas se
  volverán a fusionar las tablas y se retirará esta separación.

## Cierre de la secuencia de aceptacion (2026-08-15)

- Se desactivo `ForceNewChallengeSequenceForTesting` despues de completar la
  revision de arte; ninguna secuencia de retos queda forzada en giros normales.
- Los cinco retos nuevos permanecen habilitados dentro del catalogo aleatorio.

## Prueba de aceptación - secuencia de retos nuevos (2026-08-15)

- RGB, 180°, Lluvia de tinta, Daño -50% y HP.1 quedaron habilitados en el
  catálogo normal después de completar funcionalidad y arte.
- Una secuencia temporal de aceptación fuerza exactamente un reto por giro y
  se repite en este orden: RGB, 180°, Lluvia de tinta, Daño -50% y HP.1. Cada
  resultado conserva un jefe compatible elegido al azar.
- Los cinco selectores individuales `Force...ForTesting` permanecen apagados;
  la secuencia dedicada evita que la prioridad histórica deje siempre el mismo
  reto. Debe desactivarse antes de preparar una build pública.

## Desarrollo experimental - conjunto completo del overlay (2026-08-15)

- Creator Tools ya tiene recursos estáticos exclusivos para las 34 imágenes
  únicas del resultado: `Nada`, nueve armas, tres supers, nueve amuletos y doce
  retos. La ruleta y el HUD dentro del juego conservan sus archivos originales.
- Se añadieron al overlay los cinco retos nuevos entregados como `trippy`,
  `flip`, `hp1`, `ink` y `halfdamage`; sus rutas internas normalizadas son
  `rgb_01`, `upside_down_01`, `hp1_01`, `inkrain_01` y `halfdamage_01`.
- El navegador redirige ahora armas, supers, amuletos y retos a
  `assets/creator-tools`, además del tratamiento especial ya existente para
  `Nada`. Todos los iconos entregados conservan su tamaño y transparencia.

## Desarrollo experimental - arte animado de retos nuevos (2026-08-14)

- Se integraron las secuencias finales de tres fotogramas para `DESFASE RGB`,
  `180°`, `HP.1`, `LLUVIA DE TINTA` y `DAÑO -50%`, todas en PNG RGBA de 80 × 80.
- Los archivos aportados como `trippy`, `flip`, `hp1`, `ink` y `halfdamage`
  quedaron normalizados como `rgb_01..03`, `upside_down_01..03`,
  `hp1_01..03`, `inkrain_01..03` y `halfdamage_01..03`.
- `RouletteData` conecta ahora los cinco retos con su animación normal de tres
  frames a 12.5 fps. Los interruptores experimentales no cambiaron: Lluvia de
  tinta permanece habilitada y RGB, 180°, HP.1 y Daño -50% siguen dormidos.

## Desarrollo experimental - Creator Tools overlay (2026-08-13)

- La prueba aislada confirmó que insertar la fila del mod en el índice 4 dejaba
  visible `ELIMINAR JUGADOR 2`: `LevelPauseGUI.OnPause()` usa ese índice fijo para
  ocultar la entrada nativa. Durante ese método el mod restaura temporalmente el
  arreglo original y después recupera su fila, conservando la lógica multijugador
  de Cuphead. Creator Tools quedó activado nuevamente para validar el arreglo.
- El nuevo acomodo usa seis filas nativas para los ajustes y la acción inferior
  centrada para `COPIAR URL`. Esto elimina el texto largo que desbordaba la
  tarjeta y aprovecha mejor el diseño compartido por Visual y Sonido.
- `VISTA PREVIA` aparece inmediatamente después de `CREATOR TOOLS` y se apaga
  automáticamente al salir de la pantalla. Si el servidor estaba apagado, la
  vista previa activa Creator Tools para que no quede encendida sin hacer nada;
  el navegador reproduce tanto su entrada completa como su salida. `OPACIDAD`
  conserva el rango 25-100%, ahora en pasos de 5 puntos porcentuales.
- Se generó un ZIP con las 34 imágenes únicas que el overlay utiliza. Para
  reemplazarlas se recomienda un lienzo PNG RGBA uniforme de 256x256: cubre los
  tamaños visibles 92x92/184x184 y el pulso máximo aproximado de 198 px.
- Se verificaron los atlas originales de Cuphead: armas, supers y amuletos sólo
  existen a 80x80 (el vacío a 73x73). Las copias de 72x72 del mod pueden ganar
  ocho píxeles recuperando el original, pero no hay una fuente oficial grande
  suficiente para 2X.
- Al terminar el combate, el overlay sale en el mismo orden y con el mismo
  ritmo de entrada: iconos del primero al último cada 280 ms y texto al final,
  conservando el pulso de entrada. La secuencia se cancela sin saltos si el HUD
  reaparece durante una transición. La separación pasó de -4/-8 px a 8/16 px
  para 1X/2X.
- `Nada` ahora usa un PNG estático de 73x73 extraído del sprite nativo
  `equip_icon_empty_0001`, conservando su transparencia y volviendo blanca la
  silueta como hace el HUD. HTML, CSS y JS se sirven sin caché para que OBS
  reciba cambios de presentación al recargar; los PNG conservan su caché.
- Se añadieron nueve iconos de armas de 82x82 exclusivamente para Creator Tools
  en `assets/creator-tools/weapons`. El overlay los usa sin cambiar sus medidas
  visibles de 92x92/184x184; ruleta y HUD conservan los archivos originales.
- `TAMAÑO` ahora ofrece `1X`, `1.5X` y `2X`; el punto medio usa medidas
  proporcionales de 138 px para iconos, 12 px de separación y 51 px para el
  texto de respaldo. El valor decimal se serializa con cultura invariable.
- `ALINEACIÓN` ya no mueve únicamente el bloque exterior: iconos y texto del
  reto comparten el borde izquierdo, el centro o el borde derecho seleccionado.
- Queda documentado como pendiente el Palacio de Dados: Creator Tools todavía
  inicia una sesión y repite la entrada del overlay en cada cambio de minijefe.
  Debe conservar una única sesión durante toda la cadena y salir sólo al
  completarla o abandonarla.

- Se agrego la primera implementacion de `Creator Tools`: un servidor local
  integrado que entrega una fuente transparente para OBS y empuja su estado por
  WebSocket, sin sondeo periodico ni aplicacion auxiliar.
- HTTP y WebSocket comparten `127.0.0.1:18081`. Si el puerto esta ocupado, el
  mod avanza secuencialmente hasta encontrar uno libre entre 100 candidatos y
  conserva el resultado para la siguiente sesion.
- La pagina recibe el snapshot inmutable del HUD, los mismos iconos terrestres o
  de avion, el progreso de revelado, el texto localizado y los ajustes en vivo.
  El texto del reto se renderiza desde Unity con la misma fuente/material nativos
  que usa la fila del HUD y se sirve como PNG transparente de alta resolucion.
- Se agrego `LA PICHI RULETA` inmediatamente debajo de `OPCIONES` en el menu de
  pausa del mapa. Abre directamente la pantalla Visual real de Cuphead y presta
  temporalmente sus filas a activacion, tamano, orden vertical, alineacion,
  opacidad, vista previa y copia de URL. Por ello conserva exactamente el fondo,
  ruido, tipografia, flechas, colores, movimiento, sonidos y controles nativos,
  sin dibujar una tarjeta propia. Al cancelar se restauran todas las opciones
  visuales originales antes de volver a la pausa. No modifica el menu de pausa
  de los combates.
- La prueba tecnica aprobo compilacion sin errores, entrega HTTP, handshake
  WebSocket, envio de estado y fallback de `18081` a `18082`. Falta validar
  visualmente la nueva fila, la tarjeta, el texto nativo y un combate completo.

## Desarrollo experimental - reto de dano reducido validado (2026-08-13)

- Se implemento `DANO -50%` para combates terrestres y de avion. Los impactos
  ofensivos del jugador aplican temporalmente la mitad del multiplicador nativo
  sin alterar el dano recibido ni los datos permanentes de armas.
- La ruta cubre disparos normales, armas de avion, ataques EX y supers.
- Se aprobo manualmente la cadena completa del Rey Dado: tablero, minijefes,
  transiciones internas y combate final conservaron correctamente el reto.
- Se guardo `assets/modifiers/halfdamage.png`, aportado por el usuario, como
  referencia visual provisional.
- El reto y todos sus selectores de prueba quedaron desactivados. No aparecera
  en la ruleta hasta contar con su secuencia animada definitiva de tres frames.

## Desarrollo experimental - salpicaduras nativas y validación final (2026-08-13)

- Las salpicaduras de Lluvia de tinta fuera de Barbasalada usan ahora los 71
  fotogramas recortados originales, el pivote individual de cada frame, las cinco
  familias grande/pequeña, espejo aleatorio, 12 fps y las duraciones reales del
  juego. Se eliminó la copia anterior estirada mediante GUI/render texture.
- Las manchas se renderizan como `SpriteRenderer` temporales en la capa nativa
  `Effects`, siguen correctamente la cámara y se destruyen al terminar el clip o
  limpiar el reto. La lluvia, oscuridad y sus tiempos no fueron modificados.
- Barbasalada conserva su overlay real mediante `PirateLevelSquidInkOverlay.Hit()`
  y se aprobó la posición final del calamar adicional detrás del puente y el mar.
- Pasaron las pruebas de Goopy (pausa, reintento, abandono y victoria),
  Barbasalada, Perritos Pilotos en todas sus rotaciones, Hilda Berg cooperativo y
  la cadena completa del Rey Dado.
- Lluvia de tinta queda habilitada como resultado normal de la ruleta. Se
  desactivaron el reto y jefe forzados de las pruebas.

## Desarrollo experimental - integración nativa con Barbasalada (2026-08-13)

- Lluvia de tinta conserva su lluvia adicional y su calamar de entrada contra
  Barbasalada, pero los impactos usan directamente el overlay nativo del juego.
  Las manchas, escala, oscurecimiento y tiempos son por ello los originales.
- Las gotas y los impactos del reto respetan el alfa del oscurecimiento nativo y
  dejan de verse por delante de la tinta.
- La animación inicial conserva su posición y escala aprobadas, usa el orden de
  render nativo y ahora se coloca en el plano de juego `z = 0` para quedar detrás
  del mar frontal y del puente. Esta última profundidad queda pendiente de una
  confirmación visual en la siguiente sesión.
- Barbasalada y el reto permanecen forzados únicamente para esa prueba. Los tres
  interruptores temporales deben desactivarse antes de una compilación pública.

Este documento resume los cambios funcionales de La Pichi Ruleta. Las
versiones corresponden al número mostrado por BepInEx al cargar el mod.

## Desarrollo experimental - Abejita aprobada y cierre de pruebas (2026-08-13)

- Reynita Abejita fue aprobada manualmente con Lluvia de tinta. Su arena valida
  el comportamiento de las gotas en geometria terrestre con plataformas
  moviles, complementando el piso sencillo ya aprobado en Goopy.
- Se desactivaron el jefe, amuleto, reto y selector forzados para entregar el
  repositorio en un estado seguro. Lluvia de tinta y los demas retos nuevos no
  aparecen ni ejecutan efectos hasta que otro agente reactive expresamente sus
  interruptores de desarrollo.
- Quedan pendientes Rey Dado completo, cooperativo, decidir la convivencia con
  el calamar nativo de Barbasalada, una regresion breve posterior y el icono
  animado final.

## Desarrollo experimental - prueba con Cagney antes de Hilda (2026-08-12)

- Se cambio el siguiente objetivo y se forzo temporalmente `Levels.Flower` con
  Lluvia de tinta para revisar primero a Cagney. Hilda Berg queda pendiente como
  la siguiente prueba aérea convencional. El jefe forzado debe volver a `false`
  al terminar cada objetivo.
- La convivencia con el efecto nativo de Cagney fue aprobada manualmente y no
  necesita una excepcion. El objetivo forzado avanzo a `Levels.FlyingBlimp`
  para continuar con Hilda Berg.
- La prueba de Hilda fuerza especificamente Reliquia Maldita con grado de
  maldicion `0` en todos los giros; no usa la secuencia alternada con Reliquia
  Divina. Armas y Super permanecen aleatorios.
- Hilda y su combinación con Reliquia Maldita fueron aprobadas manualmente. El
  amuleto forzado se desactivo y el siguiente objetivo avanzo a `Levels.Bee`
  para revisar la geometria compleja de plataformas de Reynita Abejita.

## Desarrollo experimental - prueba terrestre con Goopy (2026-08-12)

- Se forzo temporalmente `Levels.Slime` con Lluvia de tinta para validar en un
  piso terrestre sencillo los impactos, orden de capas, pausa, derrota,
  reintento, knockout y limpieza al regresar al mapa. El jefe forzado debe
  volver a `false` al terminar la prueba.
- La prueba completa fue aprobada manualmente. Se desactivo nuevamente el jefe
  forzado y se conservaron como aceptados el flujo terrestre basico, pausa,
  derrota/reintento, knockout, resultados y limpieza al volver al mapa.

## Desarrollo experimental - prueba de Perritos Pilotos (2026-08-12)

- Se reactivo temporalmente Lluvia de tinta, se forzo como reto y se forzo
  `Levels.Airplane` para repetir exclusivamente Los Perritos Pilotos. La prueba
  revisara direccion, cobertura, densidad, tamaño, colisiones y limpieza de las
  gotas durante todas las rotaciones. Los tres interruptores deben volver a
  `false` antes de crear otro paquete publico.
- La prueba completa fue aprobada manualmente: la lluvia se mantuvo correcta
  durante todo el encuentro y sus rotaciones. Se desactivo nuevamente el jefe
  forzado; Lluvia de tinta permanece activada y forzada para continuar la matriz
  con jefes aleatorios.

## 0.5.130 - 2026-08-12

- Se corrigio la apertura de la ruleta con mando despues de regresar al mapa
  desde cualquier combate, tanto iniciado por la ruleta como de forma normal.
  Cuphead destruye el prompt del mapa al cargar un nivel; el mod recreaba la
  fila al volver, pero conservaba el cache de distribucion anterior. Por eso la
  fila nueva mostraba solamente el glifo nativo `B`, aunque el atajo real seguia
  siendo gatillo izquierdo + Equip. Ahora se limpian todas las referencias y el
  token de distribucion al detectar un prompt destruido, y la combinacion
  `ZL/LT/L2 + Equip` se reconstruye completamente en cada mapa.
- El prototipo de `Lluvia de tinta` y su selector forzado quedaron desactivados
  temporalmente mientras se trabajan correcciones para la version publica. La
  implementacion y sus assets permanecen disponibles para retomar las pruebas;
  mientras el interruptor principal siga apagado tampoco se instalan sus
  parches, se crea su componente ni se ejecuta su ciclo de actualizacion.

## Desarrollo experimental - Lluvia de tinta (2026-08-11)

- Se agrego el primer prototipo jugable de `Lluvia de tinta` para niveles de
  tierra y avion, actualmente habilitado y forzado para continuar sus pruebas.
- Se redibujo el icono provisional con tres bolitas nativas agrupadas pero en
  carriles distintos y deliberadamente no alineados. Asi parecen tres gotas
  simultaneas en vez de tres posiciones de una sola gota; todas conservan una
  trayectoria claramente inclinada hacia abajo a la izquierda. Los rastros
  apuntan hacia arriba a la derecha para que ya no parezcan una caida vertical;
  se actualizaron sus tres frames preparados, aunque la interfaz actualmente
  usa el primero.
- Se integraron los 36 frames originales de las gotas, la capa y grupos de
  manchas de pantalla, y las cuatro variantes originales de impacto de siete
  frames. Los assets se exportaron como Sprite para evitar contaminacion del
  atlas.
- El oscurecimiento progresivo replica los incrementos y tiempos del pulpo de
  Capitan Barbasalada. La capa oscura ahora se dibuja sobre las manchas; esto
  elimino el halo blanco de sus bordes. La escala final aceptada para las
  manchas es 0.65 horizontal y 0.115 vertical.
- Las gotas ahora usan velocidad inicial y gravedad para caer en curvas
  diagonales hacia la izquierda. La velocidad y densidad siguen pendientes de
  ajuste mediante pruebas.
- La prueba en Beppi revelo que la deteccion de `Level_Ground` descartaba los
  colliders configurados como trigger, aunque el proyectil original de Cuphead
  usa `OnTriggerEnter2D`. Ahora los acepta y muestra la animacion nativa
  `OnDeath` al impactar. Tambien se agrego diagnostico limitado del linecast
  (ruta, tipo, capa, tag, trigger y punto) para identificar diferencias entre
  arenas si alguna sigue sin reconocer el suelo. La correccion se valido
  manualmente en Beppi y el impacto ahora se muestra al tocar el piso.
- Se redujo la escala visual del impacto contra el suelo al 60% porque el
  recorte original se veia demasiado grande respecto a las gotas del reto.
- La capa oscura y las manchas de pantalla dejaron de componerse al final con
  `OnGUI`. Ahora un command buffer las dibuja en la camara justo antes de los
  efectos de imagen, igualando el orden del overlay nativo de Barbasalada. El
  grano, polvo, rayaduras y filtros configurados por Cuphead se aplican tambien
  sobre la tinta; si el shader transparente nativo no esta disponible, el mod
  conserva automaticamente el render anterior como respaldo.
- Las gotas y los impactos tambien entran ahora por ese mismo compositor. Esto
  conserva el orden original completo (gotas, impactos, manchas y oscuridad) y
  evita que los elementos tardios de `OnGUI` aparezcan encima del oscurecimiento
  o alteren visualmente el tamaño de la animacion que toca al jugador.
- El dibujo directo de cada mancha dentro del command buffer se descarto porque
  deformaba los sprites altos y podia repetir el ultimo frame/material. Ahora
  `Graphics.DrawTexture` compone primero el grupo completo en una RenderTexture
  transparente del tamaño exacto de la pantalla, usando las dimensiones y UV de
  la ruta aceptada. El compositor recibe una sola imagen plana, la coloca detras
  del velo y despues Cuphead aplica la pelicula; se acepta un fotograma de
  latencia para preservar forma, variedad y escala sin artefactos.
- La gravedad aleatoria de cada gota aumento siete puntos porcentuales, de
  `0.15-0.21` a `0.22-0.28` alturas visibles por segundo cuadrado. Los limites
  simultaneos aumentaron de `2/3/4` a `3/4/13` en Facil/Normal/Experto; los
  intervalos y probabilidades de oleada doble permanecen sin cambios.
- Se agrego una introduccion nativa del pulpo antes de `Ready/Wallop`. El reto
  ya no extiende la espera original de un segundo ni parchea `LevelIntroTime`.
  La aceleracion `10/3` fue rechazada manualmente. Los dibujos vuelven a sus 24
  fps nativos y se reproduce la secuencia completa: 18 frames de entrada, 3 de
  apertura, 22 mostrados del ciclo de 16 y los 29 de salida reconstruidos.
- La animacion ya no obliga al actor a recorrer casi una pantalla en 0.225
  segundos. `Ready/Wallop` comienza en el tiempo normal de Cuphead mientras el
  pulpo continua, y sus propios frames nativos lo sacan de la vista sin cambiar
  de velocidad ni aplicar movimiento artificial al transform.
- El pulpo ahora comienza desde `PlayerStatsManager.LevelInit()`, cuando el
  nivel ya existe pero Cuphead todavia conserva su presentacion de carga. Esto
  mantiene el inicio independiente del fundido. La prueba inmediata se ajusto
  con una espera explicita de 1.0 segundo: el primer sprite y el sonido nativo
  de entrada empiezan juntos despues de esa espera, sin modificar
  `LevelIntroTime`, `Ready/Wallop`, la duracion ni los 24 fps. La ventana de
  bolitas tambien se desplaza completa porque sigue siendo relativa al inicio
  del calamar. El callback de transicion terminada queda solo como respaldo para
  escenas especiales.
- Se reforzo la correccion de la animacion duplicada. El ID de `Level.Current`
  no es estable mientras Cuphead construye la escena, por lo que ya no define
  una sesion nueva. Dos banderas explicitas registran que el primer `LevelInit`
  ya configuro la batalla y que el pulpo ya fue programado; llamadas posteriores
  y el respaldo de transicion no pueden reiniciarlo. Derrota/reintento, salida o
  una batalla realmente nueva limpian ambas banderas.
- El primer guard de sesion revelo otro caso: el reintento nativo no llama
  `ClearInkRainChallengeSession()`, por lo que podia conservar la marca y
  bloquear tanto el pulpo como la lluvia del siguiente intento. La ruta comun
  `ResetChallengeVisualsForReload()` ahora limpia tambien Lluvia de tinta detras
  del fundido oscuro; el siguiente `LevelInit` crea una sola sesion nueva y las
  llamadas repetidas dentro de esa misma recarga siguen siendo ignoradas.
- La secuencia usa 59 sprites originales exportados del atlas de Barbasalada
  (`entrance`, apertura/ciclo de ataque y `leave`) y sus sonidos nativos de
  entrada, destape, ataque en bucle y salida. No instancia al enemigo real ni
  sus colliders, vida, reglas del jefe o proyectiles fisicos.
- Se corrigio la relacion entre la animacion y las primeras bolitas usando los
  eventos exactos de Barbasalada. La entrada dura 0.75 segundos, pero
  `OnEnterAnimationComplete` ocurre en el frame 17, a los `16/24 = 0.6667`
  segundos: ahi comienza el audio de ataque y se crea inmediatamente la primera
  bolita desde la posicion inicial de `InkOrigin`, `(46, 368)`. El clip que destapa el bote y su
  pop comienzan a los 0.75 segundos, y el ciclo abierto comienza a los 0.875.
  El ciclo abierto si anima ese mismo hijo: la curva estaba comprimida en el
  bloque streamed del clip y por eso no aparecia en `m_PositionCurves`. Se
  decodifico su ruta CRC `2960652783` (`InkOrigin`) y sus 16 polinomios cubicos
  originales de X/Y. El mod ahora los evalua, incluida la interpolacion entre
  frames, para que despues del destape las bolitas sigan exactamente la boquilla
  que se mueve con el tentaculo. El juego usa un origen animado, no un segundo
  punto distinto.
- Las siguientes bolitas del pulpo respetan la cadencia nativa: 0.21 segundos
  en Facil y 0.12 en Normal o Experto. Los intervalos vencidos se procesan en
  orden para conservar la cantidad aunque cambie la tasa de frames. El pulpo
  deja de emitir al entrar a su salida, a los 1.7917 segundos de esta version
  corta, igual que la corrutina original se detiene al abandonar Attack. Se
  mantiene el maximo de seguridad de 20 simultaneas y la secuencia visual total
  de 3 segundos para no prolongar el inicio aprobado. Cuando se va,
  el limite vuelve inmediatamente a `3/4/13` y no aparece otra oleada hasta que
  las sobrantes bajan del limite correspondiente; despues regresan las oleadas,
  probabilidades e intervalos normales desde la zona superior derecha. Esto no
  cambia la proteccion ya aprobada: solo pueden entintar
  al jugador un segundo despues de que `Level.PlayAnnouncerBegin()` inicia
  `Wallop`. La secuencia reproduce el sonido de destape
  `level_pirate_squid_attack_pop` y usa las velocidades y gravedad nativas de
  Barbasalada para que las bolitas se vean salir hacia arriba desde el calamar.
  La lluvia regular conserva su aparicion aprobada una vez terminada la
  introduccion.
- Las pruebas manuales de ventanas fijas (`1.5-2.0`, `0.5-1.0`, `0.3-2.6`,
  `1.0-2.6`, `0.52-2.5`) fueron reemplazadas por los eventos y estados nativos.
  A partir del corte de Attack, la lluvia restante conserva su movimiento y las
  futuras oleadas vuelven al origen superior habitual.
- El actor del pulpo reproduce tambien su balanceo nativo: recorre 20 unidades
  verticales con `easeInOutSine(PingPong(t, 1))`, un ciclo completo de dos
  segundos. El movimiento se convierte a la escala visual aprobada del mod y
  desplaza junto con el dibujo al `InkOrigin`; encima de ese balanceo se aplica
  la curva animada del hijo, igual que en el prefab original, sin mover por
  separado las gotas ya creadas.
- La lluvia de tinta ahora respeta la pausa real de Cuphead. El juego usa
  `CupheadTime.GlobalSpeed = 0` sin detener necesariamente `Time.deltaTime`, por
  eso antes las gotas seguian avanzando. Mientras esta pausado no cambian
  posicion, gravedad, edad, animaciones, impactos, tinta ni temporizadores. Al
  reanudar se desplazan los relojes absolutos por toda la duracion de la pausa,
  evitando saltos, salvas acumuladas o que la proteccion venza en el menu.
- La primera prueba confirmo por registro que la introduccion y sus gotas se
  ejecutaban, pero el pulpo no era visible: la camara todavia en transicion
  descartaba el dibujo directo del sprite. La ruta temporal por `OnGUI` lo hizo
  visible, pero demasiado limpio por quedar encima de la pelicula. Ahora se
  rasteriza en una textura transparente propia y se compone antes de los efectos
  de Cuphead, por lo que recibe su grano y color sin modificar las capas de
  gotas, impactos, manchas y oscurecimiento.
- La introduccion del pulpo ahora aparece centrada horizontalmente, anclada al
  piso, y al doble de su escala visual anterior (`0.55` a `1.10`).
- Se retiro la textura de pantalla intermedia del pulpo despues de observar una
  apariencia palida y temblor. Esa ruta mezclaba dos veces la transparencia y
  mostraba el fotograma preparado al final del cuadro anterior. Ahora los 59
  frames usan su pivote inferior central original en un `SpriteRenderer` real,
  colocado frente a la escena: recibe una sola vez la pelicula de Cuphead y no
  introduce latencia. El compositor aprobado de la tinta no fue modificado.
- Los 59 frames recortados del pulpo se reconstruyeron sobre su lienzo nativo
  fijo de `620 x 620`, usando el `textureRectOffset` original de cada Sprite.
  Esto evita que el centro aparente cambie con las distintas dimensiones de
  cada recorte. El actor tambien queda ligado a la camara durante la secuencia,
  evitando desplazamientos relativos mientras termina la entrada al nivel.
- El ancla inferior del pulpo bajo de `0.04` a `-0.04` en coordenadas del
  viewport para que los extremos recortados de los tentaculos queden ocultos
  debajo de la pantalla, sin cambiar su centro horizontal ni escala.
- Las bolitas no pueden oscurecer la pantalla, crear manchas ni reproducir el
  sonido de impacto hasta un segundo despues de que
  `Level.PlayAnnouncerBegin()` inicia el anuncio `Wallop`. Durante esa gracia
  siguen visibles, se mueven y chocan con el suelo, pero atraviesan al jugador.
  `_OnLevelStart()` proporciona el mismo segundo de respaldo en escenas
  especiales que no llaman al anunciador, sin ampliar la gracia normal.
- Pendiente conocido: reproducir y corregir los errores de Lluvia de tinta en
  toda la cadena del Palacio de Dados. Cada escena interna debe conservar una
  sola sesion, sin repetir la introduccion, duplicar lluvia/compositores, perder
  la gracia de daño ni limpiar el reto antes de vencer a Rey Dado.
- Pendiente de diseño: decidir que debe ocurrir si Lluvia de tinta sale contra
  el Capitan Barbasalada, cuyo combate ya usa el calamar, sus proyectiles y su
  overlay nativos. Antes de activar el reto publicamente se elegira entre
  excluir ese jefe, ocultar solo la introduccion adicional o permitir ambos
  sistemas deliberadamente; no se desactivara el ataque nativo sin esa decision.
- Tambien falta validar la introduccion en tierra, avion, reintento y
  cooperativo; despues ajustar su escala, ubicacion o duracion mediante pruebas
  si es necesario.
## 0.5.129 — 2026-08-08 (desarrollo RGB y 180°)

- Se implementó la primera versión experimental del reto `HP.1` para niveles
  terrestres y de avión. Cada jugador inicia y permanece con un máximo de una
  vida; las curaciones no pueden aumentarla, pero Corazón y Corazón Doble
  conservan su penalización de daño y los amuletos/reliquias mantienen sus
  demás efectos.
- En cooperativo, ambos jugadores conservan una vida; se permite la entrada
  tardía de P2 sin restarle la única vida al jugador donante y una reanimación
  vuelve con una vida.
- El Súper II de Ms. Chalice no concede escudo durante `HP.1`. Su corazón se
  muestra temporalmente en blanco y negro, semitransparente y con un efecto
  sencillo de televisor antiguo antes de desaparecer; el jugador continúa
  vulnerable.
- Se confirmó en el código nativo que `Anillo de Corazón`, `Reliquia Maldita`
  y `Reliquia Divina` comparten `HealerCharmParticleEffect`. Durante `HP.1`,
  cada partícula de una curación rechazada recibe el mismo shader blanco y
  negro, opacidad, jitter, parpadeo, scanlines y desvanecimiento del corazón
  rechazado de Chalice, mientras el límite de una vida bloquea la curación.
- La primera prueba mostró que la parte dominante de la animación era el
  `HealerCharmSparkEffect` raíz y permanecía a color. El hook final decora ese
  objeto en el retorno de `Effect.Create()` y cada partícula en su `Awake()`;
  así todo el conjunto lleva el efecto desde su primer fotograma visible.
- La siguiente prueba detectó un corazón todavía rosa y que el jugador podía
  quedar como silueta blanca. El efecto raíz ya no se destruye al terminar el
  glitch: queda invisible y Cuphead conserva su corrutina hasta restaurar el
  material del personaje y eliminarlo de forma nativa. Durante la animación,
  el material glitch también se reafirma cada frame para impedir que el
  Animator vuelva a colocar temporalmente el corazón rosa.
- Como aún quedaba una capa rosa, la captura visual dejó de limitarse a
  `SpriteRenderer`: ahora procesa cualquier `Renderer` descendiente y copia la
  textura del material original al shader glitch. Esto cubre las capas de
  partículas o malla sin convertir en blanco y negro la pantalla completa. La
  corrección compila y carga, pero su resultado visual queda pendiente de la
  siguiente sesión porque el usuario no alcanzó a probar esta última build.
- Se agregó el icono temporal `HP.1`, el shader correspondiente y el tercer
  recurso al AssetBundle de shaders. Durante el desarrollo se forzó
  `HP.1 + Anillo de Corazón` para validar la curación rechazada; ese selector
  temporal quedó retirado después de completar la matriz.

- La matriz final de `HP.1` quedó aprobada en tierra, avión y cooperativo:
  ambos jugadores comienzan con una vida, la reanimación y la incorporación
  tardía de P2 regresan con una vida, los reintentos conservan la regla y salir
  de la sesión restaura la vida normal.
- Se validaron Corazón, Corazón Doble, Anillo de Corazón, ambas reliquias,
  corazones del Palacio de Dados, Súper II de Ms. Chalice y deseos de Djimmi.
  Corazón y Corazón Doble conservan sus penalizaciones nativas de daño.
- Las curaciones rechazadas de Anillo/Reliquias ahora reproducen el sonido
  dedicado `hp_one_rejected_parry.wav`, aprobado tanto para tierra como avión.
- Se añadió una guardia global de Djimmi para toda pelea iniciada por la
  ruleta. El deseo no se consume ni se borra y vuelve a funcionar al entrar
  manualmente a un nivel; la prueba Normal dio 3 HP con ruleta y 9 HP sin ella.
- `HP.1` y todos sus selectores forzados quedan desactivados hasta integrar el
  icono animado definitivo. La implementación permanece compilada y lista.
- Se preparó la primera build del reto experimental `180°`, compatible con
  niveles terrestres y de avión. La cadencia final de prueba espera 0.25
  segundos con el combate normal y gira el fotograma de 0 a 180 grados durante
  0.45 segundos, sin alterar cámara, controles, posiciones, física ni hitboxes.
- El giro es plano. Una prueba de acercamiento dinámico eliminó las esquinas
  negras, pero recortaba demasiado el combate y fue descartada. La versión
  actual conserva el cuadro central a escala normal y extiende sus píxeles de
  borde sólo sobre los huecos del giro. El HUD nativo y la fila del mod giran
  con el gameplay; pausa y resultados permanecen derechos.
- La primera prueba reveló que `_FlipY = 1` cancelaba la inversión vertical del
  quad y dejaba el resultado como espejo horizontal. La corrección adicional se
  eliminó: el giro geométrico ahora invierte ambos ejes y termina de cabeza.
- Una variante intermedia añadió espejo horizontal para conservar la posición X,
  pero fue rechazada porque el resultado se percibía reflejado. La versión final
  elimina por completo la escala de espejo y aplica únicamente una rotación
  plana: a 180 grados ambos ejes se invierten y los lados intercambian lugar de
  forma natural.
- Al perder con `180°`, la tarjeta permanece invertida. Tanto `Reintentar` como
  `Salir al mapa` conservan la orientación durante el fundido y restablecen el
  efecto solamente cuando el fader ya está completamente negro.
- `Pausa → Volver a empezar` y `Pausa → Salir al mapa` comparten el mismo reset
  oculto. El mapa y cada intento nuevo aparecen normales sin mostrar el giro de
  regreso. RGB y Blanco y negro conservan su comportamiento anterior.
- La victoria mantiene su presentación independiente: sostiene el K.O. invertido
  durante 1 segundo y después vuelve visiblemente a normal en 0.45 segundos
  antes de la calificación.

- El whoosh sintético y el silbido hueco de objeto/cartoon generados durante las
  pruebas fueron rechazados. El reemplazo activo usa el efecto de violín cartoon
  proporcionado por el usuario, comprimido a 0.450 segundos, normalizado a
  -5 LUFS/-0.2 dBTP y reproducido a volumen 1.0 por el canal de Efectos. La licencia
  del MP3 original debe verificarse antes de una publicación pública.
- Después de probarlo en combate, el violín de `180°` subió otros 2 dB sin tocar
  ningún otro sonido. Sigue conectado exclusivamente al grupo SFX nativo, por lo
  que lo regulan Principal + Efectos y no el volumen de Música.
- Una segunda prueba pidió todavía más presencia: el WAV recibió otros 2.5 dB
  percibidos y mayor compresión, conservando un pico limitado a -0.3 dBTP.
- El último ajuste aumenta aproximadamente otros 1.5 dB percibidos y limita el
  pico a -0.2 dBTP, manteniendo aislado este cambio al audio de `180°`.
- El ajuste final de escucha añade 0.75 dB al WAV ya procesado, con limitador
  transparente para conservar el pico y sin alterar duración ni enrutamiento.
- Una última afinación añade otros 0.5 dB al sonido del giro y conserva el
  limitador de techo 0.988, su duración de 0.450 segundos y el canal de Efectos.
- La entrada y el regreso tras K.O. de `180°` duran 0.45 segundos y conservan
  su audio sincronizado. Derrota, reintento y salidas al mapa usan un reset
  instantáneo oculto bajo negro total, sin reproducir un giro visible.
- Reintentar comienza nuevamente normal. Las escenas internas diferentes del
  Palacio de Dados conservan el giro terminado sin repetir la entrada.
- `LevelPauseGUI.Restart()` ya no limpia el efecto en su prefijo, porque eso hacía
  visible un giro repentino al elegir Volver a empezar. Conserva la orientación
  durante el fundido nativo y espera `SceneLoader.OnFadeInEndEvent`; sólo cuando
  el fader está totalmente negro restablece los retos visuales y los mantiene en
  cero hasta detectar la nueva instancia de nivel.
- Tras aprobar las pruebas, `EnableUpsideDownChallenge` y
  `ForceUpsideDownChallengeForTesting` volvieron a `false`. El reto `180°`
  queda terminado pero dormido, igual que RGB, para activarlo en una
  actualización futura sin exponerlo todavía en la ruleta pública.
- El placeholder de `180°` fue reemplazado por un icono transparente de 80 × 80
  sin texto: una flecha crema de trazo negro, inclinada como un aro en perspectiva
  para sugerir un giro plano de 180 grados. Sigue usando un solo frame y el
  arte animado final permanece pendiente.
- El simple cambio de dirección del icono tampoco expresaba profundidad y fue
  reemplazado por un tercer diseño: el aro se adelgaza al alejarse por arriba y
  su punta nace en el arco posterior, crece y sale hacia la vista en primer plano.
- El tercer diseño aún se percibía demasiado horizontal; el arte activo conserva
  toda su perspectiva y se inclina 28 grados para reforzar la rotación diagonal.
- Durante la combinación Cagney + reto RGB, se omite el `TouchFuzzy` nativo
  del polen para que no cree corrutinas de RGB y desenfoque que compitan en
  segundo plano. El método nativo es exclusivamente visual; el impacto, daño y
  cualquier otro comportamiento del ataque no se modifican.
- La combinación fue validada manualmente: el polen conserva su daño normal y
  no produce saltos adicionales de color ni desenfoque. Los interruptores que
  habilitan/fuerzan RGB y el selector de Cagney volvieron a `false` para que el
  desarrollo permanezca inactivo en la versión pública.
- Las partidas de ruleta ya no muestran el aviso nativo para cambiar de arma,
  tenga Tiro B equipado o en `Nada`. El equipo temporal establece en `false`
  los avisos terrestre y de avión; el snapshot restaura los valores originales
  al ganar o volver al mapa. Las partidas normales de Cuphead no se modifican.
- Se añadió el reto experimental `RGB`, compatible con jefes terrestres y de
  avión, reutilizando `ChromaticAberrationFilmGrain`, el postproceso nativo que
  activa el polen de Cagney.
- El combate comienza normal durante 1.5 segundos y el desfase entra suavemente
  durante 1.25 segundos, exactamente con los tiempos de `Blanco y negro`.
- La prueba exagerada regresa al movimiento sinusoidal después de descartar los
  rebotes irregulares. Usa amplitud base 32, velocidad vertical 10 y movimiento
  horizontal a velocidad 7.3 y amplitud 70% con desfase de un cuarto de ciclo.
- Rojo usa 120% de fuerza, verde 60% y azul 90% en dirección contraria. Sus
  recorridos verticales máximos son 38.4, 19.2 y 28.8 respectivamente.
- El pulso conserva el ritmo nativo de 2.2 s y usa el 70% del desenfoque:
  comienza en +0.7, alcanza +1.12 y regresa al valor original.
- Los dos efectos se aplican en `LateUpdate`, sin acumular corutinas. Se guardan
  y restauran los vectores RGB y `BlurGamma.blurSize` al reintentar, ganar,
  abandonar, cambiar de escena o descargar el mod.
- La cámara, posiciones, controles e hitboxes no se mueven; la sensación de
  movimiento procede únicamente de las muestras RGB desplazadas y el blur diagonal.
- Durante RGB, la fila del HUD de la ruleta se coloca en `LevelHUD.Canvas` para
  recibir el mismo desfase y desenfoque que vidas y cartas. Los demás retos
  conservan el overlay independiente que evita el parpadeo del parry.
- Durante una carga transitoria conserva su estado igual que `Blanco y negro`;
  al aparecer una instancia de combate realmente nueva vuelve a ejecutar la
  entrada normal, sin apagarse a mitad de la carga.
- La funcionalidad fue aceptada y marcada como terminada. Por decisión del
  proyecto, los interruptores para habilitarla y forzarla quedan ambos en
  `false`: RGB no aparece en la ruleta de este build, pero su implementación se
  conserva lista para activarse después.
- Se añadió un placeholder estático transparente de 80 × 80 con el texto `RGB`.
  `ModifierEntry.FrameCount` permite que use un solo frame sin parpadear; los
  retos existentes conservan sus tres frames.
- El nombre de desarrollo es `RGB` en los 12 idiomas hasta recibir las
  traducciones y el arte finales.

## 0.5.128 — 2026-08-08

- Las cinco etiquetas bajo los iconos de equipo usan un único tamaño de
  fuente por idioma: parten de 14 y bajan juntas hasta 11 si alguna no cabe.
- Las etiquetas permanecen en una sola línea dentro de su área de 98 × 23;
  se conserva un ancho seguro de 94 para evitar roces con los iconos vecinos.
- La medición usa la fuente real de Cuphead, se guarda en caché y sólo se
  recalcula al cambiar el idioma o reconstruir el estilo.
- Se validaron manualmente en ruso `СПЕЦАТАКА` e `ИСПЫТАНИЕ` sin recortes ni
  saltos de línea. No fue necesario acortar las traducciones aprobadas.
- El atajo temporal de idiomas `Ctrl+F8` vuelve a quedar desactivado.

## 0.5.127 — 2026-08-08

- Se conserva la corrección inglesa `Angel and Demon` del jefe secreto.
- Se retiró por completo el diagnóstico temporal de F6 después de comprobar
  que la ruleta abrió y giró sin rechazos internos.
- `Ctrl+F8` vuelve a quedar desactivado para la distribución pública.
- El instalable listo para pegar incluye `README-LEEME.txt` en inglés y español
  en la raíz del ZIP.
- Se generó `dist/Las-Pichi-Ruleta-0.5.127.zip`, con BepInEx x64 y
  todos los assets, sin configuraciones, logs ni plugins ajenos.
- `README-LEEME.txt` se convirtió a texto plano real, sin marcadores de
  encabezado, negritas, cursivas, código ni separadores de Markdown.

## 0.5.125 — 2026-08-07 (build temporal para capturas)

- La entrada inglesa del jefe secreto `Graveyard` no expone texto utilizable
  aunque Cuphead pueda presentar su nombre mediante arte localizado.
- Para inglés, la ruleta usa ahora el respaldo textual `Angel and Demon` antes
  de consultar ese recurso; así nunca vuelve al `Ángel y Demonio` español.
- Los otros jefes y las demás rutas nativas de localización permanecen sin
  cambios. `Ctrl+F8` continúa activo temporalmente para las capturas.

## 0.5.124 — 2026-08-07 (build temporal para capturas)

- El nombre grande del jefe consulta primero la clave nativa
  `<nivel>WorldMap`, igual que `MapDifficultySelectStartUI` de Cuphead.
- Esto corrige a Ángel y Demonio: `Graveyard` no ofrece el nombre localizado y
  antes activaba el respaldo español incluso al probar inglés;
  `GraveyardWorldMap` sí corresponde al nombre que usa el juego.
- La antigua clave `<nivel>` permanece como respaldo de compatibilidad para
  cualquier entrada excepcional. `Ctrl+F8` sigue activo sólo para las capturas
  de GameBanana.

## 0.5.123 — 2026-08-07 (build temporal para capturas)

- Se reactiva temporalmente `Ctrl+F8` para recorrer los idiomas de Cuphead y
  tomar capturas para GameBanana; la primera pulsación selecciona inglés.
- El idioma original continúa restaurándose al cerrar el juego y no se guarda
  mediante `SettingsData.Save()`.
- Antes de producir el siguiente paquete público debe volver a establecerse
  `EnableLanguageTestShortcut = false`.

## 0.5.122 — 2026-08-07

### Ruleta, HUD y cierre de pruebas

- El fondo `assets/card/roulette-card.png` se sustituyó por la tercera versión
  entregada por el usuario, manteniendo intactos el tamaño 595×668 y todo el
  layout. El atajo interno `Ctrl+F8` queda deshabilitado para la compilación normal.
- Las entradas `Nada` de disparos, súper y amuleto apuntan ahora a
  `equip_icon_empty_0001`; la Equip Card reproduce los tres frames nativos igual
  que el reto desactivado, tanto durante el giro como después de detenerse.
- Tiro A continúa siendo obligatorio. Tiro B tiene exactamente 20 % de
  probabilidad de quedar en `Weapon.None`; en el otro 80 % elige un disparo no
  vacío distinto de Tiro A.
- Sólo en el HUD de batalla, cualquier resultado vacío se convierte en una
  silueta blanca del círculo nativo conservando su alpha. Si la extracción del
  atlas falla, se genera un círculo blanco segmentado seguro; la ruleta no cambia
  de color y conserva su animación nativa.
- `impact_01.wav` se reprocesó con +20 dB antes de un limitador rápido a
  −1 dB: la sonoridad integrada pasa aproximadamente de −20.01 a −12.4 LUFS
  y el volumen medio de −20.2 a −11.4 dB. La ganancia de runtime vuelve a
  `1.0`; el clip sigue en el grupo SFX nativo, así que Principal o Efectos en
  cero lo silencian completamente.
- El regreso de Saltbaker al mapa busca primero la puerta nativa
  `MapBakeryLoader` antes de los fallbacks de cocina. Así se ejecuta
  `SetPlayerReturnPos()` y no se reutiliza la posición guardada del jefe anterior.
- Se desactivaron el giro forzado que alternaba Saltbaker y el Diablo y el atajo
  temporal `Ctrl+F8`; la ruleta vuelve a elegir jefes aleatoriamente.
- Verificación local: compilación con 0 errores y 0 advertencias; la DLL
  compilada e instalada comparte SHA-256 `689C1EF0FE1D528F19B5ACA0C94BD23B09B6C367BB05BB0B57DB397FEE82100C`; el WAV
  procesado e instalado comparte `F44C76F5A12C7356E608915BC48D010C9613B2FCE4FD0D658800DD3EC63BAB98`.
- Después de sincronizar otra PC, se recompiló e instaló la misma 0.5.122 y se
  generó `dist/Gilomx-Boss-Roulette-0.5.122-BepInEx-x64.zip`, listo para pegar
  sobre Cuphead. El paquete contiene 122 archivos, los 18 componentes core de
  BepInEx y sólo este mod; no incluye configuración, logs, caché ni plugins
  ajenos. ZIP SHA-256:
  `8BB029AA69DD723E943C167AAB80A7862B01E8B82C1D8B673DF1B4B8D6ECF64E`.
- `tools/build_challenge_gifs.py` genera previews GIF de los siete retos usando
  sus tres PNG reales y la cadencia exacta de la Equip Card: 12.5 FPS u 80 ms
  por frame. Cada archivo repite 42 ciclos completos, dura 10.08 segundos y se
  detiene en el tercer frame. El paquete
  `dist/Gilomx-Boss-Roulette-Challenge-GIFs-0.5.122.zip` contiene los siete GIF
  y un README; SHA-256:
  `EBB36B6FF7AE0EA8E2BA5AA3365ACA134FFEA84B79D02D832514796CBED3A71E`.

## 0.5.121 — 2026-08-07

### Regreso a la puerta del jefe y bloqueo de interacción del mapa

- Antes de cargar el combate, la ruleta cambia PlayerData.CurrentMap a la isla
  nativa del jefe elegido usando las listas de mundos de Level.
- Al crear los jugadores de ese mapa, un prefijo de Map.CreatePlayers() busca
  el MapLevelLoader real del jefe y ejecuta SetPlayerReturnPos(). Así Cuphead
  coloca desde el primer frame a uno o dos jugadores en la entrada del jefe, no
  en el lugar donde se abrió la ruleta.
- Hay fallbacks para la entrada del Rey Dado, el casino del Diablo y la cocina
  de Saltbaker; Ángel y Demonio vuelve al mapa del DLC.
- Mientras la card está visible o terminando su animación de salida, el
  Update() nativo de AbstractMapInteractiveEntity queda bloqueado. Enter/Z
  sigue controlando la ruleta pero ya no activa una puerta situada detrás.
- La prueba manual confirmó tanto el regreso a la puerta elegida como el bloqueo
  de Enter/Z sobre una entrada situada detrás de la ruleta.
- La build candidata a publicación deja desactivados los selectores forzados de
  jefe, reto, reliquia y cartas, además del atajo interno Ctrl+F8 de idioma.
- La compilación terminó con 0 errores y 0 advertencias. El DLL compilado e
  instalado comparte SHA-256
  EB6E2FD62CCF76365E0D7C488CE644E16958D25C242919D549ACB11D179772F8;
  BepInEx confirmó la carga de 0.5.121.

## 0.5.120 — 2026-08-07

### Texto regional y nuevo fondo de la ruleta

- En `SpanishAmerica`, el reto `NoPeashooter` cambia de
  `SIN DISPARO NORMAL` a `SIN PEASHOOTER`.
- `SpanishSpain` conserva `SIN DISPARO NORMAL`; una tabla regional hereda
  el resto del español base y sobrescribe únicamente esta cadena.
- `assets/card/roulette-card.png` se reemplaza por el nuevo fondo de 595×668
  proporcionado por el usuario. No cambian coordenadas, tamaños, textos,
  animaciones ni lógica de la tarjeta.
- La compilación terminó con 0 errores y 0 advertencias. El DLL instalado coincide con el compilado (SHA-256 `88DA9E3E6F61E0F9B0AF9DF0D71A3BECB5ECD06F88D96F30B07ED1DA0FE87067`) y BepInEx confirmó la carga de 0.5.120.

## 0.5.119 — 2026-08-07

### Propuestas revisadas de español activas

- `SpanishSpain` y `SpanishAmerica` entregaron los mismos 29 valores; ambos
  enums siguen compartiendo un catálogo único, ahora actualizado con esa
  propuesta revisada.
- Entre los cambios visibles están `FÁCIL`, `NO MINIAVIÓN`,
  `SOLO BALAS DE MINIAVIÓN`, `SIN DISPARO NORMAL` y los nombres de reto en
  mayúsculas.
- La tabla activa queda registrada en
  `translations/translation_spanish_shared.md`; las dos entregas completas
  permanecen archivadas por separado para comprobar su procedencia.
- La comparación automática confirma 29/29 valores y cero diferencias en los 12 enums. La compilación terminó con 0 errores y 0 advertencias.
- El DLL compilado e instalado comparte SHA-256 `A9F36AC54D9FC37544720481184E0EE7850A13E0E9BF6D138EAE0D4EC1E1E528`; BepInEx confirmó `Gilomx Boss Roulette 0.5.119`.

## 0.5.118 — 2026-08-07

### Doce entregas de traducción revisadas

- Se validaron los 12 archivos entregados: cada uno contiene exactamente los
  29 IDs públicos, sin campos vacíos ni identificadores adicionales.
- Las tablas revisadas de inglés, francés, italiano, alemán y coreano sustituyen
  sus versiones anteriores; se activan además ruso, polaco, portugués de Brasil,
  japonés y chino simplificado.
- `Localization.Languages` selecciona ahora una tabla propia para sus 12
  valores. Sólo un idioma futuro o desconocido utiliza el español de respaldo.
- Las propuestas revisadas de `SpanishSpain` y `SpanishAmerica` se conservan
  para auditoría, pero ambos enums continúan usando exactamente el español
  compartido que ya tenía el mod, según la decisión del proyecto.
- Las entregas completas quedan en `translations/review_by_language/`; los
  diez catálogos no españoles activos se sincronizan en `translations/`.
- La comparación automática verificó 29/29 valores y cero diferencias en cada
  tabla no española; la compilación terminó con 0 errores y 0 advertencias.
- El DLL instalado coincide con el compilado (SHA-256 `8F7D15A1E7598E47672DF2C9B5E52D3A651B89E9FE3500E25CAC95603064B1A9`) y BepInEx confirmó `Gilomx Boss Roulette 0.5.118`.

## 0.5.117 — 2026-08-07

### Etiquetas de configuración en una sola línea

- El área izquierda de las filas de configuración crece de 250 a 360 unidades,
  aprovechando el espacio libre antes del valor alineado a la derecha.
- Los rótulos dejan de usar salto de línea; `CARICAMENTO AUTOMATICO` cabe completo
  sin reducir la tipografía y `DISATTIVO` conserva su posición.
- Las traducciones aprobadas permanecen sin cambios.

## 0.5.116 — 2026-08-07

### Traducción coreana aprobada

- Se incorporan exactamente los 29 textos entregados en
  `translation_korean.md`.
- `Localization.Languages.Korean` selecciona su tabla propia en la Equip Card,
  prompts del mapa y HUD de reto.
- Inglés, francés, italiano, alemán, coreano y ambos españoles quedan activos;
  los idiomas pendientes continúan usando el respaldo español.
- La entrega exacta se conserva en `translations/translation_korean.md`.

## 0.5.115 — 2026-08-06

### Español compartido para ambas regiones

- `SpanishSpain` y `SpanishAmerica` seleccionan explícitamente el diccionario
  español original del mod.
- Las dos variantes comparten exactamente las mismas 29 cadenas visibles; no
  hay adaptación regional.
- La decisión queda registrada en
  `translations/translation_spanish_shared.md` para evitar que una traducción
  futura cambie una variante por separado.

## 0.5.114 — 2026-08-06

### Traducción alemana aprobada

- Se incorporan exactamente los 29 textos entregados en
  `translation_german.md`.
- `Localization.Languages.German` selecciona su tabla propia en la Equip Card,
  prompts del mapa y HUD de reto.
- Inglés, francés, italiano y alemán quedan activos; los idiomas pendientes
  continúan con el respaldo español.
- La entrega exacta se conserva en `translations/translation_german.md`.

## 0.5.113 — 2026-08-06

### Traducción italiana aprobada

- Se incorporan exactamente los 29 textos entregados en
  `translation_italian.md`.
- `Localization.Languages.Italian` selecciona su tabla propia en la Equip
  Card, prompts del mapa y HUD de reto.
- Inglés, francés e italiano quedan activos; los demás idiomas continúan con
  el respaldo español.
- La entrega exacta se conserva en `translations/translation_italian.md`.

## 0.5.112 — 2026-08-06

### Traducción francesa aprobada

- Se incorporan exactamente los 29 textos entregados en
  `translation_french.md`.
- `Localization.Languages.French` selecciona ahora su propia tabla para la
  Equip Card, prompts del mapa y HUD de reto.
- Inglés y francés quedan activos; los demás idiomas continúan usando el
  respaldo español mientras esperan aprobación.
- La entrega exacta queda registrada en
  `translations/translation_french.md`.

## 0.5.111 — 2026-08-06

### Primera traducción aprobada: inglés

- Se incorporan exactamente los 29 textos entregados en
  `translation_english.md`.
- Cuando `Localization.language` es `English`, la Equip Card, los prompts del
  mapa y el HUD de reto cambian inmediatamente a inglés.
- Los textos internos o de la interfaz antigua no se traducen ni amplían el
  alcance público.
- Los otros once idiomas conservan el respaldo español hasta recibir una tabla
  aprobada.
- La entrega original queda registrada en
  `translations/translation_english.md`.

## 0.5.110 — 2026-08-06 (herramienta temporal)

### Selector de idioma para revisar traducciones

- `Ctrl+F8` recorre los 12 valores reales de `Localization.Languages`,
  comenzando siempre por inglés.
- El selector cambia `Localization.language`, por lo que actualiza a la vez la
  interfaz de Cuphead, sus nombres nativos y el mod mediante el evento oficial.
- Una etiqueta temporal muestra el idioma elegido durante tres segundos y el
  log registra tanto el idioma de prueba como el original.
- El idioma original se restaura en `OnApplicationQuit()` y `OnDestroy()`; el
  selector no llama a `SettingsData.Save()`.
- La herramienta completa se desactiva cambiando únicamente
  `EnableLanguageTestShortcut` a `false` antes de publicar.
- `TRANSLATION_REVIEW_TEMPLATE.md` permite entregar cada idioma conservando
  los IDs estables de las etiquetas.
- Una auditoría de las rutas que realmente se dibujan deja la plantilla en 29
  textos visibles. Se excluyen los `status.*`, la interfaz antigua, nombres de
  equipo no escritos, `challenge.none`, configuración, logs y el aviso temporal
  de idioma.

## 0.5.109 — 2026-08-06

### Base segura para la localización

- Los retos usan ahora `ModifierId`; ninguna regla de gameplay depende de una
  frase española ni cambia al traducir su nombre visible.
- Los mensajes internos usan `RouletteStatus`. Se eliminó la búsqueda de la
  palabra `PARTIDA` que decidía la acción principal de la tarjeta.
- `ModLocalization` centraliza los IDs de interfaz, detecta
  `Localization.language` y escucha cambios de idioma en caliente.
- Tarjeta, prompts del mapa, HUD y reto persistente resuelven el texto desde el
  servicio. El snapshot de combate guarda el ID del reto, no su traducción.
- Armas, supers y amuletos normales reutilizan sus nombres oficiales mediante
  `WeaponProperties.GetDisplayName()`; `Nada` y las dos reliquias conservan
  entradas propias.
- El español visible permanece idéntico mientras se revisa
  `LOCALIZATION_TRANSLATIONS.md`; las propuestas de los otros once idiomas no
  están activadas todavía.

## 0.5.108 — 2026-08-06

- Se desactiva `ForceTestBoss` después de validar toda la cadena de Rey Dado.
- La ruleta vuelve a elegir jefes normalmente; el selector genérico permanece
  disponible, pero dormido, para pruebas futuras.

## 0.5.107 — 2026-08-06

- Las victorias de los minijefes del Palacio de Dados ya no ocultan el HUD al
  comenzar `SceneLoader.CurrentlyLoading`, antes del fundido.
- Durante esas cargas internas, la fila pasa temporalmente al canvas de
  `SceneLoader` como primer hijo, debajo del fader nativo.
- La victoria final contra Rey Dado conserva su ruta aceptada mediante
  `LevelHUD.Canvas`; no se modifica su fundido.

## 0.5.106 — 2026-08-06

- El selector temporal de jefe se generaliza mediante `ForceTestBoss` y
  `ForcedTestBossLevel`.
- Para la prueba actual, todos los giros eligen `Levels.DicePalaceMain` (Rey
  Dado); armas, súper, amuleto y reto continúan girando normalmente.

## 0.5.105 — 2026-08-06

- Durante la victoria de Chef Saleroso, el HUD pasa al canvas nativo de
  `SceneLoader` como primer hijo, debajo de su fader.
- El fundido negro de tres segundos ahora cubre el HUD exactamente como cubre
  la imagen del juego, sin perderlo cuando `LevelHUD.Canvas` se desactiva.
- Los demás jefes conservan su ruta anterior mediante `LevelHUD.Canvas`.

## 0.5.104 — 2026-08-06

- Se forzó temporalmente Chef Saleroso para validar el salto de la historia
  final y la permanencia del HUD hasta calificaciones.

## 0.5.103 — 2026-08-06

- Una victoria de ruleta contra Chef Saleroso omite la historia final del DLC
  después de la calificación y vuelve directamente al mapa de la isla.
- El final del DLC se conserva cuando Chef Saleroso se derrota fuera de la
  ruleta.
- Durante la victoria de Chef Saleroso, el HUD de la ruleta permanece en su
  overlay estable hasta que comienza el cambio real a la pantalla de
  calificación, evitando que desaparezca antes por el apagado de `LevelHUD`.

## 0.5.102 — 2026-08-06

- Una victoria contra el Diablo iniciada por la ruleta vuelve al último mapa
  después de la pantalla de calificación, en lugar de reproducir el epílogo y
  regresar a la selección de partida/personaje.
- La redirección ocurre después del cálculo de nota, progreso, logros y guardado.
- Derrotar al Diablo desde el flujo normal del juego conserva el final original.

## 0.5.101 — 2026-08-06

- Ambas variantes de español muestran siempre el nombre de nivel guardado en
  `BossEntry.Fight`; así los jefes cuya clave `Selection` no contiene texto,
  como Esther Espuelas, ya no dejan el subtítulo vacío.
- Los demás idiomas conservan únicamente el nombre localizado del jefe y no
  muestran subtítulo de nivel.
- Se desactivan el reto forzado y la simulación visual de cinco cartas.

## 0.5.100 — 2026-08-06

- Al reanudar desde pausa, el HUD recupera suavemente su opacidad de 70% a
  100% durante 0.30 segundos usando tiempo no escalado.
- La entrada a pausa y la posición permanente del HUD no cambian.
- Se añade `HUD_INTEGRATION.md` con la arquitectura, capas, ciclo de vida,
  constantes, puntos de extensión y matriz de pruebas del HUD de combate.

## 0.5.99 — 2026-08-06

- La opacidad adicional durante pausa sube de 55% a 70%.
- La fila sube 1 unidad de forma permanente: los márgenes normal y de pausa
  pasan de 12 a 13, por igual para uno y dos jugadores.

## 0.5.98 — 2026-08-06

- La opacidad adicional del HUD durante pausa sube de 48% a 55%.
- La posición permanente de la fila baja 3 unidades: los márgenes normal y de
  pausa pasan de 15 a 12. El cambio se aplica por igual a uno y dos jugadores,
  sin animación ni desplazamiento al abrir el menú.

## 0.5.97 — 2026-08-06

- Se elimina el paso del HUD por `LevelHUD.Canvas` durante pausa; ese Canvas
  `ScreenSpaceCamera` aplicaba el desenfoque fuerte del combate.
- La fila se coloca como primer hijo del `LevelPauseGUI` activo, debajo de su
  tarjeta y ayudas, siguiendo la ruta UI usada al perder.
- Un `CanvasGroup` reduce su opacidad al 48% durante la pausa para conservar el
  peso visual tenue de game over sin añadir blur. Al reanudar vuelve al 100%.

## 0.5.96 — 2026-08-06

- El margen inferior durante la pausa cambia de 10 a 15 unidades, igualando el
  valor normal. La fila conserva así la misma coordenada vertical al abrir y
  cerrar el menú sin alterar la ruta de Canvas ya validada.

## 0.5.95 — 2026-08-06

- La pausa de combate ya no se detecta mediante nombres frágiles como
  `Glyph (2)` y `Help (2)`. Se consulta directamente cada `LevelPauseGUI`
  activo y su estado nativo `Paused/Animating`.
- Esto evita confundir el hit-stop de parry con una pausa y garantiza que la
  fila llegue a `LevelHUD.Canvas` cuando la tarjeta está visible.
- El texto clonado del prompt del mapa desactiva su `LocalizationHelper`
  heredado y reafirma su etiqueta cada frame. Cuphead ya no puede sustituir
  `ABRIR RULETA` por el `VOLVER` original de la plantilla.

## 0.5.94 — 2026-08-06

- Las capturas confirmaron que la pausa procesa la imagen renderizada del
  combate; no existe un `Background` UI posterior capaz de afectar a sus hijos.
- Se combina la selección del PauseGUI activo, corregida en 0.5.92, con el
  reparentado a `LevelHUD.Canvas`. La fila comparte así la misma ruta de render
  que la vida y las cartas, mientras las ayudas de PauseGUI permanecen encima.
- El log registra una sola vez el modo y `sortingOrder` del Canvas al efectuar
  el cambio, para validar el comportamiento real durante esta prueba.

## 0.5.93 — 2026-08-06

- La captura de un jugador confirmó que la fila sí entraba al PauseGUI, pero
  como hija de `Background`; Unity dibuja el Graphic del padre antes que sus
  hijos, por lo que la fila permanecía nítida y encima de esa capa.
- La fila ahora se inserta como primer hijo del propio `PauseGUI`. El
  `Background`, la tarjeta y las ayudas se renderizan después, dejando el HUD
  agregado debajo del tratamiento visual de pausa y de `CONFIRMAR / VOLVER`.

## 0.5.92 — 2026-08-06

- La detección de pausa ahora prioriza el `PauseGUI/Background` que está activo
  en la escena. Antes podía tomar otra instancia inactiva conservada en memoria
  y dejar la fila en el overlay independiente.
- Durante una pausa real, la fila utiliza exactamente la misma ruta probada al
  perder: se vuelve el primer hijo del `Background` activo, conservando el
  desenfoque y quedando detrás de las ayudas del menú.

## 0.5.91 — 2026-08-06

- Al abrir la pausa, el HUD de la ruleta se mueve temporalmente al mismo Canvas
  nativo que las vidas y cartas. Por ello recibe el desenfoque de Cuphead en
  lugar de permanecer nítido sobre el fondo.
- Las ayudas nativas `CONFIRMAR` y `VOLVER` permanecen delante de la fila.
- La fila baja 5 unidades únicamente durante la pausa para alinear mejor el
  texto con esas ayudas; la posición durante el combate no cambia.

## 0.5.90 — 2026-08-06 (prueba temporal)

- Además de las cinco cartas visuales de ambos jugadores, se fuerza el reto
  `No disparo Peashooter`, el nombre más largo del catálogo actual.
- La ruleta limita la prueba a jefes de avión compatibles para validar el ancho
  del texto en el caso cooperativo más estrecho.

## 0.5.89 — 2026-08-06 (prueba temporal)

- P1 y P2 muestran cinco cartas de súper en el HUD nativo para validar
  visualmente el centrado de la fila de resultados en cooperativo.
- El selector sólo sustituye el valor recibido por `LevelHUDPlayerSuper`; no
  modifica `PlayerStatsManager.SuperMeter`, no regala súper y no altera la
  pelea. Debe desactivarse después de aprobar el diseño.

## 0.5.88 — 2026-08-06

- En cooperativo, la fila del resultado ya no permanece pegada al margen
  derecho ni invade la vida y las cartas de P2.
- El mod mide los límites nativos de `LevelHUDPlayerHealth` y
  `LevelHUDPlayerSuper` para ambos jugadores y centra la fila en el espacio
  libre entre ellos, con margen de seguridad en cada lado.
- El ancho máximo del texto del reto se limita al espacio cooperativo disponible
  para evitar solapamientos. En partidas de un jugador se conservan exactamente
  el ancla derecha y los márgenes anteriores.

## 0.5.87 — 2026-08-06

- La cápsula manual de teclado actualiza también el `LayoutElement` que usa la
  fila nativa. Esto evita que el layout vuelva a reducir F6/F7 al ancho del
  glifo circular de mando que ocupó antes el mismo contenedor.
- Al regresar del mando al teclado se restauran la escala, el ajuste de texto y
  los modos de desbordamiento del `Text`; F6/F7 disponen de un ancho mínimo de
  35 unidades y permanecen completos dentro del recuadro.
- El bloque de mando `LT/L2/ZL + Equip` no recibe este reajuste visual.

## 0.5.86 — 2026-08-06

- El modo teclado reafirma F6/F7 después de los eventos de cambio de controles.
  `CupheadGlyph` sigue suscrito a `PlayerManager.OnControlsChanged` aunque su
  componente esté desactivado y podía volver a escribir `SHIFT` dentro de la
  cápsula manual.
- Se añadió un poco de espacio horizontal a las cápsulas manuales.

## 0.5.85 — 2026-08-06

- El indicador de mando se reordenó como `ABRIR RULETA  LT + Y` (o sus
  equivalentes de PlayStation y Switch): primero la acción y después los
  botones.
- Se desactivó el comportamiento de localización del primer texto clonado de
  PauseGUI. Ese componente restauraba `CONFIRMAR` encima del separador `+`.
- El orden de hermanos de la fila nativa quedó fijado como acción, gatillo,
  separador y glifo de Equipar.

## 0.5.84 — 2026-08-06

- El indicador inferior derecho detecta el último dispositivo activo de
  Rewired. En teclado utiliza F6/F7; en mando utiliza el gatillo izquierdo más
  el glifo nativo de Equipar para abrir, y el gatillo derecho para volver a
  girar.
- Las etiquetas físicas cambian entre `LT/RT`, `L2/R2` y `ZL/ZR` según la
  identidad del mando. El botón Equipar conserva el glifo original de Cuphead.
- Se desactivaron `ForceRelicTestSequence` y
  `ForcePlaneRelicChallengeTestSequence` después de concluir la matriz de
  pruebas de reliquias y armas de avión. Los selectores quedan dormidos para
  futuras pruebas, pero los giros normales ya no fuerzan esos resultados.

## 0.5.83 — 2026-08-06 (prueba temporal)

- Todos los giros cargan temporalmente un jefe compatible de avión.
- La matriz de cuatro giros prueba, en orden: Maldita + No bombas, Divina + No
  bombas, Maldita + No Peashooter y Divina + No Peashooter; después se repite.
- `ForcePlaneRelicChallengeTestSequence` activa RETO aunque el ajuste guardado
  esté desactivado. Jefes, armas terrestres y súper continúan siendo aleatorios.
- Retirar tanto este selector como `ForceRelicTestSequence` cuando concluyan
  las pruebas.

## 0.5.82 — 2026-08-06 (prueba temporal)

- Los retos `No disparo bombas` y `No disparo Peashooter` interceptan ahora
  también `PlanePlayerWeaponManager.SwitchWeapon()`.
- La Reliquia Maldita y la Reliquia Divina cambian el disparo de avión desde
  `CheckBasic()` mediante esa ruta directa, sin pasar por el cambio manual que
  ya estaba bloqueado. Cualquier selección prohibida se sustituye por el arma
  permitida antes de comenzar el disparo.
- El bloqueo cubre a ambos jugadores y conserva EX, súper, mini avión y los
  demás efectos de la reliquia. Continúa activa la secuencia temporal que
  alterna Reliquia Maldita y Reliquia Divina para las pruebas.

## 0.5.81 — 2026-08-06 (prueba temporal)

- Fuerza una secuencia alternada de amuletos para validar la separación de las
  reliquias: el primer giro entrega Reliquia Maldita, el segundo Reliquia
  Divina, y los siguientes repiten ese orden.
- Los jefes, armas, súper, dificultad y reto continúan seleccionándose con las
  reglas normales. Retirar `ForceRelicTestSequence` después de la prueba.

## 0.5.80 — 2026-08-06

### Reliquia Maldita y Reliquia Divina independientes

- La Reliquia Maldita se añadió como un resultado separado del conjunto de
  amuletos y utiliza el primer icono animado nativo de `charm_curse`.
- La Reliquia Maldita fuerza temporalmente el grado interno `0`, mientras que
  la Reliquia Divina fuerza el grado máximo `4` y conserva su quinto icono.
- Ambas entradas siguen equipando el mismo amuleto oficial de Cuphead; el nivel
  se sustituye solamente mientras `PlayerStatsManager` y los controladores de
  animación inicializan el combate.
- Las consultas de progreso realizadas al ganar quedan fuera del parche. No se
  cambian el cementerio, los puntos acumulados, las compras ni la partida
  guardada, y el equipamiento previo continúa restaurándose al ganar o salir.

## 0.5.79 — 2026-08-06

### Corrección definitiva del parpadeo del HUD al hacer parry

- El rastreo cuadro por cuadro mostró que el impacto del parry cambia
  temporalmente `PauseManager.state`, aunque no exista un menú de pausa real.
- El mod interpretaba cualquier valor distinto de cero como pausa y ocultaba
  la raíz del HUD durante 11 frames al no encontrar la tarjeta del menú.
- El HUD ahora entra a la jerarquía de pausa únicamente cuando el fondo real
  del menú existe y está activo; el hit-stop del parry conserva el overlay de
  juego sin desactivar los círculos ni el texto.
- Se retiraron todos los hooks y registros temporales de diagnóstico.
- Se revirtieron el orden de canvas máximo y la compuerta especulativa por
  instancia de nivel, manteniendo el uso de materiales nativos de 0.5.73.

## 0.5.78 — 2026-08-06

### Rastreo de la pausa real del efecto de parry

- La ruta central encontrada en `AbstractParryEffect.hit_cr` llama
  `OnPaused()` y `OnUnpaused()` durante el impacto; el diagnóstico intercepta
  ahora esa pausa tanto en la clase base como en tierra y avión.
- Un vigilante adicional registra cualquier cambio real de actividad,
  jerarquía, canvas, visibilidad, culling, alpha o material del HUD durante toda
  la sesión, aunque ningún hook de parry se ejecute.

## 0.5.77 — 2026-08-06

### Rastreo sin filtro de sesión y soporte para Chalice

- El evento de diagnóstico se escribe ahora antes de comprobar si la sesión del
  HUD está activa, mostrando tanto ese estado como la existencia de la raíz.
- Se añadieron `ForceParry()` y `ChaliceDashParry()` para cubrir explícitamente
  las rutas terrestres de Ms. Chalice.

## 0.5.76 — 2026-08-06

### Rastreo ampliado del parry

- La primera reproducción con 0.5.75 no generó muestras: los dos métodos
  iniciales no cubrían la ruta de parry usada durante la prueba.
- El diagnóstico intercepta ahora inicio y éxito en controladores terrestres y
  aéreos, además de `PlayerStatsManager.OnParry()`.
- El arranque informa cuántos hooks quedaron instalados para verificar el
  instrumento antes de la siguiente reproducción.

## 0.5.75 — 2026-08-06

### Instrumentación exacta del parpadeo por parry

- Las pruebas confirmaron que el aislamiento de visibilidad, orden de canvas y
  materiales de 0.5.72–0.5.74 no eliminó el síntoma.
- Se añadieron hooks temporales al parry terrestre y aéreo que registran 24
  frames desde cada impacto exitoso.
- Cada muestra incluye actividad y jerarquía del HUD, canvas, modo, capa, orden,
  visibilidad nativa, alpha, culling y shader del primer círculo.
- Esta instrumentación permitirá distinguir con evidencia si cambia el objeto
  del mod o si el destello ocurre en una etapa posterior de composición.

## 0.5.74 — 2026-08-06

### Overlay de combate completamente independiente

- El `LevelHUD` nativo se usa una sola vez para confirmar que la escena nueva
  terminó de crear su HUD; después deja de controlar la visibilidad de la fila
  durante ese intento.
- Cada nueva instancia de `Level` reinicia esa espera inicial, por lo que la
  fila no aparece antes que las vidas durante cargas o reintentos.
- El canvas persistente usa la capa de orden superior y el máximo `sortingOrder`
  durante el juego activo, evitando que el overlay visual del parry se dibuje
  encima de sus círculos y texto.
- Pausa y derrota siguen colocando la fila dentro de sus menús. La victoria
  conserva la copia al `LevelHUD` nativo para oscurecerse con el knockout.
- La prueba manual posterior confirmó que el parpadeo continuaba; esta versión
  queda registrada como otro intento descartado, no como solución final.

## 0.5.73 — 2026-08-06

### Materiales nativos del HUD durante el parry

- Se comparó la implementación de `LevelHUDPlayerHealth` del juego con la fila
  del mod: las vidas conservan el material UI nativo, mientras nuestra fila
  reemplazaba continuamente todos sus materiales por el shader de saturación.
- Los círculos usan ahora el material UI predeterminado y el texto recupera el
  material original de Cuphead durante cualquier combate normal.
- El shader personalizado queda reservado exclusivamente para la transición
  del reto `Blanco y negro` mientras la fila está en el overlay independiente.
- Al pasar al HUD nativo durante la victoria también se restauran los materiales
  normales, manteniendo el comportamiento del knockout.
- La prueba posterior confirmó que igualar los materiales tampoco eliminaba por
  sí solo el parpadeo; 0.5.74 aísla también visibilidad y orden de renderizado.

## 0.5.72 — 2026-08-06

### Primer aislamiento adicional del parry en Rey Dado

- El HUD de Rey Dado ya no consulta la activación del `LevelHUD` nativo durante
  los minijefes ni durante el combate final activo.
- Algunas escenas de `DicePalace` deshabilitan brevemente ese canvas al hacer
  parry; la comprobación anterior ocultaba nuestra fila durante un frame aunque
  estuviera dibujada en un canvas independiente.
- La cadena activa mantiene ahora su overlay persistente sin esa dependencia.
  La victoria final conserva la transferencia al HUD nativo para desaparecer
  correctamente durante el knockout.
- Las pruebas posteriores mostraron que este cambio no resolvía el parpadeo y
  que el síntoma también aparecía fuera de Rey Dado; 0.5.73 corrige la diferencia
  de materiales respecto al HUD original.

## 0.5.71 — 2026-08-05

### HUD persistente durante toda la partida de Rey Dado

- La cadena de Rey Dado conserva una sola sesión visual aunque cada minijefe
  cargue una escena distinta.
- Los círculos y sus impactos de audio se animan únicamente al entrar por
  primera vez al nivel; los cambios internos de escena y reintentos muestran
  de inmediato el HUD ya revelado.
- Mientras la cadena siga activa, el HUD permanece en su canvas independiente
  y no hereda el destello del parry de capas pertenecientes a escenas previas.
- La victoria de `DicePalaceMain` sigue trasladando el HUD a la capa nativa para
  que se oscurezca y desaparezca junto con las vidas durante el knockout.

## 0.5.70 — 2026-08-05

### Ajuste fino de la entrada del HUD

- La espera antes del primer círculo aumentó de 1.0 a 1.1 segundos.
- El intervalo entre elementos bajó ligeramente de 300 a 280 ms.
- En tierra aparecen a los 1.10, 1.38, 1.66, 1.94 y 2.22 segundos; en avión,
  a los 1.10 y 1.38 segundos.
- Volumen, compactación, pulsos y sincronización del audio permanecen iguales.

## 0.5.69 — 2026-08-05

### Pausa inicial y secuencia más rápida

- La animación del HUD espera ahora 1 segundo antes de mostrar el primer
  círculo.
- La separación entre apariciones bajó de 350 a 300 ms.
- En tierra, los cinco elementos aparecen a los 1.0, 1.3, 1.6, 1.9 y 2.2
  segundos; en avión aparecen a los 1.0 y 1.3 segundos.
- El texto del reto también incorpora la espera inicial y continúa apareciendo
  después de terminar la secuencia de círculos.
- Cada impacto de audio permanece sincronizado con su elemento.

## 0.5.68 — 2026-08-05

### HUD más audible y ágil

- El volumen relativo de cada impacto aumentó de 0.70 a 0.85, manteniendo su
  enrutamiento por los controles **Principal** y **Efectos** del juego.
- La espera entre círculos bajó de 450 a 350 ms.
- En tierra, el quinto elemento aparece a los 1.4 segundos en vez de 1.8; en
  avión, el segundo aparece a los 350 ms.
- La separación visual compacta y la sincronización de un impacto por elemento
  permanecen intactas.

## 0.5.67 — 2026-08-05

### Impacto del HUD más audible

- El volumen relativo de `impact_01.wav` durante la aparición de cada círculo
  aumentó de 0.55 a 0.70.
- El incremento sólo afecta los impactos del HUD; el giro, las selecciones y los
  sonidos de apertura/cierre conservan sus niveles anteriores.
- El audio continúa enrutado al grupo SFX nativo, por lo que sigue respondiendo
  tanto al volumen **Principal** como al volumen **Efectos** de Cuphead.

## 0.5.66 — 2026-08-05

### HUD más compacto y aparición pausada

- La separación entre los círculos de disparos, súper, amuleto y reto pasó de
  4 a −2 unidades, compensando el margen transparente incluido en los iconos.
- El grupo terrestre completo ocupa 24 unidades menos de ancho; el HUD de avión
  también conserva la misma compactación proporcional.
- La separación de 10 unidades entre el último círculo y el texto del reto no
  cambió.
- La espera entre la aparición de cada círculo aumentó de 150 a 450 ms, sumando
  los 300 ms solicitados. Los impactos de audio permanecen sincronizados con
  cada aparición.

## 0.5.65 — 2026-08-05

### Respaldo puntual para español de España

- Si `<nivel>Selection` no proporciona texto utilizable mientras el juego está
  en español de España, el subtítulo usa `BossEntry.Fight`, conservando el
  nombre español original incluido por el autor del mod.
- El respaldo se limita a español de España; español latino sigue usando su
  traducción nativa y los otros diez idiomas continúan sin subtítulo.

## 0.5.64 — 2026-08-05

### Subtítulo exclusivo para español

- El nombre del combate se muestra como texto únicamente cuando Cuphead usa
  español de España o español latinoamericano.
- En inglés, francés, italiano, alemán, coreano, ruso, polaco, portugués
  brasileño, japonés y chino simplificado se muestra sólo el nombre localizado
  del jefe.
- Se retiró por completo la ruta de imágenes de `SpriteAtlas`, evitando que el
  nombre del jefe aparezca duplicado dentro del subtítulo.
- También se eliminó la recoloración y caché de esas imágenes, que ya no son
  necesarias.

## 0.5.63 — 2026-08-05

### Mayor tamaño para los títulos gráficos

- El área disponible para los nombres de combate provenientes de `SpriteAtlas`
  aumentó de 461×34 a 487×46 unidades de diseño.
- El arte permanece centrado y conserva su proporción, pero ahora aprovecha el
  ancho completo del subtítulo y tiene aproximadamente 35% más altura máxima.
- El bloque se desplazó ligeramente hacia abajo para no cubrir el nombre grande
  del jefe y mantiene separación respecto a los círculos de equipo.
- Los idiomas que usan texto conservan exactamente el tamaño anterior.

## 0.5.62 — 2026-08-05

### Sin respaldo visual en otro idioma

- Si la traducción activa de `<nivel>Selection` no contiene ni imagen ni texto
  utilizable, el subtítulo del combate ahora queda vacío.
- Se eliminó el respaldo visual que mostraba `BossEntry.Fight` en español,
  evitando mezclar idiomas dentro de la ruleta.
- Los nombres españoles permanecen en los datos del mod como referencia interna,
  pero ya no se dibujan cuando falta un recurso nativo.

## 0.5.61 — 2026-08-05

### Color uniforme para el arte localizado

- Los títulos provenientes de los atlas nativos ya no conservan sus píxeles
  negros ni dependen de un tinte multiplicativo incapaz de aclararlos.
- Cada sprite se copia una sola vez mediante GPU, se recolorea al mismo crema
  usado por `equipFightStyle` y conserva su canal alfa y bordes suavizados.
- La textura procesada queda almacenada en caché durante la sesión y se destruye
  junto con los demás recursos del mod al cerrarlo.
- Los títulos textuales no cambian; ambos formatos comparten ahora exactamente
  el mismo tono visual.

## 0.5.60 — 2026-08-05

### Títulos nativos en los doce idiomas

- El nombre del combate ahora reproduce la estrategia completa de localización
  de la tarjeta de dificultad de Cuphead.
- Cuando `<nivel>Selection` proporciona un recurso de `SpriteAtlas`, la ruleta
  dibuja directamente ese arte localizado y respeta su proporción. Esto cubre
  los títulos que Cuphead distribuye como imagen en inglés, coreano, japonés y
  chino simplificado.
- Cuando la traducción es textual, se eliminan las etiquetas de TextMeshPro y
  los caracteres transparentes usados por el juego para ajustar su tarjeta
  original antes de mostrar el título en una sola línea.
- Si un sprite no puede recuperarse del atlas se intenta el texto traducido y,
  sólo como último respaldo, el nombre español incluido en `BossEntry`.

## 0.5.59 — 2026-08-05

### Nombre localizado del combate

- El subtítulo situado debajo del nombre del jefe ya no usa siempre el texto
  español guardado en `BossEntry.Fight`.
- Ahora consulta la clave nativa `<nivel>Selection`, la misma que usa la tarjeta
  de dificultad de Cuphead para el nombre del combate.
- La consulta se realiza al dibujar la ruleta, por lo que refleja los cambios de
  idioma del juego sin reiniciar el mod.
- El nombre español existente se conserva como respaldo si la clave no existe,
  la traducción no contiene texto o el recurso está temporalmente indisponible.

## 0.5.58 — 2026-08-05

### Audio integrado con los ajustes de Cuphead

- Los dos canales de audio propios del mod ahora se envían al grupo `sfx` del
  mezclador nativo de Cuphead.
- El volumen **Principal** afecta los sonidos del mod como volumen maestro y el
  volumen **Efectos** controla su categoría; el volumen **Música** no los altera.
- El cambio se aplica automáticamente, incluso si el jugador modifica el volumen
  mientras el juego está abierto.
- Esto incluye el giro, las selecciones, los audios de apertura/cierre usados
  como respaldo y los impactos de aparición del HUD. Los sonidos nativos de menú
  ya pasan por el mezclador del juego y no reciben volumen duplicado.

## 0.5.57 — 2026-08-05

### Cancelación del giro al cerrar la ruleta

- Cerrar la Equip Card con F6, mando o cualquier otra ruta mientras gira ahora
  cancela la tirada en vez de mantenerla activa en segundo plano.
- Se detienen inmediatamente el audio continuo del giro y los sonidos de
  selección que todavía se estén reproduciendo.
- Se descartan el resultado parcial, los elementos revelados, los pulsos y una
  posible carga pendiente.
- Al abrir nuevamente la tarjeta vuelve al estado `¡GIRAR!`; es obligatorio
  iniciar una tirada nueva.

## 0.5.56 — 2026-08-05

### Entrada estable en pantalla completa

- La inclinación aleatoria de la Equip Card permanece fija durante toda la
  animación de entrada y salida, en lugar de interpolarse mientras se mueve.
- El desplazamiento vertical se ajusta a píxeles físicos completos después de
  aplicar la escala de resolución.
- Esto evita que fuentes, iconos y líneas parezcan deformarse o moverse entre
  sí al jugar a pantalla completa con escalas fraccionarias como 1.5×.
- La posición, inclinación y composición finales de la tarjeta no cambian.

## 0.5.55 — 2026-08-05

### Sincronización del impacto del HUD

- Se detectó que `impact_01.wav` contenía entre 90 y 98 ms antes de su golpe
  audible, aunque Unity iniciaba el clip en el mismo frame que el círculo.
- Se recortaron 85 ms del inicio y se conservó una entrada suave de 5 ms para
  evitar clics. El sonido perceptible comienza ahora aproximadamente 12 ms
  después de la aparición.
- No se cambió la cadencia de los círculos ni el momento de reproducción en el
  código; la corrección está contenida en el recurso de audio.

## 0.5.54 — 2026-08-05

### Salida nativa del HUD y sonido de aparición

- Se eliminó el parpadeo que podía producirse al transferir la fila al Canvas
  nativo durante el knockout.
- Al vencer se crea una copia visual ya preparada dentro de `LevelHUD`; el
  overlay se oculta en el mismo frame, sin mover la fila visible.
- La copia permanece aunque `SceneLoader` haya iniciado la carga y desaparece
  únicamente cuando el HUD original se oscurece y es retirado antes de la
  pantalla de resultados.
- Se añadió `assets/sounds/impact_01.wav`, convertido a PCM estéreo de 16 bits
  y 44.1 kHz para Unity 2017.4.
- El sonido se reproduce una vez con la aparición de cada círculo del HUD al
  comenzar un intento. No se reproduce para el texto del reto.
- Los combates terrestres reproducen cinco impactos y los de avión dos; pausa,
  derrota y transiciones temporales no repiten sonidos ya revelados.

## 0.5.53 — 2026-08-05

### HUD estable durante parry y knockout

- La fila del resultado queda aislada del efecto de cámara del parry y ya no
  debe destellar ni pulsar cuando el jugador realiza una parada.
- Durante el combate utiliza su Canvas overlay independiente, pero continúa
  respetando la disponibilidad del HUD original para no atravesar iris ni
  apagados de fase.
- El estado visual conserva una copia del resultado y del nombre del reto por
  separado del equipamiento temporal y de las restricciones jugables.
- Al vencer, la fila permanece fija durante todo el knockout, vuelve al Canvas
  nativo para compartir su salida y desaparece cuando comienza la transición
  oscura; no aparece en la pantalla de resultados.
- La derrota, los reintentos, la pausa, los niveles de avión y el reto Blanco y
  negro conservan su comportamiento anterior.

## 0.5.52 — 2026-08-05

### Resultado de la ruleta en el HUD

- Durante una pelea iniciada por la ruleta, el HUD muestra tiro A, tiro B,
  súper, amuleto y reto junto a los puntos de vida.
- En combates de avión sólo muestra amuleto y reto, porque los dos tiros y el
  súper terrestres no se utilizan en esos niveles.
- Dentro del combate cada icono mantiene estático su primer frame para evitar
  distracciones; la entrada secuencial conserva el pulso breve de selección.
  Todos se dibujan con 70% de opacidad y el nombre del reto aparece después.
- El conjunto queda anclado al margen inferior derecho y alineado verticalmente
  con el HUD nativo. Los iconos aparecen uno por uno con el mismo pulso de
  selección de la ruleta y el texto entra al final con fundido y asentamiento.
- Los nombres largos conservan fijo el margen derecho y reducen su tipografía
  sólo cuando superan el ancho máximo disponible.
- La antigua etiqueta aislada de la esquina fue sustituida por este conjunto.
- Al pausar o perder, el HUD se reubica en la jerarquía nativa para quedar por
  encima del oscurecimiento de fondo y por debajo de la tarjeta y sus opciones.
- El reto Blanco y negro aplica la misma transición de saturación al HUD nuevo.
- Durante la pelea, la fila pertenece a `LevelHUD.Current.Canvas`, igual que las
  vidas y las cartas del juego; los iris y apagados de fase ahora la cubren de
  forma natural.
- El resultado permanece durante los reintentos y desaparece al ganar o salir.
- Los golpes de parada ya no enmascaran el audio continuo de la ruleta: ambos
  usan fuentes independientes, niveles equilibrados y el loop conserva mayor
  prioridad de reproducción.
- La animación de entrada del HUD se ejecuta una sola vez al comenzar la pelea;
  las desactivaciones temporales del Canvas durante fases o iris ya no vuelven a
  dispararla.

## 0.5.51 — 2026-08-05

### Iconos animados de retos

- Los siete retos tienen ahora secuencias propias de tres frames diseñadas con
  el estilo visual de la Equip Card de Cuphead.
- Los iconos se reproducen a 12.5 FPS, la misma frecuencia usada por armas,
  súper y amuletos dentro de la ruleta.
- `Nada` conserva el círculo vacío animado nativo del juego cuando RETO está
  desactivado.
- El slot de RETO utiliza el mismo reflejo animado de los demás elementos
  mientras gira y lo retira en cuanto queda revelado.

## 0.5.50 — 2026-08-04

### Movimiento bloqueado con la ruleta abierta

- Ambos jugadores quedan inmóviles en el mapa mientras la tarjeta de la
  ruleta está visible.
- Al abrirla se cancela inmediatamente la velocidad que llevaran los
  personajes, evitando que sigan deslizándose bajo la interfaz.
- El movimiento vuelve a funcionar en cuanto se cierra la ruleta.

## 0.5.49 — 2026-08-04

### Equip Card bloqueada después de perder

- La Equip Card de la pantalla de derrota no puede abrirse mientras siga
  activa una partida iniciada por la ruleta.
- El resultado prestado por la ruleta queda protegido durante todos los
  reintentos y no puede reemplazarse antes de reiniciar el combate.
- El bloqueo sólo afecta esa pantalla de derrota; desaparece al ganar o salir
  al mapa junto con el equipamiento temporal.

## 0.5.48 — 2026-08-04

### Reto `Blanco y negro`

- Se añadió un reto compatible con niveles normales y de avión que utiliza el
  filtro blanco y negro nativo de Cuphead durante el combate.
- Cada intento comienza a color, espera 1.5 segundos y reduce suavemente la
  saturación del fotograma visible durante 1.25 segundos. Al finalizar queda
  activo el filtro blanco y negro original del juego.
- La transición utiliza un AssetBundle de 5 KB compilado con Unity 2017.4.9f1;
  el proyecto reproducible se conserva en `tools/unity-shader`.
- Se corrigió la orientación vertical de la textura en Direct3D mediante
  `_FlipY`, evitando que el combate aparezca de cabeza.
- El filtro se aplica sin modificar ni guardar la preferencia visual real del
  jugador y desaparece al ganar o abandonar el nivel.
- El reto conserva el filtro al perder y volver a intentar.
- Temporalmente utiliza `modifiers/blackandwhite.png` como icono provisional.
- Se retiró la selección forzada de `Blanco y negro` después de las pruebas.

## 0.5.47 — 2026-08-03

### Controles con mando

- Se añadió navegación completa mediante cruceta o stick usando las acciones
  nativas de menú de Cuphead.
- El botón de confirmar ahora permite cambiar opciones, girar la ruleta y
  seleccionar `JUGAR`.
- El botón de cancelar cierra la ruleta mediante el mismo flujo seguro de `Esc`.
- `ZR`, `RT` o `R2` permite volver a girar cuando la carga automática está
  desactivada y ya existe un resultado.
- El gatillo derecho utiliza detección por pulsación para evitar giros repetidos
  mientras permanece presionado.

## 0.5.46 — 2026-08-03

### Equipamiento temporal

- La ruleta puede seleccionar el catálogo completo disponible del juego base y
  del DLC, aunque el objeto todavía no haya sido comprado en la partida.
- Se guarda el equipamiento anterior de ambos jugadores antes de entrar al
  combate.
- El equipamiento de la ruleta se conserva al perder y volver a intentar.
- El equipamiento original se restaura al ganar o abandonar el nivel.
- En Rey Dado se mantiene el resultado durante los combates internos y sólo se
  restaura después de completar el enfrentamiento principal o abandonar la
  partida.
- El inventario y las compras de la partida no se modifican.

## 0.5.45 — 2026-08-03

### Ajuste de `Solo mini avión`

- El reto permite los disparos del mini avión y el súper.
- Los disparos del avión grande, las bombas y los ataques EX continúan
  reiniciando el nivel.

## 0.5.44 — 2026-08-03

### Apertura con mando

- Se añadió el atajo de gatillo izquierdo más el botón de Equip Card:
  `ZL + X`, `LT + Y` o `L2 + Triángulo`.
- La combinación funciona para cualquiera de los dos jugadores.
- La Equip Card original queda bloqueada únicamente mientras se utiliza la
  combinación de la ruleta y vuelve a funcionar normalmente al cerrarla.

## 0.5.43 y anteriores

- La ruleta sólo puede abrirse al caminar libremente por el mapa.
- Se recreó la interfaz a partir de la Equip Card de Cuphead, con navegación por
  filas, animaciones, sonidos y etiquetas nativas.
- Se añadieron dificultad, `RETO` y carga automática como opciones persistentes.
- Los retos se muestran y aplican únicamente durante el combate y se limpian al
  ganar o abandonar el nivel.
- Se añadieron restricciones para `No Dash`, `No mini avión`, `Solo mini avión`,
  `No disparo bombas`, `No disparo Peashooter` y `No EX`.
- Se añadió detección del DLC para excluir contenido que la instalación no puede
  cargar.
- Se añadieron la carga directa del jefe seleccionado y las etiquetas nativas
  `ABRIR RULETA` y `VOLVER A GIRAR`.
