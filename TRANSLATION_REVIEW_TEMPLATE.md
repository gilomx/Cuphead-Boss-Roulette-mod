# Plantilla para entregar una traducción

Idioma objetivo: **ESCRIBE AQUÍ EL IDIOMA DE LA TABLA SIGUIENTE**

Esta plantilla contiene únicamente los **29 textos propios del mod que el
jugador puede ver en la interfaz actual**. El inventario se verificó directamente
contra la Equip Card activa, los prompts del mapa y el HUD de combate.

Estado de entregas: **inglés, francés, italiano, alemán y ambas variantes de
español aprobados e incorporados en 0.5.115**. Los dos españoles usan el texto
original. Usa esta plantilla para los seis idiomas restantes.

## Idiomas disponibles y orden de Ctrl+F8

La primera pulsación siempre selecciona inglés. Después avanza en este orden:

| Número | Idioma | Nombre interno de Cuphead | Archivo recomendado |
| ---: | --- | --- | --- |
| 1 | Inglés | `English` | `translation_english.md` |
| 2 | Francés | `French` | `translation_french.md` |
| 3 | Italiano | `Italian` | `translation_italian.md` |
| 4 | Alemán | `German` | `translation_german.md` |
| 5 | Español de España | `SpanishSpain` | `translation_spanish_spain.md` |
| 6 | Español de América Latina | `SpanishAmerica` | `translation_spanish_america.md` |
| 7 | Coreano | `Korean` | `translation_korean.md` |
| 8 | Ruso | `Russian` | `translation_russian.md` |
| 9 | Polaco | `Polish` | `translation_polish.md` |
| 10 | Portugués de Brasil | `PortugueseBrazil` | `translation_portuguese_brazil.md` |
| 11 | Japonés | `Japanese` | `translation_japanese.md` |
| 12 | Chino simplificado | `SimplifiedChinese` | `translation_simplified_chinese.md` |

Por ejemplo, para revisar inglés escribe arriba:

```text
Idioma objetivo: Inglés (`English`)
```

Cómo usarla:

1. Elige un idioma de la tabla, copia este archivo y usa el nombre recomendado.
2. Escribe únicamente la columna `Traducción aprobada`.
3. Puedes adjuntar el archivo en el chat o pegar sólo las filas que cambiaste.
4. Conserva el `ID`; así puedo incorporar el texto sin confundir etiquetas
   repetidas como `RETO`.

No traduzcas nombres de jefes, armas, supers ni amuletos: los nombres visibles
que proceden del juego usan la localización oficial de Cuphead. Los símbolos de
botones y teclas (`F6`, `F7`, `ZL`, `LT`, etc.) tampoco forman parte de estas
traducciones.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | |
| `ui.slot.super` | SÚPER | Debajo del súper | |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | |

## Textos excluidos después de la revisión

No hace falta traducir lo siguiente para la primera versión:

| Grupo | Motivo |
| --- | --- |
| `status.*` | Sólo lo dibuja `DrawRouletteLegacy()`, la interfaz antigua que ya no se ejecuta. Esto incluye `status.scene_loading`. |
| `ui.brand`, `ui.tagline`, `ui.action.close`, `ui.value.selected`, `ui.value.rolling` y `ui.controls` | Pertenecen únicamente a la interfaz antigua. |
| `ui.action.select_save` | La Equip Card sólo puede dibujarse cuando ya hay una partida cargada; el estado defensivo no llega a mostrarse. |
| `common.none`, `charm.cursed_relic` y `charm.divine_relic` | La tarjeta actual muestra iconos de equipo, no sus nombres escritos. |
| `challenge.none` | Cuando no hay reto se muestra el círculo vacío, sin el texto “Nada”. |
| Aviso de `Ctrl+F8` | Es una herramienta temporal de desarrollo y se desactivará antes del lanzamiento. |
| Configuración de BepInEx y logs | No aparecen dentro de la interfaz del juego. |
| Subtítulo del nivel | Por decisión actual sólo se muestra en español; en los demás idiomas queda vacío. |

Los IDs excluidos pueden permanecer internamente como respaldo o para depuración;
su presencia en el código no significa que deban traducirse.

## Forma corta para enviarlo directamente por chat

También puedes responder así, sin adjuntar el archivo:

```text
Idioma: English
ui.action.open_roulette = OPEN ROULETTE
ui.action.spin_again = SPIN AGAIN
challenge.no_dash = NO DASH
challenge.no_mini_plane = NO MINI PLANE
```

Las filas que no menciones quedarán pendientes de aprobación. Ningún borrador
se activará automáticamente sin revisarlo primero.
