using Nats.Akka.Core.Extension;
using Nats.Akka.Core.Factory;
using Nats.Akka.Core.Internal;
using System.Text.Json.Serialization;

const string Subject = "radar.car.input";

var natsUrl = args.Length > 0 ? args[0] : "nats://192.168.137.1:4222";
var path = args.Length > 1 ? args[1] : string.Empty;

var request = new RadarRequestDto
{
    RequestId = Environment.TickCount,
    Command = RadarNatsCommand.GetOutput,
    LidarId = 1,
    AuxLidarId = 2,
    AlgorithmMode = int.MinValue,
    MaxPoints = RadarDefaults.MaxSgmtPoints,
    Path = path,
    Pose = new Pose(),
    Params = new Params(),
    AuxParams = new Params(),
    Points = new List<NeuvUnit>(),
    AuxPoints = new List<NeuvUnit>()
};

var factory = new NatsClientFactory();
using var connection = factory.CreateClient(natsUrl, $"radar-publisher-{Guid.NewGuid():N}", reconnectOnConnect: true);

var msg = connection.Request(Subject, NatsMessageCodec.Serialize(request));
var d = msg.Data;
connection.Flush();

Console.WriteLine($"Published RadarRequestDto to '{Subject}' via {natsUrl}.");
Console.WriteLine($"RequestId: {request.RequestId}");

internal sealed class RadarRequestDto
{
    [JsonPropertyName("requestId")]
    public int RequestId { get; set; }

    [JsonPropertyName("command")]
    public RadarNatsCommand Command { get; set; } = RadarNatsCommand.GetOutput;

    [JsonPropertyName("lidarId")]
    public int LidarId { get; set; } = 1;

    [JsonPropertyName("auxLidarId")]
    public int AuxLidarId { get; set; } = 2;

    [JsonPropertyName("algorithmMode")]
    public int AlgorithmMode { get; set; } = int.MinValue;

    [JsonPropertyName("maxPoints")]
    public int MaxPoints { get; set; } = RadarDefaults.MaxSgmtPoints;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("pose")]
    public Pose Pose { get; set; } = new();

    [JsonPropertyName("params")]
    public Params Params { get; set; } = new();

    [JsonPropertyName("auxParams")]
    public Params AuxParams { get; set; } = new();

    [JsonPropertyName("points")]
    public List<NeuvUnit> Points { get; set; } = new();

    [JsonPropertyName("auxPoints")]
    public List<NeuvUnit> AuxPoints { get; set; } = new();
}

internal enum RadarNatsCommand
{
    GetOutput = 0
}

internal sealed class Pose
{
}

internal sealed class Params
{
}

internal sealed class NeuvUnit
{
}

internal static class RadarDefaults
{
    public const int MaxSgmtPoints = 200000;
}
