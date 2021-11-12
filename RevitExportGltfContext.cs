using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RevitExportGltf.Util;

namespace RevitExportGltf
{

    class RevitExportGltfContext : IExportContext
    {
        private Document doc;
        Element currentElem;
        ObjectData RootDatas;
        ObjectData InstanceNameData;
        ObjectData currentData;
        public IndexedDictionary<ObjectData> currentDatas { get; }
            = new IndexedDictionary<ObjectData>();

        public List<glTFScene> Scenes = new List<glTFScene>();
        public IndexedDictionary<glTFNode> Nodes { get; } = new IndexedDictionary<glTFNode>();
        public IndexedDictionary<glTFMesh> Meshes { get; } = new IndexedDictionary<glTFMesh>();
        public IndexedDictionary<glTFMaterial> Materials { get; } = new IndexedDictionary<glTFMaterial>();

        public IndexedDictionary<glTFTexture> Textures { get; } = new IndexedDictionary<glTFTexture>();
        public IndexedDictionary<glTFImage> Images { get; } = new IndexedDictionary<glTFImage>();
        public IndexedDictionary<glTFSampler> Samplers { get; } = new IndexedDictionary<glTFSampler>();

        public List<glTFBuffer> Buffers { get; } = new List<glTFBuffer>();
        public List<glTFBufferView> BufferViews { get; } = new List<glTFBufferView>();
        public List<glTFAccessor> Accessors { get; } = new List<glTFAccessor>();

        /// <summary>
        /// 用于存储顶点/面/法线信息，将被序列化为二进制格式的
        /// 用于最终的*.bin文件。
        /// </summary>
        public List<glTFBinaryData> binFileData { get; } = new List<glTFBinaryData>();



        private glTFNode rootNode;
        /// <summary>
        /// 几何图形列表
        ///当前正在处理的元素，以材质为键值。这
        ///在每个新元素上重新初始化
        /// </summary>
        private IndexedDictionary<GeometryData> currentGeometry;
        /// <summary>
        /// 顶点列表
        /// </summary>
        private Stack<Transform> _transformStack = new Stack<Transform>();
        private Transform CurrentTransform { get { return _transformStack.Peek(); } }


        private string FileName;
        private string directory;
        public RevitExportGltfContext(Document document, string fileName)
        {
            doc = document;
            FileName = fileName;
            directory = Path.GetDirectoryName(FileName) + "\\";
        }

        string textureFolder;
        public bool Start()
        {
            _transformStack.Push(Transform.Identity);

            RootDatas = new ObjectData();

            //场景
            glTFScene defaultScene = new glTFScene();
            defaultScene.nodes.Add(0);
            Scenes.Add(defaultScene);
            //节点
            rootNode = new glTFNode();
            rootNode.name = "rootNode";
            rootNode.children = new List<int>();
            Nodes.AddOrUpdateCurrent("rootNode", rootNode);

            //通过读取注册表相应键值获取材质库地址
            RegistryKey hklm = Registry.LocalMachine;
            RegistryKey libraryPath = hklm.OpenSubKey("SOFTWARE\\WOW6432Node\\Autodesk\\ADSKTextureLibrary\\1");
            textureFolder = libraryPath.GetValue("LibraryPaths").ToString();
            hklm.Close();
            libraryPath.Close();

            return true;
        }


        public void Finish()
        {
            // TODO: [RM] Standardize what non glTF spec elements will go into
            // this "BIM glTF superset" and write a spec for it. Gridlines below
            // are an example.

            // Add gridlines as gltf nodes in the format:
            // Origin {Vec3<double>}, Direction {Vec3<double>}, Length {double}
            int bytePosition = 0;
            int currentBuffer = 0;

            foreach (var view in BufferViews)
            {
                if (view.buffer == 0)
                {
                    bytePosition += view.byteLength;
                    continue;
                }

                if (view.buffer != currentBuffer)
                {
                    view.buffer = 0;
                    view.byteOffset = bytePosition;
                    bytePosition += view.byteLength;
                }
            }

            glTFBuffer buffer = new glTFBuffer();
            buffer.uri = Path.GetFileNameWithoutExtension(FileName) + ".bin";
            buffer.byteLength = bytePosition;
            Buffers.Clear();
            Buffers.Add(buffer);


            //写入bin文件
            directory = Path.GetDirectoryName(FileName) + "\\";
            StreamWriter swObj = new StreamWriter(directory + "binFilePrint.txt");
            using (FileStream f = File.Create(directory + buffer.uri))
            {
                using (BinaryWriter writer = new BinaryWriter(f))
                {
                    var count = 0;
                    foreach (var bin in binFileData)
                    {
                        count++;
                        foreach (var coord in bin.vertexBuffer)
                        {
                            writer.Write((float)coord);
                            swObj.Write((float)coord + " ");

                        }
                        swObj.Write("\n");
                        swObj.Write("***********vertexBuffer*******第" + count + "次*********");
                        swObj.Write("\n");

                        // TODO: add writer for normals buffer
                        foreach (var index in bin.indexBuffer)
                        {
                            writer.Write((int)index);
                            swObj.Write((int)index + " ");
                        }
                        swObj.Write("\n");
                        swObj.Write("*************indexBuffer********第" + count + "次*********");
                        swObj.Write("\n");

                        foreach (var uv in bin.uvBuffer)
                        {
                            writer.Write((float)uv);
                            swObj.Write((float)uv + " ");
                        }
                        swObj.Write("\n");
                        swObj.Write("**************uvBuffer*********第" + count + "次*********");
                        swObj.Write("\n");
                    }
                }
            }
            swObj.Close();
            //将属性打包到一个可序列化的容器中
            glTF model = new glTF();
            model.asset = new glTFVersion();
            model.scenes = Scenes;
            model.nodes = Nodes.List;
            model.meshes = Meshes.List;
            model.materials = Materials.List;
            model.textures = Textures.List;
            model.images = Images.List;
            model.samplers = Samplers.List;


            model.buffers = Buffers;
            model.bufferViews = BufferViews;
            model.accessors = Accessors;

            //写入 *.gltf 文件
            string serializedModel = JsonConvert.SerializeObject(model, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            File.WriteAllText(FileName, serializedModel);
        }

        bool isHasSimilar;
        string center;
        int index = 0;
        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            index = 0;
            Element e = doc.GetElement(elementId);
            currentElem = doc.GetElement(elementId);
            try
            {
                if ((BuiltInCategory)currentElem.Category.Id.IntegerValue == BuiltInCategory.OST_Cameras ||
                   currentElem.Category.CategoryType == CategoryType.AnalyticalModel)
                {
                    return RenderNodeAction.Skip;
                }

            }
            catch
            {
                Console.WriteLine("currentElem.Category 为null");
            }
            isHasSimilar = false;
            center = null;
            if (Nodes.Contains(e.UniqueId))
            {
                return RenderNodeAction.Skip;
            }
            try
            {

                string CategoryName = currentElem.Category.Name;
                string FamilyName = currentElem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
                if (FamilyName == "")
                {
                    FamilyName = CategoryName;
                }
                string InstanceName = currentElem.Name;
                InstanceNameData = null;
                List<string> Names = new List<string>() { CategoryName, FamilyName, InstanceName };
                getitem(RootDatas, Names);
                if (InstanceNameData == null)
                {
                    return RenderNodeAction.Skip;
                }

            }
            catch { Console.WriteLine("currentElem.Category 为null"); }

            currentData = new ObjectData();
            currentData.ElementId = currentElem.Id.ToString();
            currentData.ElementName = currentElem.Name;
            try
            {
                //在revit中不一定能获取到值
                Parameter paraArea = currentElem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                if(paraArea!=null) 
                    paraArea.AsValueString();
                    
                Parameter paraVolum = currentElem.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
                if(paraVolum!=null) 
                    paraVolum.AsValueString();

            }
            catch
            {
                Console.WriteLine("currentElem.Category 为null");
            }

            try
            {

                foreach (ObjectData similar in InstanceNameData.Children)
                {
                    if (similar.ElementArea == currentData.ElementArea && similar.ElementVolum == currentData.ElementVolum && similar.ElementArea != null && similar.ElementVolum != null)
                    {
                        if (similar.SimilarObjectID != null)
                        {
                            currentData.SimilarObjectID = similar.SimilarObjectID;
                        }
                        else
                        {
                            currentData.SimilarObjectID = similar.ElementId;
                        }
                        similar.Children.Add(currentData);
                        isHasSimilar = true;
                        break;
                    }
                }
                if (currentData.SimilarObjectID == null)
                {
                    InstanceNameData.Children.Add(currentData);
                }
            }
            catch
            {
                Console.WriteLine("InstanceNameData 属性不能为null");
            }

            currentData.ElementLocation = new List<string>();
            if (currentElem.Location is LocationPoint)
            {
                FamilyInstance family = currentElem as FamilyInstance;
                LocationPoint point = currentElem.Location as LocationPoint;
                try
                {
                    GetPoints(currentElem);
                    currentData.ElementLocation.Add(center);
                    currentData.ElementLocation.Add(family.HandOrientation.Point());
                    XYZ vect = family.HandOrientation.CrossProduct(family.FacingOrientation);
                    currentData.ElementLocation.Add(vect.Point());
                }
                catch
                {

                }
            }
            //新节点
            glTFNode newNode = new glTFNode();
            newNode.name = e.Name + "[" + e.Id.ToString() + "]";


            Debug.WriteLine("Finishing...");
            //获取构件的任意两个点
            //GetPoints(currentElem);
            //currentData.ElementLocation = EdgesPoints;
            currentDatas.AddOrUpdateCurrent(currentElem.Id.ToString(), currentData);
            if (currentData.ElementLocation.Count == 0 || currentData.ElementLocation[0] == null)
            {
                isHasSimilar = false;
            }


            if (currentElem is Mullion)
            {
                isHasSimilar = false;
            }
            if (isHasSimilar == true)
            {
                List<string> Similarpoints = currentDatas.
                    GetElement(currentData.SimilarObjectID).ElementLocation;
                List<string> CurrentPoints = currentData.ElementLocation;

                string[] p1 = Similarpoints[0].Split(new char[] { ',' });
                double x1 = Convert.ToDouble(p1[0]);
                double y1 = Convert.ToDouble(p1[1]);
                double z1 = Convert.ToDouble(p1[2]);


                string[] p2 = Similarpoints[1].Split(new char[] { ',' });
                double x2 = Convert.ToDouble(p2[0]);
                double y2 = Convert.ToDouble(p2[1]);
                double z2 = Convert.ToDouble(p2[2]);
                XYZ vect1 = new XYZ(x2, y2, z2);

                //当前构件的点
                string[] elementPoint1 = CurrentPoints[0].Split(new char[] { ',' });
                double px1 = Convert.ToDouble(elementPoint1[0]);
                double py1 = Convert.ToDouble(elementPoint1[1]);
                double pz1 = Convert.ToDouble(elementPoint1[2]);

                string[] elementPoint2 = CurrentPoints[1].Split(new char[] { ',' });
                double px2 = Convert.ToDouble(elementPoint2[0]);
                double py2 = Convert.ToDouble(elementPoint2[1]);
                double pz2 = Convert.ToDouble(elementPoint2[2]);
                XYZ vect2 = new XYZ(px2, py2, pz2);
                //平移第一个点
                double MoveX = Math.Round(px1 - x1, 2);
                double MoveY = Math.Round(py1 - y1, 2);
                double MoveZ = Math.Round(pz1 - z1, 2);
                //平移第二个点,保留两位小数
                //旋转
                if (!vect1.IsAlmostEqualTo(vect2))
                {
                    //求旋转角
                    double Radian = vect1.AngleTo(vect2);
                    //求旋转轴,求法向量
                    XYZ Ro = vect1.CrossProduct(vect2);//向量的叉乘,
                    //vect1和vect2向量共线时,即旋转180度法向量为0，旋转轴无法求出,引用第三个点求
                    if (Ro.X == 0 && Ro.Y == 0 && Ro.Z == 0)
                    {
                        string[] elementPoint3 = Similarpoints[2].Split(new char[] { ',' });
                        double px3 = Convert.ToDouble(elementPoint3[0]);
                        double py3 = Convert.ToDouble(elementPoint3[1]);
                        double pz3 = Convert.ToDouble(elementPoint3[2]);
                        Ro = new XYZ(px3, py3, pz3);
                    }

                    //向量单位化,求向量的模
                    double Norm = Math.Sqrt(Ro.X * Ro.X + Ro.Y * Ro.Y + Ro.Z * Ro.Z);
                    XYZ Unit = new XYZ(Ro.X / Norm, Ro.Y / Norm, Ro.Z / Norm);
                    ////计算4x4矩阵
                    ////相同构件位置变化
                    double[] newPonits = RotateByVector(x1, y1, z1, Unit.X, Unit.Y, Unit.Z, Radian);//算出为(-5200，3000，0),实际为(5200,-3000,0)
                    MoveX = Math.Round(px1 - newPonits[0], 2);
                    MoveY = Math.Round(py1 - newPonits[1], 2);
                    MoveZ = Math.Round(pz1 - newPonits[2], 2);
                    //左手坐标系下旋转
                    double x = Math.Round(-Unit.X);
                    double y = Math.Round(-Unit.Z);
                    double z = Math.Round(Unit.Y);
                    double C = Math.Cos(Radian);
                    double S = Math.Sin(Radian);
                    newNode.matrix = new List<double>()
                    {x*x*(1-C)+C,   x*y*(1-C)-z*S, x*z*(1-C)+y*S, 0.0,
                     x*y*(1-C)+z*S, y*y*(1-C)+C,   y*z*(1-C)-x*S, 0.0,
                     x*z*(1-C)-y*S, y*z*(1-C)+x*S, z*z*(1-C)+C,   0.0,
                     -MoveX, MoveZ, MoveY,         0.0};
                }
                else
                {
                    newNode.translation = new List<double>() { -MoveX, MoveZ, MoveY, };
                }
            }
            Nodes.AddOrUpdateCurrent(e.Id.ToString(), newNode);
            //将此节点的索引添加到根节点子数组中
            rootNode.children.Add(Nodes.CurrentIndex);
            if (isHasSimilar == true)
            {
                return RenderNodeAction.Skip;
            }


            //几何元素
            currentGeometry = new IndexedDictionary<GeometryData>();
            return RenderNodeAction.Proceed;
        }


        public void OnMaterial(MaterialNode node)
        {
            string matName;
            ElementId id = node.MaterialId;
            glTFMaterial gl_mat = new glTFMaterial();

            if (id != ElementId.InvalidElementId)
            {
                //从节点构造一个材质
                Element m = doc.GetElement(node.MaterialId);
                matName = m.Name;

                string texturePath = null;
                Asset currentAsset = null;
                try
                {
                    //写入材质名、环境反射、漫反射和透明度。
                    if (node.HasOverriddenAppearance)
                    {
                        currentAsset = node.GetAppearanceOverride();
                    }
                    else
                    {
                        currentAsset = node.GetAppearance();
                    }
                    //取得Asset中贴图信息
                    //revit 2020版本AssetProperty的[]方法改为FindByName;asset.FindByName["unifiedbitmap_Bitmap"]!!!
                    string textureFile = (FindTextureAsset(currentAsset as AssetProperty)["unifiedbitmap_Bitmap"]
                        as AssetPropertyString).Value.Split('|')[0];
                    //用Asset中贴图信息和注册表里的材质库地址得到贴图文件所在位置
                    texturePath = Path.Combine(textureFolder, textureFile.Replace("/", "\\"));
                }
                catch
                {

                }
                //构造材料
                gl_mat.name = matName;
                glTFPBR pbr = new glTFPBR();
                //第四个值是材料的Alpha覆盖率。该alphaMode属性指定如何解释alpha
                double alpha = Math.Round(node.Transparency, 2);
                if (alpha != 0)
                {
                    gl_mat.alphaMode = "BLEND";
                    gl_mat.doubleSided = "true";
                    alpha = 1 - alpha;
                }
                pbr.metallicFactor = 0f;//金属感强度
                pbr.roughnessFactor = 1f;//粗糙感强度
                gl_mat.pbrMetallicRoughness = pbr;
                Materials.AddOrUpdateCurrent(m.UniqueId, gl_mat);
                //写入贴图名称
                string textureName = string.Format("{0}.png", m.Name);



                //如果贴图文件真实存在，就复制到相应位置
                if (File.Exists(texturePath))
                {
                    string dirName = Path.GetFileNameWithoutExtension(FileName) + "(材质贴图)";
                    string dir = directory + dirName;
                    if (!Directory.Exists(dir))
                    {
                        //如果不存在就创建 dir 文件夹  
                        Directory.CreateDirectory(dir);
                    }

                    File.Copy(texturePath, Path.Combine(dir + "\\" + textureName), true);
                    glTFImage image = new glTFImage();
                    //通过 uri定位到图片资源
                    image.uri = "./" + dirName + "/" + textureName;
                    Images.AddOrUpdateCurrent(m.UniqueId, image);
                    //取样器,定义图片的采样和滤波方式
                    glTFSampler sampler = new glTFSampler();
                    sampler.magFilter = 9729;
                    sampler.minFilter = 9987;
                    sampler.wrapS = 10497;
                    sampler.wrapT = 10497;
                    Samplers.AddOrUpdateCurrent(m.UniqueId, sampler);
                    //贴图信息,使用source和ssampler指向图片和采样器
                    glTFTexture texture = new glTFTexture();
                    texture.source = Images.CurrentIndex;
                    texture.sampler = Samplers.CurrentIndex;
                    Textures.AddOrUpdateCurrent(m.UniqueId, texture);
                    //贴图索引
                    glTFbaseColorTexture bct = new glTFbaseColorTexture();
                    bct.index = Textures.CurrentIndex;
                    pbr.baseColorTexture = bct;
                }
                else
                {
                    try
                    {
                        pbr.baseColorFactor = new List<float>() { node.Color.Red / 255f, node.Color.Green / 255f, node.Color.Blue / 255f, (float)alpha / 1f };
                    }
                    catch
                    {

                    }
                }
            }
            else
            {

                string uuid = string.Format("r{0}g{1}b{2}", node.Color.Red.ToString(), node.Color.Green.ToString(), node.Color.Blue.ToString());
                // construct the material
                matName = string.Format("MaterialNode_{0}_{1}", Util.ColorToInt(node.Color), Util.RealString(node.Transparency * 100));
                gl_mat.name = matName;
                glTFPBR pbr = new glTFPBR();
                pbr.baseColorFactor = new List<float>() { node.Color.Red / 255f, node.Color.Green / 255f, node.Color.Blue / 255f, 1f };
                pbr.metallicFactor = 0f;
                pbr.roughnessFactor = 1f;
                gl_mat.pbrMetallicRoughness = pbr;
                Materials.AddOrUpdateCurrent(uuid, gl_mat);
            }
        }
        public void OnPolymesh(PolymeshTopology polymesh)
        {
            string vertex_key = Nodes.CurrentKey + "_" + Materials.CurrentKey;
            //如果vertex_key是唯一的，添加新的“current”条目
            bool isNew = currentGeometry.AddOrUpdateCurrent(vertex_key, new GeometryData());
            if (isNew == true)
            {
                index = 0;
            }
            //取得UV坐标
            IList<UV> uvs = polymesh.GetUVs();
            //顶点和索引
            Transform t = CurrentTransform;
            IList<XYZ> pts = polymesh.GetPoints();
            pts = pts.Select(p => t.OfPoint(p)).ToList();//矩阵变换
            //gltf中导出贴图时顶点数要和UV数、索引位置保持一致
            index = currentGeometry.CurrentItem.vertices.Count / 3;
            foreach (XYZ point in pts)
            {
                currentGeometry.CurrentItem.vertices.Add(Math.Round(-point.X, 2).ToString());
                currentGeometry.CurrentItem.vertices.Add(Math.Round(point.Z, 2).ToString());
                currentGeometry.CurrentItem.vertices.Add(Math.Round(point.Y, 2).ToString());
            }
            //把UV数据写入文件
            foreach (UV uv in uvs)
            {
                currentGeometry.CurrentItem.uvs.Add(Math.Round(uv.U, 2).ToString());
                currentGeometry.CurrentItem.uvs.Add(Math.Round(uv.V, 2).ToString());
            }
            //导出贴图时顶点数要和UV数保持一致   
            foreach (PolymeshFacet facet in polymesh.GetFacets())
            {
                currentGeometry.CurrentItem.index.Add(facet.V1 + index);
                currentGeometry.CurrentItem.index.Add(facet.V2 + index);
                currentGeometry.CurrentItem.index.Add(facet.V3 + index);
            }
        }

        public void OnElementEnd(ElementId elementId)
        {
            try
            {
                if ((BuiltInCategory)currentElem.Category.Id.IntegerValue == BuiltInCategory.OST_Cameras ||
                   currentElem.Category.CategoryType == CategoryType.AnalyticalModel)
                {
                    return;
                }

            }
            catch
            {
                Console.WriteLine("currentElem.Category 为null");
            }
            if (isHasSimilar == true)
            {
                Nodes.CurrentItem.mesh = Meshes.GetIndexFromUUID(currentData.SimilarObjectID);
                return;
            }
            try
            {
                //如果是rfa族文件就会 currentGeometry.CurrentIndex CurrentItem.vertices key不能为null
                if (currentGeometry.CurrentItem.vertices.Count == 0)
                {
                    return;
                }
            }
            catch
            {
                Console.WriteLine("currentGeometry.CurrentIndex CurrentItem.vertices key不能为null");
            }

            Element e = doc.GetElement(elementId);
            glTFMesh newMesh = new glTFMesh();
            newMesh.primitives = new List<glTFMeshPrimitive>();
            Meshes.AddOrUpdateCurrent(e.Id.ToString(), newMesh);
            Nodes.CurrentItem.mesh = Meshes.CurrentIndex;
            // 将currentGeometry对象转换为glTFMeshPrimitives
            foreach (KeyValuePair<string, GeometryData> kvp in currentGeometry.Dict)
            {
                glTFBinaryData elementBinary = AddGeometryMeta(kvp.Value, kvp.Key);
                binFileData.Add(elementBinary);
                string material_key = kvp.Key.Split('_')[1];
                glTFMeshPrimitive primative = new glTFMeshPrimitive();
                primative.attributes.POSITION = elementBinary.vertexAccessorIndex;
                primative.indices = elementBinary.indexAccessorIndex;
                primative.material = Materials.GetIndexFromUUID(material_key);

                //UV坐标
                primative.attributes.TEXCOORD_0 = elementBinary.uvAccessorIndex;
                Meshes.CurrentItem.primitives.Add(primative);
            }
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            _transformStack.Push(CurrentTransform.Multiply(node.GetTransform()));
            return RenderNodeAction.Proceed;
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            _transformStack.Pop();
        }

        /// <summary>
        /// Takes the intermediate geometry data and performs the calculations
        /// to convert that into glTF buffers, views, and accessors.
        /// </summary>
        /// <param name="geomData"></param>
        /// <param name="name">Unique name for the .bin file that will be produced.</param>
        /// <returns></returns>
        public glTFBinaryData AddGeometryMeta(GeometryData geomData, string name)
        {
            // add a buffer
            glTFBuffer buffer = new glTFBuffer();
            buffer.uri = name + ".bin";
            Buffers.Add(buffer);
            int bufferIdx = Buffers.Count - 1;

            /**
             * Buffer Data
             **/
            glTFBinaryData bufferData = new glTFBinaryData();
            bufferData.name = buffer.uri;
            foreach (var coord in geomData.vertices)
            {
                float vFloat = Convert.ToSingle(coord);
                bufferData.vertexBuffer.Add(vFloat);
            }
            foreach (var index in geomData.index)
            {
                bufferData.indexBuffer.Add(index);
            }
            // UV
            foreach (var UV in geomData.uvs)
            {
                float uvFloat = Convert.ToSingle(UV);
                bufferData.uvBuffer.Add(uvFloat);
            }

            //获取顶点数据的最大值和最小值
            float[] vertexMinMax = Util.GetVec3MinMax(bufferData.vertexBuffer);
            //获取面片索引数据的最大值和最小值
            int[] faceMinMax = Util.GetScalarMinMax(bufferData.indexBuffer);
            //获取uv索引数据的最大值和最小值
            float[] UVMinMax = Util.GetUVMinMax(bufferData.uvBuffer);

            /**
             * BufferViews
             **/
            // Add a vec3 buffer view
            int elementsPerVertex = 3;
            int bytesPerElement = 4;
            int bytesPerVertex = elementsPerVertex * bytesPerElement;
            int numVec3 = (geomData.vertices.Count) / elementsPerVertex;
            int sizeOfVec3View = numVec3 * bytesPerVertex;
            glTFBufferView vec3View = new glTFBufferView();
            vec3View.buffer = bufferIdx;
            vec3View.byteOffset = 0;
            vec3View.byteLength = sizeOfVec3View;
            vec3View.target = Targets.ARRAY_BUFFER;
            BufferViews.Add(vec3View);
            int vec3ViewIdx = BufferViews.Count - 1;

            // Add a faces / indexes buffer view
            int elementsPerIndex = 1;
            int bytesPerIndexElement = 4;
            int bytesPerIndex = elementsPerIndex * bytesPerIndexElement;
            int numIndexes = geomData.index.Count;
            int sizeOfIndexView = numIndexes * bytesPerIndex;
            glTFBufferView facesView = new glTFBufferView();
            facesView.buffer = bufferIdx;
            facesView.byteOffset = vec3View.byteLength;
            facesView.byteLength = sizeOfIndexView;
            facesView.target = Targets.ELEMENT_ARRAY_BUFFER;
            BufferViews.Add(facesView);
            int facesViewIdx = BufferViews.Count - 1;


            // TODO: Add a uv buffer view
            int elementsPerUV = 2;
            int bytesPerElementUV = 4;
            int bytesPerUV = elementsPerUV * bytesPerElementUV;
            int numUV = (geomData.uvs.Count) / elementsPerUV;
            int sizeOfVec3Viewuv = numUV * bytesPerUV;
            glTFBufferView UVView = new glTFBufferView();
            UVView.buffer = bufferIdx;
            UVView.byteOffset = vec3View.byteLength + facesView.byteLength;
            UVView.byteLength = sizeOfVec3Viewuv;
            UVView.target = Targets.ARRAY_BUFFER;
            BufferViews.Add(UVView);
            int uvViewIdx = BufferViews.Count - 1;

            Buffers[bufferIdx].byteLength = vec3View.byteLength + facesView.byteLength + UVView.byteLength;

            /**
             * Accessors
             **/
            // add a position accessor
            glTFAccessor positionAccessor = new glTFAccessor();
            positionAccessor.bufferView = vec3ViewIdx;
            positionAccessor.byteOffset = 0;
            positionAccessor.componentType = ComponentType.FLOAT;
            positionAccessor.count = geomData.vertices.Count / elementsPerVertex;
            //Vec3顶点或者向量
            positionAccessor.type = "VEC3";
            positionAccessor.max = new List<float>() { vertexMinMax[1], vertexMinMax[3], vertexMinMax[5] };
            positionAccessor.min = new List<float>() { vertexMinMax[0], vertexMinMax[2], vertexMinMax[4] };
            Accessors.Add(positionAccessor);
            bufferData.vertexAccessorIndex = Accessors.Count - 1;

            // add a face accessor
            glTFAccessor faceAccessor = new glTFAccessor();
            faceAccessor.bufferView = facesViewIdx;
            faceAccessor.byteOffset = 0;
            faceAccessor.componentType = ComponentType.UNSIGNED_INT;
            faceAccessor.count = numIndexes;
            //SCALAR 标量
            faceAccessor.type = "SCALAR";
            faceAccessor.max = new List<float>() { faceMinMax[1] };
            faceAccessor.min = new List<float>() { faceMinMax[0] };
            Accessors.Add(faceAccessor);
            bufferData.indexAccessorIndex = Accessors.Count - 1;

            // add a UV accessor
            glTFAccessor uvsAccessor = new glTFAccessor();
            uvsAccessor.bufferView = uvViewIdx;
            uvsAccessor.byteOffset = 0;
            uvsAccessor.componentType = ComponentType.FLOAT;
            uvsAccessor.count = numUV;
            //Vec2 是uv坐标
            uvsAccessor.type = "VEC2";
            uvsAccessor.max = new List<float>() { UVMinMax[1], UVMinMax[3] };
            uvsAccessor.min = new List<float>() { UVMinMax[0], UVMinMax[2] };
            Accessors.Add(uvsAccessor);
            bufferData.uvAccessorIndex = Accessors.Count - 1;


            return bufferData;
        }

        public bool IsCanceled()
        {
            // This method is invoked many times during the export process.
            return false;
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnViewEnd(ElementId elementId)
        {
            // do nothing
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            _transformStack.Push(CurrentTransform.Multiply(node.GetTransform()));
            return RenderNodeAction.Proceed;
        }

        public void OnLinkEnd(LinkNode node)
        {
            _transformStack.Pop();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            return RenderNodeAction.Proceed;
        }

        public void OnFaceEnd(FaceNode node)
        {
            // This method is invoked only if the  custom exporter was set to include faces.
        }

        public void OnRPC(RPCNode node)
        {
            // do nothing
        }

        public void OnLight(LightNode node)
        {
            // do nothing
        }


        public void getitem(ObjectData Root, List<string> Names)
        {
            string Name = Names.First();
            Names.RemoveAt(0);
            if (Root.Children.FirstOrDefault(t => t.ElementName == Name) == null)
            {
                ObjectData ChildrenData = new ObjectData();
                ChildrenData.ElementName = Name;
                Root.Children.Add(ChildrenData);
                if (Names.Count > 0)
                {
                    getitem(ChildrenData, Names);
                }
                else
                {
                    InstanceNameData = ChildrenData;
                }
            }
            else
            {
                ObjectData ChildrenData = Root.Children.FirstOrDefault(t => t.ElementName == Name);
                if (Names.Count > 0)
                {
                    getitem(ChildrenData, Names);
                }
                else
                {
                    InstanceNameData = ChildrenData;
                }
            }
        }

        public void GetPoints(Element element)
        {
            List<string> points = new List<string>();
            Options options = new Options();
            GeometryElement geometry = element.get_Geometry(options);
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid)
                {
                    Solid solid = obj as Solid;
                    DownEdge(solid);
                    if (center != null)
                    {
                        break;
                    }
                }
                else  //取得族实例几何信息的方法
                {
                    GeometryInstance geoInstance = obj as GeometryInstance;
                    GeometryElement geoElement = geoInstance.GetInstanceGeometry();
                    foreach (GeometryObject obj2 in geoElement)
                    {
                        Solid solid = obj2 as Solid;
                        DownEdge(solid);
                        if (center != null)
                        {
                            break;
                        }

                    }
                }
            }
        }
        public void DownEdge(Solid solid)
        {

            if (solid != null)
            {
                FaceArray faceArray = solid.Faces;
                foreach (Face face in faceArray)
                {
                    PlanarFace pf = face as PlanarFace;
                    if (pf != null && Math.Round(pf.FaceNormal.Z, 2) < 0)
                    {
                        EdgeArrayArray edgeArrays = face.EdgeLoops;
                        foreach (EdgeArray edges in edgeArrays)
                        {
                            List<string> ALLpoints = new List<string>();
                            foreach (Edge edge in edges)
                            {
                                List<string> points = new List<string>();
                                foreach (XYZ point in edge.Tessellate())
                                {
                                    points.Add(point.Point());
                                    ALLpoints.Add(point.Point());
                                }
                                //double LineLongX = (edge.Tessellate()[1].X - edge.Tessellate()[0].X) * 304.8;
                                //double LineLongY = (edge.Tessellate()[1].Y - edge.Tessellate()[0].Y) * 304.8;
                                //double LineLongZ = (edge.Tessellate()[1].Z - edge.Tessellate()[0].Z) * 304.8;
                                //EdgesPoints.AddOrUpdateCurrent
                                //    (Math.Round(Math.Sqrt(LineLongX * LineLongX + LineLongY * LineLongY + LineLongZ * LineLongZ)).ToString(), points);
                            }
                            //去除重复顶点
                            HashSet<string> NeWpoints = new HashSet<string>(ALLpoints);
                            center = GetCenter(NeWpoints.ToList());
                            //currentData.ElementLocation.Add(GetCenter(NeWpoints.ToList());
                            //EdgesPoints.AddOrUpdateCurrent("中心点", new List<string> { (GetCenter(NeWpoints.ToList())) });
                            //EdgesPoints.AddOrUpdateCurrent("向量", new List<string> { (-pf.FaceNormal).Point() });
                            break;
                        }
                        break;
                    }
                }
            }
        }

        public string GetCenter(List<string> PLPoint)
        {
            double SumX = 0;
            double SumY = 0;
            double SumZ = 0;
            for (int i = 0; i < PLPoint.Count; i++)
            {
                string[] Points = PLPoint[i].Split(new char[] { ',' });

                SumX += Convert.ToDouble(Points[0]);
                SumY += Convert.ToDouble(Points[1]);
                SumZ += Convert.ToDouble(Points[2]);
            }
            double Xc = SumX / (PLPoint.Count);
            double Yc = SumY / (PLPoint.Count);
            double Zc = SumZ / (PLPoint.Count);
            return string.Format("{0},{1},{2}", Xc, Yc, Zc);
        }
        public double[] RotateByVector(double old_x, double old_y, double old_z, double vx, double vy, double vz, double theta)
        {
            double[] NewPoint = new double[3];
            double c = Math.Cos(theta);
            double s = Math.Sin(theta);
            NewPoint[0] = (vx * vx * (1 - c) + c) * old_x + (vx * vy * (1 - c) - vz * s) * old_y + (vx * vz * (1 - c) + vy * s) * old_z;
            NewPoint[1] = (vy * vx * (1 - c) + vz * s) * old_x + (vy * vy * (1 - c) + c) * old_y + (vy * vz * (1 - c) - vx * s) * old_z;
            NewPoint[2] = (vx * vz * (1 - c) - vy * s) * old_x + (vy * vz * (1 - c) + vx * s) * old_y + (vz * vz * (1 - c) + c) * old_z;
            return NewPoint;
        }

        /// <summary>
        /// 自定义方法，用递归找到包含贴图信息：
        /// </summary>
        /// <param name="ap"></param>
        /// <returns></returns>
        private Asset FindTextureAsset(AssetProperty ap)
        {
            Asset result = null;
            if (ap.Type == AssetPropertyType.Asset)
            {
                if (!IsTextureAsset(ap as Asset))
                {
                    for (int i = 0; i < (ap as Asset).Size; i++)
                    {
                        if (null != FindTextureAsset((ap as Asset)[i]))
                        {
                            result = FindTextureAsset((ap as Asset)[i]);
                            break;
                        }
                    }
                }
                else
                {
                    result = ap as Asset;
                }
                return result;
            }
            else
            {
                for (int j = 0; j < ap.NumberOfConnectedProperties; j++)
                {
                    if (null != FindTextureAsset(ap.GetConnectedProperty(j)))
                    {
                        result = FindTextureAsset(ap.GetConnectedProperty(j));
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// 自定义方法，判断Asset是否包含贴图信息：
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        private bool IsTextureAsset(Asset asset)
        {
            AssetProperty assetProprty = GetAssetProprty(asset, "assettype");
            if (assetProprty != null && (assetProprty as AssetPropertyString).Value == "texture")
            {
                return true;
            }
            return GetAssetProprty(asset, "unifiedbitmap_Bitmap") != null;
        }

        /// <summary>
        /// 自定义方法，根据名字获取对应的AssetProprty：
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        private AssetProperty GetAssetProprty(Asset asset, string propertyName)
        {
            for (int i = 0; i < asset.Size; i++)
            {
                if (asset[i].Name == propertyName)
                {
                    return asset[i];
                }
            }
            return null;
        }
    }
}
