using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitExportGltf
{
    #region glTF格式组成
    /// <summary>
    /// The json serializable glTF file format.
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0
    /// </summary>
    public struct glTF
    {
        public glTFVersion asset;
        public List<glTFScene> scenes;
        public List<glTFNode> nodes;
        public List<glTFMesh> meshes;
        public List<glTFBuffer> buffers;
        public List<glTFBufferView> bufferViews;
        public List<glTFAccessor> accessors;
        public List<glTFMaterial> materials;
        public List<glTFTexture> textures;
        public List<glTFImage> images;
        public List<glTFSampler> samplers;
    }

    /// <summary>
    /// 版本
    /// </summary>
    public class glTFVersion
    {
        public string version = "2.0";
    }
    /// <summary>
    /// 场景
    /// </summary>
    public class glTFScene
    {
        public List<int> nodes = new List<int>();
    }

    /// <summary>
    ///场景中的节点
    /// </summary>
    public class glTFNode
    {
        public string name { get; set; }
        /// <summary>
        /// mesh个数
        /// </summary>
        public int? mesh { get; set; } = null;
        /// <summary>
        /// 矩阵
        /// </summary>
        public List<double> matrix { get; set; }


        /// <summary>
        /// 旋转
        /// </summary>
        public List<double> rotation { get; set; }

        /// <summary>
        /// 平移
        /// </summary>
        public List<double> translation { get; set; }


        /// <summary>
        /// 
        /// </summary>
        public List<int> children { get; set; }
        /// <summary>
        ///附加属性
        /// </summary>
        // public glTFExtras extras { get; set; }
    }


    /// <summary>
    /// 网格
    /// </summary>
    public class glTFMesh
    {
        public List<glTFMeshPrimitive> primitives { get; set; }
    }


    /// <summary>
    /// 属性定义GPU应该在哪里寻找网格和材质数据。
    /// </summary>
    public class glTFMeshPrimitive
    {
        public glTFAttribute attributes { get; set; } = new glTFAttribute();
        public int indices { get; set; }
        public int? material { get; set; } = null;
        public int mode { get; set; } = 4; // 4 is triangles
    }

    public class glTFAttribute
    {
        /// <summary>
        ///位置数据访问器的索引。
        /// </summary>
        public int POSITION { get; set; }

        /// <summary>
        /// 第一组UV坐标
        /// </summary>
        public int TEXCOORD_0 { get; set; }

        //public int NORMAL { get; set; }
    }

    /// <summary>
    ///对二进制数据的位置和大小的引用。
    /// </summary>
    public class glTFBuffer
    {
        /// <summary>
        /// The uri of the buffer.
        /// </summary>
        public string uri { get; set; }
        /// <summary>
        /// The total byte length of the buffer.
        /// </summary>
        public int byteLength { get; set; }
    }

    /// <summary>
    /// 对包含矢量或标量数据的缓冲区的分段的引用。
    /// </summary>
    public class glTFBufferView
    {
        /// <summary>
        /// 缓冲区的索引。
        /// </summary>
        public int buffer { get; set; }
        /// <summary>
        /// 缓冲区的偏移量(以字节为单位)。
        /// </summary>
        public int byteOffset { get; set; }
        /// <summary>
        /// bufferView的长度，以字节为单位。
        /// </summary>
        public int byteLength { get; set; }
        /// <summary>
        /// GPU缓冲区应该绑定到的目标。
        /// </summary>
        public Targets target { get; set; }
        /// <summary>
        /// A user defined name for this view.
        /// </summary>
        public string name { get; set; }
    }

    /// <summary>
    /// 逻辑数字来区分标量和矢量数组buff。
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#buffers-and-buffer-views
    /// </summary>
    public enum Targets
    {
        ARRAY_BUFFER = 34962, // signals vertex data
        ELEMENT_ARRAY_BUFFER = 34963 // signals index or face data
    }

    /// <summary>
    /// 逻辑数值以区分数组buff组件类型。
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#accessor-element-size
    /// </summary>
    public enum ComponentType
    {
        BYTE = 5120,
        UNSIGNED_BYTE = 5121,
        SHORT = 5122,
        UNSIGNED_SHORT = 5123,
        UNSIGNED_INT = 5125,
        FLOAT = 5126
    }


    /// <summary>
    /// 对包含特定数据类型的BufferView分段的引用。
    /// </summary>
    public class glTFAccessor
    {
        /// <summary>
        ///缓冲视图的索引
        /// </summary>
        public int bufferView { get; set; }
        /// <summary>
        /// 相对于bufferView开始的偏移量(以字节为单位)。
        /// </summary>
        public int byteOffset { get; set; }
        /// <summary>
        /// 属性中组件的数据类型
        /// </summary>
        public ComponentType componentType { get; set; }
        /// <summary>
        /// 此访问器引用的属性数量。
        /// </summary>
        public int count { get; set; }
        /// <summary>
        /// 指定属性是scala、向量还是矩阵
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// 此属性中每个组件的最大值。
        /// </summary>
        public List<float> max { get; set; }
        /// <summary>
        /// 此属性中每个组件的最小值。
        /// </summary>
        public List<float> min { get; set; }
        /// <summary>
        /// 此访问器的用户定义名称。
        /// </summary>
        public string name { get; set; }
    }


    /// <summary>
    /// 材质
    /// </summary>
    public class glTFMaterial
    {
        public string name { get; set; }
        public glTFPBR pbrMetallicRoughness { get; set; }
        public string alphaMode { get; set; }
        public string doubleSided { get; set; }
    }


    public class glTFPBR
    {
        //材质贴图
        public glTFbaseColorTexture baseColorTexture { get; set; }

        //材质颜色索引
        public List<float> baseColorFactor { get; set; }
        //材质金属性
        public float metallicFactor { get; set; }
        //材质粗糙度
        public float roughnessFactor { get; set; }
    }


    public class glTFbaseColorTexture
    {
        //贴图索引
        public int? index { get; set; } = null;
    }


    /// <summary>
    /// 每个texture对象可以用于多个材质对象
    /// </summary>
    public class glTFTexture
    {
        /// <summary>
        /// glTFImage的索引号
        /// </summary>
        public int? source { get; set; } = null;
        /// <summary>
        /// glTFSampler的索引号
        /// </summary>
        public int? sampler { get; set; } = null;

    }

    public class glTFImage
    {
        public string uri { get; set; }

    }

    public class glTFSampler
    {
        public float magFilter { get; set; }

        public float minFilter { get; set; }
        public float wrapS { get; set; }
        public float wrapT { get; set; }
    }


    #endregion


    #region bin文件
    /// <summary>
    /// A binary data store serialized to a *.bin file
    /// https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#binary-data-storage
    /// </summary>
    public class GeometryData
    {
        public List<string> vertices = new List<string>();
        public List<double> normals = new List<double>();
        public List<string> uvs = new List<string>();
        public List<int> index = new List<int>();
    }


    /// <summary>
    /// *.bin文件
    /// </summary>
    public class glTFBinaryData
    {
        public List<float> vertexBuffer { get; set; } = new List<float>();
        public List<int> indexBuffer { get; set; } = new List<int>();
        //public List<float> normalBuffer { get; set; } = new List<float>();

        public List<float> uvBuffer { get; set; } = new List<float>();
        public int vertexAccessorIndex { get; set; }
        public int indexAccessorIndex { get; set; }

        public int uvAccessorIndex { get; set; }

        //public int normalsAccessorIndex { get; set; }
        public string name { get; set; }
    }
    #endregion

}
