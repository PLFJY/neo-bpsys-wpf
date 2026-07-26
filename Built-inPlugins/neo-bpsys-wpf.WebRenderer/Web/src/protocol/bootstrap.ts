import type { BehaviorDocument } from '../behaviorRuntime'
import type { ControlConfig } from '../renderer/controlTypes'

export type WebLocalizationSnapshot = {
  SchemaVersion: number
  Revision: number
  Culture: string
  StaticTexts: Record<string, string>
  MapV2Texts: Record<string, WebMapV2Localization>
}
export type WebMapV2Localization = { MapKey: string; MapDisplayName: string; CampSurDisplayName: string; CampHunDisplayName: string }
export type WebLocalizedControlState = { ControlId: string; DisplayText: string }
export type Bootstrap = {
  FullWindowType: string; DisplayName: string; Layout: Layout | null; BehaviorDocument?: BehaviorDocument | null
  Resources: Record<string, string>; Diagnostics: string[]; DefaultPickingBorderResourceUrl?: string; Localization?: WebLocalizationSnapshot
}
export type Layout = { WindowSettings: { ViewboxStretch?: string }; CanvasSettings: CanvasSettings; ControlLayout: { Controls: Record<string, ControlConfig> } }
export type CanvasState = { BackgroundImage?: string; Controls?: Record<string, ControlConfig>; RequiredPlugins?: string[] }
export type CanvasSettings = { CanvasWidth?: number; CanvasHeight?: number; BackgroundImage?: string; EnableBoModeStates?: boolean; BoModeStates?: Record<string, CanvasState> }
