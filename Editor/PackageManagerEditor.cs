using System.Collections.Generic;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;

public class PackageManagerEditor : OdinEditorWindow
{
    [MenuItem("Tools/CustomPackager(submodule)Manager")]
    private static void OpenWindow()
    {
        GetWindow<PackageManagerEditor>().Show();
    }

    [OnInspectorGUI]
    [PropertyOrder(-1f)]
    private void DrawTopInfoBox()
    {
        SirenixEditorGUI.InfoMessageBox("Make sure that you have GIT successfully installed and providing Package/Submodule SSH!");
    }

    [ListDrawerSettings(
    CustomAddFunction = "AddGitPackage",
    CustomRemoveIndexFunction = "RemoveGitPackage")]
    public List<GitPackageData> Data = new List<GitPackageData>();

    private GitPackageData AddGitPackage()
    {
        return new GitPackageData();
    }

    private void RemoveGitPackage(int index)
    {
        this.Data.RemoveAt(index);
    }
    
}

public class GitPackageData
{
    [ShowInInspector, ReadOnly]
    [LabelWidth(150)]
    [BoxGroup("PackageInfo")]
    public string NameOfPackage;

    [ShowInInspector, ReadOnly]
    [LabelWidth(150)]
    [BoxGroup("PackageInfo")]
    public string DisplayName;

    [ShowInInspector, ReadOnly]
    [LabelWidth(150)]
    [BoxGroup("PackageInfo")]
    public string VersionOfPackage;

    [ValidateInput("IsValidRepo")]
    [LabelWidth(150)]
    [GUIColor(1.0f, 1.0f, 0f)]
    [BoxGroup("GitRepoInfo")]
    public string SshRepo;

    List<string> ListOfVersions;

    [ValidateInput("IsValidVersion")]
    [ValueDropdown("ListOfVersions")]
    [OnValueChanged("ChangeVersion")]
    [LabelWidth(150)]
    [BoxGroup("GitRepoInfo")]
    public string CurrentBranch;


    static AddRequest Request;


    public GitPackageData()
    {
        NameOfPackage = "";
        SshRepo = "";
        CurrentBranch = "";
        ListOfVersions = new List<string>();
    }

    [Button(ButtonSizes.Small), HorizontalGroup, PropertySpace(SpaceAfter = 20)]
    [GUIColor(0.0f, 1.0f, 0.0f)]
    public void ConnectGitPackage()
    {
        Request = Client.Add(SshRepo);
        EditorApplication.update += Progress;
    }

    [Button(ButtonSizes.Small), HorizontalGroup, PropertySpace(SpaceAfter = 20)]
    [GUIColor(0.0f, 0.0f, 1.0f)]
    public void RefreshVersions()
    {
        ListOfVersions = new List<string>();
        Process process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.Arguments = "ls-remote --heads " + SshRepo;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        while (!process.StandardOutput.EndOfStream)
        {
            string line = process.StandardOutput.ReadLine();
            if (line.Contains("refs/heads"))
            {
                int startIndex = line.LastIndexOf("/") + 1;
                string branchName = line.Substring(startIndex);
                ListOfVersions.Add(branchName);
            }
        }
        process.WaitForExit();
    }

    void Progress()
    {
        if (Request.IsCompleted)
        {
            if (Request.Status == StatusCode.Success)
            {
                UnityEngine.Debug.Log("Installed: " + Request.Result.packageId);
                UnityEngine.Debug.Log("Name: " + Request.Result.name);
                NameOfPackage = Request.Result.name;
                DisplayName = Request.Result.displayName;

                RefreshVersions();
            }
            else if (Request.Status >= StatusCode.Failure)
                UnityEngine.Debug.Log(Request.Error.message);

            EditorApplication.update -= Progress;
        }
    }

    private bool IsValidRepo(string value)
    {
        return value.Contains("git@github.com:");
    }

    private bool IsValidVersion(string value)
    {
        return value != "";
    }

    private void ChangeVersion()
    {
        string manifestPath = "Packages/manifest.json";
        string json = File.ReadAllText(manifestPath);
        JObject jsonObject = JObject.Parse(json);

        string gitUrl = SshRepo;
        string httpsUrl = gitUrl.Replace("git@github.com:", "https://github.com/");
        httpsUrl += "#";

        if (!CurrentBranch.Contains('v'))
        {
            jsonObject["dependencies"][NameOfPackage] = httpsUrl;
        } 
        else
        {
            jsonObject["dependencies"][NameOfPackage] = httpsUrl + CurrentBranch;
        }
        File.WriteAllText(manifestPath, jsonObject.ToString());

        //TODO But this is not working so
        //var packageInfos = UnityEditor.PackageManager.Client.Search(NameOfPackage);
        //VersionOfPackage = packageInfos.Result[0].version;

        //TODO This is horrible approach
        ListRequest listRequest = Client.List();
        while (!listRequest.IsCompleted) { }
        foreach (var packageInfo in listRequest.Result)
        {
            if (packageInfo.name == NameOfPackage)
            {
                VersionOfPackage = packageInfo.version;
                break;
            }
        }

        EditorApplication.delayCall += () =>
        {
            EditorApplication.ExecuteMenuItem("Assets/Refresh");
            ListRequest listRequest = Client.List(true);
            while (!listRequest.IsCompleted) { }
        };
    }
}
