using neo_bpsys_wpf.Core.Abstractions.Services;
using System.Collections;
using System.Reflection;

namespace neo_bpsys_wpf.Core.Services.FrontedLayout;

internal static class FrontedBindingPathValidator
{
    public static bool TryValidateDynamicPath(string path, out string? error)
    {
        error = null;
        if (!FrontedBindingPathParser.ContainsDynamicIndexer(path))
        {
            return true;
        }

        if (!FrontedBindingPathParser.TryParse(path, out var parsed, out var parseError))
        {
            error = $"at character {parseError!.Position}: {parseError.Message}";
            return false;
        }

        return TryResolve(parsed, typeof(ISharedDataService), out _, out error);
    }

    private static bool TryResolve(FrontedBindingPath path, Type rootType, out Type resultType, out string? error)
    {
        var currentType = rootType;
        foreach (var segment in path.Segments)
        {
            if (segment is FrontedPropertyPathSegment propertySegment)
            {
                var property = currentType.GetProperty(propertySegment.Name, BindingFlags.Instance | BindingFlags.Public);
                if (property is null || !property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    resultType = typeof(object);
                    error = $"at character {propertySegment.Position} ('{propertySegment.Name}'): property does not exist on {currentType.Name}.";
                    return false;
                }
                currentType = GetCoreType(property.PropertyType);
                continue;
            }

            var indexer = GetIndexer(currentType);
            if (indexer is null)
            {
                resultType = typeof(object);
                error = $"at character {segment.Position}: {currentType.Name} is not indexable.";
                return false;
            }

            Type indexValueType;
            if (segment is FrontedDynamicIndexerPathSegment dynamicSegment)
            {
                if (!TryResolve(dynamicSegment.Path, typeof(ISharedDataService), out indexValueType, out var indexError))
                {
                    resultType = typeof(object);
                    error = $"at character {segment.Position + 1} ('{dynamicSegment.Text}'): {indexError}";
                    return false;
                }
            }
            else if (segment is FrontedLiteralIndexerPathSegment literalSegment)
            {
                indexValueType = literalSegment.Value.GetType();
            }
            else
            {
                resultType = typeof(object);
                error = $"at character {segment.Position}: unsupported indexer.";
                return false;
            }

            if (!CanUseAsIndex(indexValueType, indexer.Value.IndexType))
            {
                resultType = typeof(object);
                error = $"at character {segment.Position}: index value type {indexValueType.Name} cannot be used as {indexer.Value.IndexType.Name}.";
                return false;
            }

            currentType = GetCoreType(indexer.Value.ValueType);
        }

        resultType = currentType;
        error = null;
        return true;
    }

    private static (Type IndexType, Type ValueType)? GetIndexer(Type type)
    {
        type = GetCoreType(type);
        if (type.IsArray)
        {
            return (typeof(int), type.GetElementType()!);
        }
        foreach (var candidate in type.GetInterfaces().Append(type))
        {
            if (!candidate.IsGenericType)
            {
                continue;
            }

            var definition = candidate.GetGenericTypeDefinition();
            var arguments = candidate.GetGenericArguments();
            if (definition == typeof(IList<>) || definition == typeof(IReadOnlyList<>))
            {
                return (typeof(int), arguments[0]);
            }
            if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
            {
                return (arguments[0], arguments[1]);
            }
        }

        if (typeof(IList).IsAssignableFrom(type))
        {
            return (typeof(int), typeof(object));
        }
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            return (typeof(object), typeof(object));
        }

        var indexer = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property => property.CanRead && property.GetIndexParameters() is [var parameter]);
        return indexer is null ? null : (indexer.GetIndexParameters()[0].ParameterType, indexer.PropertyType);
    }

    private static bool CanUseAsIndex(Type valueType, Type indexType)
    {
        valueType = GetCoreType(valueType);
        indexType = GetCoreType(indexType);
        if (indexType == typeof(object) || indexType.IsAssignableFrom(valueType))
        {
            return true;
        }
        if (indexType.IsEnum)
        {
            return valueType.IsEnum || IsIntegral(valueType) || valueType == typeof(string);
        }
        return IsIntegral(indexType) && (IsIntegral(valueType) || valueType.IsEnum)
               || indexType == typeof(string) && valueType == typeof(string);
    }

    private static bool IsIntegral(Type type) => Type.GetTypeCode(GetCoreType(type)) is TypeCode.Byte or TypeCode.SByte
        or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64;

    private static Type GetCoreType(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}
