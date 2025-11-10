using Autodesk.Revit.DB;

namespace PaperLibrary.Utilities.RevitUtilities.Models
{
    internal class GridData
    {
        public XYZ Origin { get; set; }
        public double Offset { get; set; }
        public XYZ DimDirection { get; set; } // Hướng của Dimension (vuông góc lưới)
        public XYZ GridDirection { get; set; } // Hướng của đường lưới
        public double Length { get; set; } // Chiều dài của lưới
    }
}
