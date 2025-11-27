using System.Text;
using Broker;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniBroker.Abstraction;
using MiniBroker.Grpc.Client.Configuration;
using MiniBroker.Grpc.Client.Contracts;
using MiniBroker.Grpc.Client.Services;
using Moq;

namespace MiniBroker.Grpc.Client.Tests;

[TestClass]
public class MessageDispatcherServiceTests
{
    private Mock<ILogger<MessageDispatcherService>> _loggerMock = null!;
    private Mock<IServiceProvider> _providerMock = null!;
    private Mock<ISerializerProvider> _serializerMock = null!;
    private IOptions<MiniBrokerOptions> _options = null!;
    private MessageDispatcherService _dispatcher = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<MessageDispatcherService>>();
        _providerMock = new Mock<IServiceProvider>();
        _serializerMock = new Mock<ISerializerProvider>();
        _options = Options.Create(new MiniBrokerOptions { EnableTypeSearchByName = false });

        _dispatcher = new MessageDispatcherService(
            _loggerMock.Object,
            _providerMock.Object,
            _serializerMock.Object,
            _options
        );
    }

    public class TestMessage
    {
        public string Text { get; set; } = string.Empty;
    }

    public class TestMessageHandler : IHandleMessage<TestMessage>
    {
        public TestMessage? LastMessage;
        public Context? LastContext;

        public Task OnMessage(TestMessage message, Context context)
        {
            LastMessage = message;
            LastContext = context;
            return Task.CompletedTask;
        }

        public Task OnMessage(object message, Context context)
        {
            return OnMessage((TestMessage)message, context);
        }
    }

    [TestMethod]
    public void ProcessMessage_Should_Log_When_Type_Is_Missing()
    {
        var msg = new MessageStreamProto
        {
            Type = "",
            Payload = ByteString.CopyFromUtf8("{}"),
            Context = new ContextProto
            {
                Destinataire = "test-destination",
                Source = "test-source"
            }
        };

        _dispatcher.ProcessMessage(msg);

        _loggerMock.VerifyLog(LogLevel.Error, "missing Type");
    }

    [TestMethod]
    public void ProcessMessage_Should_Log_When_Payload_Is_Empty()
    {
        var msg = new MessageStreamProto
        {
            Type = typeof(TestMessage).FullName!,
            Payload = ByteString.Empty,
            Context = new ContextProto
            {
                Destinataire = "test-destination",
                Source = "test-source"
            }
        };

        _dispatcher.ProcessMessage(msg);

        _loggerMock.VerifyLog(LogLevel.Error, "missing Payload");
    }

    [TestMethod]
    public void ProcessMessage_Should_Log_When_Type_Not_Resolved()
    {
        var msg = new MessageStreamProto
        {
            Type = "Non.Existent.Type",
            Payload = ByteString.CopyFromUtf8("{}"),
            Context = new ContextProto
            {
                Destinataire = "test-destination",
                Source = "test-source"
            }
        };

        _dispatcher.ProcessMessage(msg);

        _loggerMock.VerifyLog(LogLevel.Error, "Unknown type");
    }
    
    [TestMethod]
    public void ProcessMessage_Should_Log_When_Context_Is_Empty()
    {
        var msg = new MessageStreamProto
        {
            Type = typeof(TestMessage).FullName!,
            Payload = ByteString.CopyFromUtf8("{}"),
            Context = null
        };

        _dispatcher.ProcessMessage(msg);

        _loggerMock.VerifyLog(LogLevel.Error, "missing Context");
    }

    [TestMethod]
    public void ProcessMessage_Should_Log_When_Deserialization_Fails()
    {
        var msg = new MessageStreamProto
        {
            Type = typeof(TestMessage).FullName!,
            Payload = ByteString.CopyFromUtf8("{ bad json }"),
            Context = new ContextProto
            {
                Destinataire = "test-destination",
                Source = "test-source"
            }
        };

        _serializerMock
            .Setup(s => s.TryDeserialize(It.IsAny<byte[]>(), typeof(TestMessage), out It.Ref<object?>.IsAny))
            .Returns(false);

        _dispatcher.ProcessMessage(msg);

        _loggerMock.VerifyLog(LogLevel.Error, "Failed to deserialize");
    }

    [TestMethod]
    public void ProcessMessage_Should_Log_When_Handler_Not_Found()
    {
        var json = "{\"Text\": \"Hello\"}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var msg = new MessageStreamProto
        {
            Type = typeof(TestMessage).FullName!,
            Payload = ByteString.CopyFrom(bytes),
            Context = new ContextProto
            {
                Destinataire = "test-destination",
                Source = "test-source"
            }
        };

        object? result = new TestMessage { Text = "Hello" };
        _serializerMock
            .Setup(s => s.TryDeserialize(It.IsAny<byte[]>(), typeof(TestMessage), out result!))
            .Returns(true);

        _providerMock
            .Setup(p => p.GetService(typeof(IHandleMessage<TestMessage>)))
            .Returns(null!);

        _dispatcher.ProcessMessage(msg);

        _loggerMock.VerifyLog(LogLevel.Error, "No listener found");
    }

    [TestMethod]
    public void ProcessMessage_Should_Invoke_Handler_On_Success()
    {
        var json = "{\"Text\": \"Hello\"}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var msg = new MessageStreamProto
        {
            Type = typeof(TestMessage).FullName!,
            Payload = ByteString.CopyFrom(bytes),
            Context = new ContextProto()
            {
                Destinataire = "test-destination",
                Source = "test-source"
            }
        };

        var handler = new TestMessageHandler();
        object? instance = new TestMessage { Text = "Hello" };

        _serializerMock
            .Setup(s => s.TryDeserialize(It.IsAny<byte[]>(), typeof(TestMessage), out instance!))
            .Returns(true);

        _providerMock
            .Setup(p => p.GetService(typeof(IHandleMessage<TestMessage>)))
            .Returns(handler);

        _dispatcher.ProcessMessage(msg);

        Assert.AreEqual("Hello", handler.LastMessage?.Text);
    }
}