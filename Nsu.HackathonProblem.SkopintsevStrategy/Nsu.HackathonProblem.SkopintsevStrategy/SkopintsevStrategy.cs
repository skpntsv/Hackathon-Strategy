using Nsu.HackathonProblem.Contracts;

namespace Nsu.HackathonProblem.SkopintsevStrategy;

public class SkopintsevStrategy : ITeamBuildingStrategy
{
    private readonly Random _rnd = new();

    public IEnumerable<Team> BuildTeams(IEnumerable<Employee> teamLeads, IEnumerable<Employee> juniors, 
        IEnumerable<Wishlist> teamLeadsWishlists, IEnumerable<Wishlist> juniorsWishlists)
    {
        var leaders = teamLeads.ToList();
        var juniorsList = juniors.ToList();
        var leaderPrefs = teamLeadsWishlists.ToDictionary(w => w.EmployeeId, w => w.DesiredEmployees);
        var juniorPrefs = juniorsWishlists.ToDictionary(w => w.EmployeeId, w => w.DesiredEmployees);

        var n = leaders.Count;

        List<int[]> initialSolutions = GenerateMultipleStarts(leaders, juniorsList, leaderPrefs, juniorPrefs, n);

        var bestSolution = initialSolutions
            .OrderByDescending(sol => CalculateHarmonicMean(sol, leaders, juniorsList, leaderPrefs, juniorPrefs))
            .First();

        var optimizedSolution = GlobalOptimize(bestSolution, leaders, juniorsList, leaderPrefs, juniorPrefs);

        return CreateTeams(leaders, juniorsList, optimizedSolution);
    }

    private List<int[]> GenerateMultipleStarts(List<Employee> leaders, List<Employee> juniors,
        Dictionary<int, int[]> leaderPrefs, Dictionary<int, int[]> juniorPrefs, int n)
    {
        var solutions = new List<int[]>();

        for (var i = 0; i < 5; i++)
        {
            // Добавляем шум в матрицу
            var noisyMatrix = CreateNoisyCompatibilityMatrix(leaders, juniors, leaderPrefs, juniorPrefs, i);
            var hungarian = HungarianAlgorithm.HungarianAlgorithm.FindAssignments(noisyMatrix);
            var greedy = SolveGreedy(noisyMatrix);

            solutions.Add(hungarian);
            solutions.Add(greedy);
        }

        return solutions;
    }

    private int[,] CreateNoisyCompatibilityMatrix(List<Employee> leaders, List<Employee> juniors,
        Dictionary<int, int[]> leaderPrefs, Dictionary<int, int[]> juniorPrefs, int seed)
    {
        var n = leaders.Count;
        var matrix = new int[n, n];
        var noiseFactor = 0.1; // 10% шума

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                var leaderScore = GetPreferenceScore(leaderPrefs, leaders[i].Id, juniors[j].Id);
                var juniorScore = GetPreferenceScore(juniorPrefs, juniors[j].Id, leaders[i].Id);

                // Добавляем небольшой шум
                matrix[i, j] = -(leaderScore + juniorScore + _rnd.Next((int)(-noiseFactor * 10), (int)(noiseFactor * 10)));
            }
        }

        return matrix;
    }

    private int GetPreferenceScore(Dictionary<int, int[]> preferences, int participantId, int partnerId)
    {
        if (!preferences.ContainsKey(participantId)) return 1;
        var prefList = preferences[participantId];
        var index = Array.IndexOf(prefList, partnerId);
        return index >= 0 ? prefList.Length - index : 1;
    }

    private int[] SolveGreedy(int[,] matrix)
    {
        var n = matrix.GetLength(0);
        var assignments = new int[n];
        var usedJuniors = new HashSet<int>();

        for (var i = 0; i < n; i++)
        {
            int bestJunior = -1, bestScore = int.MaxValue;

            for (var j = 0; j < n; j++)
            {
                if (usedJuniors.Contains(j)) continue;
                if (matrix[i, j] >= bestScore) continue;
                bestScore = matrix[i, j];
                bestJunior = j;
            }

            assignments[i] = bestJunior;
            usedJuniors.Add(bestJunior);
        }

        return assignments;
    }

    private int[] GlobalOptimize(int[] assignments, List<Employee> leaders, List<Employee> juniors,
        Dictionary<int, int[]> leaderPrefs, Dictionary<int, int[]> juniorPrefs)
    {
        var improved = true;
        var n = assignments.Length;
        var noImprovementCount = 0;

        while (improved && noImprovementCount < 10)
        {
            improved = false;

            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var newAssignments = (int[])assignments.Clone();
                    (newAssignments[i], newAssignments[j]) = (newAssignments[j], newAssignments[i]);

                    var currentScore = CalculateHarmonicMean(assignments, leaders, juniors, leaderPrefs, juniorPrefs);
                    var newScore = CalculateHarmonicMean(newAssignments, leaders, juniors, leaderPrefs, juniorPrefs);

                    if (!(newScore > currentScore)) continue;
                    assignments = newAssignments;
                    improved = true;
                    noImprovementCount = 0;
                }
            }

            noImprovementCount++;
        }

        return assignments;
    }

    private double CalculateHarmonicMean(int[] assignments, List<Employee> leaders, List<Employee> juniors,
        Dictionary<int, int[]> leaderPrefs, Dictionary<int, int[]> juniorPrefs)
    {
        var satisfactions = assignments.Select((juniorIndex, leaderIndex) =>
        {
            var leaderScore = GetPreferenceScore(leaderPrefs, leaders[leaderIndex].Id, juniors[juniorIndex].Id);
            var juniorScore = GetPreferenceScore(juniorPrefs, juniors[juniorIndex].Id, leaders[leaderIndex].Id);

            return (double)(leaderScore + juniorScore);
        }).ToList();

        var sumInverse = satisfactions.Sum(s => 1.0 / s);
        return satisfactions.Count / sumInverse;
    }

    private List<Team> CreateTeams(List<Employee> leaders, List<Employee> juniors, int[] assignments)
    {
        return assignments.Select((t, i) => new Team(leaders[i], juniors[t])).ToList();
    }
}