﻿using HelixToolkit.Geometry;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX.Utilities;
using SharpDX;
using System.ComponentModel;
using System.Windows;
using MatrixTransform3D = System.Windows.Media.Media3D.MatrixTransform3D;

namespace HelixToolkit.Wpf.SharpDX;

/// <summary>
///   A translate manipulator.
/// </summary>
public class UIRotateManipulator3D : UIManipulator3D
{
    /// <summary>
    /// The axis property.
    /// </summary>
    public static readonly DependencyProperty AxisProperty = DependencyProperty.Register(
        "Axis", typeof(Vector3), typeof(UIRotateManipulator3D), new PropertyMetadata(new Vector3(0, 0, 1), ModelChanged));

    /// <summary>
    /// The diameter property.
    /// </summary>
    public static readonly DependencyProperty OuterDiameterProperty = DependencyProperty.Register(
        "OuterDiameter", typeof(double), typeof(UIRotateManipulator3D), new PropertyMetadata(1.5, ModelChanged));

    /// <summary>
    /// The inner diameter property.
    /// </summary>
    public static readonly DependencyProperty InnerDiameterProperty = DependencyProperty.Register(
        "InnerDiameter", typeof(double), typeof(UIRotateManipulator3D), new PropertyMetadata(1.0, ModelChanged));

    /// <summary>
    /// The length property.
    /// </summary>
    public static readonly DependencyProperty LengthProperty = DependencyProperty.Register(
        "Length", typeof(double), typeof(UIRotateManipulator3D), new PropertyMetadata(0.1, ModelChanged));

    /// <summary>
    /// The pivot point property.
    /// </summary>
    public static readonly DependencyProperty PivotProperty = DependencyProperty.Register(
        "Pivot", typeof(Vector3), typeof(UIRotateManipulator3D), new PropertyMetadata(new Vector3(0, 0, 0)));

    /// <summary>
    /// Gets or sets the rotation axis.
    /// </summary>
    /// <value>The axis.</value>
    [TypeConverter(typeof(Vector3Converter))]
    public Vector3 Axis
    {
        get
        {
            return (Vector3)this.GetValue(AxisProperty);
        }
        set
        {
            this.SetValue(AxisProperty, value);
        }
    }

    /// <summary>
    /// Gets or sets the diameter of the manipulator arrow.
    /// </summary>
    /// <value> The diameter. </value>
    public double OuterDiameter
    {
        get
        {
            return (double)this.GetValue(OuterDiameterProperty);
        }
        set
        {
            this.SetValue(OuterDiameterProperty, value);
        }
    }

    /// <summary>
    /// Gets or sets the inner diameter.
    /// </summary>
    /// <value>The inner diameter.</value>
    public double InnerDiameter
    {
        get
        {
            return (double)this.GetValue(InnerDiameterProperty);
        }
        set
        {
            this.SetValue(InnerDiameterProperty, value);
        }
    }

    /// <summary>
    /// Gets or sets the length of the cylinder.
    /// </summary>
    /// <value>The length.</value>
    public double Length
    {
        get
        {
            return (double)this.GetValue(LengthProperty);
        }
        set
        {
            this.SetValue(LengthProperty, value);
        }
    }

    /// <summary>
    /// Gets or sets the pivot point of the manipulator.
    /// </summary>
    /// <value> The position. </value>
    [TypeConverter(typeof(Vector3Converter))]
    public Vector3 Pivot
    {
        get
        {
            return (Vector3)this.GetValue(PivotProperty);
        }
        set
        {
            this.SetValue(PivotProperty, value);
        }
    }

    /// <summary>
    ///   Initializes a new instance of the <see cref="UIManipulator3D" /> class.
    /// </summary>
    public UIRotateManipulator3D()
    {
        this.Transform = new System.Windows.Media.Media3D.RotateTransform3D();
    }

    /// <summary>
    /// Called when geometry has been changed.
    /// </summary>
    protected override void OnModelChanged()
    {
        var mb = new MeshBuilder();
        var p0 = this.Offset; //new Vector3(0, 0, 0);
        if (this.InnerDiameter >= this.OuterDiameter)
            this.OuterDiameter = this.InnerDiameter + 0.3;

        var d = Vector3.Normalize(this.Axis);
        var p1 = p0 - (d * (float)this.Length * 0.5f);
        var p2 = p0 + (d * (float)this.Length * 0.5f);
        mb.AddPipe(p1, p2, (float)this.InnerDiameter, (float)this.OuterDiameter, 64);
        this.Geometry = mb.ToMeshGeometry3D();
    }

    /// <summary>
    /// 
    /// </summary>        
    protected override void UpdateManipulator(RoutedEventArgs e)
    {
        if (!this.isMouseCaptured)
            return;

        if (e is not Mouse3DEventArgs args || this.viewport is null)
        {
            return;
        }

        // --- get the plane for translation (camera normal is a good choice)                     
        var normal = this.cameraNormal;
        var position = this.TotalModelMatrix.Translation;

        // --- hit position 
        if (this.viewport.UnProjectOnPlane(args.Position.ToVector2(), lastHitPosWS, normal, out var newHitPos))
        {
            var v = Vector3.Normalize(this.lastHitPosWS - position);
            var u = Vector3.Normalize(newHitPos - position);

            var currentAxis = Vector3.Cross(u, v);
            var mainAxis = ToWorldVec(this.Axis);// this.Transform.Transform(this.Axis.ToVector3D()).ToVector3();
            double sign = -Vector3.Dot(mainAxis, currentAxis);
            var theta = Math.Sign(sign) * Math.Asin(currentAxis.Length()) / Math.PI * 180;
            this.Value += theta;

            var rotateTransform = new System.Windows.Media.Media3D.RotateTransform3D(new System.Windows.Media.Media3D.AxisAngleRotation3D(this.Axis.ToVector3D(), theta), Pivot.ToPoint3D());

            // rotate target
            if (this.TargetTransform != null)
            {
                this.TargetTransform = new MatrixTransform3D(rotateTransform.AppendTransform(this.TargetTransform).Value);
            }
            else
            {
                if (this.Transform == null)
                {
                    this.Transform = rotateTransform;
                }
                else
                {
                    this.Transform = new MatrixTransform3D(rotateTransform.AppendTransform(Transform).Value);
                }
            }
            this.lastHitPosWS = newHitPos;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void OnMouse3DMove(object? sender, RoutedEventArgs e)
    {
        if (IsHitTestVisible)
        {
            base.OnMouse3DMove(sender, e);
        }
    }
}
