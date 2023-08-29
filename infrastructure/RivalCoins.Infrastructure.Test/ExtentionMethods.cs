using FsCheck;
using Pulumi;

namespace RivalCoins.Infrastructure.Test;

public static class ExtentionMethods
{
    public static Property And(this Property property, System.Linq.Expressions.Expression<Func<bool>> testable, string label)
    {
        property.QuickCheckThrowOnFailure();

        var testableCompiled = testable.Compile();

        return testableCompiled().Label(label);
    }

    public static TValue Value<TValue>(this Output<TValue> output) => GetValueAsync(output).Result;

    public static Task<T> GetValueAsync<T>(this Output<T> output)
    {
        var tcs = new TaskCompletionSource<T>();
        output.Apply(v =>
        {
            tcs.SetResult(v);
            return v;
        });
        return tcs.Task;
    }
}
