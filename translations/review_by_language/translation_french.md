# Plantilla de traducción — Francés (`French`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | ARME A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | ARME B |
| `ui.slot.super` | SÚPER | Debajo del súper | SUPER |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | CHARME |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | DÉFI |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | DIFFICULTÉ |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | DÉFI |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | CHARGEMENT AUTO |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | ACTIVÉ |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | DÉSACTIVÉ |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | ACTIVÉ |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | DÉSACTIVÉ |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | FACILE |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | NORMAL |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | EXPERT |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | LANCER ! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | JOUER ! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | OUVRIR LA ROULETTE |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | RELANCER |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | PRÉPARATION DU COMBAT... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | LA ROULETTE TOURNE... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | DÉFI : |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | SANS DASH |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | SANS MINI-AVION |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | MINI-AVION UNIQUEMENT |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | SANS MINI-BOMBES |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | SANS TIR PRINCIPAL |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | SANS EX |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | NOIR ET BLANC |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `Arme A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `Arme B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `Super` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `Charme` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `FACILE` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `NORMAL` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `EXPERT` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `RUÉE` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `RÉTRÉCIR` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `RÉTRÉCIR` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `ATTAQUE EX` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `PÉTOIRE` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `TIRER` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `Vos avions sont maintenant équipés de mini:bombes_! Changez d'arme à tout moment en combat_!` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `NOIR ET BLANC` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
