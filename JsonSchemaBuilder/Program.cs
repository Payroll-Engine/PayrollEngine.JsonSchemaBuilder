using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;

namespace PayrollEngine.JsonSchemaBuilder;

static class Program
{
    private static FileInfo[] SourceAssemblies { get; set; }

    private static void Execute()
    {
        // arguments
        if (!TestArguments())
        {
            return;
        }

        // assembly
        var assemblyFileName = CommandLineArguments.AssemblyFileName;
        var assemblyPath = new FileInfo(assemblyFileName).FullName;
        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine($"Assembly {assemblyPath} is not available");
            ExitCode = ProgramExitCode.AssemblyError;
            return;
        }

        // change working path to find referenced assemblies
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFile(assemblyPath);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Error while loading assembly {assemblyFileName}: {exception.GetBaseException().Message}");
            ExitCode = ProgramExitCode.AssemblyError;
            return;
        }
        var assemblyDirectory = new FileInfo(assembly.Location).DirectoryName;
        if (!string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            SourceAssemblies = new DirectoryInfo(assemblyDirectory).GetFiles("*.dll");
        }

        // schema type
        Type schemaType = assembly.GetType(CommandLineArguments.TypeName);
        if (schemaType == null)
        {
            Console.WriteLine($"Schema type '{CommandLineArguments.TypeName}' is not available in assembly {assemblyPath}");
            ExitCode = ProgramExitCode.SchemaTypeError;
            return;
        }

        // schema generation
        var jsonSchema = GenerateSchema(schemaType);
        if (jsonSchema == null)
        {
            ExitCode = ProgramExitCode.SchemaGenerationError;
            return;
        }

        // schema save
        if (!SaveSchema(jsonSchema, CommandLineArguments.TargetFileName))
        {
            ExitCode = ProgramExitCode.SchemaSaveError;
            return;
        }

        // finish
        Console.WriteLine($"Payroll client model schema generated: {CommandLineArguments.TargetFileName}");
    }

    private static JsonSchema GenerateSchema(Type schemaType)
    {
        JsonSchema schema;
        try
        {
            var settings = new NewtonsoftJsonSchemaGeneratorSettings
            {
                SerializerSettings = new()
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    DefaultValueHandling = DefaultValueHandling.Ignore
                },
                AlwaysAllowAdditionalObjectProperties = true
            };
            var generator = new JsonSchemaGenerator(settings);
            schema = generator.Generate(schemaType);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Error while generating schema: {exception.GetBaseException().Message}");
            return null;
        }

        return schema;
    }

    private static bool SaveSchema(JsonSchema schema, string fileName)
    {
        try
        {
            var targetFolder = new FileInfo(fileName).DirectoryName;
            if (!string.IsNullOrWhiteSpace(targetFolder) && !Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            // target file
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            File.WriteAllText(fileName, schema.ToJson());
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Error while storing schema file: {exception.GetBaseException().Message}");
            return false;
        }
        return true;
    }

    private static bool TestArguments()
    {
        // arguments
        if (string.IsNullOrWhiteSpace(CommandLineArguments.AssemblyFileName) &&
            string.IsNullOrWhiteSpace(CommandLineArguments.TypeName) &&
            string.IsNullOrWhiteSpace(CommandLineArguments.TargetFileName))
        {
            ShowHelp();
            return false;
        }

        if (string.IsNullOrWhiteSpace(CommandLineArguments.AssemblyFileName))
        {
            Console.WriteLine("Missing argument: assembly file name");
            return false;
        }

        if (string.IsNullOrWhiteSpace(CommandLineArguments.TypeName))
        {
            Console.WriteLine("Missing argument: type name");
            return false;
        }

        if (string.IsNullOrWhiteSpace(CommandLineArguments.TargetFileName))
        {
            Console.WriteLine("Missing argument: target file name");
            return false;
        }

        return true;
    }

    /// <summary>Resolves dependent assemblies, using the source assembly path</summary>
    /// <param name="sender">The sender</param>
    /// <param name="args">The <see cref="ResolveEventArgs"/> instance containing the event data.</param>
    /// <returns>The assembly</returns>
    private static Assembly AssemblyResolveEventHandler(object sender, ResolveEventArgs args)
    {
        if (args.RequestingAssembly == null || SourceAssemblies == null)
        {
            return null;
        }

        // assembly name
        var assemblyName = args.Name;
        var index = assemblyName.IndexOf(',');
        if (index > 0)
        {
            assemblyName = assemblyName.Substring(0, index);
        }
        if (!assemblyName.EndsWith(".dll"))
        {
            assemblyName += ".dll";
        }

        // assembly
        var assemblyInfo = SourceAssemblies.FirstOrDefault(fi => fi.Name == assemblyName);
        if (assemblyInfo == null)
        {
            return null;
        }

        var assembly = Assembly.LoadFile(assemblyInfo.FullName);
        return assembly;
    }

    #region Help & Output

    /// <summary>Show the help screen</summary>
    private static void ShowHelp()
    {
        Console.WriteLine("Usage: JsonSchemaBuilder AssemblyFileName AssemblyType TargetFileName");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("    JsonSchemaBuilder MySourceLibrary.dll MyLibraryType MyType.schema.json");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("0   Ok");
        Console.WriteLine("1   Generic error");
        Console.WriteLine("1   Assembly error");
        Console.WriteLine("2   Schema type error");
        Console.WriteLine("3   Schema generation error");
        Console.WriteLine();
        Console.Write("Press any key...");
        Console.ReadKey();
    }

    #endregion

    private enum ProgramExitCode
    {
        GenericError = 1,
        AssemblyError = 2,
        SchemaTypeError = 3,
        SchemaGenerationError = 4,
        SchemaSaveError = 5
    }

    private static ProgramExitCode ExitCode
    {
        set => Environment.ExitCode = (int)value;
    }

    static void Main()
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveEventHandler;
            Program.Execute();
        }
        catch (Exception exception)
        {
            ExitCode = ProgramExitCode.GenericError;
            Console.WriteLine($"Application error: {exception.GetBaseException().Message}");
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolveEventHandler;
        }
    }
}