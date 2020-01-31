using System.Collections.Generic;
using Serilog.Events;
using Xunit;

namespace Serilog.Sinks.RavenDB.Tests
{
    public class RavenPropertyFormatterTests
    {
        private readonly ScalarValue _scalarComplex = new ScalarValue(new { A = 1, B = 2 });

        [Fact]
        public void ASimpleNullIsNull()
        {
            Assert.Null(RavenPropertyFormatter.Simplify(null));
        }

        [Fact]
        public void WhenAComplexTypeIsPassedAsAScalarTheResultIsAString()
        {
            var simplified = RavenPropertyFormatter.Simplify(_scalarComplex);
            Assert.IsType<string>(simplified);
        }

        [Fact]
        public void WhenASequenceIsSimplifiedTheResultIsAnArray()
        {
            var simplified = RavenPropertyFormatter.Simplify(new SequenceValue(new[] { _scalarComplex }));
            Assert.IsType<object[]>(simplified);
            Assert.IsType<string>(((object[])simplified)[0]);
        }

        [Fact]
        public void ASimplifiedStructureIsADictionary()
        {
            var simplified = RavenPropertyFormatter.Simplify(new StructureValue(new[] { new LogEventProperty("C", _scalarComplex) }));
            Assert.IsType<Dictionary<string, object>>(simplified);
            Assert.IsType<string>(((Dictionary<string, object>)simplified)["C"]);
        }
    }
}
