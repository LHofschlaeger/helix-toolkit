﻿using HelixToolkit.SharpDX.Render;
using HelixToolkit.SharpDX.Utilities;
using SharpDX;
using SharpDX.Direct3D11;
using System.Runtime.CompilerServices;

namespace HelixToolkit.SharpDX.Model;

/// <summary>
/// Default Light Model
/// </summary>
public sealed class LightsBufferModel : ILightsBufferProxy<LightStruct>
{
    public const int SizeInBytes = LightStruct.SizeInBytes * Constants.MaxLights + 4 * (4 * 2);

    private readonly LightStruct[] lights = new LightStruct[Constants.MaxLights];
    public Color4 AmbientLight { set; get; } = new(0, 0, 0, 1);
    public int LightCount { private set; get; } = 0;
    /// <summary>
    /// Gets or sets a value indicating whether the scene has environment map.
    /// </summary>
    /// <value>
    ///   <c>true</c> if the scene has environment map; otherwise, <c>false</c>.
    /// </value>
    internal bool HasEnvironmentMap = false;
    /// <summary>
    /// Gets or sets the environment map mip levels.
    /// </summary>
    /// <value>
    /// The environment map mip levels.
    /// </value>
    internal int EnvironmentMapMipLevels = 0;

    public int BufferSize
    {
        get
        {
            return SizeInBytes;
        }
    }

    public LightStruct[] Lights
    {
        get
        {
            return lights;
        }
    }

    public void IncrementLightCount()
    {
        ++LightCount;
    }

    public void ResetLightCount()
    {
        LightCount = 0;
        AmbientLight = new Color4(0, 0, 0, 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UploadToBuffer(IBufferProxy? buffer, DeviceContextProxy? context)
    {
        if (context is null || buffer is null)
        {
            return;
        }

        if (buffer.StructureSize == SizeInBytes)
        {
            var dataBox = context.MapSubresource(buffer.Buffer, 0, MapMode.WriteDiscard, MapFlags.None);
            if (dataBox is null || dataBox.Value.IsEmpty)
            {
                return;
            }
            var ptr = UnsafeHelper.Write(dataBox.Value.DataPointer, Lights, 0, Lights.Length);
            ptr = UnsafeHelper.Write(ptr, AmbientLight);
            ptr = UnsafeHelper.Write(ptr, LightCount);
            ptr = UnsafeHelper.Write(ptr, HasEnvironmentMap ? 1 : 0);
            ptr = UnsafeHelper.Write(ptr, EnvironmentMapMipLevels);
            context.UnmapSubresource(buffer.Buffer, 0);
        }
        else
        {
#if DEBUG
            throw new ArgumentException("Buffer type or size do not match the model requirement");
#endif
        }
    }
}
