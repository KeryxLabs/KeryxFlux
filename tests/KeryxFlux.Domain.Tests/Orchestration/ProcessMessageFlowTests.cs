using KeryxFlux.Application.Commands;
using KeryxFlux.Application.Handlers;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Ports;
using KeryxFlux.Domain.Tests.TestPlugins;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;
using DomainSender = KeryxFlux.Domain.Ports.ISender;

namespace KeryxFlux.Domain.Tests.Orchestration;

/// <summary>
/// Tests for ProcessMessageCommandHandler orchestration flow.
/// Verifies that messages flow through: Receiver ? Plugin ? Handler ? Senders
/// </summary>
public class ProcessMessageFlowTests
{
    private readonly Mock<IDocketManager> _mockDocketManager;
    private readonly Mock<IPluginManager> _mockPluginManager;
    private readonly Mock<ILogger<ProcessMessageCommandHandler>> _mockLogger;
    private readonly List<Mock<DomainSender>> _mockSenders;
    private readonly ProcessMessageCommandHandler _handler;

    public ProcessMessageFlowTests()
    {
        _mockDocketManager = new Mock<IDocketManager>();
        _mockPluginManager = new Mock<IPluginManager>();
        _mockLogger = new Mock<ILogger<ProcessMessageCommandHandler>>();
        _mockSenders = new List<Mock<DomainSender>>();

        // Create mock senders for each protocol
        var httpSender = CreateMockSender("http");
        var rabbitMqSender = CreateMockSender("rabbitmq");
        var kafkaSender = CreateMockSender("kafka");
        var grpcSender = CreateMockSender("grpc");

        _mockSenders.AddRange(new[] { httpSender, rabbitMqSender, kafkaSender, grpcSender });

        // Create handler with mocked dependencies
        var senders = _mockSenders.Select(m => m.Object);
        _handler = new ProcessMessageCommandHandler(
            _mockDocketManager.Object,
            _mockPluginManager.Object,
            _mockLogger.Object,
            senders);
    }

    [Fact]
    public async Task ProcessMessage_WithHttpDestination_ShouldForwardToHttpSender()
    {
        // Arrange
        var docket = CreateTestDocket("http-test", "http");
        var plugin = new EchoReceiverPlugin();
        
        SetupMocks(docket, plugin);

        var command = new ProcessMessageCommand
        {
            DocketName = "http-test",
            Message = CreateTestMessage("Hello HTTP")
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.DestinationsForwarded.ShouldBe(1);

        // Verify HTTP sender was called
        var httpSender = _mockSenders.First(s => s.Object.Type == "http");
        httpSender.Verify(
            s => s.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithRabbitMqDestination_ShouldForwardToRabbitMqSender()
    {
        // Arrange
        var docket = CreateTestDocket("rabbitmq-test", "rabbitmq");
        var plugin = new EchoReceiverPlugin();
        
        SetupMocks(docket, plugin);

        var command = new ProcessMessageCommand
        {
            DocketName = "rabbitmq-test",
            Message = CreateTestMessage("Hello RabbitMQ")
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.DestinationsForwarded.ShouldBe(1);

        // Verify RabbitMQ sender was called with correct headers
        var rabbitMqSender = _mockSenders.First(s => s.Object.Type == "rabbitmq");
        rabbitMqSender.Verify(
            s => s.SendAsync(
                It.Is<OutboundMessage>(m => 
                    m.Headers.ContainsKey("connection_string") &&
                    m.Headers.ContainsKey("exchange_name")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithKafkaDestination_ShouldForwardToKafkaProducer()
    {
        // Arrange
        var docket = CreateTestDocket("kafka-test", "kafka");
        var plugin = new EchoReceiverPlugin();
        
        SetupMocks(docket, plugin);

        var command = new ProcessMessageCommand
        {
            DocketName = "kafka-test",
            Message = CreateTestMessage("Hello Kafka")
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.DestinationsForwarded.ShouldBe(1);

        // Verify Kafka sender was called with correct headers
        var kafkaSender = _mockSenders.First(s => s.Object.Type == "kafka");
        kafkaSender.Verify(
            s => s.SendAsync(
                It.Is<OutboundMessage>(m => 
                    m.Headers.ContainsKey("bootstrap_servers") &&
                    m.Headers.ContainsKey("topic")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithGrpcDestination_ShouldForwardToGrpcSender()
    {
        // Arrange
        var docket = CreateTestDocket("grpc-test", "grpc");
        var plugin = new EchoReceiverPlugin();
        
        SetupMocks(docket, plugin);

        var command = new ProcessMessageCommand
        {
            DocketName = "grpc-test",
            Message = CreateTestMessage("Hello gRPC")
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.DestinationsForwarded.ShouldBe(1);

        // Verify gRPC sender was called with correct headers
        var grpcSender = _mockSenders.First(s => s.Object.Type == "grpc");
        grpcSender.Verify(
            s => s.SendAsync(
                It.Is<OutboundMessage>(m => 
                    m.Headers.ContainsKey("grpc_endpoint") &&
                    m.Headers.ContainsKey("service_name")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessage_WithMultipleDestinations_ShouldForwardToAll()
    {
        // Arrange
        var docket = CreateTestDocketWithMultipleDestinations();
        var plugin = new AppendTextReceiverPlugin();
        
        SetupMocks(docket, plugin);

        var command = new ProcessMessageCommand
        {
            DocketName = "multi-dest-test",
            Message = CreateTestMessage("Hello All")
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.DestinationsForwarded.ShouldBe(4); // HTTP, RabbitMQ, Kafka, gRPC

        // Verify all senders were called
        foreach (var mockSender in _mockSenders)
        {
            mockSender.Verify(
                s => s.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task ProcessMessage_WithFailingPlugin_ShouldReturnFailure()
    {
        // Arrange
        var docket = CreateTestDocket("failing-test", "http");
        var plugin = new FailingReceiverPlugin();
        
        SetupMocks(docket, plugin);

        var command = new ProcessMessageCommand
        {
            DocketName = "failing-test",
            Message = CreateTestMessage("This will fail")
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Transformation failed");

        // Verify no senders were called
        foreach (var mockSender in _mockSenders)
        {
            mockSender.Verify(
                s => s.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }

    [Fact]
    public async Task ProcessMessage_WithMissingDocket_ShouldReturnFailure()
    {
        // Arrange
        _mockDocketManager.Setup(m => m.GetDocketByName(It.IsAny<string>()))
            .Returns((Docket?)null);

        var command = new ProcessMessageCommand
        {
            DocketName = "non-existent",
            Message = CreateTestMessage("Test")
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("not found");
    }

    [Fact]
    public async Task ProcessMessage_WithTransformedPayload_ShouldForwardTransformedData()
    {
        // Arrange
        var docket = CreateTestDocket("transform-test", "http");
        var plugin = new AppendTextReceiverPlugin();
        
        SetupMocks(docket, plugin);

        var originalMessage = "Original";
        var command = new ProcessMessageCommand
        {
            DocketName = "transform-test",
            Message = CreateTestMessage(originalMessage)
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // Verify transformed payload was sent (not original)
        var httpSender = _mockSenders.First(s => s.Object.Type == "http");
        httpSender.Verify(
            s => s.SendAsync(
                It.Is<OutboundMessage>(m => 
                    System.Text.Encoding.UTF8.GetString(m.Payload).Contains("[PROCESSED]")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Helper Methods

    private Mock<DomainSender> CreateMockSender(string type)
    {
        var mock = new Mock<DomainSender>();
        mock.Setup(s => s.Type).Returns(type);
        mock.Setup(s => s.SendAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendResult.Success());
        return mock;
    }

    private void SetupMocks(Docket docket, IReceiverPlugin plugin)
    {
        _mockDocketManager.Setup(m => m.GetDocketByName(docket.Name))
            .Returns(docket);

        _mockPluginManager.Setup(m => m.LoadPlugin(It.IsAny<string>()))
            .Returns(Result<IKeryxFluxPlugin>.Success((IKeryxFluxPlugin)plugin));
    }

    private ReceivedMessage CreateTestMessage(string content)
    {
        return new ReceivedMessage
        {
            Payload = System.Text.Encoding.UTF8.GetBytes(content),
            ContentType = "text/plain",
            CorrelationId = Guid.NewGuid().ToString(),
            SourceEndpoint = "/test",
            Metadata = new Dictionary<string, string>
            {
                { "test", "true" }
            }
        };
    }

    private Docket CreateTestDocket(string name, string destinationType)
    {
        var destination = destinationType switch
        {
            "http" => new DestinationConfiguration
            {
                Name = "test-http",
                Type = "http",
                Url = "http://localhost:8080/webhook"
            },
            "rabbitmq" => new DestinationConfiguration
            {
                Name = "test-rabbitmq",
                Type = "rabbitmq",
                ConnectionString = "amqp://localhost",
                ExchangeName = "test-exchange",
                RoutingKey = "test.key"
            },
            "kafka" => new DestinationConfiguration
            {
                Name = "test-kafka",
                Type = "kafka",
                BootstrapServers = "localhost:9092",
                Topic = "test-topic"
            },
            "grpc" => new DestinationConfiguration
            {
                Name = "test-grpc",
                Type = "grpc",
                GrpcEndpoint = "localhost:5001",
                ServiceName = "TestService"
            },
            _ => throw new ArgumentException($"Unknown destination type: {destinationType}")
        };

        return new Docket
        {
            Name = name,
            Version = "1.0.0",
            Type = DocketType.Receiver,
            PluginLocation = "./test-plugin.dll",
            Receiver = new ReceiverConfiguration
            {
                Type = "http",
                Endpoint = "/test"
            },
            Forwarding = new ForwardingConfiguration
            {
                Destinations = new List<DestinationConfiguration> { destination }
            }
        };
    }

    private Docket CreateTestDocketWithMultipleDestinations()
    {
        return new Docket
        {
            Name = "multi-dest-test",
            Version = "1.0.0",
            Type = DocketType.Receiver,
            PluginLocation = "./test-plugin.dll",
            Receiver = new ReceiverConfiguration
            {
                Type = "http",
                Endpoint = "/test"
            },
            Forwarding = new ForwardingConfiguration
            {
                Destinations = new List<DestinationConfiguration>
                {
                    new() { Name = "http-dest", Type = "http", Url = "http://localhost/test" },
                    new() { Name = "rabbitmq-dest", Type = "rabbitmq", ConnectionString = "amqp://localhost", ExchangeName = "test" },
                    new() { Name = "kafka-dest", Type = "kafka", BootstrapServers = "localhost:9092", Topic = "test" },
                    new() { Name = "grpc-dest", Type = "grpc", GrpcEndpoint = "localhost:5001", ServiceName = "Test" }
                }
            }
        };
    }
}
