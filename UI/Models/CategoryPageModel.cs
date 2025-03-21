﻿using Core.Models.Db;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UI.Models.Category;

namespace UI.Models
{
    public class CategoryPageModel : ModelBase
    {
        private ObservableCollection<CategoryModel> Data_;
        public ObservableCollection<CategoryModel> Data
        {
            get { return Data_; }
            set { Data_ = value; OnPropertyChanged(); }
        }

        private CategoryModel SelectedAppCategoryItem_;
        public CategoryModel SelectedAppCategoryItem
        {
            get { return SelectedAppCategoryItem_; }
            set
            {
                if (value != null && value.Data != null)
                {
                    SelectedAppCategoryItem_ = value;
                    OnPropertyChanged();
                }
            }
        }


        private bool EditVisibility_ = false;
        public bool EditVisibility { get { return EditVisibility_; } set { EditVisibility_ = value; OnPropertyChanged(); } }

        private bool IsCreate_ = false;
        /// <summary>
        /// 是否是创建分类
        /// </summary>
        public bool IsCreate { get { return IsCreate_; } set { IsCreate_ = value; OnPropertyChanged(); } }

        private string EditName_;
        /// <summary>
        /// 编辑分类名称
        /// </summary>
        public string EditName { get { return EditName_; } set { EditName_ = value; OnPropertyChanged(); } }

        private string EditIconFile_;
        /// <summary>
        /// 编辑分类图标
        /// </summary>
        public string EditIconFile
        {
            get { return EditIconFile_; }
            set { EditIconFile_ = value; OnPropertyChanged(); }
        }
        private bool EditIsDirectoryMath_;
        /// <summary>
        /// 是否启用目录匹配
        /// </summary>
        public bool EditIsDirectoryMath { get { return EditIsDirectoryMath_; } set { EditIsDirectoryMath_ = value; OnPropertyChanged(); } }

        /// <summary>
        /// 匹配目录
        /// </summary>
        public ObservableCollection<string> EditDirectories { get; set; } = new();
        private string EditSelectedDirectory_;
        /// <summary>
        /// 当前列表选择目录
        /// </summary>
        public string EditSelectedDirectory { get { return EditSelectedDirectory_; } set { EditSelectedDirectory_ = value; OnPropertyChanged(); } }
        private string EditErrorText_;
        public string EditErrorText
        {
            get { return EditErrorText_; }
            set { EditErrorText_ = value; OnPropertyChanged(); }
        }
        private bool IsEditError_ = false;
        public bool IsEditError { get { return IsEditError_; } set { IsEditError_ = value; OnPropertyChanged(); } }

        private string EditColor_;
        public string EditColor { get { return EditColor_; } set { EditColor_ = value; OnPropertyChanged(); } }

        public class WebCategoryModel
        {
            public WebSiteCategoryModel Data { get; set; }
            public int Count { get; set; }
        }

        private bool IsSysCategory_;


        private bool IsRightClickSelected_;

        public bool IsSelectedSysCategory { get => IsRightClickSelected_; set { IsRightClickSelected_ = value; OnPropertyChanged(); } }

        public bool IsSysCategory { get => IsSysCategory_; set { IsSysCategory_ = value; OnPropertyChanged(); } }

        private ObservableCollection<WebCategoryModel> WebCategoryData_;
        public ObservableCollection<WebCategoryModel> WebCategoryData { get { return WebCategoryData_; } set { WebCategoryData_ = value; OnPropertyChanged(); } }

        private WebCategoryModel SelectedWebCategoryItem_;
        public WebCategoryModel SelectedWebCategoryItem { get { return SelectedWebCategoryItem_; } set { SelectedWebCategoryItem_ = value; OnPropertyChanged(); } }
    }
}
