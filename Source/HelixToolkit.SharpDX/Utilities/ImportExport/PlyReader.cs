﻿using SharpDX;
using Mesh3DGroup = System.Collections.Generic.List<HelixToolkit.SharpDX.Object3D>;
using FileFormatException = System.Exception;
using HelixToolkit.Geometry;

namespace HelixToolkit.SharpDX;

/// <summary>
/// Polygon File Format Reader.
/// </summary>
/// https://www.cc.gatech.edu/projects/large_models/ply.html
/// http://graphics.stanford.edu/data/3Dscanrep/
/// <remarks>
/// This reader only reads ascii ply formats.
/// This was initially meant to read models exported by Blender 3D Software.
/// </remarks>
[Obsolete("Suggest to use HelixToolkit.SharpDX.Assimp")]
public class PlyReader : ModelReader
{
    /// <summary>
    /// Initializes a new <see cref="PlyReader"/>.
    /// </summary>
    public PlyReader()
    {
        InitializeProperties();
    }

    #region Public methods
    /// <summary>
    /// Reads the model from the specified stream.
    /// </summary>
    /// <param name="s">The stream.</param>
    /// <param name="info"></param>
    /// <returns>A <see cref="Mesh3DGroup" /></returns>       
    public override Mesh3DGroup? Read(Stream s, ModelInfo info = default(ModelInfo))
    {
        InitializeProperties();
        this.Load(s);
        return this.CreateModel3D();
    }

    /// <summary>
    /// Reads the model from the specified path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The model.</returns>
    public Mesh3DGroup? Read(string path)
    {
        InitializeProperties();
        plymodelURI = path;
        Load(path);
        using var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return this.Read(s);
    }

    /// <summary>
    /// Creates a mesh from the loaded file.
    /// </summary>
    /// <returns>
    /// A <see cref="MeshGeometry3D" />.
    /// </returns>
    public MeshGeometry3D CreateMesh()
    {
        var mesh = new MeshGeometry3D();
        if (Vertices is not null && Vertices.Count > 0 && mesh.Positions is not null)
        {
            foreach (var vert in Vertices)
            {
                mesh.Positions.Add(vert);
            }
        }

        if (Faces is not null && Faces.Count > 0 && mesh.Indices is not null)
        {
            foreach (var face in Faces)
            {
                mesh.Indices.AddRange((int[])face.Clone());
            }
        }

        if (TextureCoordinates is not null && TextureCoordinates.Count > 0 && mesh.TextureCoordinates is not null)
        {
            foreach (Vector2 item in TextureCoordinates)
            {
                mesh.TextureCoordinates.Add(item);
            }
        }

        if (TextureCoordinates is not null && TextureCoordinates.Count == 0)
        {
            TextureCoordinates = null;
        }

        return mesh;
    }

    /// <summary>
    /// Creates a <see cref="MeshGeometry3D" /> object from the loaded file. Polygons are triangulated using triangle fans.
    /// </summary>
    /// <returns>
    /// A <see cref="MeshGeometry3D" />.
    /// </returns>
    public MeshGeometry3D CreateMeshGeometry3D()
    {
        var mb = new MeshBuilder(true, true);

        if (Vertices is not null && Vertices.Count > 0)
        {
            foreach (var p in this.Vertices)
            {
                mb.Positions.Add(p);
            }
        }

        if (Faces is not null && Faces.Count > 0)
        {
            foreach (var face in Faces)
            {
                mb.AddTriangleFan(face);
            }
        }

        if (Normals is not null && Normals.Count > 0)
        {
            mb.CreateNormals = true;
            if (mb.Normals is not null)
            {
                foreach (var item in Normals)
                {
                    mb.Normals.Add(item);
                }
            }
        }
        if (Normals is not null && Normals.Count == 0)
        {
            mb.CreateNormals = false;
        }

        if (TextureCoordinates is not null && TextureCoordinates.Count > 0)
        {
            mb.CreateTextureCoordinates = true;
            if (mb.TextureCoordinates is not null)
            {
                foreach (var item in TextureCoordinates)
                {
                    mb.TextureCoordinates.Add(item);
                }
            }
        }

        if (TextureCoordinates is not null && TextureCoordinates.Count == 0)
        {
            TextureCoordinates = null;
            mb.TextureCoordinates = null;
            mb.CreateTextureCoordinates = false;
        }

        var mesh = mb.ToMeshGeometry3D();
        if (mesh.Normals == null || mesh.Normals.Count == 0)
        {
            mesh.UpdateNormals();
        }

        return mesh;
    }

    /// <summary>
    /// Creates a <see cref="Mesh3DGroup" /> from the loaded file.
    /// </summary>
    /// <returns>A <see cref="Mesh3DGroup" />.</returns>
    public Mesh3DGroup CreateModel3D()
    {
        Mesh3DGroup modelGroup = new();
        var g = this.CreateMeshGeometry3D();
        var gm = new Object3D
        {
            Geometry = g,
            Material = this.DefaultMaterial,
        };
        modelGroup.Add(gm);
        return modelGroup;
    }

    /// <summary>
    /// Loads a ply file from the <see cref="Stream"/>.
    /// </summary>
    /// <param name="s">The stream containing the ply file.</param>
    public void Load(Stream s)
    {
        InitializeProperties();
        //Get the model format from the header in order for the correct strategy to be used
        using (var textReader = new StreamReader(s))
        {
            var linCount = 0;
            //!textReader.EndOfStream
            while (!textReader.EndOfStream)
            {
                var lineTxt = textReader.ReadLine() ?? string.Empty;
                var initarr = lineTxt.Split(' ');
                if (initarr[0] == "format")
                {
                    if (initarr[1] == "ascii")
                    {
                        modelFormat = PlyFormatTypes.ascii;

                    }

                    else if (initarr[1] == "binary_big_endian")
                    {
                        modelFormat = PlyFormatTypes.binary_big_endian;
                    }

                    else if (initarr[1] == "binary_little_endian")
                    {
                        modelFormat = PlyFormatTypes.binary_little_endian;
                    }
                }
                else
                {
                    continue;
                }
                linCount += 1;
            }
        }

        #region MyRegion
        switch (modelFormat)
        {
            case PlyFormatTypes.ascii:
                {
                    if (plymodelURI.Length > 6)
                    {
                        using var fs = new FileStream(plymodelURI, FileMode.Open, FileAccess.Read);
                        Load_ascii(fs);
                    }


                    break;
                }

            case PlyFormatTypes.binary_big_endian:
                {
                    if (plymodelURI.Length > 6)
                    {
                        using var fs = new FileStream(plymodelURI, FileMode.Open, FileAccess.Read);
                        Load_binaryBE(fs);
                    }
                    break;
                }

            case PlyFormatTypes.binary_little_endian:
                {

                    break;
                }
            default:
                break;
        }
        #endregion
    }

    public void Load(string filepath)
    {
        InitializeProperties();
        plymodelURI = filepath;
        using var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
        Load(fs);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the vertices of this ply model.
    /// </summary>
    public IList<Vector3>? Vertices
    {
        get; private set;
    }

    public IList<int[]>? Faces
    {
        get; private set;
    }

    /// <summary>
    /// Gets or sets the normal vectors of this ply model.
    /// </summary>
    public IList<Vector3>? Normals
    {
        get; private set;
    }

    /// <summary>
    /// Gets or sets the texture coordinates of the ply model.
    /// </summary>
    /// <remarks>S,T->X,Y</remarks>
    public IList<Vector2>? TextureCoordinates
    {
        get; private set;
    }

    /// <summary>
    /// Gets or sets the number of vertices.
    /// </summary>
    public int VerticesNumber
    {
        get; private set;
    }

    /// <summary>
    /// Gets or sets the number of faces.
    /// </summary>
    public int FacesNumber
    {
        get; private set;
    }

    /// <summary>
    /// Contains information about the Ply file received.
    /// </summary>
    public Dictionary<string, double>? ObjectInformation
    {
        get; private set;
    }

    #endregion

    #region Initialized Variables
    /// <summary>
    /// Stores the type of ply format in the <see cref="Stream"/>.
    /// </summary>
    private PlyFormatTypes modelFormat = PlyFormatTypes.ascii;

    /// <summary>
    /// Tells us what the current element in the header is, 
    /// so we can pick its property values in the subsequent lines.
    /// </summary>
    /// <remarks>
    /// Only useful in reading the ply header.
    /// </remarks>
    private PlyElements currentElement = PlyElements.none;
    /// <summary>
    /// The line containing data about an element(It starts just after the "end_header" statement).
    /// </summary>
    private int elementLine_Current = 0;
    /* A cummulative sum of each element count in the current ply model.
     * This helps to provide a range of the element position as we move through the ascii Ply file.
     */
    //private int ElLine_SeqCount = 0;//[Deprecated].
    /// <summary>
    /// Gets the number of lines that contains element data.
    /// </summary>
    private int elementLines_Count = 0;
    private string plymodelURI = "";
    private Dictionary<string, PLYElement> elements_range = new();

    #endregion

    #region private Types
    /// <summary>
    /// Contains a list of ply formatted model types.
    /// </summary>
    private enum PlyFormatTypes
    {
        /// <summary>
        /// ASCII ply format.
        /// </summary>
        ascii,
        /// <summary>
        /// Binary big endian ply format.
        /// </summary>
        binary_big_endian,
        /// <summary>
        /// Binary little endian ply format.
        /// </summary>
        binary_little_endian,


        /// <summary>
        /// An unrecognized format.
        /// </summary>
        none
    }

    /// <summary>
    /// Contains a list of supported ply elements.
    /// </summary>
    private enum PlyElements
    {
        /// <summary>
        /// The vertex ply element.
        /// </summary>
        vertex,
        /// <summary>
        /// The face ply element.
        /// </summary>
        face,

        /// <summary>
        /// An unrecognized element.
        /// </summary>
        none
    }

    /// <summary>
    /// A class that attempts to define ply elements.
    /// </summary>
    private class PLYElement
    {
        public int PropertyIndex = 0;

        /// <summary>
        /// Stores the property string with its index.
        /// </summary>
        public Dictionary<string, int> Property_with_Index = new();

        /// <summary>
        /// Initializes a new PLY element.
        /// </summary>
        /// <param name="_y1">The lower or start range.</param>
        /// <param name="_y2">The upper or end range.</param>
        /// <param name="hasNormals"></param>
        /// <param name="hasTextures"></param>
        public PLYElement(int _y1 = 0, int _y2 = 1, bool hasNormals = false, bool hasTextures = false)
        {
            PropertyIndex = 0;
            Property_with_Index = new Dictionary<string, int>();
            StartRange = _y1;
            EndRange = _y2;
            ContainsNormals = hasNormals;
            ContainsTextureCoordinates = hasTextures;
            ElementCount = EndRange - StartRange;
        }

        #region Element class properties
        /// <summary>
        /// The point from which the current element starts to get picked.
        /// </summary>
        public int StartRange
        {
            get; set;
        }

        /// <summary>
        /// The point at which the current element stops to get picked.
        /// </summary>
        public int EndRange
        {
            get; set;
        }

        /// <summary>
        /// Specifies whether or not the vertex element has a normal.
        /// </summary>
        public bool ContainsNormals { get; set; } = false;

        /// <summary>
        /// Whether or not the vertex element has a texture coordinate.
        /// </summary>
        /// <remarks>
        /// for vertices only.
        /// </remarks>
        public bool ContainsTextureCoordinates { get; set; } = false;

        /// <summary>
        /// Stores the number of the specified element in the current Ply model.
        /// </summary>
        public int ElementCount
        {
            get; private set;
        }

        #endregion

        /// <summary>
        /// Returns a value specifying whether the index: <paramref name="num"/> is in this <see cref="PLYElement"/> range.
        /// </summary>
        /// <param name="num">The index.</param>
        /// <returns></returns>
        public bool ContainsNumber(int num)
        {
            if (num >= StartRange && num <= EndRange)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    #endregion

    #region Private methods

    private bool IsDigitChar(char input)
    {
        if (input == '-' || input == '+' || input == '0' || input == '1' || input == '2' || input == '3' || input == '4' || input == '5' || input == '6' || input == '7' || input == '8' || input == '9' || input == '.')
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void InitializeProperties()
    {
        #region public:
        Vertices = new List<Vector3>();
        Faces = new List<int[]>();
        Normals = new List<Vector3>();
        TextureCoordinates = new List<Vector2>();
        FacesNumber = 0;
        VerticesNumber = 0;
        ObjectInformation = new Dictionary<string, double>();

        #endregion

        #region private:
        modelFormat = PlyFormatTypes.none;
        currentElement = PlyElements.none;
        elementLine_Current = 0;
        elementLines_Count = 0;
        elements_range = new Dictionary<string, PLYElement>();
        #endregion

    }

    /// <summary>
    /// Loads an ascii format ply file.
    /// </summary>
    /// <param name="s"></param>
    private void Load_ascii(Stream s)
    {
        using var reader = new StreamReader(s);
        while (!reader.EndOfStream)
        {

            var curline = reader.ReadLine() ?? string.Empty;
            var strarr = curline.Split(' ');
            if (string.IsNullOrEmpty(curline))
            {
                //reader.Close();
            }

            #region Heading
            //comment Line
            else if (strarr[0] == "comment" || strarr[0] == "format" || strarr[0] == "ply")
            {
            }

            //obj_info Line
            else if (strarr[0] == "obj_info")
            {
                //ObjectInformation.Add(strarr[1], double.Parse(strarr[2]));
            }

            //element Line
            else if (strarr[0] == "element")
            {
                /* Supported elements
                 * vertex
                 * face
                 */

                if (strarr[1] == "vertex")
                {
                    VerticesNumber = int.Parse(strarr[2]);
                    //Add the vertex element to the dictionary, including its contextual range
                    elements_range.Add("vertex", new PLYElement(elementLines_Count + 1, elementLines_Count + VerticesNumber));
                    elementLines_Count += int.Parse(strarr[2]);
                    //set the current element as a "vertex"element 
                    currentElement = PlyElements.vertex;
                }

                else if (strarr[1] == "face")
                {
                    FacesNumber = int.Parse(strarr[2]);

                    elements_range.Add("face", new PLYElement(elementLines_Count + 1, elementLines_Count + FacesNumber));
                    elementLines_Count += int.Parse(strarr[2]);
                    currentElement = PlyElements.face;

                }

                else
                {
                    var miscElementNumber = int.Parse(strarr[2]);

                    elements_range.Add(strarr[1], new PLYElement(elementLines_Count, elementLines_Count + miscElementNumber));
                    elementLines_Count += int.Parse(strarr[2]);
                    currentElement = PlyElements.none;
                }
            }

            //property Line
            else if (strarr[0] == "property")
            {
                //Ignore the numerical data type for now

                switch (currentElement)
                {
                    case PlyElements.vertex:
                        {
                            var propInd = elements_range["vertex"].PropertyIndex;
                            elements_range["vertex"].Property_with_Index.Add(strarr[2], propInd);
                            //Increase the property index if there's an element which has/hasn't been supported.
                            elements_range["vertex"].PropertyIndex += 1;
                            if (strarr[2] == "nx" || strarr[2] == "ny" || strarr[2] == "nz")
                            {
                                elements_range["vertex"].ContainsNormals = true;
                            }
                            else if (strarr[2] == "s" || strarr[2] == "t")
                            {
                                elements_range["vertex"].ContainsTextureCoordinates = true;
                            }

                            break;
                        }

                    case PlyElements.face:
                        {
                            //nothing to do yet

                            break;
                        }

                    default:
                        break;
                }
            }

            else if (strarr[0] == "end_header")
            {
                //end info, begin number collection.
                elementLine_Current = 0;

            }

            #endregion

            /* We pick the elements and its properties by checking whether the current
             * element line is contained in the element range.
             * The picking occurs if the first character in the string indicates a number follows.
             */
            else if (IsDigitChar(strarr[0][0]) == true)
            {
                elementLine_Current++;
                if (elements_range.ContainsKey("vertex"))
                {

                    if (elements_range["vertex"].ContainsNumber(elementLine_Current) == true)
                    {
                        //Get vertices
                        var x_Indx = elements_range["vertex"].Property_with_Index["x"];
                        var y_Indx = elements_range["vertex"].Property_with_Index["y"];
                        var z_Indx = elements_range["vertex"].Property_with_Index["z"];
                        var pt = new Vector3()
                        {
                            X = float.Parse(strarr[x_Indx]),
                            Y = float.Parse(strarr[y_Indx]),
                            Z = float.Parse(strarr[z_Indx])
                        };
                        Vertices?.Add(pt);

                        if (elements_range["vertex"].ContainsNormals == true)
                        {
                            var nx_Indx = elements_range["vertex"].Property_with_Index["nx"];
                            var ny_Indx = elements_range["vertex"].Property_with_Index["ny"];
                            var nz_Indx = elements_range["vertex"].Property_with_Index["nz"];

                            var vect3d = new Vector3
                            {
                                X = float.Parse(strarr[nx_Indx]),
                                Y = float.Parse(strarr[ny_Indx]),
                                Z = float.Parse(strarr[nz_Indx])
                            };
                            Normals?.Add(vect3d);
                        }

                        if (elements_range["vertex"].ContainsTextureCoordinates == true)
                        {
                            var s_Indx = elements_range["vertex"].Property_with_Index["s"];
                            var t_Indx = elements_range["vertex"].Property_with_Index["t"];

                            var texpt = new Vector2
                            {
                                X = float.Parse(strarr[s_Indx]),
                                Y = float.Parse(strarr[t_Indx]),
                            };
                            TextureCoordinates?.Add(texpt);
                        }
                    }
                }

                if (elements_range.ContainsKey("face"))
                {

                    if (elements_range["face"].ContainsNumber(elementLine_Current) == true)
                    {

                        var facepos = new List<int>();
                        for (var i = 1; i <= int.Parse(strarr[0]); i++)
                        {
                            facepos.Add(int.Parse(strarr[i]));
                        }
                        Faces?.Add(facepos.ToArray());
                    }
                }

                else
                {
                    continue;
                }
                //should increase the elementLine_Current iff 
                //the line contains element data.
            }

            else
            {
                continue;
            }
        }
    }

    /// <summary>
    /// Loads a binary_big_endian format ply file.
    /// </summary>
    /// <param name="s"></param>
    private void Load_binaryBE(Stream s)
    {
        using (var reader = new BinaryReader(s))
        {
            while (reader.ReadString() != null)
            {
                var curline = reader.ReadString() ?? string.Empty;
                var strarr = curline.Split(' ');
                //comment Line
                if (strarr[0] == "comment")
                {
                    continue;
                }

                //obj_info Line
                if (strarr[0] == "obj_info")
                {
                    ObjectInformation?.Add(strarr[1], double.Parse(strarr[2]));
                }

                //element Line
                if (strarr[0] == "element")
                {
                    /* Supported elements
                     * vertex
                     * face
                     */

                    if (strarr[1] == "vertex")
                    {
                        VerticesNumber = int.Parse(strarr[2]);

                        //Add the vertex element to the dictionary, including its contextual range
                        elements_range.Add("vertex", new PLYElement(_y1: elementLines_Count, _y2: elementLines_Count + VerticesNumber));
                        elementLines_Count += int.Parse(strarr[2]);
                        currentElement = PlyElements.vertex;
                    }

                    else if (strarr[1] == "face")
                    {
                        FacesNumber = int.Parse(strarr[2]);

                        elements_range.Add("face", new PLYElement(elementLines_Count, elementLines_Count + FacesNumber));
                        elementLines_Count += int.Parse(strarr[2]);
                        currentElement = PlyElements.face;
                    }
                    else
                    {
                        var miscElementNumber = int.Parse(strarr[2]);

                        elements_range.Add(strarr[1], new PLYElement(elementLines_Count, elementLines_Count + miscElementNumber));
                        elementLines_Count += int.Parse(strarr[2]);

                    }
                }

                //property Line
                if (strarr[0] == "property")
                {
                    //Ignore the numerical data type for now

                    switch (currentElement)
                    {
                        case PlyElements.vertex:
                            {
                                if (strarr[2] == "nx" || strarr[2] == "ny" || strarr[2] == "nz")
                                {
                                    elements_range["vertex"].ContainsNormals = true;
                                }

                                var propInd = elements_range["vertex"].PropertyIndex;
                                elements_range["vertex"].Property_with_Index.Add(strarr[2], propInd);
                                //Increase the property index if there's an element which has/hasn't been supported.
                                elements_range["vertex"].PropertyIndex += 1;
                                break;
                            }

                        case PlyElements.face:
                            {
                                //nothing to do yet

                                break;
                            }

                        default:
                            break;
                    }
                }

                if (strarr[0] == "end_header")
                {
                    //end info, begin number collection.
                    elementLine_Current = 0;

                }

                /* We pick the elements and its properties by checking whether the current
                 * element line is contained in the element range.
                 */
                if (elements_range["vertex"].ContainsNumber(elementLine_Current))
                {
                    //Get vertices
                    var x_Indx = elements_range["vertex"].Property_with_Index["x"];
                    var y_Indx = elements_range["vertex"].Property_with_Index["y"];
                    var z_Indx = elements_range["vertex"].Property_with_Index["z"];
                    var pt = new Vector3()
                    {
                        X = float.Parse(strarr[x_Indx]),
                        Y = float.Parse(strarr[y_Indx]),
                        Z = float.Parse(strarr[z_Indx])
                    };
                    Vertices?.Add(pt);

                    if (elements_range["vertex"].ContainsNormals == true)
                    {
                        var nx_Indx = elements_range["vertex"].Property_with_Index["nx"];
                        var ny_Indx = elements_range["vertex"].Property_with_Index["ny"];
                        var nz_Indx = elements_range["vertex"].Property_with_Index["nz"];

                        var vect3 = new Vector3
                        {
                            X = float.Parse(strarr[nx_Indx]),
                            Y = float.Parse(strarr[ny_Indx]),
                            Z = float.Parse(strarr[nz_Indx])
                        };
                    }

                    if (elements_range["vertex"].ContainsTextureCoordinates == true)
                    {

                    }
                }
                if (elements_range["face"].ContainsNumber(elementLine_Current))
                {
                    var facepos = new List<int>();
                    for (var i = 1; i <= int.Parse(strarr[0]); i++)
                    {
                        facepos.Add(int.Parse(strarr[i]));
                    }
                    Faces?.Add(facepos.ToArray());
                }


                elementLine_Current++;
            }
        }
        var br = new BinaryReader(s);
        br.ReadString();

    }

    #endregion
}
