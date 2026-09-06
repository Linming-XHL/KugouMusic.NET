namespace AvaloniaSilkEffects.Backgrounds;

public sealed record LatentAudio(float Power = 0, float Bass = 0, float LowMid = 0,
    float Mid = 0, float Vocal = 0, float Treble = 0, bool Paused = true);

public sealed record LatentPalette(EffectColor Background, EffectColor Primary,
    EffectColor Secondary, EffectColor Accent, IReadOnlyList<EffectColor> Cover)
{
    public static LatentPalette Midnight => new(new(.051f,.071f,.208f), new(.9f,.91f,.95f),
        new(.42f,.45f,.57f), new(.55f,.59f,.72f), []);

    public EffectColor[] MeshColors()
    {
        var a = Cover.Count > 0 ? Cover[0] : Secondary;
        var b = Cover.Count > 1 ? Cover[1] : Primary;
        return [a, b, Cover.Count > 2 ? Cover[2] : a,
            Cover.Count > 3 ? Cover[3] : b, Background, Accent];
    }
}

internal sealed class LatentModulation
{
    public float Power, Bass, Mid, Beat;
    private float _previousEnergy, _onset;
    public double MeshTime, DitherTime;

    public void Step(LatentAudio audio, double delta)
    {
        var p = audio.Paused ? 0 : Math.Clamp(audio.Power,0,1);
        var b = audio.Paused ? 0 : Math.Clamp(audio.Bass,0,1);
        var m = audio.Paused ? 0 : Math.Clamp(Math.Max(audio.Mid,audio.Vocal),0,1);
        var energy = audio.Paused ? 0 : MathF.Pow(
            Math.Clamp(audio.Bass,0,1)*.22f + Math.Clamp(audio.LowMid,0,1)*.18f +
            Math.Clamp(audio.Mid,0,1)*.22f + Math.Clamp(audio.Vocal,0,1)*.28f +
            Math.Clamp(audio.Treble,0,1)*.1f,.55f);
        Power += (p-Power)*.12f;
        Bass += (b-Bass)*.16f;
        Mid += (m-Mid)*.13f;
        _onset = Math.Max(_onset*.84f, Math.Clamp((energy-_previousEnergy)*7,0,1));
        _previousEnergy = energy;
        var target = Math.Clamp(energy*.42f+_onset*.85f,0,1);
        Beat += (target-Beat)*(target > Beat ? .42f : .14f);
        MeshTime += Math.Clamp(delta,0,.1)*(audio.Paused ? .3*.12 : .3+(2-.3)*Beat);
        DitherTime += Math.Clamp(delta,0,.1)*(audio.Paused ? .1*.12 : .1+(1.2-.1)*Beat);
    }
}
