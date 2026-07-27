using System.Reflection;
using System.Reflection.Emit;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;

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
    /// Builds an assembly containing one public, concrete <see cref="ModuleLifecycleBase"/>
    /// implementation that constructor-injects <see cref="INavigationProvider"/> and
    /// registers one <see cref="NavigationItem"/> during <c>InitialiseAsync</c> — proving
    /// a plugin-loaded module contributes navigation through the exact same path an
    /// ordinarily-discovered module does, with no plugin-specific navigation mechanism
    /// of any kind. Saved to <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildValidPluginAssemblyWithNavigationModule(
        string outputDirectory,
        string fileName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        string navigationItemId,
        string navigationItemTitle)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicNavigationPluginModule",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(ModuleLifecycleBase));

        var metadataAttributeCtor = typeof(ModuleMetadataAttribute).GetConstructor(
            [typeof(string), typeof(string), typeof(string)])!;
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            metadataAttributeCtor, [moduleId, moduleName, moduleVersion]));

        var providerField = typeBuilder.DefineField(
            "_navigationProvider", typeof(INavigationProvider), FieldAttributes.Private);

        DefineConstructor(typeBuilder, providerField, moduleId, moduleName, moduleVersion);
        DefineInitialiseAsyncOverride(typeBuilder, providerField, navigationItemId, navigationItemTitle);

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    private static void DefineConstructor(
        TypeBuilder typeBuilder, FieldBuilder providerField, string moduleId, string moduleName, string moduleVersion)
    {
        var baseCtor = typeof(ModuleLifecycleBase).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string), typeof(string)], null)!;

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [typeof(INavigationProvider)]);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldstr, moduleId);
        il.Emit(OpCodes.Ldstr, moduleName);
        il.Emit(OpCodes.Ldstr, moduleVersion);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, providerField);
        il.Emit(OpCodes.Ret);
    }

    private static void DefineInitialiseAsyncOverride(
        TypeBuilder typeBuilder, FieldBuilder providerField, string navigationItemId, string navigationItemTitle)
    {
        var baseInitialiseAsync = typeof(ModuleLifecycleBase).GetMethod(nameof(ModuleLifecycleBase.InitialiseAsync))!;

        var initialiseBuilder = typeBuilder.DefineMethod(
            nameof(ModuleLifecycleBase.InitialiseAsync),
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task),
            [typeof(CancellationToken)]);

        var navigationItemCtor = typeof(NavigationItem).GetConstructor(
            [typeof(string), typeof(string), typeof(int), typeof(string), typeof(string), typeof(string), typeof(Func<bool>)])!;
        var registerMethod = typeof(INavigationProvider).GetMethod(nameof(INavigationProvider.Register))!;
        var completedTaskGetter = typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetGetMethod()!;

        var il = initialiseBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, providerField);
        il.Emit(OpCodes.Ldstr, navigationItemId);
        il.Emit(OpCodes.Ldstr, navigationItemTitle);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Newobj, navigationItemCtor);
        il.Emit(OpCodes.Callvirt, registerMethod);
        il.Emit(OpCodes.Call, completedTaskGetter);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(initialiseBuilder, baseInitialiseAsync);
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
