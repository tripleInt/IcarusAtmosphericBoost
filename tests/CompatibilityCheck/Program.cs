using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private const BindingFlags DeclaredStatic = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private static string[] searchDirectories;
    private static int checks;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 3)
                throw new ArgumentException("Usage: CompatibilityCheck.exe [GameDirectory] [BepInExDirectory] [ModDllPath]");

            string gameDirectory = args.Length > 0 ? args[0]
                : @"C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program";
            string bepDirectory = args.Length > 1 ? args[1] : Path.Combine(gameDirectory, "BepInEx");
            string modPath = args.Length > 2 ? args[2]
                : Path.Combine(FindProjectDirectory(), @"dist\IcarusAtmosphericBoost.dll");
            string gameManaged = Path.Combine(gameDirectory, @"DSPGAME_Data\Managed");
            string gamePath = Path.Combine(gameManaged, "Assembly-CSharp.dll");
            searchDirectories = new[] { gameManaged, Path.Combine(bepDirectory, "core"), Path.GetDirectoryName(Path.GetFullPath(modPath)) };
            Require(File.Exists(gamePath), "Game assembly exists: " + gamePath);
            Require(File.Exists(modPath), "Compiled mod exists: " + modPath);

            // Reflection-only loading never executes game/plugin initializers, attribute
            // constructors, Harmony detours, or any Unity native calls.
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveMetadataAssembly;
            Assembly game = Assembly.ReflectionOnlyLoadFrom(gamePath);
            Assembly mod = Assembly.ReflectionOnlyLoadFrom(modPath);

            var expectedTargets = new Dictionary<string, string[]>
            {
                { "PlayerController.GameTick", new[] { "System.Int64" } },
                { "PlayerController.ApplyLocalForce", new string[0] },
                { "PlayerController.UpdatePhysicsDirect", new string[0] },
                { "PlayerController.Free", new string[0] },
                { "PlayerMove_Sail.GameTick", new[] { "System.Int64" } }
            };
            var methods = new Dictionary<string, MethodInfo>();
            foreach (var target in expectedTargets)
            {
                int separator = target.Key.LastIndexOf('.');
                Type type = game.GetType(target.Key.Substring(0, separator), true);
                string methodName = target.Key.Substring(separator + 1);
                MethodInfo[] matches = type.GetMethods(DeclaredInstance).Where(m => m.Name == methodName
                    && m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(target.Value)).ToArray();
                Require(matches.Length == 1 && matches[0].ReturnType.FullName == "System.Void",
                    "Target signature: " + target.Key + "(" + string.Join(", ", target.Value) + ") -> void");
                methods.Add(target.Key, matches[0]);
            }

            Type controller = game.GetType("PlayerController", true);
            CheckField(controller, "_lForce", "VectorLF3");
            CheckField(controller, "rigidbodySleep", "System.Boolean");
            var declaredTargets = new HashSet<string>();
            foreach (Type patchType in mod.GetTypes())
            {
                foreach (CustomAttributeData attribute in CustomAttributeData.GetCustomAttributes(patchType)
                    .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
                {
                    var arguments = attribute.ConstructorArguments;
                    Require(arguments.Count == 2 && arguments[0].Value is Type && arguments[1].Value is string,
                        "Harmony target declaration: " + patchType.FullName);
                    Type targetType = (Type)arguments[0].Value;
                    string targetKey = targetType.FullName + "." + (string)arguments[1].Value;
                    Require(methods.ContainsKey(targetKey), "Harmony target exists in inspected game: " + targetKey);
                    Require(declaredTargets.Add(targetKey), "No duplicate Harmony target: " + targetKey);
                    MethodInfo[] prefixes = patchType.GetMethods(DeclaredStatic).Where(m =>
                        CustomAttributeData.GetCustomAttributes(m).Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPrefix")).ToArray();
                    Require(prefixes.Length == 1 && prefixes[0].ReturnType.FullName == "System.Void",
                        "Harmony prefix signature: " + patchType.FullName);

                    foreach (ParameterInfo parameter in prefixes[0].GetParameters())
                    {
                        if (parameter.Name == "__instance")
                        {
                            Require(parameter.ParameterType.FullName == targetType.FullName,
                                "Harmony instance injection: " + patchType.Name);
                        }
                        else if (parameter.Name.StartsWith("___", StringComparison.Ordinal))
                        {
                            string fieldName = parameter.Name.Substring(3);
                            FieldInfo field = targetType.GetField(fieldName, DeclaredInstance);
                            Require(field != null && parameter.ParameterType.IsByRef
                                && parameter.ParameterType.GetElementType().FullName == field.FieldType.FullName,
                                "Harmony field injection: " + patchType.Name + "." + parameter.Name);
                        }
                        else
                            throw new InvalidOperationException("Unverified Harmony injection: " + patchType.FullName + "." + parameter.Name);
                    }
                }
            }
            Require(declaredTargets.SetEquals(expectedTargets.Keys), "All five expected targets are patched by the compiled mod");

            Type plugin = mod.GetType("IcarusAtmosphericBoost.Plugin", true);
            Require(plugin.BaseType.FullName == "BepInEx.BaseUnityPlugin", "Plugin derives from BepInEx.BaseUnityPlugin");
            var pluginAttributes = CustomAttributeData.GetCustomAttributes(plugin);
            Require(pluginAttributes.Any(a => a.AttributeType.FullName == "BepInEx.BepInPlugin"
                && a.ConstructorArguments.Count == 3
                && (string)a.ConstructorArguments[0].Value == "local.dsp.icarusatmosphericboost"
                && (string)a.ConstructorArguments[2].Value == "1.1.0"), "BepInEx plugin identity and version metadata");
            Require(pluginAttributes.Any(a => a.AttributeType.FullName == "BepInEx.BepInProcess"
                && a.ConstructorArguments.Count == 1
                && (string)a.ConstructorArguments[0].Value == "DSPGAME.exe"), "BepInEx process filter is DSPGAME.exe");
            Console.WriteLine("PASS: " + checks + " static compatibility checks. No Unity calls or game execution performed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void CheckField(Type owner, string name, string expectedType)
    {
        FieldInfo field = owner.GetField(name, DeclaredInstance);
        Require(field != null && field.FieldType.FullName == expectedType && field.IsPrivate,
            "Private instance field: " + owner.FullName + "." + name + " : " + expectedType);
    }

    private static Assembly ResolveMetadataAssembly(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name);
        Assembly loaded = AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == name.Name);
        if (loaded != null)
            return loaded;
        foreach (string directory in searchDirectories)
        {
            string candidate = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(candidate))
                return Assembly.ReflectionOnlyLoadFrom(candidate);
        }
        return Assembly.ReflectionOnlyLoad(args.Name);
    }

    private static string FindProjectDirectory()
    {
        DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "IcarusAtmosphericBoost.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Cannot find the mod project. Pass ModDllPath as the third argument.");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        checks++;
        Console.WriteLine("PASS " + description);
    }
}
