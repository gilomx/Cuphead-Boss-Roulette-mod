# Un ataque con valores por red

En **Interacciones**, cada ficha configura una vez el efecto, la cantidad,
la duración y cuenta regresiva de los retos, y los tiempos de espera.
Empieza con un valor de TikTok, Twitch o YouTube. **Agregar valor de otra red**
añade solamente los campos del canje de otra plataforma, hasta un valor por red.
El selector de regalos de TikTok se abre bajo demanda.

Cada valor conserva su tipo de evento: regalos, likes y follows de TikTok;
Bits, follows, suscripciones y recompensas de Twitch; gemas de YouTube.
El evento follow no necesita un costo numérico. Los valores se pueden
desactivar individualmente o quitar del ataque. La tabla muestra una fila
por configuración del ataque y una columna por red; sus controles permiten
activar, duplicar o eliminar el conjunto.

El guardado de la ficha es una sola operación. Todos sus valores se validan
y persisten juntos; si falla una fila, se supera el límite de reglas o falla
la escritura, se conserva la configuración previa. Si otra ventana modificó
las reglas desde que se abrió el editor, se conserva el borrador y se pide
volver a abrir el ataque para revisar el estado nuevo.

Las reglas existentes siguen en el formato plano de `.stream-rules.json`
(versión 7), con sus identificadores, nombres y estados. No hay migración
destructiva. La tabla agrupa reglas cuya configuración del efecto y tiempos
coincide. Si había varios valores de la misma red, se conservan en fichas
separadas. Los acumuladores y tiempos de espera de cada regla conservan su
comportamiento anterior; configurar los tiempos una vez aplica sus valores
a las reglas de cada red.

La API agrupada es `POST /api/config/interactions/rules/group/set`. Usa
`schemaVersion: 1`, `expectedRevision`, una acción (`save`, `delete`, `toggle`
o `duplicate`) y `ruleIds`. `save` recibe la configuración compartida y
`values` (de uno a tres valores, sin repetir plataforma). Los valores
existentes incluyen su `id`; los nuevos lo omiten. La respuesta conserva el
estado de reglas plano para que el motor, las copias de configuración y los
overlays sigan funcionando. La API individual anterior continúa compatible.
