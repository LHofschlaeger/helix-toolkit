﻿using HelixToolkit.SharpDX.Core.Components;
using HelixToolkit.SharpDX.Render;
using HelixToolkit.SharpDX.Shaders;
using HelixToolkit.SharpDX.Utilities;
using SharpDX;
using SharpDX.Direct3D11;

namespace HelixToolkit.SharpDX.Core;

/// <summary>
/// Outline blur effect
/// <para>Must not put in shared model across multiple viewport, otherwise may causes performance issue if each viewport sizes are different.</para>
/// </summary>
public class PostEffectBloomCore : RenderCore, IPostEffectBloom
{
    #region Variables
    private SamplerStateProxy? sampler;
    private ShaderPass? screenQuadPass;

    private ShaderPass? screenQuadCopy;

    private ShaderPass? blurPassVertical;

    private ShaderPass? blurPassHorizontal;

    private ShaderPass? screenOutlinePass;

    private int textureSlot;

    private int samplerSlot;

    private readonly ConstantBufferComponent modelCB;

    private BorderEffectStruct modelStruct;

    private PostEffectBlurCore? blurCore;
    #endregion
    #region Properties   
    private string effectName = DefaultRenderTechniqueNames.PostEffectBloom;

    /// <summary>
    /// Gets or sets the name of the effect.
    /// </summary>
    /// <value>
    /// The name of the effect.
    /// </value>
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

    /// <summary>
    /// Gets or sets the color of the border.
    /// </summary>
    /// <value>
    /// The color of the border.
    /// </value>
    public Color4 ThresholdColor
    {
        set
        {
            SetAffectsRender(ref modelStruct.Color, value);
        }
        get
        {
            return modelStruct.Color;
        }
    }

    public float BloomExtractIntensity
    {
        set
        {
            SetAffectsRender(ref modelStruct.Param.M11, value);
        }
        get
        {
            return modelStruct.Param.M11;
        }
    }

    public float BloomPassIntensity
    {
        set
        {
            SetAffectsRender(ref modelStruct.Param.M12, value);
        }
        get
        {
            return modelStruct.Param.M12;
        }
    }

    public float BloomCombineSaturation
    {
        set
        {
            SetAffectsRender(ref modelStruct.Param.M13, value);
        }
        get
        {
            return modelStruct.Param.M13;
        }
    }

    public float BloomCombineIntensity
    {
        set
        {
            SetAffectsRender(ref modelStruct.Param.M14, value);
        }
        get
        {
            return modelStruct.Param.M14;
        }
    }

    private int numberOfBlurPass = 1;
    /// <summary>
    /// Gets or sets the number of blur pass.
    /// </summary>
    /// <value>
    /// The number of blur pass.
    /// </value>
    public int NumberOfBlurPass
    {
        set
        {
            SetAffectsRender(ref numberOfBlurPass, value);
        }
        get
        {
            return numberOfBlurPass;
        }
    }
    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="PostEffectMeshOutlineBlurCore"/> class.
    /// </summary>
    public PostEffectBloomCore() : base(RenderType.GlobalEffect)
    {
        modelCB = AddComponent(new ConstantBufferComponent(new ConstantBufferDescription(DefaultBufferNames.BorderEffectCB, BorderEffectStruct.SizeInBytes)));
        ThresholdColor = new Color4(0.8f, 0.8f, 0.8f, 0f);
        BloomExtractIntensity = 1f;
        BloomPassIntensity = 0.95f;
        BloomCombineIntensity = 0.7f;
        BloomCombineSaturation = 0.7f;
    }

    protected override bool OnAttach(IRenderTechnique? technique)
    {
        screenQuadPass = technique?.GetPass(DefaultPassNames.ScreenQuad);
        screenQuadCopy = technique?.GetPass(DefaultPassNames.ScreenQuadCopy);
        blurPassVertical = technique?.GetPass(DefaultPassNames.EffectBlurVertical);
        blurPassHorizontal = technique?.GetPass(DefaultPassNames.EffectBlurHorizontal);
        screenOutlinePass = technique?.GetPass(DefaultPassNames.MeshOutline);
        textureSlot = screenOutlinePass?.PixelShader.ShaderResourceViewMapping.TryGetBindSlot(DefaultBufferNames.DiffuseMapTB) ?? 0;
        samplerSlot = screenOutlinePass?.PixelShader.SamplerMapping.TryGetBindSlot(DefaultSamplerStateNames.SurfaceSampler) ?? 0;
        sampler = technique?.EffectsManager?.StateManager?.Register(DefaultSamplers.LinearSamplerClampAni1);
        blurCore = blurPassVertical is null || blurPassHorizontal is null ? null : new PostEffectBlurCore(blurPassVertical, blurPassHorizontal, textureSlot, samplerSlot,
            DefaultSamplers.LinearSamplerClampAni1, technique?.EffectsManager);
        return true;
    }

    protected override bool OnUpdateCanRenderFlag()
    {
        return IsAttached && !string.IsNullOrEmpty(EffectName);
    }

    public override void Render(RenderContext context, DeviceContextProxy deviceContext)
    {
        var buffer = context.RenderHost.RenderBuffer;
        #region Do Bloom Pass
        modelCB.Upload(deviceContext, ref modelStruct);
        //Extract bloom samples
        deviceContext.SetRenderTarget(buffer?.FullResPPBuffer?.NextRTV);

        screenQuadPass?.PixelShader.BindTexture(deviceContext, textureSlot, buffer?.FullResPPBuffer?.CurrentSRV);
        screenQuadPass?.PixelShader.BindSampler(deviceContext, samplerSlot, sampler);
        screenQuadPass?.BindShader(deviceContext);
        screenQuadPass?.BindStates(deviceContext, StateType.All);
        deviceContext.Draw(4, 0);
        var viewport = context.Viewport;
        // Down sampling
        if (blurCore is not null && buffer?.FullResPPBuffer?.NextRTV is not null)
        {
            for (var i = 0; i < numberOfBlurPass; ++i)
            {
                blurCore.Run(context, deviceContext, buffer.FullResPPBuffer.NextRTV, ref viewport,
                    PostEffectBlurCore.BlurDepth.Two, ref modelStruct);
            }
        }

        #endregion

        #region Draw outline onto original target
        if (buffer?.FullResPPBuffer is not null)
        {
            BindTarget(null, buffer.FullResPPBuffer.CurrentRTV, deviceContext, buffer.TargetWidth, buffer.TargetHeight, false);
            screenOutlinePass?.PixelShader.BindTexture(deviceContext, textureSlot, buffer.FullResPPBuffer.NextSRV);
            screenOutlinePass?.BindShader(deviceContext);
            screenOutlinePass?.BindStates(deviceContext, StateType.All);
            deviceContext.Draw(4, 0);
            screenOutlinePass?.PixelShader.BindTexture(deviceContext, textureSlot, null);
        }
        #endregion
    }

    protected override void OnDetach()
    {
        RemoveAndDispose(ref sampler);
        RemoveAndDispose(ref blurCore);
    }

    private static void BindTarget(DepthStencilView? dsv, RenderTargetView? targetView, DeviceContextProxy context, int width, int height, bool clear = true)
    {
        if (clear)
        {
            context.ClearRenderTargetView(targetView, Maths.Color.Transparent);
        }
        context.SetRenderTargets(dsv, new RenderTargetView?[] { targetView });
        context.SetViewport(0, 0, width, height);
        context.SetScissorRectangle(0, 0, width, height);
    }
}
