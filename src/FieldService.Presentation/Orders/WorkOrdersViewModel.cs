using System.Collections.ObjectModel;
using FieldService.Application.Orders.Queries;
using FieldService.Presentation.Common;
using FieldService.Presentation.Feedback;
using FieldService.Presentation.Navigation;

namespace FieldService.Presentation.Orders;

// „Moje zlecenia” – model odczytu z modelu Zlecenia (moduł Orders nie zna modułu Visits)
public sealed class WorkOrdersViewModel(GetMyWorkOrdersHandler workOrders, IUserFeedback feedback)
    : BaseViewModel, INavigatedTo<WorkOrdersRoute>
{
    public ObservableCollection<WorkOrderSummary> WorkOrders { get; } = [];

    public async Task OnNavigatedToAsync(WorkOrdersRoute route)
    {
        var result = await workOrders.HandleAsync(new GetMyWorkOrders(), CancellationToken.None);
        if (!result.IsSuccess)
        {
            await feedback.ShowErrorAsync(result.Error!);
            return;
        }

        WorkOrders.ReplaceWith(result.Value);
    }
}
