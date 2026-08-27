using PapayApp.Core.Models;

namespace PapayApp.UI.ViewModels;

public sealed class CertificateParticipantRowViewModel : BaseViewModel
{
    private bool _isSelected = true;

    public CertificateParticipantRowViewModel(CertificateParticipant participant)
    {
        Participant = participant;
    }

    public CertificateParticipant Participant { get; }

    public int SourceRowNumber => Participant.SourceRowNumber;

    public string FullName => Participant.FullName;

    public string Identification => Participant.Identification;

    public string Summary => $"Fila {SourceRowNumber} · {Identification}";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
