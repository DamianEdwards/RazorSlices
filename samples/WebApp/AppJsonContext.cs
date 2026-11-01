using System.Text.Json.Serialization;

namespace RazorSlices.Samples.WebApp;

[JsonSerializable(typeof(ResultDto))]
partial class AppJsonContext : JsonSerializerContext
{

}
