# Ideas para versiones futuras

Este documento guarda propuestas que todavía no forman parte del mod. No deben
tratarse como funciones terminadas ni activarse en una versión pública sin sus
pruebas correspondientes.

## Dos Ms. Chalice en cooperativo y variante morada

### Comportamiento observado

La ruleta aplica el mismo resultado de amuleto a los dos jugadores. Si ambos
reciben la Galletita Astral, el código nativo de `Level.CreatePlayers()` detecta
las dos copias de `charm_chalice` y activa `BlockChaliceCharm` aleatoriamente
para uno de los jugadores. Como consecuencia:

- Un jugador se transforma en Ms. Chalice.
- El otro conserva el cuerpo de Cuphead o Mugman.
- En niveles de avión puede aparecer un estado híbrido: el jugador bloqueado
  conserva su personaje, pero el mod puede asignarle los disparos de avión de
  Chalice porque actualmente los resuelve desde el resultado global de la
  ruleta y no desde `PlayerStatsManager.isChalice` de cada jugador.

El bloqueo de la segunda transformación es una regla nativa; el estado híbrido
es un caso límite de la integración actual del mod.

### Propuesta

Investigar una modalidad opcional en la que los dos jugadores sean realmente
Ms. Chalice y el segundo utilice una paleta morada para distinguirse.

La parte visual podría resolverse clonando el material de los renderers del
segundo jugador y aplicando una sustitución de paleta mediante shader. Cuphead
incluye `PlayerRecolorHandler` y el parámetro `_RecolorFactor`, pero esa ruta
está vinculada a filtros visuales y no constituye una variante morada lista
para usar.

### Investigación necesaria

- Anular `BlockChaliceCharm` únicamente en combates iniciados por la ruleta.
- Confirmar `isChalice` individualmente después de `PlayerStatsManager.LevelInit()`.
- Revisar `PlayerManager.playerWasChalice` y los estados persistentes entre
  reintentos.
- Revisar el Palacio de Dados, que conserva un único `CHALICE_PLAYER`.
- Probar movimiento, doble salto, parry con dash, supers, EX, revivir,
  introducciones, derrotas y resultados.
- Probar las variantes de avión y evitar asignar armamento desde una condición
  global cuando los personajes sean diferentes.
- Recolorear cuerpo, avión, fantasma de reanimación, efectos, supers y cualquier
  retrato que necesite distinguir al segundo jugador.
- Crear materiales por jugador; no modificar un `sharedMaterial` que pueda
  recolorear también a P1.
- Restaurar sin residuos todos los cambios al volver al mapa.

Antes de intentar la duplicación completa también puede corregirse solamente
el estado híbrido, usando el `isChalice` real de cada jugador para elegir su
armamento de avión.

## Reto de desfase RGB inspirado en Cagney — HECHO

Estado: funcionalidad terminada y aceptada en 0.5.129. Queda desactivada por
decisión del proyecto: `EnableRgbShiftChallenge` y
`ForceRgbShiftChallengeForTesting` están en `false`, por lo que no aparece en
la ruleta. Para publicarla después se activa sólo el primer interruptor. El arte
final de tres frames y el nombre localizado siguen pendientes como presentación,
no como desarrollo funcional.

### Idea

Cagney Carnation aplica al recibir ciertos golpes un efecto visual parecido a
un desfase de los canales rojo, verde y azul. Investigar su implementación
nativa para convertirla en un reto reutilizable en otros jefes.

Nombre provisional: `Desfase RGB`.

### Objetivo visual

- Mantener un desplazamiento cromático legible durante el combate sin ocultar
  la acción.
- Usar, si es viable, el material, shader o componente nativo de Cagney en vez
  de reconstruir el efecto desde cero.
- Añadir entrada y salida suaves para evitar un corte visual brusco.
- Mantener el efecto fuera del mapa y limpiarlo al ganar, abandonar o volver a
  intentar cuando corresponda.

### Investigación necesaria

- Identificar exactamente qué evento, componente y material activa Cagney.
- Registrar propiedades, intensidad, duración y orden de render del efecto.
- Confirmar si afecta la cámara completa o sólo determinados renderers.
- Comprobar compatibilidad con pausa, derrota, iris, calificación y transiciones
  especiales.
- Definir cómo se combina con el reto `Blanco y negro`; evitar dos filtros que
  sobrescriban el mismo material o dejen la pantalla en un estado incorrecto.
- Medir el costo de render. La implementación preferida debe reutilizar el
  postproceso nativo y evitar procesamiento de imagen en CPU.
- Probar niveles terrestres, niveles de avión, P1 y cooperativo.

La implementación terminada reutiliza `ChromaticAberrationFilmGrain`, espera los
mismos 1.5 segundos de apertura normal que `Blanco y negro` y entra durante
1.25 segundos. La configuración aceptada usa amplitud base 32 y una
trayectoria sinusoidal 2D: velocidad vertical 10 y horizontal 7.3, con 70% de
amplitud horizontal. Rojo usa 120% de fuerza, verde 60% y azul 90%. También
controla `BlurGamma` sin corutinas: conserva el ciclo nativo de 2.2 segundos a
70% de fuerza. El placeholder es un único PNG transparente de 80 × 80 con el
texto `RGB`; el arte final animado sigue pendiente.

## Reto de fijado permanente

### Idea

Crear un reto terrestre que mantenga activa la acción nativa de fijar la
dirección durante toda la pelea.

Nombre provisional: `Fijado permanente`.

Se contemplan dos variantes de balance:

1. Fijado permanente con dash disponible como opción de desplazamiento.
2. Fijado permanente combinado con `No dash` para una versión extrema.

### Reglas propuestas

- Aplicarlo únicamente durante el combate; nunca simular la tecla globalmente
  ni afectar mapas, ruleta, pausa o menús.
- Intervenir la consulta de la acción de fijado dentro del controlador del
  jugador, conservando entradas independientes para P1 y P2.
- Mantener la posibilidad de apuntar en las direcciones que permita el juego
  mientras el personaje está fijado.
- Limitarlo inicialmente a niveles terrestres. En avión, los mismos botones
  pueden tener funciones diferentes y necesitan una investigación separada.
- Limpiar el estado al morir, reintentar, ganar o abandonar.

### Balance y compatibilidad

La combinación con `No dash` puede volver imposibles algunos jefes. Antes de
añadirla al catálogo aleatorio se necesita una lista de compatibilidad por
nivel y una prueba completa de cada fase. Si no existe una selección segura de
jefes, debe permanecer como reto de desarrollo o descartarse la variante
combinada.

Probar como mínimo:

- Salto, agacharse, parry, EX y súper.
- Cambios de dirección y apuntado diagonal.
- Reintento después de derrota.
- Dos jugadores con controles independientes.
- Jefes que exigen atravesar rápidamente la pantalla.
- Interacción con otros bloqueos de movimiento del propio nivel.

## Opción para jugar como Cáliz

### Idea

Agregar a los ajustes de la ruleta una opción persistente llamada
`Jugar como Cáliz`. Cuando esté activada, el resultado del espacio de amuleto
será siempre la Galletita Astral, de modo que el jugador entre a cada combate
como Ms. Chalice sin depender del resultado aleatorio.

La ruleta y el HUD deben mostrar la Galletita Astral como el resultado real;
no debe aplicarse como una sustitución oculta después del giro. Mientras esta
opción esté activa, los demás amuletos y el resultado vacío quedan excluidos
de ese espacio.

### Comportamiento propuesto

- Guardar la preferencia junto con dificultad, reto y carga automática.
- Si se cambia después de haber girado, invalidar el resultado pendiente y
  volver a mostrar `¡Girar!`, igual que con los demás ajustes.
- Mantener el préstamo temporal de equipamiento: al ganar o abandonar, se
  restaura el amuleto que el jugador tenía antes de usar la ruleta.
- Aplicar correctamente el armamento de avión de Cáliz cuando corresponda.
- No exigir que la Galletita Astral esté comprada o equipada previamente,
  siguiendo la regla actual de usar el catálogo completo.

### Cooperativo e investigación necesaria

Definir si la opción afecta a ambos jugadores o solamente a quien abrió la
ruleta. Si se aplica a los dos, debe resolverse primero el bloqueo nativo de
dos Ms. Chalice y el posible estado híbrido descrito en la sección
`Dos Ms. Chalice en cooperativo y variante morada`.

Probar P1, P2, reintentos, niveles terrestres, niveles de avión, Palacio de
Dados, supers, HUD y restauración del equipamiento al volver al mapa.

## Criterio general para incorporar estas ideas

Cada idea debe implementarse detrás de un selector temporal, documentarse en
`PROJECT_HANDOFF.md` y `CHANGELOG.md`, probarse con P1 y cooperativo, y dejar el
selector desactivado antes de publicar una versión normal.
