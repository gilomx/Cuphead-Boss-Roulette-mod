# La Pichi Ruleta Dev: alternativa para Windows PowerShell 5

El procedimiento principal es [deploy-launcher-dev.ps1](launcher-dev-deployment.md),
con PowerShell 7, cargador fijado por hash y configuración local por PC. Esta
alternativa compatible con Windows PowerShell 5 permite separar preparación y
publicación. Sigue el mismo destino y el contrato `docs/RULETA-DEV-DEPLOYMENT.md`
del repositorio `cuphead-mod-launcher`. No ejecutar ambos publicadores a la vez.

## Compilar y publicar

Desde este repositorio, con .NET SDK, Node.js/npm y las referencias locales de
Cuphead disponibles:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-RuletaDev.ps1
```

El script reconstruye el panel, la DLL y el companion autocontenido para Windows
x64. Obtiene **solo el cargador** del ZIP publicado
`dist/La-Pichi-Ruleta-0.6.0.zip`; los archivos del mod se generan desde este checkout.
Ese ZIP es una dependencia local, no se guarda en Git. En otro equipo, descarga
el paquete publicado o indica su ubicación con `-LoaderPackage`.

Las referencias del juego son de solo lectura. El script busca Cuphead en las
bibliotecas locales de Steam; también acepta `-CupheadDir` o la variable local
`CUPHEAD_DIR`. Obtiene Harmony del cargador del paquete: el original de Steam
puede estar libre de mods. Ninguna ruta de compilación determina el destino.

La ruta de entrega siempre se calcula en ejecución:

```powershell
Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CupheadModLauncher\dev\pichi-ruleta\current.zip'
```

No cambiar ese sufijo ni guardar rutas absolutas de una PC en Git. Cada equipo
publica su propio paquete. El contrato del launcher prevé detectar la tarjeta
**La Pichi Ruleta · Dev** al existir `current.zip`; publicar el ZIP no implementa
ni comprueba esa integración en el launcher instalado.

Para separar la compilación de la autorización de escritura en datos locales:

```powershell
.\tools\Publish-RuletaDev.ps1 -PrepareOnly
# Usar la ruta Package devuelta por el comando anterior:
.\tools\Publish-RuletaDev.ps1 -PreparedPackage $paquetePreparado
```

La preparación deja el ZIP, un manifiesto de hashes y, después de publicar, un
comprobante bajo `installation-backups/launcher-dev-*`, excluido de Git. La
publicación vuelve a validar esos hashes y copia a un ZIP temporal único junto
a `current.zip`. Reemplaza el destino atómicamente después de verificarlo; ante
un bloqueo hace cinco intentos separados por 250 ms. Si fallan, conserva tanto
el paquete anterior como el temporal e informa el error.

## Contenido y arranque

La raíz del ZIP es la raíz del juego: `winhttp.dll`, `doorstop_config.ini`,
`BepInEx/core/` y `BepInEx/plugins/GilomxBossRoulette/`. Este último incluye la
DLL, todos los recursos distribuibles de `assets/` y
`companion/LaPichiRuleta.TikFinity.exe`. Se comprueban la arquitectura x64, las
dependencias obligatorias, las rutas relativas y los hashes de cada archivo.

No incluye archivos originales de Cuphead, otros mods, configuraciones
personales, partidas, logs, cachés o respaldos. Los PSD y notas de autoría no
son recursos de ejecución. El catálogo de regalos dentro de `assets/` sí es
un recurso del mod; el `catalog.json` local del launcher nunca se copia o edita.

Publicar no cierra ni inicia Cuphead. La nueva versión queda disponible para el
siguiente inicio compatible desde **La Pichi Ruleta · Dev**. Las pruebas de juego se abren desde esa
entrada; el mod inicia su propio companion, nunca el script de despliegue.

El launcher administra `runtime/game`, `profiles`, el catálogo y la migración.
El agente del mod solo publica el ZIP; no escribe directamente en esas carpetas,
no instala en el original de Steam y no elimina instalaciones anteriores. Los
scripts históricos dentro de `installation-backups/` no deben reutilizarse.
La limpieza del original corresponde a **Jugar sin mods** en el launcher.

## Ajustes y datos generados

Todas estas rutas son relativas a la carpeta de juego en ejecución. El launcher
debe conservar los datos existentes por variante del mod, sin sustituirlos con
valores predeterminados del paquete:

| Ruta | Contenido |
| --- | --- |
| `BepInEx/config/mx.gilomx.cuphead.bossroulette.cfg` | Configuración general, ruleta y Creator Tools, gestionada por BepInEx. |
| `BepInEx/config/mx.gilomx.cuphead.bossroulette.pesky-mode.json` | Modo Molestoso: ritmo, cantidades, nombres y selección. |
| `BepInEx/config/mx.gilomx.cuphead.bossroulette.interaction-pacing.json` | Ritmo y grupos de Interacciones. |
| `BepInEx/config/mx.gilomx.cuphead.bossroulette.stream-rules.json` | Reglas de eventos, regalos y likes. |
| `BepInEx/config/mx.gilomx.cuphead.bossroulette.overlay-composer.json` | Diseño del overlay. |
| `BepInEx/config/mx.gilomx.cuphead.bossroulette.tap-farming.json` | Ajustes de Tap Farming. |
| `BepInEx/config/mx.gilomx.cuphead.bossroulette.pesky-battle.json` | Ajustes de Batalla Molestosa. |
| Los mismos JSON con sufijo `.bak` | Respaldo de recuperación: conservar junto al archivo principal. |

Los guardados usan archivos transitorios `.tmp` y, al restaurar el diseño,
`.restore.tmp` junto a los JSON. No se entregan en el paquete. BepInEx genera sus
propios logs y cachés bajo `BepInEx/`; son datos de diagnóstico, no ajustes del mod.

Actualmente el companion no guarda preferencias, credenciales, logs propios ni
otros archivos. Emite los eventos por stdout y el mod registra los diagnósticos
mediante BepInEx. Se ejecuta desde la carpeta `companion/` junto a la DLL y termina
con su proceso padre. Si se añade persistencia, actualizar este inventario antes
de publicar.

Los recursos se resuelven desde `Info.Location` (carpeta del plugin), los ajustes
desde `Config.ConfigFilePath`. No requieren la ubicación de Steam, del checkout
ni del ZIP. Las partidas globales de Cuphead pertenecen al juego y este contrato
no las separa ni las incorpora al paquete.

## Verificación del despliegue

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-RuletaDevDeployment.ps1
```

Cubre paquetes incompletos o alterados, arquitectura incorrecta, archivos
prohibidos, rutas inseguras, duplicados, lectores concurrentes y reemplazo
bloqueado. No inicia el juego ni modifica la instalación del launcher.
