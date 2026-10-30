using FieldService.Application.Abstractions;
using FieldService.Presentation.Common;
using FieldService.Presentation.Diagnostics;
using Moq;
using NUnit.Framework;

namespace FieldService.Presentation.Tests.Diagnostics;

// Diagnostyka: „Wymuś awarię” – wyjątek nieobsłużony do pokazu raportu awarii przy następnym starcie
[TestFixture]
public sealed class DiagnosticsViewModelForceCrashTests
{
    private static DiagnosticsViewModel CreateViewModel()
    {
        // ZAŁOŻENIE: konstruktor bez zmian względem 14; handlery zapytań nie są używane przez ForceCrashCommand,
        // więc przekazujemy null (konstruktor ich nie sprawdza – primary constructor)
        return new DiagnosticsViewModel(
            Mock.Of<IRequestStatistics>(),
            Mock.Of<ILocalCaches>(),
            null!,
            null!,
            Mock.Of<ILocalizer>());
    }

    [Test]
    public void ForceCrashCommand_Always_IsAvailable()
    {
        var viewModel = CreateViewModel();

        Assert.That(viewModel.ForceCrashCommand, Is.Not.Null);
        Assert.That(viewModel.ForceCrashCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void ForceCrashCommand_Execute_ThrowsInvalidOperationException()
    {
        var viewModel = CreateViewModel();

        Assert.Throws<InvalidOperationException>(() => viewModel.ForceCrashCommand.Execute(null));
    }
}
