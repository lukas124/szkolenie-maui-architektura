using CommunityToolkit.Mvvm.ComponentModel;
using FieldService.Domain.Visits;

namespace FieldService.Presentation.State;

// Filtr listy wizyt – obserwowalny stan współdzielony (właściciel: IAppState)
public sealed partial class VisitFilterState : ObservableObject
{
    [ObservableProperty]
    public partial bool OnlyOverdue { get; set; }

    // null = wszystkie statusy
    [ObservableProperty]
    public partial VisitStatus? Status { get; set; }
}
