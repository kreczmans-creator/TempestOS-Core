using System.Reflection;
using System.Reflection.Emit;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Plugins;

/// <summary>
/// Builds real, loadable plugin assemblies on disk at test time, using
/// <see cref="PersistedAssemblyBuilder"/> rather than a mock or a
/// pre-compiled fixture project — so <see cref="Assembly.LoadFrom(string)"/>,
/// and Module Discovery's own reflection-based scan, exercise the exact same
/// code path a real plugin's assembly would.
/// </summary>
internal static class DynamicPluginAssemblyBuilder
{
    /// <summary>
    /// Builds an assembly containing one public, concrete <see cref="IModule"/>
    /// implementation with the given metadata, and saves it to
    /// <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildValidPluginAssembly(
        string outputDirectory,
        string fileName,
        string moduleId,
        string moduleName,
        string moduleVersion)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        // The assembly's own identity (its simple name) must be unique across
        // the whole test process, independent of the on-disk file name: the
        // default AssemblyLoadContext resolves Assembly.LoadFrom by identity,
        // not by path, so two different test methods building two different
        // assemblies that both happen to use the same file name (e.g.
        // "Valid.dll") would otherwise collide and silently resolve to
        // whichever one loaded first.
        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicPluginModule",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IModule)]);

        DefineStringProperty(typeBuilder, nameof(IModule.Id), moduleId);
        DefineStringProperty(typeBuilder, nameof(IModule.Name), moduleName);
        DefineStringProperty(typeBuilder, nameof(IModule.Version), moduleVersion);

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Writes a file that is not a valid .NET assembly (or PE image at all)
    /// to <paramref name="outputDirectory"/> under <paramref name="fileName"/>,
    /// so that loading it via <see cref="Assembly.LoadFrom(string)"/> fails
    /// exactly as a corrupt plugin assembly file would.
    /// </summary>
    /// <returns>The full path to the corrupt file.</returns>
    public static string WriteCorruptAssemblyFile(string outputDirectory, string fileName)
    {
        var path = Path.Combine(outputDirectory, fileName);
        File.WriteAllBytes(path, "this is not a valid PE image"u8.ToArray());
        return path;
    }

    private static void DefineStringProperty(TypeBuilder typeBuilder, string propertyName, string constantValue)
    {
        var interfaceProperty = typeof(IModule).GetProperty(propertyName)!;
        var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.None, typeof(string), null);

        var getMethod = typeBuilder.DefineMethod(
            $"get_{propertyName}",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.NewSlot | MethodAttributes.Final,
            typeof(string),
            Type.EmptyTypes);

        var il = getMethod.GetILGenerator();
        il.Emit(OpCodes.Ldstr, constantValue);
        il.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getMethod);
        typeBuilder.DefineMethodOverride(getMethod, interfaceProperty.GetGetMethod()!);
    }
}
