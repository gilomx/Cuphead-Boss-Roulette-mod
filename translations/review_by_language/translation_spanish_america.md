# Plantilla de traducción — Español de América Latina (`SpanishAmerica`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | TIRO A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | TIRO B |
| `ui.slot.super` | SÚPER | Debajo del súper | SÚPER |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | AMULETO |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | RETO |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | DIFICULTAD |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | RETO |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | CARGA AUTOMÁTICA |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | ACTIVADO |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | DESACTIVADO |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | ACTIVADA |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | DESACTIVADA |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | FÁCIL |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | NORMAL |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | EXPERTO |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | ¡GIRAR! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | ¡JUGAR! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | ABRIR RULETA |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | VOLVER A GIRAR |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | PREPARANDO COMBATE... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | GIRANDO... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | RETO: |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | NO DASH |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | NO MINIAVIÓN |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | SOLO BALAS DE MINIAVIÓN |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | NO DISPARO BOMBAS |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | SIN PEASHOOTER |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | NO EX |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | BLANCO Y NEGRO |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `Tiro A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `Tiro B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `Súper` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `Amuleto` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `FÁCIL` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `NORMAL` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `EXPERTO` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `TURBO` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `ENCOGER` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `ENCOGER` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `ATAQUE EX` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `LANZAGUISANTES` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `DISPARAR` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `¦Ahora sus aviones están equipados con minibombas! ¦Cambien de arma en cualquier momento del combate!` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `B/N` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
