﻿using HelixToolkit.Geometry;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HelixToolkit.Geometry;

/// <summary>
/// Provides functionality to calculate a contour slice through a 3 vertex facet.(Modified from HelixToolkit.Wpf version)
/// </summary>
/// <remarks>
/// See <a href="http://paulbourke.net/papers/conrec/">CONREC</a> for further information.
/// </remarks>
public sealed class ContourHelper
{
    /// <summary>
    /// Provides the indices for the various <see cref="ContourFacetResult"/> cases.
    /// </summary>
    private static readonly IDictionary<ContourFacetResult, int[,]> ResultIndices
        = new Dictionary<ContourFacetResult, int[,]>
    {
            { ContourFacetResult.ZeroOnly, new[,] { { 0, 1 }, { 0, 2 } } },
            { ContourFacetResult.OneAndTwo, new[,] { { 0, 2 }, { 0, 1 } } },
            { ContourFacetResult.OneOnly, new[,] { { 1, 2 }, { 1, 0 } } },
            { ContourFacetResult.ZeroAndTwo, new[,] { { 1, 0 }, { 1, 2 } } },
            { ContourFacetResult.TwoOnly, new[,] { { 2, 0 }, { 2, 1 } } },
            { ContourFacetResult.ZeroAndOne, new[,] { { 2, 1 }, { 2, 0 } } },
    };

    /// <summary>
    /// The parameter 'a' of the plane equation.
    /// </summary>
    private readonly float a;

    /// <summary>
    /// The parameter 'b' of the plane equation.
    /// </summary>
    private readonly float b;

    /// <summary>
    /// The parameter 'c' of the plane equation.
    /// </summary>
    private readonly float c;

    /// <summary>
    /// The parameter 'd' of the plane equation.
    /// </summary>
    private readonly float d;

    /// <summary>
    /// The sides.
    /// </summary>
    private readonly float[] sides = new float[3];

    /// <summary>
    /// The indices.
    /// </summary>
    private readonly int[] indices = new int[3];

    /// <summary>
    /// The original mesh positions.
    /// </summary>
    private readonly Vector3[] meshPositions;

    /// <summary>
    /// The original mesh normal vectors.
    /// </summary>
    private readonly Vector3[]? meshNormals;

    /// <summary>
    /// The original mesh texture coordinates.
    /// </summary>
    private readonly Vector2[]? meshTextureCoordinates;

    /// <summary>
    /// The points.
    /// </summary>
    private readonly Vector3[] points = new Vector3[3];

    /// <summary>
    /// The normal vectors.
    /// </summary>
    private readonly Vector3[]? normals;

    /// <summary>
    /// The texture coordinates.
    /// </summary>
    private readonly Vector2[]? textures;

    /// <summary>
    /// The position count.
    /// </summary>
    private int positionCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContourHelper" /> class.
    /// </summary>
    /// <param name="planeOrigin">The plane origin.</param>
    /// <param name="planeNormal">The plane normal.</param>
    /// <param name="originalMesh">The original mesh.</param>
    public ContourHelper(Vector3 planeOrigin, Vector3 planeNormal, MeshGeometry3D originalMesh)
    {
        var hasNormals = originalMesh.Normals != null && originalMesh.Normals.Count > 0;
        var hasTextureCoordinates = originalMesh.TextureCoordinates != null && originalMesh.TextureCoordinates.Count > 0;
        this.normals = hasNormals ? new Vector3[3] : null;
        this.textures = hasTextureCoordinates ? new Vector2[3] : null;
        this.positionCount = originalMesh.Positions.Count;

        this.meshPositions = originalMesh.Positions.ToArray();
        this.meshNormals = hasNormals ? originalMesh.Normals?.ToArray() : null;
        this.meshTextureCoordinates = hasTextureCoordinates ? originalMesh.TextureCoordinates?.ToArray() : null;

        // Determine the equation of the plane as
        // ax + by + cz + d = 0
        float l = (planeNormal.X * planeNormal.X) + (planeNormal.Y * planeNormal.Y) + (planeNormal.Z * planeNormal.Z);
#if NET6_0_OR_GREATER
        l = MathF.Sqrt(l);
#else
        l = (float)Math.Sqrt(l);
#endif
        this.a = planeNormal.X / l;
        this.b = planeNormal.Y / l;
        this.c = planeNormal.Z / l;
        this.d = -((planeNormal.X * planeOrigin.X) + (planeNormal.Y * planeOrigin.Y) + (planeNormal.Z * planeOrigin.Z));
    }

    /// <summary>
    /// The contour facet result.
    /// </summary>
    private enum ContourFacetResult
    {
        /// <summary>
        /// All of the points fall above the contour plane.
        /// </summary>
        None,

        /// <summary>
        /// Only the 0th point falls below the contour plane.
        /// </summary>
        ZeroOnly,

        /// <summary>
        /// The 1st and 2nd points fall below the contour plane.
        /// </summary>
        OneAndTwo,

        /// <summary>
        /// Only the 1st point falls below the contour plane.
        /// </summary>
        OneOnly,

        /// <summary>
        /// The 0th and 2nd points fall below the contour plane.
        /// </summary>
        ZeroAndTwo,

        /// <summary>
        /// Only the second point falls below the contour plane.
        /// </summary>
        TwoOnly,

        /// <summary>
        /// The 0th and 1st points fall below the contour plane.
        /// </summary>
        ZeroAndOne,

        /// <summary>
        /// All of the points fall below the contour plane.
        /// </summary>
        All
    }

    /// <summary>
    /// Create a contour slice through a 3 vertex facet.
    /// </summary>
    /// <param name="index0">The 0th point index.</param>
    /// <param name="index1">The 1st point index.</param>
    /// <param name="index2">The 2nd point index.</param>
    /// <param name="newPositions">Any new positions that are created, when the contour plane slices through the vertex.</param>
    /// <param name="newNormals">Any new normal vectors that are created, when the contour plane slices through the vertex.</param>
    /// <param name="newTextureCoordinates">Any new texture coordinates that are created, when the contour plane slices through the vertex.</param>
    /// <param name="triangleIndices">Triangle indices for the triangle(s) above the plane.</param>
    public void ContourFacet(
        int index0,
        int index1,
        int index2,
        out Vector3[] newPositions,
        out Vector3[] newNormals,
        out Vector2[] newTextureCoordinates,
        out int[] triangleIndices)
    {
        this.SetData(index0, index1, index2);

        var facetResult = this.GetContourFacet();

        switch (facetResult)
        {
            case ContourFacetResult.ZeroOnly:
                triangleIndices = new[] { index0, this.positionCount++, this.positionCount++ };
                break;
            case ContourFacetResult.OneAndTwo:
                triangleIndices = new[] { index1, index2, this.positionCount, this.positionCount++, this.positionCount++, index1 };
                break;
            case ContourFacetResult.OneOnly:
                triangleIndices = new[] { index1, this.positionCount++, this.positionCount++ };
                break;
            case ContourFacetResult.ZeroAndTwo:
                triangleIndices = new[] { index2, index0, this.positionCount, this.positionCount++, this.positionCount++, index2 };
                break;
            case ContourFacetResult.TwoOnly:
                triangleIndices = new[] { index2, this.positionCount++, this.positionCount++ };
                break;
            case ContourFacetResult.ZeroAndOne:
                triangleIndices = new[] { index0, index1, this.positionCount, this.positionCount++, this.positionCount++, index0 };
                break;
            case ContourFacetResult.All:
                newPositions = Array.Empty<Vector3>();
                newNormals = Array.Empty<Vector3>();
                newTextureCoordinates = Array.Empty<Vector2>();
                triangleIndices = new[] { index0, index1, index2 };
                return;
            default:
                newPositions = Array.Empty<Vector3>();
                newNormals = Array.Empty<Vector3>();
                newTextureCoordinates = Array.Empty<Vector2>();
                triangleIndices = Array.Empty<int>();
                return;
        }

        var facetIndices = ResultIndices[facetResult];
        newPositions = new[]
        {
                this.CreateNewPosition(facetIndices[0, 0], facetIndices[0, 1]),
                this.CreateNewPosition(facetIndices[1, 0], facetIndices[1, 1])
            };

        if (this.normals != null)
        {
            newNormals = new[]
        {
                this.CreateNewNormal(facetIndices[0, 0], facetIndices[0, 1]),
                this.CreateNewNormal(facetIndices[1, 0], facetIndices[1, 1])
            };
        }
        else
        {
            newNormals = Array.Empty<Vector3>();
        }

        if (this.textures != null)
        {
            newTextureCoordinates = new[]
            {
                    this.CreateNewTexture(facetIndices[0, 0], facetIndices[0, 1]),
                    this.CreateNewTexture(facetIndices[1, 0], facetIndices[1, 1])
                };
        }
        else
        {
            newTextureCoordinates = Array.Empty<Vector2>();
        }
    }

    /// <summary>
    /// Calculates a new point coordinate.
    /// </summary>
    /// <param name="firstPoint">
    /// The first point coordinate.
    /// </param>
    /// <param name="secondPoint">
    /// The second point coordinate.
    /// </param>
    /// <param name="firstSide">
    /// The first side.
    /// </param>
    /// <param name="secondSide">
    /// The second side.
    /// </param>
    /// <returns>The new coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculatePoint(float firstPoint, float secondPoint, float firstSide, float secondSide)
    {
        return firstPoint - (firstSide * (secondPoint - firstPoint) / (secondSide - firstSide));
    }

    /// <summary>
    /// Gets the <see cref="ContourFacetResult"/> for the current facet.
    /// </summary>
    /// <returns>a facet result.</returns>
    private ContourFacetResult GetContourFacet()
    {
        if (this.IsSideAlone(0))
        {
            return this.sides[0] > 0 ? ContourFacetResult.ZeroOnly : ContourFacetResult.OneAndTwo;
        }

        if (this.IsSideAlone(1))
        {
            return this.sides[1] > 0 ? ContourFacetResult.OneOnly : ContourFacetResult.ZeroAndTwo;
        }

        if (this.IsSideAlone(2))
        {
            return this.sides[2] > 0 ? ContourFacetResult.TwoOnly : ContourFacetResult.ZeroAndOne;
        }

        if (this.AllSidesBelowContour())
        {
            return ContourFacetResult.All;
        }

        return ContourFacetResult.None;
    }

    /// <summary>
    /// Initializes the facet data and calculates the <see cref="sides"/> values from the specified triangle indices. 
    /// </summary>
    /// <param name="index0">The first triangle index of the facet.</param>
    /// <param name="index1">The second triangle index of the facet.</param>
    /// <param name="index2">The third triangle index of the facet.</param>
    private void SetData(int index0, int index1, int index2)
    {
        this.indices[0] = index0;
        this.indices[1] = index1;
        this.indices[2] = index2;

        this.points[0] = this.meshPositions[index0];
        this.points[1] = this.meshPositions[index1];
        this.points[2] = this.meshPositions[index2];

        if (this.normals != null)
        {
            this.normals[0] = this.meshNormals?[index0] ?? Vector3.Zero;
            this.normals[1] = this.meshNormals?[index1] ?? Vector3.Zero;
            this.normals[2] = this.meshNormals?[index2] ?? Vector3.Zero;
        }

        if (this.textures != null)
        {
            this.textures[0] = this.meshTextureCoordinates?[index0] ?? Vector2.Zero;
            this.textures[1] = this.meshTextureCoordinates?[index1] ?? Vector2.Zero;
            this.textures[2] = this.meshTextureCoordinates?[index2] ?? Vector2.Zero;
        }

        this.sides[0] = (this.a * this.points[0].X) + (this.b * this.points[0].Y) + (this.c * this.points[0].Z) + this.d;
        this.sides[1] = (this.a * this.points[1].X) + (this.b * this.points[1].Y) + (this.c * this.points[1].Z) + this.d;
        this.sides[2] = (this.a * this.points[2].X) + (this.b * this.points[2].Y) + (this.c * this.points[2].Z) + this.d;
    }

    /// <summary>
    /// Calculates the position at the plane intersection for the side specified by two triangle indices.
    /// </summary>
    /// <param name="index0">The first index.</param>
    /// <param name="index1">The second index.</param>
    /// <returns>The interpolated position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 CreateNewPosition(int index0, int index1)
    {
        var firstPoint = this.points[index0];
        var secondPoint = this.points[index1];
        var firstSide = this.sides[index0];
        var secondSide = this.sides[index1];

        return new Vector3(
            CalculatePoint(firstPoint.X, secondPoint.X, firstSide, secondSide),
            CalculatePoint(firstPoint.Y, secondPoint.Y, firstSide, secondSide),
            CalculatePoint(firstPoint.Z, secondPoint.Z, firstSide, secondSide));
    }

    /// <summary>
    /// Calculates the normal at the plane intersection for the side specified by two triangle indices.
    /// </summary>
    /// <param name="index0">The first index.</param>
    /// <param name="index1">The second index.</param>
    /// <returns>The interpolated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector3 CreateNewNormal(int index0, int index1)
    {
        var firstPoint = this.normals?[index0] ?? Vector3.Zero;
        var secondPoint = this.normals?[index1] ?? Vector3.Zero;
        var firstSide = this.sides[index0];
        var secondSide = this.sides[index1];

        return new Vector3(
            CalculatePoint(firstPoint.X, secondPoint.X, firstSide, secondSide),
            CalculatePoint(firstPoint.Y, secondPoint.Y, firstSide, secondSide),
            CalculatePoint(firstPoint.Z, secondPoint.Z, firstSide, secondSide));
    }

    /// <summary>
    /// Calculates the texture coordinate at the plane intersection for the side specified by two triangle indices.
    /// </summary>
    /// <param name="index0">The first index.</param>
    /// <param name="index1">The second index.</param>
    /// <returns>The interpolated texture coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector2 CreateNewTexture(int index0, int index1)
    {
        var firstTexture = this.textures?[index0] ?? Vector2.Zero;
        var secondTexture = this.textures?[index1] ?? Vector2.Zero;
        var firstSide = this.sides[index0];
        var secondSide = this.sides[index1];

        return new Vector2(
            CalculatePoint(firstTexture.X, secondTexture.X, firstSide, secondSide),
            CalculatePoint(firstTexture.Y, secondTexture.Y, firstSide, secondSide));
    }

    /// <summary>
    /// Determines whether the vertex at the specified index is at the opposite side of the other two vertices.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns><c>true</c> if the vertex is on its own side.</returns>
    private bool IsSideAlone(int index)
    {
        static int getNext(int i) => i + 1 > 2 ? 0 : i + 1;

        var firstSideIndex = getNext(index);
        var secondSideIndex = getNext(firstSideIndex);

        return this.sides[index] * this.sides[firstSideIndex] < 0
            && this.sides[index] * this.sides[secondSideIndex] < 0;
    }

    /// <summary>
    /// Determines whether all sides of the facet are below the contour.
    /// </summary>
    /// <returns><c>true</c> if all sides are below the contour.</returns>
    private bool AllSidesBelowContour()
    {
        return this.sides[0] >= 0
            && this.sides[1] >= 0
            && this.sides[2] >= 0;
    }
}
