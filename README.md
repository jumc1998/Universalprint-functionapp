# Universalprint Function App

This repository contains an Azure Function written in C# (.NET 8.0, isolated worker) to upload files to Microsoft Universal Print. The function exposes an HTTP endpoint that accepts a JSON payload with an upload URL, a base64-encoded file, and the file size. It handles constructing a PUT request with the correct `Content-Range` and `Content-Length` headers so that Power Automate can upload files to Universal Print.

## Project Structure

- `UploadToUniversalPrintFunctionApp/`
  - `UploadToUniversalPrintFunctionApp.csproj` – Azure Functions project file targeting .NET 8 (isolated).
  - `UploadToUniversalPrint.cs` – Implementation of the `UploadToUniversalPrint` function.

## Building

Use the Azure Functions Core Tools or the .NET SDK to build and run the function locally.

```bash
# build
dotnet build UploadToUniversalPrintFunctionApp/UploadToUniversalPrintFunctionApp.csproj
```

```bash
# start the function host
func start
```

## Example Request

```json
{
  "uploadUrl": "https://print.print.microsoft.com/uploadSessions/abc123?tempauthtoken=xyz",
  "fileBase64": "<Base64EncodedPDFContent>",
  "fileSize": 4533322
}
```

Send a POST request with the above payload to the function endpoint. The response will include the status code, reason phrase, headers, and body returned by the Universal Print upload server.
