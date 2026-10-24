using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog.Context;

namespace Identity.Services;

/// <summary>
/// Переносит идентификатор трассировки между gRPC-вызовами и добавляет его в контекст логирования.
/// </summary>
public sealed class TraceIdInterceptor : Interceptor
{
    private const string TraceIdHeader = "traceid";
    private static readonly AsyncLocal<string?> CurrentTraceId = new();

    /// <summary>
    /// Добавляет идентификатор трассировки в метаданные синхронного unary-вызова.
    /// </summary>
    /// <param name="request">Отправляемый запрос.</param>
    /// <param name="context">Контекст клиентского вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    /// <returns>Ответ удалённого gRPC-сервиса.</returns>
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, AddTraceId(context));

    /// <summary>
    /// Устанавливает идентификатор трассировки для входящего unary-вызова на время его обработки.
    /// </summary>
    /// <param name="request">Входящий запрос.</param>
    /// <param name="context">Контекст серверного вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    /// <returns>Результат обработки запроса.</returns>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        using var traceId = PushTraceId(context);
        return await continuation(request, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Устанавливает идентификатор трассировки для входящего клиентского потокового вызова.
    /// </summary>
    /// <param name="requestStream">Поток входящих запросов.</param>
    /// <param name="context">Контекст серверного вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    /// <returns>Результат обработки потока.</returns>
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var traceId = PushTraceId(context);
        return await continuation(requestStream, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Устанавливает идентификатор трассировки для входящего серверного потокового вызова.
    /// </summary>
    /// <param name="request">Входящий запрос.</param>
    /// <param name="responseStream">Поток исходящих ответов.</param>
    /// <param name="context">Контекст серверного вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var traceId = PushTraceId(context);
        await continuation(request, responseStream, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Устанавливает идентификатор трассировки для входящего двунаправленного потокового вызова.
    /// </summary>
    /// <param name="requestStream">Поток входящих запросов.</param>
    /// <param name="responseStream">Поток исходящих ответов.</param>
    /// <param name="context">Контекст серверного вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        using var traceId = PushTraceId(context);
        await continuation(requestStream, responseStream, context).ConfigureAwait(false);
    }

    /// <summary>
    /// Добавляет идентификатор трассировки в метаданные асинхронного unary-вызова.
    /// </summary>
    /// <param name="request">Отправляемый запрос.</param>
    /// <param name="context">Контекст клиентского вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    /// <returns>Результат асинхронного unary-вызова.</returns>
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, AddTraceId(context));

    /// <summary>
    /// Добавляет идентификатор трассировки в метаданные клиентского потокового вызова.
    /// </summary>
    /// <param name="context">Контекст клиентского вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    /// <returns>Результат клиентского потокового вызова.</returns>
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(AddTraceId(context));

    /// <summary>
    /// Добавляет идентификатор трассировки в метаданные серверного потокового вызова.
    /// </summary>
    /// <param name="request">Отправляемый запрос.</param>
    /// <param name="context">Контекст клиентского вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    /// <returns>Результат серверного потокового вызова.</returns>
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(request, AddTraceId(context));

    /// <summary>
    /// Добавляет идентификатор трассировки в метаданные двунаправленного потокового вызова.
    /// </summary>
    /// <param name="context">Контекст клиентского вызова.</param>
    /// <param name="continuation">Следующий обработчик вызова.</param>
    /// <returns>Результат двунаправленного потокового вызова.</returns>
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation) =>
        continuation(AddTraceId(context));

    private static IDisposable? PushTraceId(ServerCallContext context)
    {
        var incomingTraceId = context.RequestHeaders.FirstOrDefault(header => header.Key == TraceIdHeader)?.Value;
        var traceId = string.IsNullOrWhiteSpace(incomingTraceId)
            ? Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString()
            : incomingTraceId;

        if (string.IsNullOrWhiteSpace(traceId))
        {
            return null;
        }

        var previousTraceId = CurrentTraceId.Value;
        CurrentTraceId.Value = traceId;
        return new TraceIdScope(LogContext.PushProperty("TraceId", traceId), previousTraceId);
    }

    private static ClientInterceptorContext<TRequest, TResponse> AddTraceId<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var traceId = CurrentTraceId.Value
            ?? Activity.Current?.TraceId.ToString()
            ?? ActivityTraceId.CreateRandom().ToString();
        if (string.IsNullOrWhiteSpace(traceId))
        {
            return context;
        }

        var headers = new Metadata();
        if (context.Options.Headers is { } existingHeaders)
        {
            foreach (var header in existingHeaders)
            {
                if (header.Key == TraceIdHeader)
                {
                    continue;
                }

                headers.Add(header);
            }
        }

        headers.Add(TraceIdHeader, traceId);

        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));
    }

    private sealed class TraceIdScope(IDisposable logContext, string? previousTraceId) : IDisposable
    {
        public void Dispose()
        {
            logContext.Dispose();
            CurrentTraceId.Value = previousTraceId;
        }
    }
}
