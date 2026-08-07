# Plantilla de traducción — Ruso (`Russian`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | УДАР 1 |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | УДАР 2 |
| `ui.slot.super` | SÚPER | Debajo del súper | СПЕЦАТАКА |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | НАВЫК |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | ИСПЫТАНИЕ |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | СЛОЖНОСТЬ |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | ИСПЫТАНИЕ |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | АВТОЗАГРУЗКА |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | ВКЛ. |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | ВЫКЛ. |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | ВКЛ. |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | ВЫКЛ. |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | НИЗКАЯ |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | ОБЫЧНАЯ |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | ВЫСОКАЯ |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | КРУТИТЬ! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | ИГРАТЬ! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | ОТКРЫТЬ РУЛЕТКУ |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | КРУТИТЬ ЕЩЁ РАЗ |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | ПОДГОТОВКА К БОЮ... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | РУЛЕТКА КРУТИТСЯ... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | ИСПЫТАНИЕ: |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | БЕЗ РЫВКА |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | БЕЗ МИНИ-САМОЛЁТА |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | ТОЛЬКО МИНИ-ПУЛИ |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | БЕЗ МИНИ-БОМБ |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | БЕЗ ОБЫЧНОГО ВЫСТРЕЛА |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | БЕЗ EX |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | ЧЕРНО-БЕЛЫЙ |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `Удар 1` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `Удар 2` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `Спецатака` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `Навык` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `НИЗКАЯ` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `ОБЫЧНАЯ` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `ВЫСОКАЯ` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `РЫВОК` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `МИНИ` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `МИНИ` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `СПЕЦАТАКА` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `МЕЛКАШКА` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `ВЫСТРЕЛ` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `Теперь твой аэроплан оснащен мини:бомбами. Переключиться на них можно в любой момент боя.` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `ЧЕРНО-БЕЛЫЙ` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
