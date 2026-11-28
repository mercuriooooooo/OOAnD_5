using SpaceBattle.Lib;
using Xunit;

public class RatingAndHistoryTests
{
    [Fact]
    public void BattleResult_SymmetricDelta()
    {
        var rating = new RatingService();
        var beforeW = rating.Get("А").Pts;
        var beforeL = rating.Get("Б").Pts;

        rating.ApplyBattleResult("А", "Б");

        var deltaW = rating.Get("А").Pts - beforeW;
        var deltaL = beforeL - rating.Get("Б").Pts;
        Assert.True(deltaW > 0);
        Assert.Equal(deltaW, deltaL); // что начислено, то и списано
        Assert.Equal(1, rating.Get("А").Wins);
        Assert.Equal(1, rating.Get("Б").Losses);
    }

    [Fact]
    public void BattleResult_UnderdogWins_GetsMorePoints()
    {
        var rating = new RatingService();
        rating.Get("Фаворит").Pts = 1400;
        rating.Get("Андердог").Pts = 1000;

        rating.ApplyBattleResult("Андердог", "Фаворит");
        var upsetDelta = rating.Get("Андердог").Pts - 1000;

        rating.ApplyBattleResult("Фаворит", "Андердог"); // обратная ситуация
        var expectedDelta = rating.Get("Фаворит").Pts - (1400 - upsetDelta);

        Assert.True(upsetDelta > expectedDelta, "победа слабого должна стоить дороже");
    }

    [Fact]
    public void Penalty_DecreasesPts_NotBelowZero()
    {
        var rating = new RatingService();
        rating.Get("А").Pts = 5;
        rating.Penalize("А");
        rating.Penalize("А");
        Assert.Equal(0, rating.Get("А").Pts); // не уходим в минус
    }

    [Fact]
    public void CanMatch_RespectsRatingDifferenceRule()
    {
        var rating = new RatingService();
        rating.Get("А").Pts = 1000;
        rating.Get("Б").Pts = 1000 + RatingService.MaxRatingDifference;
        rating.Get("В").Pts = 1000 + RatingService.MaxRatingDifference + 1;

        Assert.True(rating.CanMatch("А", "Б"));
        Assert.False(rating.CanMatch("А", "В"));
    }

    [Fact]
    public void Leaderboard_SortedByPts()
    {
        var rating = new RatingService();
        rating.Get("А").Pts = 900;
        rating.Get("Б").Pts = 1200;
        rating.Get("В").Pts = 1000;

        var top = rating.Leaderboard();
        Assert.Equal("Б", top[0].Login);
        Assert.Equal("В", top[1].Login);
        Assert.Equal("А", top[2].Login);
    }

    [Fact]
    public void FairMatchmaker_FIFO_HeadPairsFirstWithCompatible()
    {
        var rating = new RatingService();
        rating.Get("Первый").Pts = 1000;
        rating.Get("Далёкий").Pts = 1500;   // несовместим с «Первый»
        rating.Get("Второй").Pts = 1005;    // совместим
        var mm = new FairMatchmaker(rating);

        mm.Enqueue("Первый");
        mm.Enqueue("Далёкий");
        Assert.Null(mm.TryPair()); // совместимой пары нет

        mm.Enqueue("Второй");
        var pair = mm.TryPair();

        Assert.Equal(("Первый", "Второй"), pair); // FIFO: первый в очереди — первый в бой
        Assert.Equal(1, mm.WaitingCount); // «Далёкий» остался ждать
        Assert.False(mm.Enqueue("Далёкий")); // дубликат в очереди не ставится
    }

    [Fact]
    public void BattleHistory_PersistsToJson_AndReloads()
    {
        var path = Path.Combine(Path.GetTempPath(), $"history-{Guid.NewGuid():N}.json");
        try
        {
            var history = new BattleHistoryService(path);
            history.Append(new BattleRecord { GameId = "g1", Winner = "А", Loser = "Б", Ticks = 10 });
            history.Append(new BattleRecord { GameId = "g2", Winner = "Б", Loser = "А", Ticks = 20 });

            var reloaded = new BattleHistoryService(path);
            Assert.Equal(2, reloaded.All().Count);
            Assert.Equal((1, 1), reloaded.Stats("А"));
            Assert.Single(reloaded.ForPlayer("Б").Where(r => r.Winner == "Б"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void BattleHistory_EmptyOrBrokenFile_StartsClean()
    {
        var path = Path.Combine(Path.GetTempPath(), $"history-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{битый json");
        try
        {
            var history = new BattleHistoryService(path);
            Assert.Empty(history.All());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
