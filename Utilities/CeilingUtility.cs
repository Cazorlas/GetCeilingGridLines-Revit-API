using Autodesk.Revit.DB;
using PaperLibrary.Core;
using PaperLibrary.Extensions;
using PaperLibrary.Utilities.RevitUtilities.Models;

namespace PaperLibrary.Utilities.RevitUtilities
{
    public class CeilingUtility
    {
        // Reference to the current Revit document.
        private static Document _doc = ServiceBootstrapper.Revit.Doc;
        private static View _view = _doc.ActiveView;

        /// <summary>
        /// Retrieves ceiling grid lines as a collection of <see cref="Curve"/> objects.
        /// </summary>
        /// <param name="ceiling">The target <see cref="Ceiling"/> element.</param>
        /// <param name="includeBoundary">
        /// If <c>true</c>, includes boundary curves of the ceiling's bottom face.
        /// </param>
        /// <returns>
        /// A list of grid line <see cref="Curve"/> objects, or an empty list if none are found.
        /// </returns>
        /// <remarks>
        /// On Revit 2025.3+ (.NET Core), uses the native API:
        /// <c>Ceiling.GetCeilingGridLines(bool)</c>.
        /// On older (.NET Framework) environments, reconstructs grid geometry by inferring
        /// spacing and direction from stable references and slicing with the ceiling solid.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when grid line extraction fails.
        /// </exception>
        public static List<Curve> GetCeilingGridLines(Ceiling ceiling, bool includeBoundary)
        {
#if NETCORE
            // Revit 2025.3+ (.NET Core) — use the native API directly.
            try
            {
                return ceiling.GetCeilingGridLines(includeBoundary).ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get ceiling grid lines using native API: {ex.Message}", ex);
            }
#else
            // .NET Framework — build grid lines via geometric inference (workaround approach).
            try
            {
                List<Curve> gridCurves = [];

                // Wrap the ceiling element to access its bottom face, solid, boundary, bbox, etc.
                CeilingData ceilingData = new(ceiling);

                // Compute the Z elevation of the bottom face and a safe "infinite" line length
                // based on the element's bounding box (extended to ensure we cover the whole ceiling).
                double zLevel = ceilingData.BottomFace.Origin.Z;
                double lineLength = Math.Max(
                    ceilingData.BoundingBoxXYZ.Max.X - ceilingData.BoundingBoxXYZ.Min.X,
                    ceilingData.BoundingBoxXYZ.Max.Y - ceilingData.BoundingBoxXYZ.Min.Y
                ) * 2;

                // Number of grid lines to generate in each direction (both positive and negative sides).
                int lineCount = 150;
                List<Line> infiniteLines = [];
                Console.WriteLine(ceilingData.BottomFaceStableReference);
                // ----- U direction (horizontal) — use stable reference indices {1, 5} -----
                // We attempt to retrieve two references that define the grid spacing along U.
                var refArrayU = GetGridReferences(ceilingData.BottomFaceStableReference, new[] { 1, 5 });
                if (refArrayU.Size >= 2)
                {
                    // Build GridData by measuring spacing (offset) and detecting directions via a temporary dimension.
                    GridData gridDataU = GetGridDataFromReferences(refArrayU);
                    if (gridDataU != null)
                    {
                        // Generate long lines (to be clipped later by the solid) parallel to the U grid direction.
                        infiniteLines.AddRange(GenerateLines(gridDataU, zLevel, lineLength, lineCount));
                    }
                }

                // ----- V direction (vertical) — use stable reference indices {2, 6} -----
                var refArrayV = GetGridReferences(ceilingData.BottomFaceStableReference, new[] { 2, 6 });
                if (refArrayV.Size >= 2)
                {
                    GridData gridDataV = GetGridDataFromReferences(refArrayV);
                    if (gridDataV != null)
                    {
                        infiniteLines.AddRange(GenerateLines(gridDataV, zLevel, lineLength, lineCount));
                    }
                }

                // If we could not infer any direction/spacing, return an empty list gracefully.
                if (infiniteLines.Count == 0) return gridCurves;

                // Extend each line by a large amount to guarantee intersections with the ceiling solid.
                // (Extend_Paper is a custom extension; 5000 is a practical large length in Revit internal units.)
                var extendedLines = infiniteLines.Select(x => x.Extend_Paper(5000, true));

                // Intersect each extended line with the ceiling solid to obtain clipped segments inside the ceiling.
                SolidCurveIntersectionOptions options = new SolidCurveIntersectionOptions();
                foreach (var line in extendedLines)
                {
                    SolidCurveIntersection intersection = ceilingData.Solid.IntersectWithCurve(line, options);

                    if (intersection == null || intersection.SegmentCount == 0) continue;

                    // Collect valid intersection segments (ignore near-zero length to avoid numerical noise).
                    for (int i = 0; i < intersection.SegmentCount; i++)
                    {
                        Curve segment = intersection.GetCurveSegment(i);
                        if (segment.Length > 1e-6) gridCurves.Add(segment);
                    }
                }

                // Optionally include the bottom boundary of the ceiling.
                if (includeBoundary)
                {
                    gridCurves.AddRange(ceilingData.BottomBoundary);
                }

                return gridCurves;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get ceiling grid lines: {ex.Message}", ex);
            }
#endif
        }

        #region Helper Methods

        /// <summary>
        /// Builds a <see cref="ReferenceArray"/> from a stable face reference using the provided indices.
        /// The indices (e.g., 1/5 for U, 2/6 for V) refer to sub-references encoded in the face's stable representation.
        /// If parsing for a given index fails, it is silently skipped.
        /// </summary>
        private static ReferenceArray GetGridReferences(string stableRef, int[] indices)
        {
            ReferenceArray refArray = [];
            foreach (int index in indices)
            {
                string gridRefString = $"{stableRef}/{index}";
                try
                {
                    Reference gridRef = Reference.ParseFromStableRepresentation(_doc, gridRefString);
                    if (gridRef != null) refArray.Append(gridRef);
                }
                catch
                {
                    // Ignore parse errors: not all ceilings expose all expected sub-references.
                }
            }
            return refArray;
        }

        /// <summary>
        /// Uses a temporary <see cref="Dimension"/> placed on the active view to measure
        /// the spacing (Offset) between two references, then derives:
        /// - Origin: the dimension's origin.
        /// - DimDirection: the direction along which the dimension measures (perpendicular to grid lines).
        /// - GridDirection: perpendicular to DimDirection (actual grid line direction).
        /// - Length: a reasonable base length estimated from the dimension's bounding box.
        ///
        /// The temporary dimension is cleaned up in the finally block to avoid polluting the model.
        /// </summary>
        private static GridData GetGridDataFromReferences(ReferenceArray refs)
        {            
            Dimension dim = null!;
            try
            {
                // Create a simple "dummy" dimension line. Its actual endpoints don't matter
                // as long as Revit can place the dimension referencing the provided refs.
                Line dimLine = Line.CreateBound(XYZ.Zero, new XYZ(10, 0, 0));

                // Place the temporary dimension across the provided references.
                dim = _doc.Create.NewDimension(_view, dimLine, refs);

                // Nudge the element to force a document regenerate, ensuring dimension values are computed (dirty).
                ElementTransformUtils.MoveElement(_doc, dim.Id, new XYZ(0.1, 0, 0));
                _doc.Regenerate();

                // Read measured spacing and orientations.
                double offset = dim.Value!.Value;                // Grid spacing inferred from the two references.
                XYZ origin = dim.Origin;                         // Useful as a central seed point.
                XYZ dimDir = (dim.Curve as Line)!.Direction.Normalize(); // Direction of the dimension (normal to grid lines).
                XYZ gridDir = new XYZ(-dimDir.Y, dimDir.X, 0);   // Rotate 90° in XY to get the grid line direction.

                // Estimate a "reasonable" line length from the dimension's bounding box as a fallback.
                var dimBBox = dim.get_BoundingBox(_view);
                double length = 5.0;
                if (dimBBox != null)
                {
                    length = Math.Max(dimBBox.Max.X - dimBBox.Min.X, dimBBox.Max.Y - dimBBox.Min.Y);
                }

                return new GridData
                {
                    Offset = offset,
                    Origin = origin,
                    DimDirection = dimDir,
                    GridDirection = gridDir,
                    Length = length
                };
            }
            catch (Exception ex)
            {
                // Log and degrade gracefully (the caller will handle null and decide to skip this direction).
                System.Diagnostics.Debug.WriteLine($"Failed to create dimension: {ex.Message}");
                return null;
            }
            finally
            {
                // Always delete the temporary dimension to keep the model clean.
                if (dim != null && _doc.GetElement(dim.Id) != null)
                {
                    _doc.Delete(dim.Id);
                }
            }
        }

        /// <summary>
        /// Generates long, unclipped grid lines based on <see cref="GridData"/>.
        /// Lines are centered around the origin (projected at the given Z), spaced by (i + 0.5) * Offset
        /// on both the negative and positive sides along <see cref="GridData.DimDirection"/>,
        /// and oriented parallel to <see cref="GridData.GridDirection"/>.
        /// These lines are later intersected with the ceiling solid to obtain final clipped segments.
        /// </summary>
        private static List<Line> GenerateLines(GridData data, double zLevel, double lineLength, int lineCount)
        {
            var lines = new List<Line>();
            XYZ origin = new XYZ(data.Origin.X, data.Origin.Y, zLevel);

            // Negative side: place lines every (i + 0.5) * Offset along -DimDirection.
            for (int i = 0; i < lineCount; i++)
            {
                XYZ lineOrigin = origin - (i + 0.5) * data.Offset * data.DimDirection;
                XYZ p1 = lineOrigin - lineLength * data.GridDirection;
                XYZ p2 = lineOrigin + lineLength * data.GridDirection;
                lines.Add(Line.CreateBound(p1, p2));
            }

            // Positive side: mirror the same pattern along +DimDirection.
            for (int i = 0; i < lineCount; i++)
            {
                XYZ lineOrigin = origin + (i + 0.5) * data.Offset * data.DimDirection;
                XYZ p1 = lineOrigin - lineLength * data.GridDirection;
                XYZ p2 = lineOrigin + lineLength * data.GridDirection;
                lines.Add(Line.CreateBound(p1, p2));
            }

            return lines;
        }

        #endregion
    }
}
