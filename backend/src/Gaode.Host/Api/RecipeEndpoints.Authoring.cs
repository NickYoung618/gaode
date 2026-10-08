using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;

namespace Gaode.Host.Api;

public static partial class RecipeEndpoints
{
    public sealed record EditorDraftRequest([property:JsonRequired] string Model, [property:JsonRequired] string ScenarioId, [property:JsonRequired] string UnitKind,
        [property:JsonRequired] RecipeInspectionKind InspectionKind, string? SourceRecipeId = null);
    public sealed record EditorLayoutRequest([property:JsonRequired] JsonElement Definition, string? SourceRecipeId = null,
        string? SourceVersion = null, RecipeTrayLayout? TrayLayout = null, bool? ExtraE = null,
        string? CameraPair = null, string? Material = null, int? LocalFace = null,
        string? StageId = null, int? Faces = null, int? Members = null);
    private static void MapAuthoring(RouteGroupBuilder group)
    {
        group.MapPost("/editor-layout", (EditorLayoutRequest request, IRecipeCatalog catalog, HttpContext http) =>
        {
            try
            {
                var body = JsonNode.Parse(request.Definition.GetRawText())!.AsObject();
                if (request.TrayLayout is not null)
                    MapEditorLayout(body, request.TrayLayout, ResolveEditorSource(body, request, catalog));
                if (request.ExtraE is bool enabled)
                {
                    var e = body["eCode"]!;
                    if (!enabled && e["extraPose"] is not null) { e["enabled"] = false; e["extraPose"] = null; e["scanPointRef"] = null; }
                    else if (!enabled) { /* Ordinary configured E collection is unchanged. */ }
                    else if (e["extraPose"] is not null) e["enabled"] = true;
                    else
                    {
                        var id = body["recipeId"]!.GetValue<string>();
                        var configured = catalog.GetSnapshot().Definitions.Where(d => d.RecipeId == id && d.ECode.ExtraPose is not null).ToArray();
                        if (configured.Length != 1) return BadAuthoringRequest(http, "当前对象尚无额外E扫码的后台配置。");
                        e.ReplaceWith(JsonNode.Parse(RecipeDefinitionSerialization.Serialize(configured[0]))!["eCode"]!.DeepClone());
                    }
                }
                if (request.CameraPair is string pair)
                {
                    if (pair is not ("AB" or "CD")) return BadAuthoringRequest(http, "请选择AB或CD相机组。");
                    var material = request.Material; var face = request.LocalFace;
                    var configuredSource = ResolveEditorSource(body,request,catalog);
                    var targets = body["stages"]!.AsArray()
                        .Where(s => RecipeStageIdentity.Create(s!["number"]!.GetValue<int>()) == request.StageId)
                        .SelectMany(s => s!["targets"]!.AsArray())
                        .Where(t => t!["material"]!.GetValue<string>() == material && t["localFace"]!.GetValue<int>() == face).ToArray();
                    if (targets.Length != 1) return BadAuthoringRequest(http, "当前检测对象未配置。");
                    targets[0]!["cameraPair"] = pair;
                    foreach (var slot in body["executionPositions"]!.AsObject())
                    {
                        var members = slot.Value!["members"]!.AsObject();
                        var input = members.TryGetPropertyValue(material!, out var member) ? member! : slot.Value["physicalEntity"]!;
                        var coordinates = input["coordinates"]!.AsArray();
                        var basis = coordinates.FirstOrDefault(c => c!["stageId"]!.GetValue<string>() == request.StageId && c["localFace"]!.GetValue<int>() == face);
                        if (basis is null) return BadAuthoringRequest(http, "当前拍照点后台配置缺失。");
                        foreach (var camera in pair)
                        {
                            if (coordinates.Any(c => c!["stageId"]!.GetValue<string>() == request.StageId && c["localFace"]!.GetValue<int>() == face && c["camera"]!.GetValue<string>() == camera.ToString())) continue;
                            var supplied = configuredSource.ExecutionPositions.Values.Select(v=>v.ForDetection(material)).SelectMany(v=>v.Coordinates)
                                .Where(c=>c.StageId==request.StageId&&c.LocalFace==face&&c.Camera==camera.ToString()).ToArray();
                            if(supplied.Length==0)return BadAuthoringRequest(http,"本次相机缺少真实后台采集及点位配置。");
                            var configured = supplied[0];
                            var point = JsonSerializer.SerializeToNode(configured,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                            point["slotId"]=slot.Key; point["physicalSlotIndex"]=basis["physicalSlotIndex"]?.DeepClone();
                            var profileReference = configured.CaptureProfile ?? throw new JsonException("后台采集配置缺失。");
                            var profile = configuredSource.CaptureProfiles[profileReference];
                            if (!body["captureProfiles"]!.AsObject().ContainsKey(profileReference))
                                body["captureProfiles"]![profileReference]=JsonSerializer.SerializeToNode(profile,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                            point["pointRef"] = "photo-" + Guid.NewGuid().ToString("N"); point["point"]!["id"] = point["pointRef"]!.DeepClone();
                            point["point"]!["x"] = null; point["point"]!["y"] = null; point["fixed"]!["z"] = null;
                            BlankEditorCapture(body, point, targets[0]!["captureProfile"]!.GetValue<string>()); coordinates.Add(point);
                        }
                        foreach (var coordinate in coordinates.Where(c => c!["stageId"]!.GetValue<string>() == request.StageId && c["localFace"]!.GetValue<int>() == face &&
                            !pair.Contains(c["camera"]!.GetValue<string>(), StringComparison.Ordinal) && IncompleteEditorPoint(c)).ToArray()) coordinates.Remove(coordinate);
                    }
                }
                if (request.Faces is int faces)
                {
                    if (faces < 1) return BadAuthoringRequest(http, "检测面数必须是正整数。");
                    var material = request.Material;
                    var composition = body["composition"]!.AsArray();
                    foreach (var item in composition.Where(c => material is null || c!["material"]!.GetValue<string>() == material))
                    {
                        var configured = item!["localFaces"]!.AsArray().Select(f => f!.GetValue<int>()).ToArray();
                        if (Enumerable.Range(1, faces).Except(configured).Any())
                            return BadAuthoringRequest(http, "所选检测面数尚无对应的后台配置，已有配置仍可编辑。");
                        item["localFaces"] = new JsonArray(configured.Where(f => f <= faces).Select(f => JsonValue.Create(f) as JsonNode).ToArray());
                    }
                    var stages = body["stages"]!.AsArray();
                    foreach (var stage in stages)
                    {
                        var targets = stage!["targets"]!.AsArray();
                        foreach (var target in targets.ToArray())
                            if ((material is null || target!["material"]!.GetValue<string>() == material) && target!["localFace"]!.GetValue<int>() > faces) targets.Remove(target);
                    }
                    foreach (var stage in stages.Where(s => s!["targets"]!.AsArray().Count == 0).ToArray()) stages.Remove(stage);
                    var stageMap = stages.Select((s, index) => new
                        { Old = RecipeStageIdentity.Create(s!["number"]!.GetValue<int>()), New = RecipeStageIdentity.Create(index + 1) }).ToArray();
                    for (var i = 0; i < stages.Count; i++) stages[i]!["number"] = i + 1;
                    foreach (var slot in body["executionPositions"]!.AsObject())
                    foreach (var input in EditorObjects(slot.Value!))
                    {
                        foreach (var coordinate in input["coordinates"]!.AsArray())
                        {
                            var mapped = stageMap.FirstOrDefault(m => m.Old == coordinate!["stageId"]!.GetValue<string>());
                            if (mapped is not null) coordinate!["stageId"] = mapped.New;
                        }
                        if (input["flip"] is JsonNode flip)
                        {
                            var original = flip["stages"]!.AsObject(); var mapped = new JsonObject();
                            foreach (var stage in stageMap)
                                if (original[stage.Old] is JsonNode value) mapped[stage.New] = value.DeepClone();
                            flip["stages"] = mapped;
                        }
                    }
                    foreach (var slot in body["executionPositions"]!.AsObject())
                    foreach (var input in material is null ? EditorObjects(slot.Value!) :
                        new[] { (slot.Value!["members"]!.AsObject().TryGetPropertyValue(material, out var member) ? member! : slot.Value["physicalEntity"]!).AsObject() })
                    {
                        var coordinates = input["coordinates"]!.AsArray();
                        foreach (var point in coordinates.Where(c => c!["localFace"]!.GetValue<int>() > faces).ToArray()) coordinates.Remove(point);
                    }
                }
                if (request.Members is int memberCount)
                {
                    if (body["composition"]!.AsArray().Count != memberCount)
                        return BadAuthoringRequest(http, "成员数量尚无对应后台配置，已有成员可分别编辑。");
                }
                PruneIncompleteEditorInputs(body);
                return Results.Json(new JsonObject { ["definition"] = body });
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            { return BadAuthoringRequest(http, "无法映射当前填写内容，请重新读取配置。"); }
        }).RequireAuthorization(Station01Authorization.RecipeWrite);
        group.MapPost("/editor-draft", (EditorDraftRequest request, IRecipeCatalog catalog, HttpContext http) =>
        {
            try
            {
                var sources = catalog.GetSnapshot().Definitions.Where(d => d.Model == request.Model &&
                    d.ScenarioId == request.ScenarioId && d.UnitKind == request.UnitKind &&
                    EditorKind(d) == request.InspectionKind &&
                    d.Route == EditorRoute(request.UnitKind, request.InspectionKind)).ToArray();
                if (request.SourceRecipeId is not null)
                    sources = sources.Where(d => d.RecipeId == request.SourceRecipeId).ToArray();
                if (sources.Length != 1)
                    return BadAuthoringRequest(http, "该型号、场景和类型的后台配置缺失或不唯一，无法新建。请完成对应配置准备。");
                var source = sources[0];
                if (request.InspectionKind == RecipeInspectionKind.SpecialRotation &&
                    (source.RotationWorkstation is null || source.Stages.Count != 2))
                    return BadAuthoringRequest(http, "特殊类型缺少两组检测和旋转工位的真实后台配置。");
                var draft = source with { RecipeId = "", Version = "", DefinitionDigest = "", CatalogDigest = "",
                    SchemaVersion = RecipeDefinitionSerialization.CurrentSchema, FCode = "", SortingGripperId = null,
                    Composition = source.Composition.Select(m => m with { SortingGripperId = null }).ToArray(),
                    InspectionKind = request.InspectionKind, RotationLoadingGripperId = null,
                    TrayLayout = new(10, 10, []), TraySlotMapping = null, Positions = [],
                    ExecutionPositions = new Dictionary<string, SlotExecutionInputs>(),
                    SortingTargets = new Dictionary<string, HandlingPoint>(), Capacity = 0, NgCapacity = 0, PendingCapacity = 0,
                    PlcRecipeId = null, ReleaseStatus = "draft", Approval = new("", "", "", "", [], "") };
                var body = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(draft))!.AsObject();
                if (request.InspectionKind == RecipeInspectionKind.SpecialRotation)
                {
                    foreach (var stage in body["stages"]!.AsArray()) stage!["angleDeg"] = null;
                    foreach (var point in new[] { body["rotationWorkstation"]!["place"], body["rotationWorkstation"]!["pick"] })
                        BlankHandlingPoint(point!);
                }
                // Public configured profiles remain intact. Only the actual new photo cards start blank.
                foreach (var position in body["positions"]!.AsArray())
                    BlankEditorInputs(body, position!, body["executionPositions"]![position!["slotId"]!.GetValue<string>()]!);
                return Results.Json(new JsonObject { ["definition"] = body,
                    ["authoringContext"] = JsonSerializer.SerializeToNode(new { sourceRecipeId = source.RecipeId,
                        sourceVersion = source.Version, source.Model, source.ScenarioId, source.UnitKind,
                        inspectionKind = request.InspectionKind == RecipeInspectionKind.SpecialRotation ? "specialRotation" : "ordinary" }) });
            }
            catch (JsonException error) { return BadAuthoringRequest(http, error.Message); }
            catch (Exception error) when (IsStorageReadError(error)) { return ReadUnavailable(http, error); }
        }).RequireAuthorization(Station01Authorization.RecipeWrite);
        group.MapGet("/catalog", async (IRecipeCatalog catalog, IAuthorizationService authorization, HttpContext http,
            Gaode.Application.Ports.IPublicConfiguration configuration, Gaode.Host.Composition.Station01RuntimeOptions runtime) =>
        {
            try
            {
                var snapshot = catalog.GetSnapshot();
                var purpose = configuration.LoadPublic(runtime.PublicReference).Value.Purpose;
                var items = snapshot.Definitions.Select(definition =>
                {
                    var admission = RecipeAdmission.Evaluate(definition, definition.Positions.Select(p => p.SlotId), purpose);
                    return new { definition.RecipeId, definition.Version, definition.Model, definition.FCode,
                        definition.ScenarioId, definition.UnitKind, definition.Route, definition.InspectionKind, purpose,
                        snapshot.CatalogDigest, definition.DefinitionDigest,
                        availability = admission.Eligible ? "Available" : "Restricted", restriction = admission.Reason, admission };
                }).ToArray();
                return Results.Ok(new { snapshot.SchemaVersion, snapshot.CatalogDigest, digest = snapshot.CatalogDigest,
                    count = items.Length, items, provider = "Sqlite", authoringAccess = new
                    {
                        canSave = (await authorization.AuthorizeAsync(http.User, Station01Authorization.RecipeWrite)).Succeeded,
                        canValidate = (await authorization.AuthorizeAsync(http.User, Station01Authorization.ConfigValidate)).Succeeded
                    } });
            }
            catch (Exception error) when (IsStorageReadError(error)) { return ReadUnavailable(http, error); }
        }).RequireAuthorization(Station01Authorization.Read);

        group.MapGet("/{recipeId}", (string recipeId, SqliteRecipeStore store, HttpContext http) =>
        {
            try
            {
                var content = store.GetCurrentContent(recipeId);
                if (content is null) return Station01ApiResults.NotFound(http, "RecipeNotFound", "配方不存在。");
                var tag = RecipeReadTag(content.RecipeId, content.Version);
                http.Response.Headers.ETag = tag;
                if (http.Request.Headers.IfNoneMatch.ToString() == tag) return Results.StatusCode(304);
                return Results.Json(new JsonObject
                {
                    ["definition"] = JsonNode.Parse(content.DefinitionJson),
                    ["savedAt"] = JsonSerializer.SerializeToNode(content.SavedUtc), ["requestId"] = content.RequestId,
                    ["stageIdentities"] = JsonSerializer.SerializeToNode(RecipeDefinitionSerialization.Deserialize(content.DefinitionJson)
                        .Stages.Select(stage => new { number = stage.Number, stageId = RecipeStageIdentity.Create(stage.Number) }))
                });
            }
            catch (Exception error) when (IsStorageReadError(error)) { return ReadUnavailable(http, error); }
        }).RequireAuthorization(Station01Authorization.Read);

        group.MapPost("/validate", async (HttpContext http, IRecipeCatalog catalog,
            Gaode.Application.Ports.IPublicConfiguration configuration, Gaode.Host.Composition.Station01RuntimeOptions runtime) =>
        {
            try
            {
                var (_, candidate) = await ReadCandidate(http);
                var current = catalog.GetSnapshot();
                var existing = string.IsNullOrEmpty(candidate.RecipeId) ? null :
                    current.Definitions.SingleOrDefault(d => d.RecipeId == candidate.RecipeId);
                CheckMetadata(candidate, existing);
                var result = RecipeDefinitionValidator.ValidateForSave(candidate, current);
                return Results.Ok(new { result.Valid, result.Issues,
                    admission = result.Valid ? RecipeAdmission.Evaluate(candidate, candidate.Positions.Select(p => p.SlotId), configuration.LoadPublic(runtime.PublicReference).Value.Purpose) : null });
            }
            catch (UnauthorizedAccessException) { return MetadataDenied(http); }
            catch (JsonException error) { return BadAuthoringRequest(http, error.Message); }
            catch (Exception error) when (IsStorageReadError(error)) { return ReadUnavailable(http, error); }
        }).RequireAuthorization(Station01Authorization.ConfigValidate);

        group.MapPost("", (HttpContext http, IRecipeStore store, IRecipeCatalog catalog) => SaveCandidate(http, store, catalog, null))
            .RequireAuthorization(Station01Authorization.RecipeWrite);
        group.MapPut("/{recipeId}", (string recipeId, HttpContext http, IRecipeStore store, IRecipeCatalog catalog) =>
                SaveCandidate(http, store, catalog, recipeId))
            .RequireAuthorization(Station01Authorization.RecipeWrite);
    }

    private static async Task<IResult> SaveCandidate(HttpContext http, IRecipeStore store, IRecipeCatalog catalog, string? recipeId)
    {
        try
        {
            string? expected = null;
            if (recipeId is not null)
            {
                if (string.IsNullOrEmpty(http.Request.Headers.IfMatch))
                    return Station01ApiResults.Error(http, 428, "RecipeReadVersionRequired", "请先读取完整配方，再携读取版本保存。");
                if (!TryReadTag(http.Request.Headers.IfMatch.ToString(), recipeId, out expected))
                    return BadAuthoringRequest(http, "If-Match不能代表该配方的单一读取版本。");
            }
            var (requestId, candidate) = await ReadCandidate(http);
            var snapshot = catalog.GetSnapshot();
            var existing = recipeId is null ? null : snapshot.Definitions.SingleOrDefault(d => d.RecipeId == recipeId);
            // A vanished target still goes through ExpectedVersion and returns conflict, never POST/upsert.
            if (recipeId is null || existing is not null) CheckMetadata(candidate, existing);
            var result = await store.SaveAsync(new(candidate, recipeId, expected, requestId), http.RequestAborted);
            if (result.Status == RecipeSaveStatus.Saved && result.Definition is { } definition)
            {
                http.Response.Headers.ETag = RecipeReadTag(definition.RecipeId, definition.Version);
                if (recipeId is null) http.Response.Headers.Location = "/api/v1/recipes/" + Uri.EscapeDataString(definition.RecipeId);
                // Full saved content is already confirmed. No readback/cache gate after COMMIT.
                // savedAt is obtained by complete GET; no guessed COMMIT timestamp in this receipt.
                return Results.Json(new JsonObject
                {
                    ["result"] = result.Status.ToString(), ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(definition)),
                    ["catalogDigest"] = result.CatalogDigest, ["requestId"] = requestId
                }, statusCode: recipeId is null ? 201 : 200);
            }
            var status = result.Status switch
            {
                RecipeSaveStatus.ValidationFailed => result.Issues?.Any(i => i.Code == "RecipeFCodeOccupied") == true ? 409 : 422,
                RecipeSaveStatus.VersionConflict => 412,
                _ => 503
            };
            return Station01ApiResults.Error(http, status, result.Status.ToString(), result.Status switch
            {
                RecipeSaveStatus.ValidationFailed => "配方检查未通过。",
                RecipeSaveStatus.VersionConflict => "保存版本已变化，请重新读取后编辑。",
                RecipeSaveStatus.CommitUnknown => "提交结果未确认，请读取核对；未自动重发。",
                _ => "配方保存失败，请查看诊断。"
            }, "Recipe", details: new { result = result.Status.ToString(), result.Issues, result.Reason, requestId });
        }
        catch (UnauthorizedAccessException) { return MetadataDenied(http); }
        catch (JsonException error) { return BadAuthoringRequest(http, error.Message); }
        catch (Exception error) when (IsStorageReadError(error)) { return ReadUnavailable(http, error); }
    }

    private static async Task<(string RequestId, RecipeDefinition Definition)> ReadCandidate(HttpContext http)
    {
        using var document = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("requestId", out var request) ||
            request.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(request.GetString()) ||
            !root.TryGetProperty("definition", out var definition)) throw new JsonException("requestId和完整definition必填。");
        return (request.GetString()!, RecipeDefinitionSerialization.Deserialize(definition.GetRawText()));
    }

    private static void CheckMetadata(RecipeDefinition candidate, RecipeDefinition? existing)
    {
        if (existing is null)
        {
            var approval = candidate.Approval;
            if (candidate.PlcRecipeId is not null || candidate.ReleaseStatus is not ("" or "draft") ||
                !string.IsNullOrEmpty(candidate.RecipeId) || !string.IsNullOrEmpty(candidate.Version) || !string.IsNullOrEmpty(candidate.DefinitionDigest) ||
                new[] { approval.Id, approval.Version, approval.Digest, approval.Purpose, approval.EvidenceReference }.Any(s => !string.IsNullOrEmpty(s)) ||
                approval.AllowedSlots.Count != 0) throw new UnauthorizedAccessException();
        }
        else if (candidate.RecipeId != existing.RecipeId || candidate.PlcRecipeId != existing.PlcRecipeId ||
                 candidate.ReleaseStatus != existing.ReleaseStatus ||
                 JsonSerializer.Serialize(candidate.Approval) != JsonSerializer.Serialize(existing.Approval))
            throw new UnauthorizedAccessException();
    }

    private static string RecipeReadTag(string recipeId, string version) =>
        "\"recipe-read-1." + Encode(recipeId) + "." + Encode(version) + "\"";
    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static bool TryReadTag(string text, string recipeId, out string? version)
    {
        version = null;
        if (!text.StartsWith('"') || !text.EndsWith('"')) return false;
        var parts = text[1..^1].Split('.');
        if (parts.Length != 3 || parts[0] != "recipe-read-1") return false;
        try
        {
            static string Decode(string value)
            {
                var base64 = value.Replace('-', '+').Replace('_', '/');
                return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '=')));
            }
            if (Decode(parts[1]) != recipeId) return false;
            version = Decode(parts[2]); return !string.IsNullOrEmpty(version);
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException) { return false; }
    }

    private static bool IsStorageReadError(Exception error) => error is IOException or InvalidOperationException or SqliteException or TimeoutException;
    private static IEnumerable<JsonObject> EditorObjects(JsonNode slot)
    {
        yield return slot["physicalEntity"]!.AsObject();
        foreach (var member in slot["members"]!.AsObject()) yield return member.Value!.AsObject();
    }
    private static RecipeInspectionKind EditorKind(RecipeDefinition source) => source.InspectionKind ??
        (source.Route == "specialType1Part" ? RecipeInspectionKind.SpecialRotation : RecipeInspectionKind.Ordinary);
    private static string EditorRoute(string unitKind, RecipeInspectionKind kind) =>
        RecipeDefinitionValidator.AuthoringRoute(unitKind,kind);
    private static RecipeDefinition ResolveEditorSource(JsonObject body, EditorLayoutRequest request, IRecipeCatalog catalog)
    {
        var matches = catalog.GetSnapshot().Definitions.Where(d => d.RecipeId == request.SourceRecipeId &&
            d.Version == request.SourceVersion && d.Model == body["model"]!.GetValue<string>() &&
            d.ScenarioId == body["scenarioId"]!.GetValue<string>() && d.UnitKind == body["unitKind"]!.GetValue<string>() &&
            d.Route == body["route"]!.GetValue<string>() &&
            EditorKind(d) == (body["inspectionKind"]?.GetValue<string>() == "specialRotation" ?
                RecipeInspectionKind.SpecialRotation : RecipeInspectionKind.Ordinary)).ToArray();
        if (matches.Length != 1) throw new JsonException("编辑所用后台配置已变化或不相容，请重新读取。");
        return matches[0];
    }
    private static void BlankHandlingPoint(JsonNode handling)
    {
        foreach (var axis in new[] { "x", "y", "z" }) handling["point"]![axis] = null;
    }
    private static void MapEditorLayout(JsonObject body, RecipeTrayLayout layout, RecipeDefinition source)
    {
        // Input mapping only. The unique common validator remains the save/business gate.
        if (layout.Rows != 10 || layout.Columns != 10 || layout.Cells.Any(c =>
            c.Row is < 1 or > 10 || c.Column is < 1 or > 10 ||
            c.CellId != RecipeTrayLayout.CellIdentity(c.Row, c.Column)) ||
            layout.Cells.Select(c => c.CellId).Distinct(StringComparer.Ordinal).Count() != layout.Cells.Count)
            throw new JsonException("料盘布局必须使用10×10实际格位且无重复格。");
        var previous = body["trayLayout"]?["cells"]?.AsArray() ?? new JsonArray();
        var positions = body["positions"]!.AsArray();
        var inputs = body["executionPositions"]!.AsObject();
        var targets = body["sortingTargets"]?.AsObject() ?? new JsonObject();
        body["sortingTargets"] = targets.Parent is null ? targets : targets.DeepClone();
        targets = body["sortingTargets"]!.AsObject();
        bool Unchanged(RecipeTrayCell cell) => previous.Any(c => c!["cellId"]!.GetValue<string>() == cell.CellId &&
            c["region"]!.GetValue<string>() == cell.Region.ToString());
        foreach (var position in positions.ToArray())
        {
            var cell = layout.Cells.SingleOrDefault(c => c.CellId == position!["cellId"]?.GetValue<string>() && c.Region == RecipeTrayRegion.OK);
            if (cell is not null && Unchanged(cell)) continue;
            inputs.Remove(position!["slotId"]!.GetValue<string>()); positions.Remove(position);
        }
        foreach (var target in targets.ToArray())
        {
            var cell = layout.Cells.SingleOrDefault(c => c.CellId == target.Key && c.Region != RecipeTrayRegion.OK);
            if (cell is null || !Unchanged(cell)) targets.Remove(target.Key);
        }
        var sourceJson = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(source))!.AsObject();
        foreach (var cell in layout.Cells.OrderBy(c => c.Row).ThenBy(c => c.Column))
        {
            if (cell.Region != RecipeTrayRegion.OK)
            {
                if (!targets.ContainsKey(cell.CellId))
                {
                    var supplied = source.TrayLayout?.Cells.Where(c => c.Region == cell.Region)
                        .Select(c => source.SortingTargets?.GetValueOrDefault(c.CellId)).OfType<HandlingPoint>().ToArray() ?? [];
                    if (supplied.Length == 0) throw new JsonException("所选区域缺少真实分拣点位的后台单位和基准配置。");
                    var point = JsonSerializer.SerializeToNode(supplied[0], new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                    point["point"]!["id"]="sorting-"+cell.CellId;
                    BlankHandlingPoint(point); targets[cell.CellId] = point;
                }
                continue;
            }
            if (positions.Any(p => p!["cellId"]?.GetValue<string>() == cell.CellId ||
                body["unitKind"]?.GetValue<string>() == "looseGroup" && p["members"]!.AsArray().Any(member =>
                    member!["cellId"]?.GetValue<string>() == cell.CellId))) continue;
            var templates = source.Positions.Where(p => p.CellId == cell.CellId).ToArray();
            if (templates.Length == 0 && source.Positions.Count == 1) templates = [source.Positions.Single()];
            if (templates.Length != 1) throw new JsonException("新增格位缺少唯一、相容的后台对象配置，请准备对应来源。");
            var template = templates.Single();
            var position = sourceJson["positions"]!.AsArray().Single(p => p!["slotId"]!.GetValue<string>() == template.SlotId)!.DeepClone();
            var slot = sourceJson["executionPositions"]![template.SlotId]!.DeepClone();
            var id = "slot-" + Guid.NewGuid().ToString("N");
            position["slotId"] = id; position["cellId"] = cell.CellId;
            var binding = source.TraySlotMapping?.Bindings.SingleOrDefault(b => b.CellId == cell.CellId);
            position["physicalSlotIndex"] = binding?.PhysicalSlotIndex; position["unitPattern"] = "{TrayRunId}/" + id;
            slot["slotId"] = id;
            foreach (var item in EditorObjects(slot))
            {
                item["sorting"] = new JsonObject(); item["rotation"] = null;
                var coordinates=item["coordinates"]!.AsArray();
                foreach(var coordinate in coordinates.ToArray())
                    if(!body["stages"]!.AsArray().Any(stage=>RecipeStageIdentity.Create(stage!["number"]!.GetValue<int>())==coordinate!["stageId"]!.GetValue<string>()&&
                        stage["targets"]!.AsArray().Any(t=>t!["localFace"]!.GetValue<int>()==coordinate["localFace"]!.GetValue<int>()&&
                            t["cameraPair"]!.GetValue<string>().Contains(coordinate["camera"]!.GetValue<string>(),StringComparison.Ordinal))))coordinates.Remove(coordinate);
                foreach (var coordinate in coordinates)
                { coordinate!["slotId"] = id; coordinate["physicalSlotIndex"] = binding?.PhysicalSlotIndex; }
                if (item["originPutBack"] is JsonNode origin) BlankHandlingPoint(origin);
            }
            inputs[id] = slot; positions.Add(position);
            BlankEditorInputs(body, position, slot);
        }
        // Keep the source's explicit target-cell associations during intermediate
        // selection. Removing a target deletes its point/config above; the one
        // common save validator rejects a reference until its real target exists.
        // Selection order must neither erase a known binding nor allocate another.
        body["trayLayout"] = JsonSerializer.SerializeToNode(layout, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        body["capacity"] = layout.Cells.Count(c => c.Region == RecipeTrayRegion.OK);
        body["ngCapacity"] = layout.Cells.Count(c => c.Region == RecipeTrayRegion.NG);
        body["pendingCapacity"] = layout.Cells.Count(c => c.Region == RecipeTrayRegion.Pending);
        if (source.TraySlotMapping is { } suppliedMapping)
            body["traySlotMapping"] = JsonSerializer.SerializeToNode(suppliedMapping with {
                Bindings=suppliedMapping.Bindings.Where(b=>layout.Cells.Any(c=>c.CellId==b.CellId&&c.Region==RecipeTrayRegion.OK)).ToArray()
            },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (body["traySlotMapping"]?["bindings"] is JsonArray bindings)
            foreach (var binding in bindings.ToArray())
                if (!layout.Cells.Any(c => c.CellId == binding!["cellId"]!.GetValue<string>() && c.Region == RecipeTrayRegion.OK)) bindings.Remove(binding);
    }
    private static void BlankEditorCapture(JsonObject body, JsonNode point, string configuredProfile)
    {
        var profiles = body["captureProfiles"]!.AsObject();
        var basis = point["captureProfile"]?.GetValue<string>() ?? configuredProfile;
        var profile = profiles[basis]!.DeepClone();
        var id = "photo-editor-" + Guid.NewGuid().ToString("N");
        profile["id"] = id; profile["settings"]!["profileId"] = id;
        foreach (var key in new[] { "exposureUs", "gain", "brightnessPercent" }) profile["settings"]![key] = null;
        profiles[id] = profile; point["captureProfile"] = id;
    }
    private static void BlankEditorHandling(JsonObject input)
    {
        var points = input["sorting"]!.AsObject().Select(p => p.Value).Append(input["source"]);
        foreach (var point in points.Where(p => p is not null))
            foreach (var axis in new[] { "x", "y", "z" }) point!["point"]![axis] = null;
    }
    private static void BlankEditorInputs(JsonObject body, JsonNode position, JsonNode slot)
    {
        var members = slot["members"]!.AsObject();
        foreach (var member in position["members"]!.AsArray())
        {
            var material = member!["material"]!.GetValue<string>();
            var detection = members.TryGetPropertyValue(material, out var configured) ? configured! : slot["physicalEntity"]!;
            var handling = body["unitKind"]!.GetValue<string>() == "looseGroup" ? members[material]! : slot["physicalEntity"]!;
            BlankEditorHandling(handling.AsObject());
            void BlankPurpose(string? reference)
            {
                if (reference is null) return;
                var point = handling["purposePoints"]![reference]!;
                point["point"]!["x"] = null; point["point"]!["y"] = null;
            }
            foreach (var stage in body["stages"]!.AsArray())
            foreach (var target in stage!["targets"]!.AsArray().Where(t => t!["material"]!.GetValue<string>() == material))
            {
                var stageId = RecipeStageIdentity.Create(stage["number"]!.GetValue<int>());
                var pair = target!["cameraPair"]!.GetValue<string>();
                foreach (var coordinate in detection["coordinates"]!.AsArray().Where(c =>
                    c!["stageId"]!.GetValue<string>() == stageId && pair.Contains(c["camera"]!.GetValue<string>(), StringComparison.Ordinal)))
                {
                    coordinate!["point"]!["x"] = null; coordinate["point"]!["y"] = null; coordinate["fixed"]!["z"] = null;
                    BlankEditorCapture(body, coordinate, target["captureProfile"]!.GetValue<string>());
                }
                if (handling["flip"]?["stages"]?[stageId] is JsonNode flip)
                { BlankPurpose(flip["pickPointRef"]!.GetValue<string>()); BlankPurpose(flip["putBackPointRef"]!.GetValue<string>()); }
            }
            var e = body["eCode"]!;
            if (e["enabled"]!.GetValue<bool>() && e["representativeMaterial"]!.GetValue<string>() == material)
            {
                var extra = e["extraPose"];
                var reference = (extra?["scanPointRef"] ?? e["scanPointRef"])!.GetValue<string>();
                var scan = detection["purposePoints"]![reference]!;
                scan["point"]!["x"] = null; scan["point"]!["y"] = null; scan["fixed"]!["z"] = null;
                BlankEditorCapture(body, scan, (extra?["captureProfile"] ?? e["captureProfile"])!.GetValue<string>());
                if (extra is not null)
                { BlankPurpose(extra["pickPointRef"]!.GetValue<string>()); BlankPurpose(extra["putBackPointRef"]!.GetValue<string>()); }
            }
        }
    }
    private static bool IncompleteEditorPoint(JsonNode? point) => point is not null &&
        (point["point"]?["x"] is null || point["point"]?["y"] is null || point["fixed"]?["z"] is null);
    private static void PruneIncompleteEditorInputs(JsonObject body)
    {
        // Delete only incomplete, no-longer-selected editing placeholders; preserve valid saved background values.
        var references = new HashSet<string>();
        foreach (var position in body["positions"]!.AsArray())
        {
            var slot = body["executionPositions"]![position!["slotId"]!.GetValue<string>()]!;
            var active = EditorObjects(slot).ToDictionary(o => o, _ => new HashSet<string>());
            foreach (var member in position["members"]!.AsArray())
            {
                var material = member!["material"]!.GetValue<string>();
                var members = slot["members"]!.AsObject();
                var detection = (members.TryGetPropertyValue(material, out var input) ? input! : slot["physicalEntity"]!).AsObject();
                var handling = (body["unitKind"]!.GetValue<string>() == "looseGroup" ? members[material]! : slot["physicalEntity"]!).AsObject();
                foreach (var stage in body["stages"]!.AsArray().Where(s => s!["targets"]!.AsArray().Any(t => t!["material"]!.GetValue<string>() == material)))
                {
                    var id = RecipeStageIdentity.Create(stage!["number"]!.GetValue<int>());
                    if (handling["flip"]?["stages"]?[id] is JsonNode flip)
                    { active[handling].Add(flip["pickPointRef"]!.GetValue<string>()); active[handling].Add(flip["putBackPointRef"]!.GetValue<string>()); }
                }
                var e = body["eCode"]!;
                if (e["enabled"]!.GetValue<bool>() && e["representativeMaterial"]!.GetValue<string>() == material)
                {
                    var extra = e["extraPose"];
                    active[detection].Add((extra?["scanPointRef"] ?? e["scanPointRef"])!.GetValue<string>());
                    if (extra is not null)
                    { active[handling].Add(extra["pickPointRef"]!.GetValue<string>()); active[handling].Add(extra["putBackPointRef"]!.GetValue<string>()); }
                }
            }
            foreach (var input in active)
            {
                var points = input.Key["purposePoints"]!.AsObject();
                foreach (var point in points.Where(p => !input.Value.Contains(p.Key) && IncompleteEditorPoint(p.Value)).ToArray()) points.Remove(point.Key);
                foreach (var point in input.Key["coordinates"]!.AsArray().Concat(points.Select(p => p.Value)))
                    if (point?["captureProfile"] is JsonNode profile) references.Add(profile.GetValue<string>());
            }
        }
        var profiles = body["captureProfiles"]!.AsObject();
        foreach (var profile in profiles.Where(p => p.Key.StartsWith("photo-editor-", StringComparison.Ordinal) &&
            !references.Contains(p.Key) && p.Value!["settings"]!["exposureUs"] is null).ToArray()) profiles.Remove(profile.Key);
    }
    private static IResult ReadUnavailable(HttpContext http, Exception error)
    {
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Gaode.RecipeAuthoring")
            .LogError(error, "RecipeAuthoring.ReadFailed traceId={TraceId} path={Path}", http.TraceIdentifier, http.Request.Path);
        return Station01ApiResults.Error(http, 503, "RecipeReadUnavailable", "配方读取不可用，请查看诊断。", "Storage");
    }
    private static IResult BadAuthoringRequest(HttpContext http, string reason) => Station01ApiResults.Error(http, 400,
        "InvalidRequest", "配方请求格式无效。", details: new { reason });
    private static IResult MetadataDenied(HttpContext http) => Station01ApiResults.Error(http, 403,
        "Forbidden", "身份或批准信息只读，保存不能提升生产准入。", "Authorization");
}
