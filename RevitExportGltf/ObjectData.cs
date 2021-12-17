using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RevitExportGltf
{
    class ObjectData : INotifyPropertyChanged
    {

        /// <summary>
        /// 子项
        /// </summary>
        private ObservableCollection<ObjectData> _children = new ObservableCollection<ObjectData>();
        public ObservableCollection<ObjectData> Children
        {
            get { return _children; }
            set
            {
                _children = value;
            }
        }

        public ObjectData()
        {
            _children = new ObservableCollection<ObjectData>();
            _isExpanded = false;
            _isChecked = true;
            _isEnabled = true;
        }

        public string ElementName { set; get; }
        public string ElementId { set; get; }
        public string SimilarObjectID { set; get; }
        public List<string> ElementLocation { set; get; }
        public string ElementVertices { set; get; }
        public string ElementNormal { set; get; }
        public string ElementArea { set; get; }
        public string ElementVolum { set; get; }
        public string VertexIndices { set; get; }





        public string SimilarToJson()
        {
            string s = string.Format
              ("\n \"ElementLocation\":{0},"
              + "\n \"ElementNormal\":{1}",
               "\"" + ElementLocation + "\"",
               "\"" + ElementNormal + "\"");
            return "\n{" + s + "\n}" + ",";
        }
        public string CurrentToJson()
        {
            string s = string.Format
              ("\n \"ElementName\":{0},"
              + "\n \"ElementId\":{1},"
              + "\n \"ElementLocation\":{2},"
              + "\n \"ElementVertices\":{3},"
              + "\n \"VertexIndices\":{4},"
              + "\n \"ElementNormal\":{5},"
              + "\n \"ElementArea\":{6},"
              + "\n \"ElementVolum\":{7},"
              + "\n \"SimilarObject\":{8}",
               "\"" + ElementName + "\"",
               "\"" + ElementId + "\"",
               "\"" + ElementLocation + "\"",
               "\"" + ElementVertices + "\"",
               "\"" + VertexIndices + "\"",
               "\"" + ElementNormal + "\"",
               "\"" + ElementArea + "\"",
               "\"" + ElementVolum + "\"",
               SimilarEleJson());
            return "\n{" + s + "\n}" + ",";
        }
        public string SimilarEleJson()
        {
            string s = null;
            foreach (ObjectData child in Children)
            {
                s += child.SimilarToJson();
            }
            return "[" + s + "\n]";
        }

        public string ToJson()
        {
            //string s = string.Format
            //  ("\n \"Name\":{0},"
            //  + "\n \"Children\":{1}",
            //   "\"" + ElementName + "\"",
            //   ChildrenToJson2());
            //return "\n{" + s + "\n}"+",";

            string s = null;
            s += CurrentToJson();
            return s;
        }





        /// <summary>
        /// 选中状态
        /// </summary>
        public bool _isChecked;
        public bool IsChecked
        {
            get
            {
                return _isChecked;
            }
            set
            {
                if (value != _isChecked)
                {
                    _isChecked = value;
                    NotifyPropertyChanged("IsChecked");
                    if (_isChecked)
                    {
                        foreach (ObjectData child in Children)
                        {
                            child.IsEnabled = true;
                        }
                    }
                    else
                    {
                        foreach (ObjectData child in Children)
                        {
                            child.IsEnabled = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 可见行
        /// </summary>
        private bool _isEnabled;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    NotifyPropertyChanged("IsEnabled");
                    if (_isEnabled)
                    {
                        foreach (ObjectData child in Children)
                        {
                            child.IsEnabled = true;
                        }
                    }
                    else
                    {
                        foreach (ObjectData child in Children)
                        {
                            child.IsEnabled = false;
                        }
                    }


                }
            }
        }
        /// <summary>
        /// 折叠状态
        /// </summary>
        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value != _isExpanded)
                {
                    //折叠状态改变
                    _isExpanded = value;
                    NotifyPropertyChanged("IsExpanded");
                }
            }
        }

        /// <summary>
        /// 设置所有子项的选中状态
        /// </summary>
        /// <param name="isChecked"></param>
        public void SetChildrenChecked(bool isChecked)
        {
            foreach (ObjectData child in Children)
            {
                child.IsChecked = IsChecked;
                child.SetChildrenChecked(IsChecked);
            }
        }

        /// <summary>
        /// 设置所有子项展开状态
        /// </summary>
        /// <param name="isExpanded"></param>
        public void SetChildrenExpanded(bool isExpanded)
        {
            foreach (ObjectData child in Children)
            {
                child.IsExpanded = isExpanded;
                child.SetChildrenExpanded(isExpanded);
            }
        }

        /// <summary>
        /// 属性改变事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
    }
}
