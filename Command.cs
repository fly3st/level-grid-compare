using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace LevelGridCompare
{
    /// <summary>
    /// Compares Levels and Grids between the host (architectural) model
    /// and the first loaded linked model (structural).
    ///
    /// Output:
    ///   1. TaskDialog with a summary
    ///   2. CSV on the Desktop (LevelGridCompare.csv) — opens in Excel,
    ///      Hebrew-safe (UTF-8 with BOM)
    ///
    /// Statuses reported:
    ///   MATCHED   — same name, position within tolerance
    ///   MOVED     — same name, position differs beyond tolerance (delta shown in mm)
    ///   HOST ONLY — exists in host, missing (or renamed) in link
    ///   LINK ONLY — exists in link, missing (or renamed) in host
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    public class CompareCommand : IExternalCommand
    {
        // Tolerance: positions closer than this count as "matched".
        // Revit internal units are FEET. 5 mm ≈ 0.0164 ft.
        private const double ToleranceMm = 5.0;
        private static readonly double ToleranceFt =
            UnitUtils.ConvertToInternalUnits(ToleranceMm, UnitTypeId.Millimeters);

        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // ---- 1. Find the first loaded link (your "structure" model) ----
            RevitLinkInstance linkInst = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(li => li.GetLinkDocument() != null);

            if (linkInst == null)
            {
                TaskDialog.Show("Level & Grid Compare",
                    "No loaded linked model found.\n" +
                    "Link the structural model into this file and try again.");
                return Result.Cancelled;
            }

            Document linkDoc = linkInst.GetLinkDocument();
            // Transform that maps link coordinates into host coordinates
            Transform xform = linkInst.GetTotalTransform();

            var rows = new List<string[]>();

            // ---- 2. Compare LEVELS by name + elevation ----
            Dictionary<string, double> hostLevels = CollectLevels(doc, null);
            Dictionary<string, double> linkLevels = CollectLevels(linkDoc, xform);
            CompareByName(
                hostLevels, linkLevels, "Level", rows,
                (hostVal, linkVal) => Math.Abs(hostVal - linkVal),
                delta => $"dZ = {FtToMm(delta):0.0} mm");

            // ---- 3. Compare GRIDS by name + position of the grid line ----
            Dictionary<string, Curve> hostGrids = CollectGrids(doc, null);
            Dictionary<string, Curve> linkGrids = CollectGrids(linkDoc, xform);
            CompareByName(
                hostGrids, linkGrids, "Grid", rows,
                DistanceBetweenCurves,
                delta => $"offset = {FtToMm(delta):0.0} mm");

            // ---- 4. Write CSV to Desktop ----
            string csvPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "LevelGridCompare.csv");

            var csv = new StringBuilder();
            csv.AppendLine("Category,Name,Status,Detail");
            foreach (string[] r in rows)
                csv.AppendLine(string.Join(",", r.Select(EscapeCsv)));

            // UTF-8 with BOM so Hebrew names display correctly in Excel
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));

            // ---- 5. Summary dialog ----
            int matched = rows.Count(r => r[2] == "MATCHED");
            int moved = rows.Count(r => r[2] == "MOVED");
            int hostOnly = rows.Count(r => r[2] == "HOST ONLY");
            int linkOnly = rows.Count(r => r[2] == "LINK ONLY");

            TaskDialog.Show("Level & Grid Compare",
                $"Compared against link: {linkDoc.Title}\n\n" +
                $"MATCHED:    {matched}\n" +
                $"MOVED:      {moved}\n" +
                $"HOST ONLY:  {hostOnly}\n" +
                $"LINK ONLY:  {linkOnly}\n\n" +
                $"Full report saved to:\n{csvPath}");

            return Result.Succeeded;
        }

        // ============ Collectors ============

        /// <summary>Level name -> elevation in host coordinates (feet).</summary>
        private static Dictionary<string, double> CollectLevels(Document d, Transform xform)
        {
            var dict = new Dictionary<string, double>();
            var levels = new FilteredElementCollector(d)
                .OfClass(typeof(Level))
                .Cast<Level>();

            foreach (Level lvl in levels)
            {
                double elev = lvl.Elevation;
                if (xform != null)
                    elev = xform.OfPoint(new XYZ(0, 0, elev)).Z;
                dict[lvl.Name] = elev;   // duplicate names: last one wins
            }
            return dict;
        }

        /// <summary>Grid name -> its curve in host coordinates.</summary>
        private static Dictionary<string, Curve> CollectGrids(Document d, Transform xform)
        {
            var dict = new Dictionary<string, Curve>();
            var grids = new FilteredElementCollector(d)
                .OfClass(typeof(Grid))
                .Cast<Grid>();

            foreach (Grid g in grids)
            {
                Curve c = g.Curve;
                if (c == null) continue;
                if (xform != null)
                    c = c.CreateTransformed(xform);
                dict[g.Name] = c;
            }
            return dict;
        }

        // ============ Comparison core ============

        /// <summary>
        /// Generic name-based comparison:
        /// items present in both -> measure delta -> MATCHED or MOVED;
        /// otherwise HOST ONLY / LINK ONLY.
        /// </summary>
        private static void CompareByName<T>(
            Dictionary<string, T> host,
            Dictionary<string, T> link,
            string category,
            List<string[]> rows,
            Func<T, T, double> measureDelta,
            Func<double, string> describeDelta)
        {
            foreach (var kv in host.OrderBy(k => k.Key))
            {
                if (link.TryGetValue(kv.Key, out T linkVal))
                {
                    double delta = measureDelta(kv.Value, linkVal);
                    string status = delta <= ToleranceFt ? "MATCHED" : "MOVED";
                    string detail = delta <= ToleranceFt ? "" : describeDelta(delta);
                    rows.Add(new[] { category, kv.Key, status, detail });
                }
                else
                {
                    rows.Add(new[] { category, kv.Key, "HOST ONLY", "missing or renamed in link" });
                }
            }

            foreach (var kv in link.OrderBy(k => k.Key))
            {
                if (!host.ContainsKey(kv.Key))
                    rows.Add(new[] { category, kv.Key, "LINK ONLY", "missing or renamed in host" });
            }
        }

        /// <summary>
        /// Distance between two grid curves: project the host curve's midpoint
        /// onto the link curve and measure. Good enough for parallel/renamed
        /// grid detection; V2 can add angle checks.
        /// </summary>
        private static double DistanceBetweenCurves(Curve hostCurve, Curve linkCurve)
        {
            XYZ mid = hostCurve.Evaluate(0.5, true);
            IntersectionResult proj = linkCurve.Project(mid);
            return proj != null ? proj.Distance : double.MaxValue;
        }

        // ============ Helpers ============

        private static double FtToMm(double feet) =>
            UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);

        private static string EscapeCsv(string s)
        {
            if (s == null) return "";
            if (s.Contains(',') || s.Contains('"'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
