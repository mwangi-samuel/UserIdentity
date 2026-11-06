using System.Text;
using Newtonsoft.Json;

namespace UserIdentity.IntegrationTests.TestUtils;

internal static class SerDeHelper
{
    public static HttpContent ConvertToHttpContent<T>(T data)
    {
        var jsonQuery = JsonConvert.SerializeObject(data);
        HttpContent httpContent = new StringContent(jsonQuery, Encoding.UTF8);
        httpContent.Headers.Remove("content-type");
        httpContent.Headers.Add("content-type", "application/json; charset=utf-8");

        return httpContent;
    }

    public static T? Deserialize<T>(string jsonString)
    {
        return JsonConvert.DeserializeObject<T>(jsonString);
    }
}
