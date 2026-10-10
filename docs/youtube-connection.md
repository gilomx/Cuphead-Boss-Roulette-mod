# Conexión de YouTube

El companion moderno gestiona OAuth y las credenciales. El mod y el navegador
reciben sólo estado público; ninguna credencial de usuario se incluye en los ajustes
exportables ni en los paquetes. Cada usuario de Windows autoriza un canal por
instalación y la cuenta se recuerda en el Administrador de credenciales.

## Registro compartido de la aplicación

1. Crear un proyecto «La Pichi Ruleta» en Google Cloud.
2. Habilitar **YouTube Data API v3** desde APIs y servicios → Biblioteca.
3. Configurar Google Auth Platform: nombre de la aplicación, correo de soporte
   y audiencia externa. En modo de prueba, agregar las cuentas que probarán el mod.
4. Crear un cliente OAuth de tipo **Aplicación de escritorio**. La redirección
   usa `http://127.0.0.1:<puerto aleatorio>/oauth2/callback`; el companion elige y
   ocupa el puerto antes de abrir Google en el navegador del sistema.
5. Incorporar el Client ID público en `YouTubeApplication.ClientId`. Descargar
   el JSON del cliente de escritorio y guardarlo como `youtube-oauth.local.json`
   en la raíz del checkout; Git excluye ese archivo y `client_secret*.json`.
   El companion incorpora ese recurso al compilar y comprueba que el Client ID
   coincida antes de usar `installed.client_secret` en el intercambio y la
   renovación. Sin el recurso, la conexión informa que falta configurar la app.
6. Compilar el paquete Dev con el procedimiento habitual. El usuario final
   pulsa Conectar YouTube; no necesita registrar su propia aplicación ni
   proporcionar ese JSON. Cada PC que compile debe tener su copia local.

Se solicita únicamente `youtube.readonly` y se usa PKCE S256, estado aleatorio
y acceso offline para renovar la autorización. El parámetro `client_secret`
de escritorio se distribuye dentro del companion: no puede considerarse
confidencial en una aplicación instalada y no sustituye PKCE ni el permiso
del usuario. Nunca se muestra en el panel, las URLs o los logs. Los tokens
personales sólo se guardan en Windows, después de autorizar. El proyecto debe
completar los requisitos de Google
para distribuir la aplicación fuera del grupo de prueba. Los permisos de una
aplicación externa en modo de prueba pueden caducar y requerir reconexión.

## Recepción y desconexión

La API identifica el canal autorizado y descubre sus broadcasts activos con
chat. Sin live, la cuenta permanece conectada y la búsqueda se repite cada
60 segundos. Los mensajes llegan por `liveChatMessages.streamList` (gRPC),
con cursor para reconectar y renovación del bearer antes de su vencimiento.
Se respeta la cuota: al agotarla se informa el estado y se espacia el reintento.

La normalización prepara:

| Evento | Datos conservados |
| --- | --- |
| Texto exacto 1–6 | Identidad del canal del espectador y hora local de recepción |
| Super Chat / Super Sticker | Importe decimal y moneda ISO, sin convertir monedas |
| Membresía nueva / mejora | Tipo y nivel |
| Mensaje de aniversario | Tipo milestone; no implica renovación automática |
| Membresías regaladas | Cantidad de la compra, sin duplicar destinatarios |
| Regalo con Jewels | Nombre, imagen HTTPS, valor en Jewels y delta del combo |

La primera respuesta inicializa el historial sin reproducir votos o pagos.
Los identificadores se deduplican; YouTube puede reutilizar el ID de un regalo
para incrementar el combo, por lo que sólo se contabiliza el aumento.
El registro de eventos del dashboard recibe estos datos. Las interacciones
incluyen el activador **YouTube · Gemas**, sin catálogo ni selección de regalo:
cualquier regalo suma su valor en gemas entre todos los espectadores. Por cada
umbral configurado se envía la cantidad elegida de la interacción; el sobrante
se conserva durante la sesión para la siguiente activación. No se convierten
Super Chats ni otras monedas a gemas. Cada regla conserva sus propias esperas.
El panel usa el icono de gemas de YouTube junto al importe. El simulador permite
indicar el total de gemas del evento, incluyendo su combo, sin elegir un regalo.
Las reglas se guardan en la versión 7 y las copias de la versión 6 siguen
admitidas. Los otros activadores de YouTube y los activadores múltiples quedan
para una siguiente etapa.

Desconectar detiene la recepción, borra la credencial local y revoca el refresh
token en Google. Si la red falla después de borrar, se informa que la revocación
no está confirmada y puede retirarse en la cuenta de Google. Un fallo al borrar
la credencial mantiene la recepción detenida y permite reintentar el borrado.
Cancelar una autorización pendiente nunca guarda una respuesta tardía.

## Verificación

Las pruebas del companion usan HTTP y canales ficticios para comprobar PKCE,
estado, propiedad del canal, renovación, cancelación, borrado, errores de cuota,
normalización y deduplicación. Las pruebas del mod comprueban que los comandos
requieren POST, origen local y una prueba independiente de Twitch, y que una
desconexión impide aceptar mensajes anteriores a su revisión.

La conexión de cuenta se prueba sin transmitir. Para comprobar la API del chat
se necesita una transmisión activa con chat; puede usarse una transmisión de
prueba no listada. Los eventos monetarios y membresías requieren que YouTube
los permita para ese canal y transmisión; las pruebas unitarias no efectúan pagos.

Referencias oficiales:

- [OAuth para escritorio](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Eventos de chat](https://developers.google.com/youtube/v3/live/docs/liveChatMessages)
- [Streaming del chat y definición protobuf](https://developers.google.com/youtube/v3/live/streaming-live-chat)
- [Broadcasts activos](https://developers.google.com/youtube/v3/live/docs/liveBroadcasts/list)
