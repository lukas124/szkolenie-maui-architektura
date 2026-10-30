// retpath <plik.gcdump> <końcówka nazwy typu> – ile obiektów typu trzyma korzeń GC i najkrótsza ścieżka do pierwszego z nich.
// Użycie z katalogu głównego repo (wymaga dotnet-gcdump 9.0.661903: dotnet tool install -g dotnet-gcdump --version 9.0.661903):
//   dotnet run --project tools/retpath -- po10c.gcdump Visits.VisitDetailsViewModel
// Pomija korzenie „[finalizer Handles]” i „[Dependent Handles]” – trzymają obiekty warunkowo (kolejka finalizatora,
// ConditionalWeakTable), więc to nie wyciek. Czytnik zrzutu (GCHeapDump, MemoryGraph) pochodzi z dotnet-gcdump.
using Graphs;

var dump = new GCHeapDump(args[0]);
var g = dump.MemoryGraph;
var ns = g.AllocNodeStorage();
var child = g.AllocNodeStorage();
var ts = g.AllocTypeNodeStorage();
string TypeName(NodeIndex i, Node storage) => g.GetNode(i, storage).GetType(ts).Name;

// przeszukiwanie wszerz od korzenia: prev = skąd przyszliśmy (do odtworzenia ścieżki)
var prev = new Dictionary<NodeIndex, NodeIndex> { [g.RootIndex] = NodeIndex.Invalid };
var queue = new Queue<NodeIndex>([g.RootIndex]);
var found = new List<NodeIndex>();
while (queue.Count > 0)
{
    var idx = queue.Dequeue();
    var node = g.GetNode(idx, ns);
    if (node.GetType(ts).Name.EndsWith(args[1])) found.Add(idx);
    for (var c = node.GetFirstChildIndex(); c != NodeIndex.Invalid; c = node.GetNextChildIndex())
    {
        if (prev.ContainsKey(c)) continue;
        var name = TypeName(c, child);
        if (name.Contains("finalizer Handles") || name.Contains("Dependent Handles")) continue;
        prev[c] = idx;
        queue.Enqueue(c);
    }
}

Console.WriteLine($"{args[1]}: trzymanych przez korzenie GC: {found.Count}");
if (found.Count == 0) return;
var path = new List<string>();
for (var i = found[0]; i != NodeIndex.Invalid; i = prev[i]) path.Add(TypeName(i, ns));
path.Reverse();
for (var k = 0; k < path.Count; k++) Console.WriteLine(new string(' ', k * 2) + (k > 0 ? "└ " : "") + path[k]);
