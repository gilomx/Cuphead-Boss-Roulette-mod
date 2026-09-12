# La Pichi Ruleta · Dev: paquete para el launcher

Este es el procedimiento Dev para ambas PCs. Sustituye las instalaciones y
arranques de desarrollo sobre el Cuphead original. La integración de carga en
una carpeta compartida del launcher **todavía está pendiente**: el script deja
listo el paquete, pero no hace que el launcher actual pueda consumirlo.

Se conserva también una [alternativa compatible con Windows PowerShell 5](RULETA-DEV-DEPLOYMENT.md)
para separar preparación y publicación. Usa el mismo destino; no ejecutar los
dos publicadores simultáneamente. El procedimiento principal de ambas PCs sigue
siendo el de este documento.

## Generar en cualquiera de las dos PCs

Requisitos: Windows x64, PowerShell 7.2 o posterior (`pwsh`), Git, SDK de .NET
10, Node.js 20.19+ en la rama 20 o 22.12+ y npm (según Vite fijado en el lockfile). Debe existir
una instalación local de Cuphead 1.3.4 para leer sus referencias. No necesita
tener BepInEx instalado. La primera ejecución necesita acceso a GitHub, npm y
NuGet; las posteriores también ejecutan restauración de dependencias.

Desde el checkout actualizado del repositorio:

```powershell
git pull --ff-only
pwsh -NoProfile -File ./tools/deploy-launcher-dev.ps1
```

El script localiza su repositorio mediante `$PSScriptRoot`, con independencia
del directorio desde el que se invoque. Detecta Steam mediante el registro y
lee `libraryfolders.vdf` y `appmanifest_268910.acf`. Si no encuentra una única
instalación, falla sin publicar y pide una referencia local explícita.

Para una instalación no detectada, crea **sólo en esa PC**
`launcher-dev.local.json` en la raíz del checkout. Define `cupheadDir` con la
ruta local seleccionada. Puedes usar variables de entorno; las rutas relativas
se interpretan respecto al checkout. También puedes pasar `-CupheadDir` al
script, que tiene prioridad sobre el JSON. No añadas este archivo a Git ni
copies rutas específicas de una PC a las instrucciones compartidas.

La clave opcional `bootstrapZip` (o el parámetro `-BootstrapZip`) permite usar
una copia local del ZIP de referencia publicado. Tiene que coincidir con el
SHA-256 fijado en `tools/launcher-dev-bootstrap.json`; no acepta cualquier ZIP
de una instalación. Sin esa opción, lo descarga y lo conserva en
`.deployment-cache/`. Esa caché y la configuración local están excluidas de Git.

## Destino fijo

Siempre se publica en:

```text
%LOCALAPPDATA%\CupheadModLauncher\dev\pichi-ruleta\current.zip
```

El cálculo en PowerShell es:

```powershell
$packagePath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CupheadModLauncher\dev\pichi-ruleta\current.zip'
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($packagePath))
```

No hay un parámetro para cambiar el destino. **Cada PC genera su propio paquete
local**. Esta ubicación no sincroniza archivos entre PCs; Git comparte las
instrucciones, scripts y fuentes. No se versionan paquetes ni configuración de
una máquina.

## Contenido y validación

La referencia de empaquetado completo es la
[versión publicada v0.5.129](https://github.com/gilomx/Cuphead-Boss-Roulette-mod/releases/tag/v0.5.129).
Su URL, SHA-256 y lista explícita de archivos del cargador están versionados en
`tools/launcher-dev-bootstrap.json`. Se reutiliza únicamente su cargador
BepInEx x64 y todas sus dependencias `BepInEx/core`, incluidos Harmony y
MonoMod. La DLL y los recursos antiguos de ese ZIP no se reutilizan. Para
actualizar el cargador, revisa otra publicación compatible, actualiza URL,
checksum y lista conjuntamente, y repite las validaciones.

El ZIP Dev no tiene carpeta contenedora. Sus entradas usan `/` y son relativas
a la raíz de Cuphead:

```text
winhttp.dll
doorstop_config.ini
BepInEx/core/...
BepInEx/plugins/GilomxBossRoulette/Gilomx.CupheadBossRoulette.dll
BepInEx/plugins/GilomxBossRoulette/assets/...
BepInEx/plugins/GilomxBossRoulette/companion/LaPichiRuleta.TikFinity.exe
```

La publicación:

1. Verifica el hash del ZIP base, extrae sólo la lista del cargador y comprueba
   que el proxy sea x64 y que Doorstop apunte al preloader por una ruta relativa.
2. Ejecuta `npm ci`, las validaciones de catálogos y la compilación del panel en
   un directorio temporal. Compila la DLL en Release usando las referencias
   locales de Cuphead y Harmony del cargador limpio. Publica el companion
   autocontenido de Windows x64, como un único EXE.
3. Añade los assets versionados del checkout y los archivos recién compilados
   del panel. Los assets nuevos deben añadirse a Git antes de publicar. No
   recoge archivos locales sueltos, otros mods ni contenido de la instalación.
4. Construye un ZIP temporal **en el mismo directorio que `current.zip`**.
   Reabre todas sus entradas y compara sus SHA-256 con el contenido preparado;
   rechaza archivos ausentes, añadidos, duplicados y rutas inseguras.
5. Sustituye `current.zip` con `File.Replace`, o usa un rename (`File.Move`) si
   es la primera publicación. No hay fallback que borre el anterior o escriba
   por partes. Si el destino está bloqueado o falla cualquier paso anterior,
   la ejecución falla y conserva el paquete previo. Un bloqueo exclusivo
   `.publish.lock` evita publicaciones simultáneas en esa PC.

La salida indica ruta, cantidad de archivos y SHA-256 del ZIP publicado.
Los temporales de cada ejecución se limpian al terminar. Una interrupción
forzada puede dejar temporales huérfanos; no reemplazan el paquete vigente.
Se excluyen ejecutables y datos originales de Cuphead, partidas, configuraciones
personales, logs, cachés, backups y otros mods. No se empaqueta el README de
instalación manual de la versión base, que corresponde a otro procedimiento.

Para comprobar la publicación atómica y el rechazo de paquetes defectuosos sin
tocar el juego ni el destino real:

```powershell
pwsh -NoProfile -File ./tools/test-launcher-dev-deployment.ps1
dotnet run --project ./CreatorToolsRuntimeTests/CreatorToolsRuntimeTests.csproj
```

## Independencia e integración pendiente

El script no copia nada al original, no escribe en la futura carpeta compartida,
no modifica el catálogo y no inicia o detiene procesos del juego. Puedes
generar un paquete mientras juegas. Una compilación nueva se aplicará en el
siguiente arranque cuando el launcher implemente la selección
**«La Pichi Ruleta · Dev»** y su carga compartida con los demás mods. No existe
recarga de DLL durante una partida.

`Plugin.AssetsDirectory` parte de `Info.Location`. El host del companion recibe
esa misma carpeta del plugin y abre `companion/LaPichiRuleta.TikFinity.exe`, con
su propio directorio de trabajo y ventana oculta. Ni los recursos del mod ni
el ejecutable del companion dependen del checkout o de una biblioteca de Steam.
`CupheadDir` sólo se utiliza durante la compilación; `BepInExCoreDir` apunta al
cargador temporal preparado por el script.

## Datos que el launcher deberá conservar

En la ejecución normal de BepInEx, la ruleta guarda junto a
`Config.ConfigFilePath`, bajo `BepInEx/config` de la **raíz de ejecución**. El
prefijo de todos los archivos siguientes es `mx.gilomx.cuphead.bossroulette`:

| Sufijo | Datos persistentes |
| --- | --- |
| `.cfg` | Opciones de ruleta, selección/exclusión de retos, reto equipado por ranura de partida, overlay, interruptores y otros ajustes de BepInEx del plugin. |
| `.stream-rules.json` | Reglas de regalos, likes y follows. |
| `.pesky-mode.json` | Ajustes de Modo Molestoso, nombres y selección de artículos. |
| `.interaction-pacing.json` | Balance y ritmo independientes de Interacciones. |
| `.pesky-battle.json` | Configuración de Batalla Molestosa. |
| `.tap-farming.json` | Configuración de Tap Farming. |
| `.overlay-composer.json` | Composición y posiciones del overlay. |

Conserva también los `.bak` asociados a esos JSON: el código los usa para
recuperación y migraciones. Los `.tmp` de escritura, logs y cachés no son datos
de usuario a restaurar. `BepInEx/config/BepInEx.cfg` es configuración del
cargador compartido; el launcher deberá decidir su gestión por separado, sin
mezclarla con los datos exclusivos de esta ruleta.

El companion **no escribe ajustes, credenciales ni bases de datos propios en
disco**. Se comunica con TikFinity en `ws://localhost:21213/` y transmite eventos
al mod por stdout. Su estado de conexión, colas y contadores en memoria son
transitorios; no hay una carpeta de datos del companion que migrar. La
configuración de la aplicación externa TikFinity queda fuera de este paquete.
Los logs del host se integran en el log de BepInEx.

Las partidas de Cuphead siguen siendo datos del juego, externos a este ZIP;
no se redistribuyen ni se mueven con este script. El reto manual por ranura se
guarda en el `.cfg` del plugin, no en un archivo nuevo de partida.

Antes de limpiar o migrar el original se coordinará un respaldo de sus archivos
del mod y de estos datos. **Todavía no se borra nada del original**. Para la
futura carpeta compartida, el launcher deberá respaldar/restaurar los datos de
esta ruleta al cambiar versiones o mods, evitando que la sustitución de binarios
los elimine. La migración y esa política de conservación no están implementadas
por el script de publicación.
