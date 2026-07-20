import type { RuntimeState } from '../../protocol/runtime'
import type { ImageConfig } from '../controlTypes'
import { finite } from '../controlTypes'
import { PickingBorderRenderer } from './PickingBorderRenderer'

const lockVisible = (config: ImageConfig, runtime: RuntimeState): boolean => {
  if (!config.Lockable) return false
  if (!config.LockVisibilityBindingPath) return config.LockVisibleWhen === 'Always'
  const value = runtime.values[config.LockVisibilityBindingPath]
  return config.LockVisibleWhen === 'VisibleWhenFalse' ? value === false : value === true
}

export function ImageOverlays({ name, config, runtime, resources }: { name: string; config: ImageConfig; runtime: RuntimeState; resources: Record<string, string> }) {
  const isLockVisible = lockVisible(config, runtime)
  return <>
    {config.Lockable ? <img data-lock-overlay data-animation-part="LockOverlay" data-runtime-name={`${name}LockOverlay`} src={resources[config.LockImagePath ?? '']} style={{ position: 'absolute', inset: 0, width: '100%', height: '100%', zIndex: finite(config.LockZIndexOffset, 1), visibility: isLockVisible ? 'visible' : 'hidden', pointerEvents: 'none' }} /> : null}
    <PickingBorderRenderer name={config.PickingBorderName || name} behaviorGuid={config.BehaviorGuid} available={config.PickingBorderAvailable} imagePath={config.PickingBorderImagePath} fillColor={config.PickingBorderFillColor} zIndexOffset={config.PickingBorderZIndexOffset} resources={resources} />
  </>
}
