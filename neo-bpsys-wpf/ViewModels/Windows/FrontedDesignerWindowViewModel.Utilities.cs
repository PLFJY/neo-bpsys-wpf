using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using neo_bpsys_wpf.Core;
using neo_bpsys_wpf.Core.Abstractions;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Helpers;
using neo_bpsys_wpf.Core.Messages;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Behaviors;
using neo_bpsys_wpf.Core.Models.FrontedLayout;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.Properties;
using neo_bpsys_wpf.Core.Models.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Core.Models.ScoreSystem;
using neo_bpsys_wpf.Core.Services.FrontedLayout;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Geometry;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.Parts;
using neo_bpsys_wpf.Core.Services.FrontedLayout.V3.StyleTransfer;
using neo_bpsys_wpf.Helpers;
using neo_bpsys_wpf.Services.FrontedDesigner;
using neo_bpsys_wpf.ViewModels.FrontedDesigner;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace neo_bpsys_wpf.ViewModels.Windows;

/// <summary>
/// Fronted Designer 的通用值转换与显示名解析辅助逻辑。
/// </summary>
public partial class FrontedDesignerWindowViewModel
{
    private static FrontedLayoutValidationMessage CreateMessage(
        FrontedLayoutValidationSeverity severity,
        string code,
        string message)
    {
        return new FrontedLayoutValidationMessage
        {
            Severity = severity,
            Code = code,
            Message = message
        };
    }

    private static bool TryConvertPropertyValue(
        PropertyInfo property,
        object? value,
        out object? convertedValue,
        out string errorMessage)
    {
        convertedValue = null;
        errorMessage = string.Empty;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);

        if (Nullable.GetUnderlyingType(property.PropertyType) is not null
            && string.IsNullOrWhiteSpace(text))
        {
            convertedValue = null;
            return true;
        }

        if (property.DeclaringType == typeof(GlobalScoreCellConfig)
            && property.PropertyType == typeof(string)
            && property.Name is nameof(GlobalScoreCellConfig.FontFamily)
                or nameof(GlobalScoreCellConfig.FontWeight)
                or nameof(GlobalScoreCellConfig.Color)
            && string.IsNullOrWhiteSpace(text))
        {
            convertedValue = null;
            return true;
        }

        try
        {
            if (targetType == typeof(string))
            {
                if (IsColorProperty(property.Name))
                {
                    if (!FrontedPropertyColorHelper.TryParseArgbColor(text, out var color))
                    {
                        errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
                        return false;
                    }

                    convertedValue = FrontedPropertyColorHelper.ToArgbString(color);
                }
                else
                {
                    convertedValue = text;
                }
            }
            else if (targetType == typeof(bool))
            {
                convertedValue = value is bool boolValue
                    ? boolValue
                    : bool.Parse(text ?? string.Empty);
            }
            else if (targetType.IsEnum)
            {
                convertedValue = value?.GetType() == targetType
                    ? value
                    : Enum.Parse(targetType, text ?? string.Empty, ignoreCase: true);
            }
            else if (targetType == typeof(int))
            {
                convertedValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(long))
            {
                convertedValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(float))
            {
                convertedValue = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(decimal))
            {
                convertedValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }
            else if (targetType == typeof(double))
            {
                var doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
                    return false;
                }

                convertedValue = NormalizeDoubleProperty(property.Name, doubleValue);
            }
            else
            {
                errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
                return false;
            }

            return true;
        }
        catch (Exception)
        {
            errorMessage = I18nHelper.GetLocalizedString(AppI18nDictionaries.Designer, "PropertyValidationErrors");
            return false;
        }
    }

    private static double NormalizeDoubleProperty(string propertyName, double value)
    {
        if (propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top))
        {
            return FrontedDesignerGeometryHelper.Snap(value);
        }

        if (propertyName is nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height)
            or "ImageWidth"
            or "ImageHeight")
        {
            return Math.Max(
                FrontedDesignerGeometryHelper.MinResizeWidth,
                FrontedDesignerGeometryHelper.Snap(value));
        }

        return value;
    }

    private static bool TryParsePositiveDouble(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && double.IsFinite(value)
               && value > 0D;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is double leftDouble && right is double rightDouble)
        {
            return Math.Abs(leftDouble - rightDouble) < 0.0001D;
        }

        return Equals(left, right);
    }

    private static bool IsGeometryProperty(string propertyName)
    {
        return propertyName is nameof(FrontedControlConfigBase.Left)
            or nameof(FrontedControlConfigBase.Top)
            or nameof(FrontedControlConfigBase.Width)
            or nameof(FrontedControlConfigBase.Height);
    }

    private static bool IsColorProperty(string propertyName)
    {
        return propertyName.EndsWith("Color", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("Foreground", StringComparison.OrdinalIgnoreCase)
               || propertyName.Equals("Background", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAbsoluteFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("bpui://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Path.IsPathRooted(Environment.ExpandEnvironmentVariables(value));
    }

    private void RebuildWindowOptions(string? preserveSelectedWindowTypeName)
    {
        _isRefreshingWindowOptions = !string.IsNullOrWhiteSpace(preserveSelectedWindowTypeName);
        try
        {
            WindowOptions.Clear();
            foreach (var entry in _layoutCatalog.GetEntries()
                         .Where(entry => entry.IsMigrated && entry.IsEditable))
            {
                WindowOptions.Add(new FrontedDesignerWindowOption(
                    entry.CanonicalWindowId,
                    ResolveEntryDisplayName(entry)));
            }

            var nextSelection = string.IsNullOrWhiteSpace(preserveSelectedWindowTypeName)
                ? WindowOptions.FirstOrDefault()
                : WindowOptions.FirstOrDefault(option => string.Equals(
                      option.WindowTypeName,
                      preserveSelectedWindowTypeName,
                      StringComparison.Ordinal))
                  ?? WindowOptions.FirstOrDefault();

            SelectedWindow = nextSelection;
        }
        finally
        {
            _isRefreshingWindowOptions = false;
        }
    }

    private string ResolveWindowOptionDisplayName(string windowTypeName)
    {
        var entry = _layoutCatalog.GetEntries()
            .FirstOrDefault(item => string.Equals(item.CanonicalWindowId, windowTypeName, StringComparison.Ordinal));
        return entry is null
            ? _localizationService.GetWindowDisplayName(windowTypeName)
            : ResolveEntryDisplayName(entry);
    }

    private string ResolveEntryDisplayName(FrontedDesignerLayoutCatalogEntry entry)
    {
        var settings = _settingsHostService?.Settings;
        var language = settings?.Language ?? LanguageKey.System;
        var cultureInfo = settings?.CultureInfo;

        if (_displayNamesByWindowId.TryGetValue(entry.CanonicalWindowId, out var displayNames)
            && displayNames.Count > 0)
        {
            return FrontedWindowDisplayNameResolver.ResolveDisplayName(
                displayNames,
                language,
                cultureInfo,
                entry.DisplayName);
        }

        // 自定义窗口的注册名来自布局 JSON；初始化阶段不能同步等待异步文件读取。
        if (entry.CanonicalWindowId.StartsWith(
                FrontedV3LayoutWindowPathHelper.CustomPrefix,
                StringComparison.Ordinal))
        {
            return entry.DisplayName;
        }

        // 历史 JSON 不含 DisplayNames 时，按旧资源回退解析显示名。
        if (entry.IsBuiltIn)
        {
            var localized = _localizationService.GetWindowDisplayName(entry.CanonicalWindowId);
            if (!string.IsNullOrWhiteSpace(localized)
                && !string.Equals(localized, entry.CanonicalWindowId, StringComparison.Ordinal))
            {
                return localized;
            }
        }

        return entry.DisplayName;
    }
}
