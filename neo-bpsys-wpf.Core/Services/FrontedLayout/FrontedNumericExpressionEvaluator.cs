using System.Globalization;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

/// <summary>
/// 计算动画图使用的受限数值表达式。
/// </summary>
public static class FrontedNumericExpressionEvaluator
{
    /// <summary>
    /// 尝试计算以 <c>=</c> 开头的数值表达式。
    /// </summary>
    /// <param name="expression">表达式文本。</param>
    /// <param name="variableResolver">变量解析器。</param>
    /// <param name="value">成功时的有限数值。</param>
    /// <param name="error">失败原因。</param>
    /// <returns>计算是否成功。</returns>
    public static bool TryEvaluate(string expression, Func<string, (bool Found, double Value)> variableResolver, out double value, out string error)
    {
        value = 0;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(expression) || !expression.TrimStart().StartsWith('='))
        {
            error = "Numeric expressions must start with '='.";
            return false;
        }
        try
        {
            var parser = new Parser(expression.TrimStart()[1..], variableResolver);
            value = parser.Parse();
            if (!double.IsFinite(value)) throw new InvalidOperationException("Expression result must be finite.");
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or DivideByZeroException)
        {
            error = exception.Message;
            return false;
        }
    }

    private sealed class Parser(string text, Func<string, (bool Found, double Value)> variables)
    {
        private int _position;
        public double Parse() { var value = Additive(); Skip(); if (_position != text.Length) throw new FormatException("Unexpected token in numeric expression."); return value; }
        private double Additive() { var value = Multiplicative(); while (true) { Skip(); if (Take('+')) value += Multiplicative(); else if (Take('-')) value -= Multiplicative(); else return Finite(value); } }
        private double Multiplicative() { var value = Unary(); while (true) { Skip(); if (Take('*')) value *= Unary(); else if (Take('/')) { var right = Unary(); if (right == 0) throw new DivideByZeroException("Division by zero."); value /= right; } else if (Take('%')) { var right = Unary(); if (right == 0) throw new DivideByZeroException("Division by zero."); value %= right; } else return Finite(value); } }
        private double Unary() { Skip(); if (Take('+')) return Unary(); if (Take('-')) return -Unary(); return Primary(); }
        private double Primary()
        {
            Skip(); if (Take('(')) { var value = Additive(); Expect(')'); return value; }
            if (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] == '.')) return Number();
            var name = Identifier(); Skip();
            if (Take('(')) { var args = new List<double>(); Skip(); if (!Take(')')) { do { args.Add(Additive()); Skip(); } while (Take(',')); Expect(')'); } return Function(name, args); }
            var result = variables(name); if (!result.Found) throw new InvalidOperationException($"Numeric variable '{name}' is unavailable."); return Finite(result.Value);
        }
        private double Number() { var start = _position; while (_position < text.Length && (char.IsDigit(text[_position]) || text[_position] is '.' or 'e' or 'E' or '+' or '-')) { if ((text[_position] is '+' or '-') && _position > start && text[_position - 1] is not ('e' or 'E')) break; _position++; } if (!double.TryParse(text[start.._position], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw new FormatException("Invalid numeric literal."); return Finite(value); }
        private static double Function(string name, List<double> a) => name.ToLowerInvariant() switch { "abs" when a.Count == 1 => Math.Abs(a[0]), "min" when a.Count == 2 => Math.Min(a[0], a[1]), "max" when a.Count == 2 => Math.Max(a[0], a[1]), "clamp" when a.Count == 3 => Math.Clamp(a[0], a[1], a[2]), "round" when a.Count == 1 => Math.Round(a[0]), "floor" when a.Count == 1 => Math.Floor(a[0]), "ceil" when a.Count == 1 => Math.Ceiling(a[0]), _ => throw new FormatException($"Unsupported numeric function '{name}' or argument count.") };
        private string Identifier() { Skip(); var start = _position; while (_position < text.Length && (char.IsLetterOrDigit(text[_position]) || text[_position] is '_' or '.')) _position++; if (start == _position) throw new FormatException("Expected a number, variable, or function."); return text[start.._position]; }
        private void Skip() { while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++; }
        private bool Take(char token) { if (_position < text.Length && text[_position] == token) { _position++; return true; } return false; }
        private void Expect(char token) { if (!Take(token)) throw new FormatException($"Expected '{token}'."); }
        private static double Finite(double value) => double.IsFinite(value) ? value : throw new InvalidOperationException("Expression result must be finite.");
    }
}
