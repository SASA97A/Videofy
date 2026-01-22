using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Video_Size_Optimizer.Models;
using Video_Size_Optimizer.Services;

namespace Video_Size_Optimizer.ViewModels;

public partial class RenameViewModel : ViewModelBase
{
    private readonly List<VideoFile> _targetFiles;
    private readonly FileService _fileService = new();

    [ObservableProperty] private int _renameMode = 0;
    [ObservableProperty] private string _findText = "";
    [ObservableProperty] private string _replaceText = "";
    [ObservableProperty] private string _addText = "";
    [ObservableProperty] private int _positionMode = 0;
    [ObservableProperty] private string _enumeratePattern = "Video_#";
    [ObservableProperty] private int _startNumber = 1;
    [ObservableProperty] private int _trimCount = 0;
    [ObservableProperty] private int _trimDirection = 0;

    [ObservableProperty] private string _previewText = "Select a mode to see preview";
    [ObservableProperty] private bool _isValid = true;

    public List<string> RenameModes => new() { "Replace Text", "Add Text", "Enumerate", "Trim (Delete)" };
    public List<string> PositionModes => new() { "Before Filename", "After Filename" };
    public List<string> TrimDirections => new() { "Delete from Start (Left)", "Delete from End (Right)" };
    public bool IsReplaceMode => RenameMode == 0;
    public bool IsAddMode => RenameMode == 1;
    public bool IsEnumerateMode => RenameMode == 2;
    public bool IsTrimMode => RenameMode == 3;

    public RenameViewModel(List<VideoFile> selectedFiles)
    {
        _targetFiles = selectedFiles;
        UpdatePreview();
    }  

    partial void OnRenameModeChanged(int value)
    {
        UpdatePreview();
        OnPropertyChanged(nameof(IsReplaceMode));
        OnPropertyChanged(nameof(IsAddMode));
        OnPropertyChanged(nameof(IsEnumerateMode));
        OnPropertyChanged(nameof(IsTrimMode));
    }

    partial void OnFindTextChanged(string value) => UpdatePreview();
    partial void OnReplaceTextChanged(string value) => ReplaceText = SanitizeInput(value);
    partial void OnAddTextChanged(string value) => AddText = SanitizeInput(value);
    partial void OnPositionModeChanged(int value) => UpdatePreview();
    partial void OnTrimCountChanged(int value) => UpdatePreview();
    partial void OnTrimDirectionChanged(int value) => UpdatePreview();
    partial void OnStartNumberChanged(int value) => UpdatePreview();

    partial void OnEnumeratePatternChanged(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains("#"))
        {
            EnumeratePattern = string.IsNullOrEmpty(value) ? "#" : value + "#";
        }
        EnumeratePattern = SanitizeInput(EnumeratePattern);
        UpdatePreview();
    }

    private string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Get invalid chars: < > : " / \ | ? *
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(input.Where(c => !invalidChars.Contains(c)).ToArray());

        UpdatePreview();
        return sanitized;
    }

    private void UpdatePreview()
    {
        if (!_targetFiles.Any()) return;

        var firstFile = _targetFiles.First();
        string dir = Path.GetDirectoryName(firstFile.FilePath) ?? "";
        string oldName = Path.GetFileNameWithoutExtension(firstFile.FilePath);
        string ext = Path.GetExtension(firstFile.FilePath);
        string newName = oldName;

        try
        {
            if (RenameMode == 0) // Replace
            {
                if (!string.IsNullOrEmpty(FindText))
                    newName = oldName.Replace(FindText, ReplaceText, StringComparison.OrdinalIgnoreCase);
            }
            else if (RenameMode == 1) // Add
            {
                newName = PositionMode == 0 ? $"{AddText}{oldName}" : $"{oldName}{AddText}";
            }
            else if (RenameMode == 2) // Enumerate
            {
                newName = EnumeratePattern.Replace("#", StartNumber.ToString("D2"));
            }
            else if (RenameMode == 3) // Trim
            {
                if (TrimCount > 0 && TrimCount < oldName.Length)
                {
                    newName = TrimDirection == 0
                        ? oldName.Substring(TrimCount)
                        : oldName.Substring(0, oldName.Length - TrimCount);
                }
                else if (TrimCount >= oldName.Length)
                {
                    newName = ""; // This will trigger the isEmpty error below
                }
            }

            // VALIDATION 
            bool isEmpty = string.IsNullOrWhiteSpace(newName);
            bool hasIllegal = newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
            bool isTooLong = !_fileService.IsPathLengthValid(dir, newName, ext);

            if (isEmpty)
            {
                IsValid = false;
                PreviewText = "Error: Filename cannot be empty!";
            }
            else if (hasIllegal)
            {
                IsValid = false;
                PreviewText = "Error: Contains illegal characters!";
            }
            else if (isTooLong)
            {
                IsValid = false;
                PreviewText = "Error: Path too long for Windows!";
            }
            else
            {         
                IsValid = true;
                PreviewText = $"Preview: {newName}{ext}";
            }
        }
        catch
        {
            IsValid = false;
            PreviewText = "Error: Invalid operation";
        }
    }

    public void ApplyRename()
    {
        if (!IsValid) return;

        int currentNum = StartNumber;
        foreach (var file in _targetFiles)
        {
            try
            {
                string dir = Path.GetDirectoryName(file.FilePath) ?? "";
                string ext = Path.GetExtension(file.FilePath);
                string oldName = Path.GetFileNameWithoutExtension(file.FilePath);
                string newName = oldName;

                if (RenameMode == 0)
                    newName = oldName.Replace(FindText, ReplaceText, StringComparison.OrdinalIgnoreCase);
                else if (RenameMode == 1)
                    newName = PositionMode == 0 ? $"{AddText}{oldName}" : $"{oldName}{AddText}";
                else if (RenameMode == 2)
                {
                    newName = EnumeratePattern.Replace("#", currentNum.ToString("D2"));
                    currentNum++;
                }
                else if (RenameMode == 3 && TrimCount < oldName.Length) 
                {
                        newName = TrimDirection == 0 ? oldName.Substring(TrimCount) : oldName.Substring(0, oldName.Length - TrimCount);       
                }

                string newPath = _fileService.GetUniqueFilePath(Path.Combine(dir, newName + ext));
                if (newPath != file.FilePath)
                {
                    File.Move(file.FilePath, newPath);
                    file.FilePath = newPath;
                }
            }
            catch { /* Skip locked */ }
        }
    }
}
