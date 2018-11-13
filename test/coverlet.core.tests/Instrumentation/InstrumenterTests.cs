using System;
using System.IO;
using System.Linq;
using Xunit;
using Coverlet.Core.Instrumentation;
using Coverlet.Core.Samples.Tests;

namespace Coverlet.Core.Instrumentation.Tests
{
    public class InstrumenterTests
    {
        [Fact(Skip = "To be used only validating System.Private.CoreLib instrumentation")]
        public void TestCoreLibInstrumentation()
        {
            // Attention: to run this test adjust the paths and copy the IL only version of corelib
            const string OriginalFilesDir = @"c:\s\tmp\Coverlet-CoreLib\Original\";
            const string TestFilesDir = @"c:\s\tmp\Coverlet-CoreLib\Test\";

            Directory.CreateDirectory(TestFilesDir);

            string[] files = new[]
            {
                "System.Private.CoreLib.dll",
                "System.Private.CoreLib.pdb"
            };

            foreach (var file in files)
                File.Copy(Path.Combine(OriginalFilesDir, file), Path.Combine(TestFilesDir, file), overwrite: true);

            Instrumenter instrumenter = new Instrumenter(Path.Combine(TestFilesDir, files[0]), "_coverlet_instrumented", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
            Assert.True(instrumenter.CanInstrument());
            var result = instrumenter.Instrument();
            Assert.NotNull(result);
        }

        [Fact]
        public void TestInstrument()
        {
            var instrumenterTest = CreateInstrumentor();

            var result = instrumenterTest.Instrumenter.Instrument();

            Assert.Equal(Path.GetFileNameWithoutExtension(instrumenterTest.Module), result.Module);
            Assert.Equal(instrumenterTest.Module, result.ModulePath);

            instrumenterTest.Directory.Delete(true);
        }

        [Fact]
        public void TestInstrumentCoreLib()
        {
            var instrumenterTest = CreateInstrumentor(fakeCoreLibModule: true);

            var result = instrumenterTest.Instrumenter.Instrument();

            Assert.Equal(Path.GetFileNameWithoutExtension(instrumenterTest.Module), result.Module);
            Assert.Equal(instrumenterTest.Module, result.ModulePath);

            instrumenterTest.Directory.Delete(true);
        }

        [Theory]
        [InlineData(typeof(ClassExcludedByCodeAnalysisCodeCoverageAttr))]
        [InlineData(typeof(ClassExcludedByCoverletCodeCoverageAttr))]
        public void TestInstrument_ClassesWithExcludeAttributeAreExcluded(Type excludedType)
        {
            var instrumenterTest = CreateInstrumentor();
            var result = instrumenterTest.Instrumenter.Instrument();

            var doc = result.Documents.Values.FirstOrDefault(d => Path.GetFileName(d.Path) == "Samples.cs");
            Assert.NotNull(doc);

            var found = doc.Lines.Values.Any(l => l.Class == excludedType.FullName);
            Assert.False(found, "Class decorated with with exclude attribute should be excluded");

            instrumenterTest.Directory.Delete(true);
        }

#pragma warning disable CS0612 // Type or member is obsolete
        [Theory]
        [InlineData(nameof(ClassExcludedByObsoleteAttr))]
        [InlineData("Obsolete")]
#pragma warning restore CS0612 // Type or member is obsolete
        public void TestInstrument_ClassesWithCustomExcludeAttributeAreExcluded(string excludedAttribute)
        {
            var instrumenterTest = CreateInstrumentor(attributesToIgnore: new string[] { nameof(ObsoleteAttribute) });
            var result = instrumenterTest.Instrumenter.Instrument();

            var doc = result.Documents.Values.FirstOrDefault(d => Path.GetFileName(d.Path) == "Samples.cs");
            Assert.NotNull(doc);

            var found = doc.Lines.Values.Any(l => l.Class.Equals(excludedAttribute));
            Assert.False(found, "Class decorated with with exclude attribute should be excluded");

            instrumenterTest.Directory.Delete(true);
        }

        private InstrumenterTest CreateInstrumentor(bool fakeCoreLibModule = false, string[] attributesToIgnore = null)
        {
            string module = GetType().Assembly.Location;
            string pdb = Path.Combine(Path.GetDirectoryName(module), Path.GetFileNameWithoutExtension(module) + ".pdb");
            string identifier = Guid.NewGuid().ToString();

            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), identifier));

            string destModule, destPdb;
            if (fakeCoreLibModule)
            {
                destModule = "System.Private.CoreLib.dll";
                destPdb = "System.Private.CoreLib.pdb";
            }
            else
            {
                destModule = Path.GetFileName(module);
                destPdb = Path.GetFileName(pdb);
            }

            File.Copy(module, Path.Combine(directory.FullName, destModule), true);
            File.Copy(pdb, Path.Combine(directory.FullName, destPdb), true);

            module = Path.Combine(directory.FullName, destModule);
            Instrumenter instrumenter = new Instrumenter(module, identifier, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), attributesToIgnore);
            return new InstrumenterTest
            {
                Instrumenter = instrumenter,
                Module = module,
                Identifier = identifier,
                Directory = directory
            };
        }

        class InstrumenterTest
        {
            public Instrumenter Instrumenter { get; set; }

            public string Module { get; set; }

            public string Identifier { get; set; }

            public DirectoryInfo Directory { get; set; }
        }
    }
}