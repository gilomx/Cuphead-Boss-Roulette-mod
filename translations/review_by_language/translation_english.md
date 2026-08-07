# Plantilla de traducción — Inglés (`English`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | SHOT-A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | SHOT-B |
| `ui.slot.super` | SÚPER | Debajo del súper | SUPER |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | CHARM |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | CHALLENGE |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | DIFFICULTY |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | CHALLENGE |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | AUTO-LOAD |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | ON |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | OFF |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | ON |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | OFF |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | SIMPLE |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | REGULAR |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | EXPERT |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | SPIN! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | PLAY! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | OPEN ROULETTE |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | SPIN AGAIN |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | PREPARING BATTLE... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | SPINNING... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | CHALLENGE: |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | NO DASH |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | NO MINI-PLANE |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | MINI-PLANE ONLY |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | NO MINI-BOMBS |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | NO PEASHOOTER |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | NO EX |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | BLACK & WHITE |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `SHOT-A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `SHOT-B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `SUPER` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `CHARM` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `SIMPLE` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `REGULAR` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `EXPERT` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `DASH` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `SHRINK` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `SHRINK` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `EX MOVE` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `PEASHOOTER` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `SHOOT` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `Your aeroplanes are now equipped with mini;bombs. Switch your weapon anytime during battle!` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `BLACK & WHITE` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
