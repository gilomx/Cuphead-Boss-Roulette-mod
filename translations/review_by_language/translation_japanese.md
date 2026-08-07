# Plantilla de traducción — Japonés (`Japanese`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | ショットA |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | ショットB |
| `ui.slot.super` | SÚPER | Debajo del súper | 必殺技 |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | お守り |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | チャレンジ |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | 難易度 |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | チャレンジ |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | 自動ロード |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | オン |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | オフ |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | オン |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | オフ |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | シンプル |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | レギュラー |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | エキスパート |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | 回す！ |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | プレイ！ |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | ルーレットを開く |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | もう一度回す |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | バトル準備中... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | 回転中... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | チャレンジ： |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | ダッシュ禁止 |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | ミニ化禁止 |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | ミニショットのみ |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | ミニボム禁止 |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | 通常ショット禁止 |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | EXショット禁止 |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | モノクロ |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `ショットA` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `ショットB` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `必殺技` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `お守り` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `シンプル` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `レギュラー` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `エキスパート` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `ダッシュ` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `ちぢむ` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `ちぢむ` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `EXショット` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `ミズデッポウ` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `撃つ` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `飛行機にミニボムが装備されたぞ！\nバトル中、いつでも武器の切り替えができるぞ！` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `モノクロ` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
