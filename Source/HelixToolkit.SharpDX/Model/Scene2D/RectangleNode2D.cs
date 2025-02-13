﻿using HelixToolkit.SharpDX.Core2D;
using SharpDX;

namespace HelixToolkit.SharpDX.Model.Scene2D;

public class RectangleNode2D : ShapeNode2D
{
    protected override ShapeRenderCore2DBase CreateShapeRenderCore()
    {
        return new RectangleRenderCore2D();
    }

    protected override bool OnHitTest(ref Vector2 mousePoint, out HitTest2DResult? hitResult)
    {
        hitResult = null;
        if (LayoutBoundWithTransform.Contains(mousePoint))
        {
            hitResult = new HitTest2DResult(WrapperSource);
            return true;
        }
        else
        {
            return false;
        }
    }
}
