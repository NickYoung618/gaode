using Gaode.Application.Ports;

namespace Gaode.Application.Workflow;

public sealed record FaceResultKey(string ObjectId, int LocalFace, int HeightRound)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StageId { get; init; }
}
public sealed record FaceImageResult(string Camera, MediaRef Media,
    string Disposition, string WorkerEvidence);
public sealed record FaceFusionInputs(FaceResultKey Key, FaceImageResult First,
    FaceImageResult Second);

/// <summary>Keeps committed single-image inputs until the matching AB or CD face is complete.</summary>
public sealed class FaceResultAggregator
{
    private readonly Dictionary<FaceResultKey, Dictionary<string, FaceImageResult>> inputs = [];

    public FaceFusionInputs? Add(FaceResultKey key, FaceImageResult image)
    {
        if (string.IsNullOrWhiteSpace(key.ObjectId) || key.LocalFace < 1 || key.HeightRound < 1 ||
            image.Camera is not ("A" or "B" or "C" or "D") ||
            image.Media.StorageState != "FileCompleted" ||
            image.Disposition is not ("OK" or "NG" or "Pending") ||
            string.IsNullOrWhiteSpace(image.WorkerEvidence))
            throw new InvalidDataException("FaceInputInvalid");
        if (!inputs.TryGetValue(key, out var pair)) inputs[key] = pair = new(StringComparer.Ordinal);
        var first = image.Camera is "A" or "B" ? "A" : "C";
        var second = first == "A" ? "B" : "D";
        if (pair.Keys.Any(camera => camera != first && camera != second))
            throw new InvalidDataException("MixedFaceCameraPair");
        if (!pair.TryAdd(image.Camera, image)) throw new InvalidDataException("DuplicateFaceCamera");
        return pair.TryGetValue(first, out var a) && pair.TryGetValue(second, out var b)
            ? new(key, a, b) : null;
    }

    public IReadOnlyList<FaceResultKey> IncompleteKeys => inputs
        .Where(pair => pair.Value.Count != 2).Select(pair => pair.Key).ToArray();
}
