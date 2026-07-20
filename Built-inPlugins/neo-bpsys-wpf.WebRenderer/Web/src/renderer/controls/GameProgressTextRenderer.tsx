import type { CSSProperties, ReactNode } from 'react'
import { TextVisual } from '../TextVisual'
import type { RuntimeState, WebGameProgressDisplayState } from '../../protocol/runtime'
import type { GameProgressConfig } from '../controlTypes'
import { color } from '../colors'

const enumValue = (value: unknown, names: readonly string[], fallback: string) => typeof value === 'number' ? names[value] ?? fallback : typeof value === 'string' && names.includes(value) ? value : fallback
function VerticalText({ value, config, isCjkCulture }: { value: string; config: GameProgressConfig; isCjkCulture: boolean }) {
  const selected = enumValue(config.VerticalLanguageMode, ['Auto', 'Upright', 'RotateBlock', 'StackCharacters'], 'Auto'); const mode = selected === 'Auto' ? isCjkCulture ? 'Upright' : enumValue(config.LatinVerticalMode, ['RotateBlock', 'StackCharacters'], 'RotateBlock') : selected
  if (mode === 'Upright' || mode === 'StackCharacters') return <span data-game-progress-vertical={mode} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: config.VerticalTextSpacing ?? 0 }}>{Array.from(value).map((item, index) => <span key={index}>{item}</span>)}</span>
  const direction = enumValue(config.VerticalDirection, ['Auto', 'FacingLeft', 'FacingRight'], 'Auto'); return <span data-game-progress-vertical="RotateBlock" style={{ display: 'block', transform: `rotate(${direction === 'FacingRight' ? 90 : -90}deg)`, whiteSpace: 'nowrap' }}>{value}</span>
}
export function GameProgressTextRenderer({ controlId, config, runtime }: { controlId: string; config: GameProgressConfig; runtime: RuntimeState }) {
  const mode = enumValue(config.DisplayMode, ['Inline', 'TwoLine', 'VerticalHalfOnly', 'VerticalGameOnly', 'VerticalGameAndHalf', 'VerticalSeparatedGameAndHalf', 'RibbonGameOnly', 'HorizontalGameOnly', 'HorizontalHalfOnly', 'Vertical', 'VerticalTwoLine'], 'Inline'); const parts = runtime.values[controlId] as WebGameProgressDisplayState | undefined
  const vertical = (value: string) => <VerticalText value={value} config={config} isCjkCulture={parts?.IsCjkCulture === true} />
  let content: ReactNode = parts?.IsValid ? parts.FullText : ''
  if (parts?.IsValid && !parts.IsFree) {
    if (mode === 'TwoLine') content = <>{parts.GameText}<br />{parts.HalfText}</>
    else if (mode === 'HorizontalGameOnly') content = parts.GameText
    else if (mode === 'HorizontalHalfOnly') content = parts.HalfText
    else if (mode === 'Vertical') content = vertical(parts.FullText)
    else if (mode === 'VerticalHalfOnly') content = vertical(parts.HalfText)
    else if (mode === 'VerticalGameOnly' || mode === 'RibbonGameOnly') content = vertical(parts.GameText)
    else if (mode === 'VerticalGameAndHalf' || mode === 'VerticalTwoLine') content = <span data-game-progress-groups style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: config.GroupSpacing ?? 8 }}>{vertical(parts.GameText)}{vertical(parts.HalfText)}</span>
    else if (mode === 'VerticalSeparatedGameAndHalf') content = <span data-game-progress-groups style={{ display: 'grid', gridTemplateRows: 'auto auto auto', justifyItems: 'center' }}>{vertical(parts.GameText)}{config.ShowSeparator ? <i data-game-progress-separator style={{ width: '100%', height: config.SeparatorThickness ?? 1, margin: `${(config.GroupSpacing ?? 8) / 2}px 0`, background: color(config.SeparatorColor, '#fff') }} /> : <i style={{ height: config.GroupSpacing ?? 8 }} />}{vertical(parts.HalfText)}</span>
  } else if (parts?.IsValid && parts.IsFree && (mode.startsWith('Vertical') || mode === 'RibbonGameOnly')) content = vertical(parts.FullText)
  const style: CSSProperties = { width: '100%', height: '100%', background: color(config.BackgroundColor), padding: `${config.PaddingTop ?? 0}px ${config.PaddingRight ?? 0}px ${config.PaddingBottom ?? 0}px ${config.PaddingLeft ?? 0}px` }
  return <TextVisual config={config} runtime={runtime} style={style}>{content}</TextVisual>
}
