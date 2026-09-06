using System.Runtime.InteropServices;
using Avalonia;
using AvaloniaSilkEffects.Backgrounds;
using Silk.NET.OpenGL;

namespace AvaloniaSilkEffects.Tests;

public sealed class MacOpenGlFactAttribute : FactAttribute
{
    public MacOpenGlFactAttribute()
    {
        if (!OperatingSystem.IsMacOS()) Skip = "Requires macOS CGL desktop OpenGL.";
    }
}

public sealed class LatentOpenGlTests
{
    private const string Framework = "/System/Library/Frameworks/OpenGL.framework/OpenGL";
    [DllImport(Framework)] private static extern int CGLChoosePixelFormat(int[] attributes,out nint format,out int count);
    [DllImport(Framework)] private static extern int CGLCreateContext(nint format,nint share,out nint context);
    [DllImport(Framework)] private static extern int CGLSetCurrentContext(nint context);
    [DllImport(Framework)] private static extern nint CGLGetCurrentContext();
    [DllImport(Framework)] private static extern int CGLDestroyContext(nint context);
    [DllImport(Framework)] private static extern int CGLDestroyPixelFormat(nint format);

    [MacOpenGlFact]
    public unsafe void FrozenShadersRenderResizeAndRecreateOnRealGl()
    {
        var previous = CGLGetCurrentContext();
        Assert.Equal(0,CGLChoosePixelFormat([99,0x4100,73,0],out var format,out _));
        nint native = 0;
        try
        {
            Assert.Equal(0,CGLCreateContext(format,0,out native));
            Assert.Equal(0,CGLSetCurrentContext(native));
            var library = NativeLibrary.Load(Framework);
            try
            {
                using var gl = GL.GetApi(name => NativeLibrary.TryGetExport(library,name,out var address) ? address : 0);
                using var device = new EffectDevice(gl);
                using var target = new EffectFramebuffer(gl);
                var scene = new LatentBackgroundScene();
                scene.Initialize(device);
                try
                {
                    for (var iteration=0;iteration<3;iteration++)
                    {
                        var size = new PixelSize(128+iteration*16,72+iteration*8);
                        target.EnsureSize(size.Width,size.Height);
                        scene.Resize(size,1.5);
                        var frame = new EffectFrame(TimeSpan.Zero,TimeSpan.Zero,size,1.5,0);
                        device.Render(scene,frame,(int)target.Framebuffer,EffectColor.Transparent);
                        var pixels = new byte[size.Width*size.Height*4];
                        fixed (byte* p = pixels)
                            gl.ReadPixels(0,0,(uint)size.Width,(uint)size.Height,PixelFormat.Rgba,PixelType.UnsignedByte,p);
                        Assert.Equal(GLEnum.NoError,gl.GetError());
                        Assert.All(Enumerable.Range(0,size.Width*size.Height),i => Assert.Equal((byte)255,pixels[i*4+3]));
                        Assert.True(pixels.Where((_,i)=>i%4!=3).Distinct().Count()>8,"Material must contain spatial/color variation.");
                        var first = pixels.ToArray();
                        scene.Palette = LatentPalette.Midnight with
                        {
                            Cover = new EffectColor[] { new(1,0,0),new(1,.4f,0),new(.7f,.1f,0),new(.9f,.3f,.1f) }
                        };
                        device.Render(scene,frame,(int)target.Framebuffer,EffectColor.Transparent);
                        fixed (byte* p = pixels)
                            gl.ReadPixels(0,0,(uint)size.Width,(uint)size.Height,PixelFormat.Rgba,PixelType.UnsignedByte,p);
                        Assert.False(first.SequenceEqual(pixels),"Cover palette must change the rendered material.");
                        Assert.Equal(3,device.FrameMetrics.DrawCalls);
                        scene.Palette = LatentPalette.Midnight;
                        scene.DisposeGpuResources();
                        scene.Initialize(device);
                    }
                }
                finally { scene.DisposeGpuResources(); }
            }
            finally { NativeLibrary.Free(library); }
        }
        finally
        {
            CGLSetCurrentContext(previous);
            if (native != 0) CGLDestroyContext(native);
            CGLDestroyPixelFormat(format);
        }
    }
}
