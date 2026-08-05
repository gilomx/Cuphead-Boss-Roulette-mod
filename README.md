# Gilomx Boss Roulette

Mod para Cuphead 1.3.4 que traslada al juego la lógica de la ruleta de
`gilomx.com`. Funciona tanto con el juego base como con The Delicious Last
Course: detecta automáticamente si el DLC está disponible y sólo incluye
contenido que esa instalación puede cargar.

La interfaz utiliza las fuentes y los iconos originales del menú de
equipamiento de Cuphead, combinados con los retratos de la ruleta web. Al
terminar el giro, el mod equipa el resultado y carga directamente el combate.

Consulta [CHANGELOG.md](CHANGELOG.md) para ver el historial de cambios.

## Controles

- `F6`: abrir o cerrar la ruleta.
- Mando: mantener el gatillo izquierdo y pulsar el botón de Equip Card
  (`ZL + X` en Switch, `LT + Y` en Xbox, `L2 + Triángulo` en PlayStation).
- `↑` `↓`: moverse entre las opciones y la acción principal.
- `←` `→`: cambiar el valor de la opción seleccionada.
- `Enter`: cambiar una opción o confirmar `¡GIRAR!`/`¡JUGAR!`.
- `Esc`: cerrar la tarjeta.
- Cruceta o stick: moverse y cambiar opciones con el mando.
- Botón de confirmar: cambiar una opción o confirmar `¡GIRAR!`/`¡JUGAR!`.
- `ZR`/`RT`/`R2`: volver a girar si ya existe un resultado y la carga
  automática está desactivada.
- `F7`: volver a girar en ese mismo caso desde el teclado.

El giro dura cinco segundos y después detiene, uno por segundo, jefe, armas,
súper, amuleto y reto. El modo feo añade las restricciones de la ruleta web.

## Instalación

1. Instala BepInEx 5 x64 en la carpeta de Cuphead y ejecuta el juego una vez.
2. Compila el proyecto o descarga una versión publicada.
3. Coloca la DLL y la carpeta `assets` juntas en:

   `Cuphead\BepInEx\plugins\GilomxBossRoulette\`

4. Inicia una partida guardada, entra al mapa y pulsa `F6`.

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

- Los retos se muestran y aplican durante el combate cuando la opción `RETO`
  está activada.
- Sin el DLC, la animación y el resultado excluyen automáticamente sus jefes,
  armas y amuletos. Las tres posiciones de súper pertenecen al juego base.
- La ruleta puede prestar cualquier objeto del catálogo disponible aunque no se
  haya comprado todavía.
- El equipamiento anterior de ambos jugadores se restaura al ganar o abandonar
  el nivel; al perder y reintentar se conserva el resultado de la ruleta.
- Mientras ese resultado temporal siga activo, la Equip Card no puede abrirse
  desde la pantalla de derrota; vuelve a estar disponible al salir al mapa.
- Los jugadores no pueden caminar por el mapa mientras la ruleta está abierta;
  el movimiento se recupera inmediatamente al cerrarla.
- Cuphead usa su armamento de avión automáticamente en los combates aéreos.
- Cada reto utiliza una animación propia de tres frames a la misma velocidad
  visual que las armas, los súper y los amuletos de la tarjeta.
- El reto `Blanco y negro` utiliza un AssetBundle de 5 KB compilado con Unity
  2017.4.9f1 para realizar una transición continua y termina usando el filtro
  nativo del juego. No oculta la pelea ni cambia la preferencia visual guardada.
- La Reliquia Divina utiliza el estado guardado en la partida.
- El mod no desbloquea objetos ni modifica las compras o el progreso.
