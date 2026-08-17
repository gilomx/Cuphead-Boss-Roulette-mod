# Catálogo de localización

Estado: **los 12 idiomas de Cuphead tienen tablas activas en 0.5.120**. `SpanishSpain` usa la tabla española base; `SpanishAmerica` hereda esa tabla y cambia sólo `challenge.no_peashooter` a `SIN PEASHOOTER`. `LOCALIZATION_TRANSLATIONS.md` queda como registro histórico.

## Auditoría de Creator Tools (0.5.131)

El inventario público pendiente contiene **33 IDs visibles dentro del juego**:
cinco nombres de retos y 28 textos de Creator Tools para los 12 idiomas de
Cuphead. La revisión añadió `creator.status.port_updated`, visible cuando el
servidor elige automáticamente otro puerto, a las 27 cadenas ya inventariadas.

Los nombres reales de jefes, armas, supers y amuletos continúan usando la
localización oficial de Cuphead. `LA PICHI RULETA`, `CREATOR TOOLS`, `Cuphead`,
las escalas, los porcentajes y la URL local permanecen sin traducir. La fuente
de verdad del inventario pendiente es `TRANSLATION_REVIEW_TEMPLATE.md`.

## Placeholder del reto RGB (0.5.129)

El reto experimental usa temporalmente `RGB` como nombre visible en las 12
tablas. Esto evita que otro idioma reciba un respaldo español mientras el reto
está forzado para pruebas. Antes de habilitarlo públicamente hay que decidir su
nombre final y solicitar las traducciones correspondientes.

## Placeholder del reto 180° (0.5.129, desarrollo)

El reto experimental `ModifierId.UpsideDown` usa temporalmente `180°` como
nombre visible en las 12 tablas. El texto se entiende durante la prueba sin
introducir un respaldo español en otros idiomas. Todavía no se suma a los 29
textos de la plantilla pública: antes de habilitarlo hay que elegir el nombre
final y solicitar sus traducciones.

## Ajuste uniforme de etiquetas de equipo (0.5.128)

Las cinco etiquetas bajo los iconos comparten un solo tamaño de fuente por
idioma. Se mide la tipografía real de Cuphead con `GUIStyle.CalcSize`, se
prueban tamaños de 14 a 11 y se elige el primero en que las cinco cadenas
quepan dentro de un ancho seguro de 94 unidades. Sus rectángulos siguen
midiendo 98 × 23 y no permiten salto de línea. El cálculo queda en caché hasta
que cambia el idioma o se reconstruye el estilo.

La variante rusa fue validada manualmente con `СПЕЦАТАКА` e `ИСПЫТАНИЕ`
completos. Las traducciones aprobadas no se acortaron. `Ctrl+F8` es únicamente
una herramienta temporal de revisión y está desactivado en la versión normal.

## Alcance visible verificado en 0.5.110 y aplicado en 0.5.111

La fuente de verdad para solicitar traducciones es
`TRANSLATION_REVIEW_TEMPLATE.md`. Una revisión directa de las rutas que se
dibujan actualmente redujo el alcance público a **29 textos**:

- etiquetas, ajustes y acciones de la Equip Card actual;
- los dos prompts de la ruleta en el mapa;
- el prefijo del HUD y los siete nombres reales de reto.

Los `status.*` no aparecen en la interfaz actual: `StatusText()` sólo se llama
desde `DrawRouletteLegacy()`, que no forma parte de `OnGUI()`. Por la misma
razón se excluyen el título, eslogan, ayuda y botón de cierre antiguos. La
tarjeta actual muestra iconos de equipo, no los textos `Nada`, `Reliquia
Maldita` o `Reliquia Divina`; cuando no hay reto tampoco escribe `Nada`.

El aviso de `Ctrl+F8`, los logs y las descripciones de configuración son
herramientas técnicas y no pertenecen a la traducción pública de la primera
versión. `LOCALIZATION_TRANSLATIONS.md` conserva propuestas históricas, pero
sus filas adicionales no son una solicitud de traducción.

Las diez tablas no españolas activas están versionadas como `translations/translation_<idioma>.md`; `SpanishSpain` usa `translations/translation_spanish_shared.md` como base y `SpanishAmerica` registra su tabla efectiva en `translations/translation_spanish_america.md`. Las 12 entregas revisadas completas se conservan en `translations/review_by_language/`. Cada grupo contiene exactamente 29 valores y se carga sólo para su enum.

## Base técnica implementada en 0.5.109

- `ModifierId` identifica los ocho resultados de reto; ninguna restricción de
  gameplay compara ya nombres como `No EX` o `No mini avión`.
- `RouletteStatus` sustituye las frases almacenadas en `status`; la tarjeta ya
  no busca la palabra española `PARTIDA` para decidir su acción principal.
- `ModLocalization` concentra los IDs `ModText`, el español aceptado, el
  fallback y la traducción visible de cada `ModifierId`.
- El servicio lee `Localization.language`, se suscribe a
  `Localization.OnLanguageChangedEvent` y se desconecta en `OnDestroy()`.
- Tarjeta, prompts del mapa, etiqueta del reto, HUD y la interfaz antigua
  resuelven sus textos al dibujarse. Cambiar el idioma invalida también la
  medición en caché del prompt nativo.
- Los snapshots del HUD guardan `ModifierId`, no texto. Un cambio de idioma en
  mitad de una pelea puede actualizar la etiqueta sin perder la regla activa.
- La interfaz antigua puede consultar nombres de equipo mediante
  `WeaponProperties.GetDisplayName()`, pero la Equip Card activa sólo muestra
  sus iconos y no requiere traducir esos nombres.
- Los 12 enums de `Localization.Languages` resuelven una tabla activa. Las dos variantes de español comparten la misma; los diez idiomas restantes usan su catálogo revisado independiente.
- La herramienta temporal de 0.5.110 usa `Ctrl+F8` para recorrer los 12 idiomas
  reales sin depender del menú de opciones. Se desactiva con una sola constante
  antes de publicar.
- `TRANSLATION_REVIEW_TEMPLATE.md` es el formato recomendado para que el usuario
  entregue una traducción completa o correcciones puntuales por ID. La plantilla
  incluye sólo los 29 textos visibles, además de los 12 nombres en español, sus
  enums de Cuphead, el orden exacto de `Ctrl+F8` y nombres de archivo sugeridos.

## Idiomas que Cuphead puede detectar

El juego expone el idioma activo mediante `Localization.language` y notifica
cambios con `Localization.OnLanguageChangedEvent`. Su enumeración oficial
incluye:

| Valor interno | Idioma |
| --- | --- |
| `English` | Inglés |
| `French` | Francés |
| `Italian` | Italiano |
| `German` | Alemán |
| `SpanishSpain` | Español de España |
| `SpanishAmerica` | Español de América |
| `Korean` | Coreano |
| `Russian` | Ruso |
| `Polish` | Polaco |
| `PortugueseBrazil` | Portugués de Brasil |
| `Japanese` | Japonés |
| `SimplifiedChinese` | Chino simplificado |

Las dos variantes de español pueden compartir inicialmente el texto actual o
tener vocabulario distinto si se decide después.

### Política parcial ya implementada

El nombre grande del jefe usa la localización nativa de Cuphead. El subtítulo
del nivel usa el catálogo existente `BossEntry.Fight` tanto en
`SpanishSpain` como en `SpanishAmerica`, garantizando que todos los jefes tengan
texto. En los otros diez idiomas el subtítulo queda vacío y sólo se muestra el
nombre localizado del jefe. Esta decisión ya está cerrada; el resto de la
interfaz continúa pendiente de una estrategia de traducción completa.

## Textos que puede proporcionar el propio juego

Conviene reutilizar estas traducciones oficiales en lugar de mantener copias:

| Contenido | Fuente nativa prevista | Respaldo actual |
| --- | --- | --- |
| Nombre del jefe | clave `<Levels>WorldMap` de `Localization` | `BossEntry.Character` |
| Nombre/título del combate | clave `<Levels>` de `Localization` | `BossEntry.Fight` |
| Nombre de arma | `WeaponProperties.GetDisplayName(Weapon)` | `EquipmentEntry.Name` |
| Nombre de súper | `WeaponProperties.GetDisplayName(Super)` | `EquipmentEntry.Name` |
| Nombre de amuleto | `WeaponProperties.GetDisplayName(Charm)` | `EquipmentEntry.Name` |

`Nada` no corresponde a un equipo real y no se escribe en la Equip Card, por lo
que no forma parte del catálogo público pendiente.

## Inventario técnico de textos de tarjeta

Esta sección conserva también IDs de la interfaz antigua como referencia de
implementación. **No debe usarse como plantilla de traducción**; para eso está
`TRANSLATION_REVIEW_TEMPLATE.md`.

Los siguientes identificadores son propuestas estables. La columna “actual”
registra lo que aparece hoy, no una traducción definitiva.

| ID propuesto | Texto actual | Contexto |
| --- | --- | --- |
| `ui.brand` | `CUPHEAD · BOSS ROULETTE` | Título de la interfaz antigua |
| `ui.tagline` | `¡EL DESTINO DECIDE TU PRÓXIMO COMBATE!` | Subtítulo de la interfaz antigua |
| `ui.slot.weapon_a` | `ARMA A` / `TIRO A` | Primera arma |
| `ui.slot.weapon_b` | `ARMA B` / `TIRO B` | Segunda arma |
| `ui.slot.super` | `SÚPER` | Ranura de súper |
| `ui.slot.charm` | `AMULETO` | Ranura de amuleto |
| `ui.slot.challenge` | `RETO` | Ranura de reto |
| `ui.setting.difficulty` | `DIFICULTAD` | Ajuste de dificultad |
| `ui.setting.challenge` | `RETO` / `MODO FEO` | Activación de retos; falta decidir el nombre público |
| `ui.setting.auto_load` | `CARGA AUTOMÁTICA` / `CARGA AUTO` | Carga automática del combate |
| `ui.value.enabled` | `ACTIVADO` / `ACTIVADA` | Valor activo; hoy cambia por género gramatical |
| `ui.value.disabled` | `DESACTIVADO` / `DESACTIVADA` | Valor inactivo; hoy cambia por género gramatical |
| `ui.value.selected` | `SELECCIONADO` | Campo ya detenido |
| `ui.value.rolling` | `GIRANDO...` | Campo todavía girando |
| `ui.difficulty.easy` | `SIMPLE` | Dificultad fácil |
| `ui.difficulty.normal` | `NORMAL` | Dificultad normal |
| `ui.difficulty.hard` | `EXPERTO` | Dificultad difícil |
| `ui.action.spin` | `¡GIRAR!` | Botón principal |
| `ui.action.play` | `¡JUGAR!` | Cargar resultado manualmente |
| `ui.action.close` | `CERRAR` | Cerrar tarjeta antigua |
| `ui.action.open_roulette` | `ABRIR RULETA` | Prompt nativo del mapa |
| `ui.action.spin_again` | `VOLVER A GIRAR` | Prompt nativo después de un resultado |
| `ui.action.preparing` | `PREPARANDO COMBATE...` | Banda inferior |
| `ui.action.spinning` | `GIRANDO...` | Banda inferior |
| `ui.action.select_save` | `SELECCIONA UNA PARTIDA` | Banda inferior |
| `ui.challenge_prefix` | `RETO:` | Etiqueta persistente del combate |
| `ui.controls` | `F6 ABRIR/CERRAR · F7 GIRAR · CTRL+I SELECCIÓN FORZADA` | Ayuda de controles antigua |
| `ui.controls.controller_toggle` | `Gatillo izquierdo + Equip` | Descripción neutral del atajo de mando; sus etiquetas físicas son ZL+X, LT+Y y L2+Triángulo |

## Mensajes de estado internos o heredados

Estos estados todavía existen para controlar el flujo, pero sus frases sólo se
dibujan en la interfaz antigua. No forman parte de las traducciones pendientes.

| ID propuesto | Texto actual |
| --- | --- |
| `status.ready` | `PULSA ENTER PARA GIRAR` |
| `status.spinning` | `¡LA RULETA ESTÁ GIRANDO!` |
| `status.result_ready` | `¡RESULTADO LISTO!` |
| `status.result_loading` | `¡RESULTADO LISTO! PREPARANDO COMBATE...` |
| `status.save_required` | `SELECCIONA PRIMERO UNA PARTIDA GUARDADA` |
| `status.scene_loading` | `CUPHEAD YA ESTÁ CARGANDO OTRA ESCENA` |
| `status.load_failed` | `NO SE PUDO CARGAR. REVISA LOGOUTPUT.LOG` |

Cuando se implemente, `status` debe dejar de almacenar frases y guardar un ID o
enum. Actualmente la interfaz busca la palabra `PARTIDA` dentro del texto para
decidir otra etiqueta; esa lógica no funcionaría en otros idiomas.

## Nombres de retos

| ID estable propuesto | Texto actual |
| --- | --- |
| `challenge.no_dash` | `No Dash` |
| `challenge.no_mini_plane` | `No mini avión` |
| `challenge.mini_plane_only` | `Solo mini avión` |
| `challenge.no_bombs` | `No disparo bombas` |
| `challenge.no_peashooter` | `No disparo Peashooter` |
| `challenge.no_ex` | `No EX` |
| `challenge.black_and_white` | `Blanco y negro` |
| `challenge.none` | `Nada` |

Los retos ya usan `ModifierId`; traducir su presentación no modifica sus reglas
de gameplay. `challenge.none` permanece como respaldo interno, pero no se
muestra: el círculo queda vacío cuando los retos están desactivados.

## Configuración de BepInEx

Las claves y secciones del archivo de configuración deben permanecer estables
para no perder preferencias existentes:

- `Controles/AbrirCerrar`
- `Controles/Girar`
- `Juego/CargarAutomaticamente`
- `Juego/Dificultad`
- `Juego/Reto`
- `Juego/DemoraAntesDeCargar`

Sí podrían traducirse las descripciones visibles al crear el archivo:

- `Abre o cierra la ruleta.`
- `Inicia un giro.`
- `Carga el jefe al finalizar el giro.`
- `Dificultad usada por la ruleta: Easy, Normal o Hard.`
- `Activa los retos adicionales de la ruleta.`
- `Segundos entre el resultado final y la carga.`

Estas descripciones se fijan al iniciar el plugin; no es necesario que cambien
en caliente. Falta decidir si realmente se quieren localizar o si se mantiene
un único idioma para facilitar soporte técnico.

## Logs y mensajes técnicos opcionales

Los mensajes de BepInEx también son traducibles, pero no afectan la interfaz.
Incluyen carga de escenas, audio faltante, detección del DLC, fallos al instalar
parches Harmony y errores al preparar prompts. Recomendación pendiente: mantener
los diagnósticos técnicos en inglés o español estable para que sean fáciles de
buscar, y traducir sólo los avisos que ayudan directamente al jugador.

## Elementos que normalmente no deben traducirse

- `Gilomx Boss Roulette` y el GUID del plugin.
- Teclas `F6`, `F7`, `Enter`, `Esc` y combinaciones de teclado.
- Nombres de archivos, rutas, sprites y claves de configuración.
- Valores internos de `Weapon`, `Charm`, `Super`, `Levels` y retos.
- Nombres propios oficiales cuando Cuphead ya proporciona la forma localizada.

## Verificación visual restante

1. Recorrer los 12 idiomas con `Ctrl+F8` en tarjeta, mapa y HUD de reto.
2. Confirmar glifos y métricas de las fuentes nativas para Hangul, cirílico,
   japonés y chino simplificado en ventana y pantalla completa.
3. Los rótulos de configuración disponen de 360 unidades, sin salto de línea,
   desde 0.5.117. Revisar especialmente ruso, alemán y portugués de Brasil.
4. No modificar el español compartido al corregir ajustes visuales de otros
   idiomas.

## Decisiones reservadas para el usuario

- Qué idiomas se implementan primero o si se cubren los 12 a la vez.
- Si `MODO FEO` se traduce literalmente o se renombra como “Modo reto”.
- Qué redacción debe tener cada reto en cada idioma.
- Si `Boss Roulette` permanece como marca o se traduce.
- Si se traducen configuración y logs además de la interfaz.
