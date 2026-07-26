import { describe, expect, it, vi } from 'vitest'
import behaviorDocumentJson from '../../../../../neo-bpsys-wpf/Resources/FrontedBehaviors/BpWindow.behaviors.json'
import { WebBehaviorRuntime } from './WebBehaviorRuntime'
import type { BehaviorDocument } from './behaviorTypes'

describe('WebBehaviorRuntime flow semantics', () => {
  it('runs valid parallel branches and executes Out exactly once', async () => {
    const info = vi.spyOn(console, 'info').mockImplementation(() => undefined)
    const runtime = new WebBehaviorRuntime()
    runtime.replace({ ControlBehaviorSets: [{ BehaviorGuid: '00000000-0000-0000-0000-000000000001', Behaviors: [{
      BehaviorId: 'parallel', Kind: 'Event', Trigger: { EventType: 'test' }, Graph: {
        Nodes: [
          { NodeId: 'start', NodeType: 'flow.start' },
          { NodeId: 'parallel', NodeType: 'flow.parallel', Properties: { BranchCount: 3 } },
          { NodeId: 'branch', NodeType: 'action.log', Properties: { Message: 'branch' } },
          { NodeId: 'out', NodeType: 'action.log', Properties: { Message: 'out' } },
          { NodeId: 'end', NodeType: 'flow.end' },
        ],
        Connections: [
          { SourceNodeId: 'start', SourcePort: 'Out', TargetNodeId: 'parallel', TargetPort: 'In' },
          { SourceNodeId: 'parallel', SourcePort: 'Branch1', TargetNodeId: 'branch', TargetPort: 'In' },
          { SourceNodeId: 'parallel', SourcePort: 'Branch2', TargetNodeId: 'branch', TargetPort: 'In' },
          { SourceNodeId: 'parallel', SourcePort: 'Out', TargetNodeId: 'out', TargetPort: 'In' },
          { SourceNodeId: 'branch', SourcePort: 'Out', TargetNodeId: 'end', TargetPort: 'In' },
          { SourceNodeId: 'out', SourcePort: 'Out', TargetNodeId: 'end', TargetPort: 'In' },
        ],
      },
    }] }] })

    runtime.publish({ EventType: 'test', Payload: {} })
    await vi.waitFor(() => expect(info.mock.calls.filter(call => call[1] === 'out')).toHaveLength(1))
    expect(info.mock.calls.filter(call => call[1] === 'branch')).toHaveLength(1)
    info.mockRestore()
  })

  it('does not execute a converged downstream node more than once', async () => {
    const info = vi.spyOn(console, 'info').mockImplementation(() => undefined)
    const runtime = new WebBehaviorRuntime()
    runtime.replace({ ControlBehaviorSets: [{ BehaviorGuid: '00000000-0000-0000-0000-000000000001', Behaviors: [{
      BehaviorId: 'converged-parallel', Kind: 'Event', Trigger: { EventType: 'test' }, Graph: {
        Nodes: [
          { NodeId: 'start', NodeType: 'flow.start' },
          { NodeId: 'parallel', NodeType: 'flow.parallel', Properties: { BranchCount: 2 } },
          { NodeId: 'branch1', NodeType: 'flow.delay' },
          { NodeId: 'branch2', NodeType: 'flow.delay' },
          { NodeId: 'tail', NodeType: 'action.log', Properties: { Message: 'tail' } },
          { NodeId: 'out', NodeType: 'action.log', Properties: { Message: 'out' } },
          { NodeId: 'end', NodeType: 'flow.end' },
        ],
        Connections: [
          { SourceNodeId: 'start', SourcePort: 'Out', TargetNodeId: 'parallel', TargetPort: 'In' },
          { SourceNodeId: 'parallel', SourcePort: 'Branch1', TargetNodeId: 'branch1', TargetPort: 'In' },
          { SourceNodeId: 'parallel', SourcePort: 'Branch2', TargetNodeId: 'branch2', TargetPort: 'In' },
          { SourceNodeId: 'branch1', SourcePort: 'Out', TargetNodeId: 'tail', TargetPort: 'In' },
          { SourceNodeId: 'branch2', SourcePort: 'Out', TargetNodeId: 'tail', TargetPort: 'In' },
          { SourceNodeId: 'tail', SourcePort: 'Out', TargetNodeId: 'end', TargetPort: 'In' },
          { SourceNodeId: 'parallel', SourcePort: 'Out', TargetNodeId: 'out', TargetPort: 'In' },
          { SourceNodeId: 'out', SourcePort: 'Out', TargetNodeId: 'end', TargetPort: 'In' },
        ],
      },
    }] }] })

    runtime.publish({ EventType: 'test', Payload: {} })
    await vi.waitFor(() => expect(info.mock.calls.filter(call => call[1] === 'out')).toHaveLength(1))
    expect(info.mock.calls.filter(call => call[1] === 'tail')).toHaveLength(1)
    info.mockRestore()
  })

  it('loads the real BpWindow Exit/Enter graphs and Swipe actions', () => {
    const document = behaviorDocumentJson as unknown as BehaviorDocument
    for (const displayName of ['SurPick0', 'SurPick1', 'SurPick2', 'SurPick3', 'HunPick']) {
      const set = document.ControlBehaviorSets?.find(value => value.DisplayName === displayName)
      const transition = set?.Behaviors?.find(value => value.Name === 'CharacterPickTransition')
      expect(transition?.ExitGraph?.Nodes?.some(node => node.NodeType === 'flow.parallel')).toBe(true)
      expect(transition?.EnterGraph?.Nodes?.some(node => node.NodeType === 'flow.parallel')).toBe(true)
      expect(transition?.EnterGraph?.Nodes?.some(node => node.Properties?.PropertyName === 'Opacity')).toBe(true)
      expect(transition?.EnterGraph?.Nodes?.some(node => node.Properties?.PropertyName === 'ClipInsetLeft')).toBe(true)
      expect(transition?.EnterGraph?.Nodes?.some(node => String(node.Properties?.Target).endsWith(':Swipe'))).toBe(true)
      expect(set?.AnimationParts?.some(part => part.Name === 'Swipe' && part.HeightText === '100%')).toBe(true)
    }
  })
})
