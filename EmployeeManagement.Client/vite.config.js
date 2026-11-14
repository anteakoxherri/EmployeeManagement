import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Proxy perdor profilin HTTPS ekzistues te API-se, pa ndryshime CORS.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/uploads/profile-pictures/': {
        target: 'https://localhost:7199',
        secure: false,
        changeOrigin: true,
      },
      '/api': {
        target: 'https://localhost:7199',
        // Vetem per certifikaten lokale te zhvillimit.
        secure: false,
        changeOrigin: true,
      },
    },
  },
})
