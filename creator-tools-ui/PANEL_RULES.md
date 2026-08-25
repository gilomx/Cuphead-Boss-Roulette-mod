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
- La zona operativa coloca la cola en el panel principal y, a su derecha, la
  configuración sobre la tabla de pruebas. Cada fila de prueba acepta donador,
  cantidad y espera en segundos. Un lote o varios tipos se agregan al final sin
  alterar el orden existente.
- El máximo simultáneo es persistente y configurable de 1 a 20. Se admiten 50
  elementos por lote, esperas de hasta 3600 segundos y 200 registros entre
  activos y pendientes. El canjeo activo permanece visible hasta que su actor
  termina o muere; entonces libera su cupo. Estos límites y el despacho
  pertenecen a C#, no a la vista.
- La prueba aleatoria tiene un interruptor con estado optimista inmediato. Su
  escritura se serializa y se confirma mediante una revisión independiente para
  que respuestas antiguas no reviertan el último clic. Sólo genera dentro de
  una partida activa y no acumula eventos mientras el juego no puede recibirlos.
  Todo artículo nuevo del catálogo se incorpora también a la tabla de prueba
  manual y al conjunto elegible de esta prueba aleatoria.
- Una prueba se refleja optimistamente en la tabla antes de esperar a `Update`
  de Unity. Sus filas temporales usan `Esperando al juego`; al cambiar la
  revisión del mod se eliminan y se muestra la cola autoritativa recibida. La
  UI nunca ejecuta el efecto ni lo confirma por sí misma.

## Contrato con el mod

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
no hay conectores reales, persistencia, evaluación de reglas ni acciones de
gameplay: un evento válido queda como `received` y los contadores `matched` y
`queued` permanecen en cero. El contador `valued` registra cuántos eventos
traían valor; no suma Coins, Bits o monedas ISO incompatibles entre sí.

`GET /api/config` entrega el catálogo disponible, el resultado forzado y, para
cada reto, los campos `enabled` y `canDisable`. El panel realiza cambios con
`GET /api/config/set` y mantiene el estado optimista hasta que una lectura
posterior confirma el valor guardado.

`GET /api/config/interactions` entrega disponibilidad, IDs estables, último ID,
feedback, revisiones, límites y la cola autoritativa con estados `scheduled`,
`queued` y `active`. `GET /api/config/interactions/test` recibe `item`, `donor`,
`quantity` y `delay`; sólo encola la prueba. Unity la ejecuta después en su hilo
principal y confirma el resultado incrementando la revisión. El endpoint
`GET /api/config/interactions/set` cambia el máximo simultáneo y la prueba
aleatoria. Los códigos de feedback se traducen en React y nunca se usan como
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
primeros 2.5 segundos de una partida. Los actores ya presentes permanecen
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
