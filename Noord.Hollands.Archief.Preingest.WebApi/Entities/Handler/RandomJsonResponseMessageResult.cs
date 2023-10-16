using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Handler
{
    public class RandomJsonResponseMessageResult : IActionResult
    {
        private readonly HttpResponseMessage _responseMessage;

        public RandomJsonResponseMessageResult(HttpResponseMessage responseMessage)
        {
            _responseMessage = responseMessage; // could add throw if null
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = (int)_responseMessage.StatusCode;

            foreach (var header in _responseMessage.Headers)
            {
                context.HttpContext.Response.Headers.TryAdd(header.Key, new StringValues(header.Value.ToArray()));
            }

            context.HttpContext.Response.Headers.TryAdd("Content-Type", new StringValues("application/json"));

            using (var stream = await _responseMessage.Content.ReadAsStreamAsync())
            {
                await stream.CopyToAsync(context.HttpContext.Response.Body);
                await context.HttpContext.Response.Body.FlushAsync();
            }
        }
    }
}
