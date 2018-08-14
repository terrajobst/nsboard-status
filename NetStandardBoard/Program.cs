using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Octokit;

namespace NetStandardBoard
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var result = new List<PullRequestStatus>();
            var client = new GitHubClient(new ProductHeaderValue("NsBoardReport"));
            client.Credentials = new Credentials("");

            var orgName = "dotnet";
            var repoName = "standard";

            var teamByMember = new Dictionary<string, string>();
            var teamNames = new List<string>();
            var teams = await client.Organization.Team.GetAll(orgName);

            foreach (var team in teams)
            {
                if (team.Name.StartsWith("nsboard-", StringComparison.OrdinalIgnoreCase))
                {
                    teamNames.Add(team.Name);

                    var members = await client.Organization.Team.GetAllMembers(team.Id);

                    foreach (var member in members)
                        teamByMember.Add(member.Login, team.Name);
                }
            }

            var pullRequests = await client.PullRequest.GetAllForRepository(orgName, repoName);

            foreach (var pr in pullRequests)
            {
                var issue = await client.Issue.Get(orgName, repoName, pr.Number);

                if (!HasLabel(issue, "netstandard-api"))
                    continue;

                var teamStatus = new Dictionary<string, PullRequestReviewState>();

                var reviews = await client.PullRequest.Review.GetAll(orgName, repoName, pr.Number);

                foreach (var review in reviews)
                {
                    if (teamByMember.TryGetValue(review.User.Login, out var teamName))
                        teamStatus[teamName] = review.State.Value;
                }

                var teamStatusList = new List<PullRequestReviewState>(teamNames.Count);

                foreach (var team in teamNames)
                {
                    if (teamStatus.TryGetValue(team, out var status))
                        teamStatusList.Add(status);
                    else
                        teamStatusList.Add(PullRequestReviewState.Pending);
                }

                var prStatus = new PullRequestStatus(pr.Number, pr.Title, pr.Url, teamStatusList.ToArray());
                result.Add(prStatus);
            }

            using (var writer = new StreamWriter(@"P:\results.csv"))
            {
                writer.Write("ID;Title;Url;");

                foreach (var team in teamNames)
                {
                    writer.Write(team);
                    writer.Write(";");
                }

                writer.WriteLine();

                foreach (var prStatus in result)
                {
                    writer.Write(prStatus.Number);
                    writer.Write(";");
                    writer.Write(prStatus.Title);
                    writer.Write(";");
                    writer.Write(prStatus.Url);
                    writer.Write(";");

                    foreach (var state in prStatus.BoardStatus)
                    {
                        writer.Write(state);
                        writer.Write(";");
                    }

                    writer.WriteLine();
                }
            }
        }

        private static bool HasLabel(Issue issue, string label)
        {
            foreach (var l in issue.Labels)
            {
                if (string.Equals(l.Name, label, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private sealed class PullRequestStatus
        {
            public PullRequestStatus(int number, string title, string url, IReadOnlyList<PullRequestReviewState> boardStatus)
            {
                Number = number;
                Title = title;
                Url = url;
                BoardStatus = boardStatus;
            }

            public int Number { get; }
            public string Title { get; }
            public string Url { get; }
            public IReadOnlyList<PullRequestReviewState> BoardStatus { get; }
        }
    }
}
