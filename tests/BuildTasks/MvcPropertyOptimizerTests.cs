using System.Collections;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RazorSlices.BuildTasks.Tests;

public class MvcPropertyOptimizerTests
{
    [Fact]
    public async Task RemovesDefaultMembersAndPreservesRendering()
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        var messages = new List<string>();
        Assert.Equal(16, MvcPropertyOptimizer.Remove(module, new HashSet<string>(), messages.Add));
        var slice = GetType(module, nameof(UnusedSlice));
        Assert.Equal(["CustomService"], slice.Properties.Select(p => p.Name));
        Assert.Equal(["<CustomService>k__BackingField"], slice.Fields.Select(f => f.Name));
        Assert.DoesNotContain(slice.Methods, m => m.Name is "get_Url" or "set_Url");
        Assert.Empty(GetType(module, nameof(GenericSlice)).Properties);
        Assert.NotEmpty(messages);
        Assert.Equal(0, MvcPropertyOptimizer.Remove(module, new HashSet<string>(), messages.Add));

        using var stream = new MemoryStream();
        module.Write(stream);
        stream.Position = 0;
        var context = new AssemblyLoadContext("RewrittenSlice", isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            using var instance = (RazorSlice)Activator.CreateInstance(
                assembly.GetType(typeof(UnusedSlice).FullName!)!, nonPublic: true)!;
            Assert.Equal("Hello 42", await instance.RenderAsync());
        }
        finally
        {
            context.Unload();
        }
    }

    [Theory]
    [InlineData(nameof(MinimalSlice))]
    [InlineData(nameof(MinimalModelSlice))]
    [InlineData(nameof(CustomMinimalSlice))]
    [InlineData(nameof(CustomFullSlice))]
    [InlineData(nameof(HelperImplementation))]
    [InlineData(nameof(CustomHelperImplementation))]
    public void RemovesUnusedPropertiesAcrossSliceAndHelperHierarchies(string name)
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        MvcPropertyOptimizer.Remove(module, new HashSet<string>(), _ => { });
        var type = GetType(module, name);
        Assert.Empty(type.Properties);
        Assert.Empty(type.Fields);
    }

    [Theory]
    [InlineData(nameof(MinimalSlice))]
    [InlineData(nameof(MinimalModelSlice))]
    [InlineData(nameof(HelperImplementation))]
    public void NewHierarchiesPreserveExternallyAccessibleProperties(string name)
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        var type = GetType(module, name);
        type.IsPublic = true;
        MvcPropertyOptimizer.Remove(module, new HashSet<string>(), _ => { });
        Assert.NotEmpty(type.Properties);
    }

    [Theory]
    [InlineData(nameof(MinimalSlice), "Json")]
    [InlineData(nameof(MinimalModelSlice), "Json")]
    [InlineData(nameof(HelperImplementation), "Url")]
    public void NewHierarchiesPreserveExplicitInjectionNames(string name, string property)
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        MvcPropertyOptimizer.Remove(module, new HashSet<string> { property }, _ => { });
        Assert.Contains(GetType(module, name).Properties, p => p.Name == property);
    }

    [Theory]
    [InlineData(nameof(MinimalSlice))]
    [InlineData(nameof(HelperImplementation))]
    public void NewHierarchiesPreserveReferencedProperties(string name)
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        var type = GetType(module, name);
        var getter = type.Properties.Single(p => p.Name == "Url").GetMethod;
        var execute = type.Methods.Single(m => m.Name == "ExecuteAsync");
        var il = execute.Body.GetILProcessor();
        var first = execute.Body.Instructions[0];
        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, getter));
        il.InsertBefore(first, Instruction.Create(OpCodes.Pop));
        MvcPropertyOptimizer.Remove(module, new HashSet<string>(), _ => { });
        Assert.Contains(type.Properties, p => p.Name == "Url");
    }

    [Theory]
    [InlineData(nameof(MinimalSlice), "RazorSliceBase")]
    [InlineData(nameof(HelperImplementation), "RazorSliceHelpers")]
    public void DoesNotOptimizeUnrelatedTypesWithTheSameBaseTypeName(string name, string baseName)
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        var unrelatedBase = new TypeDefinition("RazorSlices", baseName, TypeAttributes.Public | TypeAttributes.Abstract,
            module.TypeSystem.Object);
        module.Types.Add(unrelatedBase);
        var type = GetType(module, name);
        type.BaseType = unrelatedBase;
        MvcPropertyOptimizer.Remove(module, new HashSet<string>(), _ => { });
        Assert.NotEmpty(type.Properties);
    }

    [Theory]
    [InlineData(nameof(ReferencedSlice))]
    [InlineData(nameof(NonSlice))]
    [InlineData(nameof(PublicSlice))]
    [InlineData(nameof(NotAutoPropertySlice))]
    [InlineData(nameof(AuthoredSlice))]
    public void PreservesIneligibleProperties(string name)
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        MvcPropertyOptimizer.Remove(module, new HashSet<string>(), _ => { });
        Assert.Contains(GetType(module, name).Properties, p => p.Name == "Url");
    }

    [Fact]
    public void PreservesExplicitInjectionNames()
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        MvcPropertyOptimizer.Remove(module, new HashSet<string> { "Json" }, _ => { });
        Assert.Contains(GetType(module, nameof(UnusedSlice)).Properties, p => p.Name == "Json");
        Assert.Single(GetType(module, nameof(GenericSlice)).Properties);
    }

    [Fact]
    public void PreservesPropertiesExposedToFriendAssemblies()
    {
        using var resolver = CreateResolver();
        using var module = ReadFixture(resolver);
        var attribute = new CustomAttribute(module.ImportReference(
            typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute).GetConstructor([typeof(string)])!));
        attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "Friend"));
        module.Assembly.CustomAttributes.Add(attribute);
        Assert.Equal(0, MvcPropertyOptimizer.Remove(module, new HashSet<string>(), _ => { }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SignedAssembliesRequireAndPreserveSigningKey(bool supplyKey)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RazorSlices.BuildTasks.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var rsa = new RSACryptoServiceProvider(2048);
            var key = rsa.ExportCspBlob(includePrivateParameters: true);
            var keyPath = Path.Combine(directory, "key.snk");
            File.WriteAllBytes(keyPath, key);
            var path = Path.Combine(directory, "Fixture.dll");
            byte[] publicKey;
            using (var resolver = CreateResolver())
            using (var module = ReadFixture(resolver))
            {
                module.Write(path, new WriterParameters { StrongNameKeyBlob = key });
                publicKey = module.Assembly.Name.PublicKey;
            }
            var original = File.ReadAllBytes(path);
            var engine = new TestBuildEngine();
            var task = new RemoveMvcDefaultInjectedProperties
            {
                BuildEngine = engine,
                AssemblyFile = path,
                DebugType = "none",
                AssemblyOriginatorKeyFile = supplyKey ? keyPath : "",
                References = GetReferences()
            };
            Assert.Equal(supplyKey, task.Execute());
            if (supplyKey)
            {
                using var rewritten = ModuleDefinition.ReadModule(path);
                Assert.True((rewritten.Attributes & ModuleAttributes.StrongNameSigned) != 0);
                Assert.Equal(publicKey, rewritten.Assembly.Name.PublicKey);
                Assert.Equal(["CustomService"], GetType(rewritten, nameof(UnusedSlice)).Properties.Select(p => p.Name));
            }
            else
            {
                Assert.Contains(engine.Errors, e => e.Contains("RazorSlicesRemoveMvcDefaultInjectedProperties=false"));
                Assert.Equal(original, File.ReadAllBytes(path));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("portable")]
    [InlineData("embedded")]
    [InlineData("none")]
    public void TaskPreservesSymbolFormatAndExplicitInjections(string symbols)
    {
        var directory = Path.Combine(Path.GetTempPath(), "RazorSlices.BuildTasks.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "Fixture.dll");
            using (var resolver = CreateResolver())
            using (var module = ReadFixture(resolver, readSymbols: symbols != "none"))
            {
                module.Write(path, new WriterParameters
                {
                    WriteSymbols = symbols != "none",
                    SymbolWriterProvider = symbols == "none" ? null
                        : symbols == "embedded" ? new EmbeddedPortablePdbWriterProvider()
                        : new PortablePdbWriterProvider()
                });
            }
            var razorFile = Path.Combine(directory, "_ViewImports.cshtml");
            File.WriteAllText(razorFile, "@inject Microsoft.AspNetCore.Mvc.Rendering.IJsonHelper Json");
            var engine = new TestBuildEngine();
            var task = new RemoveMvcDefaultInjectedProperties
            {
                BuildEngine = engine,
                AssemblyFile = path,
                DebugType = symbols,
                References = GetReferences(),
                RazorFiles = [new TaskItem(razorFile)]
            };
            Assert.True(task.Execute(), string.Join(Environment.NewLine, engine.Errors));
            Assert.Empty(engine.Errors);
            using var rewritten = ModuleDefinition.ReadModule(path, new ReaderParameters
            {
                ReadSymbols = symbols != "none"
            });
            Assert.Equal(symbols != "none", rewritten.HasSymbols);
            Assert.Equal(symbols == "portable", File.Exists(Path.ChangeExtension(path, ".pdb")));
            Assert.Equal(["Json", "CustomService"], GetType(rewritten, nameof(UnusedSlice)).Properties.Select(p => p.Name));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static TypeDefinition GetType(ModuleDefinition module, string name) =>
        module.Types.Single(t => t.Name == name);

    private static ITaskItem[] GetReferences() =>
        AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && a.Location.Length > 0)
            .Select(a => (ITaskItem)new TaskItem(a.Location)).ToArray();

    private static ModuleDefinition ReadFixture(IAssemblyResolver resolver, bool readSymbols = false) =>
        ModuleDefinition.ReadModule(typeof(UnusedSlice).Assembly.Location, new ReaderParameters
        {
            AssemblyResolver = resolver,
            InMemory = true,
            ReadSymbols = readSymbols
        });

    private static DefaultAssemblyResolver CreateResolver()
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(RazorSlice).Assembly.Location));
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
        return resolver;
    }

    private sealed class TestBuildEngine : IBuildEngine
    {
        public List<string> Errors { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "";
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e.Message!);
        public void LogWarningEvent(BuildWarningEventArgs e) { }
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) =>
            throw new NotSupportedException();
    }
}
