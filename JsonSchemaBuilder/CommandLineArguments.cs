using System;

namespace PayrollEngine.JsonSchemaBuilder;

public static class CommandLineArguments
{
    private static string[] CommandLineArgs { get; } = Environment.GetCommandLineArgs();

    public static string AssemblyFileName =>
        GetArgument(1);

    public static string TypeName =>
        GetArgument(2);

    public static string TargetFileName =>
        GetArgument(3);

    private static string GetArgument(int index) =>
        CommandLineArgs.Length <= index ? null : CommandLineArgs[index];
}