import type { CSSProperties, ReactNode } from 'react'
import { horizontalGrid, textAlign, verticalGrid, wrapping } from './alignment'
import { color } from './colors'
import { fontFamily, fontWeight } from './fonts'
import type { RuntimeState } from '../protocol/runtime'
import type { TextStyle } from './controlTypes'

export function TextVisual({ config, runtime, children, className, style }: { config: TextStyle; runtime: RuntimeState; children: ReactNode; className?: string; style?: CSSProperties }) {
  const bound = config.ColorBindingPath ? runtime.values[config.ColorBindingPath] : undefined
  const fixedWidth = typeof config.Width === 'number'
  const fixedHeight = typeof config.Height === 'number'
  const slot: CSSProperties = { boxSizing: 'border-box', display: 'grid', width: fixedWidth ? '100%' : 'max-content', height: fixedHeight ? '100%' : 'max-content', minWidth: 0, minHeight: 0, justifyItems: horizontalGrid(config.HorizontalAlignment), alignItems: verticalGrid(config.VerticalAlignment), ...style }
  const element: CSSProperties = { boxSizing: 'border-box', display: 'block', minWidth: 0, minHeight: 0, width: horizontalGrid(config.HorizontalAlignment) === 'stretch' ? '100%' : 'max-content', height: verticalGrid(config.VerticalAlignment) === 'stretch' ? '100%' : 'max-content', maxWidth: fixedWidth ? '100%' : undefined, maxHeight: fixedHeight ? '100%' : undefined, margin: `${config.ContentMarginTop ?? 0}px ${config.ContentMarginRight ?? 0}px ${config.ContentMarginBottom ?? 0}px ${config.ContentMarginLeft ?? 0}px`, textAlign: textAlign(config.TextAlignment), whiteSpace: wrapping(config.TextWrapping), color: color(bound ?? config.Color, '#fff'), fontFamily: fontFamily(config.FontFamily), fontWeight: fontWeight(config.FontWeight), fontSize: typeof config.FontSize === 'number' && config.FontSize > 0 ? config.FontSize : undefined, lineHeight: 'normal' }
  return <div className={className} data-behavior-content data-text-layout-slot style={slot}><span data-text-element style={element}>{children}</span></div>
}
