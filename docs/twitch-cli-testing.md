# Probar interacciones con Twitch CLI sin hacer live

El paquete **La Pichi Ruleta · Dev** permite conectar el companion al servidor
local oficial de [Twitch CLI](https://dev.twitch.tv/docs/cli/websocket-event-command/).
Los eventos atraviesan el WebSocket, el normalizador y el pipe del companion
antes de entrar a las reglas y la cola del mod. Aparecen como simulaciones.

El script utiliza Twitch CLI 1.1.24 para Windows x64, descargada de la publicación
oficial y verificada con SHA-256. La conserva en `.deployment-cache/`; no añade
nada al PATH ni usa otro comando llamado `twitch` que ya esté instalado.
No requiere autorizar la CLI, un live, Bits reales ni suscripciones pagadas.

## Preparar

1. Carga el paquete actualizado desde el launcher en el siguiente arranque.
2. Abre PowerShell en la raíz del repositorio y ejecuta:

   ```powershell
   pwsh -NoProfile -File ./tools/test-twitch-eventsub.ps1
   ```

   Deja esta terminal abierta. El servidor escucha sólo en `127.0.0.1:8080` y
   exige suscripciones EventSub; no se cambia el firewall.
3. En el panel del mod ve a **Configuración → Twitch** y pulsa
   **Probar con Twitch CLI (Dev)**. Espera el estado **Prueba local activa**.
4. En Interacciones crea o activa las reglas de plataforma Twitch. Enciende
   **Interacciones del live** en el Dashboard. Durante un combate compatible
   podrás ver sus efectos; fuera de combate puedes revisar recepción y cola.

## Enviar eventos

Abre una segunda terminal en la raíz del repositorio:

```powershell
# Un follow del usuario ficticio 1001. Cambia ViewerId para probar otra persona.
pwsh -NoProfile -File ./tools/test-twitch-eventsub.ps1 -Event Follow -ViewerId 1001

# 100 Bits; prueba 60 y 40 por separado para comprobar un umbral de 100.
pwsh -NoProfile -File ./tools/test-twitch-eventsub.ps1 -Event Bits -Amount 100

# Suscripción nueva.
pwsh -NoProfile -File ./tools/test-twitch-eventsub.ps1 -Event Subscription

# Un paquete que regala cinco suscripciones.
pwsh -NoProfile -File ./tools/test-twitch-eventsub.ps1 -Event Gifted -Quantity 5

# Renovación compartida en el chat.
pwsh -NoProfile -File ./tools/test-twitch-eventsub.ps1 -Event Resubscription

# Un canje: el nombre debe coincidir con la recompensa configurada en la regla.
pwsh -NoProfile -File ./tools/test-twitch-eventsub.ps1 -Event Reward -RewardName "Mini jefe" -Amount 500
```

Todos los comandos usan el canal ficticio `42`. `Amount` es la cantidad de Bits
o el coste del canje; el coste del canje no multiplica la interacción.
`Quantity` para Gifted indica suscripciones del paquete, no mensajes separados.
Los follows se admiten una vez por persona durante la sesión del juego.
Twitch CLI 1.1.24 no genera votos del chat; esos se prueban desde el simulador
del Dashboard o el chat real de Twitch.

## Terminar y comprobar

Pulsa **Terminar prueba y volver a Twitch**. El companion restaura la cuenta
guardada sin borrarla ni revocar sus permisos. Si no había una cuenta guardada,
queda desconectado. Detén el servidor de la terminal con `Ctrl+C`.
El modo de prueba es temporal y está disponible sólo en Dev; no se persiste
como una conexión real. Si cierras el servidor primero, el companion reintenta
hasta que termines la prueba.

La prueba de integración reproducible (con el servidor abierto) es:

```powershell
dotnet run --project TikFinityCompanion/tests/LaPichiRuleta.TikFinity.Tests.csproj --configuration Release -- --twitch-cli-smoke
```

Usa un receptor aislado, sin tocar Cuphead ni el almacén de credenciales, y
comprueba los seis eventos oficiales hasta su salida NDJSON. Las pruebas del
motor verifican después su evaluación y el bloqueo del modo local en producción.
