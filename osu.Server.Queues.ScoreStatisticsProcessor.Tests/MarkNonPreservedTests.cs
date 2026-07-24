// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using Dapper;
using osu.Server.Queues.ScoreStatisticsProcessor.Models;
using Xunit;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Tests
{
    /// <summary>
    /// This tests realtime non-preserved marking, handled by `UserTotalPerformanceProcessor`.
    /// </summary>
    public class MarkNonPreservedTests : DatabaseTest
    {
        private readonly Beatmap beatmap;

        public MarkNonPreservedTests()
        {
            beatmap = AddBeatmap();

            using var db = Processor.GetDatabaseConnection();

            db.Execute("DELETE FROM `multiplayer_playlist_item_scores`");
            db.Execute("TRUNCATE TABLE `score_pins`");
        }

        [Fact]
        public void OnlyBestPPAndTotalScoresArePreserved()
        {
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 1;
                s.Score.ScoreData.TotalScoreWithoutMods = s.Score.total_score = 800_000;
                s.Score.pp = 95;
            });
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 2;
                s.Score.ScoreData.TotalScoreWithoutMods = s.Score.total_score = 850_000;
                s.Score.pp = 90;
            });
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 3;
                s.Score.ScoreData.TotalScoreWithoutMods = s.Score.total_score = 500_000;
                s.Score.pp = 85;
            });
            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 1", 2, CancellationToken);
            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 0", 1, CancellationToken);
            WaitForDatabaseState("SELECT `preserve` FROM `scores` WHERE `id` = 3", false, CancellationToken);
        }

        [Fact]
        public async Task NonBestScoresRemainPreservedIfPinned()
        {
            using var db = Processor.GetDatabaseConnection();

            await db.ExecuteAsync("INSERT INTO `score_pins` (`user_id`, `score_id`, `ruleset_id`, `display_order`) VALUES (2, 2, 0, 0)");

            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 1;
                s.Score.total_score = 800_000;
                s.Score.pp = 95;
            });
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 2;
                s.Score.total_score = 500_000;
                s.Score.pp = 85;
            });

            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 1", 2, CancellationToken);
            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 0", 0, CancellationToken);
            WaitForDatabaseState("SELECT `preserve` FROM `scores` WHERE `id` = 2", true, CancellationToken);
        }

        [Fact]
        public async Task NonBestScoresRemainPreservedIfAssociatedWithPlaylistItem()
        {
            using var db = Processor.GetDatabaseConnection();

            await db.ExecuteAsync("INSERT INTO `multiplayer_playlist_item_scores` (`user_id`, `playlist_item_id`, `score_id`) VALUES (2, 1, 2)");

            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 1;
                s.Score.total_score = 800_000;
                s.Score.pp = 95;
            });
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 2;
                s.Score.total_score = 500_000;
                s.Score.pp = 85;
            });

            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 1", 2, CancellationToken);
            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 0", 0, CancellationToken);
            WaitForDatabaseState("SELECT `preserve` FROM `scores` WHERE `id` = 2", true, CancellationToken);
        }

        [Fact]
        public void ScoreNotConsideredBestIfNotRanked()
        {
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 1;
                s.Score.total_score = 800_000;
                s.Score.pp = 95;
                s.Score.ranked = false;
            });
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 2;
                s.Score.total_score = 500_000;
                s.Score.pp = 85;
            });

            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 1", 1, CancellationToken);
            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 0", 1, CancellationToken);
            WaitForDatabaseState("SELECT `preserve` FROM `scores` WHERE `id` = 1", false, CancellationToken);
        }

        [Fact]
        public void ScorePreserveOnlyBestNotRanked()
        {
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 1;
                s.Score.total_score = 800_000;
                s.Score.pp = 95;
                s.Score.ranked = false;
            });
            SetScoreForBeatmap(beatmap.beatmap_id, s =>
            {
                s.Score.id = 2;
                s.Score.total_score = 500_000;
                s.Score.pp = 85;
                s.Score.ranked = false;
            });

            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 1", 1, CancellationToken);
            WaitForDatabaseState("SELECT COUNT(1) FROM `scores` WHERE `preserve` = 0", 1, CancellationToken);
            WaitForDatabaseState("SELECT `preserve` FROM `scores` WHERE `id` = 1", true, CancellationToken);
        }
    }
}
