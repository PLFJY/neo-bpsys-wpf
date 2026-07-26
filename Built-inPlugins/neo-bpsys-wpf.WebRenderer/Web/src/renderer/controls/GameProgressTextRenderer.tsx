import type { ReactNode } from 'react'
import type { WebLocalizationSnapshot } from '../../protocol/bootstrap'
import type { RuntimeState, WebGameProgressDisplayState } from '../../protocol/runtime'
import type { GameProgressConfig } from '../controlTypes'
import { color } from '../colors'
import {
  GameProgressHorizontalLayout,
  GameProgressRoot,
  GameProgressVerticalLayout,
  resolveDisplayMode,
  resolveVerticalDirection,
  resolveVerticalLanguageMode,
  RotatedLayoutBox,
  StackedVerticalText,
  UprightVerticalText,
} from './GameProgressLayout'

const verticalDisplayModes = new Set([
  'Vertical',
  'VerticalTwoLine',
  'VerticalHalfOnly',
  'VerticalGameOnly',
  'VerticalGameAndHalf',
  'VerticalSeparatedGameAndHalf',
  'RibbonGameOnly',
])

const nonNegative = (value: unknown, fallback: number) => typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : fallback

function VerticalText({ value, config, isCjkCulture, culture }: { value: string; config: GameProgressConfig; isCjkCulture: boolean; culture?: string }) {
  const mode = resolveVerticalLanguageMode(config, isCjkCulture)
  const characterSpacing = nonNegative(config.VerticalTextSpacing, 0)
  if (mode === 'Upright') return <UprightVerticalText value={value} culture={culture} spacing={characterSpacing} />
  if (mode === 'StackCharacters') return <StackedVerticalText value={value} culture={culture} spacing={characterSpacing} />
  return <RotatedLayoutBox direction={resolveVerticalDirection(config.VerticalDirection)}><span style={{ display: 'block', whiteSpace: 'nowrap' }}>{value}</span></RotatedLayoutBox>
}

function VerticalGroup({ value, config, parts, culture }: { value: string; config: GameProgressConfig; parts: WebGameProgressDisplayState; culture?: string }) {
  return <div data-game-progress-group style={{ width: 'max-content', height: 'max-content', display: 'grid', placeItems: 'center' }}><VerticalText value={value} config={config} isCjkCulture={parts.IsCjkCulture} culture={culture} /></div>
}

function VerticalGroups({ config, parts, culture, separated }: { config: GameProgressConfig; parts: WebGameProgressDisplayState; culture?: string; separated: boolean }) {
  const groupSpacing = nonNegative(config.GroupSpacing, 8)
  const game = <VerticalGroup value={parts.GameText} config={config} parts={parts} culture={culture} />
  const half = <VerticalGroup value={parts.HalfText} config={config} parts={parts} culture={culture} />
  if (!separated) {
    return <div data-game-progress-groups style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', width: 'max-content', height: 'max-content', gap: groupSpacing }}>{game}{half}</div>
  }
  return <div data-game-progress-groups data-game-progress-separated-groups style={{ display: 'grid', gridTemplateRows: 'auto auto auto', alignItems: 'center', width: 'max-content', height: 'max-content' }}>
    {game}
    {config.ShowSeparator
      ? <i data-game-progress-separator style={{ justifySelf: 'stretch', width: '100%', height: nonNegative(config.SeparatorThickness, 1), margin: `${groupSpacing / 2}px 0`, background: color(config.SeparatorColor, '#fff') }} />
      : <i data-game-progress-spacing style={{ width: 0, height: groupSpacing }} />}
    {half}
  </div>
}

function horizontalContent(mode: string, parts: WebGameProgressDisplayState): ReactNode {
  if (parts.IsFree || mode === 'Inline') return parts.FullText
  if (mode === 'TwoLine') return <>{parts.GameText}<br />{parts.HalfText}</>
  if (mode === 'HorizontalGameOnly') return parts.GameText
  if (mode === 'HorizontalHalfOnly') return parts.HalfText
  return parts.FullText
}

export function GameProgressTextRenderer({ controlId, config, runtime, localization }: { controlId: string; config: GameProgressConfig; runtime: RuntimeState; localization?: WebLocalizationSnapshot }) {
  const mode = resolveDisplayMode(config.DisplayMode)
  const parts = runtime.values[controlId] as WebGameProgressDisplayState | undefined
  const validParts = parts?.IsValid === true ? parts : undefined
  const isVertical = verticalDisplayModes.has(mode)
  let content: ReactNode = ''

  if (validParts) {
    if (!isVertical) {
      content = <GameProgressHorizontalLayout>{horizontalContent(mode, validParts)}</GameProgressHorizontalLayout>
    } else if (validParts.IsFree) {
      content = <GameProgressVerticalLayout><VerticalText value={validParts.FullText} config={config} isCjkCulture={validParts.IsCjkCulture} culture={localization?.Culture} /></GameProgressVerticalLayout>
    } else if (mode === 'Vertical') {
      content = <GameProgressVerticalLayout><VerticalText value={validParts.FullText} config={config} isCjkCulture={validParts.IsCjkCulture} culture={localization?.Culture} /></GameProgressVerticalLayout>
    } else if (mode === 'VerticalHalfOnly') {
      content = <GameProgressVerticalLayout><VerticalText value={validParts.HalfText} config={config} isCjkCulture={validParts.IsCjkCulture} culture={localization?.Culture} /></GameProgressVerticalLayout>
    } else if (mode === 'VerticalGameOnly' || mode === 'RibbonGameOnly') {
      content = <GameProgressVerticalLayout><VerticalText value={validParts.GameText} config={config} isCjkCulture={validParts.IsCjkCulture} culture={localization?.Culture} /></GameProgressVerticalLayout>
    } else if (mode === 'VerticalSeparatedGameAndHalf') {
      content = <GameProgressVerticalLayout><VerticalGroups config={config} parts={validParts} culture={localization?.Culture} separated /></GameProgressVerticalLayout>
    } else {
      content = <GameProgressVerticalLayout><VerticalGroups config={config} parts={validParts} culture={localization?.Culture} separated={false} /></GameProgressVerticalLayout>
    }
  }

  return <GameProgressRoot config={config} runtime={runtime} verticalLayout={isVertical}>{content}</GameProgressRoot>
}
