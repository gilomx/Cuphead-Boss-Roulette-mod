# Overlays de interacciones

En **Panel → Overlays → Interacciones** hay dos plantillas: **Tarjetas** e
**Iconos flotantes**. Cada una permite elegir **Franja continua** o **Paginado**.
Hasta cuatro elementos, la franja muestra todo el grupo estático, centrado y sin
copias repetidas. Con más de cuatro, el tamaño automático reserva espacio para cuatro y enlaza el último
con el primero sin huecos. Los cambios de reglas que reduzcan el grupo a cuatro
eliminan el desplazamiento anterior y vuelven a mostrar el grupo completo.
El paginado organiza hasta nueve elementos en tres columnas, con filas ajustadas
al contenido, deslizamiento entre páginas e intervalo configurable de 3 a 60 segundos.
Las páginas conservan la misma escala al avanzar; desactivar animaciones o reducir
movimiento en el sistema cambia de página sin deslizamiento.

Cada perfil conserva sus propias redes visibles, plantilla, formato, velocidad,
intervalo y tamaño. **Tamaño automático** conserva el ajuste anterior. Al
desactivarlo, **Altura del contenido** permite ocupar del 25% al 100% de la altura
del overlay, incluyendo el margen exterior. El contenido queda centrado y nunca
excede sus límites. Hasta cuatro ítems también se limitan al ancho necesario para
mostrar el grupo completo y siguen quietos. Con más de cuatro, la franja ocupa
todo el ancho y deja pasar menos ítems al agrandarlos; repite suficientes ciclos
para evitar huecos. En paginado el porcentaje se aplica a la página completa,
conservando todos sus ítems visibles y la misma escala al avanzar. Las escenas
antiguas mantienen el tamaño automático.

La velocidad de la franja se ajusta entre **40 y 140 px/s**. Las escenas guardadas
con una velocidad menor conservan el resto de su diseño y pasan al mínimo de 40.

El ajuste **Agrandar canjes cuando hay menos opciones** sigue siendo independiente
del tamaño general. Al mostrar menos redes, los iconos de canje y sus cantidades crecen
automáticamente. Las imágenes de los efectos conservan su proporción y se ajustan
al mismo espacio cuadrado, ignorando los márgenes transparentes y conservando su
proporción. Una imagen ancha o alta nunca agranda la tarjeta ni sale de ese espacio.
Ninguna plantilla muestra nombres, categorías, títulos ni duración de los retos.
No hay una opción para activarlos; los ajustes antiguos tampoco los reactivan.
Los canjes muestran primero el símbolo y después la cantidad; en Iconos flotantes
se superponen al borde del efecto. Los iconos flotantes ajustan cada ancho a su
imagen y precios, con los valores de separación aprobados para cada perfil. Las imágenes estrechas
reservan menos ancho sin deformarse. Todas las tarjetas comparten ancho y alto,
calculados para los canjes del catálogo, con 16 px de padding interno y espacio
entre el efecto y los precios. Sólo cada tarjeta tiene fondo.
El exterior de ambas plantillas permanece transparente.

La superposición y separación de **Iconos flotantes** quedan fijas tras la
confirmación visual: en vertical, el bloque de canjes entra 29 px en la imagen
y la franja separa sus ítems 35 px. Horizontal conserva 12 y 6 px. Son medidas
del diseño base, escaladas con el overlay. Los controles temporales se retiraron;
sus antiguos campos se ignoran al cargar sin modificar el resto de la escena.
Las tarjetas mantienen su separación de 20 px y distribución independiente.

En **Tarjetas**, **Color de las tarjetas** permite cambiar el fondo y su
transparencia. **Ruido envejecido** añade grano, escamas blancas irregulares,
motas claras y arañazos finos, incluidos trazos curvos y discontinuos sobre ese fondo;
su intensidad se ajusta del 0% al 100%, con 0% para desactivarlo. La textura es
estática y queda debajo de las imágenes y las cantidades. Cada ítem conserva su
misma posición de textura: las marcas no parpadean, aparecen ni se desplazan por
su cuenta. Ambos ajustes se
guardan por perfil y se aplican tanto a la franja como al paginado.

El contenido sale de las reglas habilitadas de **Interacciones**. La cantidad
del canje corresponde a «cada N» regalos, Bits, gemas, likes u otros eventos
configurados. Los nombres de recompensas de Twitch se conservan sólo en las
etiquetas de accesibilidad, sin texto visible junto al precio. Los regalos usan
las imágenes reales del catálogo local; Bits usa el Cheermote oficial de Twitch
y Gemas el recurso de YouTube, sin insignias adicionales con el logotipo de la red.
Las reglas con distinta cantidad de efectos o distinta duración se
mantienen separadas aunque compartan la misma imagen.

El editor permite [configurar un ataque con valores por red](interaction-values.md)
mediante una sola ficha. El overlay reúne esos valores en el mismo ítem.

Si aún no hay reglas configuradas, el diseñador muestra ejemplos señalados como
tales, con regalos reales variados e iconos propios de cada red. La vista previa
paginada completa al menos dieciocho efectos por red para poder ver dos páginas,
incluso si hay pocas reglas guardadas. OBS nunca recibe esos ejemplos ni precios.
Una capa sin reglas habilitadas o sin
redes seleccionadas permanece vacía. El overlay sólo lee las reglas y no ejecuta
interacciones ni cambia la configuración de los canjes.

Los valores predeterminados toman el diseño guardado: en vertical, franja flotante
en X=4, Y=1077, de 1072 × 313, tamaño al 96%, velocidad 80, Twitch y YouTube
visibles y TikTok oculto. La capa vertical empieza activa. Se conserva el color
de tarjetas `#662effa6` y ruido al 80%. Horizontal mantiene X=60, Y=800,
1800 × 230, tamaño automático, velocidad 40, las tres redes y capa oculta.
Estos valores se usan en perfiles nuevos o al restablecer; las escenas guardadas
conservan cada preferencia explícita. Las escenas antiguas sin esta capa la
incorporan oculta. Ajusta su posición y tamaño y usa **Copiar URL para OBS** del perfil vertical u horizontal.
Las direcciones siguen siendo `/overlay/vertical` y `/overlay/horizontal`.
El recurso `/interactions-overlay?profile=vertical` también permite mostrar
sólo esta capa con su configuración guardada.

En el **Diseñador de overlays**, selecciona **Interacciones y canjes**. En sus
ajustes de presentación, **Mostrar overlay sólo en batallas activas** controla
ambos perfiles y está activado por defecto. Se guarda con **Guardar cambios**, como
`interactionsBattleOnly` en el compositor, independientemente de las reglas y
de la distribución de ataques. Usa el estado nativo de una batalla contra jefe,
tanto normal como por ruleta; la pausa no retira el overlay. Al terminar la
batalla, cargar otra escena o volver al mapa, sale con una transición de 280 ms.
La entrada usa la misma duración y un desplazamiento vertical sutil. Desactivar
animaciones o reducir movimiento omite la transición. Un regreso rápido cancela
la salida pendiente. El diseñador y las pruebas explícitas de OBS permiten
inspeccionarlo fuera de una pelea.

La configuración se guarda junto a las demás capas en
`mx.gilomx.cuphead.bossroulette.overlay-composer.json`, conservando las copias de
recuperación y las escenas previas. El paquete Dev se publica exclusivamente
mediante `tools/deploy-launcher-dev.ps1`; el launcher lo aplica al siguiente
arranque del juego.
