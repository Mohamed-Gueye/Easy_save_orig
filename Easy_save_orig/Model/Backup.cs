using System;

namespace Easy_Save.Model;
public class Backup
{
    public string? Name { get; set; }
    public string SourceDirectory { get; set; }
    public string TargetDirectory { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Progress { get; set; } = "0%";

}
