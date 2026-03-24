using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers;

/// <summary>
/// Base controller class providing common response helper methods for all API controllers.
/// </summary>
[ApiController]
public abstract class nirmataController : ControllerBase
{
    /// <summary>
    /// Returns a 200 OK result with the specified data.
    /// </summary>
    protected IActionResult OkResult<T>(T data) => Ok(data);

    /// <summary>
    /// Returns a 201 Created result with the specified data and location.
    /// </summary>
    protected IActionResult CreatedResult(string actionName, object routeValues, object data)
        => CreatedAtAction(actionName, routeValues, data);

    /// <summary>
    /// Returns a 404 Not Found result with an optional message.
    /// </summary>
    protected IActionResult NotFoundResult(string message = "Resource not found")
        => Problem(title: "Not found", detail: message, statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// Returns a 400 Bad Request result with validation errors.
    /// </summary>
    protected IActionResult BadRequestResult(string message)
        => Problem(title: "Bad request", detail: message, statusCode: StatusCodes.Status400BadRequest);

    /// <summary>
    /// Returns a 400 Bad Request result with model state errors.
    /// </summary>
    protected IActionResult ValidationErrorResult()
        => ValidationProblem(ModelState);

    /// <summary>
    /// Returns a 204 No Content result.
    /// </summary>
    protected IActionResult NoContentResult() => NoContent();

    /// <summary>
    /// Returns a 500 Internal Server Error result with an optional message.
    /// </summary>
    protected IActionResult ErrorResult(string message = "An error occurred")
        => StatusCode(500, new { message });
}
