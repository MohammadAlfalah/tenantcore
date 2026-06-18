import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// In dev (`npm run dev`) the app calls relative `/api/*` URLs, which Vite proxies to the backend.
// In Docker, nginx serves the built app and proxies `/api/*` to the api container instead, so the
// frontend code never needs to know the backend's address.
const apiTarget = process.env.VITE_API_PROXY_TARGET ?? "http://localhost:5048";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    host: true,
    proxy: {
      "/api": {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
});
