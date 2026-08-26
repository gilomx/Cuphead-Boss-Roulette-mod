# Catálogo base de regalos de TikTok

`catalog.json` es la copia offline inicial que usará Creator Tools para
presentar regalos y, más adelante, guardar reglas por `giftId`. El snapshot
`2026-08-26.1` contiene 43 regalos observados y sus imágenes locales.

El registro `198895` y su imagen se excluyeron porque su nombre pertenecía a
otro idioma. Los nombres oficiales globales que ya aparecieron así en TikTok,
como `TikTok Universe`, permanecen sin traducir.

## Contrato actual

- `schemaVersion` cambia sólo cuando cambia la estructura que consume el mod.
- `catalogVersion` identifica actualizaciones normales del contenido.
- `giftId` siempre es texto y es la identidad estable del regalo.
- `coinsPerUnit` procede del campo `diamondCount` del export de mantenimiento.
- `sourceGiftType` se conserva como dato opaco del proveedor; no se usa todavía
  para decidir reglas o rachas.
- `imagePath` siempre apunta a una imagen incluida junto al catálogo.
- `sourceImageUrl` y `firstSeenAt` se conservan para auditoría.

Este snapshot todavía no implica conexión real con TikFinity ni evaluación de
reglas. El contrato de eventos debe añadir `giftId`, datos de racha y
deduplicación antes de despachar interacciones reales.

## Repetir la importación

Extrae un ZIP generado por la herramienta de mantenimiento y ejecuta:

```powershell
node .\tools\import_tiktok_gift_catalog.mjs `
  <directorio-extraido> `
  --catalog-version=<version> `
  --exclude=<giftId>
npm.cmd run validate:gifts --prefix creator-tools-ui
```

El build del panel ejecuta también esta validación automáticamente.
