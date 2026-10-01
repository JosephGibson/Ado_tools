using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AdoToolkit.Commands.Infrastructure;

// PowerShell builds a parameter's type from any property bag, so [pscustomobject]@{ Id = 44 }
// binds as a toolkit object whose required members are null. This names the first such member,
// in the object itself or in a toolkit object it holds, so the command can report the input.
internal static class InputGuard
{
    private const int MaximumDepth = 4;

    // Type metadata only (which members are required or hold a toolkit object), never session state.
    private static readonly ConcurrentDictionary<Type, Member[]> Members = new();

    internal static (string Type, string Member)? FindMissing(object input) => FindMissing(input, 0);

    private static (string Type, string Member)? FindMissing(object input, int depth)
    {
        ArgumentNullException.ThrowIfNull(input);
        Type type = input.GetType();
        foreach (Member member in Members.GetOrAdd(type, Describe))
        {
            object? value;
            // A computed member of an incomplete object can fail on the value it lacks.
            try { value = member.Property.GetValue(input); }
            catch (TargetInvocationException) { return (type.Name, member.Property.Name); }
            if (value is null)
            {
                if (member.Required) return (type.Name, member.Property.Name);
            }
            else if (member.Nested && depth < MaximumDepth && FindMissing(value, depth + 1) is { } nested) return nested;
        }
        return null;
    }

    private static Member[] Describe(Type type) => [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0 && !property.PropertyType.IsValueType)
        .Select(static property => new Member(property, property.IsDefined(typeof(RequiredMemberAttribute), false),
            property.PropertyType.Assembly == typeof(AdoConnection).Assembly))
        .Where(static member => member.Required || member.Nested)];

    private sealed record Member(PropertyInfo Property, bool Required, bool Nested);
}
