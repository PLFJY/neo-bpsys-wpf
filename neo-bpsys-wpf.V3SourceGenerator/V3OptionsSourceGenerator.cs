using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ReSharper disable InconsistentNaming
// 生成器内部使用 PascalCase 与下划线命名以匹配生成产物约定。

namespace neo_bpsys_wpf.V3SourceGenerator;

/// <summary>
/// Phase 8 可选 Source Generator：为标注 <c>[FrontedV3Control]</c> 的控件生成设计时 Options facade 类型，
/// 仅服务 Visual Studio XAML IntelliSense。
/// </summary>
/// <remarks>
/// <para>
/// 生成内容不参与运行时、不参与插件加载、不参与 JSON、不生成第二套属性元数据。
/// 所有生成内容来自现有 <c>FrontedV3Property&lt;T&gt;</c> 静态字段定义。
/// </para>
/// <para>
/// 生成策略：扫描控件类上所有 <c>public static readonly FrontedV3Property&lt;T&gt;</c> 字段，
/// 从构造函数首个字符串字面量提取 <c>OptionsPath</c>，按路径分段构建分层类型树，
/// 在 XAML 中通过 <c>d:DesignInstance</c> 绑定后即可获得 <c>Options.*</c> 路径补全。
/// </para>
/// <para>
/// 可靠性约束：字段初始化器必须是 <c>new FrontedV3Property&lt;T&gt;("literal", ...)</c>
/// 或 <c>new("literal", ...)</c> 形式；非字面量字段被静默跳过，不影响其他字段生成。
/// 生成失败不会影响插件运行（Generator 输出仅为 IntelliSense 提示）。
/// </para>
/// </remarks>
[Generator]
public sealed class V3OptionsSourceGenerator : IIncrementalGenerator
{
    private const string FrontedV3ControlAttributeFullName =
        "neo_bpsys_wpf.Core.Abstractions.Services.FrontedV3ControlAttribute";

    private const string FrontedV3PropertyNamespace =
        "neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties";

    private const string FrontedV3PropertyTypeName = "FrontedV3Property";

    /// <summary>
    /// 属性类型显示格式：使用 C# 关键字（如 <c>string</c> 而非 <c>System.String</c>），
    /// 包含命名空间与Nullable 标记。
    /// </summary>
    private static readonly SymbolDisplayFormat PropertyTypeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                              | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>
    /// 初始化 Generator，注册 <c>[FrontedV3Control]</c> 标注的类的源输出。
    /// </summary>
    /// <param name="context">增量 Generator 初始化上下文。</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
            FrontedV3ControlAttributeFullName,
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => ExtractControlInfo(ctx));

        var source = provider
            .Where(static c => c.HasValue)
            .Select(static (c, _) => c!.Value);

        context.RegisterSourceOutput(source, (ctx, control) => Emit(ctx, control));
    }

    /// <summary>
    /// 从 <c>[FrontedV3Control]</c> 标注的类提取控件信息：ControlId、命名空间、类名与所有可解析的属性声明。
    /// </summary>
    /// <param name="ctx">Generator 语法上下文，提供目标符号与 Attribute 数据。</param>
    /// <returns>控件信息；无可解析属性时返回 <see langword="null"/>（不生成任何源）。</returns>
    private static ControlInfo? ExtractControlInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        // 从 Attribute 构造函数提取 ControlId
        if (ctx.Attributes.Length == 0)
        {
            return null;
        }

        var attr = ctx.Attributes[0];
        if (attr.ConstructorArguments.Length < 1)
        {
            return null;
        }

        if (attr.ConstructorArguments[0].Value is not string controlId)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(controlId))
        {
            return null;
        }

        // 收集 FrontedV3Property<T> 字段
        var properties = new List<PropertyInfo>();
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not IFieldSymbol field)
            {
                continue;
            }

            if (!field.IsStatic || !field.IsReadOnly)
            {
                continue;
            }

            if (field.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            if (field.Type is not INamedTypeSymbol fieldType)
            {
                continue;
            }

            if (!IsFrontedV3PropertyGeneric(fieldType))
            {
                continue;
            }

            var propInfo = ExtractPropertyInfo(field, fieldType);
            if (propInfo is not null)
            {
                properties.Add(propInfo.Value);
            }
        }

        if (properties.Count == 0)
        {
            return null;
        }

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : classSymbol.ContainingNamespace.ToDisplayString();

        return new ControlInfo(
            controlId,
            ns,
            classSymbol.Name,
            properties.ToImmutableArray());
    }

    /// <summary>
    /// 判断类型是否为 <c>FrontedV3Property&lt;T&gt;</c>（泛型属性声明类型）。
    /// </summary>
    /// <param name="type">字段类型符号。</param>
    /// <returns>是 <c>FrontedV3Property&lt;T&gt;</c> 时返回 <see langword="true"/>。</returns>
    private static bool IsFrontedV3PropertyGeneric(INamedTypeSymbol type)
    {
        return type.IsGenericType
               && type.Name == FrontedV3PropertyTypeName
               && type.ContainingNamespace.ToDisplayString() == FrontedV3PropertyNamespace;
    }

    /// <summary>
    /// 从字段声明提取 <see cref="PropertyInfo"/>：OptionsPath（构造函数首参字面量）与属性类型 T。
    /// </summary>
    /// <param name="field">字段符号。</param>
    /// <param name="fieldType">字段的 <c>FrontedV3Property&lt;T&gt;</c> 类型。</param>
    /// <returns>属性信息；不可解析时返回 <see langword="null"/>（静默跳过）。</returns>
    private static PropertyInfo? ExtractPropertyInfo(IFieldSymbol field, INamedTypeSymbol fieldType)
    {
        if (fieldType.TypeArguments.Length < 1)
        {
            return null;
        }

        var propertyTypeSymbol = fieldType.TypeArguments[0];
        var propertyType = propertyTypeSymbol.ToDisplayString(PropertyTypeFormat);

        var optionsPath = ExtractOptionsPath(field);
        if (optionsPath is null)
        {
            return null;
        }

        return new PropertyInfo(optionsPath, propertyType);
    }

    /// <summary>
    /// 从字段初始化器提取 OptionsPath 字符串字面量。
    /// 仅识别 <c>new FrontedV3Property&lt;T&gt;("literal", ...)</c> 与 <c>new("literal", ...)</c> 形式；
    /// 非字面量（常量引用、方法调用、插值等）返回 <see langword="null"/>，字段被静默跳过。
    /// </summary>
    /// <param name="field">字段符号。</param>
    /// <returns>OptionsPath 字面量；不可提取时为 <see langword="null"/>。</returns>
    private static string? ExtractOptionsPath(IFieldSymbol field)
    {
        if (field.DeclaringSyntaxReferences.Length == 0)
        {
            return null;
        }

        if (field.DeclaringSyntaxReferences[0].GetSyntax() is not VariableDeclaratorSyntax declarator)
        {
            return null;
        }

        var initializer = declarator.Initializer;
        if (initializer is null)
        {
            return null;
        }

        var value = initializer.Value;
        BaseObjectCreationExpressionSyntax? creation = value switch
        {
            ObjectCreationExpressionSyntax obj => obj,
            ImplicitObjectCreationExpressionSyntax imp => imp,
            _ => null
        };

        if (creation?.ArgumentList is null || creation.ArgumentList.Arguments.Count < 1)
        {
            return null;
        }

        var firstArg = creation.ArgumentList.Arguments[0].Expression;
        if (firstArg is not LiteralExpressionSyntax literal)
        {
            return null;
        }

        if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return null;
        }

        return literal.Token.ValueText;
    }

    /// <summary>
    /// 生成设计时 Options facade 源代码并添加到编译输出。
    /// </summary>
    /// <param name="ctx">源生成上下文。</param>
    /// <param name="control">控件信息。</param>
    private static void Emit(SourceProductionContext ctx, ControlInfo control)
    {
        var root = BuildTree(control.Properties);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// Phase 8 Source Generator: design-time Options facade for VS XAML IntelliSense.");
        sb.AppendLine("// 不参与运行时、不参与插件加载、不参与 JSON、不生成第二套属性元数据。");
        sb.AppendLine("// 所有生成内容来自现有 FrontedV3Property 定义。");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(control.Namespace))
        {
            sb.AppendLine($"namespace {control.Namespace}");
            sb.AppendLine("{");
        }

        // 类型名前缀使用 ClassName（C# 类标识符）而非 ControlId（可能是 "Team Card"、"1Card" 等非 C# 标识符）。
        // 子类型名使用 ClassName + 完整路径（下划线连接）避免不同路径下相同 segment 产生同名类。
        var typePrefix = control.ClassName;
        GenerateDesignContextClass(sb, control, typePrefix, baseIndent: "");
        GenerateNodeClass(sb, $"{typePrefix}Options", root, typePrefix, pathPrefix: string.Empty, baseIndent: "");

        if (!string.IsNullOrEmpty(control.Namespace))
        {
            sb.AppendLine("}");
        }

        // HintName 包含 ClassName（C# 标识符），不使用 ControlId 避免非法文件名。
        var hintName = string.IsNullOrEmpty(control.Namespace)
            ? $"{control.ClassName}.Options.g.cs"
            : $"{control.Namespace.Replace('.', '_')}.{control.ClassName}.Options.g.cs";

        ctx.AddSource(hintName, sb.ToString());
    }

    /// <summary>
    /// 生成根 <c>{ClassName}DesignContext</c> 类，仅含一个返回 <c>{ClassName}Options</c> 的 <c>Options</c> 属性。
    /// </summary>
    private static void GenerateDesignContextClass(StringBuilder sb, ControlInfo control, string typePrefix, string baseIndent)
    {
        sb.AppendLine($"{baseIndent}/// <summary>");
        sb.AppendLine($"{baseIndent}/// Design-time IntelliSense facade for <see cref=\"{control.ClassName}\"/>.");
        sb.AppendLine($"{baseIndent}/// 在 XAML 中通过 d:DesignInstance 绑定以获得 Options.* 路径补全。");
        sb.AppendLine($"{baseIndent}/// </summary>");
        sb.AppendLine($"{baseIndent}public partial class {typePrefix}DesignContext");
        sb.AppendLine($"{baseIndent}{{");
        sb.AppendLine($"{baseIndent}    /// <summary>Gets the Options root for IntelliSense.</summary>");
        sb.AppendLine($"{baseIndent}    public {typePrefix}Options Options {{ get; set; }} = new {typePrefix}Options();");
        sb.AppendLine($"{baseIndent}}}");
        sb.AppendLine();
    }

    /// <summary>
    /// 按 OptionsPath 路径分段构建分层树：非叶子段作为子对象属性，末段作为叶子属性。
    /// </summary>
    /// <param name="properties">控件的所有属性声明。</param>
    /// <returns>路径树的根节点。</returns>
    private static PathNode BuildTree(ImmutableArray<PropertyInfo> properties)
    {
        var root = new PathNode();
        foreach (var prop in properties)
        {
            var segments = prop.OptionsPath.Split('.');
            var current = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var seg = segments[i];
                if (!current.Children.TryGetValue(seg, out var child))
                {
                    child = new PathNode();
                    current.Children[seg] = child;
                }

                current = child;
            }

            var leafName = segments[segments.Length - 1];
            current.LeafProperties.Add((leafName, prop.PropertyType));
        }

        return root;
    }

    /// <summary>
    /// 递归生成分层 Options 类型：当前节点的类、其子对象属性与叶子属性，再递归子节点。
    /// </summary>
    /// <param name="sb">源代码构建器。</param>
    /// <param name="className">当前节点生成的类名。</param>
    /// <param name="node">当前路径节点。</param>
    /// <param name="typePrefix">类型名前缀，通常为控件类名。</param>
    /// <param name="pathPrefix">从根到当前节点的完整路径（下划线连接），用于子类命名唯一性。</param>
    /// <param name="baseIndent">基础缩进。</param>
    private static void GenerateNodeClass(
        StringBuilder sb,
        string className,
        PathNode node,
        string typePrefix,
        string pathPrefix,
        string baseIndent)
    {
        sb.AppendLine($"{baseIndent}/// <summary>");
        sb.AppendLine($"{baseIndent}/// Design-time Options facade for {className}.");
        sb.AppendLine($"{baseIndent}/// </summary>");
        sb.AppendLine($"{baseIndent}public partial class {className}");
        sb.AppendLine($"{baseIndent}{{");

        var memberIndent = baseIndent + "    ";

        foreach (var pair in node.Children)
        {
            var seg = pair.Key;
            var child = pair.Value;
            // 子类型名使用 typePrefix + 完整路径（下划线连接）+ "Options"，
            // 确保 A.Text.Value 与 B.Text.Value 不会生成同名类。
            // 类型名中的 segment 也需要 sanitization（替换非标识符字符），
            // 但不需要 @ 前缀（类型名总带前缀，不会是单独关键字）。
            var sanitizedSeg = SanitizeForTypeName(seg);
            var childPath = string.IsNullOrEmpty(pathPrefix) ? sanitizedSeg : $"{pathPrefix}_{sanitizedSeg}";
            var childClassName = $"{typePrefix}_{childPath}Options";
            // 属性名：对 C# 关键字加 @，对非标识符 segment 进行 sanitization。
            var propertyName = SanitizeIdentifier(seg);
            sb.AppendLine($"{memberIndent}/// <summary>Gets the {seg} options group.</summary>");
            sb.AppendLine($"{memberIndent}public {childClassName} {propertyName} {{ get; set; }} = new {childClassName}();");
        }

        foreach (var leaf in node.LeafProperties)
        {
            var propName = SanitizeIdentifier(leaf.Name);
            var propType = leaf.Type;
            sb.AppendLine($"{memberIndent}/// <summary>Design-time placeholder for {leaf.Name}.</summary>");
            sb.AppendLine($"{memberIndent}public {propType} {propName} {{ get; set; }} = default!;");
        }

        sb.AppendLine($"{baseIndent}}}");
        sb.AppendLine();

        foreach (var pair in node.Children)
        {
            var seg = pair.Key;
            var child = pair.Value;
            var sanitizedSeg = SanitizeForTypeName(seg);
            var childPath = string.IsNullOrEmpty(pathPrefix) ? sanitizedSeg : $"{pathPrefix}_{sanitizedSeg}";
            var childClassName = $"{typePrefix}_{childPath}Options";
            GenerateNodeClass(sb, childClassName, child, typePrefix, childPath, baseIndent);
        }
    }

    /// <summary>
    /// 将 OptionsPath segment 转换为合法 C# 标识符：对关键字加 <c>@</c> 前缀，
    /// 对非标识符字符替换为下划线并以 <c>_</c> 开头确保不以数字开头。
    /// </summary>
    /// <param name="segment">OptionsPath 的某一段。</param>
    /// <returns>合法的 C# 标识符。</returns>
    private static string SanitizeIdentifier(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return "_";
        }

        var result = new StringBuilder(segment.Length);
        foreach (var c in segment)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
            else
            {
                result.Append('_');
            }
        }

        // 不以数字开头；空结果或数字开头加下划线前缀。
        if (result.Length == 0 || char.IsDigit(result[0]))
        {
            result.Insert(0, '_');
        }

        var identifier = result.ToString();

        // C# 关键字加 @ 前缀（允许作为属性名使用）。
        if (IsCSharpKeyword(identifier))
        {
            return "@" + identifier;
        }

        return identifier;
    }

    /// <summary>
    /// 将 OptionsPath segment 转换为可用于类型名的合法标识符片段：
    /// 替换非标识符字符为下划线，但不加 <c>@</c> 前缀，也不为数字开头加 <c>_</c> 前缀
    /// （类型名总带有前缀如 <c>{ClassName}_</c>，整体类型名不会以数字开头；
    /// 关键字作为片段不会与 C# 关键字冲突）。
    /// </summary>
    /// <param name="segment">OptionsPath 的某一段。</param>
    /// <returns>可用于类型名的 sanitized 标识符片段。</returns>
    private static string SanitizeForTypeName(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return "_";
        }

        var result = new StringBuilder(segment.Length);
        foreach (var c in segment)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
            else
            {
                result.Append('_');
            }
        }

        // 仅在结果为空时占位；不为数字开头加 _ 前缀，
        // 因为 segment 是类型名片段（如 TestControl_1CardOptions），整体类型名总以 ClassName 开头。
        if (result.Length == 0)
        {
            result.Insert(0, '_');
        }

        return result.ToString();
    }

    /// <summary>
    /// 判断字符串是否为 C# 关键字。
    /// </summary>
    /// <param name="value">要检查的字符串。</param>
    /// <returns>是 C# 关键字时返回 <see langword="true"/>。</returns>
    private static bool IsCSharpKeyword(string value)
    {
        return value switch
        {
            "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or "catch"
            or "char" or "checked" or "class" or "const" or "continue" or "decimal" or "default"
            or "delegate" or "do" or "double" or "else" or "enum" or "event" or "explicit"
            or "extern" or "false" or "finally" or "fixed" or "float" or "for" or "foreach"
            or "goto" or "if" or "implicit" or "in" or "int" or "interface" or "internal"
            or "is" or "lock" or "long" or "namespace" or "new" or "null" or "object"
            or "operator" or "out" or "override" or "params" or "private" or "protected"
            or "public" or "readonly" or "ref" or "return" or "sbyte" or "sealed" or "short"
            or "sizeof" or "stackalloc" or "static" or "string" or "struct" or "switch"
            or "this" or "throw" or "true" or "try" or "typeof" or "uint" or "ulong"
            or "unchecked" or "unsafe" or "ushort" or "using" or "virtual" or "void"
            or "volatile" or "while" => true,
            _ => false
        };
    }

    private readonly struct ControlInfo(
        string controlId,
        string @namespace,
        string className,
        ImmutableArray<PropertyInfo> properties)
    {
        public string ControlId => controlId;
        public string Namespace => @namespace;
        public string ClassName => className;
        public ImmutableArray<PropertyInfo> Properties => properties;
    }

    private readonly struct PropertyInfo(string optionsPath, string propertyType)
    {
        public string OptionsPath => optionsPath;
        public string PropertyType => propertyType;
    }

    private sealed class PathNode
    {
        public Dictionary<string, PathNode> Children { get; } = new();

        public List<(string Name, string Type)> LeafProperties { get; } = new();
    }
}
