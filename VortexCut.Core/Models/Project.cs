namespace VortexCut.Core.Models;

/// <summary>
/// VortexCut 프로젝트 모델
/// </summary>
public class Project
{
    public string Name { get; set; } = "Untitled Project";
    public uint Width { get; set; } = 1920;
    public uint Height { get; set; } = 1080;
    public double Fps { get; set; } = 30.0;
    public List<ClipModel> Clips { get; set; } = new();
    public string? FilePath { get; set; }

    public Project() { }

    public Project(string name, uint width, uint height, double fps)
    {
        Name = name;
        Width = width;
        Height = height;
        Fps = fps;
    }
}
