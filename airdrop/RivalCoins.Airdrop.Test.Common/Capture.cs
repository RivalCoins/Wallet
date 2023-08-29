using FakeItEasy;

namespace RivalCoins.Airdrop.Test.Common;

public class Capture<TParameter>
{
    private readonly List<TParameter> _captures = new List<TParameter>();
    private readonly Lazy<TParameter> _parameterCapturer;

    public Capture()
    {
        _parameterCapturer = new Lazy<TParameter>(() =>
        {
            return A<TParameter>.That.Matches(captured =>
            {
                _captures.Add(captured);

                return true;
            }, "Recording captured value");
        });
    }

    public TParameter Captured =>
        _captures.Count == 1
            ? _captures.First() : throw new Exception($"Expected to capture exactly 1 parameter value but captured {_captures.Count} parameter values.");

    public List<TParameter> MultiCaptures =>
        _captures.Count > 1
            ? _captures : throw new Exception($"Expected to capture multiple parameter values but captured {_captures.Count} parameter values.");

    public static implicit operator TParameter(Capture<TParameter> capture)
    {
        return capture._parameterCapturer.Value;
    }
}