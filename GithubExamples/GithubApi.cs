using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace GithubExamples {
    public class GithubApi {
        const string GithubEndpoint = "api.github.com";
        const string ExamplesRepositoryOwner = "DevExpress-Examples";
        const string NativeMobileRepository = "native-mobile";
        const string NativeMobileRepositoryOwner = "DevExpress";
        
        static string getRepository = "/repos/{0}/{1}";
        static string getRepositories = "/users/{0}/repos";
        static string getTree = "/repos/{0}/{1}/git/trees/{2}";

        static string getRepositoriesFileContent = $"/repos/{NativeMobileRepositoryOwner}/{NativeMobileRepository}/contents/.teamcity/repos.json";

        string oauthToken;
        
        public GithubApi(string token) {
            oauthToken = token;
        }
        
        public async Task<IEnumerable<GitRepository>> GetRepositories() {
            string[] repoToIgnore = { "t506284", "t294259", "t223734", "t279098", "t295653", "t297553", "t326911", "t499167", "t243160", "t221404", "t257726", "t178764", "t608114", "t208848" };
            
            List<GitRepository> repositories = new List<GitRepository>();
            int page = 1;
            string examplesPath = string.Format(getRepositories, ExamplesRepositoryOwner);
            var gitRepositories = await Get<GitRepository[]>(examplesPath);
            
            while(gitRepositories.Length > 0) {
                gitRepositories = await Get<GitRepository[]>(examplesPath, null, page++);
                
                var xamarinRepositories = gitRepositories.Where(
                    repo => 
                        repo.name.Contains("xamarin", StringComparison.OrdinalIgnoreCase) 
                     && repoToIgnore.All(ignoreId => !repo.name.Contains(ignoreId)));
                if(xamarinRepositories.Any()) {
                    repositories.AddRange(xamarinRepositories);
                }
            }

            foreach(var gitRepository in repositories) {
                Console.WriteLine($"found {gitRepository.name}");
                var branches = await GetBranches(gitRepository.branches_url);
                gitRepository.Branches.AddRange(branches);
                
                foreach(GitBranch branch in gitRepository.Branches) {
                    string treeUrl = string.Format(getTree, ExamplesRepositoryOwner, gitRepository.name, branch.commit.sha);
                    var tree = await GetTree(treeUrl);
                    branch.sln = tree.tree.First(entry => Regex.IsMatch(entry.path, @"^CS\/.*\.sln$")).path;
                }
            }
            
            return repositories;
        }

        public async Task UpdateRepositoriesList(string repositories) {
            GitRepository examplesRepo = await Get<GitRepository>(string.Format(getRepository, NativeMobileRepositoryOwner, NativeMobileRepository));
            var branches = await GetBranches(examplesRepo.branches_url);
            var devBranch = branches.First(branch => branch.name == "dev");
            string treeUrl = string.Format(getTree, NativeMobileRepositoryOwner, NativeMobileRepository, devBranch.commit.sha);
            var tree = await GetTree(treeUrl);
            TreeEntry reposFile = tree.tree.FirstOrDefault(file => file.path.Contains("repos.json"));
            var currentContent = await Get<GitFileContent>(getRepositoriesFileContent);
            var repositoriesBase64 = Base64Encode(repositories);

            var decodedContent = Base64Decode(currentContent.content);
            if(decodedContent == repositories) {
                Console.WriteLine("nothing new");
                return;
            }
            
            Console.WriteLine("adding new examples...");
            var commit = new CommitContent() {
                message = "update repositories",
                content = repositoriesBase64,
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
                Path = getRepositoriesFileContent
            };
            
            using(client) {
                var response = await client.PutAsync(uriBuilder.Uri, byteContent);
            }
        }

        async Task<IEnumerable<GitBranch>> GetBranches(string branchesUrl) {
            string branchesPath = branchesUrl.Split(GithubEndpoint)[1].Split("{")[0];
            return await Get<List<GitBranch>>(branchesPath);
        }

        async Task<Tree> GetTree(string treeUrl) {
            return await Get<Tree>(treeUrl, new Dictionary<string, string> {
                { "recursive", "true" }
            });
        }

        async Task<T> Get<T>(string path, Dictionary<string, string> customParameters = null, int? page = null) {
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
            await using(var response = await client.GetStreamAsync(uriBuilder.Uri)) 
            using(StreamReader sr = new StreamReader(response))
            using(JsonReader reader = new JsonTextReader(sr)) {
                var jsonSerializer = new JsonSerializer();
                var deserializeObject = jsonSerializer.Deserialize<T>(reader);
                return deserializeObject;
            }
        }
        
        void SetupAuthHeaders(HttpClient client) {
            client.DefaultRequestHeaders.Add("Authorization", $"token {oauthToken}");
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("product", "1"));
        }

        static string Base64Encode(string plainText) {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        static string Base64Decode(string base64EncodedData) {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}