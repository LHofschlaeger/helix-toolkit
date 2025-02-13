﻿using HelixToolkit.SharpDX.Core.Components;
using HelixToolkit.SharpDX.Render;
using HelixToolkit.SharpDX.Shaders;
using HelixToolkit.SharpDX.Utilities;

namespace HelixToolkit.SharpDX.Core;

public sealed class PostEffectFXAA : RenderCore, IPostEffect
{
    private string effectName = DefaultRenderTechniqueNames.PostEffectFXAA;
    public string EffectName
    {
        set
        {
            SetAffectsCanRenderFlag(ref effectName, value);
        }
        get
        {
            return effectName;
        }
    }

    private FXAALevel fxaaLevel = FXAALevel.None;
    /// <summary>
    /// Gets or sets the fxaa level.
    /// </summary>
    /// <value>
    /// The fxaa level.
    /// </value>
    public FXAALevel FXAALevel
    {
        set
        {
            SetAffectsCanRenderFlag(ref fxaaLevel, value);
        }
        get
        {
            return fxaaLevel;
        }
    }

    private int textureSlot;
    private int samplerSlot;
    private SamplerStateProxy? sampler;
    private ShaderPass? FXAAPass;
    private ShaderPass? LUMAPass;
    private readonly ConstantBufferComponent modelCB;
    private BorderEffectStruct modelStruct;

    public PostEffectFXAA() : base(RenderType.GlobalEffect)
    {
        modelCB = AddComponent(new ConstantBufferComponent(new ConstantBufferDescription(DefaultBufferNames.BorderEffectCB, BorderEffectStruct.SizeInBytes)));
    }

    protected override bool OnAttach(IRenderTechnique? technique)
    {
        FXAAPass = technique?[DefaultPassNames.FXAAPass];
        LUMAPass = technique?[DefaultPassNames.LumaPass];
        textureSlot = FXAAPass?.PixelShader.ShaderResourceViewMapping.TryGetBindSlot(DefaultBufferNames.DiffuseMapTB) ?? 0;
        samplerSlot = FXAAPass?.PixelShader.SamplerMapping.TryGetBindSlot(DefaultSamplerStateNames.SurfaceSampler) ?? 0;
        sampler = technique?.EffectsManager?.StateManager?.Register(DefaultSamplers.LinearSamplerClampAni1);
        return true;
    }

    protected override void OnDetach()
    {
        RemoveAndDispose(ref sampler);
    }

    protected override bool OnUpdateCanRenderFlag()
    {
        return IsAttached && !string.IsNullOrEmpty(EffectName) && FXAALevel != FXAALevel.None;
    }

    public override void Render(RenderContext context, DeviceContextProxy deviceContext)
    {
        var buffer = context.RenderHost.RenderBuffer;
        deviceContext.SetRenderTarget(buffer?.FullResPPBuffer?.NextRTV);
        var viewport = context.Viewport;
        deviceContext.SetViewport(ref viewport);
        deviceContext.SetScissorRectangle(ref viewport);
        OnUpdatePerModelStruct(context);
        modelCB.Upload(deviceContext, ref modelStruct);
        LUMAPass?.BindShader(deviceContext);
        LUMAPass?.BindStates(deviceContext, StateType.All);
        LUMAPass?.PixelShader.BindTexture(deviceContext, textureSlot, buffer?.FullResPPBuffer?.CurrentSRV);
        LUMAPass?.PixelShader.BindSampler(deviceContext, samplerSlot, sampler);
        deviceContext.Draw(4, 0);

        deviceContext.SetRenderTarget(buffer?.FullResPPBuffer?.CurrentRTV);
        FXAAPass?.BindShader(deviceContext);
        FXAAPass?.PixelShader.BindTexture(deviceContext, textureSlot, buffer?.FullResPPBuffer?.NextSRV);
        deviceContext.Draw(4, 0);
        FXAAPass?.PixelShader.BindTexture(deviceContext, textureSlot, null);
    }

    private void OnUpdatePerModelStruct(RenderContext context)
    {
        modelStruct.Color.Red = (float)(1 / context.ActualWidth);
        modelStruct.Color.Green = (float)(1 / context.ActualHeight);
        switch (FXAALevel)
        {
            case FXAALevel.Low:
                modelStruct.Param.M11 = 0.25f; //fxaaQualitySubpix
                modelStruct.Param.M12 = 0.250f; // FxaaFloat fxaaQualityEdgeThreshold,
                modelStruct.Param.M13 = 0.0833f; // FxaaFloat fxaaQualityEdgeThresholdMin,
                break;
            case FXAALevel.Medium:
                modelStruct.Param.M11 = 0.50f;
                modelStruct.Param.M12 = 0.166f;
                modelStruct.Param.M13 = 0.0625f;
                break;
            case FXAALevel.High:
                modelStruct.Param.M11 = 0.75f;
                modelStruct.Param.M12 = 0.125f;
                modelStruct.Param.M13 = 0.0625f;
                break;
            case FXAALevel.Ultra:
                modelStruct.Param.M11 = 1.00f;
                modelStruct.Param.M12 = 0.063f;
                modelStruct.Param.M13 = 0.0312f;
                break;
        }
    }
}
