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
        private readonly ILogger logger = A.Fake<ILogger>();

        [Fact]
        public void HealthPingHttpTriggerTestsReturnsOk()
        {
            // Arrange

            // Act
            var result = HealthPingHttpTrigger.Run(new DefaultHttpContext().Request, logger);

            // Assert
            Assert.IsType<OkResult>(result);
        }
    }
}
