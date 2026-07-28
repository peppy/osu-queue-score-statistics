// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MySqlConnector;

namespace osu.Server.Queues.ScoreStatisticsProcessor
{
    public record ProcessorContext
    {
        public required MySqlConnection Connection { get; init; }

        public required ElasticQueuePusher ElasticProcessor { get; init; }
    }
}
