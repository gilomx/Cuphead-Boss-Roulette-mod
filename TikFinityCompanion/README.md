# Acompañante local de TikFinity

`LaPichiRuleta.TikFinity.exe` mantiene la conexión WebSocket local con
TikFinity fuera del runtime antiguo de Unity. No tiene ventana ni consola y el
usuario no debe abrirlo directamente: el mod lo inicia, lee su salida estándar
y lo deja morir automáticamente cuando Cuphead termina.

## Ubicación e inicio

El artefacto publicado se instala junto al plugin en:

```text
<pluginDir>\companion\LaPichiRuleta.TikFinity.exe
```

El mod debe iniciarlo con salida estándar redirigida y pasar su PID:

```text
LaPichiRuleta.TikFinity.exe --parent-pid <pid-de-cuphead>
```

`--parent-pid=<pid>` también es válido. El proceso rechaza argumentos
desconocidos, valida que el padre exista y termina cuando ese proceso sale. Un
pipe de salida roto también hace que termine en el siguiente intento de
escritura; el PID padre es la garantía activa incluso si el socket está ocioso.

## Transporte

- Endpoint fijo oficial: `ws://localhost:21213/`.
- Reconexión automática: 1, 2, 4, 8, 16 y 30 segundos; después permanece en
  30 segundos.
- Límite por mensaje: 1 MiB.
- Sólo acepta mensajes WebSocket de texto.
- Nunca imprime logs ni payloads crudos en `stdout`: cada línea es un mensaje
  completo del protocolo NDJSON.

El estado `connected` significa únicamente que abrió el WebSocket de la API
local. No demuestra que TikFinity esté conectado a un LIVE de TikTok; la API
documentada no publica una señal separada para eso.

## Contrato NDJSON v1

Cada línea es un objeto JSON plano en UTF-8 terminado por `\n`. Todos los
campos se escriben siempre; los valores desconocidos son `null`.

Estado:

```json
{"protocolVersion":1,"kind":"status","state":"connected","connectionId":"tikfinity-local","message":"Connected to the local TikFinity WebSocket.","occurredAt":"2026-08-26T18:30:00+00:00","retryAttempt":0}
```

`state` sólo puede ser `starting`, `connecting`, `connected`, `disconnected` o
`error`. `retryAttempt` vale cero en el intento inicial y aumenta con cada
reconexión fallida.

Evento:

```json
{"protocolVersion":1,"kind":"event","eventId":"9001","idempotencyKey":"tfn1:…","connectionId":"tikfinity-local","platform":"tiktok","connector":"tikfinity","type":"gift","userName":"viewer_one","userId":"1234567890123456789","itemId":"5655","itemName":"Rose","itemImageUrl":"https://example.invalid/rose.png","count":5,"unitValue":1,"totalValue":5,"unit":"coin","currency":null,"streakId":"combo-44","streakState":"final","receivedAt":"2026-08-26T18:30:00+00:00","simulated":false,"rawEventType":"gift"}
```

Semántica principal:

- `itemId` conserva el ID del regalo como texto; el nombre sólo es
  presentación.
- `count` es `repeatCount` para regalos y `likeCount` para likes.
- `unitValue` es el valor inferido en Coins de una unidad y `totalValue` el
  acumulado. `unit` es `coin`; `currency` queda `null` porque Coins no es una
  divisa ISO.
- `eventId` conserva `msgId`/`eventId` si existe. Si no existe, se genera un ID
  estable a partir del tipo y del JSON de `data`.
- `idempotencyKey` es un SHA-256 determinista con prefijo `tfn1:`. En regalos
  incluye evento, racha, conteo y estado para deduplicar reenvíos idénticos sin
  confundir una actualización provisional con el cierre.
- `receivedAt` es la hora UTC en que el acompañante recibió el mensaje, no una
  hora prometida por TikFinity.
- `type` traduce `subscribe` a `subscription`. Los mensajes que el contrato
  actual puede accionar son `gift`, `like`, `follow` y `subscription`.
  `chat`, `share`, `roomUser` y tipos desconocidos se descartan silenciosamente
  en el límite del adaptador para no inundar al mod.

### Rachas de regalos

`streakState` usa exactamente:

- `progress`: regalo de racha (`giftType == 1`) con `repeatEnd == false`;
- `final`: regalo de racha con `repeatEnd == true`;
- `none`: regalo no acumulable o sin señal de racha;
- `null`: evento que no es regalo.

`repeatCount` es acumulativo. El motor debe mostrar `progress` si le resulta
útil, pero sólo evaluar canjeos cuando el estado sea `final` o `none`. Así no
dispara una interacción por cada avance y otra vez por el cierre. Incluso una
racha acumulable de una sola unidad puede producir un evento provisional y uno
final.

Si `giftType == 1` aparece sin `repeatEnd`, se elige conservadoramente
`progress`: perder temporalmente un canje es preferible a ejecutarlo dos veces.

## Fuentes y límites de lo documentado

Hechos tomados de documentación oficial de TikFinity:

- La [Event API de TikFinity](https://tikfinity.zerody.one/tiktok/dapi)
  requiere la app de escritorio en el mismo equipo, publica
  `ws://localhost:21213/`, define la envoltura `{ "event", "data" }` y enumera
  `chat`, `gift`, `share`, `follow`, `like`, `roomUser` y `subscribe`.
- El [ejemplo JavaScript descargable por TikFinity](https://tikfinity.zerody.one/downloads/ws_api_example.zip)
  abre un WebSocket normal, no envía autenticación ni suscripción y reintenta
  tras un cierre.
- La [integración oficial con Streamer.bot](https://tikfinity.zerody.one/streamerbot-integration)
  confirma los conceptos `userId`, `username`, `nickname`, `giftId`,
  `giftName`, `coins` y `repeatCount`.

La página de Event API remite expresamente a
[TikTok-Live-Connector](https://github.com/zerodytrash/TikTok-Live-Connector#events)
para la estructura de `data`. Esa referencia confirma la regla
`giftType !== 1 || repeatEnd === true` para procesar un regalo definitivamente,
pero TikFinity no fija una versión del esquema del conector.

Por esa falta de versión, las siguientes decisiones son inferencias defensivas
y deben validarse con capturas reales antes de considerarlas contrato de
TikFinity:

- Se aceptan tanto el esquema histórico plano como variantes anidadas bajo
  `user`, `gift`, `giftDetails` y `extendedGiftInfo`.
- `msgId` se usa como ID de evento y `groupId` como ID de racha cuando existen;
  TikFinity no promete estabilidad ni entrega única para ninguno.
- `diamondCount` se interpreta como Coins por unidad y se multiplica por
  `repeatCount`. Si sólo aparece `coins`/`totalCoins`, se interpreta como total
  y se calcula el valor unitario.
- Las rutas de imagen de regalo son alias tolerantes, no campos garantizados
  por la página de Event API.

No hay inferencias silenciosas sobre estado del LIVE: abrir el socket sólo
marca la API local como conectada.

## Desarrollo, pruebas y publicación

El proyecto no usa paquetes externos. Para desarrollo requiere el SDK de
.NET 10; el usuario final no requiere Node, .NET ni ningún runtime adicional.

Ejecutar las pruebas unitarias autocontenidas:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Publicar el ejecutable Windows x64, único, autocontenido y recortado:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1
```

Salida:

```text
artifacts\win-x64\companion\LaPichiRuleta.TikFinity.exe
```

Comprobar el EXE publicado, sus dos primeros estados NDJSON y que termina al
morir el proceso padre:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1
```

Las pruebas usan fixtures representativos de ambos esquemas. Son muestras de
compatibilidad, no capturas declaradas como oficiales de TikFinity.
