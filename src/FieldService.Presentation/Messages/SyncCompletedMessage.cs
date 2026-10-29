using FieldService.Application.Abstractions;

namespace FieldService.Presentation.Messages;

// Publikuj–subskrybuj: przebieg wysyłki zmian się skończył (wysyła korzeń kompozycji na wątku UI)
public sealed record SyncCompletedMessage(SyncResult Result);
