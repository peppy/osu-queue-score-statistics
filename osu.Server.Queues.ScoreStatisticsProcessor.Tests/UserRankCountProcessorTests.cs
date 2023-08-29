// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Scoring;
using Xunit;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Tests
{
    public class UserRankCountProcessorTests : DatabaseTest
    {
        [Fact]
        public void TestScoresFromDifferentBeatmapsAreCountedSeparately()
        {
            var firstBeatmap = AddBeatmap(b => b.beatmap_id = 1001, s => s.beatmapset_id = 1);
            var secondBeatmap = AddBeatmap(b => b.beatmap_id = 1002, s => s.beatmapset_id = 2);

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(firstBeatmap.beatmap_id, item => item.Score.ScoreInfo.Rank = ScoreRank.X);
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });

            SetScoreForBeatmap(secondBeatmap.beatmap_id, item => item.Score.ScoreInfo.Rank = ScoreRank.A);
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
                [ScoreRank.A] = 1,
            });
        }

        [Fact]
        public void TestScoresFromSameBeatmapInDifferentRulesetsAreCountedSeparately()
        {
            AddBeatmap();
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());
            waitForRankCounts("osu_user_stats_mania", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item => item.Score.ScoreInfo.Rank = ScoreRank.X);
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });
            waitForRankCounts("osu_user_stats_mania", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ruleset_id = item.Score.ScoreInfo.RulesetID = 3;
                item.Score.ScoreInfo.Rank = ScoreRank.A;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });
            waitForRankCounts("osu_user_stats_mania", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });
        }

        [Fact]
        public void TestScoreWithRankBelowADoesNothing()
        {
            AddBeatmap();
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item => item.Score.ScoreInfo.Rank = ScoreRank.B);
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());
        }

        [Fact]
        public void TestScoreFromSameBeatmapAndHigherTotalChangesCountedRank()
        {
            AddBeatmap();
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.A;
                item.Score.ScoreInfo.TotalScore = 600_000;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.X;
                item.Score.ScoreInfo.TotalScore = 700_000;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });
        }

        [Fact]
        public void TestScoreFromSameBeatmapAndLowerTotalDoesNotChangeCountedRank()
        {
            AddBeatmap();
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.A;
                item.Score.ScoreInfo.TotalScore = 600_000;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.X;
                item.Score.ScoreInfo.TotalScore = 500_000;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });
        }

        [Theory]
        [InlineData(ScoreRank.A, ScoreRank.X)]
        [InlineData(ScoreRank.X, ScoreRank.A)]
        public void TestNewestScoreFromSameBeatmapAndWithSameTotalWins(ScoreRank firstRank, ScoreRank secondRank)
        {
            const int total_score = 600_000;

            AddBeatmap();
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = firstRank;
                item.Score.ScoreInfo.TotalScore = total_score;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [firstRank] = 1,
            });

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = secondRank;
                item.Score.ScoreInfo.TotalScore = total_score;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [secondRank] = 1,
            });
        }

        [Fact]
        public void TestReprocessWithSameVersionDoesntIncrease()
        {
            AddBeatmap();

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            var score = CreateTestScore();
            score.Score.ScoreInfo.Rank = ScoreRank.A;

            PushToQueueAndWaitForProcess(score);

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });

            // the score will be marked as processed (in the database) at this point, so should not increase the rank counts if processed a second time.
            score.MarkProcessed();

            PushToQueueAndWaitForProcess(score);
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });
        }

        [Fact]
        public void TestReprocessNewHighScoreDoesntChangeCounts()
        {
            AddBeatmap();

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.A;
                item.Score.ScoreInfo.TotalScore = 600_000;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });

            var secondScore = SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.X;
                item.Score.ScoreInfo.TotalScore = 700_000;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });

            // the score will be marked as processed (in the database) at this point.
            secondScore.MarkProcessed();
            // artificially increase the `processed_version` so that the score undergoes a revert and reprocess.
            secondScore.ProcessHistory!.processed_version++;

            PushToQueueAndWaitForProcess(secondScore);

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });
        }

        [Fact]
        public void TestReprocessNewerTiedScoreDoesntChangeCounts()
        {
            const int total_score = 600_000;

            AddBeatmap();

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.A;
                item.Score.ScoreInfo.TotalScore = total_score;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });

            var secondScore = SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.X;
                item.Score.ScoreInfo.TotalScore = total_score;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });

            // the score will be marked as processed (in the database) at this point.
            secondScore.MarkProcessed();
            // artificially increase the `processed_version` so that the score undergoes a revert and reprocess.
            secondScore.ProcessHistory!.processed_version++;

            PushToQueueAndWaitForProcess(secondScore);

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.X] = 1,
            });
        }

        [Fact]
        public void TestReprocessNewNonHighScoreDoesntChangeCounts()
        {
            AddBeatmap();

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>());

            SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.A;
                item.Score.ScoreInfo.TotalScore = 700_000;
            });
            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });

            var secondScore = SetScoreForBeatmap(TEST_BEATMAP_ID, item =>
            {
                item.Score.ScoreInfo.Rank = ScoreRank.X;
                item.Score.ScoreInfo.TotalScore = 600_000;
            });

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });

            // the score will be marked as processed (in the database) at this point.
            secondScore.MarkProcessed();
            // artificially increase the `processed_version` so that the score undergoes a revert and reprocess.
            secondScore.ProcessHistory!.processed_version++;

            PushToQueueAndWaitForProcess(secondScore);

            waitForRankCounts("osu_user_stats", new Dictionary<ScoreRank, int>
            {
                [ScoreRank.A] = 1,
            });
        }

        private void waitForRankCounts(string tableName, Dictionary<ScoreRank, int> counts)
        {
            WaitForDatabaseState($"SELECT `xh_rank_count` from {tableName} WHERE `user_id` = 2", counts.GetValueOrDefault(ScoreRank.XH), CancellationToken);
            WaitForDatabaseState($"SELECT `x_rank_count` from {tableName} WHERE `user_id` = 2", counts.GetValueOrDefault(ScoreRank.X), CancellationToken);
            WaitForDatabaseState($"SELECT `sh_rank_count` from {tableName} WHERE `user_id` = 2", counts.GetValueOrDefault(ScoreRank.SH), CancellationToken);
            WaitForDatabaseState($"SELECT `s_rank_count` from {tableName} WHERE `user_id` = 2", counts.GetValueOrDefault(ScoreRank.S), CancellationToken);
            WaitForDatabaseState($"SELECT `a_rank_count` from {tableName} WHERE `user_id` = 2", counts.GetValueOrDefault(ScoreRank.A), CancellationToken);
        }
    }
}
