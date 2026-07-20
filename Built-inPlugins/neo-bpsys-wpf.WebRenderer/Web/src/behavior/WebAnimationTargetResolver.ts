export class WebAnimationTargetResolver {
  resolve(target: unknown, self: string, layer: unknown): HTMLElement | null {
    const text = typeof target === 'string' ? target.trim() : 'Self'
    let guid = self
    let part: string | undefined
    let name: string | undefined
    if (text.startsWith('part:')) {
      const [, id, ...rest] = text.split(':')
      guid = id
      part = rest.join(':')
    } else if (text.startsWith('guid:')) guid = text.slice(5)
    else if (text.startsWith('name:')) name = text.slice(5)
    else if (text !== 'Self' && /^[0-9a-f-]{36}$/i.test(text)) guid = text
    else if (text !== 'Self') name = text

    if (name) {
      const named = document.querySelector<HTMLElement>(`[data-runtime-name="${CSS.escape(name)}"], [data-control-name="${CSS.escape(name)}"]`)
      if (named?.hasAttribute('data-control-root')) return this.layer(named, layer)
      return named
    }
    const root = document.querySelector<HTMLElement>(`[data-control-root][data-behavior-guid="${CSS.escape(guid)}"]`)
    if (!root) return null
    if (part) return root.querySelector<HTMLElement>(`[data-animation-part="${CSS.escape(part)}"]`)
    return this.layer(root, layer)
  }

  private layer(root: HTMLElement, layer: unknown): HTMLElement {
    const effective = String(layer ?? 'Auto')
    if (effective === 'Content') return root.querySelector<HTMLElement>(':scope > [data-behavior-content]') ?? root
    if (effective === 'OverlayAbove') return root.querySelector<HTMLElement>(':scope > [data-overlay-above]') ?? root
    if (effective === 'OverlayBelow') return root.querySelector<HTMLElement>(':scope > [data-overlay-below]') ?? root
    return root
  }
}
