# Catálogo de interacciones

Esta guía define el contrato técnico que deben respetar todos los artículos
nuevos del catálogo de Creator Tools. Las implementaciones de referencia son
los mini zepelines verde y morado, la zanahoria teledirigida de La pandilla
raíz, la semilla azul de Clavel de Cagney y la luciérnaga incendiada de Hosco
y Tosco, la bomba teledirigida del Dr. Kahl, el lanzamiento de cabeza de la
Baronesa Von Bon Bon, las bolas de fuego de Fósforo Sombrío y los cinco
mini jefes de la Baronesa.

## Tipo Mini jefes: Baronesa

El catálogo contiene 13 artículos. El panel distingue `attack` y `mini_boss`
y ofrece el filtro Todos / Ataques / Mini jefes, compartido con las pruebas
manuales. Las reglas, Modo Molestoso y Batalla Molestosa consumen los mismos
IDs del catálogo.

| ID | Mini jefe | HP Fácil / Normal | HP Experto |
| --- | --- | ---: | ---: |
| `baroness_cupcake` | Cupcake | 185 | 235 |
| `baroness_gumball` | Máquina de chicles | 270 | 320 |
| `baroness_waffle` | Waffle | 250 | 305 |
| `baroness_candy_corn` | Maíz dulce | 225 | 250 |
| `baroness_jawbreaker` | Caramelo gigante | 180 | 220 |

La resistencia procede de las propiedades nativas de la dificultad actual.
No existe un temporizador de desaparición: la duración depende del daño del
jugador, igual que en el combate original. Cada aparición inicializa su propio
campo `health` y conserva los receptores de daño, ataques, animación de muerte
y pausas nativos. No se conecta `OnDamageTakenEvent` al castillo ni a las
propiedades del jefe de la arena; vencer una copia no adelanta sus fases ni
provoca un knockout del nivel.

### Concurrencia de mini jefes

Sólo puede haber **un mini jefe en pantalla**, sea del mismo tipo o de otro.
Es un límite fijo compartido entre donaciones/pruebas, Modo Molestoso y
Batalla Molestosa; también se respeta el máximo general de cada cola.
Todas las nuevas entradas de mini jefes esperan mientras exista uno activo.
El panel muestra esta regla sin ofrecer un límite editable.
`MiniJefesMaximosEnPantalla` y `maxMiniBosses` se conservan por compatibilidad
con configuraciones y clientes anteriores, pero siempre se normalizan a 1;
ni enviar 2 por API ni editar la configuración permite otro mini jefe.

El ejecutor implementa `ICreatorToolsExclusiveInteractionExecutor` y devuelve
`interaction_type_active` como espera temporal si alguien intenta saltarse
la validación de cola. Se cuentan los cuerpos activos hasta su destrucción,
incluida la animación de muerte; el fade de etiqueta y los efectos restantes
no reservan otro cupo. La captura de actores se reutiliza durante el mismo
frame y se invalida inmediatamente después de cada aparición.

También se reconocen los mini jefes nativos presentes. Durante la ronda de
mini jefes de la pelea original de la Baronesa, los del catálogo esperan hasta
`BaronessLevelCastle.State.Chase`, incluyendo las pausas entre convocatorias.
Así una convocatoria posterior del castillo no crea un duplicado inesperado.
El mod no bloquea ni altera el avance de esa pelea original.

`CreatorToolsMiniBossSpawnPolicy` y el harness prueban que las 25 parejas entre
los cinco IDs quedan bloqueadas, incluso con valores antiguos mayores que 1,
así como la liberación del único cupo y las entradas vacías.

Se despachan en arenas terrestres con suelo visible y en niveles de avión.
La detección usa el jugador `PlanePlayerController`, incluyendo Hilda,
Djimmi, Titi Trinos, Robot, Esther y las dos peleas aéreas del casino. Cala
María conserva la condición de agua visible. En una arena terrestre cuyo
suelo lógico queda fuera de cámara o en la cueva de Cala, el canje permanece
pendiente para la siguiente arena compatible. El despachador omite esa
entrada temporalmente y permite avanzar a las demás. Modo Molestoso usa la
misma disponibilidad.

En los aviones distintos de Cala, los cinco usan un suelo virtual de la
interacción: `cameraY - orthographicSize`, exactamente en el borde inferior
visible al aparecer y sin margen interior. Los patrones nativos conservan su
recorrido: algunos frames pueden cruzar ese borde, incluido Waffle, que baja
unas 82 unidades base por debajo de su suelo nominal. No se añaden
compensaciones ni colliders; se conservan `Level.Ground` y el área de movimiento
del jugador. Esta altura mundial se fija al aparecer: Gumball conserva la Y
inicial, CandyCorn almacena
`bottomPoint` y Waffle guarda referencias del pivote; hacer que sólo Cupcake
siguiera una cámara móvil daría alturas incoherentes. Hilda y Robot desplazan
la cámara ligeramente con el jugador, y Mr. Chimes la desplaza en X; se
conservan sus ajustes nativos. Las salidas temporales del patrón y el recorte
de extremos aún requieren revisión visual en combate.

En Cala María, `FlyingMermaidLevelSplashManager` aporta el collider físico de
entrada al agua. Su borde superior es el suelo exclusivo de la interacción;
también se comprueban los renderers nativos `wave1`/`wave2` y el encuadre. No
se crea un manager artificial ni se sustituye el agua por el piso virtual de
otros aviones cuando ésta desaparece. Todos
los puntos iniciales usan esa altura. Cupcake adapta tanto el aterrizaje como
las salpicaduras a ese suelo, y Jawbreaker persigue al avión mediante su
`AbstractPlayerController` nativo. La vida conserva los mismos HP.

En avión, el cuerpo de cada mini jefe y sus secundarios usan el **80 % del
tamaño anterior** sobre la compensación de cámara. La reducción se aplica al
root completo para alinear sprites y colliders, y se mantiene tras los giros
nativos que restablecen la escala. Los actores terrestres conservan su tamaño.
Es una proporción estable respecto al avión normal, sin cambiar al activar el
mini avión del jugador. Como referencia local, la relación lineal entre áreas
visibles de Cuphead y su avión es aproximadamente 0.76; entre sus hitboxes es
0.845. El factor 0.8 es un punto inicial de ajuste visual.

Los márgenes corporales de avión (Gumball 182, CandyCorn 122 y aterrizaje de
Cupcake 120 unidades base) usan esa misma reducción. Los demás límites de
recorrido, velocidades, HP y tiempos conservan su ajuste de cámara;
`cameraScale` y el factor corporal están separados.
Las piezas hijas de Waffle heredan el tamaño sin volver a reducir su separación
local; sus secundarios independientes se ajustan una sola vez al registrarlos.

Las copias aéreas de CandyCorn y sus mini corns convierten sus colliders a
triggers antes del primer paso de física. Los prefabs originales usan cuerpos
cinemáticos sólidos, mientras las balas y bombas básicas de avión tienen
colliders sólidos sin Rigidbody2D; esa combinación no genera contactos con
`useFullKinematicContacts` desactivado. Los callbacks de trigger y colisión de
Cuphead llegan al mismo `checkCollision`, por lo que se conservan receptores,
HP, impactos y muerte nativos. El cambio sólo afecta copias del catálogo en avión.
`tools/verify_native_aircraft_collision_contract.py` verifica los siete prefabs
relevantes del juego instalado; el contrato IL comprueba callbacks y daño de
mini corns. Regla física documentada en
[Unity 2017.4](https://docs.unity3d.com/2017.4/Documentation/ScriptReference/Rigidbody2D-useFullKinematicContacts.html).

Al entrar en `States.Head` o perder el agua visible, los actores que la usaban
se retiran junto con sus secundarios; las nuevas solicitudes esperan. La fase
se comprueba explícitamente porque las olas pueden seguir activas con otras
capas de renderizado durante la transición a la cueva.
Los límites globales del nivel y los actores originales permanecen intactos.
`usesAircraftArena` gobierna tamaño, colisiones y altura aérea;
`usesWaterFloor` gobierna exclusivamente el agua y la retirada en Cala.
No se debe usar la presencia de agua para decidir daño o tamaño de otros aviones.
Esta compatibilidad inicial todavía requiere la prueba de combate para
ajustar altura visual y duración frente a balas, bombas y supers de avión.

`NativeBaronessMiniBossCache` conserva copias inactivas de los cinco prefabs
serializados en el castillo ya retenido por `NativeBaronessHeadTossCache`.
Comparte esa precarga y no carga otra vez la escena. El guard de lifecycle
suprime las plantillas, pero cada copia jugable ejecuta su propio `Awake`
antes de `Init`. No se clonan actores de un combate que ya haya comenzado.

Los cinco previews PNG locales se regeneran mediante
`tools/extract_native_baroness_mini_boss_previews.py`. Gumball combina cuerpo,
tapa y piernas respetando los anclajes de los sprites originales.

`tools/verify_native_baroness_miniboss_contract.ps1` valida contra el DLL del
juego los HP de las tres dificultades, prefabs, firmas `Init`, vida propia,
coordenadas usadas por los adaptadores y campos de propiedad de secundarios.
Acepta `-CupheadDir` y `-CecilPath`; inspecciona IL sin arrancar Unity.

Prueba en partida requerida antes de distribuir: ejecutar cada mini jefe con
el jefe de la arena fuera de la línea de disparo, comprobar que sólo baja la
vida del mini jefe y que al morir se libera el cupo. Repetir con dos tipos
distintos y una solicitud duplicada que deba quedar pendiente, además de
pausa, derrota, reintento y vaciado de cola; verificar también sus proyectiles
secundarios y el nombre/regalo del donador. Los builds y el harness de streaming
no sustituyen esta comprobación dentro de Unity.

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
   28, color crema y contorno oscuro. Si Memphis no se puede resolver, se busca
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
   alineación entre sprite y `Collider2D`, no sólo el renderer. La etiqueta
   calcula su escala sólo desde la cámara actual, sin heredar la escala nativa
   ni la reducción corporal del actor. El fallback local de los ataques
   estándar ya recibe la escala del actor mediante `TransformPoint` y no debe
   multiplicarse una segunda vez.

Esta normalización no se hereda automáticamente por objetos que el actor crea
después como roots independientes. Es una limitación conocida de los mini
zepelines: `FireSingle` y `FireSpreadshot` llaman a `BasicProjectile.Create`,
que instancia cada bala sin padre, por lo que en peleas con cámara alejada las
balas aún se ven más pequeñas aunque el zepelín conserve su tamaño. La futura
corrección debe aplicar el mismo factor sólo a proyectiles nacidos de un
zepelín del catálogo, escalar su root completo para mantener el `Collider2D` y
no modificar velocidad, daño ni los prefabs nativos compartidos.

### Posición y seguimiento

- Para los ataques estándar, cuando el `SpriteRenderer` elegido ya tiene un
  sprite activo, el follower
  captura una sola ancla en `bounds.center.x`, `bounds.max.y + 14` y
  `bounds.center.z`. Por defecto se usa el renderer raíz; los actores cuyo dibujo
  vive en un hijo deben pasarlo explícitamente a `PrepareActor`.
- Esa ancla se convierte inmediatamente en un desplazamiento respecto al
  `Transform` del actor. A partir de ese momento sólo se sigue
  `actorTransform.position + actorOffset`, con rotación mundial neutra.
- En este seguimiento estándar los bounds no se recalculan en cada frame.
  Mantener el offset evita que los cambios de forma de sus animaciones,
  incluida la muerte, hagan brincar el nombre.
- Las transferencias explícitas entre actores pueden abrir una ventana breve
  de seguimiento dinámico. Por ejemplo, la semilla azul crea la etiqueta
  oculta, la transfiere a la planta y sigue sus
  bounds sólo durante 0.55 segundos de crecimiento; después vuelve a fijar un
  único offset. Cuando el sprite de la planta entra al viewport, la etiqueta
  aparece con un fade de 0.45 segundos. No se crea una segunda etiqueta y la
  muerte de la planta no activa otra ventana de seguimiento dinámico.
- En los ataques estándar, si el renderer todavía no está listo se usa temporalmente un desplazamiento
  vertical de 350 unidades. Cuando aparece un sprite válido se captura el ancla
  definitiva una sola vez.
- Estas magnitudes usan el espacio mundial de referencia de Cuphead. Con la
  cámara base de 720 unidades de alto, una unidad corresponde aproximadamente
  a un píxel del encuadre de referencia. El factor de cámara escala la
  separación del ancla para conservar su tamaño visual
  cuando un jefe acerca o aleja el encuadre.
- `SetVerticalOffsetPixels` permite un ajuste vertical por artículo después del
  ancla compartida y aplica el mismo factor de cámara. La planta de Cagney usa
  `+10`, por lo que el desplazamiento vertical del ancla es 24 px; la luciérnaga
  usa `-70`, con un desplazamiento de -56 px. Los dos zepelines y la zanahoria
  conservan el desplazamiento base de 14 px. Estas cifras del seguimiento
  estándar sitúan el pivote del rectángulo, no el borde inferior de las letras.
- Nunca se debe crear un seguidor paralelo ni calcular una posición de pantalla
  para resolver una geometría distinta.
- Los nombres usan siempre fuente 28 y una escala mundial uniforme calculada
  sólo con `camera.orthographicSize / 360`, como referencia común del catálogo.
  No heredan `actor.lossyScale` ni el factor compuesto del cuerpo: reducir un
  mini jefe a 0.8 o pasar el nombre a la cabeza de la Baronesa no reduce la letra.
  El regalo comparte esa escala. Cada `LateUpdate` actualiza el factor desde la
  cámara y reajusta la separación y los offsets explícitos, incluso si el zoom
  cambia después de crear la etiqueta. La escala del texto es positiva en ambos
  ejes y nunca se espejea al girar.

Los cinco mini jefes activan explícitamente `FollowAnimatedBody` en el mismo
follower compartido. Para ellos se recalcula el ancla en cada `LateUpdate` a
partir de los vértices reales de `Sprite.vertices`, transformados al mundo con
la escala, rotación y `flipX`/`flipY` actuales. Se cachean los vértices de cada
sprite, no sus bounds mundiales. Así el canvas transparente y los cambios de
pivote de Cupcake no fijan el nombre en una posición ajena al dibujo.

| Mini jefe | Ancla visual dinámica |
| --- | --- |
| Cupcake | Renderer raíz; sigue el dibujo de cada frame del salto y el golpe. |
| Maíz dulce | Renderer raíz. |
| Caramelo gigante | Renderer del hijo `Sprite`; el renderer raíz está vacío. |
| Waffle | Cuerpo raíz mientras esté visible; boca central como alternativa. Las ocho piezas expulsadas y sus efectos no forman el ancla. |
| Máquina de chicles | Unión del cuerpo raíz y la tapa visible; excluye polvo, chispas y explosiones. |

Antes de medir estas anclas, el follower invoca `RestoreActorSize`. El callback
restaura la escala corporal tras los giros nativos y evita depender del orden
entre los `LateUpdate` del estado y la etiqueta.

En este modo, los 14 píxeles base separan el borde superior del dibujo del
borde inferior real del texto o del regalo visible, el que quede más abajo.
Se conserva el pivote `(0.5, 1)`: tras `ForceMeshUpdate()` se mide el borde de
los caracteres visibles, incluidos los que usan submallas de fuentes
alternativas, y se compensa su desplazamiento local. La medición se renueva al
cambiar la presentación del regalo. Texto, icono y separación usan el factor
de cámara común, sin alterar la geometría de los actores.

### Muerte, fade y destrucción

La etiqueta debe sobrevivir brevemente al actor para que el nombre no
desaparezca de golpe:

1. El follower vive en el objeto independiente de la etiqueta y conserva la
   referencia al `Transform` del actor.
2. Mientras el actor existe, `LateUpdate` mantiene su seguimiento y comprueba
   que al menos un sprite del actor esté activo, con alfa visible, dentro del
   encuadre y de las capas dibujadas por la cámara. Si se oculta o sale de
   cámara, el nombre y regalo también se ocultan. La etiqueta puede reaparecer
   si vuelve el actor, incluyendo cuerpos con varias piezas y cambios de actor.
   Los mini jefes comprueban los meshes de sus anclas corporales configuradas;
   una pieza expulsada o un efecto aislado no conserva el nombre en pantalla.
3. Cuando Unity considera destruido al actor, la etiqueta deja de moverse y
   conserva su última posición, sin volver a consultar la geometría destruida.
4. Comienza un fade de 0.6 segundos. La misma opacidad se aplica al color del
   texto, al alfa del contorno y al regalo.
5. El fade de destrucción usa `Time.unscaledDeltaTime` y termina aunque el
   juego esté pausado o en derrota. Sólo termina etiquetas cuyo objetivo ya
   se destruyó; pausar por sí solo no elimina el nombre de un actor visible.
6. Al llegar a opacidad cero se destruye el `GameObject` de la etiqueta. Si el
   componente de texto ya no existe, el follower destruye inmediatamente su
   propio objeto para no dejar residuos.

La cola libera el cupo activo cuando el handle reporta `IsComplete`; en los
actores simples esto sucede cuando Unity los considera destruidos. Por eso el
fade saliente puede convivir brevemente con el siguiente artículo. Es
intencional y no cuenta como otro elemento activo.

No se debe destruir la etiqueta desde `OnDestroy` del actor ni hacerla hija del
actor: cualquiera de esas dos opciones elimina el fade y puede romper una
transferencia a otro objetivo. Los ataques estándar conservan su ancla fija
durante la animación de muerte; los mini jefes continúan siguiendo su cuerpo
visible hasta que se destruye el objetivo. Desactivar el actor no
equivale a destruirlo: todo ejecutor debe destruir finalmente su `GameObject` o
ampliar el contrato compartido con una señal explícita de finalización.
La política `CreatorToolsDonorLabelLifetime` tiene pruebas de pérdida de
objetivo, ocultación reversible y transferencia entre actores. Los snapshots
de fin de nivel comprueban que el actor sea visible y capturan las etiquetas
antes de apagar los sprites originales; no congelan nombres huérfanos.

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

Las precargas de escenas de Hilda, La pandilla raíz, Cagney, Hosco y Tosco,
Dr. Kahl y la Baronesa se
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

## Bomba teledirigida del Dr. Kahl

`robot_homing_bomb` reutiliza `RobotLevelHatchBombBot`, el prefab `secondary`
de `RobotLevelRobotHatch` durante la primera fase de `scene_level_robot`.
No conserva un robot auxiliar ni depende de que el jefe siga presente.

La bomba entra completamente desde la derecha, a una Y aleatoria entre 15%
y 80% del viewport. Hasta 24 candidatos ayudan a separar las entradas de otros
actores y jugadores. El margen inicial incluye el nombre del donador. Usa el
mismo lanzamiento hacia la izquierda, duración inicial aleatoria y transición
de 4 segundos al homing que `RobotLevelRobotHatch.OnSecondaryAttack`.

`HomingProjectile.Create` configura una copia inactiva; `InitBombBot` debe
ejecutarse antes de activarla, porque `Start` necesita esas propiedades para
configurar daño y movimiento. HP, velocidades, giro, colisiones, animación de
explosión y tiempo de vida proceden de la dificultad nativa. No se añade TTL:
se conservan tanto el límite nativo del homing como los respaldos originales
de `AbstractProjectile`. El cupo se libera cuando se destruye el actor, después
de la explosión cuando corresponde, no al primer evento `Die`.

La escala de cámara vive en un wrapper que incluye sprite y colisiones durante
vuelo y explosión. El nombre y el icono de regalo reutilizan la presentación
compartida, con el hueco base de 14 px y el fade de 0.6 segundos al destruirse.
Salir del nivel o reintentar elimina también el wrapper.

La tarjeta, la fila de prueba manual, las reglas de stream, Modo Molestoso y
Batalla Molestosa comparten el ID. El preview se extrae de
`robot_ph1_bombot_0001` en `atlas_robotlevel_hq` mediante
`tools/extract_native_robot_homing_bomb_preview.py`.

## Lanzamiento de cabeza de la Baronesa

`baroness_head_toss` reproduce una sola ejecución del ataque final de
`scene_level_baroness`. La apariencia encadena los estados nativos
`Castle_Chase` y `Castle_Toss`: el `Animator` y la jerarquía del castillo se
conservan para que las dos capas
`BaronessPhase2` y `BaronessPhase2Top` reciban sus sprites originales, mientras
todos los renderers del castillo, brazos, dientes y fondo permanecen ocultos.
Los scripts, colliders y rigidbodies de esa copia visual también quedan
inertes; sólo la cabeza lanzada puede interactuar con el jugador.

La Baronesa comienza completamente fuera del borde derecho, incluido el ancho
de la etiqueta. Durante los primeros 20/24 segundos avanza hacia la esquina
inferior derecha con el ciclo original `Castle_Chase`; el controlador espera a
que ese ciclo complete sus 21/24 segundos antes de entrar en `Castle_Toss`.
Así el tramo corto de caminata funciona como aviso del ataque. Su posición de
ataque deja 24% del ancho visible del dibujo
fuera del borde derecho y 70 píxeles bajo el borde inferior. La altura es fija;
no participa en el sorteo vertical de los proyectiles. El evento nativo
`FireHead` se reproduce manualmente una sola vez en el frame 19 del lanzamiento
(40/24 segundos desde la aparición). El punto de salida se lee del transform
animado `BaronessTossPoint` de la copia. `Castle_Toss` conserva sus 42 frames
completos; luego el controlador regresa a `Castle_Chase` y la Baronesa camina
fuera de cámara entre 63/24 y 83/24. Sus renderers sólo se ocultan cuando ha
terminado ese recorrido.

Los bounds usan `textureRectOffset`, `textureRect` y el pivote del sprite,
porque los frames originales miden 1128 × 960 pero concentran el dibujo en una
zona mucho menor. La entrada y la salida compensan también el desplazamiento
de la cámara durante ese intervalo para permanecer pegadas al mismo borde.

La cabeza es `BaronessLevelFollowingProjectile` y recibe directamente las
propiedades `baronessVonBonbon` de la dificultad actual. Conserva el objetivo
inicial, redirecciones, velocidad, animación, daño, collider y muerte del juego.
El nombre y el icono de regalo nacen sobre la Baronesa y se transfieren a la
cabeza al salir, sin crear una segunda etiqueta. El castillo invisible se
mantiene como padre inerte para la suscripción nativa de muerte hasta que la
cabeza abandona completamente la cámara o se destruye; existe un respaldo de
24 segundos para liberar siempre el cupo activo.

La precarga bloquea el lifecycle de los componentes `BaronessLevel*` de la
escena temporal y también reconoce copias marcadas de la interacción. Esto
evita que la jerarquía invisible inicie fases, daño o corrutinas del jefe. El
proyectil no lleva esa marca, por lo que ejecuta normalmente su `Awake`,
`Start`, movimiento y colisiones. El preview usa
`baroness_head_toss_0009` de `atlas_baronesslevel` y se puede regenerar con
`tools/extract_native_baroness_head_toss_preview.py`.

## Bolas de fuego de Fósforo Sombrío

`dragon_fireballs` reutiliza el dragón de la primera fase y el ataque nativo de
meteoros de `scene_level_dragon`. El cuerpo comienza completamente fuera del
borde derecho, entra durante 20/24 segundos y queda centrado verticalmente con
27% de su ancho fuera de cámara. El trigger
`OnMeteor` enlaza el ciclo de aviso con `MeteorStart`, un ciclo completo de
`Meteor_Anticipation_Loop`, `Meteor_Anticipation_End`, `Meteor_Attack` y
`Meteor_Attack_End`. El dragón vuelve a salir durante 20/24 segundos después del
ataque y sus sprites sólo se ocultan al completar el recorrido exterior.

Cada canje sortea un patrón de tres lanzamientos construido con los mismos
estados del ataque original: `State.Up` crea una bola con recorrido ascendente,
`State.Down` crea una con recorrido descendente y `State.Both` crea ambas a la
vez. Los patrones alternan la dirección como los patrones nativos y algunos
insertan el par simultáneo. El `Animator` conserva `Repeat` entre ataques, espera
el `shotDelay` de la dificultad actual y reproduce `FireMeteor` en el frame 7 de
cada `Meteor_Attack`; sólo después del tercero enlaza `Meteor_Attack_End` y la
salida del dragón.

Todas las bolas salen del transform animado `MouthRoot` y usan `speedX` y
`timeY` de la dificultad actual. Conservan animación, humo, sonido, trayectoria,
daño, collider y destrucción originales. El nombre y el regalo se transfieren a
la primera bola sin duplicarse sobre los proyectiles siguientes.

El ejecutor es exclusivo por tipo. Mientras un cuerpo de Fósforo no haya
completado su salida, cualquier otro `dragon_fireballs` conserva su posición
pendiente en la cola. El selector puede despachar otros artículos elegibles que
estén detrás y vuelve a habilitar al dragón justo cuando el cuerpo anterior queda
fuera de pantalla; las bolas que todavía viajen no prolongan esa espera.

La copia visual del dragón es decorativa: todos sus `MonoBehaviour`,
`Collider2D` y `Rigidbody2D` están desactivados, por lo que el cuerpo no puede
dañar, empujar ni recibir disparos. Sólo las dos bolas tienen hitbox. La precarga
serializada aísla el lifecycle de los componentes `DragonLevel*` tanto en la
escena temporal como en las copias marcadas de la interacción; los meteoros no
llevan esa marca para que ejecuten normalmente su movimiento y colisiones. El
preview usa `dragon_meteor_forward_0007` de `atlas_dragonlevel_nobg` y se
regenera con `tools/extract_native_dragon_fireballs_preview.py`.

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
   renderer o ancla configurable. El seguimiento dinámico requiere una opción
   explícita como `FollowAnimatedBody`; conservar un solo follower, la pausa
   del actor y el fade de destrucción compartido.
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
- La bomba del Dr. Kahl debe entrar desde la derecha a alturas variadas, pasar
  de su lanzamiento inicial a la persecución y conservar su explosión por daño,
  choque con jugador u otra bomba. Probar también su tiempo de vida original,
  precarga desde el mapa y captura dentro de la propia pelea del robot.
- La Baronesa debe aparecer sola en la esquina inferior derecha, sin ningún
  píxel visible del castillo, lanzar exactamente una cabeza y desaparecer. La
  cabeza debe conservar sus redirecciones nativas, daño y colisiones; el nombre
  debe transferirse del cuerpo al proyectil sin duplicarse.
- Fósforo debe entrar desde el borde derecho, completar la anticipación de
  meteoros y escupir una pareja ascendente/descendente antes de volver a salir.
  Confirmar que tocar o disparar al cuerpo no produce daño ni impacto y que sólo
  las bolas conservan daño y colliders. El nombre debe pasar a una sola bola.
- Comparar al menos un jefe con cámara base y otro con zoom alejado, como Chef
  Saleroso; sprite, colisión, etiqueta y separación deben conservar el mismo
  tamaño aparente.
- Confirmar que nombre y actor están delante del jefe y bajo los filtros del
  juego.
- Verificar que los ataques estándar conservan su desplazamiento de ancla
  durante sus animaciones. Para la semilla azul debe permanecer invisible durante la caída,
  aparecer con fade cuando la planta entre a pantalla, acompañar el crecimiento
  y quedar fijada sobre la planta sin parpadeo ni texto espejeado en ninguna
  dirección.
- En los cinco mini jefes, comprobar que las letras y el regalo siguen el
  dibujo con su separación de 14 px: Cupcake durante todo el salto y descenso,
  ambos caramelos al girar, Waffle al separarse y reunirse, y Gumball al abrir
  la tapa. Las piezas y efectos independientes no deben arrastrar el nombre.
- Comparar el tamaño común 28 en tierra y avión, durante cambios de zoom y al
  transferir el nombre de la Baronesa a su cabeza. La reducción corporal de
  los mini jefes no debe reducir texto ni regalo.
- Destruir al actor y comprobar que el nombre queda en su última posición y
  desvanece texto, contorno y regalo en aproximadamente 0.6 segundos.
- Pausar con un actor vivo: debe conservar su nombre mientras siga visible.
  Si el actor ya fue destruido, el fade de la etiqueta debe terminar incluso
  durante la pausa.
- Perder la partida: los actores presentes deben quedarse congelados y no deben
  llegar otros. Al abandonar o reiniciar la escena no deben quedar residuos.
- Activar Modo Molestoso desde el panel mientras el juego está pausado,
  comprobar el cambio de estado inmediato y después iniciar una partida.
- Con caches fríos, entrar inmediatamente por una puerta normal sin abrir la
  ruleta. Encolar primero el último artículo de la serie de precarga y confirmar
  que pasa de espera a activo dentro de esa misma pelea. Repetir con reintento,
  victoria, salida al mapa y un jefe que sea fuente de prefab para comprobar que
  nunca se duplica ni descarga su escena real.
