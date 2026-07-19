import type { CSSProperties } from 'react'
import { TextVisual } from '../TextVisual'
import type { Localization } from '../../protocol/bootstrap'
import type { RuntimeState } from '../../protocol/runtime'
import type { GameProgressConfig } from '../controlTypes'
import { color } from '../colors'

const modes = ['Inline', 'TwoLine', 'VerticalHalfOnly', 'VerticalGameOnly', 'VerticalGameAndHalf', 'VerticalSeparatedGameAndHalf', 'RibbonGameOnly', 'HorizontalGameOnly', 'HorizontalHalfOnly', 'Vertical', 'VerticalTwoLine']
const diagnostics = new Set<string>()
const enumName = (value: unknown, names: string[], fallback: string) => { const accepted = typeof value === 'number' ? names[value] : typeof value === 'string' && (names.includes(value) || value === 'FollowApp' || value === 'Arabic' || value === 'CjkNumeral' || value === 'Auto' || value === 'Upright' || value === 'RotateBlock' || value === 'StackCharacters' || value === 'FacingLeft' || value === 'FacingRight') ? value : undefined; if (!accepted && value != null) { const key = `${String(value)}:${fallback}`; if (!diagnostics.has(key)) { diagnostics.add(key); console.warn(`[Web Renderer] Unknown enum '${String(value)}'; expected ${fallback}.`) } } return accepted ?? fallback }
const cjk = (culture?: string) => /^(zh|ja|ko)/i.test(culture ?? '')
const numberText = (number: number, style: string, culture?: string) => (style === 'CjkNumeral' || style === 'Auto' && cjk(culture)) ? ['一', '二', '三', '四', '五'][number - 1] ?? String(number) : String(number)
function parts(progress: unknown, bo3: boolean, config: GameProgressConfig, localization?: Localization) {
  const index = typeof progress === 'number' ? progress : Number(progress)
  if (index === -1) return { free: true, full: localization?.Values?.['Game:GameProgressFree'] ?? 'FREE', game: '', half: '' }
  const table = bo3 ? [[1, false], [1, false], [2, false], [2, false], [3, false], [3, false], [3, true], [3, true], [5, false], [5, false], [5, true], [5, true]] : [[1,false],[1,false],[2,false],[2,false],[3,false],[3,false],[4,false],[4,false],[5,false],[5,false],[5,true],[5,true]]
  const entry = table[index] as [number, boolean] | undefined; if (!entry) return { free: true, full: '', game: '', half: '' }
  const language = enumName(config.DisplayLanguage, [], 'FollowApp'); const culture = language === 'FollowApp' ? localization?.Culture : language === 'zh_Hans' ? 'zh-CN' : language === 'ja_JP' ? 'ja-JP' : 'en-US'
  const gameNumber = numberText(entry[0], enumName(config.NumberStyle, ['Auto','Arabic','CjkNumeral'], 'Auto'), culture)
  const gameFormat = localization?.Values?.[entry[1] ? 'Game:GameProgressGameOvertimeOnlyFormat' : 'Game:GameProgressGameOnlyFormat'] ?? (entry[1] ? 'GAME {0} OVERTIME' : 'GAME {0}')
  const half = localization?.Values?.[index % 2 ? 'Game:SecondHalf' : 'Game:FirstHalf'] ?? (index % 2 ? 'SECOND HALF' : 'FIRST HALF')
  const fullFormat = localization?.Values?.[entry[1] ? 'Game:GameProgressGameOvertimeHalfFormat' : 'Game:GameProgressGameHalfFormat'] ?? '{0} {1}'
  const game = gameFormat.replace('{0}', gameNumber); return { free: false, game, half, full: fullFormat.replace('{0}', gameNumber).replace('{1}', half) }
}
function Vertical({ value, config, mode }: { value: string; config: GameProgressConfig; mode: string }) {
  const language = enumName(config.VerticalLanguageMode, ['Auto','Upright','RotateBlock','StackCharacters'], 'Auto')
  const effective = language === 'Auto' ? (cjk() ? 'Upright' : 'RotateBlock') : language
  if (effective === 'StackCharacters' || effective === 'Upright') return <span style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: config.VerticalTextSpacing || 0 }}>{Array.from(value).map((character, index) => <span key={index}>{character}</span>)}</span>
  const direction = enumName(config.VerticalDirection, ['Auto','FacingLeft','FacingRight'], 'Auto')
  return <span style={{ display: 'block', transform: `rotate(${direction === 'FacingRight' ? '90' : '-90'}deg)`, whiteSpace: 'nowrap' }}>{value}</span>
}
export function GameProgressTextRenderer({ config, runtime, localization }: { config: GameProgressConfig; runtime: RuntimeState; localization?: Localization }) {
  const mode = enumName(config.DisplayMode, modes, 'Inline'); const p = parts(runtime.values['CurrentGame.GameProgress'], runtime.values.IsBo3Mode === true, config, localization)
  const verticalMode = mode.startsWith('Vertical') || mode === 'RibbonGameOnly'; const display = p.free ? p.full : mode.includes('HalfOnly') ? p.half : mode.includes('GameOnly') || mode === 'RibbonGameOnly' ? p.game : p.full
  const style: CSSProperties = { width: '100%', height: '100%', background: color(config.BackgroundColor), padding: `${config.PaddingTop ?? 0}px ${config.PaddingRight ?? 0}px ${config.PaddingBottom ?? 0}px ${config.PaddingLeft ?? 0}px` }
  let content: React.ReactNode = verticalMode ? <Vertical value={display} config={config} mode={mode} /> : mode === 'TwoLine' && !p.free ? <>{p.game}<br />{p.half}</> : display
  if ((mode === 'VerticalTwoLine' || mode === 'VerticalGameAndHalf' || mode === 'VerticalSeparatedGameAndHalf') && !p.free) content = <span style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: config.GroupSpacing ?? 8 }}><Vertical value={p.game} config={config} mode={mode} />{mode === 'VerticalSeparatedGameAndHalf' && config.ShowSeparator ? <i style={{ width: '100%', height: config.SeparatorThickness ?? 1, background: color(config.SeparatorColor, '#fff') }} /> : null}<Vertical value={p.half} config={config} mode={mode} /></span>
  return <TextVisual config={config} runtime={runtime} style={style}>{content}</TextVisual>
}
