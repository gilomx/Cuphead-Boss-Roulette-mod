# Overlay de Ruleta en el diseñador

El overlay original de equipo y reto se configura en **Panel → Overlays →
Ruleta**. La entrada antigua del menú del juego se retiró; el menú conserva el
acceso al panel y la acción de volver.

Ruleta es una capa de los perfiles vertical (1080 × 1920) y horizontal
(1920 × 1080), junto a Farmeando taps, Batalla Molestosa y El chat elige.
Se puede mover, redimensionar, bloquear, ordenar, activar u ocultar y cambiar su
opacidad. El tamaño se ajusta al contenedor: al redimensionar la capa, el equipo,
el reto y el logo crecen o se reducen juntos sin deformarse. Conserva los controles
de equipo o reto arriba, alineación izquierda/centro/derecha, logo fuera del combate
y mantener visible o repetir la animación al reintentar. Los dos perfiles guardan
estos valores de forma independiente. Las imágenes se ajustan al espacio
disponible sin recortar los iconos.

El chat elige usa las imágenes originales del catálogo para el equipo y los
jefes. Los retos usan los PNG editados de `creator-tools/chat-chooses-modifiers`,
con nombres descriptivos y fondo transparente. Todos los artículos y jefes
llevan un borde circular fino de 2 px sobre un icono base de 72 px, que escala
con el conjunto. **Marco de la imagen** controla su color y transparencia;
el borde de las insignias de votos conserva su control independiente. Las rutas
antiguas con marco se resuelven al arte actual tanto al votar como en el resultado.

El selector antiguo de tamaño de iconos se retiró. Su valor se conserva al leer
escenas anteriores por compatibilidad y ya no interviene en la presentación.

Los cuatro overlays muestran sus textos en mayúsculas y tienen controles de **Texto** en el inspector. El estilo
predeterminado es el de la vista previa de Ruleta: sans serif, negrita (700),
blanco y sin sombra visible. Ruleta conserva esa fuente y grosor fijos; los
demás permiten cambiar fuente y grosor. Cada capa y perfil guarda color del
texto y color con opacidad de la sombra paralela. Los controles de desplazamiento
y desenfoque se retiraron; se conserva la forma de la sombra guardada.
La sombra empieza en `#00000000`; aumenta su opacidad para mostrarla. El tamaño
del texto sigue ajustándose al contenedor. Las escenas existentes conservan
sus colores y posiciones, y reciben los nuevos valores predeterminados.

Ruleta usa siempre texto web, también durante la partida, sin generar una imagen
con la tipografía de Cuphead. Las escenas que antes elegían la fuente nativa
adoptan la fuente común sin perder colores ni posiciones. El título y los iconos
ya no tienen una opacidad interna del 70%: la opacidad se controla desde el panel.

La simulación permite revisar equipo y reto, estado fuera del combate, oculto
y reintento. **Mostrar simulación en OBS** sigue siendo temporal y no cambia
el equipo de la partida. El logo aparece fuera del combate sólo si su opción
está activada.

Al seleccionar Ruleta, el lienzo permite previsualizarla aunque la capa esté
desactivada. **Repetir animación de entrada** vuelve a mostrar equipo y reto
desde el principio. Mostrar la simulación en OBS también permite revisar una
capa desactivada temporalmente; al apagar la simulación se recupera la
visibilidad configurada sin guardar cambios en su activación.

El servidor permite incrustar `/roulette-overlay` sólo desde el mismo origen,
igual que las demás capas. Esta política se comprueba en las pruebas HTTP del
servidor para que la vista previa del diseñador y la composición de OBS carguen
el componente sin bloqueos del navegador.

En el primer arranque se importan los valores del apartado `Creator Tools` del
`.cfg` del plugin, incluyendo activación y opacidad, a ambos perfiles. Se guardan
en `mx.gilomx.cuphead.bossroulette.overlay-composer.json` mediante la escritura
atómica existente. Si había una escena, su versión anterior queda en `.bak`.
Las demás capas conservan su configuración. Los valores antiguos del `.cfg`
no se borran y no sobrescriben las ediciones posteriores del diseñador. La
antigua vista previa del menú, que era temporal, no se importa.

Para OBS, usa **Copiar URL para OBS** del perfil elegido y reemplaza la dirección
de la fuente anterior por `/overlay/vertical` o `/overlay/horizontal` en el mismo
servidor local. La raíz `/` y `/index.html`, antes usadas por el overlay original,
muestran un aviso con el enlace al diseñador y los pasos para actualizar la URL.
El antiguo transporte `/ws` publica un estado desactivado; una fuente antigua
debe actualizarse o recargarse para ver el aviso. El componente nuevo recibe el
estado nativo del equipo y el reto a través del compositor, conservando las
animaciones y el texto del reto.

Validación sin iniciar Cuphead:

```powershell
node --test ./creator-tools-ui/scripts/test-roulette-overlay.mjs
dotnet run --project ./CreatorToolsRuntimeTests/CreatorToolsRuntimeTests.csproj
```
