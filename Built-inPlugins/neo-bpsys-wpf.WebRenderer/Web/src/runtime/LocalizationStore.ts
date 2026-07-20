import type { WebLocalizationSnapshot } from '../protocol/bootstrap'

/** 原子保存主程序推送的完整本地化快照。 */
export class LocalizationStore {
  snapshot: WebLocalizationSnapshot | null = null

  /** 应用 schema 正确且不早于当前 revision 的完整快照。 */
  apply(value: unknown): boolean {
    if (!isWebLocalizationSnapshot(value)) return false
    if (this.snapshot && this.snapshot.Revision >= value.Revision) return false
    this.snapshot = value
    return true
  }
}

/** 验证 Web Renderer 本地化 schema，不推断或补全资源字典。 */
export function isWebLocalizationSnapshot(value: unknown): value is WebLocalizationSnapshot {
  const item = value as WebLocalizationSnapshot | undefined
  return !!item && item.SchemaVersion === 1 && Number.isInteger(item.Revision) && item.Revision > 0
    && typeof item.Culture === 'string' && !!item.StaticTexts && typeof item.StaticTexts === 'object'
    && !!item.MapV2Texts && typeof item.MapV2Texts === 'object'
}
