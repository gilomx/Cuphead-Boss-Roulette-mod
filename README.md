# Gilomx Boss Roulette

Mod BepInEx 5 para Cuphead 1.3.4 con DLC. Traslada al juego la lógica de
`src/app/ruleta` del sitio de Gilomx.

## Controles

- `F6`: abrir o cerrar la ruleta.
- `F7`: girar.
- `Ctrl+I`: mostrar la selección forzada.

El giro dura cinco segundos y después detiene, uno por segundo, jefe, armas,
súper, amuleto y reto. Al terminar equipa el resultado y carga directamente el
combate.

## Instalación

1. Instala BepInEx 5 x64 en la carpeta de Cuphead y ejecuta el juego una vez.
2. Copia la carpeta `GilomxBossRoulette` a `Cuphead\BepInEx\plugins\`.
3. Inicia una partida guardada. La ruleta aparece automáticamente.

El archivo de configuración se crea en
`BepInEx\config\mx.gilomx.cuphead.bossroulette.cfg`.

## Notas

- El modo feo muestra el reto elegido durante el combate, igual que la web. El
  reto es una regla para el jugador; no bloquea físicamente los controles.
- Las armas terrestres se sortean también en jefes de avión, pero Cuphead usa
  su armamento de avión durante esos combates.
- La Reliquia Divina utiliza el estado de la reliquia guardado en la partida.
- El mod no guarda ni modifica el progreso por su cuenta, pero Cuphead puede
  registrar normalmente el resultado del combate.

## Compilación

```powershell
dotnet build -c Release
```

Para otra instalación:

```powershell
dotnet build -c Release -p:CupheadDir="D:\Juegos\Cuphead"
```
