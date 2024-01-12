using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Imlight.Director;

internal class ResourceContainer {
    internal ResourceContainer() {
        var baseType = typeof(RootResourceSingleton<>);
        var assembly = baseType.Assembly;

        foreach (var derivedType in assembly.GetTypes()
            .Where(t => !t.IsAbstract &&
                        !t.IsInterface &&
                        t.BaseType != null &&
                        t.BaseType.IsGenericType &&
                        t.BaseType.GetGenericTypeDefinition() == baseType)) {
            // Instantiate the derived type.
            var instance = Activator.CreateInstance(derivedType);

            // Get a delegate to the method using an expression.
            var methodDelegate = CreateDelegate<Action>(derivedType, "Initialize");

            // Invoke the method using the delegate.
            methodDelegate?.Invoke();
        }
    }

    // Helper method to create a delegate for invoking methods.
    private static TDelegate CreateDelegate<TDelegate>(Type type, string methodName)
        where TDelegate : class {
        var methodInfo = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (methodInfo == null) {
            return null;
        }

        var instance = Expression.Parameter(type, "instance");
        var methodCall = Expression.Call(instance, methodInfo);

        return Expression.Lambda<TDelegate>(methodCall, instance).Compile();
    }
}
