using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

/// <summary>
/// 测试 <see cref="neo_bpsys_wpf.V3SourceGenerator.V3OptionsSourceGenerator"/> 的标识符 sanitization
/// 与类型名生成规则，覆盖非 C# 标识符 ControlId、关键字 OptionsPath segment、相同 segment 不同路径冲突场景。
/// </summary>
/// <remarks>
/// <para>
/// 这些测试验证 Designer V3 验收 Round-3 P2 的契约：
/// <list type="bullet">
/// <item>类型名前缀使用 <c>classSymbol.Name</c> 而非 ControlId，避免 "Team Card"、"1Card" 等非 C# 标识符生成非法类型。</item>
/// <item>OptionsPath segment 为 C# 关键字时属性名加 <c>@</c> 前缀。</item>
/// <item>子类型名使用 ClassName + 完整路径（下划线连接），避免 <c>A.Text.Value</c> 与 <c>B.Text.Value</c> 生成同名类。</item>
/// <item>非标识符字符（空格、连字符、点）被替换为下划线。</item>
/// <item>数字开头的 segment 加下划线前缀。</item>
/// </list>
/// </para>
/// <para>
/// 测试通过 <see cref="CSharpGeneratorDriver"/> 执行 Generator 并断言生成源代码的内容。
/// 不需要 WPF 视觉树，因此不使用 <see cref="Infrastructure.WpfTestThread"/>。
/// </para>
/// </remarks>
public class V3OptionsSourceGeneratorIdentifierTest
{
    private const string FrontedV3PropertySource = """
        using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;

        namespace neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties
        {
            public sealed class FrontedV3Property<T>
            {
                public FrontedV3Property(string optionsPath, object? metadata = null) { }
            }
        }
        """;

    private const string FrontedV3ControlAttributeSource = """
        namespace neo_bpsys_wpf.PluginSdk
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public sealed class FrontedV3ControlAttribute : System.Attribute
            {
                public FrontedV3ControlAttribute(string controlId) { }
                public bool IsBuiltIn { get; set; }
            }
        }
        """;

    // -------------------------------------------------------------------
    // 1. ControlId with space uses ClassName for type prefix
    // -------------------------------------------------------------------

    /// <summary>
    /// ControlId 包含空格时（如 "Team Card"），生成的类型名必须使用类名（如 "TeamCardControl"）
    /// 而非 ControlId，避免生成非法 C# 类型名。
    /// </summary>
    [Fact]
    public void Emit_UsesClassNameInsteadOfControlIdForTypePrefix()
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            namespace TestPlugin
            {
                [neo_bpsys_wpf.PluginSdk.FrontedV3Control("Team Card")]
                public sealed class TeamCardControl
                {
                    public static readonly FrontedV3Property<string> TextProperty =
                        new("Content.Text");
                }
            }
            """;

        var generated = RunGenerator(source)[0];

        // 类型名使用 ClassName 而非 ControlId（"Team Card" 不是合法标识符）
        Assert.Contains("public partial class TeamCardControlDesignContext", generated.Source);
        Assert.Contains("public partial class TeamCardControlOptions", generated.Source);
        Assert.DoesNotContain("Team CardDesignContext", generated.Source);
        Assert.DoesNotContain("Team CardOptions", generated.Source);
    }

    // -------------------------------------------------------------------
    // 2. C# keyword segment gets @ prefix
    // -------------------------------------------------------------------

    /// <summary>
    /// OptionsPath segment 为 C# 关键字（如 "event"、"class"）时，属性名必须加 <c>@</c> 前缀
    /// 以生成合法的 C# 属性声明。
    /// </summary>
    [Theory]
    [InlineData("event")]
    [InlineData("class")]
    [InlineData("static")]
    [InlineData("namespace")]
    public void Emit_KeywordSegmentGetsAtPrefix(string keyword)
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            namespace TestPlugin
            {
                [neo_bpsys_wpf.PluginSdk.FrontedV3Control("Test")]
                public sealed class TestControl
                {
                    public static readonly FrontedV3Property<string> ValueProperty =
                        new("{{keyword}}.Value");
                }
            }
            """;

        var generated = RunGenerator(source)[0];

        // 属性名加 @ 前缀
        Assert.Contains($"public TestControl_{keyword}Options @{keyword} {{ get; set; }}", generated.Source);
    }

    // -------------------------------------------------------------------
    // 3. Same segment under different paths generates unique type names
    // -------------------------------------------------------------------

    /// <summary>
    /// 相同 segment 出现在不同路径下（如 <c>A.Text.Value</c> 与 <c>B.Text.Value</c>）时，
    /// 生成的子类型名必须唯一，避免重复类型声明导致编译失败。
    /// </summary>
    [Fact]
    public void Emit_SameSegmentUnderDifferentPathsGeneratesUniqueTypeNames()
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            namespace TestPlugin
            {
                [neo_bpsys_wpf.PluginSdk.FrontedV3Control("Test")]
                public sealed class TestControl
                {
                    public static readonly FrontedV3Property<string> FirstProperty =
                        new("A.Text.Value");
                    public static readonly FrontedV3Property<string> SecondProperty =
                        new("B.Text.Value");
                }
            }
            """;

        var generated = RunGenerator(source)[0];

        // 两个不同的子类型名
        Assert.Contains("public partial class TestControl_A_TextOptions", generated.Source);
        Assert.Contains("public partial class TestControl_B_TextOptions", generated.Source);
    }

    // -------------------------------------------------------------------
    // 4. Non-identifier characters are sanitized
    // -------------------------------------------------------------------

    /// <summary>
    /// OptionsPath segment 包含非标识符字符（如连字符、点）时，必须替换为下划线生成合法 C# 属性名。
    /// </summary>
    [Fact]
    public void Emit_NonIdentifierCharactersAreSanitized()
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            namespace TestPlugin
            {
                [neo_bpsys_wpf.PluginSdk.FrontedV3Control("Test")]
                public sealed class TestControl
                {
                    public static readonly FrontedV3Property<string> ValueProperty =
                        new("Custom-Group.Value");
                }
            }
            """;

        var generated = RunGenerator(source)[0];

        // 连字符被替换为下划线
        Assert.Contains("public TestControl_Custom_GroupOptions Custom_Group", generated.Source);
    }

    // -------------------------------------------------------------------
    // 5. Segment starting with digit gets underscore prefix
    // -------------------------------------------------------------------

    /// <summary>
    /// OptionsPath segment 以数字开头时（如 "1Card"），属性名必须加下划线前缀确保合法 C# 标识符。
    /// </summary>
    [Fact]
    public void Emit_DigitLeadingSegmentGetsUnderscorePrefix()
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            namespace TestPlugin
            {
                [neo_bpsys_wpf.PluginSdk.FrontedV3Control("Test")]
                public sealed class TestControl
                {
                    public static readonly FrontedV3Property<string> ValueProperty =
                        new("1Card.Value");
                }
            }
            """;

        var generated = RunGenerator(source)[0];

        // 数字开头加下划线前缀
        Assert.Contains("public TestControl_1CardOptions _1Card", generated.Source);
    }

    // -------------------------------------------------------------------
    // 6. HintName uses ClassName not ControlId
    // -------------------------------------------------------------------

    /// <summary>
    /// HintName 必须使用 ClassName 而非 ControlId，避免 ControlId 包含非法文件名字符（如空格、连字符）。
    /// </summary>
    [Fact]
    public void Emit_HintNameUsesClassNameNotControlId()
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            namespace TestPlugin
            {
                [neo_bpsys_wpf.PluginSdk.FrontedV3Control("Team Card")]
                public sealed class TeamCardControl
                {
                    public static readonly FrontedV3Property<string> TextProperty =
                        new("Content.Text");
                }
            }
            """;

        var generated = RunGenerator(source)[0];

        // HintName 包含 ClassName 和命名空间
        Assert.Equal("TestPlugin.TeamCardControl.Options.g.cs", generated.HintName);
    }

    // -------------------------------------------------------------------
    // 7. Global namespace control still generates valid hint name
    // -------------------------------------------------------------------

    /// <summary>
    /// 控件位于全局命名空间时，HintName 必须只使用 ClassName，不包含命名空间前缀。
    /// </summary>
    [Fact]
    public void Emit_GlobalNamespaceControlHasValidHintName()
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            [neo_bpsys_wpf.PluginSdk.FrontedV3Control("Test")]
            public sealed class TestControl
            {
                public static readonly FrontedV3Property<string> TextProperty =
                    new("Content.Text");
            }
            """;

        var generated = RunGenerator(source)[0];

        Assert.Equal("TestControl.Options.g.cs", generated.HintName);
    }

    // -------------------------------------------------------------------
    // 8. ControlId starting with digit still generates valid types
    // -------------------------------------------------------------------

    /// <summary>
    /// ControlId 以数字开头时（如 "1Card"），类型名必须使用 ClassName 而非 ControlId，
    /// 因为 "1CardOptions" 不是合法 C# 标识符。
    /// </summary>
    [Fact]
    public void Emit_DigitLeadingControlIdUsesClassNameForTypes()
    {
        var source = $$"""
            {{FrontedV3PropertySource}}
            {{FrontedV3ControlAttributeSource}}

            namespace TestPlugin
            {
                [neo_bpsys_wpf.PluginSdk.FrontedV3Control("1Card")]
                public sealed class FirstCardControl
                {
                    public static readonly FrontedV3Property<string> TextProperty =
                        new("Content.Text");
                }
            }
            """;

        var generated = RunGenerator(source)[0];

        // 类型名使用 ClassName 而非 "1Card"
        Assert.Contains("public partial class FirstCardControlDesignContext", generated.Source);
        Assert.Contains("public partial class FirstCardControlOptions", generated.Source);
        Assert.DoesNotContain("1CardDesignContext", generated.Source);
        Assert.DoesNotContain("1CardOptions", generated.Source);
    }

    /// <summary>
    /// 执行 Source Generator 并返回所有生成源代码。
    /// </summary>
    /// <param name="source">输入源代码。</param>
    /// <returns>生成源代码列表（HintName + Source）。</returns>
    private static (string HintName, string Source)[] RunGenerator(string source)
    {
        // 需要引用 System.Runtime 以支持基础类型解析
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new neo_bpsys_wpf.V3SourceGenerator.V3OptionsSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        // 确保没有编译诊断错误（Generator 输入本身应该合法）
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        var result = driver.GetRunResult();
        var generated = result.GeneratedTrees
            .Select(tree => (tree.FilePath, tree.GetText().ToString()))
            .ToArray();

        // 调试：如果生成树为空，给出诊断信息
        if (generated.Length == 0)
        {
            var generatorDiags = result.Diagnostics;
            // 输出输出编译的所有诊断（可能包含 Generator 内部错误）
            var outputDiags = outputCompilation.GetDiagnostics();
            throw new Xunit.Sdk.XunitException(
                $"Source Generator 未产生任何输出。Diagnostics: {string.Join(", ", diagnostics.Select(d => d.ToString()))}. " +
                $"Generator Diagnostics: {string.Join(", ", generatorDiags.Select(d => d.ToString()))}. " +
                $"Output Compilation Diagnostics: {string.Join(", ", outputDiags.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        return generated!;
    }
}
