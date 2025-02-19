﻿#if UNITY_EDITOR || UNITY_STANDALONE || DOTNET
using MongoDB.Driver;

namespace ET.Server
{
    /// <summary>
    /// 用来缓存数据
    /// </summary>
    [ChildOf(typeof(DBManagerComponent))]
    public class DBComponent: Entity, IAwake<string, string, int>
    {
        public const int TaskCount = 32;

        public MongoClient mongoClient;
        public IMongoDatabase database;
    }
}
#endif