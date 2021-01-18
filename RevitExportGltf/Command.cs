/**************************************************************************
Copyright:MyCompany

Author: chenwy

Date:2020-7-31

Description:Provide Revit export 3D file in gltf format

HistoryRecord:
**************************************************************************/

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace RevitExportGltf
{
    [Transaction(TransactionMode.Manual)]
    class Command : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //以导出当前视图
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;
            Document doc = uidoc.Document;

            //没打开文档
            if (null == doc)
            {
                message = "Please open the project.";
                return Result.Failed;
            }

            //没有打开文档
            if (null == uidoc)
            {
                message = "Please run this command in an active project document.";
                return Result.Failed;
            }

            //3D视图下
            View3D view = doc.ActiveView as View3D;
            if (null == view)
            {
                message = "Please run this command in a 3D view.";
                return Result.Failed;
            }

            //保存导出的文件 包括过滤器gltf与glb格式
            System.Windows.Forms.SaveFileDialog sdial = new System.Windows.Forms.SaveFileDialog();
            sdial.Filter = "gltf|*.gltf|glb|*.glb";
            if (sdial.ShowDialog() == DialogResult.OK)
            {
                string filename = sdial.FileName;
                string directory = Path.GetDirectoryName(filename) + "\\"; ;
                //默认值减面为等级8
                int combobox_value = 8;
                //拿到revit的doc  CustomExporter 用户自定义导出
                RevitExportGltfContext context = new RevitExportGltfContext(doc, sdial.FileName, combobox_value);
                using (CustomExporter exporter = new CustomExporter(doc, context))
                {
                    //是否包括Geom对象
                    exporter.IncludeGeometricObjects = false;
                    exporter.ShouldStopOnError = true;
                    //导出3D模型
                    exporter.Export(view);
                }

                System.Diagnostics.Process p = new System.Diagnostics.Process();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
                p.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
                p.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
                p.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
                p.StartInfo.CreateNoWindow = true;//不显示程序窗口
                p.Start();//启动程序
                //使用gltf pipeline命令行工具
                //向cmd窗口发送输入信息  （node.js已经是配置好了系统环境变量）
                //string str = @"cd D:\cmder";
                //p.StandardInput.WriteLine(str);

                //将GLTF转换为glb二进制 压缩纹理与bin顶点
                string glbName = Path.GetFileNameWithoutExtension(sdial.FileName) + "(Draco)" + ".glb";
                string glbstr = string.Format("gltf-pipeline.cmd gltf-pipeline -i {0} -o {1}", sdial.FileName, Path.GetDirectoryName(sdial.FileName) + "\\" + glbName);
                p.StandardInput.WriteLine(glbstr);


                //gltf-pipeline.c md gltf-pipeline -i model.gltf -o modelDraco.gltf -d
                //运用Draco算法将GLTF压缩  压缩纹理与bin顶点是json文件
                string gltfDracoName = Path.GetFileNameWithoutExtension(sdial.FileName) + "(Draco)" + ".gltf";
                string gltfDraco = string.Format("gltf-pipeline.cmd gltf-pipeline -i {0} -o {1} -d", sdial.FileName, Path.GetDirectoryName(sdial.FileName) + "\\" + gltfDracoName);
                p.StandardInput.WriteLine(gltfDraco);

                //gltf - pipeline - i model.gltf - t
                //压缩bin二进制为base64编码，但是保留纹理
                string gltfTextureName = Path.GetFileNameWithoutExtension(sdial.FileName) + "(Texture)" + ".gltf";
                string gltfTexture = string.Format("gltf-pipeline.cmd gltf-pipeline -i {0}  -o {1} -t", sdial.FileName, Path.GetDirectoryName(sdial.FileName) + "\\" + gltfTextureName);
                p.StandardInput.WriteLine(gltfTexture);

                p.StandardInput.AutoFlush = true;
                p.StandardInput.WriteLine("exit");

                //获取cmd窗口的输出信息
                string output = p.StandardOutput.ReadToEnd();
                System.Windows.MessageBox.Show(output);
            }
            return Result.Succeeded;
        }

    }
}
