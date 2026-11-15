// File: vite.config.ts
// Purpose: Vite build/dev-server configuration.
// Logic/Approach: The dev server is pinned to port 5173 — not because
// that's Vite's default (it is), but because the Api's CORS policy
// (VideoPlatform.Api/Program.cs, `Frontend:Origin`) and the refresh-token
// cookie's origin checks are both hardcoded to `http://localhost:5173` by
// default. Pinning it here means that stays true even if something else on
// the machine is already using 5173 (Vite would otherwise silently move to
// 5174, and every request would start failing CORS for a confusing reason).
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    strictPort: true,
  },
});
