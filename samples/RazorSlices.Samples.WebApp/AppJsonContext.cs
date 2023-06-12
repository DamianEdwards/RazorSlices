using System.Text.Json.Serialization;

namespace RazorSlices.Samples.WebApp;

#if NET8_0_OR_GREATER
[JsonSerializable(typeof(ResultDto))]
partial class AppJsonContext : JsonSerializerContext
{

}
#endif
