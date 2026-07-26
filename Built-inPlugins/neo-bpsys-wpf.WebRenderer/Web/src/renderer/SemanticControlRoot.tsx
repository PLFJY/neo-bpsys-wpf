import type { CSSProperties, ReactNode } from 'react'

/** Shared semantic DOM root used by all controls addressable by Behavior Runtime. */
export function SemanticControlRoot({ name, behaviorGuid, className, style, attributes, children }: {
  name: string
  behaviorGuid?: string
  className?: string
  style?: CSSProperties
  attributes?: Record<string, string | undefined>
  children: ReactNode
}) {
  return <div className={className} data-control-root data-control data-control-name={name} data-runtime-name={name} data-behavior-guid={behaviorGuid ?? ''} style={style} {...attributes}>{children}</div>
}
