using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RazorSlices.BuildTasks;

public sealed class RemoveMvcDefaultInjectedProperties : Task
{
    [Required]
    public string AssemblyFile { get; set; } = "";

    public ITaskItem[] References { get; set; } = [];

    public ITaskItem[] RazorFiles { get; set; } = [];

    public string AssemblyOriginatorKeyFile { get; set; } = "";

    public bool PublicSign { get; set; }

    public string DebugType { get; set; } = "";

    public override bool Execute()
    {
        var explicitInjections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in RazorFiles)
        {
            // Conservatively preserve explicitly injected names project-wide, including imports.
            foreach (Match match in Regex.Matches(File.ReadAllText(file.ItemSpec),
                @"@inject\b[^\r\n]*\b(Html|Json|Component|Url|ModelExpressionProvider)\b"))
            {
                explicitInjections.Add(match.Groups[1].Value);
            }
        }

        using var resolver = new DefaultAssemblyResolver();
        foreach (var directory in References.Select(r => Path.GetDirectoryName(r.GetMetadata("FullPath"))).Distinct())
        {
            resolver.AddSearchDirectory(directory);
        }

        var readSymbols = !string.Equals(DebugType, "none", StringComparison.OrdinalIgnoreCase);
        using var module = ModuleDefinition.ReadModule(AssemblyFile, new ReaderParameters
        {
            AssemblyResolver = resolver,
            InMemory = true,
            ReadSymbols = readSymbols,
            SymbolReaderProvider = !readSymbols ? null
                : string.Equals(DebugType, "embedded", StringComparison.OrdinalIgnoreCase)
                ? new EmbeddedPortablePdbReaderProvider()
                : new DefaultSymbolReaderProvider(throwIfNoSymbol: false)
        });

        var removed = MvcPropertyOptimizer.Remove(module, explicitInjections,
            message => Log.LogMessage(MessageImportance.Normal, message));
        if (removed == 0)
        {
            return true;
        }

        var writer = new WriterParameters
        {
            DeterministicMvid = true,
            WriteSymbols = module.HasSymbols,
            SymbolWriterProvider = module.HasSymbols ? module.SymbolReader.GetWriterProvider() : null
        };
        if ((module.Attributes & ModuleAttributes.StrongNameSigned) != 0)
        {
            if (PublicSign || string.IsNullOrEmpty(AssemblyOriginatorKeyFile))
            {
                Log.LogError("Razor Slices cannot rewrite this signed assembly without a private signing key. " +
                    "Set RazorSlicesRemoveMvcDefaultInjectedProperties=false to disable rewriting.");
                return false;
            }
            writer.StrongNameKeyBlob = File.ReadAllBytes(AssemblyOriginatorKeyFile);
        }
        module.Write(AssemblyFile, writer);
        Log.LogMessage(MessageImportance.Normal,
            $"Razor Slices removed {removed} unused MVC default injected properties from '{AssemblyFile}'.");
        return true;
    }
}

internal static class MvcPropertyOptimizer
{
    private static readonly Dictionary<string, string> DefaultProperties = new(StringComparer.Ordinal)
    {
        ["Html"] = "Microsoft.AspNetCore.Mvc.Rendering.IHtmlHelper`1",
        ["Json"] = "Microsoft.AspNetCore.Mvc.Rendering.IJsonHelper",
        ["Component"] = "Microsoft.AspNetCore.Mvc.IViewComponentHelper",
        ["Url"] = "Microsoft.AspNetCore.Mvc.IUrlHelper",
        ["ModelExpressionProvider"] = "Microsoft.AspNetCore.Mvc.ViewFeatures.IModelExpressionProvider"
    };

    internal static int Remove(ModuleDefinition module, ISet<string> explicitInjections, Action<string> log)
    {
        var types = GetTypes(module.Types).ToArray();
        var removed = 0;
        foreach (var type in types)
        {
            if (!type.CustomAttributes.Any(a =>
                    a.AttributeType.FullName == "Microsoft.AspNetCore.Razor.Hosting.RazorCompiledItemMetadataAttribute")
                || !DerivesFromSlice(type))
            {
                continue;
            }

            // Public template types can be referenced by assemblies outside this compilation.
            if (type.IsPublic || type.IsNestedPublic || module.Assembly.CustomAttributes.Any(a =>
                a.AttributeType.FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute"))
            {
                log($"Razor Slices retained MVC properties on externally accessible template '{type.FullName}'.");
                continue;
            }

            foreach (var property in type.Properties.ToArray())
            {
                if (!DefaultProperties.TryGetValue(property.Name, out var expectedType)
                    || property.PropertyType.GetElementType().FullName != expectedType
                    || !property.CustomAttributes.Any(a =>
                        a.AttributeType.FullName == "Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute"))
                {
                    continue;
                }

                if (explicitInjections.Contains(property.Name))
                {
                    log($"Razor Slices retained '{property.FullName}' because its name is explicitly injected in this project.");
                    continue;
                }

                var field = type.Fields.SingleOrDefault(f => f.Name == $"<{property.Name}>k__BackingField");
                if (field is null || !field.IsPrivate || field.IsStatic
                    || field.FieldType.FullName != property.PropertyType.FullName
                    || !IsAutoAccessor(property.GetMethod, field, setter: false)
                    || !IsAutoAccessor(property.SetMethod, field, setter: true)
                    || property.OtherMethods.Count != 0)
                {
                    log($"Razor Slices retained '{property.FullName}' because it is not a recognized auto-property.");
                    continue;
                }

                var initializers = new List<Instruction>();
                if (IsReferenced(types, property, field, initializers))
                {
                    log($"Razor Slices retained '{property.FullName}' because compiled code references it.");
                    continue;
                }

                // Preserve instruction identities for branch targets and portable PDB sequence points.
                foreach (var instruction in initializers)
                {
                    instruction.OpCode = OpCodes.Nop;
                    instruction.Operand = null;
                }
                type.Methods.Remove(property.GetMethod);
                type.Methods.Remove(property.SetMethod);
                type.Fields.Remove(field);
                type.Properties.Remove(property);
                removed++;
            }
        }
        return removed;
    }

    private static bool DerivesFromSlice(TypeDefinition type)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.Resolve().BaseType)
        {
            if (!visited.Add(baseType.FullName))
            {
                throw new InvalidOperationException($"Cyclic base type hierarchy on '{type.FullName}'.");
            }
            if (baseType.GetElementType().FullName == "RazorSlices.RazorSlice"
                && baseType.Scope.Name is "RazorSlices" or "RazorSlices.dll" or "RazorSlices.ForBenchmarks" or "RazorSlices.ForBenchmarks.dll")
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsAutoAccessor(MethodDefinition? method, FieldDefinition field, bool setter)
    {
        if (method is null || !method.HasBody || method.IsStatic || method.IsVirtual
            || method.Body.ExceptionHandlers.Count != 0)
        {
            return false;
        }
        var instructions = method.Body.Instructions.Where(i => i.OpCode != OpCodes.Nop).ToArray();
        return setter
            ? method.IsPrivate && instructions.Length == 4
                && instructions[0].OpCode == OpCodes.Ldarg_0
                && instructions[1].OpCode == OpCodes.Ldarg_1
                && instructions[2].OpCode == OpCodes.Stfld && instructions[2].Operand == field
                && instructions[3].OpCode == OpCodes.Ret
            : method.IsPublic && instructions.Length == 3
                && instructions[0].OpCode == OpCodes.Ldarg_0
                && instructions[1].OpCode == OpCodes.Ldfld && instructions[1].Operand == field
                && instructions[2].OpCode == OpCodes.Ret;
    }

    private static bool IsReferenced(TypeDefinition[] types, PropertyDefinition property, FieldDefinition field,
        List<Instruction> initializers)
    {
        foreach (var method in types.SelectMany(t => t.Methods))
        {
            if (!method.HasBody || method == property.GetMethod || method == property.SetMethod)
            {
                continue;
            }
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is MethodReference called
                    && (called.FullName == property.GetMethod.FullName || called.FullName == property.SetMethod.FullName))
                {
                    return true;
                }
                if (instruction.Operand is FieldReference used && used.FullName == field.FullName)
                {
                    if (method.DeclaringType == field.DeclaringType && method.IsConstructor && !method.IsStatic
                        && instruction.OpCode == OpCodes.Stfld
                        && instruction.Previous?.OpCode == OpCodes.Ldnull
                        && instruction.Previous.Previous?.OpCode == OpCodes.Ldarg_0
                        && !method.Body.Instructions.Any(i => i.Operand == instruction || i.Operand == instruction.Previous
                            || i.Operand is Instruction[] targets && targets.Any(t => t == instruction || t == instruction.Previous))
                        && method.Body.ExceptionHandlers.Count == 0)
                    {
                        initializers.Add(instruction.Previous.Previous);
                        initializers.Add(instruction.Previous);
                        initializers.Add(instruction);
                    }
                    else
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static IEnumerable<TypeDefinition> GetTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in GetTypes(type.NestedTypes))
            {
                yield return nested;
            }
        }
    }
}
