using System.Threading.Tasks;

namespace TargetLib;

public static class Target
{
    static async ValueTask<string> Inner(string value)
    {
        await Task.Yield();
        return value;
    }

    public static async ValueTask<string> Gather(string value)
    {
        var result = await Inner(value);
        return result;
    }
}
