using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Finding
{
    /// <summary>
    /// elastic search操作类
    /// </summary>
    class ESHelper
    {
        public static ElasticClient client;
        private const string DEFAULT_INDEX = "finding_fang";

        /// <summary>
        /// 初始化连接
        /// </summary>
        public static void InitES()
        {
            var node = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(node).DefaultIndex(DEFAULT_INDEX);
            client = new ElasticClient(settings);
        }

        /// <summary>
        /// 查询数据
        /// </summary>
        public void QueryDocument(string key)
        {
            var response = client.Search<RecordFile>(s => s.From(0).Size(10)
                .Index(DEFAULT_INDEX)
                .Query(q => q.Match(
                    mq => mq.Field(f => f.Content).Query(key)
                    )
                )
            );
            Console.WriteLine(JsonConvert.SerializeObject(response.Documents));
        }
    }
}
