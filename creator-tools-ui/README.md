# Creator Tools UI

Panel web local de La Pichi Ruleta. React se compila a los tres archivos que el
servidor interno ya publica: `config.html`, `config.css` y `config.js`.

Las rutas `/config`, `/config/roulette` y `/config/interactions` entregan ese
mismo documento. La SPA conserva el shell y cambia únicamente la vista central;
`/config` abre Ruleta como sección inicial.

## Desarrollo

```powershell
npm install
npm run dev
npm run build
```

`npm run dev` usa el servidor del mod en `http://127.0.0.1:18081` para `/api`
y `/assets`. Para trabajar sin Cuphead, inicia primero el servidor simulado:

```powershell
node scripts/mock-server.mjs
```

El servidor simulado habilita los mini zepelines verde y morado, la zanahoria
teledirigida de La pandilla raíz, la semilla azul de Cagney y la luciérnaga
incendiada de Hosco y Tosco. Conserva los lotes mixtos en la cola para
revisar el catálogo compacto, la tabla de pruebas y sus estados. Cada artículo
nuevo debe estar presente tanto en esta tabla como en la prueba aleatoria del
runtime. En el mod real se preparan los prefabs nativos desde el mapa y se
habilitan en cualquier batalla o nivel de plataformas; no se usan recreaciones
portátiles aproximadas. Los previews de los zepelines se regeneran con
`tools/extract_native_zeppelin_previews.py`; el de la zanahoria, con
`tools/extract_native_homing_carrot_preview.py`; y el de la semilla, con
`tools/extract_native_cagney_homing_plant_preview.py`. El de la luciérnaga se
regenera con `tools/extract_native_frogs_firefly_preview.py`. Todos parten de
frames nativos.
El build ejecuta `scripts/validate-interaction-catalog.mjs` y falla si la lista
central del runtime, `interactionItems` y el servidor simulado dejan de coincidir.

## Reglas permanentes

- La aplicación es una SPA. `AppShell`, conexiones, stores y servicios viven
  por encima de las vistas y no se desmontan al cambiar de sección.
- Las funciones que deban sobrevivir al cierre del navegador pertenecen al mod,
  no a un componente React.
- Solo existen dos locales: español (`es`) e inglés (`en`). No se permiten
  textos visibles escritos directamente en componentes.
- Una función nueva debe incluir sus traducciones en ambos idiomas.
- Los IDs recibidos desde el mod son estables; el panel resuelve sus etiquetas.
- Los estilos reutilizan los tokens y componentes existentes. Las vistas no
  crean colores, espaciados ni controles paralelos para resolver casos locales.
- Los cambios visuales compartidos se hacen en el componente o token base.
- Los estados de conexión y las validaciones del mod nunca se ocultan.
- Los assets se sirven localmente; el panel no depende de CDNs.
- `assets/creator-tools/config.*` son salida compilada. El código fuente vive en
  este directorio.

## Estructura

- `src/components`: primitivas visuales compartidas.
- `src/config`: estado persistente y comunicación con el mod.
- `src/features`: composición y comportamiento de cada sección.
- `src/i18n` y `src/locales`: infraestructura y catálogos ES/EN.
- `src/styles`: tokens y reglas visuales del sistema.
