import { TextVisual } from '../TextVisual'
import type { RuntimeState } from '../../protocol/runtime'
import type { TextBinding, TextConfig } from '../controlTypes'

function format(template: string, values: string[]): string { return template.replace(/\{(\d+)(?::([^}]+))?\}/g, (_, index) => values[Number(index)] ?? '') }
export function resolveBinding(binding: TextBinding | undefined, runtime: RuntimeState): string | undefined {
  const sources = binding?.Sources?.filter(source => typeof source.Path === 'string' && source.Path.length > 0) ?? []
  if (!sources.length) return undefined
  const missing = sources.some(source => runtime.values[source.Path!] === undefined)
  if (missing && binding?.FallbackText !== undefined) return binding.FallbackText
  const values = sources.map(source => { const value = runtime.values[source.Path!]; return value == null ? binding?.NullText ?? '' : String(value) })
  return binding?.StringFormat ? format(binding.StringFormat, values) : values.join(binding?.JoinSeparator ?? '')
}
export function TextRenderer({ config, runtime }: { config: TextConfig; runtime: RuntimeState }) {
  const bound = resolveBinding(config.TextBinding, runtime)
  const value = bound ?? (config.BindingPath && runtime.values[config.BindingPath] !== undefined ? String(runtime.values[config.BindingPath] ?? '') : config.Text ?? '')
  return <TextVisual config={config} runtime={runtime}>{value}</TextVisual>
}
