import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// Vite config. The dev server runs at http://localhost:5173, the same origin that
// Keycloak's `backoffice-web` client allows as a redirect URI and web origin.
// The API proxy lets the SPA call `/users` and `/auth/token` in dev without CORS issues
// during normal browsing (but CORS is still configured on the API as a fallback).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    server: {
      port: 5173,
      strictPort: true,
      // Allow Keycloak running in Docker to call back into the dev server from the host browser.
      host: true,
      proxy: {
        // Proxy API calls in dev so the SPA can use relative URLs.
        // The proxy target can be overridden via VITE_API_PROXY_TARGET in .env.local.
        '/api': {
          target: env.VITE_API_PROXY_TARGET || 'http://localhost:5157',
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/api/, ''),
        },
      },
    },
    preview: {
      port: 4173,
      strictPort: true,
      host: true,
    },
    resolve: {
      dedupe: ['react', 'react-dom'],
    },
  }
})
