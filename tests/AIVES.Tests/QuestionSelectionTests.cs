using AIVES.BusinessLogicLayer.Services;
using AIVES.DataAccessLayer.Models;
using Xunit;
namespace AIVES.Tests;
public class QuestionSelectionTests
{
    private static Question[] Pool(int count) => Enumerable.Range(1, count).Select(i => new Question
    {
        QuestionId = i, Difficulty = new[] { "Easy", "Medium", "Hard" }[(i - 1) % 3],
        BloomLevel = new[] { "Remember", "Understand", "Apply" }[(i - 1) % 3]
    }).ToArray();
    [Theory]
    [InlineData("RANDOM")]
    [InlineData("ADAPTIVE")]
    public void Select_AdequatePool_AvoidsRecentQuestions(string name)
    {
        AIVES.BusinessLogicLayer.Interfaces.IQuestionSelectionStrategy strategy = name == "RANDOM" ?
            new RandomQuestionSelectionStrategy() : new AdaptiveQuestionSelectionStrategy();
        for (int run = 0; run < 100; run++)
        {
            var selected = strategy.Select(Pool(12), 3, new HashSet<long> { 1, 2, 3 });
            Assert.Equal(3, selected.Count);
            Assert.Equal(3, selected.Select(x => x.QuestionId).Distinct().Count());
            Assert.DoesNotContain(selected, x => x.QuestionId <= 3);
        }
    }
    [Theory]
    [InlineData("RANDOM")]
    [InlineData("ADAPTIVE")]
    public void Select_SmallPool_RelaxesRecentWithoutInternalDuplicates(string name)
    {
        AIVES.BusinessLogicLayer.Interfaces.IQuestionSelectionStrategy strategy = name == "RANDOM" ?
            new RandomQuestionSelectionStrategy() : new AdaptiveQuestionSelectionStrategy();
        var selected = strategy.Select(Pool(3), 3, new HashSet<long> { 1, 2, 3 });
        Assert.Equal(3, selected.Select(x => x.QuestionId).Distinct().Count());
    }
    [Fact]
    public void Adaptive_Metadata_BalancesThreeDifficultyLevels()
    {
        var selected = new AdaptiveQuestionSelectionStrategy().Select(Pool(12), 3, new HashSet<long>());
        Assert.Equal(new[] { "Easy", "Medium", "Hard" }, selected.Select(x => x.Difficulty).ToArray());
    }
    [Fact]
    public void Adaptive_NoMetadata_FallsBackToRandomWithRecentAvoidance()
    {
        var pool = Pool(6);
        foreach (var q in pool) { q.Difficulty = null; q.BloomLevel = null; }
        var selected = new AdaptiveQuestionSelectionStrategy().Select(pool, 3, new HashSet<long> { 1, 2, 3 });
        Assert.Equal(new long[] { 4, 5, 6 }, selected.Select(x => x.QuestionId).Order().ToArray());
    }
    [Fact]
    public void Select_DuplicateCandidates_ReturnsUniqueQuestions()
    {
        var pool = Pool(3);
        var selected = new RandomQuestionSelectionStrategy().Select(pool.Concat(pool).ToArray(), 3, new HashSet<long>());
        Assert.Equal(3, selected.Select(x => x.QuestionId).Distinct().Count());
    }
}