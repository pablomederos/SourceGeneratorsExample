namespace SourceGeneratorExample.Models;

internal readonly struct RepositoryToRegister
{
    public string Namespace { get; } 
    public string ClassName { get; }
    public string AssemblyName { get; }

    public RepositoryToRegister(string @namespace, string className, string assemblyName)
    {
        Namespace = @namespace;
        ClassName = className;
        AssemblyName = assemblyName;
    }

}