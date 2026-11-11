using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Jellyfin.Plugin.RatingsRecs.Hooks;

/// <summary>
/// Calls the public <c>PluginInterface</c> of another plugin through reflection, as File Transformation,
/// Home Screen Sections and Plugin Pages document: their assemblies live in other load contexts and cannot
/// be referenced. The JSON payload is turned into the method's parameter type (Newtonsoft <c>JObject</c>)
/// through its static <c>Parse(string)</c>, so this plugin needs no Newtonsoft dependency.
/// </summary>
public static class PluginInterop
{
    /// <summary>
    /// Determines whether a plugin assembly is loaded.
    /// </summary>
    /// <param name="assemblyName">The assembly simple name.</param>
    /// <returns><c>true</c> when loaded.</returns>
    public static bool IsLoaded(string assemblyName) => Find(assemblyName) is not null;

    /// <summary>
    /// Invokes a static method that takes one JSON object.
    /// </summary>
    /// <param name="assemblyName">The assembly simple name (e.g. Jellyfin.Plugin.HomeScreenSections).</param>
    /// <param name="typeName">The full type name.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="json">The JSON payload.</param>
    /// <returns><c>null</c> on success, otherwise the state and a message for the settings page.</returns>
    public static (HookState State, string Message)? InvokeWithJson(string assemblyName, string typeName, string methodName, string json)
    {
        try
        {
            var assembly = Find(assemblyName);
            if (assembly is null)
            {
                return (HookState.Unavailable, assemblyName + " is not installed.");
            }

            var method = assembly.GetType(typeName)?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            var parameterType = method?.GetParameters() is { Length: 1 } parameters ? parameters[0].ParameterType : null;
            var parse = parameterType?.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, [typeof(string)], null);
            if (method is null || parameterType is null || parse is null)
            {
                return (HookState.Failed, "This version of " + assemblyName + " is not compatible (" + typeName + "." + methodName + " not found).");
            }

            method.Invoke(null, [parse.Invoke(null, [json])]);
            return null;
        }
        catch (Exception ex) when (ex is TargetInvocationException or MemberAccessException or ArgumentException or InvalidOperationException)
        {
            return (HookState.Failed, assemblyName + " rejected the registration: " + (ex.InnerException ?? ex).Message);
        }
    }

    private static Assembly? Find(string assemblyName) => AssemblyLoadContext.All
        .SelectMany(context => context.Assemblies)
        .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));
}
