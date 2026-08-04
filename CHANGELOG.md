# Historial de cambios

Este documento resume los cambios funcionales de Gilomx Boss Roulette. Las
versiones corresponden al número mostrado por BepInEx al cargar el mod.

## 0.5.48 — 2026-08-04

### Reto `Blanco y negro`

- Se añadió un reto compatible con niveles normales y de avión que utiliza el
  filtro blanco y negro nativo de Cuphead durante el combate.
- Cada intento comienza a color, espera 1.5 segundos y reduce suavemente la
  saturación del fotograma visible durante 1.25 segundos. Al finalizar queda
  activo el filtro blanco y negro original del juego.
- La transición utiliza un AssetBundle de 5 KB compilado con Unity 2017.4.9f1;
  el proyecto reproducible se conserva en `tools/unity-shader`.
- Se corrigió la orientación vertical de la textura en Direct3D mediante
  `_FlipY`, evitando que el combate aparezca de cabeza.
- El filtro se aplica sin modificar ni guardar la preferencia visual real del
  jugador y desaparece al ganar o abandonar el nivel.
- El reto conserva el filtro al perder y volver a intentar.
- Temporalmente utiliza `modifiers/blackandwhite.png` como icono provisional.
- Se retiró la selección forzada de `Blanco y negro` después de las pruebas.

## 0.5.47 — 2026-08-03

### Controles con mando

- Se añadió navegación completa mediante cruceta o stick usando las acciones
  nativas de menú de Cuphead.
- El botón de confirmar ahora permite cambiar opciones, girar la ruleta y
  seleccionar `JUGAR`.
- El botón de cancelar cierra la ruleta mediante el mismo flujo seguro de `Esc`.
- `ZR`, `RT` o `R2` permite volver a girar cuando la carga automática está
  desactivada y ya existe un resultado.
- El gatillo derecho utiliza detección por pulsación para evitar giros repetidos
  mientras permanece presionado.

## 0.5.46 — 2026-08-03

### Equipamiento temporal

- La ruleta puede seleccionar el catálogo completo disponible del juego base y
  del DLC, aunque el objeto todavía no haya sido comprado en la partida.
- Se guarda el equipamiento anterior de ambos jugadores antes de entrar al
  combate.
- El equipamiento de la ruleta se conserva al perder y volver a intentar.
- El equipamiento original se restaura al ganar o abandonar el nivel.
- En Rey Dado se mantiene el resultado durante los combates internos y sólo se
  restaura después de completar el enfrentamiento principal o abandonar la
  partida.
- El inventario y las compras de la partida no se modifican.

## 0.5.45 — 2026-08-03

### Ajuste de `Solo mini avión`

- El reto permite los disparos del mini avión y el súper.
- Los disparos del avión grande, las bombas y los ataques EX continúan
  reiniciando el nivel.

## 0.5.44 — 2026-08-03

### Apertura con mando

- Se añadió el atajo de gatillo izquierdo más el botón de Equip Card:
  `ZL + X`, `LT + Y` o `L2 + Triángulo`.
- La combinación funciona para cualquiera de los dos jugadores.
- La Equip Card original queda bloqueada únicamente mientras se utiliza la
  combinación de la ruleta y vuelve a funcionar normalmente al cerrarla.

## 0.5.43 y anteriores

- La ruleta sólo puede abrirse al caminar libremente por el mapa.
- Se recreó la interfaz a partir de la Equip Card de Cuphead, con navegación por
  filas, animaciones, sonidos y etiquetas nativas.
- Se añadieron dificultad, `RETO` y carga automática como opciones persistentes.
- Los retos se muestran y aplican únicamente durante el combate y se limpian al
  ganar o abandonar el nivel.
- Se añadieron restricciones para `No Dash`, `No mini avión`, `Solo mini avión`,
  `No disparo bombas`, `No disparo Peashooter` y `No EX`.
- Se añadió detección del DLC para excluir contenido que la instalación no puede
  cargar.
- Se añadieron la carga directa del jefe seleccionado y las etiquetas nativas
  `ABRIR RULETA` y `VOLVER A GIRAR`.
