# Un ataque con valores por red

En **Interacciones**, cada ficha configura una vez el efecto, la cantidad,
la duración y cuenta regresiva de los retos, y los tiempos de espera.
Empieza con un valor de TikTok, Twitch o YouTube. **Agregar valor de otra red**
añade solamente los campos del activador de otra plataforma. **Agregar canje
de Twitch** permite combinar un canje de puntos con Bits u otro activador de
Twitch en la misma ficha.
El selector de regalos de TikTok se abre bajo demanda.

Cada valor conserva su tipo de evento: regalos, likes y follows de TikTok;
Bits, follows, suscripciones y recompensas de Twitch; gemas de YouTube.
El evento follow no necesita un costo numérico. Los valores se pueden
desactivar individualmente o quitar del ataque. La tabla muestra una fila
por configuración del ataque y una columna por red; sus controles permiten
activar, duplicar o eliminar el conjunto.

## Recompensas de Twitch

En la fila del canje, puedes elegir una recompensa del canal conectado o
crear una desde la ficha con el nombre del efecto y un precio en Puntos del
canal. Si conectaste Twitch antes de esta actualización, vuelve a autorizarlo
en **Configuración → Conexiones** para crear y administrar recompensas.
El precio en puntos y el número de canjes necesarios para disparar la
interacción son ajustes independientes.

Selecciona **Crear nueva recompensa** en el selector de Twitch. Una recompensa
nueva se crea desactivada mientras editas, para que los espectadores puedan
canjearla desde que guardes la ficha con la regla e Interacciones activas.
El mod sincroniza el estado de las recompensas que administra. Si Twitch aún
no lo confirma, la ficha muestra la sincronización pendiente.

Al crear o editar una recompensa administrada por el mod, puedes indicar el
tiempo entre canjes, el máximo de canjes por stream y el máximo por usuario
por stream. Twitch aplica estos límites. El tiempo entre canjes se puede
configurar de 1 minuto a 7 días. Un cero desactiva el límite correspondiente.
Al editar una recompensa vinculada, **Guardar interacción** envía el precio
y los límites modificados a Twitch antes de guardar la ficha.

Las recompensas creadas en el Panel del creador se vinculan por ID y conservan
su gestión en Twitch. Descarga el PNG del efecto en 28, 56 o 112 px y usa el
enlace **hazlo aquí** para abrir las recompensas de la cuenta conectada y
cambiar la imagen u otros ajustes del canje.

El guardado de la ficha es una sola operación. Todos sus valores se validan
y persisten juntos; si falla una fila, se supera el límite de reglas o falla
la escritura, se conserva la configuración previa. Si otra ventana modificó
las reglas desde que se abrió el editor, se conserva el borrador y se pide
volver a abrir el ataque para revisar el estado nuevo.

Las reglas existentes siguen en el formato plano de `.stream-rules.json`
(versión 7), con sus identificadores, nombres y estados. No hay migración
destructiva. La tabla agrupa reglas cuya configuración del efecto y tiempos
coincide. Los activadores repetidos para una misma posición en la ficha se
conservan en fichas separadas. Configurar los tiempos una vez aplica sus valores
a todas las filas. Cada regla conserva sus propios acumuladores y tiempos de espera.

La API agrupada es `POST /api/config/interactions/rules/group/set`. Usa
`schemaVersion: 1`, `expectedRevision`, una acción (`save`, `delete`, `toggle`
o `duplicate`) y `ruleIds`. `save` recibe la configuración compartida y
`values` (hasta cuatro filas, con un canje de Twitch además del valor por red).
Los valores existentes incluyen su `id` y los nuevos lo omiten. La respuesta conserva el
estado de reglas plano para que el motor, las copias de configuración y los
overlays sigan funcionando. La API individual anterior continúa compatible.
