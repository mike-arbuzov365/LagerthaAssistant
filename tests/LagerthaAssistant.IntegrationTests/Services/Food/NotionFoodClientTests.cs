namespace LagerthaAssistant.IntegrationTests.Services.Food;

using System.Net;
using System.Net.Http;
using System.Text;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Services.Food;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class NotionFoodClientTests
{
    [Fact]
    public async Task GetInventoryAsync_ShouldParseSnakeCaseEnvelope_AndPageIcons()
    {
        var handler = new QueueHttpMessageHandler(
            """
            {
              "object":"list",
              "results":[
                {
                  "id":"page-1",
                  "last_edited_time":"2026-03-24T10:00:00.000Z",
                  "icon":{"type":"emoji","emoji":"🍺"},
                  "properties":{
                    "Item Name":{"type":"title","title":[{"plain_text":"Beer"}]}
                  }
                }
              ],
              "next_cursor":"cursor-2",
              "has_more":true
            }
            """,
            """
            {
              "object":"list",
              "results":[
                {
                  "id":"page-2",
                  "last_edited_time":"2026-03-24T10:05:00.000Z",
                  "icon":null,
                  "properties":{
                    "Item Name":{"type":"title","title":[{"plain_text":"Juice"}]}
                  }
                }
              ],
              "next_cursor":null,
              "has_more":false
            }
            """);

        using var httpClient = new HttpClient(handler);
        var options = new NotionFoodOptions
        {
            ApiKey = "test-api-key",
            InventoryDatabaseId = "inv-db",
            MealPlansDatabaseId = "meal-db",
            GroceryListDatabaseId = "grocery-db",
            ApiBaseUrl = "https://api.notion.test/v1"
        };

        var sut = new NotionFoodClient(httpClient, options, NullLogger<NotionFoodClient>.Instance);

        var pages = await sut.GetInventoryAsync();

        Assert.Equal(2, pages.Count);
        Assert.Equal("page-1", pages[0].Id);
        Assert.Equal("2026-03-24T10:00:00.000Z", pages[0].LastEditedTime);
        Assert.Equal("🍺", pages[0].IconEmoji);
        Assert.Equal("page-2", pages[1].Id);
        Assert.Null(pages[1].IconEmoji);
        Assert.Equal(2, handler.RequestsCount);
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public QueueHttpMessageHandler(params string[] responses)
        {
            _responses = new Queue<string>(responses);
        }

        public int RequestsCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestsCount++;
            var body = _responses.Count > 0 ? _responses.Dequeue() : """{"results":[],"next_cursor":null,"has_more":false}""";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}

