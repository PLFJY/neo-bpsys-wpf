import type { CSSProperties, ReactNode } from 'react'
import { TextVisual } from '../TextVisual'
import type { Localization } from '../../protocol/bootstrap'
import type { RuntimeState } from '../../protocol/runtime'
import type { GameProgressConfig } from '../controlTypes'
import { color } from '../colors'

type Parts = { free: boolean; game: string; half: string; full: string }
const enumValue = (value: unknown, names: readonly string[], fallback: string) => typeof value === 'number' ? names[value] ?? fallback : typeof value === 'string' && names.includes(value) ? value : fallback
const localized = (localization: Localization | undefined, key: string, fallback: string) => localization?.Values?.[key] ?? localization?.Values?.[`Game:${key}`] ?? fallback
const format = (template: string, ...values: string[]) => template.replace(/\{(\d+)\}/g, (_, index) => values[Number(index)] ?? '')
const cjk = (culture?: string) => /^(zh|ja|ko)(-|$)/i.test(culture ?? '')
function getParts(progress: unknown, isBo3: boolean, config: GameProgressConfig, localization?: Localization): Parts {
  const value = typeof progress === 'number' ? progress : Number(progress)
  if (value === -1) return { free: true, game: '', half: '', full: localized(localization, 'GameProgressFree', 'FREE') }
  const gameInfo = value === 0 ? [1, false, false] : value === 1 ? [1, false, true] : value === 2 ? [2, false, false] : value === 3 ? [2, false, true] : value === 4 ? [3, false, false] : value === 5 ? [3, false, true] : value === 6 ? [isBo3 ? 3 : 4, isBo3, false] : value === 7 ? [isBo3 ? 3 : 4, isBo3, true] : value === 8 ? [5, false, false] : value === 9 ? [5, false, true] : value === 10 ? [5, true, false] : value === 11 ? [5, true, true] : undefined
  if (!gameInfo) return { free: true, game: '', half: '', full: '' }
  const [number, overtime, second] = gameInfo as [number, boolean, boolean]; const culture = enumValue(config.DisplayLanguage, ['FollowApp', 'zh_Hans', 'en_US', 'ja_JP'], 'FollowApp') === 'FollowApp' ? localization?.Culture : enumValue(config.DisplayLanguage, ['FollowApp', 'zh_Hans', 'en_US', 'ja_JP'], 'FollowApp').replace('_', '-')
  const style = enumValue(config.NumberStyle, ['Auto', 'Arabic', 'CjkNumeral'], 'Auto'); const numberText = style === 'CjkNumeral' || style === 'Auto' && cjk(culture) ? ['一', '二', '三', '四', '五'][number - 1] : String(number)
  const half = localized(localization, second ? 'SecondHalf' : 'FirstHalf', second ? 'SECOND HALF' : 'FIRST HALF'); const game = format(localized(localization, overtime ? 'GameProgressGameOvertimeOnlyFormat' : 'GameProgressGameOnlyFormat', overtime ? 'GAME {0} OVERTIME' : 'GAME {0}'), numberText)
  return { free: false, game, half, full: format(localized(localization, overtime ? 'GameProgressGameOvertimeHalfFormat' : 'GameProgressGameHalfFormat', '{0} {1}'), numberText, half) }
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
