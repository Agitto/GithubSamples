using System.Collections.Generic;

namespace GithubExamples {
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

    public class CommitContent {
        public string message { get; set; }
        public string content { get; set; }
        public string sha { get; set; }
    }
    
    public class GitFileContent : TreeEntry {
        public string content { get; set; }
    }
}