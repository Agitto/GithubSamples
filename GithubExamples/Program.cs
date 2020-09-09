using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace GithubExamples {
    class Program {
        const string outputPath = "./xamarin_repos.json";
        
        static async Task Main(string[] args) {
            var githubApi = new GithubApi();
            githubApi.oauthToken = args[1];
            var gitRepositories = await githubApi.GetRepositories();
            string reposList = JsonConvert.SerializeObject(gitRepositories, Formatting.Indented);

            await githubApi.UpdateRepositoriesList(reposList);
        }
    }
    
    public class GithubApi {
        const string GithubEndpoint = "api.github.com";
        public string oauthToken;
        const string ExamplesRepository = "DevExpress-Examples";
        
        static string getRepositories = $"/users/{ExamplesRepository}/repos";
        static string getTree = $"/repos/{ExamplesRepository}" + "/{0}/git/trees/{1}";
        
        
        static string repositoriesPath = "/repos/Agitto/TeamcityExamples/contents/.teamcity/repos.json";

        public async Task<IEnumerable<GitRepository>> GetRepositories() {
            string[] repoToIgnore = { "t506284", "t294259", "t223734", "t279098", "t295653", "t297553", "t326911", "t499167", "t243160", "t221404", "t257726", "t178764", "t608114", "t208848" };
            
            List<GitRepository> repositories = new List<GitRepository>();
            int page = 1;
            var gitRepositories = await DoRequest<GitRepository[]>(getRepositories);
            
            while(gitRepositories.Length > 0) {
                gitRepositories = await DoRequest<GitRepository[]>(getRepositories, null, page++);
                
                var xamarinRepositories = gitRepositories.Where(
                    repo => 
                        repo.name.Contains("xamarin", StringComparison.OrdinalIgnoreCase) 
                     && repoToIgnore.All(ignoreId => !repo.name.Contains(ignoreId)));
                if(xamarinRepositories.Any()) {
                    repositories.AddRange(xamarinRepositories);
                }
            }

            foreach(var gitRepository in repositories) {
                var branches = await GetBranches(gitRepository.branches_url);
                gitRepository.Branches.AddRange(branches);
                
                foreach(GitBranch branch in gitRepository.Branches) {
                    string treeUrl = string.Format(getTree, gitRepository.name, branch.commit.sha);
                    var tree = await GetTree(treeUrl);
                    branch.sln = tree.tree.First(entry => Regex.IsMatch(entry.path, @"^CS\/.*\.sln$")).path;
                }
            }
            
            return repositories;
        }

        public async Task UpdateRepositoriesList(string repositories) {
            var gitRepositories = await DoRequest<GitRepository[]>("/users/Agitto/repos");
            GitRepository examplesRepo = gitRepositories.First(repo => repo.name.Contains("Teamcity"));
            var branches = await GetBranches(examplesRepo.branches_url);
            var master = branches.First();
            string treeUrl = string.Format("/repos/Agitto" + "/{0}/git/trees/{1}", examplesRepo.name, master.commit.sha);
            var tree = await GetTree(treeUrl);
            TreeEntry reposFile = tree.tree.FirstOrDefault(file => file.path.Contains("repos.json"));

            var commit = new CommitContent() {
                message = "update repositories",
                content = Base64Encode(repositories),
                sha = reposFile?.sha
            };
            
            var content = JsonConvert.SerializeObject(commit);
            var buffer = System.Text.Encoding.UTF8.GetBytes(content);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            SetupAuthHeaders(client);
            
            var uriBuilder = new UriBuilder() {
                Scheme = Uri.UriSchemeHttps,
                Port = -1,
                Host = GithubEndpoint,
                Path = repositoriesPath
            };
            
            using(client) {
                var response = await client.PutAsync(uriBuilder.Uri, byteContent);
            }
        }
        
        public static string Base64Encode(string plainText) {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public class CommitContent {
            public string message { get; set; }
            public string content { get; set; }
            public string sha { get; set; }
        }

        public async Task<IEnumerable<GitBranch>> GetBranches(string branchesUrl) {
            string branchesPath = branchesUrl.Split(GithubEndpoint)[1].Split("{")[0];
            return await DoRequest<List<GitBranch>>(branchesPath);
        }

        public async Task<Tree> GetTree(string treeUrl) {
            return await DoRequest<Tree>(treeUrl, new Dictionary<string, string> {
                { "recursive", "true" }
            });
        }

        void SetupAuthHeaders(HttpClient client) {
            client.DefaultRequestHeaders.Add("Authorization", $"token {oauthToken}");
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
        }

        async Task<T> DoRequest<T>(string path, Dictionary<string, string> customParameters = null, int? page = null) {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            SetupAuthHeaders(client);

            var uriBuilder = new UriBuilder() {
                Scheme = Uri.UriSchemeHttps,
                Port = -1,
                Host = GithubEndpoint,
                Path = path
            };

            var nameValueCollection = HttpUtility.ParseQueryString(uriBuilder.Query);
            nameValueCollection["type"] = "all";
            if(page.HasValue) {
                nameValueCollection["page"] = page.Value.ToString();
                nameValueCollection["per_page"] = "100";
            }

            if(customParameters != null) {
                foreach(string key in customParameters.Keys) {
                    nameValueCollection.Add(key, customParameters[key]);
                }
            }

            uriBuilder.Query = nameValueCollection.ToString();

            using(client)
            using(var response = await client.GetStreamAsync(uriBuilder.Uri)) 
            using(StreamReader sr = new StreamReader(response))
            using(JsonReader reader = new JsonTextReader(sr)) {
                var jsonSerializer = new JsonSerializer();
                var deserializeObject = jsonSerializer.Deserialize<T>(reader);
                return deserializeObject;
            }
        }
    }

    public class GitRepository {
        public int id { get; set; }
        public string name { get; set; }
        public string full_name { get; set; }
        public string description { get; set; }
        public string branches_url { get; set; }
        public string git_url { get; set; }
        public string clone_url { get; set; }
        public List<GitBranch> Branches { get; private set; } = new List<GitBranch>();
    }

    public class GitBranch {
        public string name { get; set; }
        public GitCommit commit { get; set; }
        public string sln { get; set; }
    }

    public class GitCommit {
        public string sha { get; set; }
    }

    public class Tree {
        public TreeEntry[] tree { get; set; }
    }

    public class TreeEntry {
        public string path { get; set; }
        public string sha { get; set; }
    }
}