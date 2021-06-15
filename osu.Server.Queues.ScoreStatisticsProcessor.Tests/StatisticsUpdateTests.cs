using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using osu.Game.Rulesets.Scoring;
using osu.Server.Queues.ScoreStatisticsProcessor.Models;
using Xunit;
using Xunit.Sdk;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Tests
{
    public class StatisticsUpdateTests : IDisposable
    {
        private readonly ScoreStatisticsProcessor processor;

        private readonly CancellationTokenSource cts = new CancellationTokenSource(10000);

        public StatisticsUpdateTests()
        {
            processor = new ScoreStatisticsProcessor();
            processor.ClearQueue();

            using (var db = processor.GetDatabaseConnection())
            {
                // just a safety measure for now to ensure we don't hit production. since i was running on production until now.
                // will throw if not on test database.
                db.Query<int>("SELECT * FROM test_database");

                db.Execute("TRUNCATE TABLE osu_user_stats");
                db.Execute("TRUNCATE TABLE osu_user_stats_mania");
                db.Execute("TRUNCATE TABLE solo_scores");
                db.Execute("TRUNCATE TABLE solo_scores_process_history");
            }

            Task.Run(() => processor.Run(cts.Token), cts.Token);
        }

        [Fact]
        public void TestPlaycountIncreaseMania()
        {
            waitForDatabaseState("SELECT playcount FROM osu_user_stats_mania WHERE user_id = 2", (int?)null, cts.Token);

            processor.PushToQueue(CreateTestScore(3));
            waitForDatabaseState("SELECT playcount FROM osu_user_stats_mania WHERE user_id = 2", 1, cts.Token);

            processor.PushToQueue(CreateTestScore(3));
            waitForDatabaseState("SELECT playcount FROM osu_user_stats_mania WHERE user_id = 2", 2, cts.Token);
        }

        [Fact]
        public void TestPlaycountIncrease()
        {
            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", (int?)null, cts.Token);

            processor.PushToQueue(CreateTestScore());
            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", 1, cts.Token);

            processor.PushToQueue(CreateTestScore());
            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", 2, cts.Token);
        }

        [Fact]
        public void TestProcessingSameScoreTwiceRaceCondition()
        {
            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", (int?)null, cts.Token);

            var score = CreateTestScore();

            processor.PushToQueue(score);
            processor.PushToQueue(score);
            processor.PushToQueue(score);
            processor.PushToQueue(score);
            processor.PushToQueue(score);

            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", 1, cts.Token);

            waitForTotalProcessed(5, cts.Token);

            // check only one score was counted, even though many were pushed.
            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", 1, cts.Token);
        }

        [Fact]
        public void TestPlaycountReprocessDoesntIncrease()
        {
            var score = CreateTestScore();

            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", (int?)null, cts.Token);

            processor.PushToQueue(score);
            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", 1, cts.Token);

            // the score will be marked as processed (in the database) at this point, so should not increase the playcount if processed a second time.
            score.MarkProcessed();

            processor.PushToQueue(score);
            waitForDatabaseState("SELECT playcount FROM osu_user_stats WHERE user_id = 2", 1, cts.Token);
        }

        [Fact]
        public void TestHitStatisticsIncrease()
        {
            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", (int?)null, cts.Token);

            processor.PushToQueue(CreateTestScore());
            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", 5, cts.Token);

            processor.PushToQueue(CreateTestScore());
            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", 10, cts.Token);
        }

        [Fact]
        public void TestHitStatisticsReprocessOldVersionIncrease()
        {
            var score = CreateTestScore();

            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", (int?)null, cts.Token);
            processor.PushToQueue(score);

            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", 5, cts.Token);

            score.MarkProcessed();

            // intentionally set to an older version to make sure it doesn't revert hit statistics.
            Debug.Assert(score.ProcessHistory != null);
            score.ProcessHistory.processed_version = 1;

            processor.PushToQueue(score);
            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", 10, cts.Token);
        }

        [Fact]
        public void TestHitStatisticsReprocessDoesntIncrease()
        {
            var score = CreateTestScore();

            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", (int?)null, cts.Token);
            processor.PushToQueue(score);

            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", 5, cts.Token);

            // the score will be marked as processed (in the database) at this point, so should not increase the playcount if processed a second time.
            score.MarkProcessed();

            processor.PushToQueue(score);
            waitForDatabaseState("SELECT count300 FROM osu_user_stats WHERE user_id = 2", 5, cts.Token);
        }

        private static long scoreIDSource;

        public static ScoreItem CreateTestScore(int rulesetId = 0)
        {
            return new ScoreItem
            {
                Score = new SoloScore
                {
                    user_id = 2,
                    beatmap_id = 81,
                    ruleset_id = rulesetId,
                    statistics =
                    {
                        { HitResult.Perfect, 5 }
                    },
                    id = Interlocked.Increment(ref scoreIDSource),
                    passed = true
                }
            };
        }

        private void waitForTotalProcessed(int count, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (processor.TotalProcessed == count)
                    return;

                Thread.Sleep(50);
            }

            throw new XunitException("All scores were not successfully processed");
        }

        private void waitForDatabaseState<T>(string sql, T expected, CancellationToken cancellationToken)
        {
            T lastValue = default;

            using (var db = processor.GetDatabaseConnection())
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    lastValue = db.QueryFirstOrDefault<T>(sql);
                    if (lastValue.Equals(expected))
                        return;

                    Thread.Sleep(50);
                }
            }

            throw new XunitException($"Database criteria was not met ({sql}: expected {expected} != actual {lastValue})");
        }

#pragma warning disable CA1816
        public void Dispose()
#pragma warning restore CA1816
        {
            cts.Cancel();
        }
    }
}