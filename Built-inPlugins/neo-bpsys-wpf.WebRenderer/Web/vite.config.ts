import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { execFileSync } from 'node:child_process'

function gitCommit(): string {
  try {
    return execFileSync('git', ['rev-parse', '--short=12', 'HEAD'], { encoding: 'utf8' }).trim()
  } catch {
    return 'unknown'
  }
}

const clientBuildId = process.env.WEB_RENDERER_CLIENT_BUILD_ID
  ?? `${gitCommit()}-${new Date().toISOString().replace(/[-:.TZ]/g, '')}`

export default defineConfig({
  define: { __WEB_RENDERER_CLIENT_BUILD_ID__: JSON.stringify(clientBuildId) },
  plugins: [
    react(),
    {
      name: 'web-renderer-client-build-id',
      transformIndexHtml: html => html.replace(
        '<head>',
        `<head><meta name="web-renderer-client-build-id" content="${clientBuildId}" />`)
    }
  ]
})
