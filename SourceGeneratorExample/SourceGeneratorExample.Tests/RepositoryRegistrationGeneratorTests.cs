using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace SourceGeneratorExample.Tests;

public class RepositoryRegistrationGeneratorTests
{
    private readonly VerifySettings _verifySettings = new ();

    public RepositoryRegistrationGeneratorTests()
    {
        _verifySettings.UseDirectory("TestsResults");
    }
    
    [Fact]
    public Task GeneratesRepositoryRegistration_WhenRepositoryExists()
    {
        // 1. Arrange: Definir el código fuente de entrada
        const string source = $$"""
                                using {{RepositoryMarker.MarkerNamespace}};
                                namespace MyApplication.Data
                                {
                                    public class UserRepository : {{RepositoryMarker.MarkerInterfaceName}} { }
                                    public class ProductRepository : {{RepositoryMarker.MarkerInterfaceName}} { }
                                    public abstract class BaseRepository : {{RepositoryMarker.MarkerInterfaceName}} { } // No debe ser registrado
                                    public class NotARepository { } // No debe ser registrado
                                }
                                """;

        // 2. Act: Ejecutar el generador
        var compilation = CSharpCompilation.Create(
            "MyTestAssembly",
            [ CSharpSyntaxTree.ParseText(source) ],
            [ MetadataReference.CreateFromFile(typeof(object).Assembly.Location) ]
        );

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new RepositoryRegistrationGenerator())
            .RunGenerators(compilation);

        // 3. Assert: Verificar la salida con Verify
        // Esta prueba debe generar los registros para 
        // UserRepository y ProductRepository en la extensión
        return Verifier
            .Verify(
                driver.GetRunResult().Results.Single(),
                _verifySettings
            );
    }
    
    [Fact]
    public void IncrementalGenerator_CachesOutputs()
    {
        // 1. Arrange: Definir el código fuente de entrada
        const string initialSource = $$"""
                                       using {{RepositoryMarker.MarkerNamespace}};
                                       namespace MyApplication.Data
                                       {
                                           public class UserRepository : {{RepositoryMarker.MarkerInterfaceName}} { }
                                       }
                                       """;
        SyntaxTree initialSyntaxTree = CSharpSyntaxTree.ParseText(initialSource, path: "TestFile.cs");
        var initialCompilation = CSharpCompilation.Create(
            "IncrementalTestAssembly",
            [ initialSyntaxTree ],
            [ MetadataReference
                .CreateFromFile( typeof(object).Assembly.Location ) 
            ]
        );

        // 2. Act: Ejecutar el generador
        var generator = new RepositoryRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(
                generators: [generator.AsSourceGenerator() ],
                driverOptions: new GeneratorDriverOptions(
                    IncrementalGeneratorOutputKind.None, 
                    trackIncrementalGeneratorSteps: true
                    )

            )
            .RunGenerators(initialCompilation);

        
        // 3. Arrange: Agregar una clase que no es registrable
        const string modifiedSource = $$"""
                                          using {{RepositoryMarker.MarkerNamespace}};
                                          namespace MyApplication.Data
                                          {
                                              public class UserRepository : {{RepositoryMarker.MarkerInterfaceName}} { }
                                              
                                              // Este cambio no debería provocar la regeneración de la salida
                                              // porque la clase no implementa la interfaz del marcador.
                                              public class NotARelevantChange { }
                                          }
                                          """;
        SyntaxTree modifiedSyntaxTree = CSharpSyntaxTree
            .ParseText(modifiedSource, path: "TestFile.cs");
        CSharpCompilation incrementalCompilation = initialCompilation
            .ReplaceSyntaxTree(initialSyntaxTree, modifiedSyntaxTree);
        
        
        // 4. Act: Ejecutar el generador
        driver = driver.RunGenerators(incrementalCompilation);
        GeneratorRunResult result = driver
            .GetRunResult()
            .Results
            .Single();
        
        
        // 5. Assert: El paso [CheckClassDeclarations]
        // debe identificar el "fichero" modificado
        // El paso [CheckValidClasses] debería ser
        // distinto de New o Modified porque se usa el Caché
        
        // En caso de querer usar Verify
        
        // await Verifier
        //     .Verify(
        //         driver.GetRunResult().Results.Single(),
        //         _verifySettings
        //     );
        
        // var stepResults = new
        // {
        //     FinalOutput = result.TrackedOutputSteps
        //         .SelectMany(outputStep => outputStep.Value)
        //         .SelectMany(output => output.Outputs)
        //         .Single().Reason.ToString(),
        //
        //     CheckClassDeclarations = result.TrackedSteps["CheckClassDeclarations"]
        //         .SelectMany(it => it.Outputs)
        //         .Single().Reason.ToString(),
        //
        //     CheckValidClasses = result.TrackedSteps["CheckValidClasses"]
        //         .Single()
        //         .Outputs
        //         .Single().Reason.ToString()
        // };
        
        //await Verifier.Verify(stepResults, _verifySettings);

        // Usando aserciones
        
        var allOutputs = result
            .TrackedOutputSteps
            .SelectMany(outputStep => outputStep.Value)
            .SelectMany(output => output.Outputs);
        
        (object Value, IncrementalStepRunReason Reason) output = Assert.Single(allOutputs);
        Assert.Equal(IncrementalStepRunReason.Cached, output.Reason);
        
        var assemblyNameOutputs = result
            .TrackedSteps["CheckClassDeclarations"]
            .SelectMany(it => it.Outputs);
        
        output = Assert.Single(assemblyNameOutputs);
        Assert.Equal(IncrementalStepRunReason.Modified, output.Reason);
        var syntaxOutputs = result
            .TrackedSteps["CheckValidClasses"]
            .Single()
            .Outputs;
        
        output = Assert.Single(syntaxOutputs);
        Assert.Equal(IncrementalStepRunReason.Cached, output.Reason);
    }

}