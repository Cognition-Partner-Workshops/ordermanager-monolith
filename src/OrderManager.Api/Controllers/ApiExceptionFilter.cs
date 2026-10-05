using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderManager.Api.Services;

namespace OrderManager.Api.Controllers;

public class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            ArgumentException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status409Conflict,
            InventoryServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => (int?)null
        };
        if (status is null) return;

        context.Result = new ObjectResult(new { error = context.Exception.Message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
