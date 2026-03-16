namespace nirmata.Agents.Tests.Contracts;

using System.Reflection;
using Xunit;

/// <summary>
/// Utility for validating that fake implementations correctly implement their interfaces.
/// Uses reflection to verify method signatures, return types, and parameter compatibility.
/// </summary>
public static class FakeValidator
{
    /// <summary>
    /// Validates that a fake type correctly implements its interface contract.
    /// </summary>
    /// <typeparam name="TFake">The fake implementation type.</typeparam>
    /// <typeparam name="TInterface">The interface type that should be implemented.</typeparam>
    /// <exception cref=" Xunit.Sdk.XunitException">Thrown when validation fails with details about the mismatch.</exception>
    public static void ValidateFake<TFake, TInterface>()
        where TFake : class
        where TInterface : class
    {
        var fakeType = typeof(TFake);
        var interfaceType = typeof(TInterface);

        Assert.True(interfaceType.IsInterface, $"{interfaceType.Name} must be an interface");
        Assert.True(interfaceType.IsAssignableFrom(fakeType), $"{fakeType.Name} must implement {interfaceType.Name}");

        var interfaceMethods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var fakeMethods = fakeType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToDictionary(m => GetMethodSignature(m), m => m);

        foreach (var interfaceMethod in interfaceMethods)
        {
            ValidateMethodImplementation(fakeType, interfaceMethod, fakeMethods);
        }

        var interfaceProperties = interfaceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var interfaceProperty in interfaceProperties)
        {
            ValidatePropertyImplementation(fakeType, interfaceProperty);
        }
    }

    private static void ValidateMethodImplementation(Type fakeType, MethodInfo interfaceMethod, Dictionary<string, MethodInfo> fakeMethods)
    {
        var expectedSignature = GetMethodSignature(interfaceMethod);

        if (!fakeMethods.TryGetValue(expectedSignature, out var fakeMethod))
        {
            var inheritedMethod = fakeType.GetMethod(interfaceMethod.Name, GetBindingFlagsForMethod(interfaceMethod));
            if (inheritedMethod == null || inheritedMethod.DeclaringType == interfaceMethod.DeclaringType)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Method '{interfaceMethod.Name}' with signature '{expectedSignature}' not found in {fakeType.Name}. " +
                    $"Ensure the fake implements all interface methods.");
            }
            fakeMethod = inheritedMethod;
        }

        Assert.True(
            fakeMethod.ReturnType == interfaceMethod.ReturnType ||
            (interfaceMethod.ReturnType.IsAssignableFrom(fakeMethod.ReturnType)),
            $"Return type mismatch for '{interfaceMethod.Name}': expected {interfaceMethod.ReturnType.Name}, got {fakeMethod.ReturnType.Name}");

        var interfaceParams = interfaceMethod.GetParameters();
        var fakeParams = fakeMethod.GetParameters();

        Assert.Equal(interfaceParams.Length, fakeParams.Length);

        for (int i = 0; i < interfaceParams.Length; i++)
        {
            var interfaceParam = interfaceParams[i];
            var fakeParam = fakeParams[i];

            Assert.Equal(interfaceParam.Name, fakeParam.Name);
            Assert.True(
                fakeParam.ParameterType == interfaceParam.ParameterType ||
                interfaceParam.ParameterType.IsAssignableFrom(fakeParam.ParameterType),
                $"Parameter type mismatch for '{interfaceMethod.Name}.{interfaceParam.Name}': expected {interfaceParam.ParameterType.Name}, got {fakeParam.ParameterType.Name}");
        }
    }

    private static void ValidatePropertyImplementation(Type fakeType, PropertyInfo interfaceProperty)
    {
        var fakeProperty = fakeType.GetProperty(interfaceProperty.Name, BindingFlags.Public | BindingFlags.Instance);

        Assert.True(fakeProperty != null, $"Property '{interfaceProperty.Name}' not found in {fakeType.Name}");

        if (interfaceProperty.CanRead)
        {
            Assert.True(fakeProperty!.CanRead, $"Property '{interfaceProperty.Name}' should have a getter");
        }

        if (interfaceProperty.CanWrite)
        {
            Assert.True(fakeProperty!.CanWrite, $"Property '{interfaceProperty.Name}' should have a setter");
        }

        Assert.True(
            fakeProperty!.PropertyType == interfaceProperty.PropertyType ||
            interfaceProperty.PropertyType.IsAssignableFrom(fakeProperty.PropertyType),
            $"Property type mismatch for '{interfaceProperty.Name}': expected {interfaceProperty.PropertyType.Name}, got {fakeProperty.PropertyType.Name}");
    }

    private static string GetMethodSignature(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var paramTypes = parameters.Select(p => p.ParameterType.Name);
        return $"{method.Name}({string.Join(",", paramTypes)})";
    }

    private static BindingFlags GetBindingFlagsForMethod(MethodInfo method)
    {
        var parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
        return BindingFlags.Public | BindingFlags.Instance;
    }
}
