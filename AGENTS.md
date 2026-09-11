# Despliegue de La Pichi Ruleta · Dev

Estas reglas se aplican al trabajo Dev en ambas PCs. Procedimiento y datos que
deben conservarse: [docs/launcher-dev-deployment.md](docs/launcher-dev-deployment.md).

- Usa `pwsh -NoProfile -File ./tools/deploy-launcher-dev.ps1` para compilar y
  publicar el paquete completo. Versiona el script, sus pruebas y estas
  instrucciones; excluye ZIP, cachés y configuración específica de cada PC.
- El único destino Dev es `%LOCALAPPDATA%\CupheadModLauncher\dev\pichi-ruleta\current.zip`.
  Calcúlalo exactamente así en PowerShell:

  ```powershell
  $packagePath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CupheadModLauncher\dev\pichi-ruleta\current.zip'
  $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($packagePath))
  ```

- Mantén la misma regla en ambas PCs. Cada PC genera su paquete local; esta
  ubicación no sincroniza archivos entre equipos. No versiones rutas absolutas
  con nombres de usuario, letras de unidad ni ubicaciones locales de Steam o
  del repositorio. Detecta las referencias de Cuphead o usa
  `launcher-dev.local.json`, excluido de Git.
- Genera un ZIP con rutas relativas a la raíz de Cuphead, sin carpeta
  contenedora: `winhttp.dll`, `doorstop_config.ini`, BepInEx x64 compatible con
  todas sus dependencias, `BepInEx/plugins/GilomxBossRoulette/Gilomx.CupheadBossRoulette.dll`,
  assets, companion y demás recursos de ejecución. Usa el empaquetado completo
  publicado como referencia. Excluye originales de Cuphead, partidas, logs,
  cachés, backups, configuraciones personales y otros mods.
- Compila y crea primero un ZIP temporal. Valida contenido e integridad antes
  de reemplazar `current.zip` atómicamente. Cualquier fallo debe conservar el
  paquete anterior; nunca escribas parcialmente sobre él.
- No despliegues ni arranques el mod desde la instalación original del juego.
  El original se reserva para jugar sin mods. Sólo se pueden leer sus DLL como
  referencias. Mod y companion deben resolver recursos desde el plugin en
  ejecución, sin depender de rutas de desarrollo.
- No escribas en la carpeta de ejecución compartida ni modifiques el catálogo
  del launcher. La futura opción «La Pichi Ruleta · Dev» aplicará el paquete en
  el siguiente arranque, compartiendo una carpeta de ejecución con los demás
  mods. Esa carga aún está pendiente: no afirmes que el launcher actual puede
  consumir `current.zip`.
- Puedes publicar mientras se juega. No cierres Cuphead ni su companion, no
  arranques el juego y no intentes recargar DLL en una sesión activa.
- No borres todavía los archivos del mod instalados en el original. La
  migración y limpieza se coordinarán con respaldo. Conserva los ajustes de la
  ruleta y sus copias de recuperación según el documento de despliegue; no los
  incluyas en paquetes distribuibles.
