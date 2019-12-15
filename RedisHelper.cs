using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Finding
{
    /// <summary>
    /// redis操作类
    /// </summary>
    class RedisHelper
    {
        private static string Connstr = "";
        private static object _locker = new object();
        private static ConnectionMultiplexer _instance = null;


        static RedisHelper()
        {
        }

        /// <summary>
        /// 使用一个静态属性来返回已连接的实例
        /// </summary>
        public static ConnectionMultiplexer Instance
        {
            get
            {
                if (Connstr.Length == 0)
                {
                    throw new Exception("未配置redis连接！");
                }
                if (_instance == null)
                {
                    lock (_locker)
                    {
                        if (_instance == null || !_instance.IsConnected)
                        {
                            _instance = ConnectionMultiplexer.Connect(Connstr);
                        }
                    }
                }
                return _instance;
            }
        }


        public static void SetCon(string config)
        {
            Connstr = config;
        }

        public static IDatabase GetDatabase()
        {
            return Instance.GetDatabase();
        }

        /// <summary>
        /// 拼接Key的前缀
        /// </summary>
        /// <returns></returns>
        private static string MergeKey(string key)
        {
            return key;
        }

        /// <summary>
        /// 根据key获取缓存对象
        /// </summary>
        public static T Get<T>(string key)
        {
            key = MergeKey(key);
            return Deserialize<T>(GetDatabase().StringGet(key));
        }

        /// <summary>
        /// 根据key获取缓存对象
        public static object Get(string key)
        {
            key = MergeKey(key);
            return Deserialize<object>(GetDatabase().StringGet(key));
        }

        /// <summary>
        /// 设置缓存
        /// 直接存取二进制
        /// </summary>
        public static void Set(string key, object value, int expireMinutes = 0)
        {
            key = MergeKey(key);
            if (expireMinutes > 0)
            {
                GetDatabase().StringSet(key, Serialize(value), TimeSpan.FromMinutes(expireMinutes));
            }
            else
            {
                GetDatabase().StringSet(key, Serialize(value));
            }
        }

        /// <summary>
        /// 获取所有键
        /// </summary>
        /// <param name="prefix">键前缀</param>
        public static List<string> GetAllKeys(string prefix)
        {
            var endPoints = Instance.GetEndPoints();
            List<string> keyList = new List<string>();
            foreach (var ep in endPoints)
            {
                var server = _instance.GetServer(ep);
                var keys = server.Keys(0, prefix + "*");
                foreach (var item in keys)
                {
                    keyList.Add((string)item);
                }
            }
            return keyList;
        }

        /// <summary>
        /// 判断在缓存中是否存在该key的缓存数据
        /// </summary>
        public static bool Exists(string key)
        {
            key = MergeKey(key);
            return GetDatabase().KeyExists(key);
        }

        /// <summary>
        /// 移除指定key的缓存
        /// </summary>
        public static bool Remove(string key)
        {
            key = MergeKey(key);
            return GetDatabase().KeyDelete(key);
        }

        /// <summary>
        /// 存入列表
        /// </summary>
        public static void ListPush(string key, object value)
        {
            GetDatabase().ListRightPush(key, Serialize(value));
        }

        /// <summary>
        /// 取出列表
        /// </summary>
        public static List<T> ListGet<T>(string key)
        {
            var vList = GetDatabase().ListRange(key);
            List<T> result = new List<T>();
            foreach (var item in vList)
            {
                T model = Deserialize<T>(item);
                result.Add(model);
            }
            return result;
        }


        /// <summary>
        /// 异步设置
        /// </summary>
        public static async Task SetAsync(string key, object value)
        {
            key = MergeKey(key);
            await GetDatabase().StringSetAsync(key, Serialize(value));
        }

        /// <summary>
        /// 根据key获取缓存对象
        /// </summary>
        public static async Task<object> GetAsync(string key)
        {
            key = MergeKey(key);
            object value = await GetDatabase().StringGetAsync(key);
            return value;
        }


        /// <summary>
        /// 序列化对象
        /// </summary>
        private static byte[] Serialize(object o)
        {
            if (o == null)
            {
                return null;
            }
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (MemoryStream memoryStream = new MemoryStream())
            {
                binaryFormatter.Serialize(memoryStream, o);
                byte[] objectDataAsStream = memoryStream.ToArray();
                return objectDataAsStream;
            }
        }

        /// <summary>
        /// 反序列化对象
        /// </summary>
        private static T Deserialize<T>(byte[] stream)
        {
            if (stream == null)
            {
                return default(T);
            }
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (MemoryStream memoryStream = new MemoryStream(stream))
            {
                T result = (T)binaryFormatter.Deserialize(memoryStream);
                return result;
            }
        }

    }
}
