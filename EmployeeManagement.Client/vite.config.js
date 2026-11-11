import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Konfigurimi i Vite per zhvillimin/build-in e React: https://vite.dev/config/
// Plugin-i aktivizon mbeshtetjen React; ky skedar nuk konfiguron JWT ose databazen e API-se.
export default defineConfig({
  plugins: [react()],
})
