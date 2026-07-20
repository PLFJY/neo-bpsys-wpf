import type { CSSProperties, ReactNode } from 'react'
import { TextVisual } from '../TextVisual'
import type { Localization } from '../../protocol/bootstrap'
import type { RuntimeState } from '../../protocol/runtime'
import type { GameProgressConfig } from '../controlTypes'
import { color } from '../colors'
import { localize } from '../localization'

type Parts = { free: boolean; game: string; half: string; full: string }
const enumValue = (value: unknown, names: readonly string[], fallback: string) => typeof value === 'number' ? names[value] ?? fallback : typeof value === 'string' && names.includes(value) ? value : fallback
const localized = (localization: Localization | undefined, key: string, fallback = '') => localize(localization, 'Game', key, fallback)
const format = (template: string, ...values: string[]) => template.replace(/\{(\d+)\}/g, (_, index) => values[Number(index)] ?? '')
const cjk = (culture?: string) => /^(zh|ja|ko)(-|$)/i.test(culture ?? '')
function getParts(progress: unknown, isBo3: boolean, config: GameProgressConfig, localization?: Localization): Parts {
  if (typeof progress === 'object' && progress !== null) {
    const semantic = progress as { isFree?: boolean; gameText?: string; halfText?: string; fullText?: string }
    if (typeof semantic.fullText === 'string') return {
      free: semantic.isFree === true,
      game: semantic.gameText ?? '',
      half: semantic.halfText ?? '',
      full: semantic.fullText
    }
  }
  return { free: false, game: '', half: '', full: '' }
}
function VerticalText({ value, config, culture }: { value: string; config: GameProgressConfig; culture?: string }) {
  const selected = enumValue(config.VerticalLanguageMode, ['Auto', 'Upright', 'RotateBlock', 'StackCharacters'], 'Auto'); const mode = selected === 'Auto' ? cjk(culture) ? 'Upright' : enumValue(config.LatinVerticalMode, ['RotateBlock', 'StackCharacters'], 'RotateBlock') : selected
  if (mode === 'Upright' || mode === 'StackCharacters') return <span data-game-progress-vertical={mode} style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: config.VerticalTextSpacing ?? 0 }}>{Array.from(value).map((item, index) => <span key={index}>{item}</span>)}</span>
  const direction = enumValue(config.VerticalDirection, ['Auto', 'FacingLeft', 'FacingRight'], 'Auto'); return <span data-game-progress-vertical="RotateBlock" style={{ display: 'block', transform: `rotate(${direction === 'FacingRight' ? 90 : -90}deg)`, whiteSpace: 'nowrap' }}>{value}</span>
}
export function GameProgressTextRenderer({ config, runtime, localization }: { config: GameProgressConfig; runtime: RuntimeState; localization?: Localization }) {
  const mode = enumValue(config.DisplayMode, ['Inline', 'TwoLine', 'VerticalHalfOnly', 'VerticalGameOnly', 'VerticalGameAndHalf', 'VerticalSeparatedGameAndHalf', 'RibbonGameOnly', 'HorizontalGameOnly', 'HorizontalHalfOnly', 'Vertical', 'VerticalTwoLine'], 'Inline'); const parts = getParts(runtime.values['CurrentGame.GameProgress'], runtime.values.IsBo3Mode === true, config, localization); const culture = localization?.Culture
  const vertical = (value: string) => <VerticalText value={value} config={config} culture={culture} />
  let content: ReactNode = parts.full
  if (!parts.free) {
    if (mode === 'TwoLine') content = <>{parts.game}<br />{parts.half}</>
    else if (mode === 'HorizontalGameOnly') content = parts.game
    else if (mode === 'HorizontalHalfOnly') content = parts.half
    else if (mode === 'Vertical' ) content = vertical(parts.full)
    else if (mode === 'VerticalHalfOnly') content = vertical(parts.half)
    else if (mode === 'VerticalGameOnly' || mode === 'RibbonGameOnly') content = vertical(parts.game)
    else if (mode === 'VerticalGameAndHalf' || mode === 'VerticalTwoLine') content = <span data-game-progress-groups style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: config.GroupSpacing ?? 8 }}>{vertical(parts.game)}{vertical(parts.half)}</span>
    else if (mode === 'VerticalSeparatedGameAndHalf') content = <span data-game-progress-groups style={{ display: 'grid', gridTemplateRows: 'auto auto auto', justifyItems: 'center' }}>{vertical(parts.game)}{config.ShowSeparator ? <i data-game-progress-separator style={{ width: '100%', height: config.SeparatorThickness ?? 1, margin: `${(config.GroupSpacing ?? 8) / 2}px 0`, background: color(config.SeparatorColor, '#fff') }} /> : <i style={{ height: config.GroupSpacing ?? 8 }} />}{vertical(parts.half)}</span>
  } else if (mode.startsWith('Vertical') || mode === 'RibbonGameOnly') content = vertical(parts.full)
  const style: CSSProperties = { width: '100%', height: '100%', background: color(config.BackgroundColor), padding: `${config.PaddingTop ?? 0}px ${config.PaddingRight ?? 0}px ${config.PaddingBottom ?? 0}px ${config.PaddingLeft ?? 0}px` }
  return <TextVisual config={config} runtime={runtime} style={style}>{content}</TextVisual>
}
