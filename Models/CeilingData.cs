using Autodesk.Revit.DB;
using PaperLibrary.Core;
using PaperLibrary.Extensions;

namespace PaperLibrary.Utilities.RevitUtilities.Models
{
    internal class CeilingData
    {
        private Document _doc = ServiceBootstrapper.Revit.Doc;
        public Ceiling Owner { get; private set; }
        public Solid Solid { get; private set; }
        public List<Curve> BottomBoundary { get; private set; } = [];
        public PlanarFace BottomFace { get; private set; }
        public BoundingBoxUV BoundingBoxUVOfBottomFace { get; private set; }
        public BoundingBoxXYZ BoundingBoxXYZ { get; private set; }
        public string BottomFaceStableReference { get; private set; }


        public CeilingData(Ceiling ceiling)
        {
            Owner = ceiling;
            Solid = ceiling.ToSolid_Paper() ?? throw new ArgumentNullException("Ceiling solid was null. Please check.");
            BottomFace = GetBottomFace(ceiling,out Reference bottomFaceRef) ?? throw new ArgumentNullException("Bottom face was not a plannar face or can not get bottom face from this ceiling.");
            BottomBoundary = GetBoundaryOfPlanarFace(BottomFace);
            BoundingBoxUVOfBottomFace = BottomFace.GetBoundingBox();
            BoundingBoxXYZ = ceiling.get_BoundingBox(null);
            BottomFaceStableReference = bottomFaceRef.ConvertToStableRepresentation(_doc);
        }

        private PlanarFace? GetBottomFace(Ceiling ceiling, out Reference bottomFaceRef)
        {
            // Lấy Reference mặt đáy 
            if (!(ceiling is HostObject hostObject)) throw new InvalidOperationException("Ceiling is not a HostObject.");
            bottomFaceRef = HostObjectUtils.GetBottomFaces(hostObject).FirstOrDefault() ?? throw new InvalidOperationException("Could not find bottom face reference."); ;

            var face = ceiling.GetGeometryObjectFromReference(bottomFaceRef) as Face;
            if(face is PlanarFace planarFace) return planarFace;
            return null;
        }

        private List<Curve> GetBoundaryOfPlanarFace(PlanarFace planarFace)
        {
            EdgeArrayArray edgeLoops = planarFace.EdgeLoops;
            if (edgeLoops.Size == 0) return [];
            List<Curve> curves = [];
            foreach (EdgeArray edgeArray in edgeLoops)
            {
                foreach (Edge edge in edgeArray) curves.Add(edge.AsCurve());
            }
            return curves;
        }
    }
}
