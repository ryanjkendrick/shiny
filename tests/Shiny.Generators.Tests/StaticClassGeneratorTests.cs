﻿using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shiny.Generators.Tasks;
using Xunit;
using Xunit.Abstractions;


namespace Shiny.Generators.Tests
{
    public class StaticClassGeneratorTests
    {
        readonly ITestOutputHelper output;
        public StaticClassGeneratorTests(ITestOutputHelper output)
            => this.output = output;


        [Fact]
        public void GenerateCoreClasses()
        {
            var assembly = new AssemblyGenerator();
            assembly.AddSource("[assembly:Shiny.GenerateStaticClassesAttribute]");

            var driver = CSharpGeneratorDriver.Create(new StaticClassGenerator());
            driver.RunGeneratorsAndUpdateCompilation(
                assembly.Create("Test.dll"),
                out var outputCompilation,
                out var diags
            );

            Assert.False(
                diags.Any(x => x.Severity == DiagnosticSeverity.Error),
                "Failed: " + diags.FirstOrDefault()?.GetMessage()
            );

            foreach (var syntaxTree in outputCompilation.SyntaxTrees)
                this.output.WriteLine(syntaxTree.ToString());
        }
    }
}
