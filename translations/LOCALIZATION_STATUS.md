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

Hay 28 etiquetas, valores, confirmaciones y estados del menú integrado que
deben traducirse a los 12 idiomas de Cuphead. Con los cinco retos nuevos, el
alcance público pendiente contiene **33 IDs**.

`TRANSLATION_REVIEW_TEMPLATE.md` contiene el inventario completo. Los
documentos activos de cada idioma deben actualizarse con estas filas cuando se
incorporen las nuevas revisiones.

## Fuera de alcance

Continúan excluidos los textos de la interfaz antigua, logs, configuración de
BepInEx, símbolos de botones, escalas, porcentajes, URL local y nombres de
jefes/equipo que ya proceden de la localización oficial de Cuphead.

## Cierre de esta etapa

Cuando estén aprobadas las doce entregas:

1. Incorporar los nuevos valores a `ModLocalization.cs` y Creator Tools.
2. Probar anchos, fuentes y saltos de línea en los doce idiomas.
3. Mover los 33 IDs del juego al catálogo aprobado de cada idioma.
4. Actualizar `review_by_language/` con las nuevas entregas finales.
5. Eliminar la separación temporal entre aprobado y pendiente.
