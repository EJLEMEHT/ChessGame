using System;
using System.Collections.Generic;
using System.IO;


[Serializable]
public class Problems
{
    public List<String[]> problems = new List<String[]>();
    public void LoadProblems()
    {
        var textFile = new StreamReader("Assets\\Scripts\\Problems\\ChessProblems.txt");

        string[] lines = textFile.ReadToEnd().Split('\n');
        for (int i = 0; i < lines.Length / 5; i++)
        {
            problems.Add(new string[] { lines[i * 5], lines[i * 5 + 1], lines[i * 5 + 2] });
        }

        textFile.Close();
    }
    public string[] GetRandomProblem()
    {
        System.Random random = new System.Random();
        int index = random.Next(problems.Count);
        return problems[index];
    }
}

