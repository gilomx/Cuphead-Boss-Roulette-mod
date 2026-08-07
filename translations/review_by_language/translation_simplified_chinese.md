# Plantilla de traducción — Chino simplificado (`SimplifiedChinese`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | 武器A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | 武器B |
| `ui.slot.super` | SÚPER | Debajo del súper | 必杀技 |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | 护符 |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | 挑战 |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | 难度 |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | 挑战 |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | 自动加载 |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | 开启 |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | 关闭 |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | 开启 |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | 关闭 |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | 简单 |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | 普通 |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | 专家 |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | 转动！ |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | 开始！ |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | 打开轮盘 |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | 再转一次 |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | 战斗准备中... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | 转动中... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | 挑战： |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | 禁止冲刺 |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | 禁止缩小 |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | 仅限小飞机子弹 |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | 禁止迷你炸弹 |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | 禁止普通射击 |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | 禁止EX攻击 |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | 黑白 |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `武器A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `武器B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `必杀技` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `护符` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `简单` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `普通` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `专家` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `冲刺` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `缩小` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `缩小` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `强力攻击` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `豆子枪` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `射击` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `你们的飞机现在装备上了迷你炸弹！！在战斗中可随时切换武器！` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `黑与白` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
