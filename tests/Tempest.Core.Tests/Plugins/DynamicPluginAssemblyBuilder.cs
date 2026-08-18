using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Navigation;
using Tempest.Samples;

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
    /// Builds an assembly containing one public, concrete <see cref="ModuleLifecycleBase"/>
    /// implementation that constructor-injects <see cref="ICommandDispatcher"/> and
    /// <see cref="ICommandRegistry"/>, and registers a handler and descriptor for
    /// <see cref="IncrementCounterCommand"/> (reusing the existing, already-compiled
    /// <see cref="Tempest.Samples"/> command and handler types) during
    /// <c>InitialiseAsync</c> — proving a plugin-loaded module contributes command
    /// handlers through the exact same path an ordinarily-discovered module does, with
    /// no plugin-specific Command Framework mechanism of any kind. Saved to
    /// <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildValidPluginAssemblyWithCommandModule(
        string outputDirectory,
        string fileName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        string commandId,
        string commandDisplayName)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicCommandPluginModule",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(ModuleLifecycleBase));

        var metadataAttributeCtor = typeof(ModuleMetadataAttribute).GetConstructor(
            [typeof(string), typeof(string), typeof(string)])!;
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            metadataAttributeCtor, [moduleId, moduleName, moduleVersion]));

        var dispatcherField = typeBuilder.DefineField(
            "_commandDispatcher", typeof(ICommandDispatcher), FieldAttributes.Private);
        var registryField = typeBuilder.DefineField(
            "_commandRegistry", typeof(ICommandRegistry), FieldAttributes.Private);

        DefineCommandModuleConstructor(typeBuilder, dispatcherField, registryField, moduleId, moduleName, moduleVersion);
        DefineCommandModuleInitialiseAsyncOverride(typeBuilder, dispatcherField, registryField, commandId, commandDisplayName);

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    private static void DefineCommandModuleConstructor(
        TypeBuilder typeBuilder, FieldBuilder dispatcherField, FieldBuilder registryField,
        string moduleId, string moduleName, string moduleVersion)
    {
        var baseCtor = typeof(ModuleLifecycleBase).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string), typeof(string)], null)!;

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [typeof(ICommandDispatcher), typeof(ICommandRegistry)]);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldstr, moduleId);
        il.Emit(OpCodes.Ldstr, moduleName);
        il.Emit(OpCodes.Ldstr, moduleVersion);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, dispatcherField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Stfld, registryField);
        il.Emit(OpCodes.Ret);
    }

    private static void DefineCommandModuleInitialiseAsyncOverride(
        TypeBuilder typeBuilder, FieldBuilder dispatcherField, FieldBuilder registryField,
        string commandId, string commandDisplayName)
    {
        var baseInitialiseAsync = typeof(ModuleLifecycleBase).GetMethod(nameof(ModuleLifecycleBase.InitialiseAsync))!;

        var initialiseBuilder = typeBuilder.DefineMethod(
            nameof(ModuleLifecycleBase.InitialiseAsync),
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task),
            [typeof(CancellationToken)]);

        var handlerCtor = typeof(IncrementCounterCommandHandler).GetConstructor(Type.EmptyTypes)!;
        var registerHandlerMethod = typeof(ICommandDispatcher)
            .GetMethod(nameof(ICommandDispatcher.RegisterHandler))!
            .MakeGenericMethod(typeof(IncrementCounterCommand));
        var descriptorCtor = typeof(CommandDescriptor).GetConstructor(
            [typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(Func<bool>), typeof(Func<ICommand>)])!;
        var registerDescriptorMethod = typeof(ICommandRegistry).GetMethod(nameof(ICommandRegistry.RegisterDescriptor))!;
        var completedTaskGetter = typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetGetMethod()!;

        var il = initialiseBuilder.GetILGenerator();

        // _commandDispatcher.RegisterHandler<IncrementCounterCommand>(new IncrementCounterCommandHandler());
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, dispatcherField);
        il.Emit(OpCodes.Newobj, handlerCtor);
        il.Emit(OpCodes.Callvirt, registerHandlerMethod);

        // _commandRegistry.RegisterDescriptor(new CommandDescriptor(commandId, commandDisplayName, null, null, null, null, null));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, registryField);
        il.Emit(OpCodes.Ldstr, commandId);
        il.Emit(OpCodes.Ldstr, commandDisplayName);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Newobj, descriptorCtor);
        il.Emit(OpCodes.Callvirt, registerDescriptorMethod);

        il.Emit(OpCodes.Call, completedTaskGetter);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(initialiseBuilder, baseInitialiseAsync);
    }

    /// <summary>
    /// Builds an assembly containing one public, concrete <see cref="IModule"/>
    /// implementation whose sole public constructor accepts exactly
    /// <paramref name="constructorParameterTypes"/>, in order, ignoring every
    /// argument at runtime (the constructor body only ever calls the base
    /// <see cref="object"/> constructor). Used to exercise
    /// <c>PluginAssemblyLoader.EnforceTrust</c>'s own constructor-conformance
    /// reflection check (ADR-0111) without needing those services to be
    /// resolvable — <c>LoadPlugins</c> never actually constructs a discovered
    /// module, only reflects over its declared constructor shape. Saved to
    /// <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildPluginAssemblyWithConstructorParameters(
        string outputDirectory,
        string fileName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        Type[] constructorParameterTypes)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicConstructorPluginModule",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IModule)]);

        DefineStringProperty(typeBuilder, nameof(IModule.Id), moduleId);
        DefineStringProperty(typeBuilder, nameof(IModule.Name), moduleName);
        DefineStringProperty(typeBuilder, nameof(IModule.Version), moduleVersion);

        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;
        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            constructorParameterTypes);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, objectCtor);
        il.Emit(OpCodes.Ret);

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Builds an assembly containing one public, concrete <see cref="IModule"/>
    /// implementer with NO <see cref="ModuleMetadataAttribute"/> and a public
    /// parameterless constructor whose body calls
    /// <see cref="ConstructorExecutionProbe.RecordInvocation(string)"/> with
    /// <paramref name="probeId"/> - an observable side effect proving whether
    /// <see cref="Activator.CreateInstance(Type)"/> genuinely ran for this
    /// exact type (WP 13.9.6 regression coverage).
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <paramref name="nonCompliantConstructorParameterTypes"/> is
    /// non-empty, a second public constructor overload is also defined, with
    /// those parameter types (ignored at runtime, mirroring
    /// <see cref="BuildPluginAssemblyWithConstructorParameters"/>'s own
    /// convention) and no probe call.
    /// </para>
    /// <para>
    /// <b>This does NOT produce a constructor-non-compliant type.</b>
    /// <c>PluginAssemblyLoader.HasCompliantConstructor</c> accepts a type if
    /// <i>any</i> one of its public constructors is compliant, and a
    /// parameterless constructor is always trivially compliant (zero
    /// parameters vacuously satisfy its own <c>.All(...)</c> check) -
    /// so a type built this way, with the probe-calling parameterless
    /// constructor always present, is always accepted regardless of what
    /// <paramref name="nonCompliantConstructorParameterTypes"/> contains.
    /// This parameter exists only to exercise/document that exact semantic
    /// (a passing plugin whose module happens to expose a second, otherwise
    /// non-compliant overload nobody ever calls) - it is never the right
    /// shape for a genuine constructor-non-compliance test scenario. For
    /// that, use <see cref="BuildPluginAssemblyWithConstructorParameters"/>
    /// instead: a type with no parameterless overload at all is both (a)
    /// genuinely non-compliant per <c>HasCompliantConstructor</c>, since
    /// none of its constructors can ever be compliant-by-default, and (b)
    /// guaranteed to reach <c>CreateDescriptor</c>'s own explicit
    /// "no parameterless constructor" guard - which throws
    /// <see cref="ModuleDiscoveryException"/> naming the actual fix, before
    /// <see cref="Activator.CreateInstance(Type)"/> is ever attempted - if it
    /// were ever (wrongly) reached, proving the WP 13.9.6 fix by the absence
    /// of that exception rather than by a probe call.
    /// </para>
    /// </remarks>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildUnattributedPluginModuleWithConstructorProbe(
        string outputDirectory,
        string fileName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        string probeId,
        Type[]? nonCompliantConstructorParameterTypes = null)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicProbePluginModule",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IModule)]);

        DefineStringProperty(typeBuilder, nameof(IModule.Id), moduleId);
        DefineStringProperty(typeBuilder, nameof(IModule.Name), moduleName);
        DefineStringProperty(typeBuilder, nameof(IModule.Version), moduleVersion);

        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;
        var recordInvocationMethod = typeof(ConstructorExecutionProbe).GetMethod(
            nameof(ConstructorExecutionProbe.RecordInvocation))!;

        var probeCtorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            Type.EmptyTypes);

        var probeIl = probeCtorBuilder.GetILGenerator();
        probeIl.Emit(OpCodes.Ldarg_0);
        probeIl.Emit(OpCodes.Call, objectCtor);
        probeIl.Emit(OpCodes.Ldstr, probeId);
        probeIl.Emit(OpCodes.Call, recordInvocationMethod);
        probeIl.Emit(OpCodes.Ret);

        if (nonCompliantConstructorParameterTypes is { Length: > 0 })
        {
            var secondCtorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                nonCompliantConstructorParameterTypes);

            var secondIl = secondCtorBuilder.GetILGenerator();
            secondIl.Emit(OpCodes.Ldarg_0);
            secondIl.Emit(OpCodes.Call, objectCtor);
            secondIl.Emit(OpCodes.Ret);
        }

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Builds a "second, wholly undeclared assembly" (WP 13.9.1 security
    /// remediation test scenario) containing two public types: a plain base
    /// class with a parameterless constructor, and an <see cref="IModule"/>
    /// implementer whose sole public constructor accepts exactly
    /// <paramref name="moduleConstructorParameterTypes"/>, in order, ignoring
    /// every argument at runtime (mirroring
    /// <see cref="BuildPluginAssemblyWithConstructorParameters"/>'s own
    /// constructor-shape convention). Saved to <paramref name="outputDirectory"/>
    /// under a file name that exactly matches this assembly's own generated
    /// simple name — required so that the default <c>AssemblyLoadContext</c>'s
    /// own directory-probing (triggered when a caller-declared assembly loaded
    /// via <see cref="Assembly.LoadFrom(string)"/> references this assembly by
    /// name, but never declares it in any plugin manifest) can find it purely
    /// by simple-name-plus-extension, exactly as a real, undeclared dependency
    /// DLL sitting in a plugin's own candidate folder would be found.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildSecondaryAssemblyWithBaseTypeAndModule(
        string outputDirectory,
        string namePrefix,
        string baseTypeSimpleName,
        string moduleTypeSimpleName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        Type[] moduleConstructorParameterTypes)
    {
        var assemblyName = new AssemblyName($"{namePrefix}-{Guid.NewGuid():N}");
        var dllPath = Path.Combine(outputDirectory, $"{assemblyName.Name}.dll");

        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

        var baseTypeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.{baseTypeSimpleName}",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object));

        DefineParameterlessConstructorCallingBase(baseTypeBuilder, objectCtor);
        baseTypeBuilder.CreateType();

        var moduleTypeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.{moduleTypeSimpleName}",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IModule)]);

        DefineStringProperty(moduleTypeBuilder, nameof(IModule.Id), moduleId);
        DefineStringProperty(moduleTypeBuilder, nameof(IModule.Name), moduleName);
        DefineStringProperty(moduleTypeBuilder, nameof(IModule.Version), moduleVersion);

        var moduleCtorBuilder = moduleTypeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            moduleConstructorParameterTypes);
        var moduleCtorIl = moduleCtorBuilder.GetILGenerator();
        moduleCtorIl.Emit(OpCodes.Ldarg_0);
        moduleCtorIl.Emit(OpCodes.Call, objectCtor);
        moduleCtorIl.Emit(OpCodes.Ret);

        moduleTypeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Builds a plugin's own primary, manifest-declared assembly, containing
    /// one public type that inherits from <paramref name="externalBaseTypeFullName"/>
    /// — a type declared in the already-saved, wholly separate
    /// <paramref name="externalAssemblyPath"/> assembly, never itself declared
    /// in any plugin manifest. Reproduces the exact WP 13.9.0 proof-of-concept
    /// mechanism: resolving this type's own base-type chain (which
    /// <c>PluginAssemblyLoader.EnforceTrust</c>'s own <see cref="Assembly.GetTypes"/>
    /// call does, as an ordinary part of reflecting over the primary assembly)
    /// is what forces the CLR to load <paramref name="externalAssemblyPath"/>
    /// into the <c>AppDomain</c> as a lazy, unavoidable side effect — no
    /// explicit <see cref="Assembly.LoadFrom(string)"/> of the second assembly
    /// ever appears anywhere in this builder or in the plugin's own manifest.
    /// The external base type is resolved via a temporary, dedicated
    /// <see cref="AssemblyLoadContext"/> — used only to obtain a real,
    /// reflectable <see cref="Type"/> to emit IL against; it has no bearing on
    /// how the CLR later, independently, resolves and loads
    /// <paramref name="externalAssemblyPath"/> for the built assembly's own
    /// benefit at plugin-load time.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildPrimaryPluginAssemblyDerivingFromExternalBaseType(
        string outputDirectory,
        string fileName,
        string externalAssemblyPath,
        string externalBaseTypeFullName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        bool implementIModule)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var reflectionLoadContext = new AssemblyLoadContext($"ReflectionOnly-{Guid.NewGuid():N}", isCollectible: true);
        var externalAssembly = reflectionLoadContext.LoadFromAssemblyPath(externalAssemblyPath);
        var externalBaseType = externalAssembly.GetType(externalBaseTypeFullName, throwOnError: true)!;
        var externalBaseCtor = externalBaseType.GetConstructor(Type.EmptyTypes)!;

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        var interfaces = implementIModule ? new[] { typeof(IModule) } : Type.EmptyTypes;

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicPrimaryModule",
            TypeAttributes.Public | TypeAttributes.Class,
            externalBaseType,
            interfaces);

        if (implementIModule)
        {
            DefineStringProperty(typeBuilder, nameof(IModule.Id), moduleId);
            DefineStringProperty(typeBuilder, nameof(IModule.Name), moduleName);
            DefineStringProperty(typeBuilder, nameof(IModule.Version), moduleVersion);
        }

        DefineParameterlessConstructorCallingBase(typeBuilder, externalBaseCtor);

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        reflectionLoadContext.Unload();

        return dllPath;
    }

    /// <summary>
    /// Builds a plugin's own primary, manifest-declared assembly, containing
    /// one public <see cref="IModule"/> implementer whose own non-compliant
    /// constructor accepts exactly one parameter of
    /// <paramref name="externalParameterTypeFullName"/> — a type declared in
    /// the already-saved, wholly separate <paramref name="externalAssemblyPath"/>
    /// assembly, never itself declared in any plugin manifest — ignoring the
    /// argument at runtime (mirroring <see cref="BuildPluginAssemblyWithConstructorParameters"/>'s
    /// own convention). <c>WP 13.9.3</c> security remediation test scenario:
    /// unlike <see cref="BuildPrimaryPluginAssemblyDerivingFromExternalBaseType"/>'s
    /// own base-type-inheritance mechanism (closed at <c>WP 13.9.1</c>), this
    /// reproduces the second, narrower mechanism <c>WP 13.9.2</c>'s
    /// Security/Trust re-execution found: resolving a constructor
    /// parameter's own <see cref="System.Reflection.ParameterInfo.ParameterType"/>
    /// is an equally unavoidable CLR assembly-load trigger, and it fires
    /// during <c>PluginAssemblyLoader.HasCompliantConstructor</c>'s own
    /// reflection, not <see cref="Assembly.GetTypes"/>. If
    /// <paramref name="addAlternateCompliantConstructor"/> is <see langword="true"/>,
    /// the type also gets a second, entirely parameterless (therefore
    /// trivially compliant) constructor — reproducing the specific,
    /// order-independent variant where a plugin module was fully accepted
    /// via its own alternate, compliant overload while the same mechanism
    /// still smuggled the external assembly in unvetted.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildPrimaryPluginAssemblyWithExternalConstructorParameter(
        string outputDirectory,
        string fileName,
        string externalAssemblyPath,
        string externalParameterTypeFullName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        bool addAlternateCompliantConstructor)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var reflectionLoadContext = new AssemblyLoadContext($"ReflectionOnly-{Guid.NewGuid():N}", isCollectible: true);
        var externalAssembly = reflectionLoadContext.LoadFromAssemblyPath(externalAssemblyPath);
        var externalParameterType = externalAssembly.GetType(externalParameterTypeFullName, throwOnError: true)!;

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicPrimaryConstructorParameterModule",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IModule)]);

        DefineStringProperty(typeBuilder, nameof(IModule.Id), moduleId);
        DefineStringProperty(typeBuilder, nameof(IModule.Name), moduleName);
        DefineStringProperty(typeBuilder, nameof(IModule.Version), moduleVersion);

        var externalParameterCtor = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [externalParameterType]);
        var externalParameterIl = externalParameterCtor.GetILGenerator();
        externalParameterIl.Emit(OpCodes.Ldarg_0);
        externalParameterIl.Emit(OpCodes.Call, objectCtor);
        externalParameterIl.Emit(OpCodes.Ret);

        if (addAlternateCompliantConstructor)
            DefineParameterlessConstructorCallingBase(typeBuilder, objectCtor);

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        reflectionLoadContext.Unload();

        return dllPath;
    }

    /// <summary>
    /// Builds a plugin's own primary, manifest-declared assembly carrying
    /// <b>both</b> of this suite's established transitive-load mechanisms at
    /// once (WP 13.11C): one public anchor type inheriting from
    /// <paramref name="reachableBaseTypeFullName"/> — a type in the already-saved
    /// <paramref name="reachableSecondaryAssemblyPath"/>, deliberately saved
    /// <i>alongside</i> this assembly so the default <c>AssemblyLoadContext</c>'s
    /// own directory-probing genuinely finds and loads it during
    /// <see cref="Assembly.GetTypes"/>'s own base-type-chain resolution — and,
    /// separately, one public <see cref="IModule"/> implementer whose sole
    /// constructor takes a parameter of
    /// <paramref name="unreachableParameterTypeFullName"/>, a type in
    /// <paramref name="unreachableAssemblyPath"/>, deliberately saved to a
    /// directory that probing will never search, so resolving it genuinely
    /// throws.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exact combination is what makes <c>WP 13.11B</c>'s own
    /// completed-fixed-point-scan decision observable, and it is the only
    /// shape that does. The anchor forces the reachable secondary assembly to
    /// enter the <c>AppDomain</c> during the scan step's own
    /// <see cref="Assembly.GetTypes"/> call — i.e. <i>after</i>
    /// <c>DiscoverModuleTypes</c> took that step's <c>before</c> snapshot, so
    /// the step's own before/after diff at the <i>end</i> of the loop body is
    /// the only thing that can discover it — while the module's own
    /// unresolvable constructor parameter trips the denial <i>in the middle</i>
    /// of that same step, before the diff is reached. Aborting the scan at
    /// that point (<c>WP 13.11A</c>'s own recommended partial-list shape)
    /// therefore silently strands the secondary assembly: resident in the
    /// process, its <see cref="IModule"/> implementers never scanned, never
    /// recorded denied, and yet fully visible to Module Discovery's own
    /// deliberately plugin-unaware <c>AppDomain</c> scan (ADR-0110). Letting
    /// the scan run on to its fixed point is what closes it.
    /// </para>
    /// <para>
    /// Both external types are resolved through one temporary, collectible
    /// <see cref="AssemblyLoadContext"/>, used only to obtain real, reflectable
    /// <see cref="Type"/> handles to emit IL against — exactly as
    /// <see cref="BuildPrimaryPluginAssemblyDerivingFromExternalBaseType"/> and
    /// <see cref="BuildPrimaryPluginAssemblyWithExternalConstructorParameter"/>
    /// each already do for their own single mechanism. It has no bearing on how
    /// the CLR later, independently, resolves either assembly at plugin-load
    /// time.
    /// </para>
    /// </remarks>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildPrimaryPluginAssemblyWithReachableBaseTypeAnchorAndUnresolvableConstructorParameter(
        string outputDirectory,
        string fileName,
        string reachableSecondaryAssemblyPath,
        string reachableBaseTypeFullName,
        string unreachableAssemblyPath,
        string unreachableParameterTypeFullName,
        string moduleId,
        string moduleName,
        string moduleVersion)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var reflectionLoadContext = new AssemblyLoadContext($"ReflectionOnly-{Guid.NewGuid():N}", isCollectible: true);
        var reachableAssembly = reflectionLoadContext.LoadFromAssemblyPath(reachableSecondaryAssemblyPath);
        var reachableBaseType = reachableAssembly.GetType(reachableBaseTypeFullName, throwOnError: true)!;
        var reachableBaseCtor = reachableBaseType.GetConstructor(Type.EmptyTypes)!;

        var unreachableAssembly = reflectionLoadContext.LoadFromAssemblyPath(unreachableAssemblyPath);
        var unreachableParameterType = unreachableAssembly.GetType(unreachableParameterTypeFullName, throwOnError: true)!;

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

        // The anchor. Deliberately NOT an IModule implementer - its only job
        // is to make GetTypes()'s own base-type-chain resolution pull the
        // reachable secondary assembly into the AppDomain during the scan
        // step, so that only the step's own end-of-body diff can discover it.
        var anchorTypeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicSecondaryAssemblyAnchor",
            TypeAttributes.Public | TypeAttributes.Class,
            reachableBaseType);

        DefineParameterlessConstructorCallingBase(anchorTypeBuilder, reachableBaseCtor);
        anchorTypeBuilder.CreateType();

        // The offending module: unattributed, sole constructor, unresolvable
        // parameter type - the TD-51 shape, tripping the denial mid-step.
        var moduleTypeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicUnresolvableConstructorModule",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IModule)]);

        DefineStringProperty(moduleTypeBuilder, nameof(IModule.Id), moduleId);
        DefineStringProperty(moduleTypeBuilder, nameof(IModule.Name), moduleName);
        DefineStringProperty(moduleTypeBuilder, nameof(IModule.Version), moduleVersion);

        var moduleCtorBuilder = moduleTypeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [unreachableParameterType]);
        var moduleCtorIl = moduleCtorBuilder.GetILGenerator();
        moduleCtorIl.Emit(OpCodes.Ldarg_0);
        moduleCtorIl.Emit(OpCodes.Call, objectCtor);
        moduleCtorIl.Emit(OpCodes.Ret);

        moduleTypeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        reflectionLoadContext.Unload();

        return dllPath;
    }

    private static void DefineParameterlessConstructorCallingBase(TypeBuilder typeBuilder, ConstructorInfo baseCtor)
    {
        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            Type.EmptyTypes);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ret);
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

    /// <summary>
    /// Builds an assembly containing one public, concrete type implementing
    /// <i>both</i> <see cref="IModule"/> and <see cref="IHostedService"/> —
    /// <c>WP 13.9.4</c> trust-denial execution boundary remediation test
    /// scenario. A wholly baseline-compliant, parameterless constructor, so
    /// denial (when the caller's own manifest requests an out-of-ceiling
    /// capability) comes purely from the capability check, isolating the
    /// specific case that previously had zero discovered-type data recorded
    /// for it at all (<c>FindIneligibleCapability</c> used to short-circuit
    /// before <c>DiscoverModuleTypes</c> ever ran). Proves the single most
    /// severe variant WP 13.9.4's own Adversarial Review found: a Type
    /// correctly excluded from Module Registration through one discovery
    /// pipeline (<see cref="Modules.ReflectionFrameworkDiscoveryService"/>)
    /// remaining fully reachable through the sibling, independent Hosted
    /// Service discovery/registration pipeline
    /// (<see cref="HostedServiceDiscoveryService"/>/<see cref="IHostedServiceManager"/>)
    /// unless the SAME denial is propagated to both.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildDualModuleAndHostedServiceAssembly(
        string outputDirectory,
        string fileName,
        string moduleId,
        string moduleName,
        string moduleVersion)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicDualModuleHostedServiceType",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IModule), typeof(IHostedService)]);

        DefineStringProperty(typeBuilder, nameof(IModule.Id), moduleId);
        DefineStringProperty(typeBuilder, nameof(IModule.Name), moduleName);
        DefineStringProperty(typeBuilder, nameof(IModule.Version), moduleVersion);

        DefineParameterlessConstructorCallingBase(typeBuilder, objectCtor);
        DefineCompletedTaskMethodOverride(typeBuilder, typeof(IHostedService), nameof(IHostedService.StartAsync));
        DefineCompletedTaskMethodOverride(typeBuilder, typeof(IHostedService), nameof(IHostedService.StopAsync));

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Defines a public override of <paramref name="interfaceType"/>'s own
    /// <c>Task MethodName(CancellationToken)</c>-shaped method, returning
    /// <see cref="Task.CompletedTask"/> unconditionally.
    /// </summary>
    private static void DefineCompletedTaskMethodOverride(TypeBuilder typeBuilder, Type interfaceType, string methodName)
    {
        var interfaceMethod = interfaceType.GetMethod(methodName)!;

        var methodBuilder = typeBuilder.DefineMethod(
            methodName,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task),
            [typeof(CancellationToken)]);

        var completedTaskGetter = typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetGetMethod()!;

        var il = methodBuilder.GetILGenerator();
        il.Emit(OpCodes.Call, completedTaskGetter);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(methodBuilder, interfaceMethod);
    }

    /// <summary>
    /// Builds an assembly containing one public, concrete type implementing
    /// <i>only</i> <see cref="IHostedService"/> — never <see cref="IModule"/>
    /// — whose sole public constructor accepts exactly
    /// <paramref name="constructorParameterTypes"/>, in order, ignoring every
    /// argument at runtime (mirrors <see cref="BuildPluginAssemblyWithConstructorParameters"/>'s
    /// own exact convention, adapted to a hosted-service-only shape). Used to
    /// exercise <c>PluginAssemblyLoader.EnforceTrust</c>'s own
    /// constructor-conformance check (WP 13.10B, TD-51) for the specific
    /// defect it closes: before this fix, an <see cref="IHostedService"/>-only
    /// plugin (zero discovered <see cref="IModule"/> types) short-circuited
    /// straight past the constructor-conformance check entirely, since it
    /// only ever consulted <c>moduleTypes</c>. Passing <see cref="Type.EmptyTypes"/>
    /// yields a trivially compliant, parameterless constructor — the
    /// "positive/no-regression" shape proving the fix does not overreach.
    /// Saved to <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildHostedServiceOnlyAssemblyWithConstructorParameters(
        string outputDirectory,
        string fileName,
        Type[] constructorParameterTypes)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicHostedServiceOnlyConstructorType",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IHostedService)]);

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            constructorParameterTypes);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, objectCtor);
        il.Emit(OpCodes.Ret);

        DefineCompletedTaskMethodOverride(typeBuilder, typeof(IHostedService), nameof(IHostedService.StartAsync));
        DefineCompletedTaskMethodOverride(typeBuilder, typeof(IHostedService), nameof(IHostedService.StopAsync));

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Builds an assembly containing one public, concrete
    /// <see cref="IHostedService"/>-only type (never <see cref="IModule"/>)
    /// with a wholly baseline-compliant, public parameterless constructor —
    /// the compliant, "still loads" counterpart to
    /// <see cref="BuildHostedServiceOnlyAssemblyWithConstructorParameters"/>'s
    /// own non-compliant shape (WP 13.10B, TD-51). A thin, explicitly-named
    /// wrapper over that same method (<c>Type.EmptyTypes</c> is always
    /// trivially compliant), kept as its own method so a caller's own intent
    /// — "the compliant variant" — reads directly at the call site.
    /// Saved to <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildCompliantHostedServiceOnlyAssembly(string outputDirectory, string fileName) =>
        BuildHostedServiceOnlyAssemblyWithConstructorParameters(outputDirectory, fileName, Type.EmptyTypes);

    /// <summary>
    /// Builds an assembly containing one public, concrete
    /// <see cref="IHostedService"/>-only type whose sole public constructor
    /// constructor-injects <see cref="ICurrentComponentAccessor"/> (the
    /// read-only, freely grantable interface — never the denylisted concrete
    /// <see cref="CurrentComponentAccessor"/> type) and whose <c>StartAsync</c>
    /// override reads <see cref="ICurrentComponentAccessor.Current"/>'s own
    /// <see cref="IPrincipal.Identity"/>/<see cref="IIdentity.Id"/>
    /// (or <see langword="null"/> if <c>Current</c> itself is <see langword="null"/>)
    /// and records it via <see cref="AmbientPrincipalCaptureProbe.RecordObservedIdentity"/>
    /// keyed by <paramref name="probeId"/> — an observable side effect proving
    /// which ambient component principal, if any, was genuinely pushed by
    /// <c>TempestHost</c>'s own <c>hostedServiceComponentScopeProvider</c>
    /// closure (WP 13.10B, TD-51) at the exact moment <c>StartAsync</c> ran.
    /// Saved to <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildHostedServiceOnlyAssemblyCapturingAmbientComponentPrincipal(
        string outputDirectory,
        string fileName,
        string probeId)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        var objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

        var typeBuilder = moduleBuilder.DefineType(
            $"{assemblyName.Name}.DynamicAmbientPrincipalHostedService",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            [typeof(IHostedService)]);

        var accessorField = typeBuilder.DefineField(
            "_accessor", typeof(ICurrentComponentAccessor), FieldAttributes.Private);

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            [typeof(ICurrentComponentAccessor)]);

        var ctorIl = ctorBuilder.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Call, objectCtor);
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Ldarg_1);
        ctorIl.Emit(OpCodes.Stfld, accessorField);
        ctorIl.Emit(OpCodes.Ret);

        DefineAmbientPrincipalCapturingStartAsync(typeBuilder, accessorField, probeId);
        DefineCompletedTaskMethodOverride(typeBuilder, typeof(IHostedService), nameof(IHostedService.StopAsync));

        typeBuilder.CreateType();
        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Defines a public <c>StartAsync</c> override that reads
    /// <paramref name="accessorField"/>'s own <see cref="ICurrentComponentAccessor.Current"/>
    /// property, resolves <c>?.Identity.Id</c> (or <see langword="null"/> if
    /// <c>Current</c> is <see langword="null"/>), and records the result via
    /// <see cref="AmbientPrincipalCaptureProbe.RecordObservedIdentity"/>
    /// before returning <see cref="Task.CompletedTask"/>.
    /// </summary>
    private static void DefineAmbientPrincipalCapturingStartAsync(TypeBuilder typeBuilder, FieldBuilder accessorField, string probeId)
    {
        var interfaceMethod = typeof(IHostedService).GetMethod(nameof(IHostedService.StartAsync))!;

        var methodBuilder = typeBuilder.DefineMethod(
            nameof(IHostedService.StartAsync),
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task),
            [typeof(CancellationToken)]);

        var currentGetter = typeof(ICurrentComponentAccessor).GetProperty(nameof(ICurrentComponentAccessor.Current))!.GetGetMethod()!;
        var identityGetter = typeof(IPrincipal).GetProperty(nameof(IPrincipal.Identity))!.GetGetMethod()!;
        var idGetter = typeof(IIdentity).GetProperty(nameof(IIdentity.Id))!.GetGetMethod()!;
        var recordMethod = typeof(AmbientPrincipalCaptureProbe).GetMethod(nameof(AmbientPrincipalCaptureProbe.RecordObservedIdentity))!;
        var completedTaskGetter = typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetGetMethod()!;

        var il = methodBuilder.GetILGenerator();
        var notNullLabel = il.DefineLabel();
        var afterLabel = il.DefineLabel();

        // AmbientPrincipalCaptureProbe.RecordObservedIdentity(probeId, _accessor.Current?.Identity.Id);
        il.Emit(OpCodes.Ldstr, probeId);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, accessorField);
        il.Emit(OpCodes.Callvirt, currentGetter);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brtrue_S, notNullLabel);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Br_S, afterLabel);
        il.MarkLabel(notNullLabel);
        il.Emit(OpCodes.Callvirt, identityGetter);
        il.Emit(OpCodes.Callvirt, idGetter);
        il.MarkLabel(afterLabel);

        il.Emit(OpCodes.Call, recordMethod);
        il.Emit(OpCodes.Call, completedTaskGetter);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(methodBuilder, interfaceMethod);
    }

    /// <summary>
    /// Builds an assembly containing TWO separate, legitimate, compliant,
    /// <see cref="ModuleMetadataAttribute"/>-carrying, <see cref="ModuleLifecycleBase"/>-derived
    /// <see cref="IModule"/> types in the SAME assembly — multi-module-per-plugin
    /// coverage. Each has a public parameterless constructor calling the base
    /// constructor with its own literal Id/Name/Version, mirroring
    /// <see cref="BuildValidPluginAssembly"/>'s own simplest shape, duplicated.
    /// When <paramref name="module2ThrowsOnInitialise"/> is <see langword="true"/>,
    /// the second module's own <c>InitialiseAsync</c> override throws a
    /// distinctive <see cref="InvalidOperationException"/>
    /// (<c>"WP1310B-DELIBERATE-INITIALISE-FAILURE"</c>) instead of the base
    /// class's default no-op — proving the first module's own lifecycle is
    /// unaffected by the second module's own failure, within the same
    /// plugin. When <see langword="false"/>, both modules behave identically
    /// to <see cref="BuildValidPluginAssembly"/>'s own simple, no-op shape.
    /// Saved to <paramref name="outputDirectory"/> under <paramref name="fileName"/>.
    /// </summary>
    /// <returns>The full path to the saved assembly file.</returns>
    public static string BuildValidPluginAssemblyWithTwoModules(
        string outputDirectory,
        string fileName,
        string module1Id,
        string module1Name,
        string module1Version,
        string module2Id,
        string module2Name,
        string module2Version,
        bool module2ThrowsOnInitialise)
    {
        var dllPath = Path.Combine(outputDirectory, fileName);

        var assemblyName = new AssemblyName($"{Path.GetFileNameWithoutExtension(fileName)}-{Guid.NewGuid():N}");
        var assemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        DefineSimpleLifecycleModuleType(
            moduleBuilder, $"{assemblyName.Name}.DynamicTwoModuleTypeOne",
            module1Id, module1Name, module1Version, throwsOnInitialise: false);
        DefineSimpleLifecycleModuleType(
            moduleBuilder, $"{assemblyName.Name}.DynamicTwoModuleTypeTwo",
            module2Id, module2Name, module2Version, throwsOnInitialise: module2ThrowsOnInitialise);

        assemblyBuilder.Save(dllPath);

        return dllPath;
    }

    /// <summary>
    /// Defines one public, concrete, <see cref="ModuleMetadataAttribute"/>-carrying
    /// <see cref="ModuleLifecycleBase"/>-derived type named <paramref name="typeName"/>
    /// into <paramref name="moduleBuilder"/>, with a public parameterless
    /// constructor calling the base constructor with the given literal
    /// Id/Name/Version. If <paramref name="throwsOnInitialise"/>, overrides
    /// <c>InitialiseAsync</c> to throw a distinctive
    /// <see cref="InvalidOperationException"/> instead of the base class's
    /// default no-op.
    /// </summary>
    private static void DefineSimpleLifecycleModuleType(
        ModuleBuilder moduleBuilder,
        string typeName,
        string moduleId,
        string moduleName,
        string moduleVersion,
        bool throwsOnInitialise)
    {
        var typeBuilder = moduleBuilder.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(ModuleLifecycleBase));

        var metadataAttributeCtor = typeof(ModuleMetadataAttribute).GetConstructor(
            [typeof(string), typeof(string), typeof(string)])!;
        typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            metadataAttributeCtor, [moduleId, moduleName, moduleVersion]));

        var baseCtor = typeof(ModuleLifecycleBase).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string), typeof(string), typeof(string)], null)!;

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            Type.EmptyTypes);

        var ctorIl = ctorBuilder.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Ldstr, moduleId);
        ctorIl.Emit(OpCodes.Ldstr, moduleName);
        ctorIl.Emit(OpCodes.Ldstr, moduleVersion);
        ctorIl.Emit(OpCodes.Call, baseCtor);
        ctorIl.Emit(OpCodes.Ret);

        if (throwsOnInitialise)
        {
            var baseInitialiseAsync = typeof(ModuleLifecycleBase).GetMethod(nameof(ModuleLifecycleBase.InitialiseAsync))!;

            var initialiseBuilder = typeBuilder.DefineMethod(
                nameof(ModuleLifecycleBase.InitialiseAsync),
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(Task),
                [typeof(CancellationToken)]);

            var exceptionCtor = typeof(InvalidOperationException).GetConstructor([typeof(string)])!;

            var il = initialiseBuilder.GetILGenerator();
            il.Emit(OpCodes.Ldstr, "WP1310B-DELIBERATE-INITIALISE-FAILURE");
            il.Emit(OpCodes.Newobj, exceptionCtor);
            il.Emit(OpCodes.Throw);

            typeBuilder.DefineMethodOverride(initialiseBuilder, baseInitialiseAsync);
        }

        typeBuilder.CreateType();
    }
}
