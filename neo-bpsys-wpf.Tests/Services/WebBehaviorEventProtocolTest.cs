using System;
using System.Collections.Generic;
using System.Linq;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.WebRenderer.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>Web Renderer 行为事件线协议测试。</summary>
public sealed class WebBehaviorEventProtocolTest
{
    /// <summary>标量、枚举和数组保持 JSON 原生语义。</summary>
    [Fact]
    public void ProjectsTypedPayloadWithoutRuntimeValueWrapping()
    {
        var message = WebBehaviorEventMessage.From(new FrontedBehaviorEvent
        {
            EventType = "Guidance.StepChanged",
            Payload = new Dictionary<string, object?>
            {
                ["Action"] = GameAction.PickSur,
                ["Indexes"] = new[] { 0, 1 },
                ["Visible"] = true,
                ["MapKey"] = "ArmsFactory",
                ["Ratio"] = 0.25,
                ["Metadata"] = (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Round"] = 3 },
                ["Nothing"] = null
            }
        });

        Assert.Equal(WebBehaviorEventMessage.CurrentSchemaVersion, message.SchemaVersion);
        Assert.Equal("PickSur", message.Payload["Action"]);
        Assert.Equal(new object?[] { 0, 1 }, Assert.IsType<object?[]>(message.Payload["Indexes"]));
        Assert.Equal(true, message.Payload["Visible"]);
        Assert.Equal("ArmsFactory", message.Payload["MapKey"]);
        Assert.Equal(0.25, message.Payload["Ratio"]);
        var metadata = Assert.IsType<Dictionary<string, object?>>(message.Payload["Metadata"]);
        Assert.Equal(3, metadata["Round"]);
        Assert.Null(message.Payload["Nothing"]);
        Assert.DoesNotContain(message.Payload.Values, value => value is WebRuntimeValue);
    }

    /// <summary>不支持的 CLR 对象被投影为 null 并产生诊断，而不会序列化对象图。</summary>
    [Fact]
    public void UnsupportedPayloadTypeIsNullAndDiagnosed()
    {
        var message = WebBehaviorEventMessage.From(new FrontedBehaviorEvent
        {
            EventType = "test",
            Payload = new Dictionary<string, object?> { ["Object"] = new UnsupportedPayload() }
        });

        Assert.Null(message.Payload["Object"]);
        Assert.Contains(message.Diagnostics, value => value.StartsWith("BehaviorPayloadUnsupportedType:test:Object:", StringComparison.Ordinal));
    }

    /// <summary>深度和集合限制阻止无限递归与过大数组。</summary>
    [Fact]
    public void PayloadLimitsAreEnforced()
    {
        object? nested = "end";
        for (var index = 0; index < WebBehaviorPayloadProjector.MaxDepth + 2; index++) nested = new[] { nested };
        var message = WebBehaviorEventMessage.From(new FrontedBehaviorEvent
        {
            EventType = "test",
            Payload = new Dictionary<string, object?>
            {
                ["Nested"] = nested,
                ["Large"] = Enumerable.Range(0, WebBehaviorPayloadProjector.MaxCollectionLength + 1).ToArray()
            }
        });

        Assert.Contains(message.Diagnostics, value => value.StartsWith("BehaviorPayloadDepthExceeded:test:Nested", StringComparison.Ordinal));
        Assert.Contains(message.Diagnostics, value => value.StartsWith("BehaviorPayloadCollectionLimitExceeded:test:Large", StringComparison.Ordinal));
    }

    private sealed class UnsupportedPayload { }
}
