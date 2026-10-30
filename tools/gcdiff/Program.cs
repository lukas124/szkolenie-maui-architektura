// gcdiff <przed.gcdump> <po.gcdump> [fragment nazwy typu] – porównanie dwóch zrzutów sterty: których typów przybyło.
// Tabela: liczba obiektów i rozmiar w bajtach przed, po i przyrost; malejąco po przyroście liczby, tylko typy z przyrostem,
// pierwsze 30 wierszy + podsumowanie. Tekst nadaje się do wklejenia do LLM.
// Użycie z katalogu głównego repo (wymaga dotnet-gcdump 9.0.661903: dotnet tool install -g dotnet-gcdump --version 9.0.661903):
//   dotnet run --project tools/gcdiff -- po1.gcdump po10.gcdump FieldService
// Zrzuty z dotnet-gcdump albo z .NET Meteor. Działa na macOS, Windows i Linux.
// Uwaga: zrzuty rób po wymuszonym GC (strona Diagnostyka „Wymuś GC” albo 3 zrzuty co ~5 s – liczy się ostatni),
// inaczej różnica obejmuje też obiekty czekające na finalizację, a to nie wyciek.
// Czytnik zrzutu (GCHeapDump, MemoryGraph) pochodzi z dotnet-gcdump.
using Graphs;

if (args.Length < 2)
{
    Console.WriteLine("Użycie: dotnet run --project tools/gcdiff -- przed.gcdump po.gcdump [fragment-nazwy-typu]");
    return;
}
Console.OutputEncoding = System.Text.Encoding.UTF8; // polskie znaki także w konsoli Windows
foreach (var file in args.Take(2))
{
    if (File.Exists(file)) continue;
    Console.WriteLine($"Brak pliku: {file}");
    return;
}
var filter = args.Length > 2 ? args[2] : "";
const int Top = 30;

var before = CountByType(args[0]);
var after = CountByType(args[1]);

// wiersz tabeli dla każdego typu z któregokolwiek zrzutu
var rows = before.Keys.Union(after.Keys)
    .Where(name => name.Contains(filter, StringComparison.OrdinalIgnoreCase))
    .Select(name =>
    {
        var b = before.GetValueOrDefault(name);
        var a = after.GetValueOrDefault(name);
        return (Name: name, Before: b, After: a, Diff: a.Count - b.Count, SizeDiff: a.Size - b.Size);
    })
    .ToList();
var grown = rows.Where(r => r.Diff > 0)
    .OrderByDescending(r => r.Diff).ThenByDescending(r => r.SizeDiff).ThenBy(r => r.Name)
    .ToList();

Console.WriteLine($"gcdiff {Path.GetFileName(args[0])} -> {Path.GetFileName(args[1])}" + (filter != "" ? $" (typy zawierające „{filter}”)" : ""));
Console.WriteLine();
Console.WriteLine($"{"przed",8} {"po",8} {"przyrost",8} {"B przed",10} {"B po",10} {"B przyrost",10}  typ");
foreach (var r in grown.Take(Top))
    Console.WriteLine($"{r.Before.Count,8} {r.After.Count,8} {r.Diff,8:+0;-0;0} {r.Before.Size,10} {r.After.Size,10} {r.SizeDiff,10:+0;-0;0}  {r.Name}");
if (grown.Count == 0) Console.WriteLine("(brak typów z przyrostem liczby obiektów)");
Console.WriteLine();
Console.WriteLine($"Typów z przyrostem: {grown.Count}" + (grown.Count > Top ? $" (pokazano {Top})" : ""));
Console.WriteLine($"Obiektów: {rows.Sum(r => r.Before.Count)} -> {rows.Sum(r => r.After.Count)} " +
                  $"({rows.Sum(r => r.Diff):+0;-0;0}), rozmiar: {Kb(rows.Sum(r => r.Before.Size))} KB -> {Kb(rows.Sum(r => r.After.Size))} KB " +
                  $"({Kb(rows.Sum(r => r.SizeDiff)):+0;-0;0} KB)");

// liczba obiektów i suma rozmiarów (bajty) dla każdej nazwy typu w zrzucie
static Dictionary<string, (int Count, long Size)> CountByType(string file)
{
    var g = new GCHeapDump(file).MemoryGraph;
    var node = g.AllocNodeStorage();
    var type = g.AllocTypeNodeStorage();
    var result = new Dictionary<string, (int Count, long Size)>();
    for (NodeIndex i = 0; i < g.NodeIndexLimit; i++)
    {
        g.GetNode(i, node);
        if (node.Size == 0) continue; // węzły pomocnicze (korzenie, grupy) – nie obiekty na stercie
        var name = node.GetType(type).Name;
        var (count, size) = result.GetValueOrDefault(name);
        result[name] = (count + 1, size + node.Size);
    }
    return result;
}

// bajty -> KB (zaokrąglone) do podsumowania
static long Kb(long bytes) => (long)Math.Round(bytes / 1024.0);
