using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finding
{
    [ElasticsearchType(RelationName = "file")]
    class RecordFile
    {
        public string Name { get; set; }
        public string Content { get; set; }
        // 绝对路径
        public string AbsPath { get; set; }
    }
}
