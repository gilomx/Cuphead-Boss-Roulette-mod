# Catálogo de retos para Equip Card

## Alcance

La lista aprobada para la Equip Card contiene **12 retos visibles**. El reto
`NoMiniPlane` deja de aparecer como opción independiente, pero su regla se
conserva internamente para los retos que bloquean el miniavión en niveles de
avión.

## Orden aprobado

| # | Nombre visible (español de América) | `ModifierId` visible | Compatibilidad | Descripción aprobada | Icono, frame 01 |
|---:|---|---|---|---|---|
| 1 | BLANCO Y NEGRO | `BlackAndWhite` | Ambos | La imagen del combate pasa a blanco y negro; los controles y las colisiones no cambian. | `assets/modifiers/blacknwhite_01.png` |
| 2 | Mamá escucho borroso | `RgbShift` | Ambos | La imagen del combate sufre un desfase RGB y un desenfoque pulsante; los controles y las colisiones no cambian. | `assets/modifiers/rgb_01.png` |
| 3 | Lluvia de tinta | `InkRain` | Ambos | Caen gotas de tinta. Si tocan a un jugador, manchan y oscurecen la pantalla temporalmente, pero no infligen daño. | `assets/modifiers/inkrain_01.png` |
| 4 | Volteada de cabeza | `UpsideDown` | Ambos | La imagen del combate gira 180°; los controles, la física y las colisiones no cambian. | `assets/modifiers/upside_down_01.png` |
| 5 | Una vida y te callas | `HpOne` | Ambos | Cada jugador queda limitado a 1 HP; las curaciones y el escudo del Súper II de Ms. Chalice se anulan. | `assets/modifiers/hp1_01.png` |
| 6 | Modo tieso | `StiffMode` | Ambos | En combates terrestres, el fijado se mantiene mientras tocas el suelo y el dash queda bloqueado; puedes dirigir los saltos. En niveles de avión, no puedes transformarte en miniavión. | `assets/modifiers/locked_01.png` |
| 7 | Disparos rebajados | `HalfDamage` | Ambos | Todos tus ataques infligen un 50 % menos de daño; el daño que recibes no cambia. | `assets/modifiers/halfdamage_01.png` |
| 8 | NO DASH | `NoDash` | Ambos | En combates terrestres, el dash queda bloqueado. En niveles de avión, no puedes transformarte en miniavión. | `assets/modifiers/nodash_01.png` |
| 9 | NO EX | `NoEx` | Ambos | Los ataques EX quedan bloqueados; los súperes siguen disponibles. | `assets/modifiers/noex_01.png` |
| 10 | SOLO BALAS DE MINIAVIÓN | `MiniPlaneOnly` | Avión | Puedes cambiar de tamaño, pero dañar a un enemigo con un disparo grande, una bomba o un EX reinicia el intento. Los súperes sí están permitidos. Solo funciona en niveles de avión. | `assets/modifiers/mini_01.png` |
| 11 | NO DISPARO BOMBAS | `NoBombs` | Avión | Solo puedes usar el disparo principal; las bombas quedan bloqueadas. Solo funciona en niveles de avión. | `assets/modifiers/nobombs_01.png` |
| 12 | SIN PEASHOOTER | `NoPeashooter` | Avión | Solo puedes usar bombas; el disparo principal queda bloqueado. Solo funciona en niveles de avión. | `assets/modifiers/nopeashooter_01.png` |

## Reglas de resolución aprobadas

- `NoMiniPlane` no debe mostrarse ni sortearse como reto independiente.
- Si el reto visible es `NoDash` y el nivel usa controles de avión, se aplica
  internamente la regla de `NoMiniPlane`. La interfaz conserva el nombre y el
  icono de `NO DASH`.
- Si el reto visible es `StiffMode` y el nivel usa controles de avión, también
  se aplica internamente la regla de `NoMiniPlane`. La interfaz conserva el
  nombre y el icono de `Modo tieso`.
- La adaptación de `Modo tieso` ya no se describe como un caso especial de Rey
  Dado; funciona de la misma manera en cualquier nivel de avión.
- Los tres retos colocados al final sólo se aplican en niveles de avión.

## Acción para desequipar

Los 12 renglones anteriores son las únicas opciones visibles. **Vacío** no
forma parte de la cuadrícula: se conserva la acción nativa de desequipar y el
catálogo interno mantiene `ModifierId.None` sólo para representar la ranura sin
reto.

La Equip Card no muestra la columna de compatibilidad como texto `AMBOS` o
`AVIÓN`; se conserva aquí como referencia técnica. La cláusula de avión de cada
descripción aplicable se presenta en naranja rojizo dentro del juego.

## Fuente de verdad

- IDs, compatibilidad y rutas de icono: `RouletteData.cs`,
  `RouletteData.Modifiers`.
- Nombres base y resolución por idioma: `ModLocalization.cs`,
  `ModLocalization.ModifierName`.
- Nombres efectivos revisados de los retos nuevos:
  `ModLocalization.LabelReview.cs`, `ApplyApprovedLabelReviewTexts`.
- Descripciones localizadas: `ModLocalization.ChallengeDescriptions.cs`.
- Reglas de juego: `Plugin.cs`, `HpOneChallenge.cs`, `RgbShiftChallenge.cs`,
  `UpsideDownChallenge.cs` e `InkRainChallenge.cs`.
