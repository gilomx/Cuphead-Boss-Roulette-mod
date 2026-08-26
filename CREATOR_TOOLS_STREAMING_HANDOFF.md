# Creator Tools: continuación del motor de streaming

Fecha del traspaso: **2026-08-25**

Rama: `codex/creator-tools-config-panel`

Este documento cubre únicamente la integración multistream que comienza en
`/dashboard`. El historial completo del mod y las protecciones de fases siguen
en `PROJECT_HANDOFF.md`.

## Estado que ya existe

- `/dashboard`, `/dashboard/` y `/dashboard.html` sirven la misma SPA React que
  `/config`, sin desmontar el shell, el estado compartido ni la localización.
- `GET /api/dashboard` publica estado del motor, conexiones, contadores y los
  eventos recientes.
- `GET /api/dashboard/simulate` encola eventos de prueba para TikTok, Twitch y
  YouTube. El hilo HTTP no modifica Unity: `Update` procesa como máximo 64
  comandos por frame.
- La cola HTTP está limitada a 1024 comandos y el historial circular conserva
  los últimos 500 eventos.
- El Dashboard tiene tarjetas de conexión, feed, contadores y simulador en
  español e inglés. Conserva varias conexiones reales de una misma plataforma
  cuando éstas se incorporen.
- El backend simulado todavía crea una sola conexión por plataforma y
  `/api/dashboard/simulate` no recibe `connectionId`; el enrutamiento
  multiconexión se implementa junto con los adaptadores reales.
- Todo evento continúa siendo simulado. Ya existe configuración persistente de
  reglas exactas de regalo, pero no hay conexión real, OAuth, evaluación,
  acumuladores ni despacho hacia el juego; por eso `matched` y `queued`
  permanecen en cero.
- `assets/creator-tools/gifts/catalog.json` contiene el snapshot base offline
  `2026-08-26.1`: 43 regalos de TikTok con `giftId` textual, nombre, Coins por
  unidad, tipo fuente, URL de origen, primera observación e imagen local. El
  registro `198895`, cuyo nombre estaba en otro idioma, fue excluido junto con
  su PNG. El build valida el catálogo antes de compilar la SPA.

El contrato normalizado v1 ya contiene `eventId`, `idempotencyKey`, secuencia,
sesión, conexión, plataforma, conector, tipo, usuario opcional, `userId`
opcional, cantidad, unidad, moneda, conteo, nombre de artículo, estado, fecha de
recepción y marca de simulación. Las unidades incompatibles nunca se suman:
Coins, Bits y dinero permanecen separados.

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

Interacciones ya ofrece `Catálogo y pruebas` y `Reglas de stream`. El CRUD
inicial crea, edita, duplica, activa/desactiva y elimina reglas TikTok de
regalo exacto. Cada regla guarda `giftId`, copia del nombre conocido, umbral de
unidades, interacción destino y cantidad en
`mx.gilomx.cuphead.bossroulette.stream-rules.json`, junto al config principal;
la escritura mantiene `.bak` y el mod recupera ese respaldo si hace falta.

El backend valida `giftId` contra el snapshot instalado y la interacción contra
el catálogo interno. React sólo presenta y solicita cambios; la autoridad y la
persistencia viven en C#. Las rutas locales son
`GET /api/config/interactions/rules` y
`GET /api/config/interactions/rules/set`.

### 2. Cerrar el contrato de eventos antes de evaluar reglas

El v1 sólo tiene `itemName`. Un regalo exacto necesita, como mínimo, campos
opcionales para `itemId` e imagen. Antes de aceptar eventos reales también hay
que definir explícitamente:

- valor unitario frente a valor total;
- identificador de racha/transacción;
- evento provisional de racha frente a evento final;
- deduplicación de reintentos del proveedor.

En TikTok el ID es la identidad de la regla y el nombre es presentación. El
tamaño del ID no indica el precio. Las rachas no deben disparar una regla por
cada actualización y otra vez al finalizar. Si este cambio rompe el contrato,
incrementar `schemaVersion` en lugar de alterar silenciosamente el v1.

### 3. Ampliar el editor a las demás condiciones

Las dos vistas internas ya existen:

- `Catálogo y pruebas`: conserva la interfaz actual.
- `Reglas de stream`: CRUD persistente de regalo exacto.

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

### 4. Implementar evaluación, acumuladores y despacho

- Deduplicar por conexión/sesión antes de modificar contadores.
- Evaluar todas las reglas compatibles; no detenerse en la primera.
- Mantener acumulador y sobrante por regla y por conexión.
- Convertir coincidencias en solicitudes de la cola de interacciones existente,
  nunca invocar actores directamente desde un hilo de red.
- Respetar pausa, carga, derrota y la protección de transiciones existente.
- Reflejar el ciclo completo en Dashboard: recibido, coincidente, encolado,
  ejecutado, ignorado o rechazado.
- Definir antes de publicar si los acumuladores sobreviven al reinicio del
  juego o sólo a la sesión del LIVE; esta decisión sigue abierta.

### 5. Conectar TikFinity

- Consumir el WebSocket local de TikFinity en `ws://127.0.0.1:21213/` mediante
  un componente acompañante apropiado para WebSocket/JSON si el runtime de
  Unity no resulta fiable.
- Añadir reconexión con espera progresiva, estado real de conexión y una vista
  diagnóstica del payload recibido.
- Normalizar regalos, Coins, likes, follows y suscripciones al mismo límite que
  usa el simulador.
- No reutilizar el WebSocket del Stream Overlay.
- Tratar el Event Simulator de TikFinity como prueba; verificar primero si sus
  regalos simulados también aparecen en el puerto 21213.

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
- `creator-tools-ui/src/features/dashboard/DashboardView.tsx`: Dashboard.
- `creator-tools-ui/src/features/interactions/InteractionsView.tsx`: navegación
  interna entre catálogo/pruebas y reglas.
- `creator-tools-ui/src/features/interactions/StreamRulesView.tsx`: lista y
  editor visual conectado al catálogo de regalos.
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
- Todo texto visible debe existir en los locales `es` y `en`.
- Después de cambiar la UI se instalan juntos `config.html`, `config.css` y
  `config.js`; copiar sólo el DLL deja el panel anterior.

## Validación y forma de trabajo

Comandos mínimos:

```powershell
npm.cmd run build --prefix creator-tools-ui
dotnet build CupheadBossRoulette.csproj -c Release --no-restore
node --check creator-tools-ui/scripts/mock-server.mjs
git diff --check
```

Preferencias permanentes del usuario para esta línea de trabajo:

- después de cambiar código, compilar e instalar;
- si Cuphead está abierto, cerrarlo antes de instalar;
- instalar el DLL y los tres assets compilados del panel;
- hacer commit solamente cuando el usuario lo pida explícitamente.

## Decisiones todavía abiertas

- Persistencia o reinicio de los acumuladores entre sesiones/LIVEs.
- Matriz exacta de Twitch y YouTube.
- Método de autenticación y almacenamiento local de secretos.
- Hospedaje, firma, frecuencia y política de actualización del catálogo.
- Si el acompañante multistream vive como proceso propio o dentro de otro
  componente instalable.
