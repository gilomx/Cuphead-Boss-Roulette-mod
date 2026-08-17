# Estado de localización

Este índice separa temporalmente el contenido aprobado del contenido nuevo
pendiente. Cuando las doce revisiones nuevas estén terminadas, las tablas se
fusionarán de nuevo y esta separación se retirará.

## Catálogo aprobado actual

- 29 IDs visibles de la ruleta, prompts del mapa y HUD.
- 12 idiomas aprobados y activos.
- Las entregas originales permanecen archivadas sin cambios en
  `translations/review_by_language/`.
- `SpanishAmerica` conserva el override `SIN PEASHOOTER`; `SpanishSpain` usa
  `SIN DISPARO NORMAL`.

## Pendiente de localización

### Cinco retos nuevos

- `challenge.rgb_shift`
- `challenge.upside_down`
- `challenge.hp_one`
- `challenge.ink_rain`
- `challenge.half_damage`

### Creator Tools

El alcance se divide por superficie:

- 28 etiquetas, valores, confirmaciones y estados del menú integrado deben
  traducirse a los 12 idiomas de Cuphead;
- 16 textos de la página externa `/config` sólo tendrán inglés y español;
- 3 valores especiales de equipo visibles en `/config` (`Nada`, `Reliquia
  Maldita` y `Reliquia Divina`) también serán únicamente bilingües.

En total quedan **33 IDs del juego para los 12 idiomas** (cinco retos más 28 de
Creator Tools) y **19 IDs exclusivos de `/config` para inglés y español**.

`TRANSLATION_REVIEW_TEMPLATE.md` contiene el inventario completo. Los
documentos activos de cada idioma deben actualizarse con estas filas cuando se
incorporen las nuevas revisiones.

## Fuera de alcance

Continúan excluidos los textos de la interfaz antigua, logs, configuración de
BepInEx, símbolos de botones, escalas, porcentajes, URL local y nombres de
jefes/equipo que ya proceden de la localización oficial de Cuphead. Los tres
valores especiales sin equivalente nativo sí están dentro del alcance
pendiente.

## Cierre de esta etapa

Cuando estén aprobadas las doce entregas del juego y las dos variantes de
`/config`:

1. Incorporar los nuevos valores a `ModLocalization.cs` y Creator Tools.
2. Probar el contenido del juego en los doce idiomas y `/config` en inglés y
   español.
3. Mover los 33 IDs del juego al catálogo aprobado de cada idioma e incorporar
   los 19 IDs bilingües a la implementación de `/config`.
4. Actualizar `review_by_language/` con las nuevas entregas finales.
5. Eliminar la separación temporal entre aprobado y pendiente.
