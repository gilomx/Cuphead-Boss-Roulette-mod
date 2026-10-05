# El chat elige

Evento Live del Dashboard en `/config/chat-chooses`. El mod conserva las rondas
y votos; cerrar la página no cancela el evento. TikFinity y el companion deben
estar conectados para recibir votos reales. El simulador admite eventos Chat
para probarlo sin directo.

## Flujo

Antes de iniciar, elegir **Con reto** o **Sin reto**. La elección se guarda en
la configuración del mod y queda bloqueada hasta detener o terminar el evento.
Se inicia desde el mapa, sin otra ruleta cargando ni otro Evento Live activo.

1. Jefe: hasta seis candidatos aleatorios, distintos y disponibles según DLC.
2. Disparo 1: hasta seis disparos reales; no participa Nada.
3. Disparo 2: hasta seis candidatos, sin repetir el primero. Nada entra en el
   sorteo como cualquier candidato, no ocupa un espacio garantizado.
4. Super: los tres supers y Nada, en orden aleatorio.
5. Amuleto: hasta seis candidatos aleatorios, incluyendo Nada en el sorteo.
   En tierra se excluyen las reliquias Maldita y Divina porque cambian los
   disparos y anularían las decisiones previas del chat.
6. Reto: sólo en Con reto, hasta seis candidatos habilitados y compatibles con
   el jefe; Nada participa en el sorteo.

Los jefes de avión pasan directamente del jefe al amuleto: conservan su
equipamiento nativo y sólo admiten retos compatibles con avión.

El chat escribe únicamente el número mostrado. Cada usuario tiene un voto por
ronda; otro número sustituye el anterior. El botón del jugador indica la
elección actual y lo que sigue. Cierra inmediatamente los votos y revela al
ganador durante 1.5 segundos. Si hay empate, se sortea entre los líderes; si
nadie votó, se sortea entre todos los candidatos mostrados. Los votos y mensajes
anteriores al comienzo de la siguiente ronda no se arrastran a ella.

La tarjeta **EL CHAT ELIGIÓ** aparece cinco segundos y después comienza una
cuenta de tres segundos. La carga usa el resultado, equipamiento, dificultad,
HUD, reintentos y ruta de regreso de la ruleta existente. Si el mapa dejó de
estar disponible, espera a regresar al mapa y reinicia la cuenta. Al regresar
de la batalla se libera el evento. Detener también libera la reserva de forma
inmediata, pero no cierra el juego ni interrumpe una batalla ya cargada.

## OBS y pruebas

Copiar la URL desde la página del evento. La fuente `/chat-chooses-overlay`
es transparente, se adapta a su resolución y muestra votos, resultado y cuenta.
`?lang=en` cambia los textos del overlay al inglés. Fuera de las rondas y de la
presentación final se oculta; la vista previa del Dashboard usa `preview=1` y
conserva el resultado durante la batalla para revisar las selecciones.

- `dotnet run --project CreatorToolsRuntimeTests/ChatChooses/ChatChooses.csproj`
- `dotnet run --project CreatorToolsRuntimeTests/CreatorToolsRuntimeTests.csproj`
- `dotnet run --project TikFinityCompanion/tests/LaPichiRuleta.TikFinity.Tests.csproj`
- `npm run build` desde `creator-tools-ui`.
- `npm run test:chat-chooses-overlay` desde `creator-tools-ui`.

El publicador canónico es `pwsh -NoProfile -File ./tools/deploy-launcher-dev.ps1`.
Los nuevos assets deben estar en el índice de Git antes de empaquetar. No se
instala en Cuphead original ni se cierra o arranca una sesión para verificarlo.
