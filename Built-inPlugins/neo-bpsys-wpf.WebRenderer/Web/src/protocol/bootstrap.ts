import type { BehaviorDocument } from '../behaviorRuntime'
import type { ControlConfig } from '../renderer/controlTypes'

export type Localization = { Culture?: string; Values?: Record<string, string> }
export type Bootstrap = {
  FullWindowType: string; DisplayName: string; Layout: Layout | null; BehaviorDocument?: BehaviorDocument | null
  Resources: Record<string, string>; Diagnostics: string[]; Localization?: Localization
}
export type Layout = { WindowSettings: { ViewboxStretch?: string }; CanvasSettings: CanvasSettings; ControlLayout: { Controls: Record<string, ControlConfig> } }
export type CanvasSettings = { CanvasWidth?: number; CanvasHeight?: number; BackgroundImage?: string }
