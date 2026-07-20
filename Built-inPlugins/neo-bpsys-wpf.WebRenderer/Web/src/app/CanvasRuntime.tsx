import { useEffect, useState } from 'react'
import type { Bootstrap } from '../protocol/bootstrap'
import type { RuntimeState } from '../protocol/runtime'
import { finite } from '../renderer/controlTypes'
import { WebControlRegistry } from '../renderer/WebControlRegistry'
import { fontFamily, isEmbeddedFontReference } from '../renderer/fonts'
import type { WebRenderContext } from '../renderer/WebRenderContext'

export function CanvasRuntime({ bootstrap, runtime }: { bootstrap: Bootstrap; runtime: RuntimeState }) {
  const [viewport, setViewport] = useState(() => ({ width: innerWidth, height: innerHeight }))
  useEffect(() => { const update = () => setViewport({ width: innerWidth, height: innerHeight }); addEventListener('resize', update); return () => removeEventListener('resize', update) }, [])
  const layout = bootstrap.Layout!; const canvas = layout.CanvasSettings; const width = finite(canvas.CanvasWidth, 1440); const height = finite(canvas.CanvasHeight, 810); const stretch = layout.WindowSettings.ViewboxStretch ?? 'Fill'; const sx = viewport.width / width; const sy = viewport.height / height; const scale = stretch === 'None' ? [1,1] : stretch === 'Fill' ? [sx,sy] : stretch === 'UniformToFill' ? [Math.max(sx,sy),Math.max(sx,sy)] : [Math.min(sx,sy),Math.min(sx,sy)]
  const background = canvas.BackgroundImage ? bootstrap.Resources[canvas.BackgroundImage] : undefined
  const context: WebRenderContext = { canvasWidth: width, canvasHeight: height, backgroundUrl: background, backgroundRevision: canvas.BackgroundImage ? `${canvas.BackgroundImage}:${background ?? ''}` : undefined, resources: bootstrap.Resources }
  const faces = Object.entries(bootstrap.Resources).filter(([key]) => isEmbeddedFontReference(key)).map(([key,url]) => `@font-face{font-family:"${fontFamily(key)}";src:url("${url}");font-display:block;}`).join('\n')
  return <><style>{faces}</style><div className="viewport"><div className="canvas" style={{ width, height, transform: `scale(${scale[0]},${scale[1]})`, backgroundImage: background ? `url(${background})` : undefined }}>
    {Object.entries(layout.ControlLayout.Controls).map(([name, config]) => <WebControlRegistry key={name} name={name} config={config} runtime={runtime} localization={bootstrap.Localization} resources={bootstrap.Resources} context={context} behaviorSet={bootstrap.BehaviorDocument?.ControlBehaviorSets?.find(set => set.BehaviorGuid.toLowerCase() === config.BehaviorGuid?.toLowerCase())} />)}
  </div></div></>
}
