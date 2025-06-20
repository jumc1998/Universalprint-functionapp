using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace UploadToUniversalPrintFunctionApp
{
    public static class UploadToUniversalPrint
    {
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("UploadToUniversalPrint")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    return new BadRequestObjectResult("Request body is empty.");
                }

                UploadRequest? data;
                try
                {
                    data = JsonConvert.DeserializeObject<UploadRequest>(requestBody);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to deserialize request body.");
                    return new BadRequestObjectResult("Invalid JSON payload.");
                }

                if (data == null || string.IsNullOrWhiteSpace(data.UploadUrl) || string.IsNullOrWhiteSpace(data.FileBase64) || data.FileSize <= 0)
                {
                    return new BadRequestObjectResult("Missing required fields.");
                }

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(data.FileBase64);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Invalid base64 string.");
                    return new BadRequestObjectResult("fileBase64 is not valid base64.");
                }

                using var requestMessage = new HttpRequestMessage(HttpMethod.Put, data.UploadUrl);
                requestMessage.Content = new ByteArrayContent(fileBytes);
                requestMessage.Content.Headers.ContentLength = fileBytes.Length;
                requestMessage.Content.Headers.Add("Content-Range", $"bytes 0-{fileBytes.Length - 1}/{data.FileSize}");

                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                string responseBody = await response.Content.ReadAsStringAsync();

                var headers = response.Headers.Concat(response.Content.Headers)
                    .ToDictionary(h => h.Key, h => h.Value.ToArray());

                var result = new UploadResponse
                {
                    StatusCode = (int)response.StatusCode,
                    Reason = response.ReasonPhrase,
                    Headers = headers,
                    Body = responseBody
                };

                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled exception while processing request.");
                return new ObjectResult($"Internal Server Error: {ex.Message}") { StatusCode = 500 };
            }
        }

        private class UploadRequest
        {
            [JsonProperty("uploadUrl")]
            public string UploadUrl { get; set; } = string.Empty;

            [JsonProperty("fileBase64")]
            public string FileBase64 { get; set; } = string.Empty;

            [JsonProperty("fileSize")]
            public long FileSize { get; set; }
        }

        private class UploadResponse
        {
            public int StatusCode { get; set; }
            public string? Reason { get; set; }
            public IDictionary<string, string[]>? Headers { get; set; }
            public string? Body { get; set; }
        }
    }
}
