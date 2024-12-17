using Nsu.HackathonProblem.Contracts;

namespace Nsu.HackathonProblem.SkopintsevStrategy;

public class SkopintsevStrategy : ITeamBuildingStrategy
{
    public IEnumerable<Team> BuildTeams(IEnumerable<Employee> teamLeads, IEnumerable<Employee> juniors, 
        IEnumerable<Wishlist> teamLeadsWishlists, IEnumerable<Wishlist> juniorsWishlists)
    {
        var leaders = teamLeads.ToList();
        var juniorsList = juniors.ToList();

        var leaderPreferences = teamLeadsWishlists.ToDictionary(w => w.EmployeeId, w => w.DesiredEmployees);
        var juniorPreferences = juniorsWishlists.ToDictionary(w => w.EmployeeId, w => w.DesiredEmployees);

        var compatibilityMatrix = CreateCompatibilityMatrix(leaders, juniorsList, leaderPreferences, juniorPreferences);

        var assignments = HungarianAlgorithm.HungarianAlgorithm.FindAssignments(compatibilityMatrix);

        assignments = ImproveAssignments(assignments, leaders, juniorsList, leaderPreferences, juniorPreferences);

        return CreateTeams(leaders, juniorsList, assignments);
    }

    private int[,] CreateCompatibilityMatrix(List<Employee> leaders, List<Employee> juniors,
        Dictionary<int, int[]> leaderPrefs, Dictionary<int, int[]> juniorPrefs)
    {
        var n = leaders.Count;
        var matrix = new int[n, n];

        for (var i = 0; i < n; i++)
        {
            var leaderId = leaders[i].Id;

            for (var j = 0; j < n; j++)
            {
                var juniorId = juniors[j].Id;

                var leaderScore = GetPreferenceScore(leaderPrefs, leaderId, juniorId);
                var juniorScore = GetPreferenceScore(juniorPrefs, juniorId, leaderId);

                matrix[i, j] = -(leaderScore + juniorScore);
            }
        }

        return matrix;
    }

    private int GetPreferenceScore(Dictionary<int, int[]> preferences, int participantId, int partnerId)
    {
        if (!preferences.ContainsKey(participantId)) return 1;
        var prefList = preferences[participantId];
        var index = Array.IndexOf(prefList, partnerId);
        return index >= 0 ? prefList.Length - index : 1; // Минимум 1
    }

    private int[] ImproveAssignments(int[] assignments, List<Employee> leaders, List<Employee> juniors,
        Dictionary<int, int[]> leaderPrefs, Dictionary<int, int[]> juniorPrefs)
    {
        var improved = true;
        var n = assignments.Length;

        while (improved)
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
                }
            }
        }

        return assignments;
    }

    private double CalculateHarmonicMean(int[] assignments, List<Employee> leaders, List<Employee> juniors,
        Dictionary<int, int[]> leaderPrefs, Dictionary<int, int[]> juniorPrefs)
    {
        var satisfactions = assignments.Select((juniorIndex, leaderIndex) =>
        {
            var leaderId = leaders[leaderIndex].Id;
            var juniorId = juniors[juniorIndex].Id;

            var leaderScore = GetPreferenceScore(leaderPrefs, leaderId, juniorId);
            var juniorScore = GetPreferenceScore(juniorPrefs, juniorId, leaderId);

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
