using System.ComponentModel;

namespace SaReGaMidi.UI;

public sealed class TranscriptionRow : INotifyPropertyChanged
{
    private string _beat = string.Empty;

    public int SourcePage { get; set; }

    public string Section { get; set; } = string.Empty;

    public int? StartBeat { get; set; }

    public string Beat
    {
        get => _beat;
        internal set
        {
            if (_beat == value)
                return;

            _beat = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Beat)));
        }
    }

    public string Swaras { get; set; } = string.Empty;

    public string Lyrics { get; set; } = string.Empty;

    public bool ShowGrouping { get; set; }

    public bool? StartsPrintedLine { get; set; }

    public bool IsBlank { get; set; }

    internal TranscriptionRow? BorrowedFrom { get; set; }

    public string Annotation { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
}
