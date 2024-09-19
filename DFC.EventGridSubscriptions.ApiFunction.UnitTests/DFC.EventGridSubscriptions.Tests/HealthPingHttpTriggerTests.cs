using DFC.EventGridSubscriptions.ApiFunction.Function;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DFC.EventGridSubscriptions.ApiFunction.UnitTests.DFC.EventGridSubscriptions.Tests
{
    public class HealthPingHttpTriggerTests
    {
        private readonly ILogger<HealthPingHttpTrigger> logger = A.Fake<ILogger<HealthPingHttpTrigger>>();

        [Fact]
        public void HealthPingHttpTriggerReturnsOk()
        {
            // Arrange
            var function = new HealthPingHttpTrigger(logger);
            var httpContext = new DefaultHttpContext();

            // Act
            var result = function.Run(httpContext.Request);

            // Assert
            Assert.IsType<OkResult>(result);
        }
    }
}