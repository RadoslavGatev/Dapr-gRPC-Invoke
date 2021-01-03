using Common;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GrpcServiceExample
{
    public class DataService : AppCallback.AppCallbackBase
    {
        private readonly ILogger<DataService> _logger;
        private readonly DaprClient _daprClient;
        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public DataService(DaprClient daprClient, ILogger<DataService> logger)
        {
            _daprClient = daprClient;
            _logger = logger;
        }


        public override async Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
        {
            var response = new InvokeResponse();
            switch (request.Method)
            {
                case "GetData":
                    var input = JsonSerializer.Deserialize<GetDataInput>(request.Data.Value.ToByteArray(), this.jsonOptions);
                    var output = await GetDataFromStateStore(input, context);
                    response.Data = new Any
                    {
                        Value = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes<DummyData>(output, this.jsonOptions)),
                    };
                    break;
                default:
                    break;
            }
            return response;
        }

        public override Task<ListTopicSubscriptionsResponse> ListTopicSubscriptions(Empty request, ServerCallContext context)
        {
            var result = new ListTopicSubscriptionsResponse();
            result.Subscriptions.Add(new TopicSubscription
            {
                PubsubName = Constants.PubSubName,
                Topic = Constants.TopicName
            });
            return Task.FromResult(result);
        }

        public override Task<ListInputBindingsResponse> ListInputBindings(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new ListInputBindingsResponse());
        }

        public override async Task<TopicEventResponse> OnTopicEvent(TopicEventRequest request, ServerCallContext context)
        {
            if (request.PubsubName == Constants.PubSubName)
            {
                var transaction = JsonSerializer.Deserialize<DummyData>(request.Data.ToStringUtf8(), this.jsonOptions);
                if (request.Topic == Constants.TopicName)
                {
                    await ReceiveMessageFromTopic(transaction, context);
                }
            }

            return await Task.FromResult(new TopicEventResponse()
            {
                Status = TopicEventResponse.Types.TopicEventResponseStatus.Success
            });
        }

        public async Task<DummyData> GetDataFromStateStore(GetDataInput input, ServerCallContext context)
        {
            var state = await _daprClient.GetStateEntryAsync<DummyData>(Constants.StateStoreName, input.Id);

            return state.Value;
        }

        public async Task ReceiveMessageFromTopic(DummyData data, ServerCallContext context)
        {
            await _daprClient.SaveStateAsync<DummyData>(Constants.StateStoreName, data.Id, data);
        }
    }
}
