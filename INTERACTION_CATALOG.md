# Catálogo de interacciones

Esta guía define el contrato técnico que deben respetar todos los artículos
nuevos del catálogo de Creator Tools. Las implementaciones de referencia son
los mini zepelines verde y morado, la zanahoria teledirigida de La pandilla
raíz, la semilla azul de Clavel de Cagney y la luciérnaga incendiada de Hosco
y Tosco.

## Arquitectura obligatoria

- `CreatorToolsServer` sólo recibe y encola solicitudes. Su hilo de red no debe
  crear, buscar ni destruir objetos de Unity.
- `CreatorToolsInteractionController` consume la cola desde el `Update`
  principal, aplica los límites comunes y delega cada artículo a un ejecutor.
- Cada ejecutor implementa `ICreatorToolsInteractionExecutor`, crea un actor
  jugable nativo y devuelve un `ICreatorToolsInteractionHandle`. La cola conserva
  ese handle hasta que indique `IsComplete`; así puede representar enemigos,
  proyectiles y futuros objetos compuestos sin conocer sus clases concretas.
- Los IDs públicos viven en `CreatorToolsInteractionIds.All` y el controlador
  resuelve su ejecutor por `Supports`. Modo Molestoso obtiene candidatos de esa
  misma lista y sólo elige los que reporten `IsAvailable`.
- La presentación compartida se aplica una sola vez con
  `CreatorToolsInteractionPresentation.PrepareActor(actor, donor, logWarning)`.
  Si el sprite principal vive en un hijo, se usa la sobrecarga que recibe su
  `SpriteRenderer`. No se debe copiar la lógica de etiqueta dentro de cada
  ejecutor.
- Un fallo de presentación nunca debe invalidar un actor ya creado.
  `PrepareActor` captura y registra esos errores sin cancelar el canjeo.

## Contrato visual de la etiqueta

`CreatorToolsDonorLabel` y `CreatorToolsDonorLabelFollower` son la referencia
única para los nombres de donadores. Todo enemigo o elemento visual futuro debe
reutilizarlos mediante `PrepareActor`.

Las reglas de stream pueden añadir de forma opcional el PNG local del regalo
con `CreatorToolsInteractionPresentation.SetGiftImage`. El dato debe viajar de
forma explícita por backlog, cola y executor; no se deben usar contextos globales
temporales ni descargar la URL remota durante gameplay. El label comparte con el
icono seguimiento, alpha, snapshot y prioridad de render. Las rutas manuales que
no tienen regalo conservan únicamente el nombre.

### Creación y renderizado

1. La etiqueta se crea como un `GameObject` de mundo independiente, en la misma
   capa que el actor. No es `OnGUI`, un `Canvas` de pantalla ni un elemento de
   React; por eso atraviesa la cámara de gameplay y conserva los filtros
   visuales de Cuphead.
2. El texto usa `TextMeshPro`, la fuente Memphis del juego, mayúsculas, tamaño
   22, color crema y contorno oscuro. Si Memphis no se puede resolver, se busca
   otro asset Memphis cargado y finalmente se usa la fuente predeterminada.
   La paleta compartida también define un texto alternativo casi negro
   (`#181411`). `AlternateTextColorLevels` es la única tabla que debe decidir
   qué jefes lo usan; mientras permanezca vacía conserva crema en todos los
   niveles. No se deben repartir condiciones de color entre los ejecutores.
3. En esta versión de Unity, `AddComponent<TextMeshPro>()` sustituye el
   `Transform` del objeto por un `RectTransform`. Siempre hay que obtener
   `labelText.rectTransform` después de añadir el componente; conservar una
   referencia al `Transform` anterior rompe la posición.
4. El rectángulo mide 320 × 48 y usa el pivote `(0.5, 1)`. Esta es la posición
   vertical aprobada después del ajuste visual; no debe volver a centrarse el
   pivote sin una prueba comparativa dentro del juego.
5. `BringActorToFront` registra actor y etiqueta en
   `CreatorToolsInteractionRenderPriority`. En gameplay normal reafirma cada
   `LateUpdate` la capa `ForegroundEffects`, conservando el orden relativo del
   actor y colocando la etiqueta justo después. Esto evita que `Start`, un
   `Animator` o un jefe vuelvan a dejar el elemento detrás de sus capas.
6. Cuando un `PlayerScreenEffectController` muestra un sprite de cobertura, o
   cuando se habilita el `SpriteRenderer` de
   `PirateLevelSquidInkOverlay.Current` durante la tinta de Barbasalada, actor y
   etiqueta bajan temporalmente a `Enemies`. Así el oscurecimiento de
   transformaciones, pausas, filtros y tinta permanece por delante; al
   desaparecer la cobertura ambos regresan a `ForegroundEffects`. En el caso
   de la tinta se usa el estado `enabled`, no sólo su alfa, para cubrir también
   el primer fotograma del impacto y todo el fundido. No se debe usar la capa
   global más alta, porque también supera UI, filtros y transiciones.
   `FireSingle` y `FireSpreadshot` requieren el mismo tratamiento para cada
   proyectil nuevo: se comparan los `FlyingBlimpLevelEnemyProjectile` antes y
   después del disparo y sólo las balas nacidas de un zepelín marcado como
   interacción reciben `CreatorToolsInteractionRenderPriority`.
7. `MatchGameplayCameraScale` multiplica la escala nativa del root por
   `(camera.orthographicSize * 2) / 720`. Cuphead usa un encuadre base de 720
   unidades, pero algunos jefes alejan la cámara; sin esta corrección el mismo
   actor se ve mucho más pequeño. Se escala el root completo para conservar la
   alineación entre sprite y `Collider2D`, no sólo el renderer. La separación
   de bounds de la etiqueta reutiliza el mismo factor; el fallback local ya lo
   recibe mediante `TransformPoint` y no debe multiplicarlo una segunda vez.

Esta normalización no se hereda automáticamente por objetos que el actor crea
después como roots independientes. Es una limitación conocida de los mini
zepelines: `FireSingle` y `FireSpreadshot` llaman a `BasicProjectile.Create`,
que instancia cada bala sin padre, por lo que en peleas con cámara alejada las
balas aún se ven más pequeñas aunque el zepelín conserve su tamaño. La futura
corrección debe aplicar el mismo factor sólo a proyectiles nacidos de un
zepelín del catálogo, escalar su root completo para mantener el `Collider2D` y
no modificar velocidad, daño ni los prefabs nativos compartidos.

### Posición y seguimiento

- Cuando el `SpriteRenderer` elegido ya tiene un sprite activo, el follower
  captura una sola ancla en `bounds.center.x`, `bounds.max.y + 14` y
  `bounds.center.z`. Por defecto se usa el renderer raíz; los actores cuyo dibujo
  vive en un hijo deben pasarlo explícitamente a `PrepareActor`.
- Esa ancla se convierte inmediatamente en un desplazamiento respecto al
  `Transform` del actor. A partir de ese momento sólo se sigue
  `actorTransform.position + actorOffset`, con rotación mundial neutra.
- Los bounds no se recalculan en cada frame. Una animación puede cambiar mucho
  el tamaño del sprite y recalcular el borde haría brincar el nombre, sobre todo
  durante la animación de muerte.
- La única excepción actual es una transición explícita entre dos actores. La
  semilla azul crea la etiqueta oculta, la transfiere a la planta y sigue sus
  bounds sólo durante 0.55 segundos de crecimiento; después vuelve a fijar un
  único offset. Cuando el sprite de la planta entra al viewport, la etiqueta
  aparece con un fade de 0.45 segundos. No se crea una segunda etiqueta y la
  muerte nunca activa seguimiento dinámico.
- Si el renderer todavía no está listo se usa temporalmente un desplazamiento
  vertical de 350 unidades. Cuando aparece un sprite válido se captura el ancla
  definitiva una sola vez.
- Estas magnitudes usan el espacio mundial de referencia de Cuphead. Con la
  cámara base de 720 unidades de alto, una unidad corresponde aproximadamente
  a un píxel del encuadre de referencia: el hueco de 14 se percibe como unos 14
  píxeles. El factor de cámara escala ese hueco para conservar su tamaño visual
  cuando un jefe acerca o aleja el encuadre.
- `SetVerticalOffsetPixels` permite un ajuste vertical por artículo después del
  ancla compartida y aplica el mismo factor de cámara. La planta de Cagney usa
  `+10`, por lo que su separación vertical final es 24 px; la luciérnaga usa
  `-70`, con una separación final de -56 px. Los dos zepelines y la zanahoria
  conservan el hueco base de 14 px.
- Nunca se debe crear un seguidor paralelo ni calcular una posición de pantalla
  para resolver una geometría distinta.
- La escala mundial copiada a la etiqueta siempre usa valores absolutos. Un
  actor puede conservar `lossyScale.x` negativo para mirar al otro lado, pero
  el texto del donador nunca debe heredarlo ni aparecer espejeado.

### Muerte, fade y destrucción

La etiqueta debe sobrevivir brevemente al actor para que el nombre no
desaparezca de golpe:

1. El follower vive en el objeto independiente de la etiqueta y conserva la
   referencia al `Transform` del actor.
2. Mientras el actor existe, `LateUpdate` actualiza únicamente su seguimiento.
3. Cuando Unity considera destruido al actor, la etiqueta deja de moverse y
   conserva su última posición. Esto evita el salto vertical visto cuando la
   muerte cambiaba los bounds del sprite.
4. Comienza un fade de 0.6 segundos. La misma opacidad se aplica al color del
   texto y al alfa del contorno.
5. El tiempo avanza con `Time.unscaledDeltaTime * CupheadTime.GlobalSpeed`.
   Con el juego pausado o en la pantalla de derrota, `GlobalSpeed` es cero: la
   etiqueta y su fade quedan congelados exactamente como el resto del juego.
6. Al llegar a opacidad cero se destruye el `GameObject` de la etiqueta. Si el
   componente de texto ya no existe, el follower destruye inmediatamente su
   propio objeto para no dejar residuos.

La cola libera el cupo activo cuando el handle reporta `IsComplete`; en los
actores simples esto sucede cuando Unity los considera destruidos. Por eso el
fade saliente puede convivir brevemente con el siguiente artículo. Es
intencional y no cuenta como otro elemento activo.

No se debe destruir la etiqueta desde `OnDestroy` del actor ni hacerla hija del
actor: cualquiera de esas dos opciones elimina el fade. Tampoco se debe seguir
consultando los bounds después de iniciar la muerte. Desactivar el actor no
equivale a destruirlo: todo ejecutor debe destruir finalmente su `GameObject` o
ampliar el contrato compartido con una señal explícita de finalización.

## Ciclo de partida, pausas y cola

- Un nivel de batalla o plataformas habilita interacciones 2.5 segundos
  después de `_OnLevelStart`. Durante carga, pausa real, final del nivel o antes
  de ese margen, no se despacha ningún artículo.
- La entrada puede proceder de la ruleta o de cualquier puerta nativa. El hook
  de `_OnLevelStart` es la autoridad principal y una reconciliación por
  `Level.Current` registra una instancia jugable que el hook haya observado
  antes de que el singleton quedara estable. Esa reconciliación compara el ID
  de instancia y nunca reinicia el margen cada frame.
- Al pausar o llegar a derrota, los actores existentes permanecen visibles y
  congelados. No se crean actores nuevos ni avanza el generador de Modo Molestoso.
- Perder el foco también puede llevar `CupheadTime.GlobalSpeed` a cero. Mientras
  el tiempo global no avance, una solicitud permanece pendiente y nunca se
  crea un actor cuya corrutina vaya a quedar congelada fuera de cámara.
- `_OnLevelEnd` suspende despachos sin borrar inmediatamente lo que está en
  pantalla. `Level.OnDestroy` realiza la limpieza definitiva de actores y del
  estado activo antes de cambiar de escena. Las solicitudes pendientes se
  conservan para el siguiente nivel válido.
- Si un reintento reutiliza la misma instancia de `Level`, el siguiente
  `_OnLevelStart` también limpia los actores del intento anterior antes de
  rearmar el margen. El polling no hace esa limpieza: sólo reconcilia IDs nuevos.
- El máximo simultáneo es persistente y configurable de 1 a 20. Se aplica por
  separado a la cola de Interacciones y a la de Modo Molestoso, por lo que con
  valor 1 puede existir un ataque activo de cada origen. Cada cola retira un
  registro activo cuando su handle termina.
- Las pruebas manuales aceptan una espera de 0 a 3600 segundos. Incluso con
  espacio disponible, dos despachos se separan por un mínimo de 0.35 segundos.
- Modo Molestoso conserva su estado aunque el panel se abra con el juego
  pausado. No depende del interruptor ni de los controles de cola de
  Interacciones: ambas fuentes pueden atacar durante la misma partida. Sólo
  genera durante una partida disponible, espera entre 1.25 y 3.25 segundos y
  usa su propia cola sin construir un backlog automático. Desactivarlo elimina
  sus pendientes y dispone sus actores activos sin tocar canjeos de donaciones.
- La lista de nombres de Modo Molestoso es opcional. Si está vacía, se encola
  `string.Empty` y el actor aparece sin texto ni sustituto predeterminado; la
  configuración vacía sigue siendo válida y puede permanecer activada entre
  reinicios.
- Batalla Molestosa usa el mismo `interactionQueue` que manual/LIVE, con la
  fuente `pesky_battle` y un único pendiente reservado. Sus cinco nombres vienen
  del roster reclutado por regalo, no de la lista aleatoria del modo libre.
  Master, Pausar y Vaciar omiten las entradas de Batalla; cancelarla, perder o
  ganar limpia únicamente esa fuente. El máximo activo sí es compartido por
  las tres fuentes de la cola.
- Batalla y Modo Molestoso libre son mutuamente exclusivos. Armar Batalla
  desactiva y guarda el modo libre, limpia `peskyQueue` y bloquea su reactivación
  mientras la sesión esté reclutando, lista, esperando nivel o activa.

## Proyectil nativo de referencia

`rootpack_homing_carrot` reutiliza
`VeggiesLevelCarrotHomingProjectile`, no una animación aproximada. La precarga
de `scene_level_veggies` conserva su prefab y un `VeggiesLevelCarrot` inerte que
permanece válido para la suscripción nativa de muerte. Cada aparición usa la
velocidad, rotación y HP de la dificultad actual, elige cualquier X del borde
superior y selecciona al jugador mediante la API original. Después de crear y
escalar el actor, sus bounds se desplazan hasta que el pixel visible más bajo
queda 16 unidades base por encima del límite; cuerpo y etiqueta nacen totalmente
fuera de cámara y entran mediante el homing nativo.

No existe un TTL agregado por el mod. La zanahoria conserva su muerte por
disparos, choque con jugador, choque con suelo y el respaldo nativo de 1000
segundos. Hasta que muera ocupa un cupo simultáneo; con máximo activo en 1 puede
bloquear el resto de la cola si nadie la elimina. `Level.OnDestroy` continúa
siendo la limpieza definitiva al abandonar o reiniciar. El renderer principal
se entrega explícitamente a `PrepareActor` porque puede vivir en un hijo del
objeto raíz.

## Enemigo nativo con transición de actor

`cagney_homing_plant` reutiliza la variante nativa `A` de
`FlowerLevelEnemySeed`, el paraguas azul que genera
`FlowerLevelVenusSpawn`. La semilla entra completamente desde arriba, conserva
la velocidad de caída de la dificultad y usa el suelo real cuando lo encuentra.
En avión se ignoran las colisiones de suelo; si no existe piso o cae por un
hueco, espera a quedar completamente debajo del borde inferior, se detiene 16
unidades base fuera de cámara e inicia allí `OnSeedLand`. La animación nativa
crea la planta, que termina de crecer y persigue al jugador con sus HP, giro,
velocidad, daño, colisiones y muerte originales.

Semilla y planta son roots distintos. Un estado compuesto conserva el mismo
cupo desde la caída hasta la muerte de la planta. La etiqueta permanece
invisible mientras cae la semilla, se transfiere al nuevo actor y comienza su
fade de entrada sólo cuando la planta es visible. La escala visual de la planta
vive en un wrapper: su root nativo mantiene `localScale.x` en `±1`, porque
`move_cr` multiplica el
avance por ese valor. Escalar directamente ese root alteraría su velocidad en
jefes con cámara alejada. El wrapper escala juntos sprite y `Collider2D` sin
modificar movimiento ni estadísticas.

Cuando la semilla cae sobre una plataforma, el estado conserva el punto de
impacto en coordenadas locales. La semilla y su dibujo de crecimiento siguen la
traslación de esa superficie incluso después de crear la mordelona. El anclaje
se libera en `FlowerLevelEnemySeed.KillSeed`, el evento de la animación que hace
desaparecer ese crecimiento. `FlowerLevelVenusSpawn` nunca se ancla a la
plataforma: desde que nace usa exclusivamente su persecución original y no
hereda escala ni rotación de la superficie.

## Enemigo nativo con seguimiento por fases

`frogs_firefly` reutiliza `FrogsLevelTallFirefly`, el enemigo incendiado que
expulsa la rana alta durante la primera fase. Su `Create` nativo conserva HP,
velocidad, daño, invencibilidad inicial, colisiones y muerte de la dificultad
actual. La corrutina original entra hacia un primer destino, desacelera y luego
repite indefinidamente la secuencia de espera y avance hacia el jugador.

El template persistente permanece inactivo para no participar en el nivel, pero
debe activarse sólo alrededor de `FrogsLevelTallFirefly.Create` y restaurarse en
un `finally`. `AbstractProjectile.Create` copia el estado activo del template e
`Init` inicia inmediatamente `initialMove_cr`; si se crea desde un template
inactivo, Unity descarta esa corrutina y deja una luciérnaga válida pero inmóvil
fuera de cámara, ocupando indefinidamente su lugar en la cola.

Como la rana no existe fuera de su nivel, el punto inicial se coloca detrás del
borde derecho y el primer destino se sortea entre 78% y 84% del ancho del
viewport, siempre en la misma altura. Esta franja acorta la entrada respecto al
antiguo 72%, conserva variación y deja cuerpo y etiqueta dentro del margen
derecho seguro. La Y se elige entre 20% y 72%, con hasta 24 intentos para
separarse de actores y jugadores. El margen inicial cubre también el ancho de
la etiqueta, por lo que cuerpo y nombre empiezan completamente fuera de cámara.
No se añade TTL: ocupa su cupo hasta morir por daño o colisión, y se limpia al
terminar el nivel.

La animación nativa fuerza `localScale.x = 1` al comenzar. La normalización de
cámara vive por ello en un wrapper y el actor conserva su escala local nativa;
así no pierde la corrección de tamaño ni deforma sprite y `Collider2D` en jefes
con zoom alejado.

Las precargas de escenas de Hilda, La pandilla raíz, Cagney y Hosco y Tosco se
serializan mediante `NativeInteractionPreloadCoordinator`. Todo cache nuevo que retenga una carga
aditiva antes de activarla debe adquirir y liberar ese coordinador, incluso en
fallo o `Dispose`, para no bloquear la cola asíncrona de escenas de Unity. Sus
prefixes Harmony deben comprobar además que `__instance` pertenece a la escena
temporal concreta; nunca deben suprimir el lifecycle de otro nivel que empiece
durante la precarga.

El mapa es la ventana preferida, pero una entrada normal no debe dejar el resto
de la cola bloqueado en `native_assets_loading`. Después del mismo margen de
2.5 segundos, y sólo con gameplay estable, sin pausa ni transición, los caches
pendientes pueden continuar serialmente. Antes de iniciar una carga, cada cache
comprueba si su escena fuente es la pelea actual: en ese caso debe capturar el
prefab de los objetos ya cargados y jamás abrir una segunda copia aditiva del
mismo jefe.

## Pasos para añadir un artículo

1. Crear un ID estable en `CreatorToolsInteractionIds.All`, su tarjeta de
   catálogo, preview y traducciones ES/EN. La colección de artículos del panel
   alimenta tanto las tarjetas como la tabla de prueba manual: no se acepta un
   artículo nuevo que sólo aparezca en una de las dos.
2. Implementar un ejecutor aislado que construya o invoque el actor desde el
   hilo principal, respete `canSpawn` y registrarlo en el controlador.
3. Aplicar `CreatorToolsInteractionPresentation.PrepareActor` después de que el
   actor esté completamente creado y antes de marcar la entrada como activa.
4. Devolver un handle del actor real para que su finalización libere el cupo
   simultáneo. Definir explícitamente si termina por muerte natural o por un TTL;
   si se elige muerte natural, documentar que conservará el cupo hasta morir y
   garantizar siempre limpieza en `EndGameplayLevel` y `Dispose`.
5. Si necesita otra geometría, extender la presentación compartida con un
   renderer o ancla configurable; mantener sin cambios el seguimiento único,
   la pausa y el fade de destrucción.
6. Verificar que una excepción al crear la etiqueta deje vivo al actor y genere
   un diagnóstico completo en el log.
7. Confirmar obligatoriamente las dos rutas de prueba: fila manual con donador,
   cantidad y espera, y selección aleatoria cuando el ejecutor esté disponible.
   `CreatorToolsInteractionIds.All` vuelve automática la elegibilidad aleatoria;
   cualquier excepción debe ser explícita y documentada.

## Prueba manual mínima

- Probar el artículo solo y con el máximo simultáneo en 2 o más.
- Confirmar la entrada prevista de cada tipo y la separación entre actores. Los
  zepelines usan alturas variadas; la zanahoria debe entrar desde fuera de todo
  el borde superior. La semilla azul también entra desde arriba; en tierra debe
  brotar al tocar piso y en avión debe desaparecer bajo el borde inferior antes
  de que la planta crezca y regrese persiguiendo al jugador. La luciérnaga debe
  entrar completamente desde la derecha, frenar dentro del encuadre y conservar
  sus pausas y avances sucesivos hacia el jugador.
- Comparar al menos un jefe con cámara base y otro con zoom alejado, como Chef
  Saleroso; sprite, colisión, etiqueta y separación deben conservar el mismo
  tamaño aparente.
- Confirmar que nombre y actor están delante del jefe y bajo los filtros del
  juego.
- Verificar que el nombre sigue al actor sin cambiar de distancia durante sus
  animaciones. Para la semilla azul debe permanecer invisible durante la caída,
  aparecer con fade cuando la planta entre a pantalla, acompañar el crecimiento
  y quedar fijada sobre la planta sin parpadeo ni texto espejeado en ninguna
  dirección.
- Matar al actor y comprobar que el nombre no salta, queda en su última posición
  y desvanece texto y contorno en aproximadamente 0.6 segundos.
- Pausar con un actor vivo y durante el fade: nada debe moverse ni desaparecer.
- Perder la partida: los actores presentes deben quedarse congelados y no deben
  llegar otros. Al abandonar o reiniciar la escena no deben quedar residuos.
- Activar Modo Molestoso desde el panel mientras el juego está pausado,
  comprobar el cambio de estado inmediato y después iniciar una partida.
- Con caches fríos, entrar inmediatamente por una puerta normal sin abrir la
  ruleta. Encolar primero el último artículo de la serie de precarga y confirmar
  que pasa de espera a activo dentro de esa misma pelea. Repetir con reintento,
  victoria, salida al mapa y un jefe que sea fuente de prefab para comprobar que
  nunca se duplica ni descarga su escena real.
