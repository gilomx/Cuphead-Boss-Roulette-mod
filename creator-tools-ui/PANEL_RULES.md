# Panel de configuración: comportamiento y reglas

Este documento es el contrato de producto y desarrollo del panel React servido
en `/config` y `/dashboard`. Complementa el README técnico de
`creator-tools-ui`.

## Comportamiento actual

- La barra lateral y sus proveedores permanecen montados. Cambiar de sección
  sustituye únicamente la vista central, sin navegar a otra página ni reiniciar
  conexiones que deban seguir funcionando.
- El indicador bajo el logo es global para toda la aplicación. Su estado
  inicial es `Conectando con Cuphead`; después usa `Cambios guardados`,
  `Guardando cambios`, `Esperando confirmación` o el estado de error. No
  describe solamente el forzado de la ruleta.
- Los seis selects de forzado comienzan en la primera opción compatible. Tiro 2
  omite el valor actual de Tiro 1 para evitar duplicados. Todas las opciones
  `Nada` usan `assets/creator-tools/empty.png`, el mismo recurso del overlay.
- El área de forzado conserva espacio inferior equivalente al menú de un select
  para que las últimas listas puedan abrirse sin quedar cortadas.
- Los retos usan los iconos estáticos de `assets/creator-tools/modifiers`,
  compartidos con el Stream Overlay. Pulsarlos cambia su elegibilidad y guarda
  la lista en `Juego/RetosDesactivados` dentro de la configuración de BepInEx.
- Desactivar un reto sólo lo excluye del resultado aleatorio final. Todos los
  retos compatibles continúan recorriendo la animación mientras gira la
  ruleta.
- Siempre debe quedar al menos un reto activo de cada tipo: `plane`, `ground` y
  `both` (Avión, Tierra y Ambos). El último activo queda bloqueado en la UI y el
  servidor vuelve a validar la regla. Al cargar una configuración antigua que
  dejó una categoría vacía, el mod reactiva su primer reto disponible.
- El forzado puede seleccionar un reto aunque se haya excluido del resultado
  aleatorio: la exclusión configura el sorteo normal, no elimina contenido del
  catálogo ni de las herramientas de grabación.
- El encabezado principal comienza aproximadamente a media altura del logo para
  conservar la relación visual entre navegación y contenido.
- `/config`, `/config/roulette`, `/config/interactions` y `/dashboard` cargan la
  misma SPA. La ruta base abre Ruleta; el historial del navegador cambia de
  vista sin desmontar el shell ni volver a solicitar el documento.
- Dashboard consulta su propio estado operativo y no depende de que el catálogo
  de Ruleta ya esté disponible en el mapa. Muestra el estado del motor y de cada
  conexión por separado, además de contadores y hasta 500 eventos recientes.
  Su simulador sólo crea entradas normalizadas; no decide una interacción ni
  inventa una regla coincidente.
- Interacciones muestra el catálogo de canjeos. `hilda_purple_zeppelin`,
  `hilda_green_zeppelin`, `rootpack_homing_carrot` y
  `cagney_homing_plant` y `frogs_firefly` están disponibles en cualquier
  batalla o nivel de plataformas, entre por la ruleta o por una puerta normal.
  El mapa es la ventana preferida para preparar los prefabs originales y el
  gameplay estable puede terminar los que sigan pendientes. La zanahoria aparece como `Proyectil`; los dos
  zepelines, la semilla y la luciérnaga aparecen como `Enemigo`. Las tarjetas son un resumen
  vertical
  pequeño con el primer frame nativo arriba y la información debajo; no
  contienen descripciones, controles de prueba ni un estado operativo
  duplicado.
- El catálogo compartido clasifica los artículos anteriores como `attack` y
  los cinco mini jefes de la Baronesa como `mini_boss`. El selector de tipo
  filtra tanto tarjetas como pruebas sin perder los datos escritos por ID.
  Reglas de stream, Modo Molestoso y Batalla Molestosa reutilizan esa lista.
  La explicación de vida independiente aparece fuera de las tarjetas y, al
  elegir un mini jefe, en el formulario de reglas. No promete segundos fijos:
  la resistencia procede de la dificultad nativa y la duración depende del
  daño recibido. Están disponibles en arenas terrestres con suelo visible y en
  niveles de avión. Cala María requiere agua visible; al desaparecer, se retiran
  los mini jefes presentes y las solicitudes esperan una fase o arena compatible.
  Los demás niveles de avión usan un suelo virtual fijo al aparecer,
  exactamente en el borde inferior visible y sin margen interior.
  La UI sólo explica esta compatibilidad;
  el suelo y la disponibilidad los decide C#, sin controles nuevos en el panel.
- La zona operativa coloca la cola en el panel principal y, a su derecha, la
  configuración sobre la tabla de pruebas. Cada fila de prueba acepta donador,
  cantidad y espera en segundos. Un lote o varios tipos se agregan al final sin
  alterar el orden existente.
- El máximo simultáneo es persistente y configurable de 1 a 20. Se admiten 50
  elementos por lote, esperas de hasta 3600 segundos y 200 registros entre
  activos y pendientes. El canjeo activo permanece visible hasta que su actor
  termina o muere; entonces libera su cupo. Estos límites y el despacho
  pertenecen a C#, no a la vista.
- Los mini jefes tienen además un máximo fijo de uno en pantalla, compartido
  entre Interacciones, Modo Molestoso y Batalla Molestosa. Los siguientes esperan
  a que desaparezca el actual, aunque sean distintos. La UI muestra esta regla
  sin un control para editarla. También respetan el máximo general de elementos;
  C# vuelve a comprobar ambos límites al despachar.
- Todo artículo nuevo del catálogo se incorpora tanto a la tabla de prueba
  manual como al catálogo configurable de Modo Molestoso. La sección de
  Interacciones no contiene un generador aleatorio automático: ese uso pertenece
  exclusivamente a Modo Molestoso.
- Modo Molestoso tiene una cola operativa separada. El interruptor general,
  Pausar y Vaciar de Interacciones no la controlan; su propio interruptor basta
  porque al desactivarlo puede borrar pendientes y actores activos sin perder
  donaciones. Si ambos modos están activos, la vista informa que los ataques de
  donaciones continuarán junto con los del modo.
- Modo Molestoso muestra la configuración a la derecha de Molestias pendientes
  en escritorio, apilada en pantallas estrechas (hasta 68rem). Cantidades por
  aparición, ajustes durante minijefe y resumen usan desplegables. El botón superior
  y `Ctrl+I` enfocan su primer campo. Interacciones usa el mismo componente de
  controles, con su propio interruptor de espaciado y sus valores independientes.
  Los campos se agrupan en Ataques normales y Minijefes; las ayudas y el resumen
  explican tiempos y cantidades, sin exponer nombres de implementación.
- El intervalo de molestias normales admite 0.35–300 s y el descanso entre
  minijefes 0–300 s, con mínimo <= máximo. El primer mini compatible no consume
  ese descanso: puede entrar tras el margen seguro de inicio de 3 s. Los relojes
  se congelan en pausa, se reinician al reintentar y no se consumen entre sí.
- Los grupos `light`, `strong` y `mini_boss` se muestran como Molestias leves,
  Molestias intensas y Minijefes. Son independientes de la dificultad de Cuphead.
  Catálogo y pruebas comparten filtro de grupo, conservando borradores por ID.
  Cada rango de cantidad para leves/intensas admite enteros 1–20. El runtime
  revalida espacio y exclusividad por alta, por lo que puede salir un grupo menor.
  El modo automático mezcla artículos del mismo grupo; las Interacciones agrupan
  únicamente unidades pendientes y conservan sus donadores, regalos y retrasos.
- Permitir varias molestias intensas a la vez está apagado por defecto.
  Las intensas seleccionadas siempre son elegibles; sin marcar, Modo Molestoso
  sólo añade una cuando no queda ninguna intensa activa, contando ambas colas.
  Se revalida después de cada aparición del grupo y entre grupos distintos.
  Marcar permite concurrencia respetando los demás límites; desmarcar no retira
  los actores vivos. Interacciones conserva su admisión independiente. La lista
  individual permanece habilitada y no cambia al tocar la casilla.
  `allowConcurrentStrongInteractions` reemplaza el ajuste incorrecto
  `allowStrongInteractions`; v8 migra al nuevo default false sin traducir el
  antiguo opt-in de categoría a permiso de concurrencia.
- Guardar cambios sólo guarda la sección actual. Usar estos ajustes en ambos
  modos copia tiempos y cantidades, conservando interruptores y otros ajustes.
  Volver a los ajustes originales prepara un borrador y requiere guardar.
  El estado de conexión/guardado siempre refleja la confirmación autoritativa.
- `Nombres aleatorios` es una configuración opcional. Cero nombres no es un
  error ni bloquea el interruptor: el panel debe explicar que los ataques se
  mostrarán sin nombre y permitir guardar la lista vacía.
- Una prueba se refleja optimistamente en la tabla antes de esperar a `Update`
  de Unity. Sus filas temporales usan `Esperando al juego`; al cambiar la
  revisión del mod se eliminan y se muestra la cola autoritativa recibida. La
  UI nunca ejecuta el efecto ni lo confirma por sí misma.

## Contrato con el mod

`GET /api/config/pesky` expone los campos actuales y sus defaults con prefijo
`default`. Ambos modos usan `minimumInterval`/`maximumInterval` para molestias
normales, `miniBossMinimumInterval`/`miniBossMaximumInterval` para el descanso
entre minijefes, `lightMinimumBatch`/`lightMaximumBatch` y
`strongMinimumBatch`/`strongMaximumBatch` para cantidad por aparición. Se conservan
`miniBossIntervalMultiplier` y `maximumCompanionsDuringMiniBoss` como límites de
ataques comunes mientras hay un minijefe. `miniBossCooldownSeconds` es alias
compatible del mínimo del rango; una petición antigua con sólo ese descanso
actualiza ambos extremos. Un par nuevo omitido conserva sus valores, un par
incompleto se rechaza. Toda validación precede a cualquier mutación.

`GET /api/config/pesky/set` acepta los campos sin prefijo. Interacciones los
recibe en `/api/config/interactions/set` con prefijo `pacing.` y requiere su
propio `pacing.enabled`. La confirmación de Interacciones compara todos los
campos y `settingsRevision`. La migración a Pesky JSON v6 y la del archivo
independiente de Interacciones conservan ajustes y guardan respaldo. El descanso
antiguo se copia a ambos tiempos y los tamaños de grupo nuevos inician en 1.

`GET /api/dashboard` entrega un snapshot con `schemaVersion`, `revision`,
estado del motor, conexiones, contadores y eventos ordenados del más reciente al
más antiguo. `GET /api/dashboard/simulate` acepta `platform`, `type`, `user`,
`userId`, `amount`, `unit`, `currency`, `count` e `itemName` y responde 202 al
encolarlo o 429 si la cola temporal está llena. `user`, `userId`, `unit` y
`currency` pueden quedar vacíos para eventos que no los tengan. El procesamiento
real ocurre en el hilo principal de Unity. Este GET mutable existe sólo como
laboratorio local de la primera etapa;
los conectores reales deberán usar una entrada autenticada y acotada sin
acoplar sus payloads específicos a React o a los objetos de Unity.

El contrato v1 separa `platform`, `connector` y `connectionId`, y asigna
`eventId`, `idempotencyKey`, `streamSessionId` y una `sequence` local. Todavía
no hay conectores reales, evaluación de reglas ni acciones de gameplay: un
evento válido queda como `received` y los contadores `matched` y `queued`
permanecen en cero. El contador `valued` registra cuántos eventos
traían valor; no suma Coins, Bits o monedas ISO incompatibles entre sí.

La copia base de regalos vive en
`/assets/creator-tools/gifts/catalog.json`. `schemaVersion` describe su
estructura y `catalogVersion` sus actualizaciones de datos; la identidad de un
regalo es siempre `giftId`, no su nombre visible. Cada entrada referencia un PNG
local y el build valida el catálogo antes de compilar la SPA. `Reglas de stream`
ya usa ese snapshot para configurar reglas exactas y el backend vuelve a validar
el `giftId`; todavía no las evalúa contra eventos porque primero el contrato
normalizado debe transportar `giftId` y la semántica de rachas/deduplicación.

`GET /api/config/interactions/rules` entrega el CRUD autoritativo de reglas.
`GET /api/config/interactions/rules/set` acepta `create`, `update`, `toggle`,
`duplicate` y `delete`. C# valida regalo, interacción y límites, y persiste el
resultado con respaldo junto al config de BepInEx; React nunca es la fuente de
verdad de esas reglas.

`GET /api/config` entrega el catálogo disponible, el resultado forzado y, para
cada reto, los campos `enabled` y `canDisable`. El panel realiza cambios con
`GET /api/config/set` y mantiene el estado optimista hasta que una lectura
posterior confirma el valor guardado.

`GET /api/config/interactions` entrega disponibilidad, IDs estables, último ID,
feedback, revisiones, límites y la cola autoritativa con estados `scheduled`,
`queued` y `active`. `GET /api/config/interactions/test` recibe `item`, `donor`,
`quantity` y `delay`; sólo encola la prueba. Unity la ejecuta después en su hilo
principal y confirma el resultado incrementando la revisión. El endpoint
`GET /api/config/interactions/set` cambia el máximo simultáneo (`maxActive`)
y la imagen del regalo (`showGiftImage`). Cada parámetro es opcional y conserva
los ajustes omitidos. El parámetro heredado `maxMiniBosses` se acepta por
compatibilidad, pero siempre se normaliza a 1.
`GET /api/config/interactions` incluye `maxMiniBosses: 1` y la revisión de ajustes
para confirmar el guardado. Los códigos de feedback se traducen en React y nunca se usan como
reglas de negocio.

Los zepelines nunca se recrean con sprites, proyectiles o movimiento
aproximados. El ejecutor usa `enemyPrefabA` para el morado de disparo individual
y `enemyPrefabB` para el verde de ráfaga, conservando ambos grafos nativos
completos. Durante Hilda usa `SummonEnemy()`; en los demás niveles instancia el
clon correspondiente con propiedades frescas de la dificultad actual. El mod
adapta la posición a la cámara activa y aplica la presentación compartida. La
etiqueta es un `TextMeshPro` de mundo independiente con la fuente Memphis:
captura una sola ancla sobre el sprite, sigue el desplazamiento del actor y, al
destruirse éste, permanece fija mientras texto y contorno desvanecen durante
0.6 segundos. La altura del actor se elige al azar en el rango seguro 120–610 e
intenta conservar 165 unidades respecto a los demás actores activos. La
distancia parte de `stopDistance.RandomFloat()`, suma un desplazamiento
aleatorio de 55–105 hacia la derecha y se limita a 390–535; durante Hilda se
vuelve a escribir después de `SummonEnemy()`. No se fijan coordenadas desde
React. El contrato completo para artículos futuros está en
[INTERACTION_CATALOG.md](../INTERACTION_CATALOG.md).

El mod bloquea despachos durante carga, pausa, derrota, cierre del nivel y los
primeros 3 segundos de una partida. Los actores ya presentes permanecen
congelados al perder; la limpieza definitiva ocurre al destruirse la escena.

La validación importante siempre se repite en C#. React puede impedir una
interacción inválida por ergonomía, pero no es la autoridad para decidir qué
resultados puede producir la ruleta.

## Reglas permanentes

- La aplicación es una SPA. `AppShell`, conexiones, stores y servicios viven
  por encima de las vistas y no se desmontan al cambiar de sección.
- Los elementos compartidos —logo, navegación, indicador global, selector de
  idioma y proveedores— pertenecen al shell; una sección no los recrea.
- Las funciones que deban sobrevivir al cierre del navegador pertenecen al mod,
  no a un componente React.
- El panel sólo tiene dos locales: español (`es`) e inglés (`en`). No se
  permiten textos visibles escritos directamente en componentes.
- Una función nueva debe incluir sus traducciones en ambos idiomas.
- Los IDs recibidos desde el mod son estables; el panel resuelve sus etiquetas.
- El estado inicial de un control debe ser explícito y venir del mod. En los
  selects de catálogo, el valor predeterminado es la primera opción válida.
- Los estilos reutilizan los tokens y componentes existentes. Las vistas no
  crean colores, espaciados ni controles paralelos para resolver casos locales.
- Los cambios visuales compartidos se hacen en el componente o token base.
- Los recursos que ya cumplen una función en el overlay se reutilizan en el
  panel; no se mantienen copias visuales distintas del mismo icono.
- Los estados de conexión y las validaciones del mod nunca se ocultan.
- Una restricción de negocio se implementa en el mod y se refleja en la UI; no
  se confía únicamente en botones deshabilitados o estado local del navegador.
- Todo artículo visual nuevo reutiliza
  `CreatorToolsInteractionPresentation.PrepareActor`; no implementa su propia
  etiqueta, seguimiento o destrucción.
- Los assets se sirven localmente; el panel no depende de CDNs.
- `assets/creator-tools/config.*` son salida compilada. El código fuente vive en
  `creator-tools-ui`.

## Estructura

- `src/components`: primitivas visuales compartidas.
- `src/config`: estado persistente y comunicación con el mod.
- `src/features`: composición y comportamiento de cada sección.
- `src/i18n` y `src/locales`: infraestructura y catálogos ES/EN.
- `src/styles`: tokens y reglas visuales del sistema.
