# Historial de cambios

Este documento resume los cambios funcionales de Gilomx Boss Roulette. Las
versiones corresponden al número mostrado por BepInEx al cargar el mod.

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
