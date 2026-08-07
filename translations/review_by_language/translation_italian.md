# Plantilla de traducción — Italiano (`Italian`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | SPARO A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | SPARO B |
| `ui.slot.super` | SÚPER | Debajo del súper | SUPER |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | AMULETO |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | SFIDA |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | DIFFICOLTÀ |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | SFIDA |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | CARICAMENTO AUTOMATICO |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | ATTIVA |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | DISATTIVA |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | ATTIVO |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | DISATTIVO |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | FACILE |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | NORMALE |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | ESPERTO |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | GIRA! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | GIOCA! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | APRI LA ROULETTE |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | GIRA DI NUOVO |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | PREPARAZIONE SCONTRO... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | GIRO IN CORSO... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | SFIDA: |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | SENZA DASH |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | SENZA MINI-AEREO |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | SOLO MINI-AEREO |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | SENZA MINI BOMBE |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | SENZA MITRAGLIATRICE |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | SENZA EX |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | MONOCROMO |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `Sparo A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `Sparo B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `Super` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `Amuleto` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `FACILE` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `NORMALE` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `ESPERTO` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `SCATTA` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `MINI` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `MINI` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `MOSSA EX` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `SCACCIACANI` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `SPARA` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `Ora i vostri aerei sono dotati di mini bombe! Potete cambiare arma in qualsiasi momento durante la battaglia!` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `MONOCROMO` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
