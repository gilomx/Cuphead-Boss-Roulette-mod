# Catálogo de interacciones

Esta guía define el contrato técnico que deben respetar todos los artículos
nuevos del catálogo de Creator Tools. Las implementaciones de referencia son
los mini zepelines verde y morado y la zanahoria teledirigida de La pandilla
raíz.

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
  resuelve su ejecutor por `Supports`. La prueba aleatoria obtiene candidatos de
  esa misma lista y sólo elige los que reporten `IsAvailable`.
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

### Creación y renderizado

1. La etiqueta se crea como un `GameObject` de mundo independiente, en la misma
   capa que el actor. No es `OnGUI`, un `Canvas` de pantalla ni un elemento de
   React; por eso atraviesa la cámara de gameplay y conserva los filtros
   visuales de Cuphead.
2. El texto usa `TextMeshPro`, la fuente Memphis del juego, mayúsculas, tamaño
   22, color crema y contorno oscuro. Si Memphis no se puede resolver, se busca
   otro asset Memphis cargado y finalmente se usa la fuente predeterminada.
3. En esta versión de Unity, `AddComponent<TextMeshPro>()` sustituye el
   `Transform` del objeto por un `RectTransform`. Siempre hay que obtener
   `labelText.rectTransform` después de añadir el componente; conservar una
   referencia al `Transform` anterior rompe la posición.
4. El rectángulo mide 320 × 48 y usa el pivote `(0.5, 1)`. Esta es la posición
   vertical aprobada después del ajuste visual; no debe volver a centrarse el
   pivote sin una prueba comparativa dentro del juego.
5. `BringActorToFront` mueve todos los renderers del actor a la capa de gameplay
   más alta y conserva su orden relativo. La etiqueta copia la capa del
   renderer superior y usa el siguiente `sortingOrder`. Esto mantiene actor y
   nombre al frente sin sacarlos del procesamiento de la cámara.
6. `MatchGameplayCameraScale` multiplica la escala nativa del root por
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
- Si el renderer todavía no está listo se usa temporalmente un desplazamiento
  vertical de 350 unidades. Cuando aparece un sprite válido se captura el ancla
  definitiva una sola vez.
- Nunca se debe crear un seguidor paralelo ni calcular una posición de pantalla
  para resolver una geometría distinta.

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

- Un nivel de batalla o plataformas habilita interacciones tres segundos
  después de `_OnLevelStart`. Durante carga, pausa real, final del nivel o antes
  de ese margen, no se despacha ningún artículo.
- Al pausar o llegar a derrota, los actores existentes permanecen visibles y
  congelados. No se crean actores nuevos ni avanza el generador aleatorio.
- `_OnLevelEnd` suspende despachos sin borrar inmediatamente lo que está en
  pantalla. `Level.OnDestroy` realiza la limpieza definitiva de actores y del
  estado activo antes de cambiar de escena. Las solicitudes pendientes se
  conservan para el siguiente nivel válido.
- El máximo simultáneo es persistente y configurable de 1 a 20. La cola sigue
  siendo la autoridad y retira un registro activo cuando su handle termina.
- Las pruebas manuales aceptan una espera de 0 a 3600 segundos. Incluso con
  espacio disponible, dos despachos se separan por un mínimo de 0.35 segundos.
- La prueba aleatoria conserva su estado aunque el panel se abra con el juego
  pausado. Sólo genera durante una partida disponible, espera entre 1.25 y 3.25
  segundos y nunca construye un backlog automático.

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

Las precargas de escenas de catálogo se serializan mediante
`NativeInteractionPreloadCoordinator`. Todo cache nuevo que retenga una carga
aditiva antes de activarla debe adquirir y liberar ese coordinador, incluso en
fallo o `Dispose`, para no bloquear la cola asíncrona de escenas de Unity. Sus
prefixes Harmony deben comprobar además que `__instance` pertenece a la escena
temporal concreta; nunca deben suprimir el lifecycle de otro nivel que empiece
durante la precarga.

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
  el borde superior.
- Comparar al menos un jefe con cámara base y otro con zoom alejado, como Chef
  Saleroso; sprite, colisión, etiqueta y separación deben conservar el mismo
  tamaño aparente.
- Confirmar que nombre y actor están delante del jefe y bajo los filtros del
  juego.
- Verificar que el nombre sigue al actor sin cambiar de distancia durante sus
  animaciones.
- Matar al actor y comprobar que el nombre no salta, queda en su última posición
  y desvanece texto y contorno en aproximadamente 0.6 segundos.
- Pausar con un actor vivo y durante el fade: nada debe moverse ni desaparecer.
- Perder la partida: los actores presentes deben quedarse congelados y no deben
  llegar otros. Al abandonar o reiniciar la escena no deben quedar residuos.
- Activar la prueba aleatoria desde el panel mientras el juego está pausado,
  comprobar el cambio de estado inmediato y después iniciar una partida.
