using Com.VibeCoding.SaveClipboard.Models;
using Com.VibeCoding.SaveClipboard.Services;
using Xunit;

namespace SaveClipboard.Tests;

public class ClipboardPrivacyPolicyTests
{
    [Fact]
    public void 沒有標記時允許擷取()
    {
        Assert.Equal(ClipboardSkipReason.None, ClipboardPrivacyPolicy.Evaluate(false, null));
    }

    [Fact]
    public void 來源標記不記錄時略過()
    {
        Assert.Equal(
            ClipboardSkipReason.ExcludedBySource,
            ClipboardPrivacyPolicy.Evaluate(hasExcludeMarker: true, canIncludeInHistory: null));
    }

    [Fact]
    public void 來源停用歷史時略過()
    {
        Assert.Equal(
            ClipboardSkipReason.HistoryDisabledBySource,
            ClipboardPrivacyPolicy.Evaluate(hasExcludeMarker: false, canIncludeInHistory: false));
    }

    [Fact]
    public void 來源允許歷史時允許擷取()
    {
        Assert.Equal(
            ClipboardSkipReason.None,
            ClipboardPrivacyPolicy.Evaluate(hasExcludeMarker: false, canIncludeInHistory: true));
    }

    [Fact]
    public void 排除標記優先於允許歷史()
    {
        Assert.Equal(
            ClipboardSkipReason.ExcludedBySource,
            ClipboardPrivacyPolicy.Evaluate(hasExcludeMarker: true, canIncludeInHistory: true));
    }
}
