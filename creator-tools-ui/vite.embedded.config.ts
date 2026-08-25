import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "/",
  plugins: [
    react(),
    {
      name: "creator-tools-spa-routes",
      configureServer(server) {
        server.middlewares.use((request, _response, next) => {
          const mutableRequest = request as typeof request & { url?: string };
          const url = mutableRequest.url ?? "";
          const queryIndex = url.indexOf("?");
          const pathname = queryIndex >= 0 ? url.slice(0, queryIndex) : url;
          const query = queryIndex >= 0 ? url.slice(queryIndex) : "";
          const isConfigRoute = pathname === "/config" ||
            pathname === "/config/" ||
            pathname.startsWith("/config/roulette") ||
            pathname.startsWith("/config/interactions") ||
            pathname.startsWith("/config/pesky");
          const isDashboardRoute = pathname === "/dashboard" ||
            pathname === "/dashboard/" ||
            pathname === "/dashboard.html";
          if (isConfigRoute || isDashboardRoute) {
            mutableRequest.url = `/config.html${query}`;
          }
          next();
        });
      },
    },
  ],
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
