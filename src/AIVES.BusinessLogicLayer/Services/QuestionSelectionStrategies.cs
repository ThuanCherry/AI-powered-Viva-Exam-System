using AIVES.BusinessLogicLayer.Interfaces;
using AIVES.DataAccessLayer.Models;
namespace AIVES.BusinessLogicLayer.Services;
public class RandomQuestionSelectionStrategy : IQuestionSelectionStrategy
{
    public string Name => "RANDOM";
    public virtual IReadOnlyList<Question> Select(IReadOnlyList<Question> candidates, int count, IReadOnlySet<long> recent)
    {
        var available = candidates.DistinctBy(x => x.QuestionId).ToArray();
        var preferred = available.Where(x => !recent.Contains(x.QuestionId)).OrderBy(_ => Random.Shared.Next()).ToList();
        var fallback = available.Where(x => recent.Contains(x.QuestionId)).OrderBy(_ => Random.Shared.Next());
        return preferred.Concat(fallback).Take(count).ToArray();
    }
}
public class AdaptiveQuestionSelectionStrategy : IQuestionSelectionStrategy
{
    public string Name => "ADAPTIVE";
    public IReadOnlyList<Question> Select(IReadOnlyList<Question> candidates, int count, IReadOnlySet<long> recent)
    {
        var remaining = candidates.DistinctBy(x => x.QuestionId).ToList();
        if (remaining.All(x => string.IsNullOrWhiteSpace(x.Difficulty) && string.IsNullOrWhiteSpace(x.BloomLevel)))
            return new RandomQuestionSelectionStrategy().Select(candidates, count, recent);
        var chosen = new List<Question>();
        for (var index = 0; index < count && remaining.Count > 0; index++)
        {
            var preferred = remaining.Where(x => !recent.Contains(x.QuestionId)).ToArray();
            var choices = preferred.Length > 0 ? preferred : remaining.ToArray();
            var target = index % 3;
            var bucket = choices.Where(x => Level(x) == target).ToArray();
            if (bucket.Length == 0)
            {
                var min = choices.Min(x => Math.Abs(Level(x) - target));
                bucket = choices.Where(x => Math.Abs(Level(x) - target) == min).ToArray();
            }
            var next = bucket[Random.Shared.Next(bucket.Length)];
            chosen.Add(next);
            remaining.Remove(next);
        }
        return chosen;
    }
    private static int Level(Question q)
    {
        var difficulty = q.Difficulty?.Trim().ToUpperInvariant();
        if (difficulty is "EASY" or "LOW") return 0;
        if (difficulty is "HARD" or "HIGH") return 2;
        if (difficulty is "MEDIUM" or "NORMAL") return 1;
        var bloom = q.BloomLevel?.Trim().ToUpperInvariant();
        return bloom switch { "REMEMBER" => 0, "APPLY" or "ANALYZE" or "EVALUATE" or "CREATE" => 2, _ => 1 };
    }
}