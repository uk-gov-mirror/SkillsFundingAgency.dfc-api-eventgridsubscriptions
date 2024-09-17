using DFC.EventGridSubscriptions.Data;
using DFC.EventGridSubscriptions.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.EventGrid;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace DFC.EventGridSubscriptions.ApiFunction.Function
{
    public class DeadLetterHttpTrigger
    {
        private readonly IOptionsMonitor<EventGridSubscriptionClientOptions> options;
        private readonly ISubscriptionService subscriptionService;
        private readonly ILogger log;

        public DeadLetterHttpTrigger(IOptionsMonitor<EventGridSubscriptionClientOptions> options, ISubscriptionService subscriptionService, ILogger<DeadLetterHttpTrigger> log)
        {
            this.options = options;
            this.subscriptionService = subscriptionService;
            this.log = log;
        }

        [Function("ProcessDeadLetter")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "DeadLetter/api/updates")] HttpRequest req)
        {
            req.EnableBuffering();
            Initialise(req);

            log.LogInformation($"C# HTTP trigger function begun");
            string response = string.Empty;

            if (req.Body == null)
            {
                return new NoContentResult();
            }

            var reader = new StreamReader(req.Body);
            string requestContent = await reader.ReadToEndAsync().ConfigureAwait(false);
            log.LogInformation("Received events: {RequestContent}", requestContent);

            EventGridSubscriber eventGridSubscriber = new EventGridSubscriber();

            EventGridEvent[] eventGridEvents = eventGridSubscriber.DeserializeEventGridEvents(requestContent);

            foreach (EventGridEvent eventGridEvent in eventGridEvents)
            {
                if (eventGridEvent.Data.GetType() == typeof(SubscriptionValidationEventData))
                {
                    if (eventGridEvent.Data is not SubscriptionValidationEventData eventData)
                    {
                        throw new InvalidDataException($"{nameof(SubscriptionValidationEventData)} in EventGridEvent {eventGridEvent.Id} is null");
                    }

                    log.LogInformation("Got SubscriptionValidation event data, validation code: {ValidationCode}, topic: {Topic}", eventData.ValidationCode, eventGridEvent.Topic);

                    var responseData = new SubscriptionValidationResponse()
                    {
                        ValidationResponse = eventData.ValidationCode,
                    };

                    return new JsonResult(responseData, new JsonSerializerOptions())
                    {
                        StatusCode = (int)HttpStatusCode.OK,
                    };
                }
                else if (eventGridEvent.Data.GetType() == typeof(StorageBlobCreatedEventData))
                {
                    if (options.CurrentValue.DeadLetterBlobContainerName == null)
                    {
                        throw new ArgumentException(nameof(options.CurrentValue.DeadLetterBlobContainerName));
                    }

                    if (eventGridEvent.Data is not StorageBlobCreatedEventData eventData)
                    {
                        throw new InvalidDataException($"{nameof(StorageBlobCreatedEventData)} in EventGridEvent {eventGridEvent.Id} is null");
                    }

                    if (eventData.Url.Contains(options.CurrentValue.DeadLetterBlobContainerName, StringComparison.OrdinalIgnoreCase))
                    {
                        log.LogInformation("Processing Dead Lettered Event");

                        var blobString = $"{options.CurrentValue.DeadLetterBlobContainerName}/{options.CurrentValue.TopicName}/";

                        int startIndex = eventData.Url.IndexOf(blobString, StringComparison.OrdinalIgnoreCase) + blobString.Length;
                        int endIndex = eventData.Url.IndexOf("/", startIndex, StringComparison.OrdinalIgnoreCase);
                        var subscriberName = eventData.Url[startIndex..endIndex];

                        log.LogError("Dead Lettered Event, Blob URL: {Url}, SubscriberName {SubscriberName}", eventData.Url, subscriberName);

                        if (options.CurrentValue.DeadLetterStaleSubscriptionRemovalEnabled)
                        {
                            var result = await subscriptionService.StaleSubscription(subscriberName).ConfigureAwait(false);
                            return HandleHttpStatusCode(result);
                        }

                        return new OkResult();
                    }
                }
            }

            return new JsonResult(response, new JsonSerializerOptions())
            {
                StatusCode = (int)HttpStatusCode.OK,
            };
        }

        private static void Initialise(HttpRequest req)
        {
            Activity.Current ??= new Activity($"{nameof(DeadLetterHttpTrigger)}").Start();

            if (req == null)
            {
                throw new ArgumentNullException(nameof(req));
            }
        }

        private static IActionResult HandleHttpStatusCode(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.OK => // 200
                    new OkResult(),
                HttpStatusCode.Created => // 201
                    new CreatedResult(),
                HttpStatusCode.BadRequest => // 400
                    new BadRequestResult(),
                HttpStatusCode.Unauthorized => // 401
                    new UnauthorizedResult(),
                HttpStatusCode.Forbidden => // 403
                    new ForbidResult(),
                HttpStatusCode.NotFound => // 404
                    new NotFoundResult(),
                HttpStatusCode.Conflict => // 409
                    new ConflictResult(),
                HttpStatusCode.InternalServerError => // 500
                    new StatusCodeResult(500),
                _ => new StatusCodeResult((int)statusCode)
            };
        }
    }
}