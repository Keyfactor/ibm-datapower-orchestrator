using System;
using System.Net;
using Keyfactor.Extensions.Orchestrator.DataPower.Client;

namespace DataPower.Tests
{
    public class DataPowerApiExceptionTests
    {
        [Fact]
        public void Find_ReturnsSelf_WhenExceptionIsDataPowerApiException()
        {
            var ex = new DataPowerApiException("bad", HttpStatusCode.Forbidden, "AddCryptoCertificate", "{}");
            Assert.Same(ex, DataPowerApiException.Find(ex));
        }

        [Fact]
        public void Find_UnwrapsInnerException()
        {
            var apiEx = new DataPowerApiException("bad", HttpStatusCode.NotFound, "ListDomains", "{}");
            var wrapper = new InvalidOperationException("wrapped", apiEx);

            var found = DataPowerApiException.Find(wrapper);

            Assert.Same(apiEx, found);
        }

        [Fact]
        public void Find_UnwrapsAggregateException()
        {
            var apiEx = new DataPowerApiException("bad", HttpStatusCode.BadRequest, "AddCertificateFile", "{}");
            var agg = new AggregateException(new Exception("other"), apiEx);

            var found = DataPowerApiException.Find(agg);

            Assert.Same(apiEx, found);
        }

        [Fact]
        public void Find_ReturnsNull_WhenNoneInChain()
        {
            var ex = new InvalidOperationException("plain", new Exception("also plain"));
            Assert.Null(DataPowerApiException.Find(ex));
        }

        [Fact]
        public void Find_ReturnsNull_ForNullInput()
        {
            Assert.Null(DataPowerApiException.Find(null));
        }

        [Fact]
        public void Constructor_SetsProperties()
        {
            var ex = new DataPowerApiException("msg", HttpStatusCode.InternalServerError, "SaveConfig", "body text");

            Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
            Assert.Equal("SaveConfig", ex.Operation);
            Assert.Equal("body text", ex.ResponseBody);
            Assert.Equal("msg", ex.Message);
        }

        [Fact]
        public void Constructor_WithInnerException_SetsPropertiesAndInnerException()
        {
            var inner = new InvalidOperationException("root cause");
            var ex = new DataPowerApiException("msg", HttpStatusCode.BadGateway, "ListDomains", "body", inner);

            Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
            Assert.Equal("ListDomains", ex.Operation);
            Assert.Equal("body", ex.ResponseBody);
            Assert.Same(inner, ex.InnerException);
        }

        [Fact]
        public void Find_AggregateExceptionWithNoDataPowerApiExceptionInside_ReturnsNull()
        {
            var agg = new AggregateException(new Exception("a"), new Exception("b"));
            Assert.Null(DataPowerApiException.Find(agg));
        }
    }
}
