using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        "neo_bpsys_wpf.PluginSdk.FrontedV3ControlAttribute";

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

        GenerateDesignContextClass(sb, control, baseIndent: "");
        GenerateNodeClass(sb, control.ControlId + "Options", root, control.ControlId, baseIndent: "");

        if (!string.IsNullOrEmpty(control.Namespace))
        {
            sb.AppendLine("}");
        }

        var hintName = $"{control.ClassName}.Options.g.cs";
        ctx.AddSource(hintName, sb.ToString());
    }

    /// <summary>
    /// 生成根 <c>{ControlId}DesignContext</c> 类，仅含一个返回 <c>{ControlId}Options</c> 的 <c>Options</c> 属性。
    /// </summary>
    private static void GenerateDesignContextClass(StringBuilder sb, ControlInfo control, string baseIndent)
    {
        sb.AppendLine($"{baseIndent}/// <summary>");
        sb.AppendLine($"{baseIndent}/// Design-time IntelliSense facade for <see cref=\"{control.ClassName}\"/>.");
        sb.AppendLine($"{baseIndent}/// 在 XAML 中通过 d:DesignInstance 绑定以获得 Options.* 路径补全。");
        sb.AppendLine($"{baseIndent}/// </summary>");
        sb.AppendLine($"{baseIndent}public partial class {control.ControlId}DesignContext");
        sb.AppendLine($"{baseIndent}{{");
        sb.AppendLine($"{baseIndent}    /// <summary>Gets the Options root for IntelliSense.</summary>");
        sb.AppendLine($"{baseIndent}    public {control.ControlId}Options Options {{ get; set; }} = new {control.ControlId}Options();");
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
    /// <param name="controlId">控件 ControlId，用于子类命名前缀。</param>
    /// <param name="baseIndent">基础缩进。</param>
    private static void GenerateNodeClass(
        StringBuilder sb,
        string className,
        PathNode node,
        string controlId,
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
            var childClassName = $"{controlId}{seg}Options";
            sb.AppendLine($"{memberIndent}/// <summary>Gets the {seg} options group.</summary>");
            sb.AppendLine($"{memberIndent}public {childClassName} {seg} {{ get; set; }} = new {childClassName}();");
        }

        foreach (var leaf in node.LeafProperties)
        {
            var propName = leaf.Name;
            var propType = leaf.Type;
            sb.AppendLine($"{memberIndent}/// <summary>Design-time placeholder for {propName}.</summary>");
            sb.AppendLine($"{memberIndent}public {propType} {propName} {{ get; set; }} = default!;");
        }

        sb.AppendLine($"{baseIndent}}}");
        sb.AppendLine();

        foreach (var pair in node.Children)
        {
            var seg = pair.Key;
            var child = pair.Value;
            var childClassName = $"{controlId}{seg}Options";
            GenerateNodeClass(sb, childClassName, child, controlId, baseIndent);
        }
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
