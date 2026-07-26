const namedColors = new Set([
  'aliceblue', 'antiquewhite', 'aqua', 'aquamarine', 'azure', 'beige', 'bisque',
  'black', 'blanchedalmond', 'blue', 'blueviolet', 'brown', 'burlywood', 'cadetblue',
  'chartreuse', 'chocolate', 'coral', 'cornflowerblue', 'cornsilk', 'crimson',
  'cyan', 'darkblue', 'darkcyan', 'darkgoldenrod', 'darkgray', 'darkgreen',
  'darkgrey', 'darkkhaki', 'darkmagenta', 'darkolivegreen', 'darkorange',
  'darkorchid', 'darkred', 'darksalmon', 'darkseagreen', 'darkslateblue',
  'darkslategray', 'darkslategrey', 'darkturquoise', 'darkviolet', 'deeppink',
  'deepskyblue', 'dimgray', 'dimgrey', 'dodgerblue', 'firebrick', 'floralwhite',
  'forestgreen', 'fuchsia', 'gainsboro', 'ghostwhite', 'gold', 'goldenrod',
  'gray', 'green', 'greenyellow', 'grey', 'honeydew', 'hotpink', 'indianred',
  'indigo', 'ivory', 'khaki', 'lavender', 'lavenderblush', 'lawngreen',
  'lemonchiffon', 'lightblue', 'lightcoral', 'lightcyan', 'lightgoldenrodyellow',
  'lightgray', 'lightgreen', 'lightgrey', 'lightpink', 'lightsalmon',
  'lightseagreen', 'lightskyblue', 'lightslategray', 'lightslategrey',
  'lightsteelblue', 'lightyellow', 'lime', 'limegreen', 'linen', 'magenta',
  'maroon', 'mediumaquamarine', 'mediumblue', 'mediumorchid', 'mediumpurple',
  'mediumseagreen', 'mediumslateblue', 'mediumspringgreen', 'mediumturquoise',
  'mediumvioletred', 'midnightblue', 'mintcream', 'mistyrose', 'moccasin',
  'navajowhite', 'navy', 'oldlace', 'olive', 'olivedrab', 'orange', 'orangered',
  'orchid', 'palegoldenrod', 'palegreen', 'paleturquoise', 'palevioletred',
  'papayawhip', 'peachpuff', 'peru', 'pink', 'plum', 'powderblue', 'purple',
  'rebeccapurple', 'red', 'rosybrown', 'royalblue', 'saddlebrown', 'salmon',
  'sandybrown', 'seagreen', 'seashell', 'sienna', 'silver', 'skyblue',
  'slateblue', 'slategray', 'slategrey', 'snow', 'springgreen', 'steelblue',
  'tan', 'teal', 'thistle', 'tomato', 'turquoise', 'violet', 'wheat', 'white',
  'whitesmoke', 'yellow', 'yellowgreen', 'transparent',
])

const invalidColorDiagnostics = new Set<string>()

function reportInvalidColor(value: string) {
  if (invalidColorDiagnostics.has(value)) return
  invalidColorDiagnostics.add(value)
  console.warn(`[Web Renderer] invalid WPF color Diagnostic=InvalidWpfColor Value=${value}`)
}

function parseColor(value: string): string | undefined {
  const trimmed = value.trim()
  const argb = /^#([0-9a-f]{8})$/i.exec(trimmed)
  if (argb) {
    const hex = argb[1]
    return `rgba(${parseInt(hex.slice(2, 4), 16)}, ${parseInt(hex.slice(4, 6), 16)}, ${parseInt(hex.slice(6, 8), 16)}, ${parseInt(hex.slice(0, 2), 16) / 255})`
  }

  if (/^#[0-9a-f]{6}$/i.test(trimmed)) return trimmed
  if (namedColors.has(trimmed.toLowerCase())) return trimmed.toLowerCase()
  return undefined
}

/** Convert a WPF Brush/Color string to a CSS color without changing ARGB meaning. */
export function wpfColor(value: unknown, fallback = 'transparent'): string {
  if (typeof value !== 'string' || value.trim().length === 0) return parseColor(fallback) ?? fallback
  const parsed = parseColor(value)
  if (parsed) return parsed
  reportInvalidColor(value.trim())
  return parseColor(fallback) ?? fallback
}

/** Backwards-compatible name for the shared WPF color conversion used by renderers. */
export function color(value: unknown, fallback = 'transparent'): string {
  return wpfColor(value, fallback)
}
