import { createRoot } from 'react-dom/client'
import { WebRendererApp } from './app/WebRendererApp'
import './styles.css'

declare const __WEB_RENDERER_CLIENT_BUILD_ID__: string

console.info(`[Web Renderer] client build ${__WEB_RENDERER_CLIENT_BUILD_ID__}`)
createRoot(document.getElementById('root')!).render(<WebRendererApp />)
