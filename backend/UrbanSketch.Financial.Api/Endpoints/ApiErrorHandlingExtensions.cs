namespace UrbanSketch.Financial.Api.Endpoints;

public static class ApiErrorHandlingExtensions
{
    public static WebApplication UseApiErrorHandling(this WebApplication app)
    {
        // Explicit handling also protects Development responses from exception/connection details.
        app.UseExceptionHandler(handler => handler.Run(context =>
            TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                type: "about:blank",
                title: "Внутренняя ошибка сервера.")
            .ExecuteAsync(context)));
        return app;
    }
}
