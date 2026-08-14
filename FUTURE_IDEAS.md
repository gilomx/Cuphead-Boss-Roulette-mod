# Ideas para versiones futuras

Este documento guarda propuestas que todavía no forman parte del mod. No deben
tratarse como funciones terminadas ni activarse en una versión pública sin sus
pruebas correspondientes.

## Dos Ms. Chalice en cooperativo y variante morada

### Comportamiento observado

La ruleta aplica el mismo resultado de amuleto a los dos jugadores. Si ambos
reciben la Galletita Astral, el código nativo de `Level.CreatePlayers()` detecta
las dos copias de `charm_chalice` y activa `BlockChaliceCharm` aleatoriamente
para uno de los jugadores. Como consecuencia:

- Un jugador se transforma en Ms. Chalice.
- El otro conserva el cuerpo de Cuphead o Mugman.
- En niveles de avión puede aparecer un estado híbrido: el jugador bloqueado
  conserva su personaje, pero el mod puede asignarle los disparos de avión de
  Chalice porque actualmente los resuelve desde el resultado global de la
  ruleta y no desde `PlayerStatsManager.isChalice` de cada jugador.

El bloqueo de la segunda transformación es una regla nativa; el estado híbrido
es un caso límite de la integración actual del mod.

### Propuesta

Investigar una modalidad opcional en la que los dos jugadores sean realmente
Ms. Chalice y el segundo utilice una paleta morada para distinguirse.

La parte visual podría resolverse clonando el material de los renderers del
segundo jugador y aplicando una sustitución de paleta mediante shader. Cuphead
incluye `PlayerRecolorHandler` y el parámetro `_RecolorFactor`, pero esa ruta
está vinculada a filtros visuales y no constituye una variante morada lista
para usar.

### Investigación necesaria

- Anular `BlockChaliceCharm` únicamente en combates iniciados por la ruleta.
- Confirmar `isChalice` individualmente después de `PlayerStatsManager.LevelInit()`.
- Revisar `PlayerManager.playerWasChalice` y los estados persistentes entre
  reintentos.
- Revisar el Palacio de Dados, que conserva un único `CHALICE_PLAYER`.
- Probar movimiento, doble salto, parry con dash, supers, EX, revivir,
  introducciones, derrotas y resultados.
- Probar las variantes de avión y evitar asignar armamento desde una condición
  global cuando los personajes sean diferentes.
- Recolorear cuerpo, avión, fantasma de reanimación, efectos, supers y cualquier
  retrato que necesite distinguir al segundo jugador.
- Crear materiales por jugador; no modificar un `sharedMaterial` que pueda
  recolorear también a P1.
- Restaurar sin residuos todos los cambios al volver al mapa.

Antes de intentar la duplicación completa también puede corregirse solamente
el estado híbrido, usando el `isChalice` real de cada jugador para elegir su
armamento de avión.

## Reto de desfase RGB inspirado en Cagney — HECHO

Estado: funcionalidad terminada y aceptada en 0.5.129. Queda desactivada por
decisión del proyecto: `EnableRgbShiftChallenge` y
`ForceRgbShiftChallengeForTesting` están en `false`, por lo que no aparece en
la ruleta. Para publicarla después se activa sólo el primer interruptor. El arte
final de tres frames y el nombre localizado siguen pendientes como presentación,
no como desarrollo funcional.

### Idea

Cagney Carnation aplica al recibir ciertos golpes un efecto visual parecido a
un desfase de los canales rojo, verde y azul. Investigar su implementación
nativa para convertirla en un reto reutilizable en otros jefes.

Nombre provisional: `Desfase RGB`.

### Objetivo visual

- Mantener un desplazamiento cromático legible durante el combate sin ocultar
  la acción.
- Usar, si es viable, el material, shader o componente nativo de Cagney en vez
  de reconstruir el efecto desde cero.
- Añadir entrada y salida suaves para evitar un corte visual brusco.
- Mantener el efecto fuera del mapa y limpiarlo al ganar, abandonar o volver a
  intentar cuando corresponda.

### Investigación necesaria

- Identificar exactamente qué evento, componente y material activa Cagney.
- Registrar propiedades, intensidad, duración y orden de render del efecto.
- Confirmar si afecta la cámara completa o sólo determinados renderers.
- Comprobar compatibilidad con pausa, derrota, iris, calificación y transiciones
  especiales.
- Definir cómo se combina con el reto `Blanco y negro`; evitar dos filtros que
  sobrescriban el mismo material o dejen la pantalla en un estado incorrecto.
- Medir el costo de render. La implementación preferida debe reutilizar el
  postproceso nativo y evitar procesamiento de imagen en CPU.
- Probar niveles terrestres, niveles de avión, P1 y cooperativo.

La implementación terminada reutiliza `ChromaticAberrationFilmGrain`, espera los
mismos 1.5 segundos de apertura normal que `Blanco y negro` y entra durante
1.25 segundos. La configuración aceptada usa amplitud base 32 y una
trayectoria sinusoidal 2D: velocidad vertical 10 y horizontal 7.3, con 70% de
amplitud horizontal. Rojo usa 120% de fuerza, verde 60% y azul 90%. También
controla `BlurGamma` sin corutinas: conserva el ciclo nativo de 2.2 segundos a
70% de fuerza. El placeholder es un único PNG transparente de 80 × 80 con el
texto `RGB`; el arte final animado sigue pendiente.

## Reto de fijado permanente

### Idea

Crear un reto terrestre que mantenga activa la acción nativa de fijar la
dirección durante toda la pelea.

Nombre provisional: `Fijado permanente`.

Se contemplan dos variantes de balance:

1. Fijado permanente con dash disponible como opción de desplazamiento.
2. Fijado permanente combinado con `No dash` para una versión extrema.

### Reglas propuestas

- Aplicarlo únicamente durante el combate; nunca simular la tecla globalmente
  ni afectar mapas, ruleta, pausa o menús.
- Intervenir la consulta de la acción de fijado dentro del controlador del
  jugador, conservando entradas independientes para P1 y P2.
- Mantener la posibilidad de apuntar en las direcciones que permita el juego
  mientras el personaje está fijado.
- Limitarlo inicialmente a niveles terrestres. En avión, los mismos botones
  pueden tener funciones diferentes y necesitan una investigación separada.
- Limpiar el estado al morir, reintentar, ganar o abandonar.

### Balance y compatibilidad

La combinación con `No dash` puede volver imposibles algunos jefes. Antes de
añadirla al catálogo aleatorio se necesita una lista de compatibilidad por
nivel y una prueba completa de cada fase. Si no existe una selección segura de
jefes, debe permanecer como reto de desarrollo o descartarse la variante
combinada.

Probar como mínimo:

- Salto, agacharse, parry, EX y súper.
- Cambios de dirección y apuntado diagonal.
- Reintento después de derrota.
- Dos jugadores con controles independientes.
- Jefes que exigen atravesar rápidamente la pantalla.
- Interacción con otros bloqueos de movimiento del propio nivel.

## Opción para jugar como Cáliz

### Idea

Agregar a los ajustes de la ruleta una opción persistente llamada
`Jugar como Cáliz`. Cuando esté activada, el resultado del espacio de amuleto
será siempre la Galletita Astral, de modo que el jugador entre a cada combate
como Ms. Chalice sin depender del resultado aleatorio.

La ruleta y el HUD deben mostrar la Galletita Astral como el resultado real;
no debe aplicarse como una sustitución oculta después del giro. Mientras esta
opción esté activa, los demás amuletos y el resultado vacío quedan excluidos
de ese espacio.

### Comportamiento propuesto

- Guardar la preferencia junto con dificultad, reto y carga automática.
- Si se cambia después de haber girado, invalidar el resultado pendiente y
  volver a mostrar `¡Girar!`, igual que con los demás ajustes.
- Mantener el préstamo temporal de equipamiento: al ganar o abandonar, se
  restaura el amuleto que el jugador tenía antes de usar la ruleta.
- Aplicar correctamente el armamento de avión de Cáliz cuando corresponda.
- No exigir que la Galletita Astral esté comprada o equipada previamente,
  siguiendo la regla actual de usar el catálogo completo.

### Cooperativo e investigación necesaria

Definir si la opción afecta a ambos jugadores o solamente a quien abrió la
ruleta. Si se aplica a los dos, debe resolverse primero el bloqueo nativo de
dos Ms. Chalice y el posible estado híbrido descrito en la sección
`Dos Ms. Chalice en cooperativo y variante morada`.

Probar P1, P2, reintentos, niveles terrestres, niveles de avión, Palacio de
Dados, supers, HUD y restauración del equipamiento al volver al mapa.

## Reto de jugar al revés (pantalla volteada) — HECHO

Estado: implementación y pruebas manuales terrestres terminadas en 0.5.129.
`EnableUpsideDownChallenge` y `ForceUpsideDownChallengeForTesting` están en
`false`, por lo que queda compilado pero dormido hasta recibir el nuevo icono
animado y completar su matriz final de pruebas.

### Diseño final

El combate permanece normal durante 0.25 segundos y después el fotograma final
gira suavemente durante 0.45 segundos alrededor de su centro hasta quedar a
180 grados. Es una rotación plana pura: no existe espejo, cambio de escala
horizontal ni salto en el punto medio. Al terminar, izquierda y derecha
intercambian lugares como en una imagen física girada media vuelta.

No se aplica zoom. El cuadro central conserva escala 1:1 y los píxeles de borde
se extienden únicamente sobre los huecos de los ángulos intermedios. La
transformación no modifica posiciones reales, hitboxes, física, entradas ni
controles.

### Ciclo de vida aceptado

- El HUD nativo y la fila de la ruleta giran junto con el combate.
- Al perder, la tarjeta permanece invertida. Reintentar o salir al mapa mantiene
  la orientación durante el fundido y la limpia solamente bajo negro total.
- Reiniciar o salir al mapa desde pausa usa exactamente el mismo reset oculto.
- Al ganar, conserva el K.O. invertido durante 1 segundo y regresa visiblemente
  a normal en 0.45 segundos antes de la calificación.
- La prueba terrestre confirmó derrota, reintento, ambas salidas al mapa,
  reinicio desde pausa, sonido y limpieza. Antes de publicarlo faltan avión,
  cooperativo, parrys repetidos y la cadena completa del Palacio de Dados.
- El icono actual es temporal; el nuevo arte animado sigue pendiente.

## Reto aleatorio por intervalos

### Idea

Crear un reto dinámico llamado provisionalmente `Aleatorio` que alterne dos
fases durante todo el intento:

1. `5 segundos sin reto`: el combate funciona normalmente.
2. `10 segundos con reto`: se elige uno de los retos compatibles con ese nivel,
   se muestra su nombre junto al contador y se mantiene activo hasta llegar a
   cero.

Al terminar los 10 segundos se limpia por completo el reto activo, comienza una
nueva pausa de 5 segundos sin reto y después se selecciona el siguiente. El
ciclo `5 sin reto → 10 con reto` se repite hasta terminar el intento. Queda por
confirmar únicamente si el siguiente reto puede repetir el anterior.

### Reglas y arquitectura propuestas

- Crear una lista de compatibilidad específica para retos que puedan activarse
  y retirarse a mitad de una pelea. Ser compatible con un jefe al iniciar el
  nivel no garantiza que el cambio en caliente sea seguro.
- Limpiar por completo el reto anterior antes de activar el siguiente; nunca
  permitir que bloqueos, filtros o restricciones de daño se acumulen.
- Usar tiempo de juego para ambos contadores, de modo que la pausa no consuma
  ni los 5 segundos de descanso ni los 10 segundos de reto.
- Mostrar en el HUD tanto el nombre del reto actual como los segundos restantes,
  sin sustituir permanentemente la etiqueta `Aleatorio` del resultado original.
- Reiniciar el ciclo en la fase de 5 segundos sin reto al perder y reintentar.
  En el Palacio de Dados, tratar toda la cadena como un solo intento y no
  reiniciar la animación ni el contador por cada escena interna.
- Al ganar, abandonar o volver al mapa, cancelar el temporizador y restaurar
  inmediatamente cualquier estado controlado por el reto activo.
- Definir el comportamiento de restricciones de avión que necesitan imponer
  un arma inicial (`No bombas` y `No peashooter`) cuando aparezcan a mitad del
  combate.
- Evaluar individualmente `Solo mini avión`, filtros visuales y cualquier reto
  que reinicie el nivel por una infracción ocurrida durante el cambio de estado.
- Evitar seleccionar el propio reto `Aleatorio` dentro de su catálogo para no
  crear recursión.
- Probar P1, cooperativo, terrestre, avión, reintentos, pausa, knockout,
  Palacio de Dados y cambios de escena.

## Reto HP.1 (implementación terminada, en espera de arte)

### Estado actual

La implementación funcional está terminada y superó su matriz manual. Permanece
desactivada mediante `ExperimentalFeatures.EnableHpOneChallenge = false` y
`ForceHpOneChallengeForTesting = false` únicamente porque falta sustituir el
icono temporal por el icono animado definitivo de ruleta/HUD. Hasta entonces no
puede aparecer en giros normales.

El reto funciona en niveles terrestres y de avión y fija la vida actual y
máxima de cada jugador en exactamente 1 HP durante toda la sesión de batalla.
Perder, reintentar, revivir o incorporar tarde a P2 conserva la regla; ganar o
volver al mapa restaura el comportamiento normal sin modificar el perfil.

### Reglas implementadas y validadas

- `Corazón` y `Corazón Doble` pueden salir. No aumentan HP y conservan sus
  multiplicadores nativos de daño recibido (`0.95` y `0.90`, respectivamente).
- `Reliquia Maldita`, `Reliquia Divina` y `Anillo de Corazón` conservan sus
  demás efectos, pero no pueden aumentar ni recuperar vida.
- Las curaciones rechazadas de Anillo/Reliquias usan el efecto blanco y negro,
  semitransparente y glitchoso, además de
  `assets/sounds/hp_one_rejected_parry.wav` en lugar del golpe de parry de
  curación habitual. La misma ruta funciona en tierra y avión.
- Galletita Astral conserva a Ms. Chalice. Su Súper II no concede escudo; el
  corazón rechazado se vuelve blanco y negro, semitransparente y glitchoso
  antes de desaparecer, y el jugador continúa vulnerable.
- Los corazones del Palacio de Dados, las reliquias y cualquier otra curación
  nativa permanecen limitados a 1 HP.
- `RouletteDjimmiGuard` suprime los deseos de Djimmi en cualquier pelea iniciada
  mediante la ruleta, no sólo durante HP.1. No consume ni borra el deseo: al
  entrar manualmente a otro nivel vuelve a aplicarse. La prueba en Normal dio
  3 HP con ruleta y 9 HP sin ruleta. Cuphead ya excluye Djimmi nativamente en
  Experto.
- En cooperativo, P1 y P2 comienzan con 1 HP; un fantasma revive con 1 HP; un
  P2 incorporado durante la pelea entra con 1 HP; volver a intentar conserva
  una vida para ambos; salir de la ruleta restaura la vida normal.
- El icono actual de candado `HP.1` es temporal y de un solo frame.

### Pendiente antes de reactivarlo

Crear e integrar el icono animado definitivo para todas las apariciones del
reto. Después conviene ejecutar una regresión corta de giro, HUD, reintento y
cooperativo, habilitar `EnableHpOneChallenge` y mantener todos los selectores
`Force...ForTesting` desactivados.

## Creator Tools: overlay local

### Objetivo

La primera versión de `Creator Tools` se limita a ofrecer una fuente transparente
para OBS, Streamlabs u otro programa compatible con fuentes de navegador. Al
comenzar un combate elegido por la ruleta, el overlay debe mostrar los mismos
resultados que el HUD del mod, pero con un diseño pensado para poder verse más
grande en una transmisión o grabación: los iconos en una fila superior y el
texto del reto debajo. Otras herramientas para creadores quedan fuera de esta
primera etapa hasta que el overlay esté terminado y aceptado.

Debe funcionar completamente en la computadora del jugador, sin internet, una
cuenta, un servicio remoto ni una aplicación auxiliar. El overlay no debe
convertirse en una segunda fuente de estado: debe reutilizar la sesión y el
snapshot inmutable que ya controlan el HUD de combate.

### Arquitectura propuesta

- Integrar en el plugin un servidor local mínimo basado en `TcpListener`,
  enlazado exclusivamente a `127.0.0.1`. El mismo puerto sirve la página HTML,
  sus recursos locales y actualiza la conexión a WebSocket en una ruta dedicada.
  Se evita `HttpListener` por sus posibles requisitos de URL ACL.
- Cuphead usa .NET 3.5 y no incluye `System.Net.WebSockets`. Para no agregar una
  DLL externa al instalable, implementar dentro del mod solamente la parte
  necesaria del protocolo WebSocket: handshake RFC 6455, mensajes de texto,
  ping/pong, cierre y desconexión limpia.
- Servir la fuente transparente en una URL estable, por ejemplo
  `http://127.0.0.1:18081/`. El navegador abre después una conexión local como
  `ws://127.0.0.1:18081/ws`; no consulta periódicamente un endpoint de estado.
- El puerto se administra automáticamente y lo comparten la página HTTP y el
  WebSocket para no crear dos posibles conflictos. Cambiar solamente la ruta
  `/ws` no permite compartir un puerto ya ocupado por otro servidor. La
  dirección permanece limitada a `127.0.0.1`; no se expone el mod a la red
  local ni a internet.
- Al cambiar el puerto, `COPIAR URL DEL OVERLAY` debe entregar automáticamente
  la nueva dirección. El puerto preferido predeterminado es `18081`. Si está
  ocupado, el servidor prueba secuencialmente `18082`, `18083` y los siguientes,
  hasta un máximo de 100 candidatos. No elige un valor aleatorio. El primer
  puerto libre se guarda como el nuevo preferido para que la dirección permanezca
  estable en lanzamientos posteriores.
  Si el puerto efectivo cambia respecto al que OBS podía tener guardado, el menú
  debe mostrar un aviso claro para volver a copiar la URL. Si no se encuentra un
  puerto disponible dentro del rango de búsqueda, se desactiva solamente
  `Creator Tools`, sin modificar Cuphead ni afectar la ruleta.
- Enviar un snapshot completo inmediatamente al conectar o reconectar. Después,
  el mod solamente empuja otro mensaje cuando cambia la sesión, la cantidad de
  elementos revelados, la visibilidad, el idioma o un ajuste visual. El formato
  del mensaje puede seguir siendo JSON compacto; WebSocket elimina el sondeo,
  mientras JSON mantiene el protocolo sencillo de inspeccionar y ampliar.
- Enlazar la publicación con `BeginBattleResultHudSession()` y usar
  `battleHudResultSnapshot` como fuente de verdad. Limpiar el estado junto con
  `EndBattleResultHudSession()` al regresar al mapa.
- Mantener el overlay durante pausa, derrota, reintento y victoria con las
  mismas reglas que el HUD. No crear otro ciclo de vida para el reto o el
  equipamiento.
- Publicar una revisión o identificador de sesión y el número de elementos ya
  revelados. Esto permite que el navegador muestre cada icono y después el
  texto al mismo tiempo que el HUD, sin intentar calcular por separado el
  momento de la animación. Una conexión que llega a mitad del combate recibe el
  estado actual y no vuelve a reproducir una animación antigua.
- El hilo de red nunca debe acceder a objetos de Unity. El hilo principal debe
  preparar un DTO o JSON inmutable y sustituirlo de forma segura; el servidor
  únicamente devuelve ese estado ya construido.
- Detener el listener limpiamente al cerrar el plugin o el juego. Si se agotan
  los 100 puertos candidatos, registrar el problema y desactivar solo el
  overlay, sin afectar la ruleta ni Cuphead.

### Diseño del overlay

- Fondo completamente transparente.
- La distribución predeterminada coloca los iconos del resultado en una fila
  superior y el texto localizado del reto debajo. Una opción permite invertir
  solamente la composición para colocar el texto arriba y los iconos abajo.
  En ambas distribuciones la secuencia de entrada permanece igual: primero se
  revelan los iconos y después aparece la etiqueta del reto.
- La etiqueta del reto debe verse con exactamente la misma tipografía que la
  fila del HUD del mod. Ese HUD clona el texto auxiliar nativo de Cuphead
  (`PauseGUI/Text (1)`) con su fuente y material, y usa
  `CupheadMemphis-Medium` solamente como respaldo.
- No depender de una fuente instalada en Windows ni incluir una aproximación
  web. El hilo principal de Unity renderiza la etiqueta localizada a una imagen
  transparente de alta resolución usando el mismo componente nativo, la guarda
  en caché y publica su revisión al overlay. El navegador anima esa imagen como
  una sola etiqueta. Esto conserva glifos, material y espaciado en todos los
  idiomas sin entregar el archivo de fuente del juego.
- Regenerar la etiqueta solamente cuando cambie el reto, el idioma o la escala
  del overlay. La captura y codificación ocurren siempre en el hilo principal;
  el servidor WebSocket sólo transmite la referencia inmutable ya preparada.
- Reutilizar los primeros frames estáticos aceptados para el HUD de combate y
  los recursos incluidos en el mod; el navegador debe cargarlos una vez y
  conservarlos en caché.
- Respetar el contenido reducido definido para niveles de avión.
- Ocultar el resultado anterior si Cuphead se cierra o el navegador deja de
  recibir estado durante aproximadamente dos segundos.
- La vista previa debe usar datos simulados y desactivarse automáticamente al
  comenzar una pelea real para no sustituir un resultado válido.
- Pendiente especial del Palacio de Dados: conservar una sola sesión y el mismo
  estado ya revelado durante tablero, minijefes y jefe final. Actualmente cada
  cambio de minijefe reinicia la animación del overlay; debe animarse sólo al
  inicio real y salir únicamente al ganar o abandonar toda la cadena.

### Integración propuesta en los menús de Cuphead

Agregar una entrada `LA PICHI RULETA` al menú nativo que se abre con pausa
desde el mapa. La primera versión no modifica el menú de pausa de los combates
ni agrega una entrada al menú principal. La nueva fila debe integrarse en
`MapPauseUI`, usar la misma tipografía, selección, sonidos y controles nativos,
y funcionar igual con teclado o mando. Su posición aceptada es inmediatamente
debajo de la fila nativa `OPCIONES`.

Seleccionar `LA PICHI RULETA` abre directamente la pantalla nativa que Cuphead
usa para `OPCIONES -> VISUAL`, pero sus filas se sustituyen temporalmente por la
configuración del mod. De esta forma se conservan exactamente su fondo, ruido,
tipografía, flechas, colores, movimiento, sonidos, transiciones y navegación.
No existe todavía un submenú intermedio para `CREATOR TOOLS`, porque es la única
familia de opciones disponible. El título pasa a `LA PICHI RULETA`; al cancelar,
los botones y valores Visual originales se restauran antes de volver a la pausa.
Si en el futuro aparecen otras familias, esta pantalla podrá convertirse en un
índice de categorías sin cambiar la entrada del menú del mapa.

Mientras la pantalla esté abierta, el mapa permanece pausado y el menú nativo no
recibe selecciones por debajo. `Cancelar` regresa al menú de pausa; no cierra la
pausa ni mueve al personaje. Cerrar después el menú nativo reanuda el mapa
normalmente. La entrada permanece
disponible aunque el overlay esté desactivado, porque es también el lugar para
volver a activarlo, copiar la URL vigente o consultar un error del servidor.

Opciones iniciales:

- `CREATOR TOOLS`: activado/desactivado. Inicia o detiene el servicio local y conserva la
  preferencia.
- `VISTA PREVIA`: activada/desactivada, para acomodar la fuente mientras se
  observa OBS. Es temporal y vuelve automáticamente a desactivado al salir de
  esta pantalla.
- `TAMAÑO`: 1x/1.5x/2x. Generar internamente una versión de mayor resolución evita
  depender únicamente del escalado de OBS y mejora la lectura de iconos
  pequeños.
- `ORDEN`: iconos arriba/texto arriba. Cambia la composición vertical en vivo,
  sin alterar el orden temporal de la animación.
- `ALINEACIÓN`: izquierda/centro/derecha dentro del lienzo transparente.
- `OPACIDAD`: 25-100 %, en pasos de 5 puntos porcentuales.
- `COPIAR URL DEL OVERLAY`: copia la URL vigente al portapapeles, reproduce la
  confirmación nativa y muestra temporalmente `URL COPIADA`. No debe abrir un
  navegador ni sacar al jugador del menú.

Los cambios de tamaño, alineación y opacidad deben reflejarse automáticamente
en una fuente de OBS que ya esté abierta, sin recargarla ni volver a copiar la
URL. El puerto resuelto se conserva internamente para mantener estable la
dirección entre sesiones, pero no aparece como una opción editable en el menú.

### Conexión y rendimiento

- Todo el tráfico permanece dentro de `127.0.0.1`. Los mensajes de estado miden
  solo unos pocos kilobytes y los iconos no se transfieren con cada cambio.
- El hilo principal prepara un mensaje inmutable al cambiar el estado; el hilo
  de red se limita a enviarlo a las conexiones activas. Con estas reglas, no hay
  consultas constantes ni búsquedas de componentes de Unity desde la red.
- Enviar un ping espaciado solamente para detectar conexiones muertas. Si se
  cierra Cuphead o se pierde la conexión, la página oculta el resultado y trata
  de reconectar con espera progresiva, sin conservar información obsoleta.
- Usar caché normal para imágenes, fuentes y estilos. Limitar la primera versión
  a estado enviado desde el juego hacia el navegador: ningún mensaje o endpoint
  puede girar la ruleta, cargar niveles o controlar Cuphead.

### Implementación por etapas

1. Servidor local HTTP/WebSocket, página transparente, estado sincronizado,
   reconexión e invalidación de resultados antiguos.
2. Menús, persistencia, copia de URL y vista previa.
3. Ajustes visuales, localización completa y sincronización fina de la aparición
   de iconos y texto.
4. Evaluar como ampliación opcional el nombre, imagen del jefe, dificultad o
   temas visuales, sin recargar la primera versión.

## Criterio general para incorporar estas ideas

Cada idea debe implementarse detrás de un selector temporal, documentarse en
`PROJECT_HANDOFF.md` y `CHANGELOG.md`, probarse con P1 y cooperativo, y dejar el
selector desactivado antes de publicar una versión normal.
