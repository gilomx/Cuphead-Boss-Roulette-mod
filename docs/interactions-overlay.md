# Overlays de interacciones

En **Panel → Overlays → Interacciones** hay dos plantillas: **Tarjetas** e
**Iconos flotantes**. Cada una permite elegir **Franja continua** o **Paginado**.
La franja enlaza el último elemento con el primero; el paginado organiza nueve
elementos estáticos en tres columnas, con filas ajustadas al contenido y un
intervalo configurable de 3 a 60 segundos.

Cada perfil conserva sus propias redes visibles, plantilla, formato, velocidad
e intervalo. Al mostrar menos redes, los iconos de canje y sus cantidades crecen
automáticamente. Las imágenes de los efectos conservan su proporción y se ajustan
por la altura de su contenido visible, ignorando los márgenes transparentes.
Los iconos flotantes empiezan sin nombres, categorías ni duración de los retos.
La opción **Nombres de los ítems** permite mostrar u ocultar los nombres en ambas
plantillas; al elegir Tarjetas se activan y al elegir Iconos flotantes se ocultan.
Los canjes se superponen al borde del efecto y muestran primero el símbolo y
después la cantidad. Las tarjetas usan el mismo acomodo compacto al paginar.

El contenido sale de las reglas habilitadas de **Interacciones**. La cantidad
del canje corresponde a «cada N» regalos, Bits, gemas, likes u otros eventos
configurados. Los nombres de recompensas de Twitch se conservan para identificar
el canje. Las reglas con distinta cantidad de efectos o distinta duración se
mantienen separadas aunque compartan la misma imagen.

Si aún no hay reglas configuradas, el diseñador puede mostrar ejemplos señalados
como tales; OBS nunca recibe esos ejemplos. Una capa sin reglas habilitadas o sin
redes seleccionadas permanece vacía. El overlay sólo lee las reglas y no ejecuta
interacciones ni cambia la configuración de los canjes.

La nueva capa empieza oculta en los perfiles existentes. Actívala, ajusta su
posición y tamaño y usa **Copiar URL para OBS** del perfil vertical u horizontal.
Las direcciones siguen siendo `/overlay/vertical` y `/overlay/horizontal`.
El recurso `/interactions-overlay?profile=vertical` también permite mostrar
sólo esta capa con su configuración guardada.

La configuración se guarda junto a las demás capas en
`mx.gilomx.cuphead.bossroulette.overlay-composer.json`, conservando las copias de
recuperación y las escenas previas. El paquete Dev se publica exclusivamente
mediante `tools/deploy-launcher-dev.ps1`; el launcher lo aplica al siguiente
arranque del juego.
