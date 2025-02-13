﻿using Microsoft.Extensions.Logging;
using SharpDX;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HelixToolkit.SharpDX.Utilities;

/// <summary>
/// Base class for array based static octree.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class StaticOctree<T> : IOctreeBasic where T : unmanaged
{
    private static readonly ILogger logger = Logger.LogManager.Create<StaticOctree<T>>();
    public const int OctantSize = 8;

    /// <summary>
    /// Octant structure, size = 80 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    protected struct Octant
    {
        public readonly BoundingBox Bound;
        private int c0, c1, c2, c3, c4, c5, c6, c7;
        public readonly int Parent;
        public readonly int Index;
        public int Start
        {
            set; get;
        }
        public int End
        {
            set; get;
        }
        public bool IsBuilt
        {
            set; get;
        }
        public byte ActiveNode
        {
            private set; get;
        }
        public bool HasChildren
        {
            get
            {
                return ActiveNode != 0;
            }
        }
        public bool IsEmpty
        {
            get
            {
                return Count == 0;
            }
        }
        public int Count
        {
            get
            {
                return End - Start;
            }
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Octant"/> struct.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="index">The index.</param>
        /// <param name="bound">The bound.</param>
        public Octant(int parent, int index, ref BoundingBox bound)
        {
            Parent = parent;
            Index = index;
            Bound = bound;
            Start = End = 0;
            ActiveNode = 0;
            IsBuilt = false;
            c0 = c1 = c2 = c3 = c4 = c5 = c6 = c7 = -1;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Octant"/> struct.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="index">The index.</param>
        public Octant(int parent, int index)
        {
            Parent = parent;
            Index = index;
            Bound = new BoundingBox();
            Start = End = 0;
            ActiveNode = 0;
            IsBuilt = false;
            c0 = c1 = c2 = c3 = c4 = c5 = c6 = c7 = -1;
        }
        /// <summary>
        /// Gets the index of the child.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetChildIndex(int index)
        {
            return index switch
            {
                0 => c0,
                1 => c1,
                2 => c2,
                3 => c3,
                4 => c4,
                5 => c5,
                6 => c6,
                7 => c7,
                _ => -1,
            };
        }
        /// <summary>
        /// Sets the index of the child.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetChildIndex(int index, int value)
        {
            switch (index)
            {
                case 0:
                    c0 = value;
                    break;
                case 1:
                    c1 = value;
                    break;
                case 2:
                    c2 = value;
                    break;
                case 3:
                    c3 = value;
                    break;
                case 4:
                    c4 = value;
                    break;
                case 5:
                    c5 = value;
                    break;
                case 6:
                    c6 = value;
                    break;
                case 7:
                    c7 = value;
                    break;
            }
            if (value >= 0)
            {
                ActiveNode |= (byte)(1 << index);
            }
        }
        /// <summary>
        /// Gets or sets the <see cref="System.Int32"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="System.Int32"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        public int this[int index]
        {
            get
            {
                return GetChildIndex(index);
            }
            set
            {
                SetChildIndex(index, value);
            }
        }
        /// <summary>
        /// Determines whether [has child at index] [the specified index].
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>
        ///   <c>true</c> if [has child at index] [the specified index]; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasChildAtIndex(int index)
        {
            return (ActiveNode & (byte)(1 << index)) != 0;
        }
    }
    /// <summary>
    /// Octant array, used to manage a internal octant array, which is the storage for the entire octree
    /// </summary>
    protected sealed class OctantArray
    {
        internal Octant[] array = new Octant[128];
        public int Count
        {
            private set; get;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="OctantArray"/> class.
        /// </summary>
        /// <param name="bound">The bound.</param>
        /// <param name="length">The length.</param>
        public OctantArray(BoundingBox bound, int length)
        {
            var octant = new Octant(-1, 0, ref bound)
            {
                Start = 0,
                End = length
            };
            array[0] = octant;
            ++Count;
            //var size = System.Runtime.InteropServices.Marshal.SizeOf(octant);
        }
        /// <summary>
        /// Adds the specified parent index.
        /// </summary>
        /// <param name="parentIndex">Index of the parent.</param>
        /// <param name="childIndex">Index of the child.</param>
        /// <param name="bound">The bound.</param>
        /// <param name="newParent">The parent out.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int parentIndex, int childIndex, BoundingBox bound, ref Octant newParent)
        {
            if (array.Length < Count + OctantSize)
            {
                var newSize = array.Length * 2;
                if (newSize > int.MaxValue / 4) //Size is too big
                {
                    return false;
                }
                var newArray = new Octant[array.Length * 2];
                Array.Copy(array, newArray, Count);
                array = newArray;
            }
            ref var parent = ref array[parentIndex];

            array[Count] = new Octant(parent.Index, Count, ref bound);
            parent[childIndex] = Count;
            ++Count;
            newParent = parent;
            return true;
        }
        /// <summary>
        /// Compacts the octree array, remove all unused storage space at the end of the array.
        /// </summary>
        public void Compact()
        {
            if (array.Length > Count)
            {
                var newArray = new Octant[Count];
                Array.Copy(array, newArray, Count);
                array = newArray;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Octant Get(int i)
        {
            return ref array[i];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Octant this[int index]
        {
            get
            {
                return array[index];
            }
            set
            {
                array[index] = value;
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    public event EventHandler<EventArgs>? Hit;

    private OctantArray? octants;

    private readonly List<BoundingBox> hitPathBoundingBoxes = new();
    /// <summary>
    /// Internal octant array size.
    /// </summary>
    public int OctantArraySize
    {
        get
        {
            return octants != null ? octants.Count : 0;
        }
    }
    /// <summary>
    ///
    /// </summary>
    public IList<BoundingBox> HitPathBoundingBoxes
    {
        get
        {
            return hitPathBoundingBoxes.AsReadOnly();
        }
    }

    private static readonly ObjectPool<Stack<KeyValuePair<int, int>>> hitStackPool
        = new(() => { return new Stack<KeyValuePair<int, int>>(); }, 10);

    /// <summary>
    /// The minumum size for enclosing region is a 1x1x1 cube.
    /// </summary>
    public float MIN_SIZE
    {
        get
        {
            return Parameter.MinimumOctantSize;
        }
    }

    public OctreeBuildParameter Parameter
    {
        private set; get;
    }

    /// <summary>
    ///
    /// </summary>
    protected T[]? Objects
    {
        private set; get;
    }

    /// <summary>
    ///
    /// </summary>
    public bool TreeBuilt { private set; get; } = false;

    /// <summary>
    ///
    /// </summary>
    public BoundingBox Bound
    {
        private set; get;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="parameter"></param>
    public StaticOctree(OctreeBuildParameter parameter)
    {
        Parameter = parameter;
    }

    /// <summary>
    /// Call to build the tree
    /// </summary>
    public void BuildTree()
    {
        if (TreeBuilt)
        {
            return;
        }
#if DEBUG
        var tick = Stopwatch.GetTimestamp();
#endif
        Objects = GetObjects();
        octants = new OctantArray(GetMaxBound(), Objects.Length);
        TreeTraversal(new Stack<KeyValuePair<int, int>>(), BuildSubTree, null);
        octants.Compact();
        TreeBuilt = true;
        Bound = octants[0].Bound;
#if DEBUG
        tick = Stopwatch.GetTimestamp() - tick;
        logger.LogDebug("Build static tree time = {0}; Total = {1}", (double)tick / Stopwatch.Frequency * 1000, octants.Count);
#endif
    }

    protected abstract T[] GetObjects();
    /// <summary>
    /// Get the max bounding box of the octree
    /// </summary>
    /// <returns></returns>
    protected abstract BoundingBox GetMaxBound();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CheckDimension(ref BoundingBox bound)
    {
        var dimensions = bound.Maximum - bound.Minimum;

        if (dimensions == Vector3.Zero)
        {
            return false;
        }
        dimensions = bound.Maximum - bound.Minimum;
        //Check to see if the dimensions of the box are greater than the minimum dimensions
        if (dimensions.X < MIN_SIZE && dimensions.Y < MIN_SIZE && dimensions.Z < MIN_SIZE)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    /// <summary>
    /// Build sub tree nodes
    /// </summary>
    protected void BuildSubTree(int index)
    {
        var octant = octants![index];
        if (octant.IsBuilt)
        {
            return;
        }
        var b = octant.Bound;
        if (CheckDimension(ref b) && !octant.IsEmpty
            && octant.Count > this.Parameter.MinObjectSizeToSplit)
        {
            var octantBounds = CreateOctants(ref b, Parameter.MinimumOctantSize);
            if (octantBounds.Length == OctantSize)
            {
                var start = octant.Start;
                var childOctant = new Octant();
                for (var childOctantIdx = 0; childOctantIdx < OctantSize; ++childOctantIdx)
                {
                    var count = 0;
                    var end = octant.End;
                    var hasChildOctant = false;
                    var childIdx = -1;

                    for (var i = end - 1; i >= start; --i)
                    {
                        var obj = Objects![i];
                        if (IsContains(ref octantBounds[childOctantIdx], GetBoundingBoxFromItem(ref obj), ref obj))
                        {
                            if (!hasChildOctant)//Add New Child Octant if not having one.
                            {
                                if (!octants.Add(index, childOctantIdx, octantBounds[childOctantIdx], ref octant))
                                {
                                    logger.LogDebug("Failed to add child");
                                    break;
                                }
                                childIdx = octant[childOctantIdx];
                                childOctant = octants[childIdx];
                                hasChildOctant = true;
                            }
                            ++count;
                            childOctant.End = end;
                            var s = end - count;
                            childOctant.Start = s;
                            //swap objects. Move object into parent octant start/end range
                            //Move object into child octant start/end range
                            (Objects[s], Objects[i]) = (Objects[i], Objects[s]);
                        }
                    }

                    if (hasChildOctant)
                    {
                        octants[childIdx] = childOctant;
                    }
                    octant.End = end - count;
                }
            }
        }

        octant.IsBuilt = true;
        octants[index] = octant;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    protected abstract BoundingBox GetBoundingBoxFromItem(ref T item);

    /// <summary>
    /// This finds the dimensions of the bounding box necessary to tightly enclose all items in the object list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected BoundingBox FindEnclosingBox(int index)
    {
        ref var octant = ref octants!.array[index];
        if (octant.Count == 0)
        {
            return new BoundingBox();
        }
        var b = GetBoundingBoxFromItem(ref Objects![octant.Start]);
        for (var i = octant.Start + 1; i < octant.End; ++i)
        {
            var bound = GetBoundingBoxFromItem(ref Objects![i]);
            BoundingBox.Merge(ref b, ref bound, out b);
        }
        return b;
    }

    /// <summary>
    /// Create child octant bounding boxes for current parent bounding box
    /// </summary>
    /// <param name="box"></param>
    /// <param name="minSize"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BoundingBox[] CreateOctants(ref BoundingBox box, float minSize)
    {
        var dimensions = box.Maximum - box.Minimum;
        if (dimensions == Vector3.Zero || (dimensions.X < minSize || dimensions.Y < minSize || dimensions.Z < minSize))
        {
            return Array.Empty<BoundingBox>();
        }
        var half = dimensions / 2.0f;
        var center = box.Minimum + half;
        var minimum = box.Minimum;
        var maximum = box.Maximum;
        //Create subdivided regions for each octant
        return new BoundingBox[] {
                    new BoundingBox(minimum, center),
                    new BoundingBox(new Vector3(center.X, minimum.Y, minimum.Z), new Vector3(maximum.X, center.Y, center.Z)),
                    new BoundingBox(new Vector3(center.X, minimum.Y, center.Z), new Vector3(maximum.X, center.Y, maximum.Z)),
                    new BoundingBox(new Vector3(minimum.X, minimum.Y, center.Z), new Vector3(center.X, center.Y, maximum.Z)),
                    new BoundingBox(new Vector3(minimum.X, center.Y, minimum.Z), new Vector3(center.X, maximum.Y, center.Z)),
                    new BoundingBox(new Vector3(center.X, center.Y, minimum.Z), new Vector3(maximum.X, maximum.Y, center.Z)),
                    new BoundingBox(center, maximum),
                    new BoundingBox(new Vector3(minimum.X, center.Y, center.Z), new Vector3(center.X, maximum.Y, maximum.Z))
                    };
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="source"></param>
    /// <param name="target"></param>
    /// <param name="targetObj"></param>
    /// <returns></returns>
    protected virtual bool IsContains(ref BoundingBox source, BoundingBox target, ref T targetObj)
    {
        return BoxContainsBox(ref source, ref target);
    }

    /// <summary>
    /// Common function to traverse the tree
    /// </summary>
    /// <param name="stack"></param>
    /// <param name="process"></param>
    /// <param name="canVisitChildren"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TreeTraversal(Stack<KeyValuePair<int, int>> stack, Action<int> process,
        Func<int, bool>? canVisitChildren = null)
    {
        var parent = -1;
        var curr = -1;
        var dummy = new Octant(-1, -1);
        dummy[0] = 0;
        var parentOctant = dummy;
        while (true)
        {
            while (++curr < OctantSize)
            {
                if (parentOctant.HasChildAtIndex(curr))
                {
                    var childIdx = parentOctant[curr];
                    process(childIdx);
                    ref var octant = ref octants!.array[childIdx];
                    if (octant.HasChildren && (canVisitChildren == null || canVisitChildren(octant.Index)))
                    {
                        stack.Push(new KeyValuePair<int, int>(parent, curr));
                        parent = octant.Index;
                        curr = -1;
                        parentOctant = octants[parent];
                    }
                }
            }
            if (stack.Count == 0)
            {
                break;
            }
            var prev = stack.Pop();
            parent = prev.Key;
            curr = prev.Value;
            if (parent == -1)
            {
                break;
            }
            parentOctant = octants![parent];
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    /// <param name="model"></param>
    /// <param name="geometry"></param>
    /// <param name="modelMatrix"></param>
    /// <param name="hits"></param>
    /// <returns></returns>
    public bool HitTest(HitTestContext? context, object? model, Geometry3D? geometry, Matrix modelMatrix, ref List<HitTestResult> hits)
    {
        return HitTest(context, model, geometry, modelMatrix, false, ref hits);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    /// <param name="model"></param>
    /// <param name="geometry"></param>
    /// <param name="modelMatrix"></param>
    /// <param name="returnMultiple"></param>
    /// <param name="hits"></param>
    /// <returns></returns>
    public bool HitTest(HitTestContext? context, object? model, Geometry3D? geometry, Matrix modelMatrix,
        bool returnMultiple, ref List<HitTestResult> hits)
    {
        return HitTest(context, model, geometry, modelMatrix, returnMultiple, ref hits, 0);
    }

    /// <summary>
    /// Hits the test.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="model">The model.</param>
    /// <param name="geometry">The geometry.</param>
    /// <param name="modelMatrix">The model matrix.</param>
    /// <param name="hits">The hits.</param>
    /// <param name="hitThickness">The hit thickness.</param>
    /// <returns></returns>
    public virtual bool HitTest(HitTestContext? context, object? model, Geometry3D? geometry, Matrix modelMatrix,
        ref List<HitTestResult> hits, float hitThickness)
    {
        return HitTest(context, model, geometry, modelMatrix, false, ref hits, hitThickness);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    /// <param name="model"></param>
    /// <param name="geometry"></param>
    /// <param name="modelMatrix"></param>
    /// <param name="returnMultiple"></param>
    /// <param name="hits"></param>
    /// <param name="hitThickness"></param>
    /// <returns></returns>
    public virtual bool HitTest(HitTestContext? context, object? model, Geometry3D? geometry, Matrix modelMatrix,
        bool returnMultiple, ref List<HitTestResult> hits, float hitThickness)
    {
        if (context is null)
        {
            return false;
        }

        if (hits == null)
        {
            hits = new List<HitTestResult>();
        }
        hitPathBoundingBoxes.Clear();
        var hitStack = hitStackPool.GetObject();
        hitStack.Clear();
        var isHit = false;
        var modelHits = new List<HitTestResult>();
        var modelInv = modelMatrix.Inverted();
        if (modelInv == MatrixHelper.Zero)
        {
            return false;
        }//Cannot be inverted
        var rayWS = context.RayWS;
        var rayModel = new Ray(Vector3Helper.TransformCoordinate(rayWS.Position, modelInv), Vector3.Normalize(Vector3.TransformNormal(rayWS.Direction, modelInv)));

        var parent = -1;
        var curr = -1;
        var dummy = new Octant(-1, -1);
        dummy[0] = 0;
        var parentOctant = dummy;
        while (true)
        {
            while (++curr < OctantSize)
            {
                if (parentOctant.HasChildAtIndex(curr))
                {
                    ref var octant = ref octants!.array[parentOctant[curr]];
                    var isIntersect = false;
                    var nodeHit = HitTestCurrentNodeExcludeChild(ref octant,
                        context, model, geometry, modelMatrix, ref rayModel, returnMultiple, ref modelHits, ref isIntersect, hitThickness);
                    isHit |= nodeHit;
                    if (isIntersect && octant.HasChildren)
                    {
                        hitStack.Push(new KeyValuePair<int, int>(parent, curr));
                        parent = octant.Index;
                        curr = -1;
                        parentOctant = octants[parent];
                    }
                    if (Parameter.RecordHitPathBoundingBoxes && nodeHit)
                    {
                        var n = octant;
                        while (true)
                        {
                            hitPathBoundingBoxes.Add(n.Bound);
                            if (n.Parent >= 0)
                            {
                                n = octants[n.Parent];
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            if (hitStack.Count == 0)
            {
                break;
            }
            var prev = hitStack.Pop();
            parent = prev.Key;
            curr = prev.Value;
            if (parent == -1)
            {
                break;
            }
            parentOctant = octants![parent];
        }
        hitStackPool.PutObject(hitStack);
        if (!isHit)
        {
            hitPathBoundingBoxes.Clear();
        }
        else
        {
            hits.AddRange(modelHits);
            Hit?.Invoke(this, EventArgs.Empty);
        }
        return isHit;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sphere"></param>
    /// <param name="points"></param>
    /// <returns></returns>
    public virtual bool FindNearestPointBySphere(HitTestContext? context, ref BoundingSphere sphere, ref List<HitTestResult> points)
    {
        if (points == null)
        {
            points = new List<HitTestResult>();
        }
        var hitStack = hitStackPool.GetObject();
        hitStack.Clear();
        var isHit = false;

        var parent = -1;
        var curr = -1;
        var dummy = new Octant(-1, -1);
        dummy[0] = 0;
        var parentOctant = dummy;
        while (true)
        {
            while (++curr < OctantSize)
            {
                if (parentOctant.HasChildAtIndex(curr))
                {
                    ref var octant = ref octants!.array[parentOctant[curr]];
                    var isIntersect = false;
                    var nodeHit = FindNearestPointBySphereExcludeChild(ref octant, context, ref sphere, ref points, ref isIntersect);
                    isHit |= nodeHit;
                    if (octant.HasChildren && isIntersect)
                    {
                        hitStack.Push(new KeyValuePair<int, int>(parent, curr));
                        parent = octant.Index;
                        curr = -1;
                        parentOctant = octants[parent];
                    }
                }
            }
            if (hitStack.Count == 0)
            {
                break;
            }
            var prev = hitStack.Pop();
            parent = prev.Key;
            curr = prev.Value;
            if (parent == -1)
            {
                break;
            }
            parentOctant = octants![parent];
        }
        hitStackPool.PutObject(hitStack);
        return isHit;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    /// <param name="point"></param>
    /// <param name="results"></param>
    /// <param name="heuristicSearchFactor"></param>
    /// <returns></returns>
    public virtual bool FindNearestPointFromPoint(HitTestContext? context, ref Vector3 point, ref List<HitTestResult>? results, float heuristicSearchFactor = 1f)
    {
        results ??= new List<HitTestResult>();
        var hitStack = hitStackPool.GetObject();
        hitStack.Clear();
        var sphere = new BoundingSphere(point, float.MaxValue);
        var isHit = false;
        heuristicSearchFactor = Math.Min(1.0f, Math.Max(0.1f, heuristicSearchFactor));

        var parent = -1;
        var curr = -1;
        var dummy = new Octant(-1, -1);
        dummy[0] = 0;
        var parentOctant = dummy;
        while (true)
        {
            while (++curr < OctantSize)
            {
                if (parentOctant.HasChildAtIndex(curr))
                {
                    ref var octant = ref octants!.array[parentOctant[curr]];
                    var isIntersect = false;
                    var nodeHit = FindNearestPointBySphereExcludeChild(ref octant, context, ref sphere, ref results, ref isIntersect);
                    isHit |= nodeHit;
                    if (isIntersect)
                    {
                        if (results.Count > 0)
                        {
                            sphere.Radius = (float)results[0].Distance * heuristicSearchFactor;
                        }
                        if (octant.HasChildren)
                        {
                            hitStack.Push(new KeyValuePair<int, int>(parent, curr));
                            parent = octant.Index;
                            curr = -1;
                            parentOctant = octants![parent];
                        }
                    }
                }
            }
            if (hitStack.Count == 0)
            {
                break;
            }
            var prev = hitStack.Pop();
            parent = prev.Key;
            curr = prev.Value;
            if (parent == -1)
            {
                break;
            }
            parentOctant = octants![parent];
        }
        hitStackPool.PutObject(hitStack);
        return isHit;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="point"></param>
    /// <param name="radius"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool FindNearestPointByPointAndSearchRadius(HitTestContext? context, ref Vector3 point, float radius, ref List<HitTestResult> result)
    {
        var sphere = new BoundingSphere(point, radius);
        return FindNearestPointBySphere(context, ref sphere, ref result);
    }
    /// <summary>
    /// Find nearest point by sphere on current node only.
    /// </summary>
    /// <param name="octant"></param>
    /// <param name="context"></param>
    /// <param name="sphere"></param>
    /// <param name="points"></param>
    /// <param name="isIntersect"></param>
    /// <returns></returns>
    protected abstract bool FindNearestPointBySphereExcludeChild(ref Octant octant, HitTestContext? context,
        ref BoundingSphere sphere, ref List<HitTestResult> points, ref bool isIntersect);

    /// <summary>
    /// Hit test for current node.
    /// </summary>
    /// <param name="octant"></param>
    /// <param name="context"></param>
    /// <param name="model"></param>
    /// <param name="geometry"></param>
    /// <param name="modelMatrix"></param>
    /// <param name="rayModel"></param>
    /// <param name="returnMultiple">Return multiple hit results or only the closest one</param>
    /// <param name="hits"></param>
    /// <param name="isIntersect"></param>
    /// <param name="hitThickness"></param>
    /// <returns></returns>
    protected abstract bool HitTestCurrentNodeExcludeChild(ref Octant octant, HitTestContext? context, object? model, Geometry3D? geometry,
        Matrix modelMatrix, ref Ray rayModel, bool returnMultiple, ref List<HitTestResult> hits, ref bool isIntersect, float hitThickness);

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public LineGeometry3D CreateOctreeLineModel()
    {
        var builder = new LineBuilder();
        for (var i = 0; i < octants!.Count; ++i)
        {
            var box = octants.array[i].Bound;
            var verts = new Vector3[8];
            verts[0] = box.Minimum;
            verts[1] = new Vector3(box.Minimum.X, box.Minimum.Y, box.Maximum.Z); //Z
            verts[2] = new Vector3(box.Minimum.X, box.Maximum.Y, box.Minimum.Z); //Y
            verts[3] = new Vector3(box.Maximum.X, box.Minimum.Y, box.Minimum.Z); //X

            verts[7] = box.Maximum;
            verts[4] = new Vector3(box.Maximum.X, box.Maximum.Y, box.Minimum.Z); //Z
            verts[5] = new Vector3(box.Maximum.X, box.Minimum.Y, box.Maximum.Z); //Y
            verts[6] = new Vector3(box.Minimum.X, box.Maximum.Y, box.Maximum.Z); //X
            builder.AddLine(verts[0], verts[1]);
            builder.AddLine(verts[0], verts[2]);
            builder.AddLine(verts[0], verts[3]);
            builder.AddLine(verts[7], verts[4]);
            builder.AddLine(verts[7], verts[5]);
            builder.AddLine(verts[7], verts[6]);

            builder.AddLine(verts[1], verts[6]);
            builder.AddLine(verts[1], verts[5]);
            builder.AddLine(verts[4], verts[2]);
            builder.AddLine(verts[4], verts[3]);
            builder.AddLine(verts[2], verts[6]);
            builder.AddLine(verts[3], verts[5]);
        }
        return builder.ToLineGeometry3D();
    }
    #region Special Tests
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool BoxContainsBox(ref BoundingBox source, ref BoundingBox target)
    {
        //Source contains target
        return source.Minimum.X <= target.Minimum.X && (target.Maximum.X <= source.Maximum.X &&
            source.Minimum.Y <= target.Minimum.Y && target.Maximum.Y <= source.Maximum.Y) &&
            source.Minimum.Z <= target.Minimum.Z && target.Maximum.Z <= source.Maximum.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool BoxDisjointSphere(BoundingBox box, ref BoundingSphere sphere)
    {
        Vector3Helper.Clamp(ref sphere.Center, ref box.Minimum, ref box.Maximum, out var vector);
        var distance = Vector3.DistanceSquared(sphere.Center, vector);

        return distance > sphere.Radius * sphere.Radius;
    }
    #endregion
}
