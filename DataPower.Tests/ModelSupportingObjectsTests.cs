using Keyfactor.Extensions.Orchestrator.DataPower.Models.SupportingObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DataPower.Tests
{
    public class DomainInfoTests
    {
        [Fact]
        public void HrefRoundTrips()
        {
            var d = new DomainInfo { Name = "default", Href = "/mgmt/domains/config/default" };
            Assert.Equal("/mgmt/domains/config/default", d.Href);
        }
    }

    // Direct coverage of CertDetailValueConverter's Read/Write branches - this handles
    // both the old ({"value": "..."}) and new ("...") shapes DataPower's REST API has
    // used across firmware versions for CryptoCertificate detail fields.
    public class CertDetailValueConverterTests
    {
        [Fact]
        public void Read_NewFormatString_SetsValue()
        {
            var result = JsonConvert.DeserializeObject<CertDetailValue>("\"abc123\"");
            Assert.Equal("abc123", result!.Value);
        }

        [Fact]
        public void Read_OldFormatObjectWithStringValue_SetsValue()
        {
            var result = JsonConvert.DeserializeObject<CertDetailValue>("{\"value\":\"abc123\"}");
            Assert.Equal("abc123", result!.Value);
        }

        [Fact]
        public void Read_TopLevelInteger_ConvertsToString()
        {
            var result = JsonConvert.DeserializeObject<CertDetailValue>("12345");
            Assert.Equal("12345", result!.Value);
        }

        [Fact]
        public void Read_OldFormatObjectWithIntegerValue_ConvertsToString()
        {
            var result = JsonConvert.DeserializeObject<CertDetailValue>("{\"value\":12345}");
            Assert.Equal("12345", result!.Value);
        }

        [Fact]
        public void Read_OldFormatObjectWithBooleanValue_UsesToString()
        {
            var result = JsonConvert.DeserializeObject<CertDetailValue>("{\"value\":true}");
            Assert.Equal("True", result!.Value);
        }

        [Fact]
        public void Read_Null_ReturnsNull()
        {
            var result = JsonConvert.DeserializeObject<CertDetailValue>("null");
            Assert.Null(result);
        }

        [Fact]
        public void Read_ObjectWithoutValueProperty_ReturnsNull()
        {
            var result = JsonConvert.DeserializeObject<CertDetailValue>("{}");
            Assert.Null(result);
        }

        [Fact]
        public void Write_NonNullValue_UsesOldFormat()
        {
            var json = JsonConvert.SerializeObject(new CertDetailValue { Value = "abc123" });
            Assert.Equal("{\"value\":\"abc123\"}", json);
        }

        [Fact]
        public void Write_NullValue_WritesJsonNull()
        {
            var wrapper = new CertificateDetailsObject { SerialNumber = null };
            var json = JsonConvert.SerializeObject(wrapper);
            Assert.Contains("\"SerialNumber\":null", json);
        }

        [Fact]
        public void WriteJson_CalledDirectlyWithNull_WritesNullToken()
        {
            // JsonConvert.SerializeObject shortcuts null property values to "null"
            // without invoking the attached converter at all, so the converter's own
            // null-check branch needs a direct call to actually execute.
            var converter = new CertDetailValueConverter();
            using var writer = new JTokenWriter();

            converter.WriteJson(writer, null!, JsonSerializer.CreateDefault());

            Assert.Equal(JTokenType.Null, writer.Token!.Type);
        }
    }
}
