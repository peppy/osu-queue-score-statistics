// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using MySqlConnector;
using osu.Server.Queues.ScoreStatisticsProcessor.Commands.Maintenance;
using osu.Server.Queues.ScoreStatisticsProcessor.Models;
using StatsdClient;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Processors
{
    /// <summary>
    /// Marks irrelevant scores as non-preserved ahead of calculating totals which may depend on this flag.
    /// </summary>
    public class MarkNonPreservedProcessor : IProcessor
    {
        // This processor needs to run after PP is calculated as this is used in cleanup rules.
        public const int ORDER = ScorePerformanceProcessor.ORDER + 1;

        public int Order => ORDER;

        public bool RunOnFailedScores => false;

        public bool RunOnLegacyScores => true;

        public void RevertFromUserStats(SoloScore score, UserStats userStats, int previousVersion, MySqlConnection conn, MySqlTransaction transaction,
                                        List<Action<ProcessorContext>> postTransactionActions,
                                        DogStatsdService dogStatsd)
        {
        }

        public void ApplyToUserStats(SoloScore score, UserStats userStats, MySqlConnection conn, MySqlTransaction transaction, List<Action<ProcessorContext>> postTransactionActions,
                                     DogStatsdService dogStatsd)
        {
            List<SoloScore> scores = (conn.Query<SoloScore>(
                """
                SELECT
                    s.id, s.beatmap_id, s.ranked,
                    IF(s.data->'$.mods' IS NULL, '{}', JSON_OBJECT('mods', s.data->'$.mods')) AS data,
                    s.total_score, s.legacy_total_score, s.pp
                FROM scores s
                WHERE
                    s.user_id = @UserId
                    AND s.ruleset_id = @RulesetId
                    AND s.beatmap_id = @BeatmapId
                    AND s.pp IS NOT NULL
                    AND s.preserve = 1
                    AND NOT EXISTS (SELECT 1 FROM score_pins pins WHERE pins.score_id = s.id AND pins.user_id = @UserId AND pins.ruleset_id = @RulesetId)
                    AND NOT EXISTS (SELECT 1 FROM multiplayer_playlist_item_scores mp WHERE mp.score_id = s.id AND mp.user_id = @UserId);
                """, new
                {
                    UserId = score.user_id,
                    RulesetId = score.ruleset_id,
                    BeatmapId = score.beatmap_id,
                }, transaction: transaction)).ToList();

            foreach (var s in scores)
            {
                // check whether this score is a user high (either total_score or pp)
                if (MarkNonPreservedScoresCommand.CheckIsUserHigh(scores, s, out _))
                    continue;

                conn.Execute("UPDATE scores SET preserve = 0, unix_updated_at = UNIX_TIMESTAMP() WHERE id = @scoreId", new
                {
                    scoreId = s.id
                }, transaction: transaction);

                postTransactionActions.Add(context =>
                {
                    context.ElasticProcessor.PushToQueue(new ElasticQueuePusher.ElasticScoreItem { ScoreId = (long?)s.id });
                });
            }
        }

        public void ApplyGlobal(SoloScore score, MySqlConnection conn)
        {
        }
    }
}
