using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.Db
{
    /// <summary>
    /// 网页链接模型
    /// </summary>
    /// 
    [Table("WebUrlModels")]
    public class WebUrlModel
    {
        [Key]
        public int ID { get; set; }
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? IconFile { get; set; }
    }
}
