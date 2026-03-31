import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: './',
  build: {
    outDir: '../BaguetteDesign.Api/wwwroot',
    emptyOutDir: true,
  },
})
