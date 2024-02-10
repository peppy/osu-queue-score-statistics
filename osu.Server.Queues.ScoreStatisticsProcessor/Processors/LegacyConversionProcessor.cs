// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Dapper;
using MySqlConnector;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Scoring;
using osu.Server.Queues.ScoreStatisticsProcessor.Commands.Queue;
using osu.Server.Queues.ScoreStatisticsProcessor.Helpers;
using osu.Server.Queues.ScoreStatisticsProcessor.Models;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Processors
{
    public class LegacyConversionProcessor : IProcessor
    {
        public bool RunOnFailedScores => true;
        public bool RunOnLegacyScores => true;

        public void RevertFromUserStats(SoloScoreInfo score, UserStats userStats, int previousVersion, MySqlConnection conn, MySqlTransaction transaction)
        {
        }

        public void ApplyToUserStats(SoloScoreInfo score, UserStats userStats, MySqlConnection conn, MySqlTransaction transaction)
        {
            var ruleset = LegacyRulesetHelper.GetRulesetFromLegacyId(score.RulesetID);
            var databaseInfo = LegacyDatabaseHelper.GetRulesetSpecifics(score.RulesetID);

            var highScore = conn.QuerySingle($"SELECT * FROM {databaseInfo.}")

            ScoreInfo referenceScore = BatchInserter.CreateReferenceScore(ruleset.RulesetInfo.OnlineID, highScore);
            string serialisedScore = SerialiseScoreData(referenceScore);
            insertBuilder.Append(
                $"({highScore.user_id}, {rulesetId}, {highScore.beatmap_id}, {(highScore.replay ? "1" : "0")}, {(highScore.ShouldPreserve ? "1" : "0")}, '{referenceScore.Rank.ToString()}', {(highScore.pass ? "1" : "0")}, {referenceScore.Accuracy}, {referenceScore.MaxCombo}, {referenceScore.TotalScore}, '{serialisedScore}', {highScore.pp?.ToString() ?? "null"}, {highScore.score_id}, {referenceScore.LegacyTotalScore}, '{highScore.date.ToString("yyyy-MM-dd HH:mm:ss")}', {highScore.date.ToUnixTimeSeconds()})");
        }

        public void ApplyGlobal(SoloScoreInfo score, MySqlConnection conn)
        {
        }
    }
}
