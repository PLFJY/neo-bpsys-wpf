#nullable enable

using System.Collections.Generic;
using System.Linq;
using neo_bpsys_wpf.Controls;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.Tests.Infrastructure;
using Xunit;

namespace neo_bpsys_wpf.Tests.Controls;

/// <summary>
/// 测试 <see cref="CharacterSelector.FindIndex"/> 在简称/全称搜索时是否尊重 <see cref="CharacterSelector.DisabledKeys"/> 排斥机制。
/// </summary>
[Collection(WpfUiCollectionDefinition.Name)]
public class CharacterSelectorFindIndexTest
{
    /// <summary>
    /// 创建测试用 Character，显式指定 abbrev/fullSpell 以保证搜索匹配可预测。
    /// </summary>
    private static Character CreateChara(string name, string abbrev, string fullSpell)
        => new(name, Camp.Sur, $"{name}.png", abbrev, fullSpell);

    /// <summary>
    /// 构建测试用字典：包含三个角色，其中 "医生" 与 "园丁" 共享 abbrev 前缀 "y"。
    /// </summary>
    private static SortedDictionary<string, Character> CreateDict()
        => new()
        {
            { "医生", CreateChara("医生", "ys", "yisheng") },
            { "园丁", CreateChara("园丁", "yd", "yuanding") },
            { "律师", CreateChara("律师", "ls", "lvshi") }
        };

    private static int IndexOfKey(SortedDictionary<string, Character> dict, string key)
        => dict.Keys.ToList().IndexOf(key);

    [Fact]
    public void FindIndex_NoDisabledKeys_FindsAbbrevMatch()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector { ItemsSource = dict, DisabledKeys = null };

            var found = selector.FindIndex("ys");

            Assert.Equal(IndexOfKey(dict, "医生"), found);
        });
    }

    [Fact]
    public void FindIndex_NoDisabledKeys_FindsFullSpellMatch()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector { ItemsSource = dict, DisabledKeys = null };

            var found = selector.FindIndex("yisheng");

            Assert.Equal(IndexOfKey(dict, "医生"), found);
        });
    }

    [Fact]
    public void FindIndex_NoDisabledKeys_FindsNameMatch()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector { ItemsSource = dict, DisabledKeys = null };

            var found = selector.FindIndex("医");

            Assert.Equal(IndexOfKey(dict, "医生"), found);
        });
    }

    [Fact]
    public void FindIndex_WhenSoleMatchIsDisabled_ReturnsMinusOne()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector
            {
                ItemsSource = dict,
                DisabledKeys = new HashSet<string> { "医生" }
            };

            // "ys" 仅匹配 "医生"，但其已被禁用 → 应返回 -1
            var found = selector.FindIndex("ys");

            Assert.Equal(-1, found);
        });
    }

    [Fact]
    public void FindIndex_WhenFirstMatchIsDisabled_ReturnsSecondMatch()
    {
        WpfTestThread.Run(() =>
        {
            // 两个角色共享相同 abbrev "ab"，确保搜索同时命中两者
            var dict = new SortedDictionary<string, Character>
            {
                { "医生", CreateChara("医生", "ab", "ab") },
                { "律师", CreateChara("律师", "ab", "ab") }
            };

            // 禁用排序在前的那个，验证搜索会跳过它并命中下一个
            var firstKey = dict.Keys.First();
            var secondKey = dict.Keys.Skip(1).First();
            var selector = new CharacterSelector
            {
                ItemsSource = dict,
                DisabledKeys = new HashSet<string> { firstKey }
            };

            var found = selector.FindIndex("ab");

            Assert.Equal(IndexOfKey(dict, secondKey), found);
        });
    }

    [Fact]
    public void FindIndex_WhenAllMatchesDisabled_ReturnsMinusOne()
    {
        WpfTestThread.Run(() =>
        {
            var dict = new SortedDictionary<string, Character>
            {
                { "医生", CreateChara("医生", "ab", "ab") },
                { "律师", CreateChara("律师", "ab", "ab") }
            };

            // 两者均匹配 "ab" 且均被禁用 → 应返回 -1
            var selector = new CharacterSelector
            {
                ItemsSource = dict,
                DisabledKeys = new HashSet<string> { "医生", "律师" }
            };

            var found = selector.FindIndex("ab");

            Assert.Equal(-1, found);
        });
    }

    [Fact]
    public void FindIndex_DisabledKeyNotMatchingSearch_DoesNotAffectResult()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector
            {
                ItemsSource = dict,
                DisabledKeys = new HashSet<string> { "律师" }
            };

            // 搜索 "ys" 匹配 "医生"；"律师" 虽被禁用但不匹配该搜索 → 不影响结果
            var found = selector.FindIndex("ys");

            Assert.Equal(IndexOfKey(dict, "医生"), found);
        });
    }

    [Fact]
    public void FindIndex_EmptyDisabledKeys_BehavesAsNoRestriction()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector
            {
                ItemsSource = dict,
                DisabledKeys = new HashSet<string>()
            };

            var found = selector.FindIndex("ys");

            Assert.Equal(IndexOfKey(dict, "医生"), found);
        });
    }

    [Fact]
    public void IsSearchError_FalseByDefault()
    {
        WpfTestThread.Run(() =>
        {
            var selector = new CharacterSelector { ItemsSource = CreateDict() };

            Assert.False(selector.IsSearchError);
        });
    }

    [Fact]
    public void IsSearchError_StaysFalse_WhenSuccessfulSelectionSetExternally()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector { ItemsSource = dict };

            // 外部将 SelectedIndex 设为有效值不应触发错误状态
            selector.SelectedIndex = IndexOfKey(dict, "医生");

            Assert.False(selector.IsSearchError);
        });
    }

    [Fact]
    public void IsSearchError_Clears_WhenSelectedIndexBecomesValid()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector
            {
                ItemsSource = dict,
                DisabledKeys = new HashSet<string> { "医生" }
            };

            // 模拟一次失败搜索：FindIndex 返回 -1 → 触发错误状态
            Assert.Equal(-1, selector.FindIndex("ys"));
            selector.SelectedIndex = -1;
            selector.IsSearchError = true;
            Assert.True(selector.IsSearchError);

            // 之后通过下拉点选等方式选中一个有效角色 → 错误应清除
            selector.SelectedIndex = IndexOfKey(dict, "律师");

            Assert.False(selector.IsSearchError);
        });
    }

    [Fact]
    public void IsSearchError_NotCleared_WhenSelectedIndexStaysInvalid()
    {
        WpfTestThread.Run(() =>
        {
            var dict = CreateDict();
            var selector = new CharacterSelector
            {
                ItemsSource = dict,
                DisabledKeys = new HashSet<string> { "医生" }
            };

            selector.IsSearchError = true;
            // SelectedIndex 仍为 -1（无效）→ 不应清除错误
            selector.SelectedIndex = -1;

            Assert.True(selector.IsSearchError);
        });
    }
}
