import { useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import './styles.css'

type Health = { hostVersion: string; pluginVersion: string; ipcStatus: string }

function App() {
  const [health, setHealth] = useState<Health | null>(null)
  const [connection, setConnection] = useState('Connecting')
  useEffect(() => {
    const refresh = () => fetch('/health').then(response => response.json()).then(setHealth).catch(() => setConnection('Disconnected'))
    refresh()
    const scheme = location.protocol === 'https:' ? 'wss' : 'ws'
    const socket = new WebSocket(`${scheme}://${location.host}/ws`)
    socket.onopen = () => setConnection('Connected')
    socket.onmessage = event => setHealth(JSON.parse(event.data))
    socket.onclose = () => { setConnection('Disconnected'); refresh() }
    socket.onerror = () => setConnection('Disconnected')
    return () => socket.close()
  }, [])
  return <main><h1>Web Renderer Experimental</h1><dl><dt>Connection status</dt><dd>{connection}</dd><dt>Host version</dt><dd>{health?.hostVersion ?? 'Unknown'}</dd><dt>Plugin version</dt><dd>{health?.pluginVersion ?? 'Unknown'}</dd><dt>IPC status</dt><dd>{health?.ipcStatus ?? 'Unknown'}</dd></dl></main>
}

createRoot(document.getElementById('root')!).render(<App />)
