using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FieldService.Application.Visits.Queries;
using FieldService.Domain.Visits;
using FieldService.Presentation.Collections;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Visits;

// Historia wizyt ze stronicowaniem: lista dociąga następną stronę, gdy zbliża się koniec (RemainingItemsThreshold).
// Strona = jedno AddRange (jedno powiadomienie dla listy).
public sealed partial class VisitHistoryViewModel(GetVisitHistoryHandler history, IVisitItemFactory items,
    IUserFeedback feedback, INavigationService navigation) : BaseViewModel, INavigatedTo<VisitHistoryRoute>
{
    public const int PageSize = 30;

    // ostatnio wczytana strona; 0 = jeszcze nic
    private int _page;

    public BatchObservableCollection<VisitHistoryItem> Visits { get; } = [];

    // false = wszystkie wpisy już na liście (albo ekran jeszcze nie otwarty)
    [ObservableProperty]
    public partial bool HasMore { get; set; }

    public Task OnNavigatedToAsync(VisitHistoryRoute route)
    {
        HasMore = true;
        return LoadNextPageAsync();
    }

    [RelayCommand]
    private Task LoadMoreAsync() => !HasMore || IsBusy ? Task.CompletedTask : LoadNextPageAsync();

    private async Task LoadNextPageAsync()
    {
        IsBusy = true;
        try
        {
            var result = await history.HandleAsync(new GetVisitHistory(_page + 1, PageSize), CancellationToken.None);
            if (!result.IsSuccess)
            {
                await feedback.ShowErrorAsync(result.Error!);
                return;
            }

            var page = result.Value;
            Visits.AddRange(page.Items.Select(entry => items.CreateHistory(entry, () => OpenAsync(entry.Id))));
            _page = page.Page;
            HasMore = Visits.Count < page.TotalCount;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task OpenAsync(Guid id) => navigation.NavigateAsync(new VisitDetailsRoute(new VisitId(id)));
}
