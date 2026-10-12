using System.Reflection;
using System.Reflection.Emit;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class FeatureDependencyTests
{
    private const string ApplicationPrefix = "CodigoActivo.Application.";

    private static readonly string[] SharedSlices = ["Abstractions", "Common"];

    private static readonly Dictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => unchecked((ushort)code.Value));

    private const BindingFlags Declared =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    private static Dictionary<string, HashSet<string>> SliceDependencies()
    {
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var type in typeof(IQuery<>).Assembly.GetTypes())
        {
            if (SliceOf(type) is not { } slice)
            {
                continue;
            }

            var used = dependencies.TryGetValue(slice, out var existing)
                ? existing
                : dependencies[slice] = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in TypesUsedBy(type).SelectMany(Expand))
            {
                if (SliceOf(target) is { } other && other != slice)
                {
                    used.Add(other);
                }
            }
        }

        return dependencies;
    }

    private static string? SliceOf(Type type)
    {
        var ns = type.Namespace;
        if (ns is null || !ns.StartsWith(ApplicationPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = ns[ApplicationPrefix.Length..];
        var end = rest.IndexOf('.', StringComparison.Ordinal);
        return end < 0 ? rest : rest[..end];
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.HasElementType)
        {
            return Expand(type.GetElementType()!);
        }

        return type.IsGenericType && !type.IsGenericTypeDefinition
            ? type.GetGenericArguments().SelectMany(Expand).Prepend(type.GetGenericTypeDefinition())
            : [type];
    }

    private static IEnumerable<Type> TypesUsedBy(Type type)
    {
        var attributes = type.GetMembers(Declared)
            .Prepend(type)
            .SelectMany(member => member.CustomAttributes);
        foreach (var used in attributes.SelectMany(AttributeTypes))
        {
            yield return used;
        }

        foreach (var constraint in ConstraintsOf(type.GetGenericArguments()))
        {
            yield return constraint;
        }

        if (type.BaseType is { } baseType)
        {
            yield return baseType;
        }

        foreach (var contract in type.GetInterfaces())
        {
            yield return contract;
        }

        foreach (var field in type.GetFields(Declared))
        {
            yield return field.FieldType;
        }

        foreach (var property in type.GetProperties(Declared))
        {
            yield return property.PropertyType;
        }

        var methods = type.GetMethods(Declared)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(Declared));
        foreach (var method in methods)
        {
            var parameters = method.GetParameters().ToList();
            if (method is MethodInfo info)
            {
                yield return info.ReturnType;
                parameters.Add(info.ReturnParameter);
                foreach (var constraint in ConstraintsOf(info.GetGenericArguments()))
                {
                    yield return constraint;
                }
            }

            foreach (var parameter in parameters)
            {
                yield return parameter.ParameterType;
                foreach (var used in parameter.CustomAttributes.SelectMany(AttributeTypes))
                {
                    yield return used;
                }
            }

            foreach (var used in TypesInBody(method))
            {
                yield return used;
            }
        }
    }

    private static IEnumerable<Type> ConstraintsOf(Type[] arguments)
    {
        return arguments
            .Where(argument => argument.IsGenericParameter)
            .SelectMany(argument => argument.GetGenericParameterConstraints());
    }

    private static IEnumerable<Type> AttributeTypes(CustomAttributeData attribute)
    {
        return attribute
            .ConstructorArguments.Concat(
                attribute.NamedArguments.Select(argument => argument.TypedValue)
            )
            .SelectMany(ArgumentTypes)
            .Prepend(attribute.AttributeType);
    }

    private static IEnumerable<Type> ArgumentTypes(CustomAttributeTypedArgument argument)
    {
        IEnumerable<Type> types = [argument.ArgumentType];
        return argument.Value switch
        {
            Type value => types.Append(value),
            IEnumerable<CustomAttributeTypedArgument> items => types.Concat(
                items.SelectMany(ArgumentTypes)
            ),
            _ => types,
        };
    }

    private static IEnumerable<Type> TypesInBody(MethodBase method)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            yield break;
        }

        foreach (var local in body.LocalVariables)
        {
            yield return local.LocalType;
        }

        var caught = body.ExceptionHandlingClauses.Where(clause =>
            clause.Flags == ExceptionHandlingClauseOptions.Clause
        );
        foreach (var clause in caught)
        {
            yield return clause.CatchType!;
        }

        var il = body.GetILAsByteArray() ?? [];
        var position = 0;
        while (position < il.Length)
        {
            ushort value = il[position++];
            if (value == 0xFE)
            {
                value = (ushort)(0xFE00 | il[position++]);
            }

            var code = OpCodesByValue[value];
            if (
                code.OperandType
                is OperandType.InlineField
                    or OperandType.InlineMethod
                    or OperandType.InlineTok
                    or OperandType.InlineType
            )
            {
                foreach (var used in Resolve(method, BitConverter.ToInt32(il, position)))
                {
                    yield return used;
                }
            }

            position += OperandSize(code.OperandType, il, position);
        }
    }

    private static IEnumerable<Type> Resolve(MethodBase method, int token)
    {
        var member = method.Module.ResolveMember(
            token,
            method.DeclaringType is { IsGenericType: true } owner
                ? owner.GetGenericArguments()
                : null,
            method.IsGenericMethod ? method.GetGenericArguments() : null
        );
        return member switch
        {
            Type type => [type],
            FieldInfo field => [field.DeclaringType!, field.FieldType],
            MethodInfo called => [called.DeclaringType!, .. called.GetGenericArguments()],
            MethodBase called => [called.DeclaringType!],
            _ => [],
        };
    }

    private static int OperandSize(OperandType operand, byte[] il, int position)
    {
        return operand switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget
            or OperandType.ShortInlineI
            or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
            _ => 4,
        };
    }

    [Fact]
    public void SharedSlicesNeverDependOnFeatures()
    {
        var offenders = SliceDependencies()
            .Where(entry => SharedSlices.Contains(entry.Key))
            .SelectMany(entry =>
                entry
                    .Value.Where(target => !SharedSlices.Contains(target))
                    .Select(target => $"{entry.Key} -> {target}")
            )
            .ToList();

        SliceDependencies().Should().ContainKeys(SharedSlices);
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void SliceDependenciesAlwaysSeeSignaturesAndMethodBodies()
    {
        var dependencies = SliceDependencies();

        dependencies["Users"].Should().Contain("Accounts");
        dependencies["Participation"].Should().Contain("Activities");
    }

    [Fact]
    public void AccountsNeverDependOnUsers()
    {
        SliceDependencies()["Accounts"].Should().NotContain("Users");
    }

    [Fact]
    public void FeaturesNeverDependOnEachOtherInACycle()
    {
        var dependencies = SliceDependencies();

        var offenders = dependencies
            .SelectMany(entry =>
                entry
                    .Value.Where(target => Reaches(dependencies, target, entry.Key))
                    .Select(target => $"{entry.Key} -> {target}")
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static bool Reaches(
        Dictionary<string, HashSet<string>> dependencies,
        string from,
        string target
    )
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(dependencies.GetValueOrDefault(from) ?? []);
        while (pending.TryPop(out var current))
        {
            if (current == target)
            {
                return true;
            }

            if (visited.Add(current))
            {
                foreach (var next in dependencies.GetValueOrDefault(current) ?? [])
                {
                    pending.Push(next);
                }
            }
        }

        return false;
    }
}
