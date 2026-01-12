using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using neo_bpsys_wpf.Core.Enums;
using Wpf.Ui.Controls;

namespace neo_bpsys_wpf.Core.Attributes;

[System.AttributeUsage(AttributeTargets.Class)]
public class BackendPageInfo(
    string id,
    string name,
    SymbolRegular icon = SymbolRegular.Person532,
    BackendPageCategory category = BackendPageCategory.External)
    : Attribute
{
    public string Name { get; } = name;

    public string Id { get; } = id;

    public SymbolRegular Icon { get; } = icon;

    public BackendPageCategory Category { get; } = category;

    public Type? PageType { get; internal set; }
}