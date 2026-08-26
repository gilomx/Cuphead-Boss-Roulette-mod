# Creator Tools: continuación del motor de streaming

Fecha del traspaso: **2026-08-26**

Rama: `codex/creator-tools-config-panel`

Este documento cubre únicamente la integración multistream que comienza en
`/dashboard`. El historial completo del mod y las protecciones de fases siguen
en `PROJECT_HANDOFF.md`.

## Estado que ya existe

- `/dashboard`, `/dashboard/` y `/dashboard.html` sirven la misma SPA React que
  `/config`, sin desmontar el shell, el estado compartido ni la localización.
- `GET /api/dashboard` publica estado del motor, conexiones, contadores y los
  eventos recientes.
- `GET /api/dashboard/simulate` valida el regalo y registra su vencimiento en
  el hilo HTTP; nunca modifica Unity. La agenda monotónica admite hasta 1024
  pruebas y `Update` ejecuta como máximo 64 eventos vencidos por frame. El
  historial circular conserva los últimos 500 eventos.
- El Dashboard tiene tarjetas de conexión, feed, contadores y simulador en
  español e inglés. TikFinity publica ahora su estado real de API local;
  Twitch y YouTube permanecen marcados como pendientes.
- El backend simulado todavía crea una sola conexión por plataforma y
  `/api/dashboard/simulate` no recibe `connectionId`; el enrutamiento
  multiconexión se implementa junto con los adaptadores reales.
- TikFinity ya es una conexión real. El mod inicia de forma invisible el EXE
  autocontenido de `companion`, consume su NDJSON y lo termina con Cuphead. El
  usuario sólo necesita mantener abierta la aplicación normal de TikFinity.
- La primera vertical funciona de punta a punta: regalo exacto -> deduplicación
  -> cierre de racha -> reglas -> acumuladores por regla/conexión -> cola de
  interacciones. `matched` y `queued` reflejan actividad real y simulada.
- Los umbrales consumidos que no caben en la cola de 200 se conservan en un
  backlog de despacho y se reintentan desde `Update`; editar o eliminar una
  regla no borra deuda ya ganada. Cada deuda mantiene la interacción que estaba
  configurada al recibir el regalo.
- El CRUD persiste una instantánea candidata antes de reemplazar las reglas
  activas. Un `save_failed` deja intacto tanto el motor como sus acumuladores.
- El endpoint de reglas ejecuta ese CRUD .NET/filesystem de forma serializada
  en su propio hilo HTTP y responde con el estado confirmado aunque Cuphead no
  tenga foco. El juego conserva su comportamiento nativo: al perder el foco se
  congela. Ningún endpoint debe bloquear ni descartar solicitudes por ello; el
  trabajo que necesita Unity espera y se aplica desde `Update` cuando el usuario
  vuelve al juego.
- La cola de mensajes del acompañante preserva regalos finales frente a ráfagas
  de likes/estados, coalesce estados redundantes y reporta de forma agregada
  cualquier descarte por presión.
- `Reglas de stream` usa una tabla estilo cola. Crear o editar reemplaza el
  contenido del mismo panel por un formulario completo animado; al guardar
  regresa a la tabla y resalta la fila confirmada. La vista está separada en
  componentes de tabla, fila, formulario, selector y catálogo. Ya no está
  detrás de una pestaña: ocupa el primer panel del workspace de Interacciones.
  Las imágenes de esa tabla permanecen a opacidad normal.
- Cuando una regla crea una interacción, la ruta del PNG local del regalo viaja
  por el backlog y la cola hasta el actor. `CreatorToolsDonorLabel` lo muestra a
  la izquierda del nombre del donador con alpha base `0.8`, lo incluye en sus
  fades y snapshot de fin de nivel y respeta la misma prioridad de render.
  Manual, Random Test y Modo Molestoso no muestran icono.
- La antigua cola de `Canjeos pendientes` es ahora el componente compartido
  `InteractionQueuePanel`, se llama `Canjeos en curso` y vive en el Dashboard
  justo antes de `Tiempo real`. Mantiene la cola confirmada y la optimista.
- El simulador usa un dropdown buscable que abre los 43 regalos al enfocarse y
  filtra por nombre o alias sin exigir IDs. Para TikTok/regalo envía únicamente
  `giftId`, usuario, cantidad y `delaySeconds`: el mod deriva nombre, imagen y
  Coins desde el catálogo instalado. `Retraso (segundos)` acepta 0 como envío
  inmediato y sobrevive a cerrar o recargar el navegador (no a cerrar Cuphead).
  Las pruebas se limitan a 1000 unidades.
- `assets/creator-tools/gifts/catalog.json` contiene el snapshot base offline
  `2026-08-26.1`: 43 regalos de TikTok con `giftId` textual, nombre, Coins por
  unidad, tipo fuente, URL de origen, primera observación e imagen local. El
  registro `198895`, cuyo nombre estaba en otro idioma, fue excluido junto con
  su PNG. El build valida el catálogo antes de compilar la SPA.

El contrato del Dashboard es ahora `schemaVersion: 2` y añade `itemId`, imagen,
valor unitario/total, ID y estado de racha, además de conservar `amount` como
alias temporal de compatibilidad. El protocolo plano acompañante/mod es v1.
Las unidades incompatibles nunca se suman: Coins, Bits y dinero permanecen
separados.

## Decisiones aprobadas con el usuario

- Plataformas objetivo: TikTok mediante TikFinity, Twitch y YouTube.
- La configuración de canjeos pertenece a **Interacciones**. El estado de
  conexiones, eventos recibidos y operación pertenece a `/dashboard`.
- El motor interno debe ser neutral al proveedor. Cada adaptador traduce su
  plataforma al contrato normalizado antes de evaluar reglas.
- Debe soportar multistream y varias conexiones. Una regla puede limitarse a
  una conexión o plataforma concreta.
- TikTok necesita reglas por regalo específico, monedas, likes, follows y
  suscripciones.
- Twitch no debe presentar regalos de TikTok: su métrica de valor son Bits.
  Sus capacidades definitivas, igual que las de YouTube, deben verificarse con
  sus APIs oficiales antes de cerrar la matriz.
- Todas las reglas que coincidan se ejecutan independientemente. Cada regla
  mantiene su propio acumulador y conserva el sobrante al usar umbrales de
  "cada N".
- Nunca se mezclan Coins, Bits ni monedas reales en un acumulador global.
- Las reglas se guardan en el mod; no deben depender de que el navegador siga
  abierto.

## Estado y orden recomendado de implementación

### 1. Editor persistente de regalos exactos — implementado

Interacciones ya presenta el catálogo, las pruebas y `Reglas de stream` en un
solo recorrido visible, sin pestañas. El CRUD inicial crea, edita, duplica,
activa/desactiva y elimina reglas TikTok de
regalo exacto. Cada regla guarda `giftId`, copia del nombre conocido, umbral de
unidades, interacción destino y cantidad en
`mx.gilomx.cuphead.bossroulette.stream-rules.json`, junto al config principal;
la escritura mantiene `.bak` y el mod recupera ese respaldo si hace falta.

El backend valida `giftId` contra el snapshot instalado y la interacción contra
el catálogo interno. React sólo presenta y solicita cambios; la autoridad y la
persistencia viven en C#. Las rutas locales son
`GET /api/config/interactions/rules` y
`GET /api/config/interactions/rules/set`.

### 2. Cerrar el contrato de eventos antes de evaluar reglas — implementado

El contrato v2 define explícitamente:

- valor unitario frente a valor total;
- identificador de racha/transacción;
- evento provisional de racha frente a evento final;
- deduplicación de reintentos del proveedor.

En TikTok el ID es la identidad de la regla y el nombre es presentación. Las
rachas `progress` se muestran como ignoradas y sólo `final` o `none` llegan a
las reglas. La deduplicación se hace antes de contadores y acumuladores.

### 3. Ampliar el editor a las demás condiciones

El workspace visible ya contiene el catálogo y las pruebas, además del CRUD
persistente de regalo exacto en `Reglas de stream`.

Cada regla necesita nombre, estado, plataforma/conexión, tipo de evento,
condición, interacción de destino y cantidad de interacciones. Debe permitir
crear, editar, duplicar, activar/desactivar y eliminar.

La interfaz y C# deben compartir una matriz de capacidades validada por el
backend. Los controles incompatibles se ocultan; una regla existente que deje
de ser compatible se marca como incompatible y no se elimina.

El regalo concreto por `giftId`, con cada N unidades opcional, ya está cubierto.
El resto de las condiciones aprobadas debe incorporarse sobre el mismo contrato:

- Coins o Bits cada N;
- likes cada N;
- follow o suscripción cada N, con 1 como valor inicial;
- canje por ID/nombre únicamente en plataformas que lo ofrezcan.

### 4. Implementar evaluación, acumuladores y despacho — primera vertical implementada

- Se deduplica por conexión antes de modificar contadores.
- Todas las reglas exactas compatibles se evalúan independientemente.
- Cada regla/conexión conserva su sobrante de `cada N` durante la sesión actual.
- Las coincidencias entran a la cola existente desde el hilo principal de
  Unity; por ello heredan pausa, carga, derrota y protecciones de transición.
- Dashboard refleja `received`, `matched`, `queued` e `ignored`. Registrar el
  resultado posterior `executed`/`rejected` sigue pendiente para una fase de
  telemetría de la cola.

### 5. Conectar TikFinity — primera vertical implementada

- `LaPichiRuleta.TikFinity.exe` consume el endpoint oficial
  `ws://localhost:21213/`, sin autenticación ni suscripción adicional.
- Se reconecta con espera 1, 2, 4, 8, 16 y 30 segundos, publica estados NDJSON
  y termina al morir el PID de Cuphead o cerrarse su pipe de salida.
- Acepta esquemas planos y anidados y normaliza regalos, Coins, likes, follows
  y suscripciones. Chat, share y roomUser se descartan en el adaptador.
- El socket `connected` sólo significa **API local de TikFinity conectada**; no
  demuestra que el LIVE esté activo porque la API oficial no expone esa señal.
- Pruebas autocontenidas cubren normalización, rachas, deduplicación, contrato,
  reconexión y ciclo de vida. Aún conviene validar capturas reales y el Event
  Simulator de TikFinity antes de considerar estable toda deriva de payload.

### 6. Añadir Twitch y YouTube

Implementarlos como adaptadores separados después de estabilizar una regla de
punta a punta con TikFinity. Investigar documentación oficial para autenticación,
renovación de tokens, capacidades y límites. Los secretos no deben aparecer en
el estado JSON ni en logs.

## Catálogo de regalos de TikTok

Ya existe una primera copia base dentro del mod, importada desde el ZIP de
mantenimiento `catalogo-regalos-2026-08-26.zip`. El importador reproducible vive
en `tools/import_tiktok_gift_catalog.mjs` y el contrato del snapshot se explica
en `assets/creator-tools/gifts/README.md`. Los usuarios finales **no deben
farmear el catálogo**.

La arquitectura acordada es:

`lives de batalla -> recolector externo -> revisión -> catálogo publicado -> mod`

El recolector puede usar TikTok-Live-Connector como herramienta de mantenimiento
aunque el mod en ejecución use TikFinity. Debe producir un catálogo de datos,
no código ejecutable, con al menos:

- `giftId` almacenado como texto;
- nombre y alias conocidos;
- Coins por unidad;
- imagen o miniatura estable;
- primera y última observación;
- estado/confianza y, si está disponible, región.

Mantener dos versiones independientes:

- `schemaVersion`: estructura que entiende el mod;
- `catalogVersion`: cambios normales de contenido.

El mod debe incluir una copia base para funcionar sin internet. En una etapa
posterior puede consultar un manifiesto `latest.json`, validar esquema y hash,
instalar el catálogo de manera atómica y conservar el último catálogo válido.
Una actualización de datos no debería exigir publicar otro DLL. Un regalo que
desaparece se conserva como histórico; un precio contradictorio requiere
revisión y no se reemplaza automáticamente.

Las reglas guardan el `giftId` y una copia del nombre conocido, por lo que una
falla de descarga o un catálogo viejo no debe impedir que un evento real
coincida.

## Archivos principales

- `CreatorToolsDashboardController.cs`: contrato, historial, simulación y
  snapshot del Dashboard.
- `CreatorToolsStreamRulesController.cs`: validación, CRUD, snapshots HTTP y
  persistencia con respaldo de reglas exactas de regalo.
- `CreatorToolsServer.cs`: rutas y colas HTTP.
- `CreatorToolsOverlay.cs`: ciclo de vida y llamada desde Unity `Update`.
- `Streaming/CreatorToolsStreamEvent.cs`: límite neutral de eventos y resultados.
- `Streaming/TikFinityCompanionHost.cs`: proceso hijo, cola acotada y reinicio.
- `Streaming/TikFinityCompanionProtocol.cs`: parser NDJSON plano compatible con
  .NET 3.5.
- `TikFinityCompanion/`: cliente WebSocket moderno, normalizador, pruebas y
  publicación single-file.
- `creator-tools-ui/src/features/dashboard/DashboardView.tsx`: Dashboard.
- `creator-tools-ui/src/features/interactions/InteractionsView.tsx`: navegación
  interna entre catálogo/pruebas y reglas.
- `creator-tools-ui/src/features/interactions/StreamRulesView.tsx`: orquestador
  de la tabla y el formulario de reglas.
- `StreamRulesTable.tsx`, `StreamRuleRow.tsx`, `StreamRuleForm.tsx` y
  `TikTokGiftPicker.tsx`: componentes visuales del flujo de reglas.
- `creator-tools-ui/src/model.ts`: contratos TypeScript.
- `creator-tools-ui/scripts/mock-server.mjs`: entorno de desarrollo sin juego.
- `assets/creator-tools/gifts/catalog.json`: catálogo base offline de TikTok.
- `creator-tools-ui/scripts/validate-gift-catalog.mjs`: validación del catálogo
  y de sus PNG durante el build.
- `tools/import_tiktok_gift_catalog.mjs`: importador de exports revisados.
- `creator-tools-ui/PANEL_RULES.md`: invariantes visuales y de arquitectura.

## Invariantes del panel y del servidor

- El servidor usa solamente `127.0.0.1:18081`; no busca un puerto alternativo.
  Si está ocupado, se muestra la página estática de diagnóstico.
- El servidor permanece activo aunque el Stream Overlay esté desactivado. Ese
  interruptor controla únicamente lo que muestra el overlay.
- Al abrir Panel de control desde el mapa, primero se cierra el menú y después
  se abre `/config`.
- La protección de transiciones permanece activa y su control público sigue
  oculto.
- React no debe conservar reglas o conexiones que necesiten sobrevivir al
  cierre del navegador.
- El acompañante nunca se abre manualmente: el mod posee su ciclo de vida y su
  ruta instalada fija es `companion\LaPichiRuleta.TikFinity.exe`.
- La subcarpeta `TikFinityCompanion` debe permanecer en `DefaultItemExcludes`
  del proyecto net35; de otro modo sus DLL modernas contaminan las referencias
  de Unity durante el build.
- Todo texto visible debe existir en los locales `es` y `en`.
- Después de cambiar la UI se instalan juntos `config.html`, `config.css` y
  `config.js`; copiar sólo el DLL deja el panel anterior.

## Validación y forma de trabajo

Comandos mínimos:

```powershell
npm.cmd run build --prefix creator-tools-ui
dotnet build CupheadBossRoulette.csproj -c Release --no-restore
node --check creator-tools-ui/scripts/mock-server.mjs
powershell -ExecutionPolicy Bypass -File TikFinityCompanion/scripts/test.ps1
powershell -ExecutionPolicy Bypass -File TikFinityCompanion/scripts/publish-win-x64.ps1
powershell -ExecutionPolicy Bypass -File TikFinityCompanion/scripts/smoke-test.ps1
git diff --check
```

Preferencias permanentes del usuario para esta línea de trabajo:

- después de cambiar código, compilar e instalar;
- si Cuphead está abierto, cerrarlo antes de instalar;
- instalar el DLL, los tres assets compilados del panel y el EXE de
  `companion`;
- hacer commit solamente cuando el usuario lo pida explícitamente.

## Decisiones todavía abiertas

- Matriz exacta de Twitch y YouTube.
- Método de autenticación y almacenamiento local de secretos.
- Hospedaje, firma, frecuencia y política de actualización del catálogo.
- Señal fiable de sesión/LIVE para decidir cuándo reiniciar acumuladores sin
  depender solamente del reinicio de Cuphead.
- Telemetría de ejecución/rechazo posterior a la entrada en cola.
