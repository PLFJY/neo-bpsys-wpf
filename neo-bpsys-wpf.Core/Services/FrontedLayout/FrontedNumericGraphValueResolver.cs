using System.Globalization;
using System.Text.Json;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 解析图中数值端口和受限数值表达式的运行时值。
/// </summary>
internal sealed class FrontedNumericGraphValueResolver(FrontedNodeGraph graph, FrontedGraphExecutionContext context)
{
    private readonly HashSet<Guid> _resolving = [];

    public bool TryResolveActionValue(FrontedNode action, string inputPort, string literal, out string value, out string error)
    {
        var incoming = graph.Connections.FirstOrDefault(connection => connection.TargetNodeId == action.NodeId && connection.TargetPort == inputPort);
        if (incoming is not null)
        {
            if (!TryResolveNode(incoming.SourceNodeId, out var numeric, out error)) { value = string.Empty; return false; }
            value = numeric.ToString("G17", CultureInfo.InvariantCulture);
            return true;
        }
        if (!literal.TrimStart().StartsWith('=')) { value = literal; error = string.Empty; return true; }
        if (!FrontedNumericExpressionEvaluator.TryEvaluate(literal, ResolvePayload, out var result, out error)) { value = string.Empty; return false; }
        value = result.ToString("G17", CultureInfo.InvariantCulture);
        return true;
    }

    private bool TryResolveNode(Guid nodeId, out double value, out string error)
    {
        value = 0; error = string.Empty;
        if (!_resolving.Add(nodeId)) { error = "Numeric value graph contains a cycle."; return false; }
        try
        {
            var node = graph.FindNode(nodeId);
            if (node is null) { error = "Numeric value source node is missing."; return false; }
            switch (node.NodeType)
            {
                case "value.number":
                    return TryNumber(node.Properties.GetValueOrDefault("Value"), out value, out error);
                case "value.eventContext":
                    var path = GetString(node, "Path");
                    var resolved = ResolvePayload(path);
                    if (resolved.Found) { value = resolved.Value; return true; }
                    return TryNumber(node.Properties.GetValueOrDefault("FallbackValue"), out value, out error);
                case "math.negate": return Unary(node, value => -value, out value, out error);
                case "math.abs": return Unary(node, Math.Abs, out value, out error);
                case "math.round": return Unary(node, Math.Round, out value, out error);
                case "math.floor": return Unary(node, Math.Floor, out value, out error);
                case "math.ceil": return Unary(node, Math.Ceiling, out value, out error);
                case "math.add": return Binary(node, (left, right) => left + right, out value, out error);
                case "math.subtract": return Binary(node, (left, right) => left - right, out value, out error);
                case "math.multiply": return Binary(node, (left, right) => left * right, out value, out error);
                case "math.min": return Binary(node, Math.Min, out value, out error);
                case "math.max": return Binary(node, Math.Max, out value, out error);
                case "math.divide": return Binary(node, (left, right) => right == 0 ? throw new DivideByZeroException("Division by zero.") : left / right, out value, out error);
                case "math.modulo": return Binary(node, (left, right) => right == 0 ? throw new DivideByZeroException("Division by zero.") : left % right, out value, out error);
                case "math.clamp":
                    if (!Input(node, "Value", out var input, out error) || !Input(node, "Min", out var min, out error) || !Input(node, "Max", out var max, out error)) return false;
                    value = Math.Clamp(input, min, max); return EnsureFinite(ref value, out error);
                default: error = $"Node '{node.NodeType}' does not produce a numeric value."; return false;
            }
        }
        catch (Exception exception) when (exception is DivideByZeroException or InvalidOperationException)
        { error = exception.Message; return false; }
        finally { _resolving.Remove(nodeId); }
    }

    private bool Unary(FrontedNode node, Func<double, double> operation, out double value, out string error)
    { if (!Input(node, "Value", out var input, out error)) { value = 0; return false; } value = operation(input); return EnsureFinite(ref value, out error); }
    private bool Binary(FrontedNode node, Func<double, double, double> operation, out double value, out string error)
    { if (!Input(node, "Left", out var left, out error) || !Input(node, "Right", out var right, out error)) { value = 0; return false; } value = operation(left, right); return EnsureFinite(ref value, out error); }
    private bool Input(FrontedNode node, string port, out double value, out string error)
    { var connection = graph.Connections.FirstOrDefault(item => item.TargetNodeId == node.NodeId && item.TargetPort == port); if (connection is null) { value = 0; error = $"Numeric input '{port}' is not connected."; return false; } return TryResolveNode(connection.SourceNodeId, out value, out error); }
    private (bool Found, double Value) ResolvePayload(string path)
    {
        var payload = path.StartsWith("StartEvent.", StringComparison.Ordinal) ? context.StartEventPayload : path.StartsWith("StopEvent.", StringComparison.Ordinal) ? context.StopEventPayload : context.EventPayload;
        var key = path.Contains('.') ? path[(path.IndexOf('.') + 1)..] : path;
        if (!payload.TryGetValue(key, out var raw) && !payload.TryGetValue(path, out raw)) return (false, 0);
        return TryConvert(raw, out var value) ? (true, value) : (false, 0);
    }
    private static bool TryNumber(JsonElement element, out double value, out string error) { value = 0; error = string.Empty; if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value)) return true; if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value)) return true; error = "Numeric constant is invalid."; return false; }
    private static bool TryConvert(object? raw, out double value)
    {
        value = 0;
        return raw is not null
               && double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value);
    }
    private static string GetString(FrontedNode node, string key) => node.Properties.TryGetValue(key, out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString() : string.Empty;
    private static bool EnsureFinite(ref double value, out string error) { error = string.Empty; if (double.IsFinite(value)) return true; error = "Numeric result must be finite."; return false; }
}
