import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// /assets/{token} is reserved for the host-authorized bpui resources.
// Keep Vite's own hashed files in a distinct URL namespace.
export default defineConfig({ plugins: [react()], build: { assetsDir: 'static' } })
