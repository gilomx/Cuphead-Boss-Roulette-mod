import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "/",
  plugins: [react()],
  build: {
    outDir: "../assets/creator-tools",
    emptyOutDir: false,
    sourcemap: false,
    rollupOptions: {
      input: "config.html",
      output: {
        entryFileNames: "config.js",
        chunkFileNames: "config-[name]-[hash].js",
        assetFileNames: (asset) =>
          asset.names.some((name) => name.endsWith(".css"))
            ? "config.css"
            : "config-[name]-[hash][extname]",
      },
    },
  },
  server: {
    proxy: {
      "/api": "http://127.0.0.1:18081",
      "/assets": "http://127.0.0.1:18081",
    },
  },
});
