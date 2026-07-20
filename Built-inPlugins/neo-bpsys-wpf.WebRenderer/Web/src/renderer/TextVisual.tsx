import type { CSSProperties, ReactNode } from 'react'
import { horizontal, textAlign, vertical, wrapping } from './alignment'
import { color } from './colors'
import { fontFamily, fontWeight } from './fonts'
import type { RuntimeState } from '../protocol/runtime'
import type { TextStyle } from './controlTypes'

export function TextVisual({ config, runtime, children, className, style }: { config: TextStyle; runtime: RuntimeState; children: ReactNode; className?: string; style?: CSSProperties }) {
  const bound = config.ColorBindingPath ? runtime.values[config.ColorBindingPath] : undefined
  const visual: CSSProperties = { boxSizing: 'border-box', display: 'flex', justifyContent: horizontal(config.HorizontalAlignment), alignItems: vertical(config.VerticalAlignment), textAlign: textAlign(config.TextAlignment), whiteSpace: wrapping(config.TextWrapping), color: color(bound ?? config.Color, '#fff'), fontFamily: fontFamily(config.FontFamily), fontWeight: fontWeight(config.FontWeight), fontSize: typeof config.FontSize === 'number' && config.FontSize > 0 ? config.FontSize : undefined, lineHeight: 'normal', ...style }
  return <div className={className} data-behavior-content style={visual}>{children}</div>
}
