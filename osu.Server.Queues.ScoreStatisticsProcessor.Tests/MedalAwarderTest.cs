// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapper;
using osu.Server.Queues.ScoreStatisticsProcessor.Models;
using osu.Server.Queues.ScoreStatisticsProcessor.Processors;
using Xunit;

namespace osu.Server.Queues.ScoreStatisticsProcessor.Tests
{
    public abstract class MedalAwarderTest : DatabaseTest
    {
        private readonly List<MedalProcessor.AwardedMedal> awardedMedals = new List<MedalProcessor.AwardedMedal>();

        protected MedalAwarderTest(AssemblyName[]? externalProcessorAssemblies = null)
            : base(externalProcessorAssemblies)
        {
            MedalProcessor.MedalAwarded += onMedalAwarded;

            using (var db = Processor.GetDatabaseConnection())
            {
                db.Execute("TRUNCATE TABLE osu_achievements");
                db.Execute("TRUNCATE TABLE osu_user_achievements");

                db.Execute("TRUNCATE TABLE osu_beatmappacks");
                db.Execute("TRUNCATE TABLE osu_beatmappacks_items");

                db.Execute("TRUNCATE TABLE daily_challenge_user_stats");
            }
        }

        protected void AddMedal(int medalId, int? mode = null)
        {
            using (var db = Processor.GetDatabaseConnection())
            {
                db.Execute("INSERT INTO osu_achievements (achievement_id, slug, ordering, progression, name, mode) VALUES (@medalId, 'A', 1, 1, 'medal', @mode)",
                    new
                    {
                        medalId,
                        mode
                    });
            }
        }

        protected void AddPackMedal(int medalId, int packId, IReadOnlyList<Beatmap> beatmaps)
        {
            AddMedal(medalId);

            using (var db = Processor.GetDatabaseConnection())
            {
                db.Execute($"INSERT INTO osu_beatmappacks (pack_id, url, name, author, tag, date) VALUES ({packId}, 'https://osu.ppy.sh', 'pack', 'wang', 'PACK', NOW())");

                foreach (int setId in beatmaps.GroupBy(b => b.beatmapset_id).Select(g => g.Key))
                    db.Execute($"INSERT INTO osu_beatmappacks_items (pack_id, beatmapset_id) VALUES ({packId}, {setId})");
            }
        }

        protected void AssertSingleMedalAwarded(int medalId)
        {
            Assert.Collection(awardedMedals, a => Assert.Equal(medalId, a.Medal.achievement_id));
        }

        protected void AssertMedalAwarded(int medalId)
        {
            Assert.Contains(awardedMedals, a => medalId == a.Medal.achievement_id);
        }

        protected void AssertMedalNotAwarded(int medalId)
        {
            Assert.Collection(awardedMedals, a => Assert.NotEqual(medalId, a.Medal.achievement_id));
        }

        protected void AssertNoMedalsAwarded()
        {
            Assert.Empty(awardedMedals);
        }

        private void onMedalAwarded(MedalProcessor.AwardedMedal awarded)
        {
            awardedMedals.Add(awarded);

            // Usually osu-web would do this.
            using (var db = Processor.GetDatabaseConnection())
            {
                db.Execute($"INSERT INTO osu_user_achievements (achievement_id, user_id, beatmap_id) VALUES ({awarded.Medal.achievement_id}, {awarded.Score.user_id}, {awarded.Score.beatmap_id})");
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            MedalProcessor.MedalAwarded -= onMedalAwarded;
        }
    }
}
