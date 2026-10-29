namespace FieldService.Application.Parts.Queries;

// Model odczytu: część z katalogu (nazwa i cena jednostkowa)
public sealed record CatalogPart(string PartId, string Name, decimal UnitPrice);
