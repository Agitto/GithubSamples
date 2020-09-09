using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace GithubExamples {
    class Program {
        const string outputPath = "./xamarin_repos.json";
        
        static async Task Main(string[] args) {
            var githubApi = new GithubApi(args[1]);
            
            Console.WriteLine("getting repositories...");
            
            var repositories = await githubApi.GetRepositories();
            string repositoriesJson = JsonConvert.SerializeObject(repositories, Formatting.Indented);
            await githubApi.UpdateRepositoriesList(repositoriesJson);
        }
    }
}