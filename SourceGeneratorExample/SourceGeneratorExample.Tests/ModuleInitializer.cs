using System.Runtime.CompilerServices;
using VerifyTests;

namespace SourceGeneratorExample.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
    
    
}