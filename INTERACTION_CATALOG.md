# Catálogo de interacciones

Esta guía define el contrato técnico que deben respetar todos los artículos
nuevos del catálogo de Creator Tools. La implementación de referencia son los
mini zepelines verde y morado.

## Arquitectura obligatoria

- `CreatorToolsServer` sólo recibe y encola solicitudes. Su hilo de red no debe
  crear, buscar ni destruir objetos de Unity.
- `CreatorToolsInteractionController` consume la cola desde el `Update`
  principal, aplica los límites comunes y delega cada artículo a un ejecutor.
- Cada ejecutor crea un actor jugable nativo y devuelve una referencia de Unity
  que la cola pueda conservar hasta que el actor sea destruido.
- La presentación compartida se aplica una sola vez con
  `CreatorToolsInteractionPresentation.PrepareActor(actor, donor, logWarning)`.
  No se debe copiar la lógica de etiqueta dentro de cada ejecutor.
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

### Posición y seguimiento

- Cuando el `SpriteRenderer` raíz ya tiene un sprite activo, el follower captura
  una sola ancla en `bounds.center.x`, `bounds.max.y + 14` y `bounds.center.z`.
- Esa ancla se convierte inmediatamente en un desplazamiento respecto al
  `Transform` del actor. A partir de ese momento sólo se sigue
  `actorTransform.position + actorOffset`, con rotación mundial neutra.
- Los bounds no se recalculan en cada frame. Una animación puede cambiar mucho
  el tamaño del sprite y recalcular el borde haría brincar el nombre, sobre todo
  durante la animación de muerte.
- Si el renderer todavía no está listo se usa temporalmente un desplazamiento
  vertical de 350 unidades. Cuando aparece un sprite válido se captura el ancla
  definitiva una sola vez.
- Si un artículo futuro dibuja su sprite principal en un hijo y no en la raíz,
  se debe ampliar la API compartida para recibir el renderer correcto. No se
  debe crear un seguidor paralelo ni calcular una posición de pantalla.

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

La cola libera el cupo activo en cuanto el actor pasa a `null`; por eso el fade
saliente puede convivir brevemente con el siguiente artículo. Es intencional y
no cuenta como otro enemigo activo.

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
  siendo la autoridad y retira un registro activo cuando su actor pasa a `null`.
- Las pruebas manuales aceptan una espera de 0 a 3600 segundos. Incluso con
  espacio disponible, dos despachos se separan por un mínimo de 0.35 segundos.
- La prueba aleatoria conserva su estado aunque el panel se abra con el juego
  pausado. Sólo genera durante una partida disponible, espera entre 1.25 y 3.25
  segundos y nunca construye un backlog automático.

## Pasos para añadir un artículo

1. Crear un ID estable, su tarjeta de catálogo, preview y traducciones ES/EN.
2. Implementar un ejecutor aislado que construya o invoque el actor desde el
   hilo principal y respete `canSpawn`.
3. Aplicar `CreatorToolsInteractionPresentation.PrepareActor` después de que el
   actor esté completamente creado y antes de marcar la entrada como activa.
4. Registrar el actor real en la cola o tracker para que su destrucción libere
   el cupo simultáneo.
5. Si necesita otra geometría, extender la presentación compartida con un
   renderer o ancla configurable; mantener sin cambios el seguimiento único,
   la pausa y el fade de destrucción.
6. Verificar que una excepción al crear la etiqueta deje vivo al actor y genere
   un diagnóstico completo en el log.

## Prueba manual mínima

- Probar el artículo solo y con el máximo simultáneo en 2 o más.
- Confirmar alturas variadas dentro de pantalla y separación entre actores.
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
