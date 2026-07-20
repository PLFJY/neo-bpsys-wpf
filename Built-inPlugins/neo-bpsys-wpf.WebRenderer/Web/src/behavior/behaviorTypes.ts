export type RecordValue = Record<string, unknown>

export type BehaviorEvent = {
  EventType: string
  WindowType?: string
  CanvasName?: string
  Source?: string
  Payload: RecordValue
}

export type BehaviorNode = { NodeId: string; NodeType: string; Properties?: RecordValue }
export type BehaviorEdge = { SourceNodeId: string; SourcePort: string; TargetNodeId: string; TargetPort: string }
export type BehaviorGraph = { Nodes?: BehaviorNode[]; Connections?: BehaviorEdge[] }
export type BehaviorTrigger = { EventType?: string; Filters?: { Left?: string; Operator?: string; Right?: string }[] }

export type FrontedVisualEffect = {
  Kind?: 'None' | 'Glow' | 'DropShadow' | string
  Color?: string
  Opacity?: number
  BlurRadius?: number
  ShadowDepth?: number
  Direction?: number
}

export type AnimationPartConfig = {
  Name?: string
  Kind?: 'Rectangle' | 'Border' | 'Image' | string
  Layer?: 'BelowContent' | 'AboveContent' | string
  Width?: number | null
  Height?: number | null
  WidthText?: string | null
  HeightText?: string | null
  Left?: number
  Top?: number
  Fill?: string | null
  Stroke?: string | null
  StrokeThickness?: number
  ImagePath?: string | null
  Opacity?: number
  Visibility?: string
  ZIndex?: number
  IsHitTestVisible?: boolean
  Effect?: FrontedVisualEffect
}

export type FrontedBehavior = {
  BehaviorId: string
  Name?: string
  Enabled?: boolean
  Kind?: string
  Trigger?: BehaviorTrigger
  StartTrigger?: BehaviorTrigger
  StopTriggers?: BehaviorTrigger[]
  Graph?: BehaviorGraph
  StartGraph?: BehaviorGraph
  LoopGraph?: BehaviorGraph
  StopGraph?: BehaviorGraph
  ExitGraph?: BehaviorGraph
  EnterGraph?: BehaviorGraph
  TransitionTrigger?: BehaviorTrigger
  ReentryPolicy?: string
  LoopPolicy?: RecordValue
}

export type ControlBehaviorSet = {
  BehaviorGuid: string
  DisplayName?: string
  AnimationParts?: AnimationPartConfig[]
  Behaviors?: FrontedBehavior[]
}

export type BehaviorDocument = { ControlBehaviorSets?: ControlBehaviorSet[] }

export type BehaviorContext = {
  event: BehaviorEvent
  start?: BehaviorEvent
  stop?: BehaviorEvent
  guid: string
  display?: string
}
