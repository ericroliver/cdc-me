
using Newtonsoft.Json;

namespace Softbase;

public static class JsonUtilities
{
    public static string ToJson<T>(this T model, bool pretty = false)
    {
        return JsonConvert.SerializeObject(model, pretty ? Formatting.Indented : Formatting.None);
    }

    public static T FromJson<T>(this string buffer)
    {
        return JsonConvert.DeserializeObject<T>(buffer);
    }
}
