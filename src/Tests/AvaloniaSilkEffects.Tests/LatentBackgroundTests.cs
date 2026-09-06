using AvaloniaSilkEffects.Backgrounds;
using SkiaSharp;

namespace AvaloniaSilkEffects.Tests;

public sealed class LatentBackgroundTests
{
    [Fact]
    public void CoverColorsKeepFoliaOrderAndThemeFallback()
    {
        var theme = LatentPalette.Midnight;
        Assert.Equal(new[] { theme.Secondary,theme.Primary,theme.Secondary,theme.Primary,theme.Background,theme.Accent },
            theme.MeshColors());
        var a = new EffectColor(1,0,0);
        Assert.Equal(new[] { a,theme.Primary,a,theme.Primary,theme.Background,theme.Accent },
            (theme with { Cover = new[]{a} }).MeshColors());
    }
    [Fact]
    public void CoverExtractionUsesStraightRgbaAndSkipsTransparentPixels()
    {
        using var bitmap = new SKBitmap(50,50);
        bitmap.Erase(new SKColor(220,40,20,200));
        using var data = bitmap.Encode(SKEncodedImageFormat.Png,100);
        var colors = LatentCoverPalette.ExtractEncoded(data.ToArray());
        Assert.Single(colors);
        Assert.InRange(colors[0].R,.85f,.87f);
        bitmap.Erase(SKColors.Transparent);
        using var transparent = bitmap.Encode(SKEncodedImageFormat.Png,100);
        Assert.Empty(LatentCoverPalette.ExtractEncoded(transparent.ToArray()));
    }
    [Fact]
    public void PausedBackgroundRunsAtOriginalReducedSpeed()
    {
        var state = new LatentModulation();
        state.Step(new LatentAudio(),.1);
        Assert.Equal(.0036,state.MeshTime,8);
        Assert.Equal(.0012,state.DitherTime,8);
        Assert.Equal(0,state.Bass);
    }
    [Fact]
    public void BeatResponseIsDeterministicAndBounded()
    {
        var a = new LatentModulation();
        var b = new LatentModulation();
        var audio = new LatentAudio(1,1,1,1,1,1,false);
        for (var i=0;i<120;i++) { a.Step(audio,1d/60); b.Step(audio,1d/60); }
        Assert.Equal(a.MeshTime,b.MeshTime);
        Assert.InRange(a.Beat,0,1);
        Assert.InRange(a.MeshTime,.6,4);
    }
}
