# Revit导出gltf工具说明文档

### 1.性能优化-使用压缩工具

推荐使用gltf或者glb来加载模型。如果有大量外部模型，一定要结合使用gltf-pipeline与Draco。
这很重要，有时候我们获得的gltf模型文件后，我们可以轻易的压缩图片；但有些scene.bin文件可以达到100M或200M以上，模型多了之后会很大程度影响使用体验。
结合使用gltf-pipeline与Draco，可以有效的压缩文件甚至在10M以下！

原因：为什么需要压缩gltf，原始的gltf格式包含三部分”gltf格式文件“、”bin二进制几何数据“、”纹理贴图“；3D模型的顶点压缩文件会体积减少但是GPU压力变大！纹理贴图压缩会导致体积变大 但是更方便传输等等。  

##### Draco简介

 Draco 由谷歌 Chrome 媒体团队设计，旨在大幅加速 3D 数据的编码、传输和解码。因为研发团队的 Chrome 背景，这个开源算法的首要应用对象是浏览器。但既然谷歌把它开源，现在全世界的开发者可以去探索 Draco 在其他场景的应用，比如说非网页端。目前，谷歌提供了它的两个版本： JavaScript 和 C++。

 Draco 可以被用来压缩 mesh 和点云数据。它还支持压缩点（ compressing points），连接信息，纹理协调，颜色信息，法线（ normals）以及其他与几何相关的通用属性。谷歌官方发布的 Draco Mesh 文件压缩率，它大幅优于 ZIP。

 谷歌宣称，若使用 Draco，含 3D 图像的应用，其文件大小能大幅缩小，并不在视觉保真度上做妥协。对于用户来说，这意味着 app/PC桌面应用 下载会更快，浏览器的 3D 图像载入得更快，VR 和 AR 画面的传输只需要占用原先一小部分的带宽、渲染得更快并且看起来画质清晰。

 同时Cesium1.44开始支持解析draco压缩算法的gltf/glb模型，将模型使用具有draco算法的工具进行压缩，例如blender等，Cesium加载模型时，进行模型的解析。
 对比使用draco算法压缩的模型，模型的数据量变小了相当多，这样会提高网络上的传输速度。在3D Tiles的大批量模型中，使用这一算法进行提前压缩，可以大大的减少网络传输的数据量。
 [Draco压缩gltf与3Dtiles的Mesh](https://cesium.com/blog/2018/04/09/draco-compression/)

### 2.压缩工具的安装

这里使用的是gltf-pipeline开源压缩工具，这个现阶段使用很多也是非常的成熟的工具；由于gltf-pipeline是依赖于node.js的环境所以需要先安装node.js。

##### 1.安装node.js

[下载地址](https://nodejs.org/zh-cn/#home-downloadhead)
[安装教程](https://www.runoob.com/nodejs/nodejs-install-setup.html)

*特别说明npm工具已经在node.js 的windows中继承了不需要安装*

---

##### 2.安装gltf-pipeline压缩工具

```shell
npm install -g gltf-pipeline
```
然后配置为系统环境就是要在CMD命令下能运行以下的命令：
Converting a glTF to glb

```shell
gltf-pipeline -i model.gltf -o model.glb
gltf-pipeline -i model.gltf -b
```

Converting a glTF to Draco glTF
```shell
gltf-pipeline -i model.gltf -o modelDraco.gltf -d
```
---

Saving separate textures
```shell
gltf-pipeline -i model.gltf -o modelTexture.gltf -t
```

##### 3.Revit导出Gltf
先打开Revit->打开某个项目->切换为3D视图->附加模块->外部工具->点击add-in manager ->选择对应的loaded command ->双击运行或者点击底下“Run”按钮

### 4.导出模型生成的文件

如果运行成功会在选定的目录下生成如下的文件，其中各个文件的作用如下：

生成的gltf格式文件：
|  文件名称 | 文件类型 | 说明 |
|  :----    | :----   | :---- | :---- | :---- |
| model  | gltf | 这是标准的gltf文件通过images 与buffers分别的uri来获取同层目录 下的bin与带纹理贴图的材质|
| bin  | binary file | 主要是gltf中保存的几何数据包括顶点、顶点索引、uv坐标、法向量坐标等等 |
| model(材质贴图)  | png | 主要是gltf中需要链接到的带有纹理贴图的材质 |
| model(Texture)  | gltf |原model.gltf文件把.bin文件进行顶点的压缩进gltf中去但是保留了带有纹理贴图的材质注意如果模型没有纹理贴图的就没有该文件夹 |
| model(Draco)  | gltf |原model.gltf文件把.bin文件进行顶点的压缩进gltf中同时也对纹理进行压缩进gltf并且使用base64编码表示 ，特别注意gltf它本身是json文件|
| model(Draco)  | glb |与model(Draco).gltf相似但是 glb不再是json而是binary file保存|

### 5.如何打开gltf格式的3D模型

- 可以使用VScode打开，只需要安装vscode 的**gltf  tools**插件就可以；
- 同时也可以安装官方推荐查看器[gltf-view](https://github.com/donmccurdy/three-gltf-viewer/releases/tag/v1.5.1)