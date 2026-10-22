import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: { proxy: {
    '/api': 'http://localhost:5234',
    '/health': 'http://localhost:5234',
    '/hubs': { target: 'http://localhost:5234', ws: true },
  } },
})
