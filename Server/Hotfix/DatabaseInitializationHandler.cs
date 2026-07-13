// ================================================================================
// 数据库初始化事件处理器（Hotfix 层 / 可热更）
// ================================================================================
// 在 OnCreateScene 事件中执行 MongoDB 连通性探测、8 集合创建与索引初始化。
// 使用 static flag + Interlocked.CompareExchange 守卫，确保仅第一个进入的 Scene
// 负责执行全部初始化，其余 Scene 直接跳过。
// 连通性探测失败即 Environment.Exit(1)，阻止服务在无数据库状态下启动。
// ================================================================================

using System;
using System.Threading;
using Entity;
using Fantasy;
using Fantasy.Async;
using Fantasy.Database;
using Fantasy.Event;
using MongoDB.Driver;

namespace Hotfix
{
    /// <summary>
    /// 职责：Scene 创建时执行 MongoDB 连通性探测与 8 集合索引初始化。
    /// </summary>
    /// <remarks>
    /// Responsibility: Execute MongoDB connectivity probe and 8-collection index initialization on Scene creation.
    /// </remarks>
    public sealed class DatabaseInitializationHandler : AsyncEventSystem<OnCreateScene>
    {
        // 初始化标志：0=未初始化，1=已初始化 / Initialization flag: 0=not initialized, 1=initialized
        private static int _initialized;

        /// <summary>
        /// 职责：Scene 创建完成时触发；首个 Scene 执行数据库初始化，其余跳过。
        /// </summary>
        /// <remarks>
        /// Responsibility: Triggered on Scene creation; the first Scene performs DB init, others skip.
        /// </remarks>
        /// <param name="self">OnCreateScene 事件参数 / Event args.</param>
        protected override async FTask Handler(OnCreateScene self)
        {
            // Interlocked.CompareExchange 守卫：仅第一个进入的 Scene 执行初始化。
            // Interlocked.CompareExchange guard: only the first Scene entering performs initialization.
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            {
                await FTask.CompletedTask;
                return;
            }

            var scene = self.Scene;
            var database = scene.World?.Database;

            // 数据库实例为空，说明 WorldConfig 未正确配置 MongoDB 连接，直接退出。
            // Database instance is null, meaning WorldConfig did not configure MongoDB connection correctly; exit immediately.
            if (database == null)
            {
                Log.Error("Database initialization failed: MongoDB database instance is null. Check WorldConfig dbConnection. Exiting.");
                Environment.Exit(1);
                return;
            }

            // 连通性探测：尝试对 AccountData 集合计数，不直接碰 MongoDB.Driver API。
            // Connectivity probe: attempt to count AccountData collection, without directly touching MongoDB.Driver API.
            try
            {
                await database.Count<AccountData>();
            }
            catch (Exception e)
            {
                Log.Error($"Database connectivity probe failed: {e.Message}. Exiting.");
                Environment.Exit(1);
                return;
            }

            // 创建 8 个集合并初始化全部索引。
            // Create 8 collections and initialize all indexes.
            try
            {
                await CreateCollectionsAndIndexes(database);
            }
            catch (Exception e)
            {
                Log.Error($"Database collection/index initialization failed: {e.Message}. Exiting.");
                Environment.Exit(1);
                return;
            }

            scene.LogInfo("Database initialization completed: 8 collections + indexes created.");
            await FTask.CompletedTask;
        }

        /// <summary>
        /// 职责：创建 8 个集合并建立全部索引（含 unique 与 TTL）。
        /// </summary>
        /// <remarks>
        /// Responsibility: Create 8 collections and establish all indexes (including unique and TTL).
        /// </remarks>
        /// <param name="database">Fantasy 数据库接口 / Fantasy database interface.</param>
        private static async FTask CreateCollectionsAndIndexes(IDatabase database)
        {
            // ---- 创建集合（幂等，已存在则跳过） / Create collections (idempotent, skip if exists) ----
            await database.CreateDB<AccountData>();
            await database.CreateDB<PlayerData>();
            await database.CreateDB<MatchHistoryData>();
            await database.CreateDB<ReplayData>();
            await database.CreateDB<BanData>();
            await database.CreateDB<AnalyticsEventData>();
            await database.CreateDB<RoomConfigData>();
            await database.CreateDB<MmrSeasonData>();

            // ---- 创建索引 / Create indexes ----

            // AccountData: AccountId(unique) / DeviceId(unique)
            await database.CreateIndex<AccountData>(
                new object[]
                {
                    Builders<AccountData>.IndexKeys.Ascending(x => x.AccountId),
                    Builders<AccountData>.IndexKeys.Ascending(x => x.DeviceId)
                },
                new object[]
                {
                    new CreateIndexOptions<AccountData> { Unique = true },
                    new CreateIndexOptions<AccountData> { Unique = true }
                });

            // PlayerData: PlayerId(unique) / AccountId / Mmr(desc)
            await database.CreateIndex<PlayerData>(
                new object[]
                {
                    Builders<PlayerData>.IndexKeys.Ascending(x => x.PlayerId),
                    Builders<PlayerData>.IndexKeys.Ascending(x => x.AccountId),
                    Builders<PlayerData>.IndexKeys.Descending(x => x.Mmr)
                },
                new object[]
                {
                    new CreateIndexOptions<PlayerData> { Unique = true },
                    new CreateIndexOptions<PlayerData>(),
                    new CreateIndexOptions<PlayerData>()
                });

            // MatchHistoryData: MatchId(unique) / SettleAt(desc) / Players.PlayerId(multikey)
            await database.CreateIndex<MatchHistoryData>(
                new object[]
                {
                    Builders<MatchHistoryData>.IndexKeys.Ascending(x => x.MatchId),
                    Builders<MatchHistoryData>.IndexKeys.Descending(x => x.SettleAt),
                    Builders<MatchHistoryData>.IndexKeys.Ascending("Players.PlayerId")
                },
                new object[]
                {
                    new CreateIndexOptions<MatchHistoryData> { Unique = true },
                    new CreateIndexOptions<MatchHistoryData>(),
                    new CreateIndexOptions<MatchHistoryData>()
                });

            // ReplayData: MatchId(unique) / ExpireAt(TTL, ExpireAfter=TimeSpan.Zero)
            await database.CreateIndex<ReplayData>(
                new object[]
                {
                    Builders<ReplayData>.IndexKeys.Ascending(x => x.MatchId),
                    Builders<ReplayData>.IndexKeys.Ascending(x => x.ExpireAt)
                },
                new object[]
                {
                    new CreateIndexOptions<ReplayData> { Unique = true },
                    new CreateIndexOptions<ReplayData> { ExpireAfter = TimeSpan.Zero }
                });

            // BanData: PlayerId / ExpireAt(TTL, ExpireAfter=TimeSpan.Zero)
            await database.CreateIndex<BanData>(
                new object[]
                {
                    Builders<BanData>.IndexKeys.Ascending(x => x.PlayerId),
                    Builders<BanData>.IndexKeys.Ascending(x => x.ExpireAt)
                },
                new object[]
                {
                    new CreateIndexOptions<BanData>(),
                    new CreateIndexOptions<BanData> { ExpireAfter = TimeSpan.Zero }
                });

            // AnalyticsEventData: PlayerId / MatchId / EventType / Timestamp(desc)
            await database.CreateIndex<AnalyticsEventData>(
                new object[]
                {
                    Builders<AnalyticsEventData>.IndexKeys.Ascending(x => x.PlayerId),
                    Builders<AnalyticsEventData>.IndexKeys.Ascending(x => x.MatchId),
                    Builders<AnalyticsEventData>.IndexKeys.Ascending(x => x.EventType),
                    Builders<AnalyticsEventData>.IndexKeys.Descending(x => x.Timestamp)
                },
                new object[]
                {
                    new CreateIndexOptions<AnalyticsEventData>(),
                    new CreateIndexOptions<AnalyticsEventData>(),
                    new CreateIndexOptions<AnalyticsEventData>(),
                    new CreateIndexOptions<AnalyticsEventData>()
                });

            // RoomConfigData: RoomId(unique) / Status / CreatedAt
            await database.CreateIndex<RoomConfigData>(
                new object[]
                {
                    Builders<RoomConfigData>.IndexKeys.Ascending(x => x.RoomId),
                    Builders<RoomConfigData>.IndexKeys.Ascending(x => x.Status),
                    Builders<RoomConfigData>.IndexKeys.Ascending(x => x.CreatedAt)
                },
                new object[]
                {
                    new CreateIndexOptions<RoomConfigData> { Unique = true },
                    new CreateIndexOptions<RoomConfigData>(),
                    new CreateIndexOptions<RoomConfigData>()
                });

            // MmrSeasonData: SeasonId(unique)
            await database.CreateIndex<MmrSeasonData>(
                new object[]
                {
                    Builders<MmrSeasonData>.IndexKeys.Ascending(x => x.SeasonId)
                },
                new object[]
                {
                    new CreateIndexOptions<MmrSeasonData> { Unique = true }
                });
        }
    }
}
