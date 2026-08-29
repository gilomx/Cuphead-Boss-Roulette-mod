# Referencias nativas de la Equip Card de retos

Los PNG de `originales/` fueron extraídos como `Sprite` desde la instalación
local de Cuphead. Conservan resolución, orientación y canal alfa.

## Frente de la tarjeta

- `372_ch_equip_front.png`: frente inglés de Cuphead, 595×668 RGBA.
- `385_mm_equip_front.png`: frente inglés de Mugman, 595×668 RGBA.
- `353_ch_equip_front_no_text.png`: frente de Cuphead sin los cinco rótulos
  inferiores, 595×668 RGBA.
- `251_mm_equip_front_no_text.png`: frente de Mugman sin los cinco rótulos
  inferiores, 595×668 RGBA.
- `6581939381173291130_equip_icon_empty.png`: glifo vacío que el juego coloca
  como una capa separada, 80×80 RGBA.
- `1592_ch_equip_front_LOC_chaos.png` y
  `902_mm_equip_front_LOC_chaos.png`: acabado/ruido de la banda inferior usado
  en los once idiomas no ingleses; no contienen traducciones.
- `377_generic_equip_front_title_noise.png`: textura frontal común.

Cuphead usa el frente inglés con `Shot-A`, `Shot-B`, `Super`, `Charm` y `List`
ya impresos. Para los demás idiomas dispone de los dos fondos `no_text` y
coloca los cinco rótulos como componentes de texto separados, traducidos por
`LocalizationHelper`. El logotipo superior `Cuphead/Mugman Equip Card` y
`1P/2P` permanece pintado en todos los fondos como parte del diseño.

El disco negro de **List** sí está pintado tanto en los fondos ingleses como en
los fondos `no_text`. El glifo `equip_icon_empty` es otra capa y no es el origen
de ese relleno negro. Para el mod, la entrega más flexible son las dos versiones
`no_text` corregidas, una roja y una verde, manteniendo exactamente el lienzo de
595×668; los cinco rótulos pueden quedar siempre como texto localizable.

## Reverso y cuadrículas

- `381_ch_equip_back.png`: reverso base de Cuphead, 595×668 RGBA.
- `695_mm_equip_back.png`: reverso base de Mugman, 595×668 RGBA.
- `968_generic_equip_back_9_icons.png`: cuadrícula transparente de 9 huecos
  que utiliza ahora la pantalla de retos, 595×668 RGBA.
- `1187_generic_equip_back_6_icons.png`: cuadrícula nativa de 6 huecos.
- `1691_generic_equip_back_super_icons.png`: cuadrícula nativa de súperes.
- `1434_ch_equip_back_title_noise.png` y
  `824_mm_equip_back_title_noise.png`: ruido/textura del encabezado.

El selector usa una capa transparente de 595×668 con 12 huecos en filas 5-4-3.
**Vacío** no aparece dentro de la cuadrícula; se mantiene la acción nativa de
desequipar.

La ilustración por sí sola no crea más opciones: el selector nativo sólo trae
9 objetos interactivos en esta cuadrícula. El mod clona los 3
objetos restantes en tiempo de ejecución, los coloca sobre los centros del
nuevo diseño y construye su navegación; no hace falta editar un prefab de
Unity para conseguirlo.

## Entrega recomendada

1. Frente Cuphead `no_text` corregido: PNG RGBA, 595×668.
2. Frente Mugman `no_text` corregido: PNG RGBA, 595×668.
3. Cuadrícula nueva: PNG RGBA transparente, 595×668, con 12 huecos.
4. Si los centros no son círculos fáciles de detectar, una lista de
   coordenadas `(x, y)` medida desde la esquina superior izquierda.

El catálogo de nombres, compatibilidad, descripciones e iconos está en
`catalogo-retos.md`. El inventario completo de textos por idioma y las reglas
para el siguiente agente están en `localizacion-equip-card.md`.
