﻿using System;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;


namespace Shiny.Generators.Tests
{
    public class StartupGenerationTests : IDisposable
    {
        readonly ITestOutputHelper output;
        readonly AssemblyGenerator generator;
        Compilation compilation;


        public StartupGenerationTests(ITestOutputHelper output)
        {
            this.output = output;
            this.generator = new AssemblyGenerator();
            this.generator.AddReference("Mono.Android");
            this.generator.AddReference("Shiny");
            this.generator.AddReference("Shiny.Core");
        }


        public void Dispose()
        {
            if (this.compilation != null)
                this.output.WriteSyntaxTrees(this.compilation);
        }


        [Fact]
        public void Test()
        {
            this.generator.AddSource("[assembly: Shiny.ShinyApplicationAttribute]");
            this.compilation = this.generator.DoGenerate(
                nameof(Test),
                new AndroidApplicationSourceGenerator()
            );
            //compile.AssertTypesExist(""); // android app, shiny startup
        }


        [Fact]
        public void JobDetection()
        {
            this.generator.AddSource(@"
[assembly: Shiny.ShinyApplicationAttribute]
using System;
using System.Threading;
using System.Threading.Tasks;


namespace MyTest
{
    public class DetectionJob : Shiny.Jobs.IJob
    {
        public async Task Run(JobInfo jobInfo, CancellationToken cancelToken) {}
    }
}");
            this.compilation = this.generator.DoGenerate(
                nameof(JobDetection),
                new AndroidApplicationSourceGenerator()
            );
        }


        [Fact]
        public void ExistingStartupDetectionSameAssembly()
        {
            this.generator.AddSource(@"
[assembly: Shiny.ShinyApplicationAttribute]
using System;
using System.Threading;
using System.Threading.Tasks;


namespace MyTest
{
    public class ExistingStartup : Shiny.ShinyStartup
    {
         public override void ConfigureServices(IServiceCollection services) {}
    }
}");
            this.compilation = this.generator.DoGenerate(
                nameof(ExistingStartupDetectionSameAssembly),
                new AndroidApplicationSourceGenerator()
            );

            this.compilation.GetTypeByMetadataName("MyTest.AppShinyStartup").Should().BeNull("it shouldn't have been auto-generated");
            this.compilation.GetTypeByMetadataName("MyTest.ExistingStartup").Should().NotBeNull("it was created");
        }


        public void ExistingStartupDefined()
        {
        }


        public void ModuleDetection()
        {
        }


        public void StartupTaskDetection()
        {
        }


        [Fact]
        public void DelegateDetection()
        {
            this.generator.AddReference("Shiny.Locations");
            this.generator.AddReference("Shiny.Locations.Abstractions");
            this.generator.AddSource(@"
[assembly: Shiny.ShinyApplicationAttribute]
namespace Test
{
    public class TestGpsDelegate : Shiny.Locations.IGpsDelegate
    {
        public Task OnReading(IGpsReading reading) => throw new NotImplementedException();
    }
}");
            this.compilation = this.generator.DoGenerate(
                nameof(DelegateDetection),
                new AndroidApplicationSourceGenerator()
            );
        }
    }
}
