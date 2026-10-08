# Copias de configuración del panel

En **Configuración** hay dos paneles: **Conexiones** y **Exportar e importar
configuraciones**. Una cuenta Twitch autorizada ofrece **Desconectar cuenta**;
para usar otra cuenta primero se desconecta la anterior y después se conecta
la nueva. Los controles de prueba de Twitch CLI siguen disponibles sólo en Dev.

**Exportar configuraciones** descarga un único archivo JSON con los ajustes
guardados de Ruleta, controles, retos equipados por ranura, exclusiones de retos,
Interacciones y sus reglas, Modo Molestoso, Batalla Molestosa, Farmeando taps,
los dos perfiles de overlays y el idioma del panel. También conserva la
identidad del regalo personalizado necesaria para recuperar sus reglas.
Los archivos que aún no se han guardado se exportan con sus valores
predeterminados, para que una importación reemplace también los ajustes que
habían quedado personalizados en el destino.
Si Modo Molestoso aún no tiene archivo propio, conserva el límite heredado
de Interacciones que utiliza esa instalación.

Las conexiones, cuentas autorizadas, tokens, secretos y datos del launcher
quedan fuera de la copia. Las reglas se transfieren por plataforma, sin una
identidad de cuenta o conexión. Twitch conserva sus credenciales en el
Administrador de credenciales de Windows; este mecanismo no las lee ni modifica.
Cada instalación autoriza sus propias cuentas. Importar una copia conserva
las conexiones que ya existen en esa PC. Las partidas, los eventos activos,
las colas y las simulaciones temporales no son configuración y no se trasladan.

Para importar, selecciona una copia y pulsa **Importar y reemplazar ajustes**.
El servidor vuelve a validar formato, versión, tipos, límites y contenido.
Sólo admite una lista fija de archivos del plugin y sus campos conocidos;
rechaza archivos adicionales, rutas, claves duplicadas y campos de conexión.
Una copia inválida no modifica los ajustes ni reemplaza una importación pendiente.

La importación queda preparada en
`mx.gilomx.cuphead.bossroulette.pending-import.json`, junto al `.cfg` del plugin.
Puede cancelarse desde el panel. Los ajustes del juego se aplican en el próximo
arranque, antes de registrar las opciones o construir los controladores. El
idioma del panel cambia al confirmar la importación. Ninguna sesión activa ni
conexión se reinicia al preparar la copia.

Antes de aplicar se respaldan los archivos actuales y sus `.bak` en una carpeta
única bajo `BepInEx/config/pichi-settings-backups/` de la raíz de ejecución.
Cada sustitución de archivo es atómica; si un paso falla se restauran los
archivos ya modificados y se conserva la copia pendiente para reintentar.
Los respaldos anteriores se conservan. La copia pendiente y los respaldos son
datos locales de usuario y nunca pertenecen al ZIP del mod.

Contrato HTTP local:

- `GET /api/settings` informa disponibilidad e importación pendiente, y entrega
  una prueba de control temporal que no forma parte de la copia portable.
- `POST /api/settings/export` descarga la copia.
- `POST /api/settings/import` prepara una copia de hasta 1 MiB.
- `POST /api/settings/cancel-import` cancela la copia pendiente.

Los POST requieren origen local y `X-Pichi-Settings-Control`. Ningún endpoint
acepta una ruta de disco elegida por el navegador. Las pruebas del harness usan
directorios temporales para comprobar transferencias completas, exclusión de
conexiones, validación, cancelación, recuperación y el contrato HTTP.
