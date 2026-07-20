import { useEffect, useRef, useState } from 'react'
import { CanvasRuntime } from './CanvasRuntime'
import { WebBehaviorRuntime, type BehaviorDocument } from '../behaviorRuntime'
import { emptyRuntime, type RuntimeMessage, type RuntimeState } from '../protocol/runtime'
import type { Bootstrap } from '../protocol/bootstrap'
import { RuntimeStore } from '../runtime/RuntimeStore'
import { waitForTransitionCommit } from '../behavior/TransitionCommitBarrier'
import type { BehaviorEvent, RecordValue } from '../behavior/behaviorTypes'

const base64 = (value: string) => btoa(unescape(encodeURIComponent(value))).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '')
export function WebRendererApp() {
  const encoded = location.pathname.startsWith('/render/') ? location.pathname.slice('/render/'.length) : null
  const [bootstrap, setBootstrap] = useState<Bootstrap | null>(null); const [error, setError] = useState<string | null>(null); const [windows, setWindows] = useState<{ fullWindowType: string; displayName: string }[]>([])
  const [runtime, setRuntime] = useState<RuntimeState>(emptyRuntime)
  const store = useRef(new RuntimeStore())
  const behavior = useRef(new WebBehaviorRuntime())
  useEffect(() => store.current.subscribe(() => setRuntime(store.current.state)), [])
  useEffect(() => {
    const load = () => {
      if (!encoded) return fetch('/api/windows').then(response => response.json()).then(setWindows).catch(() => setError('无法读取窗口列表。'))
      return fetch(`/api/bootstrap/${encoded}`).then(async response => response.ok ? response.json() : Promise.reject(await response.json())).then((value: Bootstrap) => { if (!value?.Layout) throw new Error('Bootstrap schema is invalid.'); setBootstrap(value); setError(null) }).catch(() => setError('无法加载或验证布局 bootstrap。'))
    }
    void load()
    const scheme = location.protocol === 'https:' ? 'wss' : 'ws'
    let closed = false; let retry: number | undefined
    const connect = () => {
      const socket = new WebSocket(`${scheme}://${location.host}/ws`)
      const acknowledge = (type: string, correlationId: unknown) => { if (typeof correlationId === 'string' && socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type, correlationId })) }
      socket.onopen = () => { socket.send(JSON.stringify({ type: 'page.attach', fullWindowType: encoded })); void load() }
      socket.onmessage = event => {
        try {
          const message = JSON.parse(event.data) as RuntimeMessage & { payload?: Record<string, unknown> }
          if (message.type === 'transition.prepare') {
            const id = message.payload?.correlationId
            void behavior.current.prepareTransition((message.payload ?? {}) as { correlationId?: string; requests?: RecordValue[] }).catch(error => console.warn('[Web Renderer] transition prepare failed.', error)).finally(() => acknowledge('transition.exitCompleted', id))
            return
          }
          if (message.type === 'transition.committed') {
            const correlationId = String(message.payload?.correlationId ?? '')
            const requiredGeneration = Number(message.payload?.requiredGeneration ?? message.payload?.generation ?? 0)
            const requiredSequence = Number(message.payload?.requiredSequence ?? 0)
            void waitForTransitionCommit(store.current, { correlationId, requiredGeneration, requiredSequence }).then(() => behavior.current.commitTransition(correlationId)).catch(error => console.warn('[Web Renderer] transition commit failed.', error)).finally(() => acknowledge('transition.enterCompleted', correlationId))
            return
          }
          if (message.type === 'transition.cancel') { behavior.current.cancelTransition(String(message.payload?.correlationId ?? '')); return }
          if (message.type === 'bootstrap.changed') { behavior.current.dispose(); store.current.reset(); void load(); return }
          if (message.type === 'behavior.event' && message.payload) { behavior.current.publish(message.payload as unknown as BehaviorEvent); return }
          const payload = message.payload
          if (!payload || typeof payload.Sequence !== 'number') return
          if (message.type === 'snapshot' || message.type === 'bindingPatch') void store.current.enqueue(message.type, payload)
        } catch { setError('收到无效的实时状态消息。') }
      }
      socket.onclose = () => { behavior.current.dispose(); store.current.reset(); if (!closed) retry = window.setTimeout(connect, 1000) }
      return socket
    }
    const socket = connect()
    return () => { closed = true; behavior.current.dispose(); store.current.reset(); if (retry) clearTimeout(retry); socket.close() }
  }, [encoded])
  useEffect(() => { behavior.current.replace(bootstrap?.BehaviorDocument as BehaviorDocument | undefined) }, [bootstrap])
  if (!encoded) return <main className="window-index"><h1>Web Renderer</h1>{windows.map(window => <a key={window.fullWindowType} href={`/render/${base64(window.fullWindowType)}`}>{window.displayName}</a>)}</main>
  if (error || !bootstrap?.Layout) return <main className="error-page"><h1>布局无法渲染</h1><p>{error ?? bootstrap?.Diagnostics.join('\n') ?? 'LayoutMissing'}</p></main>
  return <CanvasRuntime bootstrap={bootstrap} runtime={runtime} />
}
