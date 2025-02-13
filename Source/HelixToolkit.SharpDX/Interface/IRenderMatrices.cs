﻿using SharpDX;

namespace HelixToolkit.SharpDX;

public interface IRenderMatrices
{
    /// <summary>
    /// Gets the view matrix.
    /// </summary>
    /// <value>
    /// The view matrix.
    /// </value>
    Matrix ViewMatrix
    {
        get;
    }
    /// <summary>
    /// Gets the inversed view matrix.
    /// </summary>
    /// <value>
    /// The inversed view matrix.
    /// </value>
    Matrix ViewMatrixInv
    {
        get;
    }

    /// <summary>
    /// Gets the projection matrix.
    /// </summary>
    /// <value>
    /// The projection matrix.
    /// </value>
    Matrix ProjectionMatrix
    {
        get;
    }

    /// <summary>
    /// Gets the viewport matrix.
    /// </summary>
    /// <value>
    /// The viewport matrix.
    /// </value>
    Matrix ViewportMatrix
    {
        get;
    }
    /// <summary>
    /// Gets the screen view projection matrix.
    /// </summary>
    /// <value>
    /// The screen view projection matrix.
    /// </value>
    Matrix ScreenViewProjectionMatrix
    {
        get;
    }
    /// <summary>
    /// Gets a value indicating whether this instance is perspective.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is perspective; otherwise, <c>false</c>.
    /// </value>
    bool IsPerspective
    {
        get;
    }
    /// <summary>
    /// Gets the actual width.
    /// </summary>
    /// <value>
    /// The actual width.
    /// </value>
    float ActualWidth
    {
        get;
    }
    /// <summary>
    /// Gets the actual height.
    /// </summary>
    /// <value>
    /// The actual height.
    /// </value>
    float ActualHeight
    {
        get;
    }
    /// <summary>
    /// Gets the dpi scale.
    /// </summary>
    /// <value>
    /// The dpi scale.
    /// </value>
    float DpiScale
    {
        get;
    }
    /// <summary>
    /// Gets the render host.
    /// </summary>
    /// <value>
    /// The render host.
    /// </value>
    IRenderHost? RenderHost
    {
        get;
    }
    /// <summary>
    /// Gets the bounding frustum.
    /// </summary>
    /// <value>
    /// The bounding frustum.
    /// </value>
    FrustumCameraParams CameraParams
    {
        get;
    }
    /// <summary>
    /// Updates all the internal metrices.
    /// </summary>
    void Update();
}
