# Plantilla de traducción — Coreano (`Korean`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | 무기 A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | 무기 B |
| `ui.slot.super` | SÚPER | Debajo del súper | 필살기 |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | 능력 |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | 도전 |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | 난이도 |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | 도전 |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | 자동 로드 |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | 켜짐 |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | 꺼짐 |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | 켜짐 |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | 꺼짐 |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | 쉬움 |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | 보통 |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | 어려움 |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | 돌리기! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | 시작! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | 룰렛 열기 |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | 다시 돌리기 |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | 전투 준비 중... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | 회전 중... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | 도전: |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | 대시 금지 |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | 소형 비행기 금지 |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | 소형 비행기 총알만 |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | 소형 폭탄 금지 |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | 기본 공격 금지 |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | EX 공격 금지 |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | 흑백 |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `무기 A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `무기 B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `필살기` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `능력` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `쉬움` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `보통` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `어려움` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `대시` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `움츠리기` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `움츠리기` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `강화 공격` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `장난감 총` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `슛` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `비행기에 소형 폭탄이 장착되었어! 전투 중 언제든지 무기를 바꿀 수 있어!` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `흑백` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
