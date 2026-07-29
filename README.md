# Gilomx Boss Roulette

Mod para Cuphead 1.3.4 con DLC que traslada al juego la lógica de la ruleta de
`gilomx.com`.

La interfaz utiliza las fuentes y los iconos originales del menú de
equipamiento de Cuphead, combinados con los retratos de la ruleta web. Al
terminar el giro, el mod equipa el resultado y carga directamente el combate.

## Controles

- `F6`: abrir o cerrar la ruleta.
- `F7`: girar.
- `Ctrl+I`: mostrar la selección forzada.

El giro dura cinco segundos y después detiene, uno por segundo, jefe, armas,
súper, amuleto y reto. El modo feo añade las restricciones de la ruleta web.

## Instalación

1. Instala BepInEx 5 x64 en la carpeta de Cuphead y ejecuta el juego una vez.
2. Compila el proyecto o descarga una versión publicada.
3. Coloca la DLL y la carpeta `assets` juntas en:

   `Cuphead\BepInEx\plugins\GilomxBossRoulette\`

4. Inicia una partida guardada y pulsa `F7`.

El archivo de configuración se crea en:

`BepInEx\config\mx.gilomx.cuphead.bossroulette.cfg`

## Compilación

La ruta predeterminada del proyecto es la instalación habitual de Steam:

```powershell
dotnet build -c Release
```

Para otra instalación:

```powershell
dotnet build -c Release -p:CupheadDir="D:\Juegos\Cuphead"
```

## Notas

- Los retos del modo feo se muestran durante el combate; son reglas para el
  jugador y no bloquean físicamente los controles.
- Cuphead usa su armamento de avión automáticamente en los combates aéreos.
- La Reliquia Divina utiliza el estado guardado en la partida.
- El mod no modifica el progreso por su cuenta.
