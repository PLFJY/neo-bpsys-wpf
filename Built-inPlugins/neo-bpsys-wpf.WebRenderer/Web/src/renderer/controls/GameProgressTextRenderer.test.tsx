import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { GameProgressTextRenderer } from './GameProgressTextRenderer'
import { displayModeNames, resolveDisplayMode, resolveVerticalLanguageMode, rotationDegrees, segmentGraphemes } from './GameProgressLayout'
import type { GameProgressConfig } from '../controlTypes'
import type { RuntimeState, WebGameProgressDisplayState } from '../../protocol/runtime'

const config = (overrides: Partial<GameProgressConfig> = {}): GameProgressConfig => ({ ControlType: 'GameProgressText', FontSize: 28, FontWeight: 'SemiBold', ...overrides })
const runtime = (parts: Partial<WebGameProgressDisplayState> = {}): RuntimeState => ({
  values: { progress: { IsValid: true, IsFree: false, GameNumber: 1, IsOvertime: false, Half: 'First', FullText: 'GAME 1 FIRST HALF', GameText: 'GAME 1', HalfText: 'FIRST HALF', IsCjkCulture: false, ...parts } },
  sequence: 1,
  generation: 1,
  localizationRevision: 1,
})

describe('GameProgress layout semantics', () => {
  it('maps all persisted display mode values without changing their numeric contract', () => {
    expect(displayModeNames.map((_, index) => resolveDisplayMode(index))).toEqual(displayModeNames)
    expect(resolveDisplayMode('RibbonGameOnly')).toBe('RibbonGameOnly')
    expect(resolveDisplayMode(999)).toBe('Inline')
  })

  it('selects WPF vertical language modes and directions', () => {
    expect(resolveVerticalLanguageMode(config({ VerticalLanguageMode: 'Auto' }), true)).toBe('Upright')
    expect(resolveVerticalLanguageMode(config({ VerticalLanguageMode: 'Auto', LatinVerticalMode: 'RotateBlock' }), false)).toBe('RotateBlock')
    expect(resolveVerticalLanguageMode(config({ VerticalLanguageMode: 'Auto', LatinVerticalMode: 'StackCharacters' }), false)).toBe('StackCharacters')
    expect(rotationDegrees('FacingLeft')).toBe(-90)
    expect(rotationDegrees('FacingRight')).toBe(90)
  })

  it('uses grapheme clusters and preserves whitespace as characters', () => {
    expect(segmentGraphemes('自由 对局')).toEqual(['自', '由', ' ', '对', '局'])
    expect(segmentGraphemes('👨‍👩‍👧‍👦')).toEqual(['👨‍👩‍👧‍👦'])
  })

  it('uses FullText for Free Game in every vertical display mode', () => {
    for (const mode of ['Vertical', 'VerticalTwoLine', 'VerticalHalfOnly', 'VerticalGameOnly', 'VerticalGameAndHalf', 'VerticalSeparatedGameAndHalf', 'RibbonGameOnly'] as const) {
      const html = renderToStaticMarkup(<GameProgressTextRenderer controlId="progress" config={config({ DisplayMode: mode })} runtime={runtime({ IsFree: true, FullText: 'FREE GAME', GameText: 'WRONG GAME', HalfText: 'WRONG HALF', IsCjkCulture: false })} />)
      expect(html).toContain('FREE GAME')
      expect(html).not.toContain('WRONG GAME')
      expect(html).not.toContain('WRONG HALF')
    }
  })

  it('centers CJK characters and applies spacing only between characters', () => {
    const html = renderToStaticMarkup(<GameProgressTextRenderer controlId="progress" config={config({ DisplayMode: 'Vertical', VerticalLanguageMode: 'Upright', VerticalTextSpacing: 6, FontFamily: undefined })} runtime={runtime({ FullText: '自由对局', GameText: '自由', HalfText: '对局', IsCjkCulture: true })} />)
    expect(html).toContain('data-game-progress-root')
    expect(html).toContain('data-game-progress-content-slot')
    expect(html).toContain('position:absolute;inset:0')
    expect(html).toContain('display:flex;align-items:center;justify-content:center;width:100%;height:100%')
    expect(html).toContain('font-family:system-ui, &quot;Segoe UI&quot;, sans-serif')
    expect(html.match(/margin-bottom:6px/g)).toHaveLength(3)
    expect(html).toContain('margin-bottom:0')
  })

  it('keeps the vertical layout in the full content slot while the character column stays intrinsic', () => {
    const html = renderToStaticMarkup(<GameProgressTextRenderer controlId="progress" config={config({ DisplayMode: 'Vertical', VerticalLanguageMode: 'Upright' })} runtime={runtime({ FullText: '自由对局', IsCjkCulture: true })} />)
    expect(html).toContain('data-game-progress-vertical-layout="true" style="display:flex;align-items:center;justify-content:center;width:100%;height:100%;min-width:0;min-height:0"')
    expect(html).toContain('data-game-progress-vertical="Upright" style="display:flex;flex-direction:column;align-items:center;width:max-content;height:max-content"')
  })

  it('builds final-size groups and separator from the vertical layout', () => {
    const html = renderToStaticMarkup(<GameProgressTextRenderer controlId="progress" config={config({ DisplayMode: 'VerticalSeparatedGameAndHalf', VerticalLanguageMode: 'RotateBlock', VerticalDirection: 'FacingRight', GroupSpacing: 12, ShowSeparator: true })} runtime={runtime()} />)
    expect(html).toContain('data-game-progress-separated-groups')
    expect(html).toContain('data-game-progress-separator')
    expect(html).toContain('width:100%')
    expect(html).toContain('margin:6px 0')
    expect(html).toContain('data-game-progress-rotation="90"')
  })

  it('keeps horizontal modes in the dedicated horizontal root', () => {
    const html = renderToStaticMarkup(<GameProgressTextRenderer controlId="progress" config={config({ DisplayMode: 'TwoLine', HorizontalAlignment: 'Center', VerticalAlignment: 'Center', TextAlignment: 'Center', PaddingLeft: 4, PaddingTop: 5 })} runtime={runtime()} />)
    expect(html).toContain('data-game-progress-horizontal-layout')
    expect(html).toContain('justify-content:center')
    expect(html).toContain('align-items:center')
    expect(html).toContain('text-align:center')
    expect(html).toContain('padding:5px 0px 0px 4px')
  })
})
