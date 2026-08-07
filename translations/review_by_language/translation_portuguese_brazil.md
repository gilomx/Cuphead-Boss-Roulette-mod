# Plantilla de traducción — Portugués de Brasil (`PortugueseBrazil`)

Estado de esta copia: **archivo de revisión; no activa ni sustituye ninguna traducción**.

La tabla siguiente conserva sin cambios los 29 campos y el español de referencia de
`TRANSLATION_REVIEW_TEMPLATE.md`. La columna `Traducción aprobada` contiene la revisión aprobada.

| ID | Español de referencia | Dónde aparece | Traducción aprobada |
| --- | --- | --- | --- |
| `ui.slot.weapon_a` | TIRO A | Debajo del primer disparo | TIRO-A |
| `ui.slot.weapon_b` | TIRO B | Debajo del segundo disparo | TIRO-B |
| `ui.slot.super` | SÚPER | Debajo del súper | SUPER |
| `ui.slot.charm` | AMULETO | Debajo del amuleto | RELÍQUIAS |
| `ui.slot.challenge` | RETO | Debajo del círculo de reto | DESAFIO |
| `ui.setting.difficulty` | DIFICULTAD | Ajuste de la Equip Card | DIFICULDADE |
| `ui.setting.challenge` | RETO | Ajuste para activar retos | DESAFIO |
| `ui.setting.auto_load` | CARGA AUTOMÁTICA | Ajuste de carga del combate | CARREGAMENTO AUTOMÁTICO |
| `ui.value.enabled` | ACTIVADO | Valor del ajuste de reto | ATIVADO |
| `ui.value.disabled` | DESACTIVADO | Valor del ajuste de reto | DESATIVADO |
| `ui.value.enabled_feminine` | ACTIVADA | Valor de carga automática | ATIVADO |
| `ui.value.disabled_feminine` | DESACTIVADA | Valor de carga automática | DESATIVADO |
| `ui.difficulty.easy` | SIMPLE | Dificultad elegida | FÁCIL |
| `ui.difficulty.normal` | NORMAL | Dificultad elegida | NORMAL |
| `ui.difficulty.hard` | EXPERTO | Dificultad elegida | ESPECIALISTA |
| `ui.action.spin` | ¡GIRAR! | Banda inferior antes del giro | GIRAR! |
| `ui.action.play` | ¡JUGAR! | Banda inferior con carga automática desactivada | JOGAR! |
| `ui.action.open_roulette` | ABRIR RULETA | Prompt del mapa | ABRIR ROLETA |
| `ui.action.spin_again` | VOLVER A GIRAR | Prompt del mapa después de un giro manual | GIRAR NOVAMENTE |
| `ui.action.preparing` | PREPARANDO COMBATE... | Banda inferior antes de cargar | PREPARANDO COMBATE... |
| `ui.action.spinning` | GIRANDO... | Banda inferior durante el giro | GIRANDO... |
| `ui.challenge_prefix` | RETO: | HUD durante el combate | DESAFIO: |
| `challenge.no_dash` | No Dash | Nombre de reto en el HUD | SEM DASH |
| `challenge.no_mini_plane` | No mini avión | Nombre de reto en el HUD | SEM MINIAVIÃO |
| `challenge.mini_plane_only` | Solo mini avión | Nombre de reto en el HUD | SÓ MINIAVIÃO |
| `challenge.no_bombs` | No disparo bombas | Nombre de reto en el HUD | SEM MINIBOMBAS |
| `challenge.no_peashooter` | No disparo Peashooter | Nombre de reto en el HUD | SEM METRALHADORA |
| `challenge.no_ex` | No EX | Nombre de reto en el HUD | SEM EX |
| `challenge.black_and_white` | Blanco y negro | Nombre de reto en el HUD | PRETO E BRANCO |

## Cadenas originales relacionadas dentro de Cuphead

Estas referencias fueron extraídas del `LocalizationAsset` del juego. No rellenan
automáticamente la columna anterior: ayudan al traductor a conservar la terminología
oficial y a redactar correctamente los retos del mod.

| Campo del mod | Concepto solicitado | Clave de Cuphead | Cadena original en este idioma | Estado en Cuphead |
| --- | --- | --- | --- | --- |
| `ui.slot.weapon_a` | SHOT-A | `WeaponA` | `Tiro-A` | `IN-GAME` |
| `ui.slot.weapon_b` | SHOT-B | `WeaponB` | `Tiro-B` | `IN-GAME` |
| `ui.slot.super` | SUPER | `Super` | `Super` | `IN-GAME` |
| `ui.slot.charm` | CHARM | `Charm` | `Relíquias` | `IN-GAME` |
| `ui.difficulty.easy` | SIMPLE | `DifficultyMenuEasy` | `FÁCIL` | `IN-GAME` |
| `ui.difficulty.normal` | REGULAR | `DifficultyMenuNormal` | `NORMAL` | `IN-GAME` |
| `ui.difficulty.hard` | EXPERT | `DifficultyMenuHard` | `ESPECIALISTA` | `IN-GAME` |
| `challenge.no_dash` | DASH | `TutorialDash` | `CORRER` | `IN-GAME` |
| `challenge.no_mini_plane` | SHRINK | `TutorialShrink` | `ENCOLHER` | `IN-GAME` |
| `challenge.mini_plane_only` | SHRINK | `TutorialShrink` | `ENCOLHER` | `IN-GAME` |
| `challenge.no_ex` | EX MOVE | `TutorialExShot` | `GOLPE EX` | `IN-GAME` |
| `challenge.no_peashooter` | PEASHOOTER terrestre | `level_weapon_peashot_name` | `CHUMBINHO` | `IN-GAME` |
| `challenge.no_peashooter` | Disparar en avión | `TutorialShmupShoot` | `ATIRAR` | `IN-GAME` |
| `challenge.no_peashooter` | Nombre interno del tiro de avión | `plane_weapon_peashot_name` | `PEASHOT` | `Do Not Translate` |
| `challenge.no_bombs` | Minibombas en frase visible | `Canteen_Tooltip_ShmupWeapons` | `Seus aviões agora tem minibombas!! Para usá:las, é só trocar de arma durante a batalha!` | `IN-GAME` |
| `challenge.no_bombs` | Nombre interno de la bomba del avión | `plane_weapon_bomb_name` | `BOMB` | `Do Not Translate` |
| `challenge.black_and_white` | BLACK & WHITE | `OptionMenuFilterBlackWhite` | `PRETO E BRANCO` | `IN-GAME` |

### Notas de uso

- `OptionMenuFilterBlackWhite` es la opción real de Visual → Filtros.
- `plane_weapon_peashot_name` y `plane_weapon_bomb_name` son marcadores internos
  etiquetados `Do Not Translate`; no deben copiarse como localización del reto.
- Para las minibombas se incluye la frase visible completa porque Cuphead no ofrece
  una etiqueta localizada independiente. El traductor debe extraer el sustantivo y
  adaptarlo a la gramática de “sin/no usar”.
- Ninguna referencia de esta sección constituye por sí sola una traducción aprobada.
