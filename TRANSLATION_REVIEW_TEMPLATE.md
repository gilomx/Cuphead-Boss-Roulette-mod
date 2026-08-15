# Plantilla para entregar una traducción

Idioma objetivo: **ESCRIBE AQUÍ EL IDIOMA DE LA TABLA SIGUIENTE**

Esta plantilla separa los **29 textos aprobados** del catálogo anterior y los
**26 textos nuevos pendientes**. El inventario cubre la Equip Card, los prompts
del mapa, el HUD de combate y el menú de Creator Tools.

Estado del catálogo anterior: **los doce idiomas están aprobados y activos**.
Las cinco cadenas de retos y las 21 de Creator Tools deben revisarse ahora sin
alterar las traducciones que ya habían sido aceptadas.

## Idiomas disponibles y orden histórico de Ctrl+F8

El atajo permanece desactivado. Si se habilita temporalmente para revisión,
la primera pulsación selecciona inglés y después avanza en este orden:

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

## Catálogo aprobado actual

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

## Pendiente de localización

Completa únicamente la columna `Traducción aprobada`. Estos IDs todavía no
deben mezclarse con el catálogo anterior hasta que las doce revisiones estén
validadas.

### Retos nuevos

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `challenge.rgb_shift` | RGB / desfase de canales | Nombre del reto en ruleta, HUD y overlay | |
| `challenge.upside_down` | 180 grados | Nombre del reto en ruleta, HUD y overlay | |
| `challenge.hp_one` | HP.1 / una vida | Nombre del reto en ruleta, HUD y overlay | |
| `challenge.ink_rain` | Lluvia de tinta | Nombre del reto en ruleta, HUD y overlay | |
| `challenge.half_damage` | Daño -50% | Nombre del reto en ruleta, HUD y overlay | |

### Creator Tools

`CREATOR TOOLS`, las escalas, los porcentajes y la URL local se conservan sin
traducción. Traduce las siguientes etiquetas y mensajes:

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `creator.menu.preview` | VISTA PREVIA | Menú Creator Tools | |
| `creator.menu.size` | TAMAÑO | Menú Creator Tools | |
| `creator.menu.order` | ORDEN | Menú Creator Tools | |
| `creator.menu.alignment` | ALINEACIÓN | Menú Creator Tools | |
| `creator.menu.opacity` | OPACIDAD | Menú Creator Tools | |
| `creator.action.copy_overlay_url` | COPIAR URL DEL OVERLAY | Menú alternativo | |
| `creator.action.copy_url` | COPIAR URL | Menú nativo | |
| `creator.value.enabled` | ACTIVADO | Valor de opción | |
| `creator.value.disabled` | DESACTIVADO | Valor de opción | |
| `creator.order.icons_above` | ICONOS ARRIBA | Valor de orden | |
| `creator.order.text_above` | TEXTO ARRIBA | Valor de orden | |
| `creator.alignment.left` | IZQUIERDA | Valor de alineación | |
| `creator.alignment.center` | CENTRO | Valor de alineación | |
| `creator.alignment.right` | DERECHA | Valor de alineación | |
| `creator.feedback.copied` | COPIADA | Confirmación corta | |
| `creator.feedback.url_copied` | URL COPIADA | Aviso temporal | |
| `creator.controls.change_back` | ACEPTAR: CAMBIAR · CANCELAR: VOLVER | Ayuda inferior | |
| `creator.status.server_disabled` | SERVIDOR DESACTIVADO | Estado local | |
| `creator.status.no_port` | NO HAY UN PUERTO DISPONIBLE | Error local | |
| `creator.status.client` | CLIENTE | Contador singular | |
| `creator.status.clients` | CLIENTES | Contador plural | |

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
