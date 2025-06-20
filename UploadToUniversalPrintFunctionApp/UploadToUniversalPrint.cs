using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace UploadToUniversalPrintFunctionApp
{
    public class UploadToUniversalPrint
    {
        private static readonly HttpClient httpClient = new HttpClient();

        [Function("UploadToUniversalPrint")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            FunctionContext context)
        {
            var log = context.GetLogger("UploadToUniversalPrint");
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Request body is empty.");
                    return badResponse;
                }

                UploadRequest? data;
                try
                {
                    data = JsonConvert.DeserializeObject<UploadRequest>(requestBody);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to deserialize request body.");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid JSON payload.");
                    return badResponse;
                }

                if (data == null || string.IsNullOrWhiteSpace(data.UploadUrl) || string.IsNullOrWhiteSpace(data.FileBase64) || data.FileSize <= 0)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Missing required fields.");
                    return badResponse;
                }

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(data.FileBase64);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Invalid base64 string.");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("fileBase64 is not valid base64.");
                    return badResponse;
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

                var ok = req.CreateResponse(HttpStatusCode.OK);
                await ok.WriteAsJsonAsync(result);
                return ok;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Unhandled exception while processing request.");
                var error = req.CreateResponse(HttpStatusCode.InternalServerError);
                await error.WriteStringAsync($"Internal Server Error: {ex.Message}");
                return error;
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
