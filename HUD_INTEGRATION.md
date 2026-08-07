# Guía de integración del HUD de combate

Esta guía describe la arquitectura aceptada del HUD que muestra el resultado de
la ruleta durante una pelea. Léela antes de agregar indicadores, iconos, texto o
estados nuevos. El historial de experimentos y correcciones sigue en
[PROJECT_HANDOFF.md](PROJECT_HANDOFF.md); aquí se conserva únicamente el diseño
final y sus invariantes.

## Archivos principales

- `BattleResultHud.cs`: creación, contenido, posición, capas, animación,
  saturación y ciclo de vida del HUD.
- `Plugin.cs`: inicia y termina la sesión, conserva el HUD durante una victoria
  y contiene los hooks de juego que disparan esos cambios.
- `NativeMapPrompt.cs`: llama `UpdateBattleResultHud()` desde `LateUpdate`,
  después de que la UI nativa terminó su actualización del cuadro.
- `RouletteData.cs`: catálogo y nombres de sprites/texturas que alimentan los
  iconos.
- `GameTheme.cs`: acceso a sprites y fuentes originales de Cuphead.

## Invariantes visuales actuales

Los valores están concentrados al inicio de `BattleResultHud.cs`:

| Constante | Valor | Propósito |
| --- | ---: | --- |
| `BattleHudAlpha` | `0.70` | Alpha propio de iconos y texto. |
| `BattleHudImpactVolume` | `1.0` | Ganancia neutra; la sonoridad ya está incorporada en el WAV procesado. |
| `BattleHudPauseAlphaMultiplier` | `0.70` | Alpha del `CanvasGroup` mientras la pausa está visible. |
| `BattleHudResumeAlphaDuration` | `0.30 s` | Regreso suave del `CanvasGroup` a `1.0` al reanudar. |
| `BattleHudIconSize` | `48` | Ancho y alto de cada icono. |
| `BattleHudIconGap` | `-2` | Superposición horizontal que compensa el aire transparente del arte. |
| `BattleHudRightMargin` | `26` | Margen derecho en un jugador. |
| `BattleHudBottomMargin` | `13` | Posición vertical normal. |
| `BattleHudPauseBottomMargin` | `13` | Posición vertical durante pausa; igualarlo evita saltos. |
| `BattleHudTextGap` | `10` | Separación entre el último icono y el reto. |
| `BattleHudMaxTextWidth` | `420` | Ancho máximo antes de reducir la fuente. |
| `BattleHudMultiplayerSideGap` | `18` | Aire respecto a los HUD nativos de P1 y P2. |

El alpha de pausa es multiplicativo: cada elemento conserva alpha `0.70` y la
raíz recibe otro `0.70` mediante `CanvasGroup`. No reemplaces uno con el otro sin
validar visualmente el resultado. Al reanudar, sólo cambia el alpha; la posición
no se anima ni se recalcula de una forma distinta.

## Modelo de contenido

`battleHudIcons` se crea con cinco `RawImage` en este orden lógico:

1. Tiro A.
2. Tiro B.
3. Súper.
4. Amuleto.
5. Reto.

En un nivel terrestre se muestran los cinco. En un nivel de avión se reutilizan
los dos primeros objetos visuales como amuleto y reto, y
`battleHudVisibleIconCount` pasa a `2`. Los disparos y el súper terrestre no se
muestran porque no controlan el avión.

`BeginBattleResultHudSession()` copia el `RouletteResult` y el nombre del reto a
un snapshot. Toda la presentación debe leer ese snapshot, no la configuración
mutable de la tarjeta. Así, reintentos, cambios internos de escena y restauración
del equipamiento no cambian lo que se ve durante la pelea.

Los objetos nativos usan `ApplyNativeBattleHudIcon()`: busca el primer frame con
`theme.GetSprite()`, conserva el atlas mediante `uvRect` y sólo recurre al PNG
del mod si el sprite no existe. Los retos usan una textura estática. El HUD no
reproduce continuamente las animaciones de la Equip Card; sólo muestra el primer
frame para no distraer durante el combate.

Cuando un disparo, súper, amuleto o reto queda vacío, el HUD no usa el arte
negro directamente: `ApplyWhiteBattleHudEmptyIcon()` conserva el alpha del
primer frame nativo y lo convierte en una silueta blanca. Si el recorte del
atlas no es seguro, genera un círculo blanco segmentado de 72×72. Esta regla
es exclusiva del HUD de combate; la Equip Card conserva el círculo oscuro y
anima sus tres frames nativos.

## Ciclo de vida

1. `BeginBattleResultHudSession()` se ejecuta al lanzar el nivel desde la
   ruleta. Activa la presentación, toma el snapshot y reinicia la entrada.
2. `UpdateBattleResultHud()` se ejecuta en `LateUpdate`. Valida que haya una
   batalla activa, prepara la UI, elige su capa, actualiza contenido, saturación
   y entrada.
3. Las transiciones de iris o fase pueden desactivar temporalmente el HUD
   nativo. En ese caso la fila se oculta, pero no se borra el progreso de su
   entrada.
4. Al perder y reintentar se conserva la sesión y no se repite la animación.
5. `KeepBattleResultHudThroughVictory()` activa la ruta de victoria y traslada
   una copia preparada al Canvas nativo. Chef Saleroso conserva la misma raíz
   y la mueve al canvas de `SceneLoader`, debajo del fader, porque desactiva
   `LevelHUD.Canvas` antes de terminar la transición.
6. `EndBattleResultHudSession()` limpia el snapshot únicamente al volver al mapa
   o abandonar definitivamente la pelea.

El Palacio de Dados encadena varias escenas dentro de una sola pelea lógica.
`BattleHudUsesDicePalaceChain()` evita reiniciar la entrada y sus sonidos entre
casillas. También permite conservar la fila mientras
`SceneLoader.CurrentlyLoading` está activo al finalizar un minijefe. Durante
esa carga interna, si `battleHudFollowNativeVictoryLayer` continúa en `false`,
`PlaceBattleHudOnGameplayLayer()` usa `PlaceBattleHudOnSceneTransitionLayer()`
para que el fader nativo oscurezca la fila en lugar de ocultarla antes. La
victoria final contra Rey Dado pone el flag en `true` y debe continuar por
`LevelHUD.Canvas`. No conviertas una ocultación temporal en un fin de sesión ni
reinicies el snapshot entre casillas.

## Capas y estados de render

| Estado | Padre del HUD | Motivo |
| --- | --- | --- |
| Combate normal | Canvas overlay persistente propio | Evita destellos y pulsos producidos por el parry sobre la cámara de `LevelHUD`. |
| Pausa | Primer hijo del `LevelPauseGUI` activo | El oscurecimiento afecta la fila y la tarjeta/ayudas quedan encima. |
| Derrota | Primer hijo del `Background` de `LevelGameOverGUI` | Comparte la presentación del menú de derrota sin tapar sus controles. |
| Victoria | Copia dentro de `LevelHUD.Canvas` | Se oscurece y desaparece junto con la vida y las cartas nativas. |
| Victoria de Chef Saleroso | Primer hijo de `SceneLoader.canvas` | Sobrevive al apagado temprano de `LevelHUD`; el fader nativo queda encima y oscurece la fila junto con el juego. |
| Iris/fase sin HUD nativo | Oculto temporalmente | No atraviesa la máscara y conserva su estado de entrada. |

Durante combate normal, `PlaceBattleHudOnGameplayLayer()` mantiene la raíz en
el overlay y exige que `LevelHUD.Canvas` exista y esté visible. No coloques el
HUD directamente en `LevelHUD.Canvas` durante toda la pelea: esa implementación
ya produjo parpadeos al hacer parry.

La pausa real se detecta recorriendo instancias activas de `LevelPauseGUI` y
comprobando `pauseGui.state != 0`. No uses `PauseManager.state`: el hit-stop de
un parry también modifica ese estado durante algunos cuadros y hacía que el HUD
saltara de jerarquía. Al entrar en pausa el alpha de raíz cambia inmediatamente
a `0.70`; al salir, `FadeBattleHudRootAlphaToFull()` usa
`Time.unscaledDeltaTime` durante `0.30 s`, por lo que la transición no depende
de `timeScale`.

## Posición para uno y dos jugadores

En un jugador la raíz se ancla abajo a la derecha, con pivote `(1, 0)`, margen
derecho `26` y margen inferior `13`.

En cooperativo no hay una coordenada fija. `TryGetBattleHudMultiplayerGap()`
obtiene por reflexión `LevelHUD.cuphead` y `LevelHUD.mugman`, mide sólo sus
componentes `health` y `super`, transforma esos límites desde el Canvas nativo
al padre actual y centra la fila en el espacio libre. Esto permite que funcione
con distinta cantidad de cartas, resolución y capa de pausa. Si no se pueden
medir ambos jugadores, vuelve de forma segura a la posición de un jugador.

No agregues un desplazamiento exclusivo de pausa si lo que se busca es una
posición permanente: `BattleHudBottomMargin` y
`BattleHudPauseBottomMargin` deben cambiar juntos para evitar un salto al abrir
el menú.

## Entrada, sonido y estado estable

La entrada usa tiempo real para seguir funcionando aunque el juego cambie
`timeScale`:

- Espera inicial: `1.1 s`.
- Separación entre iconos: `0.28 s`.
- Pulso por icono: `0.38 s`, con un máximo de `1.075x`.
- Aparición del texto: `0.28 s` después de los iconos.
- Sonido: `impact_01.wav`, una sola vez por icono revelado, volumen relativo `1.0`.

El WAV está procesado con +20 dB antes de un limitador rápido a −1 dB. Sus
mediciones pasan de aproximadamente −20.01 a −12.4 LUFS y de −20.2 a
−11.4 dB de volumen medio. El valor `1.0` evita volver a amplificarlo en
`PlayOneShot()`. `effectsAudioSource` continúa conectado a
`AudioManagerMixer.GetGroups().sfx`, por lo que los controles Principal y
Efectos del juego lo atenúan y cualquiera de ellos en cero lo silencia.

Después de esa entrada los iconos quedan en escala `1`, el texto deja de
animarse y toda la fila permanece quieta. `battleHudImpactPlayedCount` evita
duplicar sonidos. Si se agrega un elemento, su revelado debe participar en este
contador; no se debe crear un `Update` separado que vuelva a pulsarlo.

## Blanco y negro

El overlay independiente necesita el material de saturación propio para seguir
la transición del reto `Blanco y negro`. `UpdateBattleResultHudSaturation()`
actualiza `_Saturation` con `1 - blackAndWhiteBlend` y lo aplica a iconos y
texto. En la capa nativa de victoria se restaura el material normal para que la
cámara de Cuphead controle el resultado final.

Cualquier elemento gráfico nuevo debe aceptar el mismo material cuando vive en
el overlay. De lo contrario permanecerá a color mientras el resto de la pelea
se vuelve blanco y negro.

## Cómo agregar un indicador nuevo

1. Decide si el dato pertenece al resultado inmutable. Si es así, añádelo al
   snapshot de `BeginBattleResultHudSession()`.
2. Reserva o crea su `RawImage`/`Text` en `CreateBattleHudRoot()`. Mantén
   `raycastTarget = false`.
3. Llena su contenido en `UpdateBattleResultHudContents()` usando primero un
   sprite nativo y después un fallback del mod.
4. Define explícitamente si aparece en niveles terrestres, de avión o ambos.
5. Inclúyelo en `UpdateBattleResultHudLayout()`, `BattleHudIconsWidth()` y el
   cálculo de texto largo. No desplaces sólo una capa.
6. Inclúyelo en `UpdateBattleResultHudReveal()` y en el contador de impactos,
   o decláralo estático si no debe participar en la entrada.
7. Haz que reciba el material de saturación de `Blanco y negro`.
8. Verifica que `TrySwapBattleHudToNativeVictoryLayer()` encuentre el nuevo
   componente en la copia. Si aumenta el número de iconos, actualiza también la
   validación mínima de `nativeIcons`.
   Chef Saleroso es la excepción: `battleHudHoldOverlayThroughVictory` conserva
   la raíz hasta que `Level.Current` deja de ser una batalla, pero durante el
   fundido debe vivir como primer hijo de `SceneLoader.canvas`, nunca encima de
   su fader.
9. Conserva la misma raíz durante pausa, derrota, reintento y Palacio de Dados;
   no crees overlays paralelos para cada estado.
10. Documenta cualquier constante visual nueva aquí y en `CHANGELOG.md`.

## Matriz mínima de pruebas

Antes de publicar una extensión del HUD, probar:

- Un jugador, nivel terrestre, reto corto y reto largo.
- Un jugador, nivel de avión: sólo amuleto y reto.
- Dos jugadores con pocas y cinco cartas en ambos lados.
- Pausar y reanudar: misma posición, menú por encima y fade de `0.30 s`.
- Hacer varios parry: sin destello, pulso ni cambio de jerarquía.
- Perder, volver a intentar y abandonar al mapa.
- Ganar: la fila debe desaparecer junto con el HUD nativo, no antes.
- Una transición de iris o cambio de fase que enmascare el HUD original.
- Una cadena del Palacio de Dados.
- Reto `Blanco y negro`, incluyendo iconos y texto.
- Resoluciones 1280×720 y 1920×1080 como mínimo.

## Selectores temporales de prueba

En la versión `0.5.122`:

- `ForceFiveSuperCardsForHudTest = false`.
- `ForcedTestChallenge = ModifierId.None`.
- `ForceRelicTestSequence = false`.
- `ForcePlaneRelicChallengeTestSequence = false`.
- `ForceTestBoss = false`.
- `ForcedTestBossSequence = { Levels.Saltbaker, Levels.Devil }`.
- `EnableLanguageTestShortcut = false`.

La secuencia Saltbaker/Diablo y el atajo `Ctrl+F8` permanecen disponibles sólo
como mecanismos de diagnóstico, pero ambos están desactivados. Todos los
selectores temporales están apagados en la compilación normal.

## Errores que no deben reintroducirse

- No usar `PauseManager.state` para detectar el menú de pausa.
- No mantener la fila en `LevelHUD.Canvas` durante combate activo.
- No aumentar el `sortingOrder` para tapar tarjetas o controles nativos.
- No reiniciar la entrada cuando el HUD nativo desaparece por una fase o iris.
- No animar continuamente los frames de los iconos durante la pelea.
- No limpiar el snapshot al perder si todavía se puede reintentar.
- No modificar sólo el margen de pausa si se desea mover la posición general.
- No restaurar alpha `1.0` en un solo cuadro al reanudar; conservar el fade con
  tiempo no escalado.
