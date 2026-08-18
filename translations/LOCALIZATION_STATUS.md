# Estado de localización

Estado: **etapa cerrada, implementada y verificada el 2026-08-17**.

## Catálogo activo

- 29 IDs del catálogo anterior.
- 25 IDs aprobados en esta ronda: cinco retos nuevos y 20 textos visibles de
  Creator Tools.
- Un texto adicional, `creator.menu.logo`, integrado después para la nueva
  opción del overlay.
- Total actual de esta auditoría: **55 IDs visibles en 12 idiomas**.
- Los nombres de jefes, armas, supers y amuletos siguen procediendo de la
  localización oficial de Cuphead.

`ModLocalization.LabelReview.cs` aplica las 25 traducciones revisadas y
`ModLocalization.CreatorToolsBrand.cs` contiene la etiqueta Logo. Creator
Tools convierte el texto visible a mayúsculas para conservar el diseño nativo.

## Evidencia y archivo

- `PENDING_LABEL_LOCALIZATION_REVIEW.md` conserva literalmente las entregas
  originales. Su nombre es histórico; esas filas ya no están pendientes.
- `TRANSLATION_REVIEW_TEMPLATE.md` conserva el inventario cerrado de la ronda.
- `review_by_language/` conserva las entregas históricas anteriores sin
  reescribirlas.
- La revisión dentro del juego comprobó los doce idiomas, los anchos y fuentes,
  el centrado al cambiar de idioma y después de reiniciar, los retos y el
  overlay.

## Próxima ronda

No hay IDs nuevos definidos todavía. Cuando se acuerde otra localización:

1. Crear un inventario separado con sus IDs y contexto.
2. No volver a marcar como pendientes las 25 filas cerradas aquí.
3. Conservar el orden interno de idiomas:
   English, French, Italian, German, SpanishSpain, SpanishAmerica, Korean,
   Russian, Polish, PortugueseBrazil, Japanese y SimplifiedChinese.
4. Mantener separadas las entregas literales de las tablas activas hasta su
   aprobación.

## Fuera de alcance

Continúan excluidos los textos de la interfaz antigua, logs, la página interna
`/config`, configuración de BepInEx, símbolos de botones, escalas, porcentajes,
la URL local y nombres que ya proceden de Cuphead.
