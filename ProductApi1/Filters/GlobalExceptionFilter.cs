using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ProductApi1.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<GlobalExceptionFilter> _logger;

        public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception, "Unhandled exception occurred");

            var result = new ObjectResult(new
            {
                error = "An error occurred while processing your request.",
                details = context.Exception.Message
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };

            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}