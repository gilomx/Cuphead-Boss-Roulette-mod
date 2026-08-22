# Panel de configuración: comportamiento y reglas

Este documento es el contrato de producto y desarrollo del panel React servido
en `/config`. Complementa el README técnico de `creator-tools-ui`.

## Comportamiento actual

- La barra lateral y sus proveedores permanecen montados. Cambiar de sección
  sustituye únicamente la vista central, sin navegar a otra página ni reiniciar
  conexiones que deban seguir funcionando.
- El indicador bajo el logo es global para toda la aplicación. Su estado
  inicial es `Conectando con Cuphead`; después usa `Cambios guardados`,
  `Guardando cambios`, `Esperando confirmación` o el estado de error. No
  describe solamente el forzado de la ruleta.
- Los seis selects de forzado comienzan en la primera opción compatible. Tiro 2
  omite el valor actual de Tiro 1 para evitar duplicados. Todas las opciones
  `Nada` usan `assets/creator-tools/empty.png`, el mismo recurso del overlay.
- El área de forzado conserva espacio inferior equivalente al menú de un select
  para que las últimas listas puedan abrirse sin quedar cortadas.
- Los retos usan los iconos estáticos de `assets/creator-tools/modifiers`,
  compartidos con el Stream Overlay. Pulsarlos cambia su elegibilidad y guarda
  la lista en `Juego/RetosDesactivados` dentro de la configuración de BepInEx.
- Desactivar un reto sólo lo excluye del resultado aleatorio final. Todos los
  retos compatibles continúan recorriendo la animación mientras gira la
  ruleta.
- Siempre debe quedar al menos un reto activo de cada tipo: `plane`, `ground` y
  `both` (Avión, Tierra y Ambos). El último activo queda bloqueado en la UI y el
  servidor vuelve a validar la regla. Al cargar una configuración antigua que
  dejó una categoría vacía, el mod reactiva su primer reto disponible.
- El forzado puede seleccionar un reto aunque se haya excluido del resultado
  aleatorio: la exclusión configura el sorteo normal, no elimina contenido del
  catálogo ni de las herramientas de grabación.
- El encabezado principal comienza aproximadamente a media altura del logo para
  conservar la relación visual entre navegación y contenido.

## Contrato con el mod

`GET /api/config` entrega el catálogo disponible, el resultado forzado y, para
cada reto, los campos `enabled` y `canDisable`. El panel realiza cambios con
`GET /api/config/set` y mantiene el estado optimista hasta que una lectura
posterior confirma el valor guardado.

La validación importante siempre se repite en C#. React puede impedir una
interacción inválida por ergonomía, pero no es la autoridad para decidir qué
resultados puede producir la ruleta.

## Reglas permanentes

- La aplicación es una SPA. `AppShell`, conexiones, stores y servicios viven
  por encima de las vistas y no se desmontan al cambiar de sección.
- Los elementos compartidos —logo, navegación, indicador global, selector de
  idioma y proveedores— pertenecen al shell; una sección no los recrea.
- Las funciones que deban sobrevivir al cierre del navegador pertenecen al mod,
  no a un componente React.
- El panel sólo tiene dos locales: español (`es`) e inglés (`en`). No se
  permiten textos visibles escritos directamente en componentes.
- Una función nueva debe incluir sus traducciones en ambos idiomas.
- Los IDs recibidos desde el mod son estables; el panel resuelve sus etiquetas.
- El estado inicial de un control debe ser explícito y venir del mod. En los
  selects de catálogo, el valor predeterminado es la primera opción válida.
- Los estilos reutilizan los tokens y componentes existentes. Las vistas no
  crean colores, espaciados ni controles paralelos para resolver casos locales.
- Los cambios visuales compartidos se hacen en el componente o token base.
- Los recursos que ya cumplen una función en el overlay se reutilizan en el
  panel; no se mantienen copias visuales distintas del mismo icono.
- Los estados de conexión y las validaciones del mod nunca se ocultan.
- Una restricción de negocio se implementa en el mod y se refleja en la UI; no
  se confía únicamente en botones deshabilitados o estado local del navegador.
- Los assets se sirven localmente; el panel no depende de CDNs.
- `assets/creator-tools/config.*` son salida compilada. El código fuente vive en
  `creator-tools-ui`.

## Estructura

- `src/components`: primitivas visuales compartidas.
- `src/config`: estado persistente y comunicación con el mod.
- `src/features`: composición y comportamiento de cada sección.
- `src/i18n` y `src/locales`: infraestructura y catálogos ES/EN.
- `src/styles`: tokens y reglas visuales del sistema.
