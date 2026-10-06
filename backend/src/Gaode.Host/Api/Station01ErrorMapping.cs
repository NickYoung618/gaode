using Microsoft.EntityFrameworkCore;
using Gaode.Application.Configuration;

namespace Gaode.Host.Api;

public static class Station01ErrorMapping
{
    public static IResult Map(HttpContext context, Exception error) => error switch
    {
        ConfigurationException configuration => Station01ApiResults.Error(context,
            StatusCodes.Status400BadRequest, configuration.Code, configuration.Message, "Configuration"),
        ArgumentException argument => Station01ApiResults.Error(context,
            StatusCodes.Status400BadRequest, "InvalidRequest", "请求参数无效", details: argument.Message),
        UnauthorizedAccessException => Station01ApiResults.Error(context,
            StatusCodes.Status403Forbidden, "Forbidden", "当前身份无权执行此操作", "Authorization"),
        FileNotFoundException => Station01ApiResults.NotFound(context, "MediaNotFound", "媒体不存在或尚未保存"),
        TimeoutException timeout => Station01ApiResults.Error(context,
            StatusCodes.Status503ServiceUnavailable, "DependencyTimeout", "依赖服务处理超时", "Dependency", true,
            new { reason = timeout.Message }),
        DbUpdateException storage => Station01ApiResults.Error(context,
            StatusCodes.Status503ServiceUnavailable, "PersistenceFailed",
            "持久化提交失败，请通过GET核对后重试", "Storage", true,
            new { reason = storage.GetType().Name }),
        InvalidOperationException operation when operation.Message is "RequestConflict" or "PhysicalRunHeld" =>
            Station01ApiResults.Error(context, StatusCodes.Status409Conflict, operation.Message,
                "StateConflict", details: "请求冲突或设备仍被当前运行占用"),
        InvalidOperationException operation when operation.Message == "RunCapacityUnavailable" =>
            Station01ApiResults.Error(context, StatusCodes.Status429TooManyRequests, "Busy", "流程容量不足", "Capacity", true),
        InvalidOperationException operation when operation.Message == "HostStopping" =>
            Station01ApiResults.Error(context, StatusCodes.Status503ServiceUnavailable, "SaveUnavailable",
                "Host正在有界关闭，已拒绝新动作准入", "Lifecycle", true),
        InvalidOperationException operation => Station01ApiResults.Error(context,
            StatusCodes.Status409Conflict, "OperationRejected", "当前状态不允许此操作", "State", details: operation.Message),
        _ => Station01ApiResults.Error(context, StatusCodes.Status500InternalServerError,
            "InternalError", "后端处理失败", "Internal")
    };
}
