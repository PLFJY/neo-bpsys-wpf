import type { RuntimeState } from '../../protocol/runtime'
import type { ImageConfig } from '../controlTypes'
import { finite } from '../controlTypes'

const lockVisible = (config: ImageConfig, runtime: RuntimeState): boolean => {
  if (!config.Lockable) return false
  if (!config.LockVisibilityBindingPath) return config.LockVisibleWhen === 'Always'
  const value = runtime.values[config.LockVisibilityBindingPath]
  return config.LockVisibleWhen === 'VisibleWhenFalse' ? value === false : value === true
}

export function ImageOverlays({ name, config, runtime, resources }: { name: string; config: ImageConfig; runtime: RuntimeState; resources: Record<string, string> }) {
  const mask = resources[config.PickingBorderImagePath ?? '']
  const isLockVisible = lockVisible(config, runtime)
  return <>
    {config.Lockable ? <img data-lock-overlay data-animation-part="LockOverlay" data-runtime-name={`${name}LockOverlay`} src={resources[config.LockImagePath ?? '']} style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', zIndex: finite(config.LockZIndexOffset, 1), visibility: isLockVisible ? 'visible' : 'hidden', pointerEvents: 'none' }} /> : null}
    {config.PickingBorderAvailable ? <div data-animation-part="PickingBorder" data-runtime-name={config.PickingBorderName || `${name}PickingBorder`} data-picking-border style={{ position: 'absolute', inset: 0, zIndex: finite(config.PickingBorderZIndexOffset, 2), background: '#fff', maskImage: mask ? `url(${mask})` : undefined, WebkitMaskImage: mask ? `url(${mask})` : undefined, maskSize: '100% 100%', WebkitMaskSize: '100% 100%', opacity: 0, visibility: 'hidden', pointerEvents: 'none' }} /> : null}
  </>
}
