import type { RuntimeStore } from '../runtime/RuntimeStore'

export type TransitionCommitRequirement = {
  correlationId: string
  requiredGeneration: number
  requiredSequence: number
}

export async function waitForTransitionCommit(store: RuntimeStore, requirement: TransitionCommitRequirement, timeoutMs = 5000): Promise<boolean> {
  const applied = await store.waitFor(requirement.requiredGeneration, requirement.requiredSequence, timeoutMs)
  console.info(`[Web Renderer] transition commit barrier. correlationId=${requirement.correlationId} requiredGeneration=${requirement.requiredGeneration} requiredSequence=${requirement.requiredSequence} appliedGeneration=${store.state.generation} appliedSequence=${store.state.sequence} result=${applied ? 'applied' : 'fail-open'}`)
  return applied
}
