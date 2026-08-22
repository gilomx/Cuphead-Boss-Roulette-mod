# Creator Tools UI

Panel web local de La Pichi Ruleta. React se compila a los tres archivos que el
servidor interno ya publica: `config.html`, `config.css` y `config.js`.

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
