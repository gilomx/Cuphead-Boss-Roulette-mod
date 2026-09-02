# Mugman Skin Mod

Proyecto independiente para crear y, en una segunda etapa, cargar una skin de
Mugman. Comparte el repositorio con La Pichi Ruleta, pero **no comparte DLL,
GUID de BepInEx ni compilación**.

## Estado

- Flujo de extracción y organización para Photoshop: listo.
- Proyecto BepInEx independiente: creado.
- Reemplazo de sprites durante el juego: pendiente hasta tener la primera
  secuencia editada con la que validar pivotes, recorte y rendimiento.

## Por qué está separado

`Gilomx.MugmanSkinMod.csproj` genera `Gilomx.MugmanSkinMod.dll`. La raíz de
La Pichi Ruleta excluye toda esta carpeta, por lo que compilar uno no introduce
código del otro. En una instalación futura, el usuario podrá activar o quitar
la skin sin afectar la ruleta.

## 1. Preparar Python

Desde la raíz del repositorio:

```powershell
python -m pip install -r MugmanSkinMod\tools\requirements.txt
```

## 2. Extraer y ordenar los frames

```powershell
python MugmanSkinMod\tools\extract_mugman_ground.py
```

Si Cuphead está instalado en otro lugar:

```powershell
python MugmanSkinMod\tools\extract_mugman_ground.py `
  --game-dir "D:\SteamLibrary\steamapps\common\Cuphead"
```

El resultado aparece en `MugmanSkinMod/generated/`:

- `original/`: recortes originales, útiles como referencia y respaldo.
- `aligned/`: frames sobre el canvas lógico de Unity, listos para Photoshop.
- `contact-sheets/`: vistas numeradas por animación.
- `manifest.json` y `manifest.csv`: pivote, tamaño, posición y nombre original.
- `summary.json`: conteos por acción y secuencia.

Los archivos generados están ignorados por Git porque contienen arte extraído
de una instalación local de Cuphead.

## 3. Trabajar una animación en Photoshop

1. En Photoshop abre `Archivo > Secuencias de comandos > Examinar`.
2. Ejecuta `photoshop/ImportarSecuenciaComoGrupos.jsx`.
3. Selecciona, por ejemplo,
   `generated/aligned/02_movimiento/mugman_run`.
4. El script crea un grupo por frame con `Gorra`, `Barba` y el original
   bloqueado. También construye automáticamente un cuadro de animación por
   grupo.
5. Abre `Ventana > Línea de tiempo` para reproducir o cambiar la duración. No
   uses `Make Frames From Layers`: Photoshop convertiría también las tres capas
   internas de cada grupo en cuadros independientes.
6. Guarda el PSD fuera de `generated/`, preferentemente en
   `MugmanSkinMod/photoshop-work/`.
7. Ejecuta `photoshop/ExportarGruposComoPNG.jsx` para exportar todos los grupos
   conservando sus nombres originales.

No recortes el canvas alineado. El manifiesto preserva el pivote utilizado por
Unity y permitirá que el mod de ejecución coloque cada frame sin saltos.

## Orden sugerido de producción

1. `mugman_idle` (5) para definir la gorra y la barba.
2. `mugman_run` (16) para validar seguimiento y deformación.
3. Salto, dash y agachado.
4. Disparo normal y apuntado.
5. Daño, parry, EX, muerte y supers.

Cuando `idle` y `run` estén editados, ya habrá suficiente material para
implementar y probar el reemplazo en tiempo real antes de dibujar los 656
frames restantes.
