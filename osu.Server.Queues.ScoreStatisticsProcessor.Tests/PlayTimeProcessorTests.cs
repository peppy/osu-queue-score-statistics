// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Online.API;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using Xunit;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Tests
{
    public class PlayTimeProcessorTests : DatabaseTest
    {
        private const int beatmap_length = 158;

        public PlayTimeProcessorTests()
        {
            AddBeatmap(b => b.total_length = beatmap_length);
            AddBeatmapAttributes<OsuDifficultyAttributes>(mods: [new OsuModDoubleTime()]);
        }

        [Fact]
        public void TestPlayTimeIncrease()
        {
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            var testScore = CreateTestScore();
            testScore.Score.ended_at = testScore.Score.started_at!.Value + TimeSpan.FromSeconds(50);

            PushToQueueAndWaitForProcess(testScore);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", 50, CancellationToken);

            testScore = CreateTestScore();
            testScore.Score.ended_at = testScore.Score.started_at!.Value + TimeSpan.FromSeconds(100);

            PushToQueueAndWaitForProcess(testScore);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", 150, CancellationToken);
        }

        [Fact]
        public void TestPlayTimeIncreaseHigherThanBeatmapLength()
        {
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            // Beatmap used in test score is 158 seconds.

            var testScore = CreateTestScore();
            testScore.Score.ended_at = testScore.Score.started_at!.Value + TimeSpan.FromSeconds(200);

            PushToQueueAndWaitForProcess(testScore);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", beatmap_length, CancellationToken);
        }

        [Fact]
        public void TestPlayTimeIncreaseHigherThanBeatmapLengthWithModApplication()
        {
            // Beatmap used in test score is 158 seconds.
            // Double time means this is reduced to 105 seconds.
            const double double_time_rate = 1.5;

            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            var testScore = CreateTestScore();
            testScore.Score.ScoreData.Mods = new[]
            {
                new APIMod(new OsuModDoubleTime()),
            };
            testScore.Score.ended_at = testScore.Score.started_at!.Value + TimeSpan.FromSeconds(200);

            PushToQueueAndWaitForProcess(testScore);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int)(beatmap_length / double_time_rate), CancellationToken);
        }

        [Fact]
        public void TestPlayTimeIncreaseHigherThanBeatmapLengthWithModApplicationCustomRate()
        {
            // Beatmap used in test score is 158 seconds.
            // Double time with custom rate means this is reduced to 112 seconds.
            const double custom_rate = 1.4;

            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            var testScore = CreateTestScore();
            testScore.Score.ScoreData.Mods = new[]
            {
                new APIMod(new OsuModDoubleTime { SpeedChange = { Value = custom_rate } }),
            };
            testScore.Score.ended_at = testScore.Score.started_at!.Value + TimeSpan.FromSeconds(200);

            PushToQueueAndWaitForProcess(testScore);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int)(beatmap_length / custom_rate), CancellationToken);
        }

        [Fact]
        public void TestPlayTimeDoesNotIncreaseIfFailedAndPlayTooShort()
        {
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            var score = CreateTestScore();
            score.Score.ended_at = score.Score.started_at!.Value + TimeSpan.FromSeconds(4);
            score.Score.passed = false;

            PushToQueueAndWaitForProcess(score);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", 0, CancellationToken);
        }

        [Fact]
        public void TestPlayTimeDoesNotIncreaseIfFailedAndScoreTooLow()
        {
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            var score = CreateTestScore();
            score.Score.total_score = 20;
            score.Score.passed = false;

            PushToQueueAndWaitForProcess(score);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", 0, CancellationToken);
        }

        [Theory]
        [InlineData(3, 40)]
        [InlineData(9, 100)]
        [InlineData(19, 200)]
        [InlineData(19, 500)]
        public void TestPlayTimeDoesNotIncreaseIfFailedAndTooFewObjectsHit(int hitCount, int totalCount)
        {
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            var score = CreateTestScore();
            score.Score.ScoreData.Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = hitCount };
            score.Score.ScoreData.MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = totalCount };
            score.Score.passed = false;

            PushToQueueAndWaitForProcess(score);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", 0, CancellationToken);
        }

        [Fact]
        public void TestPlayTimeDoesIncreaseIfPassedAndShort()
        {
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", (int?)null, CancellationToken);

            var score = CreateTestScore();
            score.Score.ended_at = score.Score.started_at!.Value + TimeSpan.FromSeconds(4);

            PushToQueueAndWaitForProcess(score);
            WaitForDatabaseState("SELECT total_seconds_played FROM osu_user_stats WHERE user_id = 2", 4, CancellationToken);
        }
    }
}
