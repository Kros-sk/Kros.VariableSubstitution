using FluentAssertions;
using FluentAssertions.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NSubstitute;
using System.Collections.Generic;
using Xunit;

namespace Kros.VariableSubstitution.Tests
{
    public class JsonVariableSubstituterShould
    {
        [Theory]
        [MemberData(nameof(GetData))]
        public void SubstituteVariablesInJson(string sourceJson, IDictionary<string, string> variables, string expected)
        {
            ILogger logger = Substitute.For<ILogger>();
            IVariablesProvider provider = variables.AsProvider();

            var actual = JToken.Parse((new JsonVariableSubstituter(logger)).Substitute(provider, sourceJson).Result);
            var expectedJson = JToken.Parse(expected);

            actual.Should().BeEquivalentTo(expectedJson);
        }

        public static IEnumerable<object[]> GetData()
        {
            yield return new object[] {
                "{\"Foo\": \"bar\"}",
                new Dictionary<string, string>() { { "Foo", "substituted value" } },
                "{\"Foo\": \"substituted value\"}" };

            yield return new object[] {
                "{\"Foo\": \"\"}",
                new Dictionary<string, string>() { { "foo", "substituted value" } },
                "{\"Foo\": \"substituted value\"}" };

            yield return new object[] {
                "{\"Foo\": 22}",
                new Dictionary<string, string>() { { "foo", "11" } },
                "{\"Foo\": 11}" };

            yield return new object[] {
                "{\"Foo\": true, \"Bar\": 25}",
                new Dictionary<string, string>() { { "foo", "false" } },
                "{\"Foo\": false, \"Bar\": 25}" };

            yield return new object[] {
                "{\"Foo\": \"\"}",
                new Dictionary<string, string>() { { "foo", "https://foo.net/api/path" } },
                "{\"Foo\": \"https://foo.net/api/path\"}" };

            yield return new object[] {
                "{\"Foo\": {\"prop1\": \"\", \"prop2\": 23}}",
                new Dictionary<string, string>() { { "foo.prop1", "injected value" } },
                "{\"Foo\": {\"prop1\": \"injected value\", \"prop2\": 23}}" };

            yield return new object[] {
                "{\"Foo\": {\"prop1\": \"\", \"prop2\": {\"BAR\": \"\"}}}",
                new Dictionary<string, string>() { { "foo.prop2.bar", "new value" } },
                "{\"Foo\": {\"prop1\": \"\", \"prop2\": {\"BAR\": \"new value\"}}}" };

            yield return new object[] {
                "{\"Foo\": [58, 96, 15, 0]}",
                new Dictionary<string, string>() { { "foo.1", "1259" } },
                "{\"Foo\": [58, 1259, 15, 0]}" };

            yield return new object[] {
                "{\"Foo\": [{\"bar\": \"value1\"}, {\"bar\": \"value2\"}, {\"bar\": \"\"}]}",
                new Dictionary<string, string>() { { "foo.2.bar", "value3" } },
                "{\"Foo\": [{\"bar\": \"value1\"}, {\"bar\": \"value2\"}, {\"bar\": \"value3\"}]}" };

            yield return new object[] {
                "{\"Foo\": [{\"bar\": \"value1\"}, {\"bar\": \"value2\"}, {\"bar\": \"\"}], \"Bar\": {\"prop1\": 25, \"prop2\":\"value\"}}",
                new Dictionary<string, string>() {
                    { "foo.0.bar", "new value" },
                    { "Bar.prop1", "0" },
                    { "Bar.prop2", "injected" },
                    { "Bar.nonexistingproperty", "value" }
                },
                "{\"Foo\": [{\"bar\": \"new value\"}, {\"bar\": \"value2\"}, {\"bar\": \"\"}], \"Bar\": {\"prop1\": 0, \"prop2\":\"injected\"}}" };
        }
    }
}
