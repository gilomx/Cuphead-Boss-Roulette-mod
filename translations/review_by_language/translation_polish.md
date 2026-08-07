# Plantilla de traducción — Polaco (`Polish`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | BROŃ A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | BROŃ B |
| `ui.slot.super` | SÚPER | Debajo del súper | SUPER |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | CZAR |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | WYZWANIE |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | TRUDNOŚĆ |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | WYZWANIE |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | AUTOMATYCZNE ŁADOWANIE |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | WŁĄCZONE |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | WYŁĄCZONE |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | WŁĄCZONE |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | WYŁĄCZONE |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | PROSTY |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | ZWYKŁY |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | EKSPERCKI |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | ZAKRĘĆ! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | GRAJ! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | OTWÓRZ RULETKĘ |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | ZAKRĘĆ PONOWNIE |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | PRZYGOTOWANIE DO WALKI... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | RULETKA SIĘ KRĘCI... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | WYZWANIE: |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | BEZ DASHA |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | BEZ MAŁEGO SAMOLOTU |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | TYLKO MAŁY SAMOLOT |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | BEZ BOMB |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | BEZ DZIAŁKA |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | BEZ EX |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | CZARNO-BIAŁY |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `Broń A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `Broń B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `Super` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `Czar` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `PROSTY` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `ZWYKŁY` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `EKSPERCKI` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `PĘD` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `ZMNIEJSZ` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `ZMNIEJSZ` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `ATAK EX` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `GROCHÓWKA` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `STRZAŁ` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `GROCHÓWKA` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `Teraz wasze samolociki są uzbrojone w bomby! Możecie zmieniać broń w dowolnym momencie walki!` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMBA` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `CZARNO-BIAŁY` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
