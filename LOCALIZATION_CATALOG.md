# Catálogo de localización pendiente

Estado: **sólo planificación**. La versión actual del mod conserva todos sus
textos en español. Este documento no autoriza ni implementa traducciones; sirve
para decidir más adelante qué se traduce, a qué idiomas y con qué redacción.

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

## Textos que puede proporcionar el propio juego

Conviene reutilizar estas traducciones oficiales en lugar de mantener copias:

| Contenido | Fuente nativa prevista | Respaldo actual |
| --- | --- | --- |
| Nombre del jefe | clave `<Levels>WorldMap` de `Localization` | `BossEntry.Character` |
| Nombre/título del combate | clave `<Levels>` de `Localization` | `BossEntry.Fight` |
| Nombre de arma | `WeaponProperties.GetDisplayName(Weapon)` | `EquipmentEntry.Name` |
| Nombre de súper | `WeaponProperties.GetDisplayName(Super)` | `EquipmentEntry.Name` |
| Nombre de amuleto | `WeaponProperties.GetDisplayName(Charm)` | `EquipmentEntry.Name` |

`Nada` no corresponde a un equipo real y necesita traducción propia. Antes de
implementar debe verificarse visualmente qué clave de jefe corresponde al
nombre y cuál al título del combate; las rutas anteriores son las que usa la
interfaz interna de selección de nivel de Cuphead.

## Textos propios de la tarjeta

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

## Mensajes de estado

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
| `challenge.none` | `Nada` |

Antes de traducir retos es obligatorio separar su ID interno del texto visible.
La versión actual compara frases como `No mini avión` para aplicar las reglas;
traducirlas directamente rompería la funcionalidad. `ModifierId` debería ser
un enum y la traducción sólo una propiedad de presentación. El selector
temporal de pruebas también debe usar ese ID, no una frase.

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

## Requisitos técnicos para una implementación futura

1. Crear un servicio único, por ejemplo `ModLocalization`, con fallback a
   inglés y después al español actual.
2. Leer `Localization.language` al mostrar la interfaz.
3. Suscribirse a `Localization.OnLanguageChangedEvent` en `Awake()` y quitar la
   suscripción en `OnDestroy()`.
4. Guardar estados y retos como enums/IDs, nunca como texto localizado.
5. Actualizar en caliente la tarjeta, prompts del mapa y etiqueta del reto.
6. Obtener nombres de contenido desde Cuphead y usar los textos de
   `RouletteData` sólo como respaldo.
7. Seleccionar fuentes nativas compatibles con cada idioma. Esto es esencial
   para cirílico, coreano, japonés y chino simplificado.
8. Revisar anchos y saltos de línea: alemán, francés, ruso y portugués pueden
   ocupar más espacio que el español; CJK necesita fuentes y tamaños propios.

## Decisiones reservadas para el usuario

- Qué idiomas se implementan primero o si se cubren los 12 a la vez.
- Si `MODO FEO` se traduce literalmente o se renombra como “Modo reto”.
- Qué redacción debe tener cada reto en cada idioma.
- Si `Boss Roulette` permanece como marca o se traduce.
- Si se traducen configuración y logs además de la interfaz.
- Si español de España y español de América comparten textos.
