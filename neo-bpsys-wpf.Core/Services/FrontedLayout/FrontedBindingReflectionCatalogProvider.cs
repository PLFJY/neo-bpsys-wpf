using CommunityToolkit.Mvvm.ComponentModel;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Binding;
using neo_bpsys_wpf.Core.Models.FrontedLayout.Designer;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

public sealed class FrontedBindingReflectionCatalogProvider : IFrontedBindingCatalogProvider
{
    private readonly IFrontedBindingRootProvider _rootProvider;
    private readonly IEnumerable<IFrontedBindingCatalogContributor> _contributors;
    private IReadOnlyList<FrontedBindingTreeNode>? _cache;

    public FrontedBindingReflectionCatalogProvider()
        : this(new DefaultFrontedBindingRootProvider(), [])
    {
    }

    public FrontedBindingReflectionCatalogProvider(
        IFrontedBindingRootProvider rootProvider,
        IEnumerable<IFrontedBindingCatalogContributor> contributors)
    {
        _rootProvider = rootProvider;
        _contributors = contributors;
    }

    public IReadOnlyList<FrontedBindingTreeNode> BuildCatalog()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var rootChildren = _rootProvider.GetRoots()
            .Select(BuildRoot)
            .Concat(_contributors.SelectMany(contributor => contributor.BuildNodes()))
            .ToArray();

        _cache =
        [
            new FrontedBindingTreeNode
            {
                DisplayName = "ISharedDataService",
                TypeName = FormatTypeName(typeof(ISharedDataService)),
                ValueType = typeof(ISharedDataService),
                Children = rootChildren
            }
        ];
        return _cache;
    }

    private FrontedBindingTreeNode BuildRoot(FrontedBindingRootDescriptor root)
    {
        var children = BuildCollectionChildren(
            root.Name,
            root.ValueType,
            root.FixedCount,
            root.KnownKeys,
            0,
            new HashSet<Type>())
            ?? BuildTypeChildren(root.Name, root.ValueType, 0, new HashSet<Type>());

        return CreateNode(root.Name, root.Name, root.ValueType, children);
    }

    private IReadOnlyList<FrontedBindingTreeNode> BuildTypeChildren(
        string prefix,
        Type type,
        int depth,
        ISet<Type> visitedTypes)
    {
        type = GetCoreType(type);
        var objectAttribute = type.GetCustomAttribute<FrontedBindingObjectAttribute>(inherit: true);
        if (objectAttribute is null || depth >= objectAttribute.MaxDepth || !visitedTypes.Add(type))
        {
            return [];
        }

        try
        {
            if (!objectAttribute.IncludePublicProperties)
            {
                return BuildForcedProperties(prefix, type, depth, visitedTypes);
            }

            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => ShouldIncludeProperty(property, parentAutoIncludes: true))
                .Select(property => BuildPropertyNode(prefix, property, depth + 1, visitedTypes))
                .Where(node => node is not null)
                .Cast<FrontedBindingTreeNode>()
                .ToArray();
        }
        finally
        {
            visitedTypes.Remove(type);
        }
    }

    private IReadOnlyList<FrontedBindingTreeNode> BuildForcedProperties(
        string prefix,
        Type type,
        int depth,
        ISet<Type> visitedTypes) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => ShouldIncludeProperty(property, parentAutoIncludes: false))
            .Select(property => BuildPropertyNode(prefix, property, depth + 1, visitedTypes))
            .Where(node => node is not null)
            .Cast<FrontedBindingTreeNode>()
            .ToArray();

    private FrontedBindingTreeNode? BuildPropertyNode(
        string parentPath,
        PropertyInfo property,
        int depth,
        ISet<Type> visitedTypes)
    {
        var bindable = property.GetCustomAttribute<FrontedBindableAttribute>(inherit: true);
        var path = $"{parentPath}.{property.Name}";
        var propertyType = property.PropertyType;
        var collection = property.GetCustomAttribute<FrontedBindingCollectionAttribute>(inherit: true);
        var collectionChildren = BuildCollectionChildren(
            path,
            propertyType,
            collection?.FixedCount is > -1 ? collection.FixedCount : null,
            collection?.KnownKeys,
            depth,
            visitedTypes);

        if (collectionChildren is not null)
        {
            return CreateNode(property.Name, path, propertyType, collectionChildren);
        }

        if (IsLeafType(propertyType) || bindable?.IncludeChildren == false)
        {
            return CreateNode(property.Name, path, propertyType, []);
        }

        var children = BuildTypeChildren(path, propertyType, depth, visitedTypes);
        if (children.Count == 0 && bindable is null && !IsLeafType(propertyType))
        {
            return null;
        }

        return CreateNode(property.Name, path, propertyType, children);
    }

    private IReadOnlyList<FrontedBindingTreeNode>? BuildCollectionChildren(
        string path,
        Type type,
        int? fixedCount,
        IReadOnlyList<string>? knownKeys,
        int depth,
        ISet<Type> visitedTypes)
    {
        var dictionaryValueType = GetDictionaryValueType(type);
        if (dictionaryValueType is not null)
        {
            if (knownKeys is null || knownKeys.Count == 0)
            {
                return [];
            }

            return knownKeys
                .Select(key => BuildIndexedNode($"[{key}]", $"{path}[{EscapeDictionaryKey(key)}]", dictionaryValueType, depth, visitedTypes))
                .ToArray();
        }

        var itemType = GetEnumerableItemType(type);
        if (itemType is null)
        {
            return null;
        }

        if (fixedCount is null or < 0)
        {
            return [];
        }

        return Enumerable.Range(0, fixedCount.Value)
            .Select(index => BuildIndexedNode($"[{index}]", $"{path}[{index}]", itemType, depth, visitedTypes))
            .ToArray();
    }

    private FrontedBindingTreeNode BuildIndexedNode(
        string displayName,
        string path,
        Type valueType,
        int depth,
        ISet<Type> visitedTypes)
    {
        var children = IsLeafType(valueType)
            ? []
            : BuildTypeChildren(path, valueType, depth + 1, visitedTypes);

        return CreateNode(displayName, path, valueType, children);
    }

    private static bool ShouldIncludeProperty(PropertyInfo property, bool parentAutoIncludes)
    {
        if (property.GetCustomAttribute<FrontedBindingIgnoreAttribute>(inherit: true) is not null)
        {
            return false;
        }

        if (property.GetIndexParameters().Length > 0 || property.GetMethod is null || !property.GetMethod.IsPublic)
        {
            return false;
        }

        // Exclude IsActive inherited from ObservableRecipient (internal messenger state, not fronted binding data)
        if (property is { Name: nameof(ObservableRecipient.IsActive) }
            && property.DeclaringType == typeof(ObservableRecipient))
        {
            return false;
        }

        var type = GetCoreType(property.PropertyType);
        if (typeof(Delegate).IsAssignableFrom(type) || typeof(ICommand).IsAssignableFrom(type))
        {
            return false;
        }

        return parentAutoIncludes || property.GetCustomAttribute<FrontedBindableAttribute>(inherit: true) is not null;
    }

    private static FrontedBindingTreeNode CreateNode(
        string displayName,
        string? path,
        Type valueType,
        IReadOnlyList<FrontedBindingTreeNode> children) =>
        new()
        {
            DisplayName = displayName,
            FullPath = path,
            TypeName = FormatTypeName(valueType),
            ValueType = valueType,
            Children = children
        };

    private static Type? GetEnumerableItemType(Type type)
    {
        type = GetCoreType(type);
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type.GetInterfaces()
            .Append(type)
            .Where(candidate => candidate.IsGenericType)
            .FirstOrDefault(candidate => candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private static Type? GetDictionaryValueType(Type type)
    {
        type = GetCoreType(type);
        return type.GetInterfaces()
            .Append(type)
            .Where(candidate => candidate.IsGenericType)
            .FirstOrDefault(candidate => candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                                         || candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            ?.GetGenericArguments()[1];
    }

    private static bool IsLeafType(Type type)
    {
        var coreType = GetCoreType(type);
        return coreType == typeof(string)
               || coreType == typeof(decimal)
               || coreType == typeof(DateTime)
               || coreType == typeof(TimeSpan)
               || coreType.IsPrimitive
               || coreType.IsEnum
               || typeof(ImageSource).IsAssignableFrom(coreType);
    }

    private static Type GetCoreType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static string FormatTypeName(Type type)
    {
        var nullableType = Nullable.GetUnderlyingType(type);
        var coreType = nullableType ?? type;
        if (coreType.IsGenericType)
        {
            var name = coreType.Name;
            var tickIndex = name.IndexOf('`', StringComparison.Ordinal);
            if (tickIndex >= 0)
            {
                name = name[..tickIndex];
            }

            name = $"{name}<{string.Join(", ", coreType.GetGenericArguments().Select(FormatTypeName))}>";
            return nullableType is null ? name : $"{name}?";
        }

        return nullableType is null ? coreType.Name : $"{coreType.Name}?";
    }

    private static string EscapeDictionaryKey(string key) => $"'{key.Replace("'", "\\'", StringComparison.Ordinal)}'";
}
